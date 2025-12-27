using System.IO;
using System.Reflection; // 追加
using System.Text.Json;
using StellasoraPotentialOverlay.Models;

namespace StellasoraPotentialOverlay.Services;

public class MasterDataService
{
    // ファイル名（リソース名の検索に使用）
    private const string JsonFileName = "characters.json";
    
    public List<string> CharacterNames { get; private set; } = new();
    private Dictionary<string, CharacterData> _characterMap = new();

    public bool IsLoaded => _characterMap.Count > 0;

    public void LoadData()
    {
        try
        {
            // 現在実行中のアセンブリ（EXE）を取得
            var assembly = Assembly.GetExecutingAssembly();

            // リソース名を検索（"名前空間.フォルダ.ファイル名" という形式になっているため検索する）
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(str => str.EndsWith(JsonFileName));

            if (string.IsNullOrEmpty(resourceName))
            {
                System.Diagnostics.Debug.WriteLine($"[MasterData] リソース '{JsonFileName}' が見つかりません。");
                return;
            }

            // ストリームを開いて読み込む
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;

            using var reader = new StreamReader(stream);
            var jsonString = reader.ReadToEnd();
            
            // JSONパース処理（以前と同じ）
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("characters", out var charsElement))
            {
                CharacterNames = JsonSerializer.Deserialize<List<string>>(charsElement.GetRawText()) ?? new();
            }

            foreach (var charName in CharacterNames)
            {
                if (root.TryGetProperty(charName, out var charDataElement))
                {
                    var charData = JsonSerializer.Deserialize<CharacterData>(charDataElement.GetRawText());
                    if (charData != null)
                    {
                        _characterMap[charName] = charData;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine("[MasterData] データのロードに成功しました。");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MasterData] Load Error: {ex.Message}");
        }
    }

    /// <summary>
    /// 指定したキャラ・ポジションの素質リストを取得
    /// </summary>
    public List<PotentialViewModel> GetPotentials(string charName, bool isMain)
    {
        // （変更なし）以前のコードのまま
        var list = new List<PotentialViewModel>();
        if (!_characterMap.ContainsKey(charName)) return list;

        var data = _characterMap[charName];
        var category = isMain ? data.Main : data.Support;

        foreach (var p in category.Core)
        {
            list.Add(new PotentialViewModel
            {
                Name = p.Name,
                Rarity = p.Rarity,
                Description = p.Description,
                Category = "コア",
                TargetLevel = "取得のみ"
            });
        }

        foreach (var p in category.Sub)
        {
            list.Add(new PotentialViewModel
            {
                Name = p.Name,
                Rarity = p.Rarity,
                Description = p.Description,
                Category = "サブ",
                TargetLevel = "取得のみ"
            });
        }

        return list;
    }
}