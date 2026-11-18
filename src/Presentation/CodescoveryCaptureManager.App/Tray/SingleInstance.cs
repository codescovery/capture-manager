using System;
using System.Threading;

namespace CodescoveryCaptureManager.App.Tray
{
    /// <summary>One running copy per user session. A second launch asks the first one to show its window and exits.</summary>
    public sealed class SingleInstance : IDisposable
    {
        private const string MutexName = @"Local\Codescovery.CaptureManager";
        private const string ShowEventName = @"Local\Codescovery.CaptureManager.Show";

        private readonly Mutex _mutex;
        private readonly EventWaitHandle _showRequested;

        private SingleInstance(Mutex mutex, EventWaitHandle showRequested)
        {
            _mutex = mutex;
            _showRequested = showRequested;
        }

        /// <summary>Returns null (after asking the running copy to show itself) when another instance exists.</summary>
        public static SingleInstance TryAcquire()
        {
            var mutex = new Mutex(true, MutexName, out var isFirst);
            var showRequested = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            if (isFirst) return new SingleInstance(mutex, showRequested);

            showRequested.Set();
            showRequested.Dispose();
            mutex.Dispose();
            return null;
        }

        public void ListenForShowRequests(Action onShowRequested)
        {
            var thread = new Thread(() =>
            {
                while (_showRequested.WaitOne()) onShowRequested();
            })
            { IsBackground = true, Name = "SingleInstanceListener" };
            thread.Start();
        }

        public void Dispose()
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
