// One background Qalam process owns the keyboard hook and tray icon. A second
// launch (for example from the pinned taskbar shortcut) wakes that process and
// asks it to show the main window instead of creating a duplicate tray icon.

using System;
using System.Threading;
using System.Windows;

namespace Qalam
{
    internal sealed class SingleInstance : IDisposable
    {
        private readonly Mutex _mutex;
        private readonly EventWaitHandle _event;
        private readonly Thread _listener;
        private volatile bool _stopping;
        private bool _started;

        public bool IsFirst { get; private set; }

        public event Action Activated;

        public SingleInstance()
        {
            _mutex = TryAcquireMutex(MutexName());
            if (_mutex == null) return;

            bool created;
            _event = new EventWaitHandle(false, EventResetMode.AutoReset, EventName(), out created);
            IsFirst = true;
            _listener = new Thread(Listen);
            _listener.IsBackground = true;
            _listener.Name = "Qalam activation";
        }

        public void Start()
        {
            if (!IsFirst || _started) return;
            _started = true;
            _listener.Start();
        }

        public static bool SignalExisting()
        {
            // The first instance creates its mutex before its event, so a
            // launch can race the event creation. Retry briefly.
            for (int attempt = 0; attempt < 24; attempt++)
            {
                try
                {
                    using (EventWaitHandle handle = EventWaitHandle.OpenExisting(EventName()))
                    {
                        handle.Set();
                        return true;
                    }
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    Thread.Sleep(50);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        private void Listen()
        {
            while (!_stopping)
            {
                try
                {
                    if (!_event.WaitOne(400)) continue;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (_stopping) return;
                Action handler = Activated;
                if (handler == null) continue;
                try
                {
                    Application app = Application.Current;
                    if (app != null) app.Dispatcher.BeginInvoke(handler);
                    else handler();
                }
                catch (Exception ex)
                {
                    Logger.Write("activation: " + ex.Message);
                }
            }
        }

        private static Mutex TryAcquireMutex(string name)
        {
            bool created;
            Mutex mutex = new Mutex(true, name, out created);
            if (created) return mutex;

            try
            {
                if (mutex.WaitOne(0)) return mutex;
            }
            catch (AbandonedMutexException)
            {
                // The previous Qalam exited without releasing its mutex;
                // ownership passes to this process.
                return mutex;
            }
            catch (Exception)
            {
                // Fall through and report that another instance owns it.
            }

            mutex.Dispose();
            return null;
        }

        private static string Suffix()
        {
            string user = Environment.UserName;
            if (string.IsNullOrEmpty(user)) user = Environment.UserDomainName;
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                user = user.Replace(c, '_');
            return "Qalam." + user;
        }

        private static string MutexName()
        {
            return @"Local\" + Suffix() + ".Instance";
        }

        private static string EventName()
        {
            return @"Local\" + Suffix() + ".Activate";
        }

        public void Dispose()
        {
            if (!IsFirst) return;
            _stopping = true;
            try { if (_event != null) _event.Set(); }
            catch { }
            try { if (_listener != null) _listener.Join(600); }
            catch { }
            try { if (_event != null) _event.Dispose(); }
            catch { }
            try { _mutex.ReleaseMutex(); }
            catch { }
            try { _mutex.Dispose(); }
            catch { }
        }
    }
}
