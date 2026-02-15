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
            Canvas.SetLeft(border, detection.X);
            Canvas.SetTop(border, detection.Y - 35); 
            OverlayCanvas.Children.Add(border);
        }
    }

    /// <summary>
    /// デバッグモード: OCR領域を表示（文字はDebugWindowに表示するため枠線のみ）
    /// </summary>
    public void DrawDebugInfo(List<OcrDebugResult> debugResults) {
        OverlayCanvas.Children.Clear();

        foreach (var result in debugResults) {
            // 領域の枠線を描画（シアン、半透明）
            var rectangle = new Rectangle {
                Width = result.Width,
                Height = result.Height,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 0, 255, 255)), // シアン
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            Canvas.SetLeft(rectangle, result.X);
            Canvas.SetTop(rectangle, result.Y);
            OverlayCanvas.Children.Add(rectangle);

            // 認識テキストを表示
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // 黒背景（半透明）
                Padding = new Thickness(2),
                CornerRadius = new CornerRadius(2)
            };

            var textBlock = new TextBlock
            {
                Text = result.RecognizedText,
                Foreground = Brushes.Cyan,
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };

            border.Child = textBlock;

            // 枠の上に表示
            Canvas.SetLeft(border, result.X);
            Canvas.SetTop(border, result.Y - 20); 
            OverlayCanvas.Children.Add(border);
        }
    }

    /// <summary>
    /// オーバーレイをクリア
    /// </summary>
    public void ClearOverlay() { OverlayCanvas.Children.Clear(); }
}
