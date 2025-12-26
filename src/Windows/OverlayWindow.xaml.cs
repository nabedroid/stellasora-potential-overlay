using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using StellasoraPotentialOverlay.Models;

namespace StellasoraPotentialOverlay;

/// <summary>
/// オーバーレイウィンドウ
/// </summary>
public partial class OverlayWindow : Window
{
    #region Windows API

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    #endregion

    private SolidColorBrush _borderBrush = new SolidColorBrush(Colors.Lime);
    private SolidColorBrush _textBrush = new SolidColorBrush(Colors.White);
    private int _borderThickness = 2;
    private int _textSize = 14;
    private double _clientOffsetX = 0;
    private double _clientOffsetY = 0;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OverlayWindow_Loaded;
    }

    private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // マウス・キーボード入力を透過させる
        var hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
    }

    /// <summary>
    /// オーバーレイの位置とサイズを設定
    /// </summary>
    public void SetBounds(WindowInfo windowInfo)
    {
        Left = windowInfo.WindowBounds.X;
        Top = windowInfo.WindowBounds.Y;
        Width = windowInfo.WindowBounds.Width;
        Height = windowInfo.WindowBounds.Height;
        _clientOffsetX = windowInfo.ClientOffsetX;
        _clientOffsetY = windowInfo.ClientOffsetY;
    }

    /// <summary>
    /// オーバーレイのスタイルを設定
    /// </summary>
    public void SetStyle(string borderColor, int borderThickness, string textColor, int textSize)
    {
        try
        {
            _borderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderColor));
            _textBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor));
            _borderThickness = borderThickness;
            _textSize = textSize;
        }
        catch
        {
            // デフォルト値を使用
        }
    }

    public void DrawOcrDetections(List<OcrDetectionResult> detections)
    {
        OverlayCanvas.Children.Clear();

        foreach (var detection in detections)
        {
            if (detection.MatchedTarget == null)
                continue;

            SolidColorBrush displayBrush;
            try
            {
                displayBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(detection.MatchedTarget.DisplayColor));
            }
            catch
            {
                displayBrush = new SolidColorBrush(Colors.Yellow);
            }

            // 余白と背景を担当するBorder
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Padding = new Thickness(8, 4, 8, 4), // ここで余白を設定
                CornerRadius = new CornerRadius(2)   // 少し角を丸くすると見やすいです
            };

            // 横並びを担当するStackPanel
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            // 目標レベル
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"目標: {detection.MatchedTarget.TargetLevel}",
                Foreground = displayBrush,
                FontSize = _textSize,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            });

            // メモ
            if (!string.IsNullOrWhiteSpace(detection.MatchedTarget.Memo))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $" | {detection.MatchedTarget.Memo}",
                    Foreground = _textBrush,
                    FontSize = _textSize,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            border.Child = stackPanel;

            // 表示位置の設定
            Canvas.SetLeft(border, detection.X + _clientOffsetX);
            Canvas.SetTop(border, detection.Y + _clientOffsetY - 35); 
            OverlayCanvas.Children.Add(border);
        }
    }

    /// <summary>
    /// デバッグモード: OCR領域と認識文字を表示
    /// </summary>
    public void DrawDebugInfo(List<OcrDebugResult> debugResults) {
        OverlayCanvas.Children.Clear();

        foreach (var result in debugResults) {
            // 領域の枠線を描画（青色）
            var rectangle = new Rectangle {
                Width = result.Width,
                Height = result.Height,
                Stroke = new SolidColorBrush(Colors.Cyan),
                StrokeThickness = 3,
                Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255)) // 半透明シアン
            };

            Canvas.SetLeft(rectangle, result.X + _clientOffsetX);
            Canvas.SetTop(rectangle, result.Y + _clientOffsetY);
            OverlayCanvas.Children.Add(rectangle);

            // 領域名を表示
            var regionNameTextBlock = new TextBlock {
                Text = result.RegionName,
                Foreground = new SolidColorBrush(Colors.Cyan),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0))
            };

            Canvas.SetLeft(regionNameTextBlock, result.X + _clientOffsetX);
            Canvas.SetTop(regionNameTextBlock, result.Y + _clientOffsetY - 25);
            OverlayCanvas.Children.Add(regionNameTextBlock);

            // 認識文字を表示
            var recognizedTextBlock = new TextBlock {
                Text = string.IsNullOrWhiteSpace(result.RecognizedText)
                    ? "[認識なし]"
                    : $"認識: {result.RecognizedText}",
                Foreground = new SolidColorBrush(Colors.Yellow),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = result.Width
            };

            Canvas.SetLeft(recognizedTextBlock, result.X + _clientOffsetX);
            Canvas.SetTop(recognizedTextBlock, result.Y + _clientOffsetY + result.Height + 5);
            OverlayCanvas.Children.Add(recognizedTextBlock);
        }
    }

    /// <summary>
    /// オーバーレイをクリア
    /// </summary>
    public void ClearOverlay() { OverlayCanvas.Children.Clear(); }
}
