namespace StellasoraPotentialOverlay.Models;

/// <summary>
/// 検索対象の文字設定
/// </summary>
public class CharacterTarget
{
    /// <summary>ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>検索する文字</summary>
    public string SearchText { get; set; } = string.Empty;
    
    /// <summary>目標レベル</summary>
    public string TargetLevel { get; set; } = "取得のみ";
    
    /// <summary>メモ</summary>
    public string Memo { get; set; } = string.Empty;
    
    /// <summary>表示色 (ARGB)</summary>
    public string DisplayColor { get; set; } = "#FFFFFF00"; // 黄色
    
    /// <summary>有効/無効</summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 利用可能な目標レベルのリスト
    /// </summary>
    public static List<string> AvailableTargetLevels => new()
    {
        "取得のみ",
        "レベル1",
        "レベル1～",
        "レベル6"
    };
}
