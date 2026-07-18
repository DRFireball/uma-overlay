using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace UmaCompanion.App.Reading;

/// <summary>
/// Creates the WinRT <see cref="IDirect3DDevice"/> that Windows.Graphics.Capture requires.
/// The only interop in the capture stack: D3D11CreateDevice for the underlying device, then
/// CreateDirect3D11DeviceFromDXGIDevice to project it into WinRT.
/// </summary>
internal static class Direct3D11DeviceFactory
{
    private const int DriverTypeHardware = 1;
    private const int DriverTypeWarp = 5;               // software fallback (no/blocked GPU)
    private const int CreateDeviceBgraSupport = 0x20;   // required for D2D/capture interop
    private const int SdkVersion = 7;                   // D3D11_SDK_VERSION

    private static readonly Guid DxgiDeviceIid = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        IntPtr adapter, int driverType, IntPtr software, int flags,
        IntPtr featureLevels, int featureLevelCount, int sdkVersion,
        out IntPtr device, IntPtr featureLevel, IntPtr immediateContext);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(
        IntPtr dxgiDevice, out IntPtr graphicsDevice);

    public static IDirect3DDevice Create()
    {
        var hr = D3D11CreateDevice(
            IntPtr.Zero, DriverTypeHardware, IntPtr.Zero, CreateDeviceBgraSupport,
            IntPtr.Zero, 0, SdkVersion, out var d3dDevice, IntPtr.Zero, IntPtr.Zero);
        if (hr < 0)
        {
            hr = D3D11CreateDevice(
                IntPtr.Zero, DriverTypeWarp, IntPtr.Zero, CreateDeviceBgraSupport,
                IntPtr.Zero, 0, SdkVersion, out d3dDevice, IntPtr.Zero, IntPtr.Zero);
        }
        Marshal.ThrowExceptionForHR(hr);

        try
        {
            var iid = DxgiDeviceIid;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3dDevice, ref iid, out var dxgiDevice));
            try
            {
                Marshal.ThrowExceptionForHR(
                    CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var inspectable));
                try
                {
                    return MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
                }
                finally
                {
                    Marshal.Release(inspectable);
                }
            }
            finally
            {
                Marshal.Release(dxgiDevice);
            }
        }
        finally
        {
            Marshal.Release(d3dDevice);
        }
    }
}
