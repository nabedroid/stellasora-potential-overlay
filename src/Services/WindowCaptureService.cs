using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using StellasoraPotentialOverlay.Models; // 必要に応じて
using WinRT; // 追加: MarshalInterface<T>.FromAbi用

// Windows Runtime / Graphics Capture
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
// using System.Runtime.InteropServices.WindowsRuntime; // WinRTと衝突する可能性があるため削除推奨だが一旦コメントアウトまたは維持

namespace StellasoraPotentialOverlay.Services;

public class WindowCaptureService : IDisposable
{
    private IntPtr _targetWindowHandle = IntPtr.Zero;
    
    // WGC関連
    private IDirect3DDevice? _device;
    private GraphicsCaptureItem? _captureItem;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private SoftwareBitmap? _latestBitmap;
    private object _frameLock = new object();
    private bool _isCapturing = false;

    public WindowCaptureService()
    {
        // Direct3D11デバイスの作成
        _device = Direct3D11Helper.CreateDevice();
    }

    /// <summary>
    /// 対象ウィンドウを設定
    /// </summary>
    public bool SetTargetWindow(string windowTitle)
    {
        // 既にキャプチャ中の場合は停止
        StopCapture();
        
        _targetWindowHandle = FindWindow(null, windowTitle);
        return _targetWindowHandle != IntPtr.Zero;
    }

    /// <summary>
    /// ウィンドウが有効かチェック
    /// </summary>
    public bool IsWindowValid()
    {
        if (_targetWindowHandle == IntPtr.Zero) return false;
        RECT rect;
        return GetWindowRect(_targetWindowHandle, out rect);
    }

    /// <summary>
    /// キャプチャを開始
    /// </summary>
    public void StartCapture()
    {
        if (_targetWindowHandle == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine($"[WGC] StartCapture Failed: WindowHandle is Zero");
            return;
        }

        if (_isCapturing)
        {
            System.Diagnostics.Debug.WriteLine($"[WGC] StartCapture Skipped: Already capturing");
            return;
        }

        try
        {
            // GraphicsCaptureItemを作成
            _captureItem = CaptureHelper.CreateItemForWindow(_targetWindowHandle);
            if (_captureItem == null)
            {
                System.Diagnostics.Debug.WriteLine($"[WGC] StartCapture Failed: CaptureItem is null");
                return;
            }

            // FramePoolを作成
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _device,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                _captureItem.Size);

            _framePool.FrameArrived += OnFrameArrived;

            // セッションを開始
            _session = _framePool.CreateCaptureSession(_captureItem);
            _session.StartCapture();
            _isCapturing = true;
            System.Diagnostics.Debug.WriteLine($"[WGC] StartCapture: Session Started for {_targetWindowHandle}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WGC] Capture Start Error: {ex}");
            StopCapture();
        }
    }

    /// <summary>
    /// キャプチャを停止
    /// </summary>
    public void StopCapture()
    {
        _isCapturing = false;

        _session?.Dispose();
        _session = null;

        if (_framePool != null)
        {
            _framePool.FrameArrived -= OnFrameArrived;
            _framePool.Dispose();
            _framePool = null;
        }

        _captureItem = null;

        lock (_frameLock)
        {
            _latestBitmap?.Dispose();
            _latestBitmap = null;
        }
        
        System.Diagnostics.Debug.WriteLine($"[WGC] Capture Stopped");
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        using var frame = sender.TryGetNextFrame();
        if (frame == null) return;

        // サイズ変更の検知
        if (frame.ContentSize.Width != _captureItem!.Size.Width ||
            frame.ContentSize.Height != _captureItem!.Size.Height)
        {
            _framePool!.Recreate(
                _device,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                frame.ContentSize);
        }

        try
        {
            // Bitmapを取得してキャッシュ
            using var bitmap = SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface).AsTask().Result;
            
            lock (_frameLock)
            {
                _latestBitmap?.Dispose();
                _latestBitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WGC] OnFrameArrived Error: {ex.Message}");
        }
    }

    /// <summary>
    /// 最新のフレームを取得 (System.Drawing.Bitmap)
    /// </summary>
    public Bitmap? GetLatestFrame()
    {
        try
        {
            SoftwareBitmap? softwareBitmap = null;
            lock (_frameLock)
            {
                if (_latestBitmap != null)
                {
                    // コピーを作成して返す
                    softwareBitmap = SoftwareBitmap.Copy(_latestBitmap);
                }
            }

            if (softwareBitmap == null) return null;

            // SoftwareBitmap -> System.Drawing.Bitmap 変換
            using (softwareBitmap)
            {
                using var buffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Read);
                using var reference = buffer.CreateReference();
                
                unsafe
                {
                    IntPtr bufferPtr;
                    uint capacity;

                    reference.As<IMemoryBufferByteAccess>().GetBuffer(out bufferPtr, out capacity);
                    
                    byte* data = (byte*)bufferPtr;

                    var bitmap = new Bitmap(softwareBitmap.PixelWidth, softwareBitmap.PixelHeight, PixelFormat.Format32bppArgb);
                    var bitmapData = bitmap.LockBits(
                        new Rectangle(0, 0, bitmap.Width, bitmap.Height), 
                        ImageLockMode.WriteOnly, 
                        PixelFormat.Format32bppArgb);
                    
                    var plane = buffer.GetPlaneDescription(0);
                    int srcStride = plane.Stride;
                    int dstStride = bitmapData.Stride;
                    int rows = softwareBitmap.PixelHeight;
                    int widthInBytes = softwareBitmap.PixelWidth * 4;

                    for (int y = 0; y < rows; y++)
                    {
                        Buffer.MemoryCopy(
                            data + (y * srcStride),
                            (void*)(bitmapData.Scan0 + (y * dstStride)),
                            widthInBytes,
                            widthInBytes);
                    }

                    bitmap.UnlockBits(bitmapData);
                    return bitmap;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WGC] GetLatestFrame Error: {ex}");
            return null;
        }
    }
    
    // 従来のBitmap取得メソッド（後方互換用、ただしStartCaptureが必要）
    public Bitmap? CaptureWindow()
    {
        // StartCaptureされていなければ開始を試みる（ただし非同期なので即座には取れない）
        if (!_isCapturing) StartCapture();
        
        return GetLatestFrame();
    }

    /// <summary>
    /// ウィンドウ情報を取得
    /// </summary>
    public WindowInfo? GetWindowInfo()
    {
        if (_targetWindowHandle == IntPtr.Zero) return null;

        // DWM（見た目上の真の外枠）を取得
        RECT dwmRect;
        int result = DwmGetWindowAttribute(
            _targetWindowHandle, 
            DWMWA_EXTENDED_FRAME_BOUNDS, 
            out dwmRect, 
            Marshal.SizeOf(typeof(RECT))
        );
        if (result != 0) return null;

        // クライアント領域（中身）のサイズと位置を取得
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

    public void Dispose()
    {
        StopCapture();
        _device?.Dispose();
    }

    #region Win32 API

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    #endregion
}

