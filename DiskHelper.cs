using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace WinCleanPro
{
    public static class DiskHelper
    {
        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        public static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public const uint SHERB_NOCONFIRMATION = 0x00000001;
        public const uint SHERB_NOPROGRESSUI = 0x00000002;
        public const uint SHERB_NOSOUND = 0x00000004;

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        public static extern int DnsFlushResolverCache();

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";

            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            if (order == 0)
                return string.Format("{0} {1}", (long)len, suffixes[order]);
            else
                return string.Format("{0:0.##} {1}", len, suffixes[order]);
        }

        public static bool IsRunningAsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static void RestartAsAdministrator()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(startInfo);
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("No se pudo iniciar como administrador: " + ex.Message, "SS Clan Cleaner Pro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        public static DriveSpaceInfo GetSystemDriveInfo()
        {
            try
            {
                string systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
                if (string.IsNullOrEmpty(systemDrive)) systemDrive = "C:\\";

                DriveInfo drive = new DriveInfo(systemDrive);
                long total = drive.TotalSize;
                long free = drive.AvailableFreeSpace;
                long used = total - free;
                double percentUsed = total > 0 ? ((double)used / total) * 100.0 : 0.0;

                // Also check total drives count
                int fixedDrivesCount = 0;
                long globalFree = 0;
                try
                {
                    foreach (DriveInfo d in DriveInfo.GetDrives())
                    {
                        if (d.IsReady && d.DriveType == DriveType.Fixed)
                        {
                            fixedDrivesCount++;
                            globalFree += d.AvailableFreeSpace;
                        }
                    }
                }
                catch { }

                return new DriveSpaceInfo
                {
                    DriveLetter = drive.Name,
                    TotalBytes = total,
                    FreeBytes = free,
                    UsedBytes = used,
                    PercentUsed = percentUsed,
                    FormattedTotal = FormatBytes(total),
                    FormattedFree = FormatBytes(free),
                    FormattedUsed = FormatBytes(used),
                    FixedDrivesCount = fixedDrivesCount,
                    GlobalFreeBytes = globalFree,
                    FormattedGlobalFree = FormatBytes(globalFree)
                };
            }
            catch
            {
                return new DriveSpaceInfo
                {
                    DriveLetter = "C:\\",
                    TotalBytes = 0,
                    FreeBytes = 0,
                    UsedBytes = 0,
                    PercentUsed = 0,
                    FormattedTotal = "N/A",
                    FormattedFree = "N/A",
                    FormattedUsed = "N/A",
                    FixedDrivesCount = 1,
                    GlobalFreeBytes = 0,
                    FormattedGlobalFree = "0 B"
                };
            }
        }

        public static System.Collections.Generic.List<string> GetActiveConflictingProcesses(System.Collections.Generic.IEnumerable<string> processNames)
        {
            var active = new System.Collections.Generic.List<string>();
            if (processNames == null) return active;

            foreach (string name in processNames)
            {
                try
                {
                    Process[] procs = Process.GetProcessesByName(name);
                    if (procs != null && procs.Length > 0)
                    {
                        if (!active.Contains(name))
                        {
                            active.Add(name);
                        }
                    }
                }
                catch { }
            }
            return active;
        }

        // CAPA DE SEGURIDAD: procesos críticos que jamás deben forzarse a cerrar,
        // sin importar qué categoría los haya declarado como "conflictivos".
        // Matar el shell de Windows (explorer) o el propio proceso del limpiador
        // puede dejar al usuario sin escritorio/barra de tareas o interrumpir la
        // limpieza a medias.
        private static readonly string[] CriticalProcessNames = { "explorer", "svchost", "csrss", "wininit", "winlogon", "services", "lsass", "smss" };

        public static bool CloseConflictingProcesses(System.Collections.Generic.IEnumerable<string> processNames)
        {
            if (processNames == null) return true;
            bool anyClosed = false;
            string currentProcessName = Process.GetCurrentProcess().ProcessName;

            foreach (string name in processNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                bool isCritical = false;
                foreach (string critical in CriticalProcessNames)
                {
                    if (string.Equals(name, critical, StringComparison.OrdinalIgnoreCase)) { isCritical = true; break; }
                }
                if (isCritical || string.Equals(name, currentProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Nunca forzar el cierre del shell, procesos del sistema o de nosotros mismos.
                }

                try
                {
                    Process[] procs = Process.GetProcessesByName(name);
                    foreach (var p in procs)
                    {
                        try
                        {
                            // Primero un cierre amable; solo se fuerza (Kill) si la app no responde.
                            if (!p.CloseMainWindow())
                            {
                                p.Kill();
                            }
                            anyClosed = true;
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return anyClosed;
        }
    }

    public class DriveSpaceInfo
    {
        public string DriveLetter { get; set; }
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
        public long UsedBytes { get; set; }
        public double PercentUsed { get; set; }
        public string FormattedTotal { get; set; }
        public string FormattedFree { get; set; }
        public string FormattedUsed { get; set; }
        public int FixedDrivesCount { get; set; }
        public long GlobalFreeBytes { get; set; }
        public string FormattedGlobalFree { get; set; }
    }
}
