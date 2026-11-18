using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CodescoveryCaptureManager.Domain.Diagnostics
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Fatal,
    }

    /// <summary>
    /// Text log in one directory, rotated by size: capture-manager.log, then capture-manager.1.log (newest) up to the file limit.
    /// A line identical to the previous one is counted instead of written, so a failure repeated every frame cannot flood the file.
    /// Never throws: a log that cannot be written is dropped.
    /// </summary>
    public sealed class RollingFileLog : IDisposable
    {
        public const string FileName = "capture-manager.log";
        public const long DefaultMaxFileBytes = 1024 * 1024;
        public const int DefaultMaxFiles = 5;

        private readonly object _gate = new();
        private readonly long _maxFileBytes;
        private readonly int _maxFiles;
        private readonly Func<DateTime> _clock;
        private string _lastEntry;
        private int _repeats;

        public RollingFileLog(string directory, long maxFileBytes = DefaultMaxFileBytes, int maxFiles = DefaultMaxFiles, Func<DateTime> clock = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxFileBytes, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxFiles, 1);
            Directory = directory;
            _maxFileBytes = maxFileBytes;
            _maxFiles = maxFiles;
            _clock = clock ?? (() => DateTime.Now);
        }

        public string Directory { get; }

        public string FilePath => Path.Combine(Directory, FileName);

        public void Write(LogLevel level, string message, Exception exception = null)
        {
            var entry = FormatEntry(level, message, exception);
            lock (_gate)
            {
                if (entry == _lastEntry)
                {
                    _repeats++;
                    return;
                }

                FlushRepeats();
                _lastEntry = entry;
                Append($"{Timestamp()} {entry}");
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                FlushRepeats();
                _lastEntry = null;
            }
        }

        private void FlushRepeats()
        {
            if (_repeats == 0) return;
            Append($"{Timestamp()} {Label(LogLevel.Info)} Previous entry repeated {_repeats} more time(s).");
            _repeats = 0;
        }

        private void Append(string text)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                var bytes = Encoding.UTF8.GetBytes(text + Environment.NewLine);
                RotateWhenFull(bytes.Length);
                using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                stream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // Nowhere left to report it.
            }
        }

        private void RotateWhenFull(int incomingBytes)
        {
            var current = new FileInfo(FilePath);
            if (!current.Exists || current.Length + incomingBytes <= _maxFileBytes) return;

            if (_maxFiles == 1)
            {
                current.Delete();
                return;
            }

            File.Delete(ArchivePath(_maxFiles - 1));
            for (var index = _maxFiles - 2; index >= 1; index--)
                MoveWhenPresent(ArchivePath(index), ArchivePath(index + 1));
            File.Move(FilePath, ArchivePath(1), overwrite: true);
        }

        private static void MoveWhenPresent(string source, string destination)
        {
            if (File.Exists(source)) File.Move(source, destination, overwrite: true);
        }

        private string ArchivePath(int index)
            => Path.Combine(Directory, $"{Path.GetFileNameWithoutExtension(FileName)}.{index}{Path.GetExtension(FileName)}");

        private string Timestamp()
            => _clock().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        private static string FormatEntry(LogLevel level, string message, Exception exception)
        {
            var line = $"{Label(level)} {message}";
            return exception == null ? line : line + Environment.NewLine + Indent(exception.ToString());
        }

        private static string Label(LogLevel level) => level switch
        {
            LogLevel.Info => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            _ => "FATAL",
        };

        private static string Indent(string text)
            => "    " + text.Replace(Environment.NewLine, Environment.NewLine + "    ");
    }
}