#region WGC Helpers

public static class Direct3D11Helper
{
    [DllImport("d3d11.dll", EntryPoint = "D3D11CreateDevice", CallingConvention = CallingConvention.StdCall)]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        int driverType, // D3D_DRIVER_TYPE_HARDWARE = 1
        IntPtr Software,
        uint Flags, // D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20
        IntPtr pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion, // D3D11_SDK_VERSION = 7
        out IntPtr ppDevice,
        out uint pFeatureLevel,
        out IntPtr ppImmediateContext); // ID3D11DeviceContext

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

    public static IDirect3DDevice CreateDevice()
    {
        var d3dDevice = CreateD3DDevice();
        return CreateDirect3DDeviceFromDXGIDevice(d3dDevice);
    }

    private static IntPtr CreateD3DDevice()
    {
        // 0x20 = D3D11_CREATE_DEVICE_BGRA_SUPPORT needed for Direct2D/DirectWrite interop
        uint creationFlags = 0x20; 
        
        int hr = D3D11CreateDevice(
            IntPtr.Zero, 
            1, // D3D_DRIVER_TYPE_HARDWARE
            IntPtr.Zero, 
            creationFlags, 
            IntPtr.Zero, 
            0, 
            7, // D3D11_SDK_VERSION
            out IntPtr pDevice, 
            out uint featureLevel, 
            out IntPtr pContext);

        if (hr != 0) throw new Exception("D3D11CreateDevice failed");
        
        // Contextはここでは不要なのでRelease
        if (pContext != IntPtr.Zero) Marshal.Release(pContext);

        return pDevice;
    }

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern UInt32 CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11SurfaceFromDXGISurface", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern UInt32 CreateDirect3D11SurfaceFromDXGISurface(IntPtr dxgiSurface, out IntPtr graphicsSurface);

    public static IDirect3DDevice CreateDirect3DDeviceFromDXGIDevice(IntPtr dxgiDevice)
    {
        // IDirect3DDeviceを作るには、WinRTのIInspectableなどが必要だが
        // ここではWindows.Graphics.DirectX.Direct3D11.CreateDirect3D11DeviceFromDXGIDevice (C++/WinRT)
        // または相互運用機能を使う。
        // C#からは直接呼べないので、IDirect3DDxgiInterfaceAccessを使う
        // ...正直、C#だけで完結させるのは少し面倒なので、ここでは
        // Windows.Graphics.DirectX.Direct3D11.Direct3D11Helpers は存在しないため
        // 以下の簡略化された方法（IDirect3DDeviceをCOMから作成）を試みる。
        
        // 実際には、Windows.Graphics.DirectX.Direct3D11名前空間には静的メソッドがない。
        // 一般的には、D3D11CreateDeviceで作成したデバイスを IDXGIDevice にキャストし、
        // CreateDirect3D11DeviceFromDXGIDevice APIを呼ぶ必要がある。
        
        // しかし、CoreWindowを使わないデスクトップアプリでは、これ以上シンプルにやるには
        // SharpDXなどのライブラリを使うのが一般的。
        // 今回はライブラリを追加できないので、COMインターフェースを定義してどうにかする。

        // IDirect3DDeviceを取得するためのヘルパー
        var factory = new Direct3D11DeviceFactory();
        return factory.CreateDirect3DDevice(dxgiDevice);
    }
}

