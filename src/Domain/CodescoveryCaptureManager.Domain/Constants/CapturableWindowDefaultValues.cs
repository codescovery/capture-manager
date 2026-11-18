namespace CodescoveryCaptureManager.Domain.Constants
{
    public class CapturableWindowDefaultValues
    {
        /// <summary>Milliseconds between the checks that back up the foreground hook (closed windows, changed titles).</summary>
        public const int SafetyCheckInterval = 1000;
    }
}