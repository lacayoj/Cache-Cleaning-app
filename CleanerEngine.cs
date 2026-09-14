using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinCleanPro
{
    public class CleanerEngine
    {
        public event Action<ProgressUpdate> ProgressChanged;
        public event Action<string> LogMessage;

        // =========================================================================
        // CAPA DE SEGURIDAD 1: LISTA BLANCA / NEGRA DE CARPETAS DEL SISTEMA
        // Evita bajo cualquier circunstancia borrar raíces críticas de Windows o usuario.
        // =========================================================================
        public static bool IsPathSafeForCleanup(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string raw = path.Trim();
            if (raw.Length <= 3 || raw.EndsWith(":")) return false;

            try
            {
                string fullPath = Path.GetFullPath(raw).TrimEnd('\\', '/');

                // 1. Prohibir raíces de unidad (C:, D:, C:\, etc.)
                string root = Path.GetPathRoot(fullPath).TrimEnd('\\', '/');
                if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase) || fullPath.Length <= 3)
                {
                    return false;
                }

                // 2. Prohibir carpetas maestras de Windows
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\', '/');
                if (string.Equals(fullPath, winDir, StringComparison.OrdinalIgnoreCase)) return false;

                string sys32 = Environment.SystemDirectory.TrimEnd('\\', '/');
                if (string.Equals(fullPath, sys32, StringComparison.OrdinalIgnoreCase)) return false;

                string sysWow64 = Path.Combine(winDir, "SysWOW64").TrimEnd('\\', '/');
                if (string.Equals(fullPath, sysWow64, StringComparison.OrdinalIgnoreCase)) return false;

                // 3. Prohibir Archivos de Programa
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\', '/');
                if (string.Equals(fullPath, progFiles, StringComparison.OrdinalIgnoreCase)) return false;

                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\', '/');
                if (string.Equals(fullPath, progFilesX86, StringComparison.OrdinalIgnoreCase)) return false;

                // 4. Prohibir raíz del perfil de usuario y carpetas personales
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
                if (string.Equals(fullPath, userProfile, StringComparison.OrdinalIgnoreCase)) return false;

                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop).TrimEnd('\\', '/');
                if (string.Equals(fullPath, desktop, StringComparison.OrdinalIgnoreCase)) return false;

                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments).TrimEnd('\\', '/');
                if (string.Equals(fullPath, docs, StringComparison.OrdinalIgnoreCase)) return false;

                string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures).TrimEnd('\\', '/');
                if (string.Equals(fullPath, pics, StringComparison.OrdinalIgnoreCase)) return false;

                string music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic).TrimEnd('\\', '/');
                if (string.Equals(fullPath, music, StringComparison.OrdinalIgnoreCase)) return false;

                string vids = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos).TrimEnd('\\', '/');
                if (string.Equals(fullPath, vids, StringComparison.OrdinalIgnoreCase)) return false;

                // 5. Prohibir carpetas de datos personales de WhatsApp bajo cualquier condición
                if (fullPath.IndexOf("WhatsApp", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (fullPath.IndexOf("Media", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        fullPath.IndexOf("LocalState", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        fullPath.IndexOf("ChatStorage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        fullPath.IndexOf("databases", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // =========================================================================
        // CAPA DE SEGURIDAD 2: ARCHIVOS DEL SISTEMA PROTEGIDOS
        // Nunca borrar drivers (.sys), consolas (.msc), imágenes de disco ni registros.
        // =========================================================================
        public static bool IsProtectedSystemFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return true;
            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                string fileName = Path.GetFileName(filePath).ToLowerInvariant();

                // Extensiones protegidas críticas del sistema operativo
                if (ext == ".sys" || ext == ".msc" || ext == ".vhd" || ext == ".vhdx" || ext == ".iso")
                {
                    return true;
                }

                // Archivos de registro y arranque de Windows
                if (fileName == "ntuser.dat" || fileName == "sam" || fileName == "security" ||
                    fileName == "software" || fileName == "system" || fileName == "bootmgr" ||
                    fileName == "bcd" || fileName == "boot.ini" || fileName == "desktop.ini")
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return true;
            }
        }

        // =========================================================================
        // CAPA DE SEGURIDAD 3: PROTECCIÓN CONTRA REPARSE POINTS (JUNCTIONS / SYMLINKS)
        // Evita que enlaces simbólicos en carpetas temporales redirijan hacia archivos reales.
        // =========================================================================
        public static bool IsReparsePointOrSymlink(string dirPath)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(dirPath);
                return (di.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
            }
            catch
            {
                return true; // Ante la duda, no entrar
            }
        }

        public List<CleanCategoryItem> GetDefaultCategories()
        {
            List<CleanCategoryItem> categories = new List<CleanCategoryItem>();

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string userTemp = Path.GetTempPath();

            // ==========================================
            // 1. SISTEMA OPERATIVO Y DISCO
            // ==========================================

            // 1.1 User Temp (%TEMP%)
            CleanCategoryItem userTempCat = new CleanCategoryItem
            {
                Id = "user_temp",
                Name = "Caché y Temporales de Usuario",
                Description = "Archivos temporales generados por aplicaciones en tu sesión (%TEMP%).",
                IconSymbol = "⚡",
                Type = CategoryType.UserTemp,
                Group = CategoryGroup.System,
                RequiresAdmin = false,
                IsSelected = true
            };
            AddIfSafe(userTempCat.TargetPaths, userTemp);
            AddIfSafe(userTempCat.TargetPaths, Path.Combine(localAppData, "Temp"));
            categories.Add(userTempCat);

            // 1.2 System Temp (C:\Windows\Temp)
            CleanCategoryItem sysTempCat = new CleanCategoryItem
            {
                Id = "system_temp",
                Name = "Temporales del Sistema Windows",
                Description = "Archivos temporales creados por servicios y el núcleo de Windows (C:\\Windows\\Temp).",
                IconSymbol = "🖥️",
                Type = CategoryType.SystemTemp,
                Group = CategoryGroup.System,
                RequiresAdmin = true,
                IsSelected = true
            };
            AddIfSafe(sysTempCat.TargetPaths, Path.Combine(winDir, "Temp"));
            categories.Add(sysTempCat);

            // 1.3 Prefetch (C:\Windows\Prefetch) - Registros de precarga que Windows recrea automáticamente
            CleanCategoryItem prefetchCat = new CleanCategoryItem
            {
                Id = "prefetch",
                Name = "Archivos Prefetch de Windows",
                Description = "Registros de precarga de aplicaciones antiguas para optimizar el arranque.",
                IconSymbol = "🚀",
                Type = CategoryType.Prefetch,
                Group = CategoryGroup.System,
                RequiresAdmin = true,
                IsSelected = true
            };
            AddIfSafe(prefetchCat.TargetPaths, Path.Combine(winDir, "Prefetch"));
            categories.Add(prefetchCat);

            // 1.4 Windows Update Cache (SoftwareDistribution\Download)
            // NOTA DE SEGURIDAD: Solo se limpia la carpeta Download (paquetes ya instalados).
            // NUNCA se toca DataStore (base de datos histórica de actualizaciones).
            CleanCategoryItem winUpdateCat = new CleanCategoryItem
            {
                Id = "win_update",
                Name = "Caché de Windows Update",
                Description = "Paquetes de instalación de actualizaciones de Windows ya descargados.",
                IconSymbol = "🔄",
                Type = CategoryType.WindowsUpdate,
                Group = CategoryGroup.System,
                RequiresAdmin = true,
                IsSelected = true
            };
            AddIfSafe(winUpdateCat.TargetPaths, Path.Combine(winDir, "SoftwareDistribution", "Download"));
            categories.Add(winUpdateCat);

            // 1.5 Thumbnails & Icon Cache
            // NOTA DE SEGURIDAD: Solo apunta a las bases de datos de miniaturas (.db) de Explorer.
            CleanCategoryItem thumbCat = new CleanCategoryItem
            {
                Id = "thumbnails",
                Name = "Caché de Miniaturas e Íconos",
                Description = "Bases de datos de vista previa de miniaturas del Explorador de Windows.",
                IconSymbol = "🖼️",
                Type = CategoryType.Thumbnails,
                Group = CategoryGroup.System,
                RequiresAdmin = false,
                IsSelected = true
            };
            // NOTA DE SEGURIDAD: NO se agrega "explorer" a ConflictingProcesses.
            // Forzar el cierre del shell de Windows (explorer.exe) puede dejar al
            // usuario sin escritorio ni barra de tareas hasta reiniciarlo manualmente.
            // Los archivos de miniaturas bloqueados simplemente se omiten con seguridad.
            AddIfSafe(thumbCat.TargetPaths, Path.Combine(localAppData, @"Microsoft\Windows\Explorer"));
            string iconCacheFile = Path.Combine(localAppData, "IconCache.db");
            if (File.Exists(iconCacheFile) && IsPathSafeForCleanup(iconCacheFile)) thumbCat.DirectFiles.Add(iconCacheFile);
            categories.Add(thumbCat);

            // 1.6 Error Reports & WER Dumps
            CleanCategoryItem errorCat = new CleanCategoryItem
            {
                Id = "error_reports",
                Name = "Informes de Error y Reportes WER",
                Description = "Informes acumulados de fallos de aplicaciones (Windows Error Reporting).",
                IconSymbol = "📋",
                Type = CategoryType.ErrorReports,
                Group = CategoryGroup.System,
                RequiresAdmin = false,
                IsSelected = true
            };
            AddIfSafe(errorCat.TargetPaths, Path.Combine(localAppData, "CrashDumps"));
            AddIfSafe(errorCat.TargetPaths, Path.Combine(programData, @"Microsoft\Windows\WER\ReportArchive"));
            AddIfSafe(errorCat.TargetPaths, Path.Combine(programData, @"Microsoft\Windows\WER\ReportQueue"));
            categories.Add(errorCat);

            // 1.7 Memory Dumps (BSOD Dumps)
            // NOTA DE SEGURIDAD: Solo archivos MEMORY.DMP y Minidump (volcados tras pantalla azul).
            CleanCategoryItem memDumpCat = new CleanCategoryItem
            {
                Id = "memory_dumps",
                Name = "Volcados de Memoria del Sistema (MEMORY.DMP)",
                Description = "Volcados completos generados tras pantallas azules (pueden pesar entre 1 GB y 16 GB).",
                IconSymbol = "💾",
                Type = CategoryType.WindowsMemoryDump,
                Group = CategoryGroup.System,
                RequiresAdmin = true,
                IsSelected = false
            };
            string memDmp = Path.Combine(winDir, "MEMORY.DMP");
            if (File.Exists(memDmp) && IsPathSafeForCleanup(memDmp)) memDumpCat.DirectFiles.Add(memDmp);
            AddIfSafe(memDumpCat.TargetPaths, Path.Combine(winDir, "Minidump"));
            categories.Add(memDumpCat);

            // 1.8 Delivery Optimization Cache (Archivos compartidos de actualización P2P de Windows)
            CleanCategoryItem doCat = new CleanCategoryItem
            {
                Id = "delivery_opt",
                Name = "Caché de Optimización de Entrega",
                Description = "Archivos descargados en caché para compartir actualizaciones en la red local.",
                IconSymbol = "🚚",
                Type = CategoryType.DeliveryOptimization,
                Group = CategoryGroup.System,
                RequiresAdmin = true,
                IsSelected = false
            };
            AddIfSafe(doCat.TargetPaths, Path.Combine(winDir, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache"));
            categories.Add(doCat);

            // 1.9 DNS Cache
            CleanCategoryItem dnsCat = new CleanCategoryItem
            {
                Id = "dns_cache",
                Name = "Caché DNS del Sistema",
                Description = "Vacía la resolución de nombres DNS para corregir errores de conexión.",
                IconSymbol = "📡",
                Type = CategoryType.DnsCache,
                Group = CategoryGroup.System,
                RequiresAdmin = false,
                IsSelected = true
            };
            categories.Add(dnsCat);

            // 1.10 Recycle Bin
            CleanCategoryItem recycleCat = new CleanCategoryItem
            {
                Id = "recycle_bin",
                Name = "Papelera de Reciclaje",
                Description = "Elimina permanentemente los archivos en la papelera de reciclaje de todos los discos.",
                IconSymbol = "🗑️",
                Type = CategoryType.RecycleBin,
                Group = CategoryGroup.System,
                RequiresAdmin = false,
                IsSelected = false
            };
            categories.Add(recycleCat);

            // ==========================================
            // 2. NAVEGADORES WEB (Caché pura; NO contraseñas, NO historial, NO cookies)
            // ==========================================
            CleanCategoryItem browserCat = new CleanCategoryItem
            {
                Id = "browser_cache",
                Name = "Caché de Navegadores Web",
                Description = "Caché web, GPU y scripts temporales de Google Chrome, Edge, Brave y Firefox.",
                IconSymbol = "🌐",
                Type = CategoryType.BrowserCache,
                Group = CategoryGroup.Browsers,
                RequiresAdmin = false,
                IsSelected = true
            };
            browserCat.ConflictingProcesses.AddRange(new[] { "chrome", "msedge", "brave", "firefox" });

            // Chrome: solo carpetas 'Cache', 'GPUCache', 'Code Cache'
            AddIfSafe(browserCat.TargetPaths, Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache"));
            AddIfSafe(browserCat.TargetPaths, Path.Combine(localAppData, @"Google\Chrome\User Data\Default\GPUCache"));
            AddIfSafe(browserCat.TargetPaths, Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Code Cache\js"));
            AddIfSafe(browserCat.TargetPaths, Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Service Worker\CacheStorage"));

            // Edge: solo carpetas 'Cache', 'GPUCache', 'Code Cache'
            AddIfSafe(browserCat.TargetPaths, Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache"));
            AddIfSafe(browserCat.TargetPaths, Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\GPUCache"));
            AddIfSafe(browserCat.TargetPaths, Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Code Cache\js"));
            AddIfSafe(browserCat.TargetPaths, Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Service Worker\CacheStorage"));

            // Brave
            AddIfSafe(browserCat.TargetPaths, Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"));
            AddIfSafe(browserCat.TargetPaths, Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\GPUCache"));

            // Firefox: únicamente 'cache2'
            string ffProfiles = Path.Combine(localAppData, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(ffProfiles))
            {
                try
                {
                    foreach (string prof in Directory.GetDirectories(ffProfiles))
                    {
                        AddIfSafe(browserCat.TargetPaths, Path.Combine(prof, "cache2"));
                    }
                }
                catch { }
            }
            categories.Add(browserCat);

            // ==========================================
            // 3. GRÁFICOS Y JUEGOS (GPU SHADERS)
            // ==========================================
            CleanCategoryItem gpuCat = new CleanCategoryItem
            {
                Id = "gpu_shaders",
                Name = "Caché de Shaders (DirectX, NVIDIA, AMD)",
                Description = "Shaders compilados de tu tarjeta gráfica. Su purga libera gigas y previene stuttering en juegos.",
                IconSymbol = "🎮",
                Type = CategoryType.GpuShaders,
                Group = CategoryGroup.GamingGpu,
                RequiresAdmin = false,
                IsSelected = false
            };
            AddIfSafe(gpuCat.TargetPaths, Path.Combine(localAppData, "D3DSCache"));
            AddIfSafe(gpuCat.TargetPaths, Path.Combine(localAppData, @"NVIDIA\DXCache"));
            AddIfSafe(gpuCat.TargetPaths, Path.Combine(localAppData, @"NVIDIA\GLCache"));
            AddIfSafe(gpuCat.TargetPaths, Path.Combine(localAppData, @"NVIDIA Corporation\NV_Cache"));
            AddIfSafe(gpuCat.TargetPaths, Path.Combine(localAppData, @"AMD\DxCache"));
            AddIfSafe(gpuCat.TargetPaths, Path.Combine(localAppData, @"AMD\DxcCache"));
            categories.Add(gpuCat);

            // ==========================================
            // 4. APLICACIONES Y MENSAJERÍA (100% SEGURO)
            // ==========================================

            // Discord: Solo carpetas 'Cache', 'Code Cache', 'GPUCache'
            CleanCategoryItem discordCat = new CleanCategoryItem
            {
                Id = "media_discord",
                Name = "Caché de Discord",
                Description = "Caché de imágenes, avatares y archivos temporales de Discord.",
                IconSymbol = "💬",
                Type = CategoryType.MediaDiscord,
                Group = CategoryGroup.MediaApps,
                RequiresAdmin = false,
                IsSelected = false
            };
            discordCat.ConflictingProcesses.Add("discord");
            AddIfSafe(discordCat.TargetPaths, Path.Combine(appData, @"discord\Cache"));
            AddIfSafe(discordCat.TargetPaths, Path.Combine(appData, @"discord\Code Cache\js"));
            AddIfSafe(discordCat.TargetPaths, Path.Combine(appData, @"discord\GPUCache"));
            categories.Add(discordCat);

            // Spotify: Solo 'Storage' y 'Data' temporales
            CleanCategoryItem spotifyCat = new CleanCategoryItem
            {
                Id = "media_spotify",
                Name = "Caché de Spotify",
                Description = "Pistas cacheadas y portadas temporales descargadas por Spotify.",
                IconSymbol = "🎵",
                Type = CategoryType.MediaSpotify,
                Group = CategoryGroup.MediaApps,
                RequiresAdmin = false,
                IsSelected = false
            };
            spotifyCat.ConflictingProcesses.Add("spotify");
            AddIfSafe(spotifyCat.TargetPaths, Path.Combine(localAppData, @"Spotify\Storage"));
            AddIfSafe(spotifyCat.TargetPaths, Path.Combine(localAppData, @"Spotify\Data"));
            categories.Add(spotifyCat);

            // Telegram: ÚNICAMENTE 'tdata\user_data\cache' (stickers y vistas previas en la nube).
            // NUNCA toca llaves de sesión ni mensajes.
            CleanCategoryItem telegramCat = new CleanCategoryItem
            {
                Id = "media_telegram",
                Name = "Caché de Telegram (Stickers y Vistas Previas)",
                Description = "Caché temporal en la nube de Telegram (se vuelve a sincronizar si la necesitas; tus fotos quedan seguras).",
                IconSymbol = "✈️",
                Type = CategoryType.MediaTelegram,
                Group = CategoryGroup.MediaApps,
                RequiresAdmin = false,
                IsSelected = false
            };
            telegramCat.ConflictingProcesses.Add("telegram");
            AddIfSafe(telegramCat.TargetPaths, Path.Combine(appData, @"Telegram Desktop\tdata\user_data\cache"));
            categories.Add(telegramCat);

            // NOTA DE SEGURIDAD ABSOLUTA: WhatsApp no se incluye para garantizar 0 riesgo en fotos familiares o laborales.

            // ==========================================
            // 5. HERRAMIENTAS DE DESARROLLADOR (Solo caché descargable/regenerable)
            // ==========================================
            // NOTA DE SEGURIDAD: Solo se tocan cachés HTTP/temporales que las herramientas
            // vuelven a descargar o regenerar automáticamente. NUNCA se toca el almacén de
            // paquetes ya instalados (p.ej. ~/.nuget/packages, node_modules, venv), código
            // fuente, ni configuración/credenciales (npmrc, .gitconfig, tokens).
            CleanCategoryItem devCat = new CleanCategoryItem
            {
                Id = "dev_cache",
                Name = "Caché de Herramientas de Desarrollador",
                Description = "Caché temporal de npm, Yarn, pip, NuGet (HTTP) y Visual Studio Code. No borra paquetes instalados ni proyectos.",
                IconSymbol = "💻",
                Type = CategoryType.DeveloperCache,
                Group = CategoryGroup.Developer,
                RequiresAdmin = false,
                IsSelected = false
            };

            // npm: caché de paquetes descargados (se re-descarga automáticamente)
            AddIfSafe(devCat.TargetPaths, Path.Combine(appData, "npm-cache"));

            // Yarn: caché de paquetes (v1 clásico y v2+ Berry)
            AddIfSafe(devCat.TargetPaths, Path.Combine(localAppData, @"Yarn\Cache"));
            AddIfSafe(devCat.TargetPaths, Path.Combine(localAppData, @"Yarn\Berry\cache"));

            // pip: caché de wheels/descargas de paquetes de Python
            AddIfSafe(devCat.TargetPaths, Path.Combine(localAppData, @"pip\Cache"));

            // NuGet: ÚNICAMENTE la caché de respuestas HTTP temporal.
            // NUNCA la carpeta de paquetes restaurados (~/.nuget/packages), que muchos
            // proyectos necesitan para compilar sin conexión.
            AddIfSafe(devCat.TargetPaths, Path.Combine(localAppData, @"NuGet\v3-cache"));
            AddIfSafe(devCat.TargetPaths, Path.Combine(localAppData, @"NuGet\plugins-cache"));

            // Visual Studio Code: caché de disco, no configuración ni extensiones
            AddIfSafe(devCat.TargetPaths, Path.Combine(appData, @"Code\Cache"));
            AddIfSafe(devCat.TargetPaths, Path.Combine(appData, @"Code\CachedData"));
            AddIfSafe(devCat.TargetPaths, Path.Combine(appData, @"Code\Code Cache"));
            AddIfSafe(devCat.TargetPaths, Path.Combine(appData, @"Code\GPUCache"));

            if (devCat.TargetPaths.Count > 0)
            {
                devCat.ConflictingProcesses.Add("Code");
                categories.Add(devCat);
            }

            return categories;
        }

        private void AddIfSafe(List<string> list, string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string normalized = Path.GetFullPath(path).TrimEnd('\\', '/');
                    if (Directory.Exists(normalized) && IsPathSafeForCleanup(normalized))
                    {
                        bool exists = false;
                        foreach (string item in list)
                        {
                            if (string.Equals(Path.GetFullPath(item).TrimEnd('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase))
                            {
                                exists = true;
                                break;
                            }
                        }
                        if (!exists)
                        {
                            list.Add(normalized);
                        }
                    }
                }
                catch { }
            }
        }

        public void ScanCategories(List<CleanCategoryItem> categories, Action<CleanCategoryItem> onCategoryScanned)
        {
            foreach (var cat in categories)
            {
                cat.ItemCount = 0;
                cat.TotalSizeBytes = 0;
                cat.StatusText = "Analizando...";

                if (cat.Type == CategoryType.DnsCache)
                {
                    cat.ItemCount = 1;
                    cat.TotalSizeBytes = 0;
                    cat.StatusText = "Listo para purgar";
                }
                else if (cat.Type == CategoryType.RecycleBin)
                {
                    cat.StatusText = "Listo para vaciar";
                }
                else
                {
                    long totalSize = 0;
                    int count = 0;

                    // Archivos directos seguros
                    if (cat.DirectFiles != null)
                    {
                        foreach (string file in cat.DirectFiles)
                        {
                            try
                            {
                                if (File.Exists(file) && IsPathSafeForCleanup(file) && !IsProtectedSystemFile(file))
                                {
                                    FileInfo fi = new FileInfo(file);
                                    totalSize += fi.Length;
                                    count++;
                                }
                            }
                            catch { }
                        }
                    }

                    // Carpetas destino seguras
                    if (cat.TargetPaths != null)
                    {
                        foreach (string path in cat.TargetPaths)
                        {
                            if (Directory.Exists(path) && IsPathSafeForCleanup(path))
                            {
                                ScanDirectory(path, ref count, ref totalSize);
                            }
                        }
                    }

                    cat.ItemCount = count;
                    cat.TotalSizeBytes = totalSize;
                    cat.StatusText = count > 0 ? string.Format("{0} ({1} archivos)", DiskHelper.FormatBytes(totalSize), count) : "Limpio (0 archivos)";
                }

                if (onCategoryScanned != null)
                {
                    onCategoryScanned(cat);
                }
            }
        }

        private void ScanDirectory(string dirPath, ref int count, ref long totalSize)
        {
            // Seguridad: no escanear enlaces simbólicos/junctions
            if (IsReparsePointOrSymlink(dirPath)) return;

            try
            {
                DirectoryInfo dir = new DirectoryInfo(dirPath);
                FileInfo[] files = dir.GetFiles("*", SearchOption.TopDirectoryOnly);
                foreach (FileInfo f in files)
                {
                    try
                    {
                        if (!IsProtectedSystemFile(f.FullName))
                        {
                            totalSize += f.Length;
                            count++;
                        }
                    }
                    catch { }
                }

                DirectoryInfo[] subDirs = dir.GetDirectories();
                foreach (DirectoryInfo subDir in subDirs)
                {
                    if (!IsReparsePointOrSymlink(subDir.FullName) && IsPathSafeForCleanup(subDir.FullName))
                    {
                        ScanDirectory(subDir.FullName, ref count, ref totalSize);
                    }
                }
            }
            catch { }
        }

        public SummaryResult CleanCategories(List<CleanCategoryItem> categories, CleanerOptions options, CancellationToken cancellationToken)
        {
            if (options == null) options = new CleanerOptions();

            DateTime startTime = DateTime.Now;
            long totalBytesCleaned = 0;
            int totalFilesDeleted = 0;
            int totalFilesSkipped = 0;

            int totalEstimatedWork = 0;
            foreach (var cat in categories)
            {
                if (cat.IsSelected)
                {
                    totalEstimatedWork += Math.Max(1, cat.ItemCount);
                }
            }
            if (totalEstimatedWork == 0) totalEstimatedWork = 1;

            int processedWork = 0;

            foreach (var cat in categories)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!cat.IsSelected) continue;

                Log(string.Format("== Iniciando limpieza: {0} ==", cat.Name));

                // 1. DNS Flush
                if (cat.Type == CategoryType.DnsCache)
                {
                    try
                    {
                        DiskHelper.DnsFlushResolverCache();
                        Log("✓ Caché DNS purgado correctamente.");
                    }
                    catch (Exception ex)
                    {
                        Log("! Error purgando DNS: " + ex.Message);
                    }
                    processedWork++;
                    ReportProgress(cat.Id, cat.Name, "Caché DNS purgado", (double)processedWork / totalEstimatedWork * 100.0, totalBytesCleaned, totalFilesDeleted, totalFilesSkipped);
                    continue;
                }

                // 2. Papelera de reciclaje oficial de Windows
                if (cat.Type == CategoryType.RecycleBin)
                {
                    try
                    {
                        DiskHelper.SHEmptyRecycleBin(IntPtr.Zero, null, DiskHelper.SHERB_NOCONFIRMATION | DiskHelper.SHERB_NOPROGRESSUI | DiskHelper.SHERB_NOSOUND);
                        Log("✓ Papelera de reciclaje vaciada.");
                    }
                    catch (Exception ex)
                    {
                        Log("! Error vaciando papelera: " + ex.Message);
                    }
                    processedWork++;
                    ReportProgress(cat.Id, cat.Name, "Papelera vaciada", (double)processedWork / totalEstimatedWork * 100.0, totalBytesCleaned, totalFilesDeleted, totalFilesSkipped);
                    continue;
                }

                // 3. Archivos directos seguros (ej. MEMORY.DMP)
                if (cat.DirectFiles != null)
                {
                    foreach (string filePath in cat.DirectFiles)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        // Blindaje de seguridad
                        if (!IsPathSafeForCleanup(filePath) || IsProtectedSystemFile(filePath))
                        {
                            totalFilesSkipped++;
                            processedWork++;
                            continue;
                        }

                        if (File.Exists(filePath))
                        {
                            try
                            {
                                FileInfo fi = new FileInfo(filePath);
                                if (options.SkipRecentFiles24h && (DateTime.Now - fi.LastWriteTime).TotalHours < 24)
                                {
                                    totalFilesSkipped++;
                                    processedWork++;
                                    Log(string.Format("Protegido por filtro 24h: {0}", Path.GetFileName(filePath)));
                                    continue;
                                }

                                long len = fi.Length;
                                File.SetAttributes(filePath, FileAttributes.Normal);
                                File.Delete(filePath);
                                totalBytesCleaned += len;
                                totalFilesDeleted++;
                                processedWork++;
                                Log(string.Format("✓ Eliminado: {0}", Path.GetFileName(filePath)));
                            }
                            catch (Exception ex)
                            {
                                totalFilesSkipped++;
                                processedWork++;
                                Log(string.Format("! Omitido (en uso): {0} - {1}", Path.GetFileName(filePath), ex.Message));
                            }
                        }
                    }
                }

                // 4. Carpetas destino seguras
                if (cat.TargetPaths != null)
                {
                    foreach (string targetPath in cat.TargetPaths)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        if (!Directory.Exists(targetPath) || !IsPathSafeForCleanup(targetPath)) continue;

                        CleanDirectoryContents(targetPath, cat.Id, cat.Name, options, ref totalBytesCleaned, ref totalFilesDeleted, ref totalFilesSkipped, ref processedWork, totalEstimatedWork, cancellationToken);
                    }
                }

                // Recalcular con precision el estado real tras la limpieza
                if (cat.Type == CategoryType.DnsCache)
                {
                    cat.ItemCount = 0;
                    cat.TotalSizeBytes = 0;
                    cat.StatusText = "✓ Purgado";
                }
                else if (cat.Type == CategoryType.RecycleBin)
                {
                    cat.ItemCount = 0;
                    cat.TotalSizeBytes = 0;
                    cat.StatusText = "✓ Vaciada";
                }
                else
                {
                    long remainingBytes = 0;
                    int remainingCount = 0;
                    if (cat.DirectFiles != null)
                    {
                        foreach (string f in cat.DirectFiles)
                        {
                            if (File.Exists(f))
                            {
                                try
                                {
                                    FileInfo fi = new FileInfo(f);
                                    remainingBytes += fi.Length;
                                    remainingCount++;
                                }
                                catch { }
                            }
                        }
                    }
                    if (cat.TargetPaths != null)
                    {
                        foreach (string p in cat.TargetPaths)
                        {
                            if (Directory.Exists(p))
                            {
                                ScanDirectory(p, ref remainingCount, ref remainingBytes);
                            }
                        }
                    }
                    cat.ItemCount = remainingCount;
                    cat.TotalSizeBytes = remainingBytes;
                    if (remainingCount == 0)
                    {
                        cat.StatusText = "✓ Limpio (0 B)";
                    }
                    else
                    {
                        cat.StatusText = string.Format("✓ Limpio ({0} en uso por Windows)", remainingCount);
                    }
                }
            }

            TimeSpan duration = DateTime.Now - startTime;
            Log(string.Format("== Limpieza finalizada en {0:0.0}s. Liberado: {1}, Eliminados: {2}, Protegidos/Omitidos: {3} ==",
                duration.TotalSeconds, DiskHelper.FormatBytes(totalBytesCleaned), totalFilesDeleted, totalFilesSkipped));

            return new SummaryResult
            {
                TotalBytesFreed = totalBytesCleaned,
                TotalItemsDeleted = totalFilesDeleted,
                TotalItemsSkipped = totalFilesSkipped,
                Duration = duration
            };
        }

        private void CleanDirectoryContents(string dirPath, string catId, string catName, CleanerOptions options, ref long totalBytes, ref int totalDeleted, ref int totalSkipped, ref int processedWork, int totalEstimatedWork, CancellationToken token)
        {
            // Seguridad: nunca ingresar en enlaces simbólicos / uniones NTFS
            if (IsReparsePointOrSymlink(dirPath) || !IsPathSafeForCleanup(dirPath)) return;

            // 1. Limpieza de archivos
            try
            {
                string[] files = Directory.GetFiles(dirPath);
                foreach (string file in files)
                {
                    if (token.IsCancellationRequested) return;

                    // Seguridad: nunca tocar archivos protegidos del sistema
                    if (IsProtectedSystemFile(file) || !IsPathSafeForCleanup(file))
                    {
                        totalSkipped++;
                        processedWork++;
                        continue;
                    }

                    try
                    {
                        FileInfo fi = new FileInfo(file);

                        // Regla de protección de 24 horas para instaladores activos
                        if (options.SkipRecentFiles24h && (DateTime.Now - fi.LastWriteTime).TotalHours < 24)
                        {
                            totalSkipped++;
                            processedWork++;
                            continue;
                        }

                        long len = fi.Length;
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);

                        totalBytes += len;
                        totalDeleted++;
                        processedWork++;

                        string fileName = Path.GetFileName(file);
                        ReportProgress(catId, catName, fileName, Math.Min(99.0, (double)processedWork / totalEstimatedWork * 100.0), totalBytes, totalDeleted, totalSkipped);

                        if (totalDeleted % 10 == 0)
                        {
                            Thread.Sleep(1);
                        }
                    }
                    catch
                    {
                        // Archivo bloqueado por Windows o proceso en ejecución -> omitir con seguridad
                        totalSkipped++;
                        processedWork++;
                    }
                }
            }
            catch { }

            // 2. Limpieza recursiva de subcarpetas seguras
            try
            {
                string[] subDirs = Directory.GetDirectories(dirPath);
                foreach (string subDir in subDirs)
                {
                    if (token.IsCancellationRequested) return;

                    // Si es symlink o junction, no entrar
                    if (IsReparsePointOrSymlink(subDir) || !IsPathSafeForCleanup(subDir))
                    {
                        continue;
                    }

                    CleanDirectoryContents(subDir, catId, catName, options, ref totalBytes, ref totalDeleted, ref totalSkipped, ref processedWork, totalEstimatedWork, token);

                    // Borrar subcarpeta únicamente si quedó 100% vacía
                    try
                    {
                        if (Directory.GetFileSystemEntries(subDir).Length == 0)
                        {
                            Directory.Delete(subDir, false);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public List<FileDetailItem> GetTopFilesForCategory(CleanCategoryItem cat, int maxFiles = 80)
        {
            List<FileDetailItem> items = new List<FileDetailItem>();
            if (cat == null) return items;

            // Archivos directos
            if (cat.DirectFiles != null)
            {
                foreach (string file in cat.DirectFiles)
                {
                    try
                    {
                        if (File.Exists(file) && IsPathSafeForCleanup(file) && !IsProtectedSystemFile(file))
                        {
                            FileInfo fi = new FileInfo(file);
                            items.Add(new FileDetailItem
                            {
                                Name = fi.Name,
                                FullPath = fi.FullName,
                                SizeBytes = fi.Length,
                                LastModified = fi.LastWriteTime
                            });
                        }
                    }
                    catch { }
                }
            }

            // Carpetas destino
            if (cat.TargetPaths != null)
            {
                foreach (string dir in cat.TargetPaths)
                {
                    if (Directory.Exists(dir) && IsPathSafeForCleanup(dir))
                    {
                        CollectFilesFromDirectory(dir, items, 0, 4);
                    }
                }
            }

            // Ordenar de mayor a menor tamaño
            items.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));

            if (items.Count > maxFiles)
            {
                items = items.GetRange(0, maxFiles);
            }

            return items;
        }

        private void CollectFilesFromDirectory(string dirPath, List<FileDetailItem> list, int currentDepth, int maxDepth)
        {
            if (currentDepth > maxDepth) return;
            if (IsReparsePointOrSymlink(dirPath) || !IsPathSafeForCleanup(dirPath)) return;

            try
            {
                string[] files = Directory.GetFiles(dirPath);
                foreach (string f in files)
                {
                    try
                    {
                        if (!IsProtectedSystemFile(f))
                        {
                            FileInfo fi = new FileInfo(f);
                            list.Add(new FileDetailItem
                            {
                                Name = fi.Name,
                                FullPath = fi.FullName,
                                SizeBytes = fi.Length,
                                LastModified = fi.LastWriteTime
                            });
                        }
                    }
                    catch { }
                }

                string[] subDirs = Directory.GetDirectories(dirPath);
                foreach (string sd in subDirs)
                {
                    if (!IsReparsePointOrSymlink(sd) && IsPathSafeForCleanup(sd))
                    {
                        CollectFilesFromDirectory(sd, list, currentDepth + 1, maxDepth);
                    }
                }
            }
            catch { }
        }

        private void ReportProgress(string catId, string catName, string currentItem, double pct, long bytesCleaned, int itemsCleaned, int itemsSkipped)
        {
            if (ProgressChanged != null)
            {
                ProgressChanged(new ProgressUpdate
                {
                    CategoryId = catId,
                    CurrentCategoryName = catName,
                    CurrentItemPath = currentItem,
                    Percentage = pct,
                    BytesCleanedSoFar = bytesCleaned,
                    ItemsCleanedSoFar = itemsCleaned,
                    ItemsSkippedSoFar = itemsSkipped
                });
            }
        }

        private void Log(string message)
        {
            if (LogMessage != null)
            {
                LogMessage(string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, message));
            }
        }
    }
}
