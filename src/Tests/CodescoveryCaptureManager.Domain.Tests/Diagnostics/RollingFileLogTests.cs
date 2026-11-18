using System;
using System.IO;
using System.Linq;
using CodescoveryCaptureManager.Domain.Diagnostics;
using Xunit;

namespace CodescoveryCaptureManager.Domain.Tests.Diagnostics
{
    public sealed class RollingFileLogTests : IDisposable
    {
        private static readonly DateTime FixedTime = new(2026, 11, 18, 14, 3, 22, 123);

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "ccm-log-tests", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }

        [Fact]
        public void Write_FirstEntry_CreatesDirectoryAndWritesTimestampLevelAndMessage()
        {
            using var log = CreateLog();

            log.Write(LogLevel.Info, "Started.");

            Assert.Equal(new[] { "2026-11-18 14:03:22.123 INFO  Started." }, ReadLines(log.FilePath));
        }

        [Fact]
        public void Write_WithException_AppendsIndentedExceptionWithType()
        {
            using var log = CreateLog();

            log.Write(LogLevel.Error, "Frame processing failed.", new InvalidOperationException("device lost"));

            var lines = ReadLines(log.FilePath);
            Assert.Equal("2026-11-18 14:03:22.123 ERROR Frame processing failed.", lines[0]);
            Assert.Equal("    System.InvalidOperationException: device lost", lines[1]);
        }

        [Fact]
        public void Write_SameEntryRepeated_WritesItOnceAndCountsTheRest()
        {
            using var log = CreateLog();

            for (var attempt = 0; attempt < 4; attempt++)
                log.Write(LogLevel.Error, "Frame tap failed.");
            log.Write(LogLevel.Info, "Recovered.");

            Assert.Equal(new[]
            {
                "2026-11-18 14:03:22.123 ERROR Frame tap failed.",
                "2026-11-18 14:03:22.123 INFO  Previous entry repeated 3 more time(s).",
                "2026-11-18 14:03:22.123 INFO  Recovered.",
            }, ReadLines(log.FilePath));
        }

        [Fact]
        public void Dispose_WithPendingRepeats_WritesTheCount()
        {
            var log = CreateLog();
            log.Write(LogLevel.Warning, "Snapshot failed.");
            log.Write(LogLevel.Warning, "Snapshot failed.");

            log.Dispose();

            Assert.Equal("2026-11-18 14:03:22.123 INFO  Previous entry repeated 1 more time(s).", ReadLines(log.FilePath).Last());
        }

        [Fact]
        public void Write_FileOverLimit_MovesItToArchiveAndStartsNewFile()
        {
            using var log = CreateLog(maxFileBytes: 120);

            log.Write(LogLevel.Info, "first " + new string('a', 60));
            log.Write(LogLevel.Info, "second " + new string('b', 60));

            Assert.Contains("first", File.ReadAllText(ArchivePath(1)));
            Assert.Contains("second", File.ReadAllText(log.FilePath));
            Assert.DoesNotContain("first", File.ReadAllText(log.FilePath));
        }

        [Fact]
        public void Write_MoreRotationsThanLimit_KeepsOnlyMaxFiles()
        {
            using var log = CreateLog(maxFileBytes: 120, maxFiles: 3);

            for (var entry = 1; entry <= 6; entry++)
                log.Write(LogLevel.Info, $"entry{entry} " + new string('x', 60));

            Assert.Equal(3, Directory.GetFiles(_directory).Length);
            Assert.Contains("entry6", File.ReadAllText(log.FilePath));
            Assert.Contains("entry5", File.ReadAllText(ArchivePath(1)));
            Assert.Contains("entry4", File.ReadAllText(ArchivePath(2)));
        }

        [Fact]
        public void Write_DirectoryCannotBeCreated_DoesNotThrow()
        {
            Directory.CreateDirectory(_directory);
            var blockingFile = Path.Combine(_directory, "not-a-folder");
            File.WriteAllText(blockingFile, string.Empty);
            using var log = new RollingFileLog(blockingFile);

            var failure = Record.Exception(() => log.Write(LogLevel.Info, "dropped"));

            Assert.Null(failure);
        }

        [Fact]
        public void AppLog_BeforeStart_DropsEntries()
        {
            AppLog.Stop();

            var failure = Record.Exception(() => AppLog.Error("nobody listens", new InvalidOperationException()));

            Assert.Null(failure);
        }

        private RollingFileLog CreateLog(long maxFileBytes = RollingFileLog.DefaultMaxFileBytes, int maxFiles = RollingFileLog.DefaultMaxFiles)
            => new(_directory, maxFileBytes, maxFiles, () => FixedTime);

        private string ArchivePath(int index) => Path.Combine(_directory, $"capture-manager.{index}.log");

        private static string[] ReadLines(string path) => File.ReadAllLines(path);
    }
}
