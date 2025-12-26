namespace StellasoraPotentialOverlay.Models;

/// <summary>
/// OCR検出結果のデータモデル
/// </summary>
public class OcrDetectionResult
{
    /// <summary>認識された文字</summary>
    public string RecognizedText { get; set; } = string.Empty;
    
    /// <summary>マッチした検索対象</summary>
    public CharacterTarget? MatchedTarget { get; set; }
    
    /// <summary>認識位置のX座標</summary>
    public int X { get; set; }
    
    /// <summary>認識位置のY座標</summary>
    public int Y { get; set; }
    
    /// <summary>認識領域の幅</summary>
    public int Width { get; set; }
    
    /// <summary>認識領域の高さ</summary>
    public int Height { get; set; }
    
    /// <summary>OCR信頼度 (0.0 - 1.0)</summary>
    public double Confidence { get; set; }
}
