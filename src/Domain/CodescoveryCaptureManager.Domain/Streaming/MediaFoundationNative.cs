using System;
using System.Runtime.InteropServices;
using SharpDX.MediaFoundation;

namespace CodescoveryCaptureManager.Domain.Streaming
{
    internal static class MediaFoundationNative
    {
        public const int MfVersion = 0x00020070;
        public const uint EnumSyncMft = 0x1;
        public const uint EnumHardware = 0x4;
        public const uint EnumSortAndFilter = 0x40;

        public static readonly Guid CodecApiIid = new Guid("901db4c7-31ce-41a2-85dc-8fa0bf41b8da");
        public static readonly Guid LowLatency = new Guid("9c27891a-ed7a-40e1-88e8-b22727a024ee");
        public static readonly Guid RateControlMode = new Guid("1c0608e9-370c-4710-8a58-cb6181c42423");
        public static readonly Guid MeanBitRate = new Guid("f7222374-2144-4815-b550-a37f8e12ee52");
        public static readonly Guid BPictureCount = new Guid("8d390aac-dc5c-4200-b57f-814d04babab2");
        public static readonly Guid ForceKeyFrame = new Guid("398c1b98-8353-475a-9ef2-8f265d260345");

        private static readonly Guid VideoMajor = new Guid("73646976-0000-0010-8000-00AA00389B71");
        private static readonly Guid H264Subtype = new Guid("34363248-0000-0010-8000-00AA00389B71");

        [StructLayout(LayoutKind.Sequential)]
        private struct RegisterTypeInfo
        {
            public Guid MajorType;
            public Guid SubType;
        }

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFStartup(int version, int flags);

        [DllImport("mf.dll", ExactSpelling = true)]
        private static extern int MFShutdownObject(IntPtr unknown);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFTEnumEx(Guid category, uint flags, IntPtr input, ref RegisterTypeInfo output, out IntPtr activates, out int count);

        [StructLayout(LayoutKind.Sequential)]
        private struct OutputDataBuffer
        {
            public int StreamId;
            public IntPtr Sample;
            public int Status;
            public IntPtr Events;
        }

        private const int ProcessOutputVtableIndex = 25;

        /// <summary>
        /// IMFTransform::ProcessOutput through the vtable: SharpDX does not copy the sample the MFT returns
        /// back into its managed array. Returns the HRESULT; the sample pointer is the MFT's (ref counted).
        /// </summary>
        public static unsafe int ProcessOutput(Transform transform, IntPtr providedSample, out IntPtr outputSample)
        {
            var buffer = new OutputDataBuffer { StreamId = 0, Sample = providedSample, Status = 0, Events = IntPtr.Zero };
            var self = transform.NativePointer;
            var vtable = *(IntPtr**)self;
            var call = (delegate* unmanaged[Stdcall]<IntPtr, int, int, OutputDataBuffer*, int*, int>)vtable[ProcessOutputVtableIndex];
            int status;
            var hr = call(self, 0, 1, &buffer, &status);
            outputSample = buffer.Sample;
            if (buffer.Events != IntPtr.Zero) Marshal.Release(buffer.Events);
            return hr;
        }

        public static void Startup()
        {
            Marshal.ThrowExceptionForHR(MFStartup(MfVersion, 0));
        }

        public static void Shutdown(Transform transform)
        {
            MFShutdownObject(transform.NativePointer);
        }

        /// <summary>Activates H.264 encoders, best first. Hardware ones only when requested.</summary>
        public static Transform[] EnumerateH264Encoders(bool hardware)
        {
            var flags = (hardware ? EnumHardware : EnumSyncMft) | EnumSortAndFilter;
            var output = new RegisterTypeInfo { MajorType = VideoMajor, SubType = H264Subtype };
            var hr = MFTEnumEx(TransformCategoryGuids.VideoEncoder, flags, IntPtr.Zero, ref output, out var array, out var count);
            if (hr < 0 || count == 0) return Array.Empty<Transform>();
            try
            {
                return ActivateAll(array, count);
            }
            finally
            {
                Marshal.FreeCoTaskMem(array);
            }
        }

        private static Transform[] ActivateAll(IntPtr array, int count)
        {
            var transforms = new System.Collections.Generic.List<Transform>();
            for (var index = 0; index < count; index++)
            {
                var transform = TryActivate(Marshal.ReadIntPtr(array, index * IntPtr.Size));
                if (transform != null) transforms.Add(transform);
            }
            return transforms.ToArray();
        }

        private static Transform TryActivate(IntPtr activatePointer)
        {
            try
            {
                using var activate = new Activate(activatePointer);
                return activate.ActivateObject<Transform>();
            }
            catch (SharpDX.SharpDXException)
            {
                return null;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct Variant
        {
            [FieldOffset(0)] public ushort Type;
            [FieldOffset(8)] public int Value;
        }

        private const ushort VtBool = 11;
        private const ushort VtUi4 = 19;
        private const int SetValueVtableIndex = 9;

        /// <summary>ICodecAPI of the encoder as a raw (ref counted) pointer. Raw vtable calls avoid COM apartment marshaling, which fails from the encoder thread.</summary>
        public static IntPtr QueryCodecApi(Transform transform) => transform.QueryInterfaceOrNull(CodecApiIid);

        public static void ReleaseCodecApi(IntPtr codec)
        {
            if (codec != IntPtr.Zero) Marshal.Release(codec);
        }

        public static unsafe void TrySet(IntPtr codec, Guid api, object value)
        {
            if (codec == IntPtr.Zero) return;
            var variant = value is bool flag
                ? new Variant { Type = VtBool, Value = flag ? -1 : 0 }
                : new Variant { Type = VtUi4, Value = unchecked((int)(uint)value) };
            var vtable = *(IntPtr**)codec;
            var call = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, Variant*, int>)vtable[SetValueVtableIndex];
            var hr = call(codec, &api, &variant);
            if (hr < 0) Console.WriteLine($"Encoder setting {api} not applied (0x{hr:X8}).");
        }
    }
}
