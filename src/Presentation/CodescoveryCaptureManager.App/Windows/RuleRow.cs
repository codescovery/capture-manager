using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CodescoveryCaptureManager.Domain.Profiles;

namespace CodescoveryCaptureManager.App.Windows
{
    /// <summary>Editable row of the profile dialog; converts to and from <see cref="WindowRule"/>.</summary>
    internal sealed class RuleRow : INotifyPropertyChanged
    {
        public static TitleMatchMode[] Modes { get; } = Enum.GetValues<TitleMatchMode>();

        private TitleMatchMode _titleMatch;
        private bool _autoAttach;

        public RuleRow(WindowRule rule)
        {
            ProcessName = rule.ProcessName;
            _titleMatch = rule.TitleMatch;
            TitlePattern = rule.TitlePattern ?? string.Empty;
            _autoAttach = rule.AutoAttach;
        }

        public string ProcessName { get; }

        public string TitlePattern { get; set; }

        public TitleMatchMode TitleMatch
        {
            get => _titleMatch;
            set
            {
                _titleMatch = value;
                // "Any title" would auto-share windows the user never picked: keep auto-attach off.
                if (value == TitleMatchMode.Any) _autoAttach = false;
                Raise();
                Raise(nameof(AutoAttach));
                Raise(nameof(MatchesEveryWindow));
                Raise(nameof(TitleApplies));
            }
        }

        public bool AutoAttach
        {
            get => _autoAttach;
            set
            {
                _autoAttach = value && _titleMatch != TitleMatchMode.Any;
                Raise();
            }
        }

        public bool MatchesEveryWindow => _titleMatch == TitleMatchMode.Any;

        public bool TitleApplies => !MatchesEveryWindow;

        public WindowRule ToRule() => new(ProcessName, TitleMatch, TitlePattern.Trim(), AutoAttach);

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
