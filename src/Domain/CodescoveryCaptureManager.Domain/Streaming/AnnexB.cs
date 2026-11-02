namespace CodescoveryCaptureManager.Domain.Streaming
{
    internal static class AnnexB
    {
        private const int IdrSlice = 5;

        /// <summary>True when the access unit holds an IDR slice (start codes 00 00 01 / 00 00 00 01).</summary>
        public static bool ContainsKeyFrame(byte[] data)
        {
            for (var index = 0; index + 3 < data.Length; index++)
            {
                if (!IsStartCode(data, index)) continue;
                if ((data[index + 3] & 0x1F) == IdrSlice) return true;
            }

            return false;
        }

        private static bool IsStartCode(byte[] data, int index)
            => data[index] == 0 && data[index + 1] == 0 && data[index + 2] == 1;
    }
}
