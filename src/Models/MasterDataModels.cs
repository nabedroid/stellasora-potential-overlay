using System.Text.Json.Serialization;
using System.Windows.Media;

namespace StellasoraPotentialOverlay.Models;

// characters.json の構造に対応するクラス群

public class PotentialItem {
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = ""; // "ピンク", "虹", "金"

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

public class PotentialCategory {
    [JsonPropertyName("core")]
    public List<PotentialItem> Core { get; set; } = new();

    [JsonPropertyName("sub")]
    public List<PotentialItem> Sub { get; set; } = new();
}

public class CharacterData {
    [JsonPropertyName("main")]
    public PotentialCategory Main { get; set; } = new();

    [JsonPropertyName("support")]
    public PotentialCategory Support { get; set; } = new();
}

// 画面表示用のラッパークラス
public class PotentialViewModel {
    public string Name { get; set; } = "";
    public string Rarity { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    
    // ユーザー編集用
    public bool IsSelected { get; set; }
    public string TargetLevel { get; set; } = "取得のみ";
    public string Memo { get; set; } = "";
    
    // 表示用プロパティ
    public string DisplayColor {
        get {
            return Rarity switch {
                "ピンク" => "#ff49c3",
                "虹" => "#8057ff",
                "金" => "#de8a0b",
                _ => "#FFFFFFFF"
            };
        }
    }

    // バインディング用のBrushプロパティ（追加）
    [JsonIgnore]
    public Brush DisplayBrush {
        get {
            try {
                return (SolidColorBrush)(new BrushConverter().ConvertFrom(DisplayColor) ?? Brushes.Transparent);
            }
            catch {
                return Brushes.Transparent;
            }
        }
    }
}