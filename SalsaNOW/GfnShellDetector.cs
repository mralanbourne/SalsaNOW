using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SalsaNOW
{
    internal static class GfnShellDetector
    {
        private static readonly string[] KNOWN_SHELL_NAMES = new string[]
        {
            "CustomExplorer",
            "GFNShell",
            "KioskShell",
            "NvShell",
        };

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static Process FindGfnShell()
        {
            foreach (var name in KNOWN_SHELL_NAMES)
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    for (int i = 1; i < procs.Length; i++) procs[i].Dispose();
                    return procs[0];
                }
            }

            int foundPid = 0;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                var title = new StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);
                if (title.Length == 0) return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0) return true;

                Process proc = null;
                try
                {
                    proc = Process.GetProcessById((int)pid);
                    string procName = proc.ProcessName.ToLowerInvariant();

                    if (procName == "explorer" || procName == "salsanow" ||
                        procName.Contains("shellExperienceHost") ||
                        procName.Contains("SearchUI") ||
                        procName.Contains("ApplicationFrameHost"))
                        return true;

                    try
                    {
                        string exePath = proc.MainModule.FileName;
                        if (exePath.Contains("Asgard") ||
                            exePath.Contains("GeForceNOW") ||
                            exePath.Contains("NVIDIA"))
                        {
                            foundPid = (int)pid;
                            return false;
                        }
                    }
                    catch { }

                    string titleLower = title.ToString().ToLowerInvariant();
                    if (titleLower.Contains("custom") || titleLower.Contains("kiosk") ||
                        titleLower.Contains("gfn") || titleLower.Contains("geforce"))
                    {
                        foundPid = (int)pid;
                        proc.Dispose();
                        return false;
                    }
                }
                catch { }
                finally { proc?.Dispose(); }

                return true;
            }, IntPtr.Zero);

            if (foundPid > 0)
            {
                try { return Process.GetProcessById(foundPid); } catch { }
            }

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.SessionId == 1 && proc.MainWindowHandle != IntPtr.Zero)
                    {
                        string exePath = proc.MainModule.FileName;
                        if (exePath.StartsWith(@"C:\Asgard", StringComparison.OrdinalIgnoreCase))
                            return proc;
                    }
                }
                catch { }
            }

            return null;
        }

        public static IntPtr FindShellWindow()
        {
            foreach (var name in KNOWN_SHELL_NAMES)
            {
                IntPtr hWnd = NativeMethods.FindWindowByCaption(IntPtr.Zero, name);
                if (hWnd != IntPtr.Zero) return hWnd;
            }

            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                var title = new StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);
                if (title.Length == 0) return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                try
                {
                    var proc = Process.GetProcessById((int)pid);
                    if (proc.ProcessName.ToLowerInvariant() == "explorer" ||
                        proc.ProcessName.ToLowerInvariant() == "salsanow")
                        return true;

                    try
                    {
                        string path = proc.MainModule.FileName;
                        if (path.Contains("Asgard") || path.Contains("NVIDIA"))
                        {
                            found = hWnd;
                            return false;
                        }
                    }
                    catch { }
                }
                catch { }
                return true;
            }, IntPtr.Zero);

            return found;
        }
    }
}
