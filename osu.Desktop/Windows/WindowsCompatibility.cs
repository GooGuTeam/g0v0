// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;
using osu.Framework.Logging;

namespace osu.Desktop.Windows
{
    [SupportedOSPlatform("windows")]
    public static class WindowsCompatibility
    {
        private const string app_compat_layers_key = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        private const string disable_fso_flag = "DISABLEDXMAXIMIZEDWINDOWEDMODE";

        /// <summary>
        /// Checks if "Disable fullscreen optimizations" (禁用全屏优化) is enabled for the current application.
        /// If not, writes the compatibility flag to the current user's registry before any graphics components are loaded.
        /// </summary>
        public static void EnsureFullscreenOptimizationsDisabled()
        {
            try
            {
                string? exePath = getExecutablePath();
                if (string.IsNullOrEmpty(exePath))
                {
                    Logger.Log(@"Could not determine application executable path for compatibility configuration.", LoggingTarget.Runtime, LogLevel.Verbose);
                    return;
                }

                if (isFullscreenOptimizationsDisabled(exePath))
                {
                    Logger.Log(@"Fullscreen optimizations already disabled in registry.", LoggingTarget.Runtime, LogLevel.Verbose);
                    return;
                }

                setFullscreenOptimizationsDisabled(exePath);
                Logger.Log($@"Disabled fullscreen optimizations in registry for: {exePath}", LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception ex)
            {
                Logger.Log($@"Failed to update fullscreen optimizations in registry: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }
        }

        private static bool isFullscreenOptimizationsDisabled(string exePath)
        {
            // Check HKLM first (machine-wide settings)
            try
            {
                using var hklmKey = Registry.LocalMachine.OpenSubKey(app_compat_layers_key, writable: false);
                if (hklmKey?.GetValue(exePath) is string hklmVal && hklmVal.Contains(disable_fso_flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                // Accessing HKLM might fail due to security policy; ignore and proceed to HKCU
            }

            // Check HKCU (user-specific settings)
            try
            {
                using var hkcuKey = Registry.CurrentUser.OpenSubKey(app_compat_layers_key, writable: false);
                if (hkcuKey?.GetValue(exePath) is string hkcuVal && hkcuVal.Contains(disable_fso_flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                // Ignored
            }

            return false;
        }

        private static void setFullscreenOptimizationsDisabled(string exePath)
        {
            using var hkcuKey = Registry.CurrentUser.CreateSubKey(app_compat_layers_key, writable: true);
            if (hkcuKey == null)
            {
                Logger.Log(@"Failed to open or create HKCU AppCompatFlags\Layers registry key.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            object? existing = hkcuKey.GetValue(exePath);
            string newVal;

            if (existing is string existingStr && !string.IsNullOrWhiteSpace(existingStr))
            {
                if (existingStr.Contains(disable_fso_flag, StringComparison.OrdinalIgnoreCase))
                    return;

                newVal = $"{existingStr.TrimEnd()} {disable_fso_flag}";
            }
            else
            {
                newVal = $"~ {disable_fso_flag}";
            }

            hkcuKey.SetValue(exePath, newVal, RegistryValueKind.String);
        }

        private static string? getExecutablePath()
        {
            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && !Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(processPath);

            string assemblyLocation = typeof(WindowsCompatibility).Assembly.Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                string candidate = Path.ChangeExtension(assemblyLocation, ".exe");
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }

            string baseDirCandidate = Path.Combine(AppContext.BaseDirectory, "g0v0!.exe");
            if (File.Exists(baseDirCandidate))
                return Path.GetFullPath(baseDirCandidate);

            string osuCandidate = Path.Combine(AppContext.BaseDirectory, "osu!.exe");
            if (File.Exists(osuCandidate))
                return Path.GetFullPath(osuCandidate);

            return !string.IsNullOrEmpty(processPath) ? Path.GetFullPath(processPath) : null;
        }
    }
}
