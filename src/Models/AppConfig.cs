namespace StellasoraPotentialOverlay.Models;

/// <summary>
/// アプリケーション設定のデータモデル
/// </summary>
public class AppConfig
{
    /// <summary>対象ウィンドウのタイトル</summary>
    public string TargetWindowTitle { get; set; } = "StellaSora";
    
    /// <summary>キャプチャ間隔 (ミリ秒)</summary>
    public int CaptureIntervalMs { get; set; } = 1000;
    
    /// <summary>左端のX座標（比率: 0.0-1.0）</summary>
    public double LeftX { get; set; } = 0.119;
    
    /// <summary>中心のX座標（比率: 0.0-1.0）</summary>
    public double CenterX { get; set; } = 0.387;
    
    /// <summary>右端のX座標（比率: 0.0-1.0）</summary>
    public double RightX { get; set; } = 0.656;
    
    /// <summary>検索対象の文字リスト</summary>
    public List<CharacterTarget> CharacterTargets { get; set; } = new();
    
    /// <summary>オーバーレイの枠線色 (ARGB)</summary>
    public string BorderColor { get; set; } = "#FF00FF00"; // 緑
    
    /// <summary>オーバーレイの枠線の太さ</summary>
    public int BorderThickness { get; set; } = 2;
    
    /// <summary>オーバーレイのテキスト色 (ARGB)</summary>
    public string TextColor { get; set; } = "#FFFFFFFF"; // 白
    
    /// <summary>オーバーレイのテキストサイズ</summary>
    public int TextSize { get; set; } = 14;
    
    /// <summary>デバッグモード</summary>
    public bool DebugMode { get; set; } = false;
    
    /// <summary>Y座標（比率: 0.0-1.0）</summary>
    public double CommonY { get; set; } = 0.504;
    
    /// <summary>幅（比率: 0.0-1.0）</summary>
    public double CommonWidth { get; set; } = 0.227;
    
    /// <summary>高さ（比率: 0.0-1.0）</summary>
    public double CommonHeight { get; set; } = 0.097;
}

