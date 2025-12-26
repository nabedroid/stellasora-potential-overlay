using System.Collections.ObjectModel;
using System.Windows;
using StellasoraPotentialOverlay.Models;
using StellasoraPotentialOverlay.Services;

namespace StellasoraPotentialOverlay;

/// <summary>
/// 設定ウィンドウ
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ConfigurationService _configService;
    private AppConfig _config;
    private ObservableCollection<CharacterTarget> _characters;

    public SettingsWindow(ConfigurationService configService, AppConfig config)
    {
        InitializeComponent();
        
        _configService = configService;
        _config = config;
        _characters = new ObservableCollection<CharacterTarget>(config.CharacterTargets);

        LoadSettings();
    }

    /// <summary>
    /// 設定を画面に読み込み
    /// </summary>
    private void LoadSettings()
    {
        // ウィンドウリストを更新
        RefreshWindowList();

        // 対象ウィンドウを選択
        if (!string.IsNullOrEmpty(_config.TargetWindowTitle))
        {
            WindowComboBox.SelectedItem = _config.TargetWindowTitle;
        }

        // 認識設定
        IntervalTextBox.Text = _config.CaptureIntervalMs.ToString();
        DebugModeCheckBox.IsChecked = _config.DebugMode;

        // OCR領域共通設定
        CommonYTextBox.Text = _config.CommonY.ToString();
        CommonWidthTextBox.Text = _config.CommonWidth.ToString();
        CommonHeightTextBox.Text = _config.CommonHeight.ToString();
        // 個別のX座標
        LeftXTextBox.Text = _config.LeftX.ToString();
        CenterXTextBox.Text = _config.CenterX.ToString();
        RightXTextBox.Text = _config.RightX.ToString();

        // 文字検索設定
        CharactersDataGrid.ItemsSource = _characters;
    }

    /// <summary>
    /// ウィンドウリストを更新
    /// </summary>
    private void RefreshWindowList()
    {
        var windows = WindowCaptureService.GetAvailableWindows();
        WindowComboBox.ItemsSource = windows;
    }

    private void RefreshWindows_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindowList();
    }

    /// <summary>
    /// 文字を追加
    /// </summary>
    private void AddCharacter_Click(object sender, RoutedEventArgs e)
    {
        var character = new CharacterTarget
        {
            SearchText = "新しい文字",
            TargetLevel = "取得のみ",
            Memo = "",
            DisplayColor = "#FFFFFF00",
            IsEnabled = true
        };

        _characters.Add(character);
    }

    /// <summary>
    /// 選択した文字を削除
    /// </summary>
    private void RemoveCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (CharactersDataGrid.SelectedItem is CharacterTarget selectedCharacter)
        {
            _characters.Remove(selectedCharacter);
        }
    }

    /// <summary>
    /// 保存
    /// </summary>
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 設定を更新
            _config.TargetWindowTitle = WindowComboBox.SelectedItem?.ToString() ?? "";
            
            if (int.TryParse(IntervalTextBox.Text, out int interval))
                _config.CaptureIntervalMs = Math.Max(100, interval);

            _config.DebugMode = DebugModeCheckBox.IsChecked ?? false;

            // OCR領域共通設定を保存
            if (double.TryParse(CommonYTextBox.Text, out double commonY))
                _config.CommonY = commonY;
            if (double.TryParse(CommonWidthTextBox.Text, out double commonWidth))
                _config.CommonWidth = commonWidth;
            if (double.TryParse(CommonHeightTextBox.Text, out double commonHeight))
                _config.CommonHeight = commonHeight;
            if (double.TryParse(LeftXTextBox.Text, out double leftX))
                _config.LeftX = leftX;
            if (double.TryParse(CenterXTextBox.Text, out double centerX))
                _config.CenterX = centerX;
            if (double.TryParse(RightXTextBox.Text, out double rightX))
                _config.RightX = rightX;

            // 文字検索設定を保存
            _config.CharacterTargets = _characters.ToList();

            // 保存
            if (_configService.SaveConfig(_config))
            {
                MessageBox.Show("設定を保存しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("設定の保存に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// キャンセル
    /// </summary>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
