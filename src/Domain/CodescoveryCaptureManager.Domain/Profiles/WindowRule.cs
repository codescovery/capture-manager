namespace CodescoveryCaptureManager.Domain.Profiles
{
    public enum TitleMatchMode
    {
        /// <summary>Any window of the process. Risky with auto-attach: shares windows the user never picked.</summary>
        Any,
        Contains,
        Exact,
        Regex,
    }

    /// <summary>Identifies windows by process and title, since HWNDs do not survive an app restart.</summary>
    public sealed record WindowRule(string ProcessName, TitleMatchMode TitleMatch, string TitlePattern, bool AutoAttach);
}
