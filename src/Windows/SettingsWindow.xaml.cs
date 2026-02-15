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
