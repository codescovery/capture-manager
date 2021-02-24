using System;
using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Interop;

namespace CodescoveryCaptureManager.Domain.Helpers
{
    public static class Direct3D11Helper
    {
        static Guid ID3D11Device = new Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140");
        static Guid ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        
        public static IDirect3DDevice CreateDevice()=> CreateDevice(false);
        public static IDirect3DDevice CreateDevice(bool useWARP)
        {
            var d3dDevice = new SharpDX.Direct3D11.Device(
                useWARP ? SharpDX.Direct3D.DriverType.Software : SharpDX.Direct3D.DriverType.Hardware,
                SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport);
            var device = CreateDirect3DDeviceFromSharpDXDevice(d3dDevice);
            return device;
        }

        public static IDirect3DDevice CreateDirect3DDeviceFromSharpDXDevice(SharpDX.Direct3D11.Device d3dDevice)
        {
            using var device3 = d3dDevice.QueryInterface<SharpDX.DXGI.Device3>();
            var hr = NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(device3.NativePointer, out var pUnknown);

            if (hr != 0) return null;
            var device = Marshal.GetObjectForIUnknown(pUnknown) as IDirect3DDevice;
            Marshal.Release(pUnknown);

            return device;
        }


        public static SharpDX.Direct3D11.Device CreateSharpDXDevice(IDirect3DDevice device)
        {
            var access = (IDirect3DDxgiInterfaceAccess)device;
            var d3dPointer = access.GetInterface(ID3D11Device);
            var d3dDevice = new SharpDX.Direct3D11.Device(d3dPointer);

            return d3dDevice;
        }

        public static SharpDX.Direct3D11.Texture2D CreateSharpDXTexture2D(IDirect3DSurface surface)
        {
            var access = (IDirect3DDxgiInterfaceAccess)surface;
            var d3dPointer = access.GetInterface(ID3D11Texture2D);
            var d3dSurface = new SharpDX.Direct3D11.Texture2D(d3dPointer);
            return d3dSurface;
        }
    }
}