// 簡易的なヘルパー（実際にはもっと複雑なCOM定義が必要になるが、ここでは最小限に）
[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDirect3DDxgiInterfaceAccess
{
    IntPtr GetInterface([In] ref Guid iid);
}

class Direct3D11DeviceFactory
{
    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    public IDirect3DDevice CreateDirect3DDevice(IntPtr dxgiDevice)
    {
        IntPtr pGraphicsDevice;
        int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out pGraphicsDevice);
        if (hr != 0) throw new Exception("CreateDirect3D11DeviceFromDXGIDevice failed");

        // CsWinRT対応: MarshalInterface<T>.FromAbi を使用してラッパーを取得
        var device = MarshalInterface<IDirect3DDevice>.FromAbi(pGraphicsDevice);
        Marshal.Release(pGraphicsDevice);
        return device;
    }
}

public static class CaptureHelper
{
    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(
            [In] IntPtr window,
            [In] ref Guid iid);

        IntPtr CreateForMonitor(
            [In] IntPtr monitor,
            [In] ref Guid iid);
    }

    [DllImport("api-ms-win-core-winrt-l1-1-0.dll")]
    private static extern int RoGetActivationFactory(
        IntPtr activatableClassId, // HSTRING
        [In] ref Guid iid,
        out IntPtr factory);

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        uint length,
        out IntPtr hstring);

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    public static GraphicsCaptureItem CreateItemForWindow(IntPtr hWnd)
    {
        // GraphicsCaptureItemのActivationFactoryを取得
        // Windows.Graphics.Capture.GraphicsCaptureItem
        string classId = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Guid iid = typeof(IGraphicsCaptureItemInterop).GUID;
        IntPtr factoryPtr = IntPtr.Zero;
        IntPtr hstring = IntPtr.Zero;

        try
        {
            // HSTRING作成
            int hrStr = WindowsCreateString(classId, (uint)classId.Length, out hstring);
            if (hrStr != 0) throw new MarshalDirectiveException($"WindowsCreateString failed: 0x{hrStr:X}");

            // Factory取得
            int hr = RoGetActivationFactory(hstring, ref iid, out factoryPtr);
            if (hr != 0) throw new COMException("RoGetActivationFactory failed", hr);
        }
        finally
        {
            if (hstring != IntPtr.Zero)
            {
                WindowsDeleteString(hstring);
            }
        }

        var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
        Marshal.Release(factoryPtr);

        // GraphicsCaptureItemを作成
        // IIDはGraphicsCaptureItemのIInspectableなどではなく、
        // 実際にはABI.Windows.Graphics.Capture.IGraphicsCaptureItemのIIDが必要
        
        // {79C3F95B-31F7-4EC2-A464-632EF5D30760}
        Guid itemIid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        var pointer = interop.CreateForWindow(hWnd, ref itemIid);
        
        // CsWinRTを使用しているため、Marshal.GetObjectForIUnknownではなく、
        // WinRT.MarshalInterface<T>.FromAbi を使用してラッパーを取得する
        var captureItem = MarshalInterface<GraphicsCaptureItem>.FromAbi(pointer);
        Marshal.Release(pointer);
        
        return captureItem;
    }
}

[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMemoryBufferByteAccess
{
    void GetBuffer(out IntPtr buffer, out uint capacity);
}

#endregion
