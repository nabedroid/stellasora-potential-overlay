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

    public SettingsWindow(ConfigurationService configService, AppConfig config)
    {
        InitializeComponent();
        
        _configService = configService;
        _config = config;

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
            var list = WindowComboBox.ItemsSource as List<string>;
            if (list != null && !list.Contains(_config.TargetWindowTitle))
            {
                // 現在起動していなくても、設定上の名前は維持して表示
                var newList = new List<string>(list) { _config.TargetWindowTitle };
                WindowComboBox.ItemsSource = newList;
            }
            WindowComboBox.SelectedItem = _config.TargetWindowTitle;
        }

        // 認識設定
        IntervalTextBox.Text = _config.CaptureIntervalMs.ToString();
        DebugModeCheckBox.IsChecked = _config.DebugMode;

        // OCR前処理
        UseColorThresholdCheckBox.IsChecked = _config.UseColorThreshold;
        ColorThresholdTextBox.Text = _config.ColorThreshold.ToString();

        // OCR領域共通設定
        CommonYTextBox.Text = _config.CommonY.ToString();
        CommonWidthTextBox.Text = _config.CommonWidth.ToString();
        CommonHeightTextBox.Text = _config.CommonHeight.ToString();
        // 個別のX座標
        LeftXTextBox.Text = _config.LeftX.ToString();
        CenterXTextBox.Text = _config.CenterX.ToString();
        RightXTextBox.Text = _config.RightX.ToString();
        X2LeftXTextBox.Text = _config.X2LeftX.ToString();
        X2RightXTextBox.Text = _config.X2RightX.ToString();
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

            // OCR前処理
            _config.UseColorThreshold = UseColorThresholdCheckBox.IsChecked ?? false;
            if (int.TryParse(ColorThresholdTextBox.Text, out int colorThreshold))
                _config.ColorThreshold = Math.Max(0, Math.Min(255, colorThreshold));
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
            if (double.TryParse(X2LeftXTextBox.Text, out double x2LeftX))
                _config.X2LeftX = x2LeftX;
            if (double.TryParse(X2RightXTextBox.Text, out double x2RightX))
                _config.X2RightX = x2RightX;

            // 保存
            if (_configService.SaveConfig(_config))
            {
                // MessageBox.Show("設定を保存しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
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
