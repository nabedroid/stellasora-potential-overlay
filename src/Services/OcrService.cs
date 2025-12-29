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

        int windowWidth = capturedImage.Width;
        int windowHeight = capturedImage.Height;
        List<double> xs = new List<double> { config.LeftX, config.CenterX, config.RightX, config.X2LeftX, config.X2RightX };

        foreach (var x in xs) {
            try {
                // 1. 広めの領域を切り出し
                var rect = CalculateRect(x, config.CommonY, config.CommonWidth, config.CommonHeight, windowWidth, windowHeight);
                if (rect.IsEmpty) continue;

                // 2. 方針B: 特定色抽出フィルタ + 拡大
                using var processedBitmap = config.UseColorThreshold 
                    ? PreprocessImageWithColorFilter(capturedImage, rect, config.ColorThreshold) 
                    : capturedImage.Clone(rect, capturedImage.PixelFormat);
                
                // 3. OCR実行
                var ocrResult = await RecognizeBitmapAsync(processedBitmap);
                if (ocrResult == null || ocrResult.Lines.Count == 0) continue;

                // 4. 行フィルタリング (一番上の行だけを採用)
                // OcrLine自体には座標がないため、その行に含まれる単語(Words)の座標を参照する
                var firstLine = ocrResult.Lines
                    .Where(l => l.Words.Count > 0)
                    .OrderBy(l => l.Words[0].BoundingRect.Y) // Y座標順（上から下へ）
                    .FirstOrDefault();

                if (firstLine != null && !string.IsNullOrWhiteSpace(firstLine.Text)) {
                    // マッチング
                    var matchedTarget = FindMatchingTarget(firstLine.Text, config.CharacterTargets);
                    if (matchedTarget != null) {
                        results.Add(new OcrDetectionResult
                        {
                            RecognizedText = firstLine.Text,
                            MatchedTarget = matchedTarget,
                            X = rect.X,
                            Y = rect.Y,
                            Width = rect.Width,
                            Height = rect.Height,
                            Confidence = 1.0
                        });
                    }
                }
            }
            catch {
                continue;
            }
        }

        return results;
    }

    // デバッグ用
    public async Task<List<OcrDebugResult>> RecognizeTextDebugAsync(Bitmap capturedImage, AppConfig config)
    {
        var results = new List<OcrDebugResult>();
        if (!_isInitialized || _ocrEngine == null || capturedImage == null) return results;

        int windowWidth = capturedImage.Width;
        int windowHeight = capturedImage.Height;
        List<double> xs = new List<double> { config.LeftX, config.CenterX, config.RightX, config.X2LeftX, config.X2RightX };
        int regionId = 0;

        foreach (var x in xs) {
            regionId++;
            var rect = CalculateRect(x, config.CommonY, config.CommonWidth, config.CommonHeight, windowWidth, windowHeight);
            if (rect.IsEmpty) continue;

            using var processedBitmap = config.UseColorThreshold 
                ? PreprocessImageWithColorFilter(capturedImage, rect, config.ColorThreshold) 
                : capturedImage.Clone(rect, capturedImage.PixelFormat);
            var ocrResult = await RecognizeBitmapAsync(processedBitmap);

            string displayText = "[認識なし]";
            
            if (ocrResult != null && ocrResult.Lines.Count > 0) {
                // Y座標順にソートしてデバッグ表示
                var lines = ocrResult.Lines
                    .Where(l => l.Words.Count > 0)
                    .OrderBy(l => l.Words[0].BoundingRect.Y)
                    .ToList();

                if (lines.Count > 0) {
                    var firstLine = lines[0].Text;
                    var otherLines = string.Join(", ", lines.Skip(1).Select(l => l.Text));
                    
                    displayText = $"採用: {firstLine}";
                    if (!string.IsNullOrEmpty(otherLines)) {
                        displayText += $"\n(無視: {otherLines})";
                    }
                }
            }

            results.Add(new OcrDebugResult {
                RecognizedText = displayText,
                RegionId = regionId,
                RegionName = $"Region {regionId}",
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height
            });
        }
        return results;
    }

    /// <summary>
    /// 方針Bの実装: 特定色抽出フィルタ
    /// </summary>
    private Bitmap PreprocessImageWithColorFilter(Bitmap source, Rectangle rect, int threshold = 100)
    {
        // 1. 切り出し
        using var region = source.Clone(rect, source.PixelFormat);

        // 2. 拡大 (OCR精度向上用、3倍程度)
        int scale = 3;
        var dest = new Bitmap(region.Width * scale, region.Height * scale, PixelFormat.Format32bppArgb);
        
        using (var g = Graphics.FromImage(dest)) {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(region, 0, 0, dest.Width, dest.Height);
        }

        // 3. ピクセル単位のフィルタ処理
        var rectData = new Rectangle(0, 0, dest.Width, dest.Height);
        var data = dest.LockBits(rectData, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

        unsafe {
            byte* ptr = (byte*)data.Scan0;
            int totalBytes = data.Stride * dest.Height;
            int bytesPerPixel = 4; // ARGB

            for (int i = 0; i < totalBytes; i += bytesPerPixel) {
                // B, G, R の順
                byte b = ptr[i];
                byte g = ptr[i + 1];
                byte r = ptr[i + 2];

                // R, G, B すべてが閾値より低い（＝暗い色）なら文字として残す（黒にする）
                // それ以外は白にする
                bool isDarkText = (r < threshold && g < threshold && b < threshold);

                if (isDarkText) {
                    ptr[i] = 0;     // B
                    ptr[i + 1] = 0; // G
                    ptr[i + 2] = 0; // R
                }
                else {
                    ptr[i] = 255;     // B
                    ptr[i + 1] = 255; // G
                    ptr[i + 2] = 255; // R
                }
                ptr[i + 3] = 255; // Alpha
            }
        }
        
        dest.UnlockBits(data);
        return dest;
    }

    private Rectangle CalculateRect(double xRatio, double yRatio, double wRatio, double hRatio, int totalW, int totalH) {
        int x = (int)(xRatio * totalW);
        int y = (int)(yRatio * totalH);
        int w = (int)(wRatio * totalW);
        int h = (int)(hRatio * totalH);

        if (x < 0) x = 0;
        if (y < 0) y = 0;
        if (x + w > totalW) w = totalW - x;
        if (y + h > totalH) h = totalH - y;

        return new Rectangle(x, y, w, h);
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
    private CharacterTarget? FindMatchingTarget(string recognizedText, List<CharacterTarget> targets, bool useFuzzySearch = true) {
        // ノイズ除去
        string cleanedRecognizedText = Regex.Replace(recognizedText, @"[ 　.,'’]", "");
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
            // そのまま比較
            if (cleanedRecognizedText.Contains(target.SearchText)) return target;
            // ぱ（パ）行、ば（バ）行をは（ハ）行に寄せて比較
            if (useFuzzySearch) {
                if (normalizedRecognizedText.Contains(Normalize(target.SearchText))) return target;
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