using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using StellasoraPotentialOverlay.Models;

namespace StellasoraPotentialOverlay.Services;

public class OcrService : IDisposable {
    private OcrEngine? _ocrEngine;
    private bool _isInitialized = false;

    public async Task<bool> InitializeAsync() {
        return await Task.Run(() => {
            try {
                System.Diagnostics.Debug.WriteLine("[OCR] 初期化開始...");
                var language = new Windows.Globalization.Language("ja");
                if (!OcrEngine.IsLanguageSupported(language)) {
                    _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                }
                else {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(language);
                }

                if (_ocrEngine == null) return false;

                _isInitialized = true;
                return true;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[OCR] 初期化エラー: {ex.Message}");
                return false;
            }
        });
    }

    public async Task<List<OcrDetectionResult>> RecognizeTextAsync(Bitmap capturedImage, AppConfig config) {
        var results = new List<OcrDetectionResult>();

        if (!_isInitialized || _ocrEngine == null || capturedImage == null)
            return results;

        try {
            // 1. OCR実行
            var ocrResult = await RecognizeBitmapAsync(capturedImage);

            if (ocrResult != null && ocrResult.Lines.Count > 0) {
                foreach (var line in ocrResult.Lines) {
                    if (string.IsNullOrWhiteSpace(line.Text)) continue;

                    // マッチング
                    var matchedTarget = FindMatchingTarget(line.Text, config.CharacterTargets);
                    if (matchedTarget != null) {
                        var boundingRect = GetLineBoundingRect(line);
                        
                        results.Add(new OcrDetectionResult
                        {
                            RecognizedText = line.Text,
                            MatchedTarget = matchedTarget,
                            X = (int)boundingRect.X,
                            Y = (int)boundingRect.Y,
                            Width = (int)boundingRect.Width,
                            Height = (int)boundingRect.Height,
                            Confidence = 1.0
                        });
                    }
                }
            }
        }
        catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine($"[OCR Error] {ex.Message}");
        }

        return results;
    }

    // デバッグ用
    public async Task<List<OcrDebugResult>> RecognizeTextDebugAsync(Bitmap capturedImage, AppConfig config)
    {
        var results = new List<OcrDebugResult>();
        if (!_isInitialized || _ocrEngine == null || capturedImage == null) return results;

        try {
            // 1. OCR実行
            var ocrResult = await RecognizeBitmapAsync(capturedImage);

            if (ocrResult != null) {
                foreach (var line in ocrResult.Lines) {
                    var text = line.Text;
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var boundingRect = GetLineBoundingRect(line);

                    results.Add(new OcrDebugResult {
                        RecognizedText = text,
                        RegionId = 0, // 全体なので0
                        RegionName = "Text",
                        X = (int)boundingRect.X,
                        Y = (int)boundingRect.Y,
                        Width = (int)boundingRect.Width,
                        Height = (int)boundingRect.Height
                    });
                }
            }
        }
        catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine($"[OCR Debug Error] {ex.Message}");
        }
        
        return results;
    }

    private Windows.Foundation.Rect GetLineBoundingRect(OcrLine line) {
        // 行全体のBoundingRectを計算（単語の結合）
        if (line.Words.Count == 0) return new Windows.Foundation.Rect();
        
        double x = line.Words.Min(w => w.BoundingRect.X);
        double y = line.Words.Min(w => w.BoundingRect.Y);
        double r = line.Words.Max(w => w.BoundingRect.Right);
        double b = line.Words.Max(w => w.BoundingRect.Bottom);
        
        return new Windows.Foundation.Rect(x, y, r - x, b - y);
    }

    private async Task<OcrResult?> RecognizeBitmapAsync(Bitmap bitmap) {
        if (_ocrEngine == null) return null;

        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Bmp);
        memoryStream.Position = 0;

        using var randomAccessStream = new InMemoryRandomAccessStream();
        await randomAccessStream.WriteAsync(memoryStream.ToArray().AsBuffer());

        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        return await _ocrEngine.RecognizeAsync(softwareBitmap);
    }

    /// <summary>
    /// OCR結果のテキストと一致する素質を返す
    /// </summary>
    // 削除対象の記号リスト
    // ユーザーが見やすく修正しやすいように文字列連結で定義
    private static readonly string IgnoreCharacters = 
        " " + "　" +             // 空白
        ".,、。" +               // 句読点
        "()[]{}（）【】「」" +   // 括弧
        "!?！？" +               // 感嘆符・疑問符
        ":;：；" +               // コロン類
        "'\"’" + "”" +           // 引用符
        "-‐−~～";               // その他記号（長音記号「ー」は含まない）

    /// <summary>
    /// ノイズ（削除対象記号）を除去する
    /// </summary>
    private string RemoveNoise(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        // 削除対象の文字が含まれていたら削除
        // パフォーマンスより可読性と保守性を優先
        var sb = new System.Text.StringBuilder();
        foreach (char c in text)
        {
            if (IgnoreCharacters.IndexOf(c) == -1)
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// OCR結果のテキストと一致する素質を返す
    /// </summary>
    private CharacterTarget? FindMatchingTarget(string recognizedText, List<CharacterTarget> targets, bool useFuzzySearch = true) {
        // ノイズ除去（OCR結果）
        string cleanedRecognizedText = RemoveNoise(recognizedText);

        // ぱ（パ）行、ば（バ）行をは（ハ）行に寄せる
        var diacriticMap = new Dictionary<char, char> {
            {'ば','は'}, {'ぱ','は'}, {'バ','ハ'}, {'パ','ハ'},
            {'び','ひ'}, {'ぴ','ひ'}, {'ビ','ヒ'}, {'ピ','ヒ'},
            {'ぶ','ふ'}, {'ぷ','ふ'}, {'ブ','フ'}, {'プ','フ'},
            {'べ','へ'}, {'ぺ','へ'}, {'ベ','ヘ'}, {'ペ','ヘ'},
            {'ぼ','ほ'}, {'ぽ','ほ'}, {'ボ','ホ'}, {'ポ','ホ'}
        };
        string Normalize(string text) => string.Concat(text.Select(c => diacriticMap.GetValueOrDefault(c, c)));
        string normalizedRecognizedText = Normalize(cleanedRecognizedText);

        // 有効にチェックが入っている素質を検索
        foreach (var target in targets.Where(t => t.IsEnabled)) {
            // 空文字の場合はスキップ
            if (string.IsNullOrEmpty(target.SearchText)) continue;

            // マッチング対象（ターゲット）もノイズ除去
            string cleanedTargetText = RemoveNoise(target.SearchText);
            if (string.IsNullOrEmpty(cleanedTargetText)) continue; // ノイズ除去により空になった場合はスキップ

            // そのまま比較
            if (cleanedRecognizedText.Contains(cleanedTargetText)) return target;

            // ぱ（パ）行、ば（バ）行をは（ハ）行に寄せて比較
            if (useFuzzySearch) {
                if (normalizedRecognizedText.Contains(Normalize(cleanedTargetText))) return target;
            }
        }
        return null;
    }

    public void Dispose() {
        _ocrEngine = null;
        _isInitialized = false;
        GC.SuppressFinalize(this);
    }
}