using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace T2medFon
{
    /// <summary>
    /// Diagnosis for focus problems: only active while t2med-fon.log exists next to the configuration.
    /// Logs window classes and process names but no window titles, which may contain patient names.
    /// </summary>
    internal sealed class FocusLog
    {
        private readonly string path;
        private readonly StringBuilder text = new StringBuilder();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private volatile bool stopped;
        private Thread watcher;
        private IntPtr remembered;

        public FocusLog(string path) { if (File.Exists(path)) this.path = path; }

        public static string DefaultPath()
        {
            return Path.Combine(Path.GetDirectoryName(Config.DefaultPath()), "t2med-fon.log");
        }

        public void Start(IntPtr remembered)
        {
            if (path == null) return;
            this.remembered = remembered;
            Line("--- " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " t2med-fon " + Program.Version);
            Line("Aufrufkette: " + ParentChain());
            Line("Vordergrund beim Start: " + Describe(remembered));
            Line("Fensterstapel: " + ZOrder(remembered));
            watcher = new Thread(Watch);
            watcher.IsBackground = true;
            watcher.Start(remembered);
        }

        public void Stop(int exitCode, string restore)
        {
            if (path == null) return;
            stopped = true;
            watcher.Join();
            Line("Exitcode " + exitCode + ", Fenster zurückholen: " + restore);
            Line("Vordergrund beim Ende: " + Describe(GetForegroundWindow()));
            Line("Fensterstapel: " + ZOrder(remembered));
            try { File.AppendAllText(path, text.ToString(), Encoding.UTF8); }
            catch (IOException) { } // diagnosis must never get in the way of dialing
            catch (UnauthorizedAccessException) { }
        }

        private void Watch(object remembered)
        {
            IntPtr last = (IntPtr)remembered;
            while (!stopped)
            {
                IntPtr current = GetForegroundWindow();
                if (current != last) Line("Vordergrund wechselt zu " + Describe(current));
                last = current;
                Thread.Sleep(10);
            }
        }

        private void Line(string line)
        {
            lock (text) text.AppendFormat("{0,6} ms  {1}\r\n", clock.ElapsedMilliseconds, line);
        }

        private static string Describe(IntPtr window)
        {
            if (window == IntPtr.Zero) return "(keins)";
            StringBuilder className = new StringBuilder(256);
            GetClassName(window, className, className.Capacity);
            uint pid;
            GetWindowThreadProcessId(window, out pid);
            string flags = (IsWindowVisible(window) ? "" : " unsichtbar") + (IsIconic(window) ? " minimiert" : "");
            if ((GetWindowLong(window, GwlExStyle) & WsExTopmost) != 0) flags += " immer-oben";
            int cloaked;
            if (DwmGetWindowAttribute(window, DwmwaCloaked, out cloaked, 4) == 0 && cloaked != 0) flags += " verborgen";
            IntPtr owner = GetWindow(window, GwOwner);
            if (owner != IntPtr.Zero) flags += " Besitzer 0x" + owner.ToInt64().ToString("X");
            Rect rect;
            if (GetWindowRect(window, out rect)) flags += " @" + rect.Left + "," + rect.Top + " " + (rect.Right - rect.Left) + "x" + (rect.Bottom - rect.Top);
            return "0x" + window.ToInt64().ToString("X") + " [" + className + "] " + ProcessName(pid) + " (" + pid + ")" + flags;
        }

        // Visible, uncloaked top-level windows from top to bottom, down to the remembered window (marked *).
        private static string ZOrder(IntPtr remembered)
        {
            List<string> windows = new List<string>();
            bool found = false;
            EnumWindows(delegate(IntPtr window, IntPtr unused)
            {
                int cloaked;
                if (window != remembered && (!IsWindowVisible(window) || (DwmGetWindowAttribute(window, DwmwaCloaked, out cloaked, 4) == 0 && cloaked != 0))) return true;
                found = window == remembered;
                windows.Add((found ? "*" : "") + Describe(window));
                return !found && windows.Count < 15;
            }, IntPtr.Zero);
            if (!found) windows.Add("(gemerktes Fenster nicht unter den obersten 15)");
            return "\r\n           " + string.Join("\r\n           ", windows);
        }

        private static string ParentChain()
        {
            Dictionary<uint, ProcessEntry> processes = new Dictionary<uint, ProcessEntry>();
            IntPtr snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
            if (snapshot == new IntPtr(-1)) return "(unbekannt)";
            try
            {
                ProcessEntry entry = new ProcessEntry();
                entry.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry));
                for (bool ok = Process32FirstW(snapshot, ref entry); ok; ok = Process32NextW(snapshot, ref entry))
                    processes[entry.ProcessId] = entry;
            }
            finally { CloseHandle(snapshot); }
            List<string> chain = new List<string>();
            uint pid = (uint)Process.GetCurrentProcess().Id;
            for (int depth = 0; depth < 6; depth++)
            {
                ProcessEntry entry;
                if (!processes.TryGetValue(pid, out entry)) { chain.Add("(" + pid + " beendet)"); break; }
                chain.Add(entry.ExeFile + " (" + pid + ")");
                if (entry.ParentProcessId == 0 || entry.ParentProcessId == pid) break;
                pid = entry.ParentProcessId;
            }
            return string.Join(" <- ", chain);
        }

        private static string ProcessName(uint pid)
        {
            try { using (Process process = Process.GetProcessById((int)pid)) return process.ProcessName; }
            catch (ArgumentException) { return "?"; }
            catch (InvalidOperationException) { return "?"; }
        }

        private const uint Th32csSnapProcess = 0x2;
        private const uint GwOwner = 4;
        private const int DwmwaCloaked = 14;
        private const int GwlExStyle = -20;
        private const int WsExTopmost = 0x8;

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect { public int Left, Top, Right, Bottom; }

#pragma warning disable 0169, 0649 // filled by Process32FirstW/NextW
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry
        {
            public uint Size;
            private uint usage;
            public uint ProcessId;
            private IntPtr defaultHeapId;
            private uint moduleId;
            private uint threads;
            public uint ParentProcessId;
            private int priorityBase;
            private uint flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExeFile;
        }
#pragma warning restore 0169, 0649

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint command);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr window);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
