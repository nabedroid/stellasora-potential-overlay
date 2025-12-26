namespace StellasoraPotentialOverlay.Models;

using System.Drawing;

/// <summary>
/// ウィンドウの情報
/// </summary>
public class WindowInfo
{
    /// <summary>
    /// ウィンドウ全体の座標（DWMの真の枠）。オーバーレイの配置に使用
    /// </summary>
    public Rectangle WindowBounds { get; set; }
    
    /// <summary>
    /// クライアント領域（中身）の座標。キャプチャや解析に使用
    /// </summary>
    public Rectangle ClientBounds { get; set; }

    /// <summary>
    /// ウィンドウ全体(WindowBounds)から見たクライアント領域の左上オフセット
    /// </summary>
    public int ClientOffsetX => ClientBounds.X - WindowBounds.X;
    public int ClientOffsetY => ClientBounds.Y - WindowBounds.Y;
}