using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using StellasoraPotentialOverlay.Models;

namespace StellasoraPotentialOverlay.Services;

/// <summary>
/// ウィンドウキャプチャサービス
/// </summary>
public class WindowCaptureService
{
    #region Windows API

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
    private const uint SRCCOPY = 0x00CC0020;

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }



    #endregion

    private IntPtr _targetWindowHandle = IntPtr.Zero;

    /// <summary>
    /// 対象ウィンドウを設定
    /// </summary>
    public bool SetTargetWindow(string windowTitle)
    {
        _targetWindowHandle = FindWindow(null, windowTitle);
        return _targetWindowHandle != IntPtr.Zero;
    }

    /// <summary>
    /// 対象ウィンドウのハンドルを取得
    /// </summary>
    public IntPtr GetTargetWindowHandle() => _targetWindowHandle;

    /// <summary>
    /// ウィンドウが有効かチェック
    /// </summary>
    public bool IsWindowValid()
    {
        if (_targetWindowHandle == IntPtr.Zero)
            return false;

        RECT rect;
        return GetWindowRect(_targetWindowHandle, out rect);
    }

    /// <summary>
    /// 対象ウィンドウが現在最前面（アクティブ）かどうかを判定
    /// </summary>
    public bool IsWindowActive()
    {
        if (_targetWindowHandle == IntPtr.Zero)
            return false;

        // 最小化されている場合はアクティブではないとみなす
        if (IsIconic(_targetWindowHandle))
            return false;

        // 現在の最前面ウィンドウと一致するか
        return GetForegroundWindow() == _targetWindowHandle;
    }

    /// <summary>
    /// ウィンドウの位置とサイズを取得
    /// </summary>
    // public Rectangle? GetWindowBounds()
    // {
    //     if (_targetWindowHandle == IntPtr.Zero)
    //         return null;

    //     RECT rect;
    //     if (!GetWindowRect(_targetWindowHandle, out rect))
    //         return null;

    //     return new Rectangle(
    //         rect.Left,
    //         rect.Top,
    //         rect.Right - rect.Left,
    //         rect.Bottom - rect.Top
    //     );
    // }
    public WindowInfo? GetWindowInfo()
    {
        if (_targetWindowHandle == IntPtr.Zero) return null;

        // 1. DWM（見た目上の真の外枠）を取得
        RECT dwmRect;
        int result = DwmGetWindowAttribute(
            _targetWindowHandle, 
            DWMWA_EXTENDED_FRAME_BOUNDS, 
            out dwmRect, 
            Marshal.SizeOf(typeof(RECT))
        );
        if (result != 0) return null;

        // 2. クライアント領域（中身）のサイズと位置を取得
        RECT cRect;
        if (!GetClientRect(_targetWindowHandle, out cRect)) return null;
        
        POINT upperLeft = new POINT { X = 0, Y = 0 };
        ClientToScreen(_targetWindowHandle, ref upperLeft);

        return new WindowInfo
        {
            WindowBounds = new Rectangle(
                dwmRect.Left, 
                dwmRect.Top, 
                dwmRect.Right - dwmRect.Left, 
                dwmRect.Bottom - dwmRect.Top),
            
            ClientBounds = new Rectangle(
                upperLeft.X, 
                upperLeft.Y, 
                cRect.Right - cRect.Left, 
                cRect.Bottom - cRect.Top)
        };
    }
    /// <summary>
    /// ウィンドウをキャプチャ
    /// </summary>
    //public Bitmap? CaptureWindow()
    //{
    //    if (_targetWindowHandle == IntPtr.Zero)
    //        return null;

    //    // ウィンドウがアクティブでない場合はキャプチャしない
    //    if (!IsWindowActive())
    //        return null;

    //    RECT rect;
    //    if (!GetWindowRect(_targetWindowHandle, out rect))
    //        return null;

    //    int width = rect.Right - rect.Left;
    //    int height = rect.Bottom - rect.Top;

    //    if (width <= 0 || height <= 0)
    //        return null;

    //    Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

    //    using (Graphics graphics = Graphics.FromImage(bitmap))
    //    {
    //        IntPtr hdc = graphics.GetHdc();
    //        PrintWindow(_targetWindowHandle, hdc, 0);
    //        graphics.ReleaseHdc(hdc);
    //    }

    //    return bitmap;
    //}
    public Bitmap? CaptureWindow()
    {
        if (!IsWindowActive()) return null;

        var info = GetWindowInfo();
        if (info == null) return null;

        int width = info.ClientBounds.Width;
        int height = info.ClientBounds.Height;

        IntPtr hdcScreen = GetDC(IntPtr.Zero);
        Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            IntPtr hdcDest = graphics.GetHdc();
            // info.ClientBounds の座標をそのまま使える！
            BitBlt(hdcDest, 0, 0, width, height, hdcScreen, info.ClientBounds.X, info.ClientBounds.Y, SRCCOPY);
            graphics.ReleaseHdc(hdcDest);
        }
        
        ReleaseDC(IntPtr.Zero, hdcScreen);
        return bitmap;
    }


    /// <summary>
    /// 利用可能なウィンドウのリストを取得
    /// </summary>
    public static List<string> GetAvailableWindows()
    {
        var windows = new List<string>();
        
        EnumWindows((hWnd, lParam) =>
        {
            if (IsWindowVisible(hWnd))
            {
                int length = GetWindowTextLength(hWnd);
                if (length > 0)
                {
                    var builder = new System.Text.StringBuilder(length + 1);
                    GetWindowText(hWnd, builder, builder.Capacity);
                    string title = builder.ToString();
                    
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        windows.Add(title);
                    }
                }
            }
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    #region Additional Windows API for EnumWindows

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    #endregion
}
