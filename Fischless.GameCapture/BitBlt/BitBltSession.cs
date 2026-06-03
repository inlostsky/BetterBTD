using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenCvSharp;
using Vanara.PInvoke;

namespace Fischless.GameCapture.BitBlt;

public class BitBltSession : IDisposable
{
    private const int RcBitBlt = 1;

    private readonly HWND _hWnd;
    private readonly object _lockObject = new();
    private readonly int _stride;
    private readonly int _bufferSize;
    private readonly ConcurrentStack<IntPtr> _bufferPool = [];

    private Gdi32.SafeHBITMAP _hBitmap;
    private IntPtr _bitsPtr;
    private Gdi32.SafeHDC _hdcDest;
    private User32.SafeReleaseHDC _hdcSrc;
    private HGDIOBJ _oldBitmap;
    private bool _disposed;

    public BitBltSession(HWND hWnd, int width, int height)
    {
        if (hWnd.IsNull)
        {
            throw new Exception("hWnd is invalid");
        }

        if (width <= 0 || height <= 0)
        {
            throw new Exception("Invalid width or height");
        }

        _hWnd = hWnd;
        Width = width;
        Height = height;

        lock (_lockObject)
        {
            try
            {
                _hdcSrc = User32.GetDC(_hWnd);
                if (_hdcSrc.IsInvalid)
                {
                    throw new Exception($"Failed to get DC for {_hWnd}");
                }

                var hdcRasterCaps = Gdi32.GetDeviceCaps(_hdcSrc, Gdi32.DeviceCap.RASTERCAPS);
                if ((hdcRasterCaps & RcBitBlt) == 0)
                {
                    throw new Exception("BitBlt not supported");
                }

                var hdcSrcPixel = Gdi32.GetDeviceCaps(_hdcSrc, Gdi32.DeviceCap.BITSPIXEL);
                if (hdcSrcPixel != 32 && hdcSrcPixel != 24)
                {
                    throw new Exception("BitBlt only support 24 or 32 bit pixel color");
                }

                var hdcSrcPlanes = Gdi32.GetDeviceCaps(_hdcSrc, Gdi32.DeviceCap.PLANES);
                if (hdcSrcPlanes > 1)
                {
                    throw new Exception("BitBlt only support 1 plane");
                }

                var hdcClip = Gdi32.GetDeviceCaps(_hdcSrc, Gdi32.DeviceCap.CLIPCAPS);
                if (hdcClip == 0)
                {
                    throw new Exception("Device does not support clipping");
                }

                _hdcDest = Gdi32.CreateCompatibleDC(_hdcSrc);
                if (_hdcDest.IsInvalid)
                {
                    Debug.Fail("Failed to create CompatibleDC");
                    throw new Exception($"Failed to create CompatibleDC for {_hWnd}");
                }

                var bmi = new Gdi32.BITMAPINFO
                {
                    bmiHeader = new Gdi32.BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf<Gdi32.BITMAPINFOHEADER>(),
                        biWidth = Width,
                        biHeight = -Height,
                        biPlanes = 1,
                        biBitCount = 24,
                        biCompression = Gdi32.BitmapCompressionMode.BI_RGB,
                        biSizeImage = 0
                    }
                };

                _hBitmap = Gdi32.CreateDIBSection(
                    _hdcDest,
                    bmi,
                    Gdi32.DIBColorMode.DIB_RGB_COLORS,
                    out _bitsPtr,
                    IntPtr.Zero);

                if (_hBitmap.IsInvalid || _bitsPtr == IntPtr.Zero)
                {
                    if (!_hBitmap.IsInvalid)
                    {
                        Gdi32.DeleteObject(_hBitmap);
                    }

                    throw new Exception($"Failed to create dIB section for {_hdcDest}");
                }

                var bitmap = Gdi32.GetObject<Gdi32.BITMAP>(_hBitmap);
                if (bitmap.bmPlanes != 1 || bitmap.bmBitsPixel != 24)
                {
                    throw new Exception("Unsupported bitmap format");
                }

                _stride = bitmap.bmWidthBytes;
                _bufferSize = bitmap.bmWidth * bitmap.bmHeight * 3;

                _oldBitmap = Gdi32.SelectObject(_hdcDest, _hBitmap);
                if (_oldBitmap.IsNull)
                {
                    throw new Exception("Failed to select object");
                }
            }
            catch
            {
                ReleaseResources();
                throw;
            }
            finally
            {
                Gdi32.GdiFlush();
            }
        }
    }

    public int Width { get; }

    public int Height { get; }

    public bool Invalid => _disposed || _hWnd.IsNull || _hdcSrc.IsInvalid || _hdcDest.IsInvalid || _hBitmap.IsInvalid || _bitsPtr == IntPtr.Zero;

    public void Dispose()
    {
        lock (_lockObject)
        {
            ReleaseResources();
        }

        GC.SuppressFinalize(this);
    }

    public unsafe Mat? GetImage()
    {
        lock (_lockObject)
        {
            if (_disposed || Invalid)
            {
                return null;
            }

            var success = Gdi32.BitBlt(
                _hdcDest,
                0,
                0,
                Width,
                Height,
                _hdcSrc,
                0,
                0,
                Gdi32.RasterOperationMode.SRCCOPY);

            if (!success || !Gdi32.GdiFlush())
            {
                return null;
            }

            var buffer = AcquireBuffer();
            var step = Width * 3;
            if (_stride == step)
            {
                Buffer.MemoryCopy(_bitsPtr.ToPointer(), buffer.ToPointer(), _bufferSize, _bufferSize);
            }
            else
            {
                for (var i = 0; i < Height; i++)
                {
                    Buffer.MemoryCopy((void*)(_bitsPtr + _stride * i), (void*)(buffer + step * i), step, step);
                }
            }

            return BitBltMat.FromPixelData(this, Height, Width, MatType.CV_8UC3, buffer, step);
        }
    }

    private IntPtr AcquireBuffer()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BitBltSession));
        }

        if (!_bufferPool.TryPop(out var buffer))
        {
            buffer = Marshal.AllocHGlobal(_bufferSize);
        }

        return buffer;
    }

    public void ReleaseBuffer(IntPtr buffer)
    {
        if (buffer == IntPtr.Zero)
        {
            return;
        }

        lock (_lockObject)
        {
            if (_disposed)
            {
                Marshal.FreeHGlobal(buffer);
                return;
            }

            _bufferPool.Push(buffer);
        }
    }

    private void ReleaseResources()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Gdi32.GdiFlush();

        if (!_oldBitmap.IsNull)
        {
            Gdi32.SelectObject(_hdcDest, _oldBitmap);
            _oldBitmap = Gdi32.SafeHBITMAP.Null;
        }

        if (!_hBitmap.IsNull)
        {
            Gdi32.DeleteObject(_hBitmap);
            _hBitmap = Gdi32.SafeHBITMAP.Null;
        }

        if (!_hdcDest.IsNull)
        {
            Gdi32.DeleteDC(_hdcDest);
            _hdcDest = Gdi32.SafeHDC.Null;
        }

        if (_hdcSrc != IntPtr.Zero)
        {
            User32.ReleaseDC(_hWnd, _hdcSrc);
            _hdcSrc = User32.SafeReleaseHDC.Null;
        }

        _bitsPtr = IntPtr.Zero;

        while (_bufferPool.TryPop(out var buffer))
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
