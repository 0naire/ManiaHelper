using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal static class OsuInstallation {
    const uint QueryLimitedInformation = 0x1000;
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool QueryFullProcessImageName(IntPtr handle, uint flags, StringBuilder filename, ref int length);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

    internal static string ValidExecutable(string candidate) {
        if (String.IsNullOrWhiteSpace(candidate) || !Path.IsPathRooted(candidate)) return null;
        if (!String.Equals(Path.GetFileName(candidate), "osu!.exe", StringComparison.OrdinalIgnoreCase)) return null;
        return File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
    }

    static string ProcessPath(int pid) {
        var handle = OpenProcess(QueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return null;
        try {
            var buffer = new StringBuilder(32768);
            int length = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref length) ? ValidExecutable(buffer.ToString()) : null;
        } finally { CloseHandle(handle); }
    }

    internal static string Resolve(string connected, string saved) {
        uint foreground;
        GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string focused = null;
        bool running = false;
        foreach (var process in Process.GetProcessesByName("osu!")) using (process) {
            running = true;
            string path = ProcessPath(process.Id);
            if (path == null) continue;
            paths.Add(path);
            if (process.Id == foreground) focused = path;
        }
        if (connected != null && paths.Contains(connected)) return connected;
        if (focused != null) return focused;
        if (paths.Count == 1) foreach (var path in paths) return path;
        if (paths.Count > 1) return saved != null && paths.Contains(saved) ? saved : null;
        return ValidExecutable(connected) ?? (running ? null : ValidExecutable(saved));
    }

    internal static string ConfigPath(string executable, string user) {
        var dir = Path.GetDirectoryName(executable);
        var own = Path.Combine(dir, "osu!." + user + ".cfg");
        if (File.Exists(own)) return own;
        var candidates = new List<string>();
        foreach (var path in Directory.GetFiles(dir, "osu!.*.cfg")) {
            if (!String.Equals(Path.GetFileName(path), "osu!.cfg", StringComparison.OrdinalIgnoreCase)) candidates.Add(path);
        }
        if (candidates.Count == 1) return candidates[0];
        throw new FileNotFoundException("선택한 osu!에서 현재 Windows 계정의 설정을 찾을 수 없습니다. 게임을 한 번 실행해 주세요.", own);
    }
}
