namespace StellasoraPotentialOverlay.Models;

/// <summary>
/// デバッグ用OCR認識結果
/// </summary>
public class OcrDebugResult
{
    /// <summary>認識された文字</summary>
    public string RecognizedText { get; set; } = string.Empty;
    
    /// <summary>認識領域のID</summary>
    public int RegionId { get; set; }
    
    /// <summary>認識領域の名前</summary>
    public string RegionName { get; set; } = string.Empty;
    
    /// <summary>認識位置のX座標</summary>
    public int X { get; set; }
    
    /// <summary>認識位置のY座標</summary>
    public int Y { get; set; }
    
    /// <summary>認識領域の幅</summary>
    public int Width { get; set; }
    
    /// <summary>認識領域の高さ</summary>
    public int Height { get; set; }
}
