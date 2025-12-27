using System.Windows;
using System.Windows.Media;
using StellasoraPotentialOverlay.Models;
using StellasoraPotentialOverlay.Services;

namespace StellasoraPotentialOverlay;

public partial class SetupWindow : Window
{
    public List<CharacterTarget> GeneratedTargets { get; private set; } = new();

    // UIバインディング用プロパティ（保存時に参照）
    public string SelectedMain { get; private set; } = "";
    public string SelectedSup1 { get; private set; } = "";
    public string SelectedSup2 { get; private set; } = "";

    public SetupWindow(MasterDataService masterDataService, AppConfig config)
    {
        InitializeComponent();

        // データのロード
        if (!masterDataService.IsLoaded)
        {
            masterDataService.LoadData();
        }

        // 各コントロールの初期化 (タイトル, 色, データ, 設定, 主力かどうか)
        MainControl.Setup("【主力】", Colors.Red, masterDataService, config, true);
        Sup1Control.Setup("【支援1】", Colors.Blue, masterDataService, config, false);
        Sup2Control.Setup("【支援2】", Colors.Blue, masterDataService, config, false);

        // 前回選択状態の復元
        MainControl.SetSelectedCharacter(config.SelectedMainChar);
        Sup1Control.SetSelectedCharacter(config.SelectedSupportChar1);
        Sup2Control.SetSelectedCharacter(config.SelectedSupportChar2);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // 1. 選択されたキャラ名を取得
        SelectedMain = MainControl.GetSelectedCharacter();
        SelectedSup1 = Sup1Control.GetSelectedCharacter();
        SelectedSup2 = Sup2Control.GetSelectedCharacter();

        // 2. 選択された素質をすべて回収
        GeneratedTargets.Clear();
        GeneratedTargets.AddRange(MainControl.GetSelectedTargets());
        GeneratedTargets.AddRange(Sup1Control.GetSelectedTargets());
        GeneratedTargets.AddRange(Sup2Control.GetSelectedTargets());

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}