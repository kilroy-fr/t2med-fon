using System;
using System.Runtime.InteropServices;

namespace T2medFon
{
    /// <summary>Remembers the window the link was clicked in and activates it again before exiting.</summary>
    internal sealed class Foreground
    {
        private readonly IntPtr window;

        public Foreground() { window = GetForegroundWindow(); }

        public IntPtr Window { get { return window; } }

        // Started by a click, the process may still take the foreground; without a window of its own,
        // Windows otherwise hands activation to whatever window comes next in the Z order, not T2med.
        // Returns what happened, for FocusLog.
        public string Restore()
        {
            if (window == IntPtr.Zero || !IsWindow(window)) return "Fenster nicht mehr vorhanden";
            if (IsIconic(window)) return "minimiert, bleibt so"; // minimized meanwhile: leave it to the user
            string result = GetForegroundWindow() == window ? "war vorne" : "SetForegroundWindow " + (SetForegroundWindow(window) ? "ok" : "abgelehnt");
            // Seen on the workstation: T2med (JavaFX) kept the focus but already lay below other windows when the link was handed over.
            bool raised = SetWindowPos(window, HwndTop, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
            return result + ", nach oben " + (raised ? "ok" : "abgelehnt");
        }

        private static readonly IntPtr HwndTop = IntPtr.Zero;
        private const uint SwpNoSize = 0x1, SwpNoMove = 0x2, SwpNoActivate = 0x10;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);
    }
}
