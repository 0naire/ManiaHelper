using System;
using Microsoft.Win32;

internal static class AutoStart {
    const string SettingsKey = @"Software\0naire\ManiaHelper";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "0naireManiaHelper";
    const string Preference = "StartWithOsu";

    public static bool IsEnabled {
        get {
            using (var settings = Registry.CurrentUser.OpenSubKey(SettingsKey))
                return settings != null && Equals(settings.GetValue(Preference), 1);
        }
    }

    public static void RegisterFromSetup() {
        using (var settings = Registry.CurrentUser.OpenSubKey(SettingsKey)) {
            if (settings != null && Equals(settings.GetValue(Preference), 0)) return;
        }
        SetEnabled(true);
    }

    public static void SetEnabled(bool enabled) {
        using (var settings = Registry.CurrentUser.CreateSubKey(SettingsKey))
        using (var startup = Registry.CurrentUser.CreateSubKey(RunKey)) {
            if (enabled)
                startup.SetValue(RunName, "\"" + System.Windows.Forms.Application.ExecutablePath + "\" --watch", RegistryValueKind.String);
            else
                startup.DeleteValue(RunName, false);
            settings.SetValue(Preference, enabled ? 1 : 0, RegistryValueKind.DWord);
        }
    }
}
