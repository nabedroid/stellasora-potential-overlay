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

    /// <summary>選択中の主力キャラクター</summary>
    public string SelectedMainChar { get; set; } = "";
    
    /// <summary>選択中の支援キャラクター1</summary>
    public string SelectedSupportChar1 { get; set; } = "";
    
    /// <summary>選択中の支援キャラクター2</summary>
    public string SelectedSupportChar2 { get; set; } = "";

    /// <summary>選択中の素質リスト</summary>
    public List<CharacterTarget> SelectedPotentials { get; set; } = new();
}

