using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using StellasoraPotentialOverlay.Models;
using StellasoraPotentialOverlay.Services;

namespace StellasoraPotentialOverlay;

/// <summary>
/// キャラクター選択と素質リストのセットを表示する部品
/// </summary>
public partial class CharacterSelectionControl : UserControl
{
    private MasterDataService? _masterDataService;
    private AppConfig? _config;
    private bool _isMainPosition; // 主力かどうか
    private ObservableCollection<PotentialViewModel> _items = new();

    public CharacterSelectionControl()
    {
        InitializeComponent();
        PotentialsGrid.ItemsSource = _items;
    }

    /// <summary>
    /// 初期設定（親ウィンドウから呼び出す）
    /// </summary>
    public void Setup(string title, Color titleColor, MasterDataService masterData, AppConfig config, bool isMain)
    {
        // タイトルと色の設定
        HeaderTitle.Text = title;
        HeaderTitle.Foreground = new SolidColorBrush(titleColor);

        // 依存サービスの注入
        _masterDataService = masterData;
        _config = config;
        _isMainPosition = isMain;

        // キャラクターリストの設定
        CharCombo.ItemsSource = _masterDataService.CharacterNames;
    }

    /// <summary>
    /// 選択中のキャラクターを設定（復元用）
    /// </summary>
    public void SetSelectedCharacter(string charName)
    {
        if (CharCombo.ItemsSource is List<string> list && list.Contains(charName))
        {
            CharCombo.SelectedItem = charName;
        }
    }

    /// <summary>
    /// 現在選択されているキャラクター名を取得
    /// </summary>
    public string GetSelectedCharacter()
    {
        return CharCombo.SelectedItem as string ?? "";
    }

    /// <summary>
    /// チェックが入っている素質ターゲットを取得
    /// </summary>
    public List<CharacterTarget> GetSelectedTargets()
    {
        var result = new List<CharacterTarget>();
        foreach (var item in _items)
        {
            if (item.IsSelected)
            {
                result.Add(new CharacterTarget
                {
                    SearchText = item.Name,
                    TargetLevel = item.TargetLevel,
                    Memo = item.Memo,
                    DisplayColor = item.DisplayColor,
                    IsEnabled = true
                });
            }
        }
        return result;
    }

    // コンボボックス変更時の処理
    private void CharCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_masterDataService == null || _config == null) return;

        var charName = CharCombo.SelectedItem as string;
        _items.Clear();

        if (string.IsNullOrEmpty(charName)) return;

        var potentials = _masterDataService.GetPotentials(charName, _isMainPosition);
        foreach (var p in potentials)
        {
            // 既存設定の引き継ぎ
            var existing = _config.CharacterTargets.FirstOrDefault(t => t.SearchText == p.Name);
            if (existing != null)
            {
                p.IsSelected = existing.IsEnabled;
                p.TargetLevel = existing.TargetLevel;
                p.Memo = existing.Memo;
            }
            _items.Add(p);
        }
    }
}