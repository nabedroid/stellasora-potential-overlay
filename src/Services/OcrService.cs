using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using StellasoraPotentialOverlay.Models;

namespace StellasoraPotentialOverlay.Services;

/// <summary>
/// OCRサービス (Windows.Media.Ocr API使用)
/// </summary>
public class OcrService
{
    private OcrEngine? _ocrEngine;
    private bool _isInitialized = false;

    /// <summary>
    /// OCRエンジンを初期化
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[OCR] 初期化開始...");
            var language = new Windows.Globalization.Language("ja");
            _ocrEngine = OcrEngine.TryCreateFromLanguage(language);
            
            if (_ocrEngine == null)
            {
                System.Diagnostics.Debug.WriteLine("[OCR] エラー: OCRエンジンの初期化に失敗 - 日本語言語パックが見つかりません");
                return false;
            }

            _isInitialized = true;
            System.Diagnostics.Debug.WriteLine($"[OCR] 初期化成功: 言語={language.DisplayName}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OCR] 初期化エラー: {ex.Message}");
            _isInitialized = false;
            return false;
        }
    }

    /// <summary>
    /// 指定された領域からOCR認識を実行
    /// </summary>
    public async Task<List<OcrDetectionResult>> RecognizeTextAsync(Bitmap capturedImage, AppConfig config) {
        var results = new List<OcrDetectionResult>();

        if (!_isInitialized || _ocrEngine == null || capturedImage == null)
            return results;

        int windowWidth = capturedImage.Width;
        int windowHeight = capturedImage.Height;
        List<double> xs = new List<double> { config.LeftX, config.CenterX, config.RightX };

        foreach (var x in xs)
        {
            try
            {
                // 領域を切り出し
                var rect = new System.Drawing.Rectangle(
                    (int)(x * windowWidth),
                    (int)(config.CommonY * windowHeight),
                    (int)(config.CommonWidth * windowWidth),
                    (int)(config.CommonHeight * windowHeight)
                );
                    
                // 範囲チェック
                if (rect.X < 0 || rect.Y < 0 || 
                    rect.X + rect.Width > windowWidth || 
                    rect.Y + rect.Height > windowHeight)
                    continue;

                using var regionBitmap = capturedImage.Clone(rect, capturedImage.PixelFormat);
                
                // OCR実行
                var recognizedText = await RecognizeFromBitmapAsync(regionBitmap);
                
                if (string.IsNullOrWhiteSpace(recognizedText))
                    continue;

                // 検索対象とマッチング
                var matchedTarget = FindMatchingTarget(recognizedText, config.CharacterTargets);
                
                if (matchedTarget != null)
                {
                    results.Add(new OcrDetectionResult
                    {
                        RecognizedText = recognizedText,
                        MatchedTarget = matchedTarget,
                        X = rect.X,
                        Y = rect.Y,
                        Width = rect.Width,
                        Height = rect.Height,
                        Confidence = 1.0
                    });
                }
            }
            catch
            {
                continue;
            }
        }
        

        return results;
    }

    /// <summary>
    /// 渡されたBitmap全体を解析（上下に分割して精度向上）
    /// </summary>
    private async Task<string> RecognizeFromBitmapAsync(Bitmap bitmap)
    {
        if (_ocrEngine == null) return string.Empty;

        // 上下に分ける境界線
        int midY = bitmap.Height / 2;
        Rectangle upperRect = new Rectangle(0, 0, bitmap.Width, midY);
        Rectangle lowerRect = new Rectangle(0, midY, bitmap.Width, bitmap.Height - midY);

        // 同じ閾値で両方の領域を処理
        int threshold = 192;
        
        string upperText = await ProcessRegionAsync(bitmap, upperRect, threshold);
        string lowerText = await ProcessRegionAsync(bitmap, lowerRect, threshold);

        var result = upperText + lowerText;
        System.Diagnostics.Debug.WriteLine($"[OCR] 最終結合結果: [{result}]");
        return result;
    }

    /// <summary>
    /// 特定の矩形領域を切り出し、加工してOCRを実行する
    /// </summary>
    private async Task<string> ProcessRegionAsync(Bitmap original, Rectangle rect, int threshold)
    {
        Bitmap? regionBmp = null;
        Bitmap? scaledBmp = null;
        Bitmap? binarizedBmp = null;

        try
        {
            // 1. 領域切り出し
            regionBmp = original.Clone(rect, original.PixelFormat);

            // 2. 拡大処理（OCRは文字サイズがある程度大きくないと反応しません）
            int scale = 3;
            scaledBmp = new Bitmap(regionBmp.Width * scale, regionBmp.Height * scale);
            using (var g = Graphics.FromImage(scaledBmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                g.DrawImage(regionBmp, 0, 0, scaledBmp.Width, scaledBmp.Height);
            }

            // 3. 二値化（ApplyBinarization は既存のものを使用）
            binarizedBmp = ApplyBinarization(scaledBmp, threshold);

            // 4. SoftwareBitmapに変換してOCR実行
            using var memoryStream = new System.IO.MemoryStream();
            regionBmp.Save(memoryStream, ImageFormat.Bmp);
            memoryStream.Position = 0;

            using var randomAccessStream = new InMemoryRandomAccessStream();
            await randomAccessStream.WriteAsync(memoryStream.ToArray().AsBuffer());
            
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);
            return string.Join("", ocrResult.Lines.Select(line => line.Text));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OCR] 領域処理エラー: {ex.Message}");
            return string.Empty;
        }
        finally
        {
            // 確実にメモリを解放
            regionBmp?.Dispose();
            scaledBmp?.Dispose();
            binarizedBmp?.Dispose();
        }
    }

    /// <summary>
    /// デバッグ用: 指定された領域からOCR認識を実行（マッチング不要）
    /// </summary>
    public async Task<List<OcrDebugResult>> RecognizeTextDebugAsync(Bitmap capturedImage, AppConfig config) {
        var results = new List<OcrDebugResult>();

        if (!_isInitialized || _ocrEngine == null || capturedImage == null)
            return results;

        int windowWidth = capturedImage.Width;
        int windowHeight = capturedImage.Height;
        List<double> xs = new List<double> { config.LeftX, config.CenterX, config.RightX };

        foreach (var x in xs)
        {
            try
            {
                // 領域を切り出し
                var rect = new System.Drawing.Rectangle(
                    (int)(x * windowWidth),
                    (int)(config.CommonY * windowHeight),
                    (int)(config.CommonWidth * windowWidth),
                    (int)(config.CommonHeight * windowHeight)
                );
                
                // 範囲チェック
                if (rect.X < 0 || rect.Y < 0 || 
                    rect.X + rect.Width > windowWidth || 
                    rect.Y + rect.Height > windowHeight)
                    continue;

                using var regionBitmap = capturedImage.Clone(rect, capturedImage.PixelFormat);
                
                // OCR実行
                var recognizedText = await RecognizeFromBitmapAsync(regionBitmap);

                results.Add(new OcrDebugResult
                {
                    RecognizedText = recognizedText,
                    X = rect.X,
                    Y = rect.Y,
                    Width = rect.Width,
                    Height = rect.Height
                });
            }
            catch
            {
                continue;
            }
        }

        return results;
    }

    /// <summary>
    /// 高速な二値化処理（エフェクト対策）
    /// </summary>
    private Bitmap ApplyBinarization(Bitmap source, int threshold = 192)
    {
        var dest = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        
        var rect = new Rectangle(0, 0, source.Width, source.Height);
        var srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var destData = dest.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* srcPtr = (byte*)srcData.Scan0;
            byte* destPtr = (byte*)destData.Scan0;
            int pixelCount = source.Width * source.Height;

            for (int i = 0; i < pixelCount; i++)
            {
                int baseIdx = i * 4;
                byte b = srcPtr[baseIdx];
                byte g = srcPtr[baseIdx + 1];
                byte r = srcPtr[baseIdx + 2];

                // 輝度の計算 (Rec.601)
                int gray = (int)(0.299 * r + 0.587 * g + 0.114 * b);

                // エフェクト（薄い白）対策：
                // 閾値より「暗い」部分を黒（文字）とし、それ以外を白（背景）にする
                // 文字が黒系・背景が白系の場合、thresholdを低め（100前後）にするとエフェクトが消えます
                byte result = (gray < threshold) ? (byte)0 : (byte)255;

                destPtr[baseIdx] = result;     // B
                destPtr[baseIdx + 1] = result; // G
                destPtr[baseIdx + 2] = result; // R
                destPtr[baseIdx + 3] = 255;    // A
            }
        }

        source.UnlockBits(srcData);
        dest.UnlockBits(destData);
        return dest;
    }

    /// <summary>
    /// 認識された文字と検索対象をマッチング
    /// </summary>
    private CharacterTarget? FindMatchingTarget(string recognizedText, List<CharacterTarget> targets)
    {
        // 空白を除去して比較
        var cleanedText = recognizedText.Replace(" ", "").Replace("　", "");

        foreach (var target in targets.Where(t => t.IsEnabled))
        {
            var cleanedSearchText = target.SearchText.Replace(" ", "").Replace("　", "");
            
            // 部分一致で検索
            if (cleanedText.Contains(cleanedSearchText))
            {
                return target;
            }
        }

        return null;
    }

    /// <summary>
    /// リソースの解放
    /// </summary>
    public void Dispose()
    {
        _ocrEngine = null;
        _isInitialized = false;
    }
}
