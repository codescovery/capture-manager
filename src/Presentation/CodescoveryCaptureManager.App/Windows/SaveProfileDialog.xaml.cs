using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using CodescoveryCaptureManager.App.Helpers;
using CodescoveryCaptureManager.Domain.Profiles;

namespace CodescoveryCaptureManager.App.Windows
{
    /// <summary>Names a profile and lets the user edit its rules before saving.</summary>
    public partial class SaveProfileDialog : Window
    {
        private readonly ObservableCollection<RuleRow> _rows;

        public SaveProfileDialog(string suggestedName, IEnumerable<WindowRule> rules)
        {
            InitializeComponent();
            ProfileName.Text = suggestedName;
            _rows = new ObservableCollection<RuleRow>(rules.Select(rule => new RuleRow(rule)));
            RuleList.ItemsSource = _rows;
        }

        public CaptureProfile Result { get; private set; }

        /// <summary>Shows the dialog; returns the new profile, or null when cancelled.</summary>
        public static CaptureProfile Prompt(Window owner, string suggestedName, IEnumerable<WindowRule> rules)
        {
            var dialog = new SaveProfileDialog(suggestedName, rules) { Owner = owner };
            return dialog.ShowDialog() == true ? dialog.Result : null;
        }

        private void RemoveRule_OnClick(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).Tag is RuleRow row)
                _rows.Remove(row);
        }

        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            var problem = FindProblem();
            ErrorText.Text = problem ?? string.Empty;
            if (problem != null) return;

            Result = CaptureProfile.Create(ProfileName.Text.Trim(), _rows.Select(row => row.ToRule()).ToList());
            DialogResult = true;
        }

        private string FindProblem()
        {
            if (string.IsNullOrWhiteSpace(ProfileName.Text)) return "Give the profile a name.";
            if (_rows.Count == 0) return "A profile needs at least one rule.";

            var invalidRegexRow = _rows.FirstOrDefault(row => row.TitleMatch == TitleMatchMode.Regex && !IsValidRegex(row.TitlePattern));
            return invalidRegexRow == null ? null : $"Invalid regular expression for {invalidRegexRow.ProcessName}.";
        }

        private static bool IsValidRegex(string pattern)
        {
            try
            {
                _ = new Regex(pattern ?? string.Empty);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            DarkTitleBar.Apply(this);
        }
    }
}
