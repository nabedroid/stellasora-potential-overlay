using System.Windows;
using System.Windows.Threading;
using StellasoraPotentialOverlay.Models;
using StellasoraPotentialOverlay.Services;

namespace StellasoraPotentialOverlay;

/// <summary>
/// メインウィンドウ
/// </summary>
public partial class MainWindow : Window
{
    private readonly ConfigurationService _configService;
    private readonly WindowCaptureService _captureService;
    private readonly OcrService _ocrService;
    private readonly OverlayWindow _overlayWindow;
    private readonly DispatcherTimer _captureTimer;

    private AppConfig _config;
    private bool _isRunning = false;

    public MainWindow()
    {
        InitializeComponent();

        // サービスの初期化
        _configService = new ConfigurationService();
        _captureService = new WindowCaptureService();
        _ocrService = new OcrService();
        _overlayWindow = new OverlayWindow();

        // 設定の読み込み
        _config = _configService.LoadConfig();

        // OCRエンジンの初期化
        InitializeOcrAsync();

        // タイマーの初期化
        _captureTimer = new DispatcherTimer();
        _captureTimer.Tick += CaptureTimer_Tick;

        // 初期表示
        UpdateStatus();

        // ウィンドウを閉じる時の処理
        Closing += MainWindow_Closing;
    }

    /// <summary>
    /// OCRエンジンを初期化
    /// </summary>
    private async void InitializeOcrAsync()
    {
        Console.WriteLine("[MainWindow] OCR初期化を開始します...");
        var result = await _ocrService.InitializeAsync();
        if (!result)
        {
            Console.WriteLine("[MainWindow] OCR初期化に失敗しました");
            MessageBox.Show("OCRエンジンの初期化に失敗しました。\n日本語言語パックがインストールされているか確認してください。",
                "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            Console.WriteLine("[MainWindow] OCR初期化に成功しました");
        }
    }

    /// <summary>
    /// ステータス表示を更新
    /// </summary>
    private void UpdateStatus()
    {
        TargetWindowTextBlock.Text = string.IsNullOrEmpty(_config.TargetWindowTitle) 
            ? "未設定" 
            : _config.TargetWindowTitle;
    }

    /// <summary>
    /// 開始ボタン
    /// </summary>
    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_config.TargetWindowTitle))
        {
            MessageBox.Show("対象ウィンドウが設定されていません。\n設定画面から対象ウィンドウを選択してください。", 
                "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_config.CharacterTargets.Count == 0 || !_config.CharacterTargets.Any(t => t.IsEnabled))
        {
            MessageBox.Show("検索対象の文字が設定されていません。\n設定画面から検索文字を追加してください。",
                "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 対象ウィンドウを設定
        if (!_captureService.SetTargetWindow(_config.TargetWindowTitle))
        {
            MessageBox.Show($"ウィンドウ '{_config.TargetWindowTitle}' が見つかりません。", 
                "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // オーバーレイのスタイルを設定
        _overlayWindow.SetStyle(
            _config.BorderColor,
            _config.BorderThickness,
            _config.TextColor,
            _config.TextSize
        );

        // オーバーレイを表示
        _overlayWindow.Show();

        // タイマー開始
        _captureTimer.Interval = TimeSpan.FromMilliseconds(_config.CaptureIntervalMs);
        _captureTimer.Start();

        _isRunning = true;
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusTextBlock.Text = "認識中";
        StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
    }

    /// <summary>
    /// 停止ボタン
    /// </summary>
    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopCapture();
    }

    /// <summary>
    /// キャプチャを停止
    /// </summary>
    private void StopCapture()
    {
        _captureTimer.Stop();
        _overlayWindow.Hide();
        _overlayWindow.ClearOverlay();

        _isRunning = false;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        StatusTextBlock.Text = "停止中";
        StatusTextBlock.Foreground = System.Windows.Media.Brushes.Gray;
    }

    /// <summary>
    /// 設定ボタン
    /// </summary>
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            MessageBox.Show("認識を停止してから設定を変更してください。", 
                "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var settingsWindow = new SettingsWindow(_configService, _config);
        if (settingsWindow.ShowDialog() == true)
        {
            // 設定を再読み込み
            _config = _configService.LoadConfig();
            
            // OCRサービスを再初期化
            InitializeOcrAsync();
            
            UpdateStatus();
        }
    }

    /// <summary>
    /// キャプチャタイマーのイベント
    /// </summary>
    private async void CaptureTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            // ウィンドウが有効かチェック
            if (!_captureService.IsWindowValid())
            {
                MessageBox.Show("対象ウィンドウが見つかりません。認識を停止します。", 
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                StopCapture();
                return;
            }

            // ウィンドウの位置とサイズを取得
            var windowInfo = _captureService.GetWindowInfo();
            if (windowInfo == null)
                return;

            // オーバーレイの位置を更新
            _overlayWindow.SetBounds(windowInfo);

            // ウィンドウをキャプチャ
            using var capturedImage = _captureService.CaptureWindow();
            if (capturedImage == null)
                return;

            // デバッグモードの場合
            if (_config.DebugMode)
            {
                // デバッグ用OCR認識を実行
                var debugResults = await _ocrService.RecognizeTextDebugAsync(capturedImage, _config);

                // デバッグ情報を描画
                _overlayWindow.DrawDebugInfo(debugResults);
            }
            else
            {
                // 通常モード: OCR認識を実行
                var detections = await _ocrService.RecognizeTextAsync(capturedImage, _config);

                // オーバーレイに描画
                _overlayWindow.DrawOcrDetections(detections);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"エラーが発生しました: {ex.Message}", 
                "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            StopCapture();
        }
    }

    /// <summary>
    /// ウィンドウを閉じる時の処理
    /// </summary>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        StopCapture();
        _ocrService.Dispose();
        _overlayWindow.Close();
    }
}
