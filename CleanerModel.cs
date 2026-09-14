using System;
using System.Collections.Generic;

namespace WinCleanPro
{
    public enum CategoryType
    {
        UserTemp,
        SystemTemp,
        Prefetch,
        WindowsUpdate,
        BrowserCache,
        Thumbnails,
        ErrorReports,
        DnsCache,
        RecycleBin,
        // GPU & Gaming
        GpuShaders,
        // Media & Social Apps
        MediaDiscord,
        MediaSpotify,
        MediaTelegram,
        // Windows System Heavy Dumps
        WindowsMemoryDump,
        DeliveryOptimization,
        // Developer Tooling Caches
        DeveloperCache
    }

    public enum CategoryGroup
    {
        All,
        System,
        Browsers,
        GamingGpu,
        MediaApps,
        Developer
    }

    public enum CleanProfile
    {
        Quick,
        Deep,
        Gamer,
        Developer,
        Custom
    }

    public enum AppState
    {
        Idle,
        Scanning,
        ScanCompleted,
        Cleaning,
        CleanCompleted
    }

    public class CleanerOptions
    {
        public bool SkipRecentFiles24h { get; set; }

        public CleanerOptions()
        {
            SkipRecentFiles24h = false;
        }
    }

    public class FileDetailItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public long SizeBytes { get; set; }
        public string FormattedSize { get { return DiskHelper.FormatBytes(SizeBytes); } }
        public DateTime LastModified { get; set; }
        public string FormattedDate { get { return LastModified.ToString("yyyy-MM-dd HH:mm"); } }
        public bool IsRecent { get { return (DateTime.Now - LastModified).TotalHours < 24; } }
    }

    public class CleanCategoryItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconSymbol { get; set; }
        public CategoryType Type { get; set; }
        public CategoryGroup Group { get; set; }
        public bool IsSelected { get; set; }
        public bool RequiresAdmin { get; set; }
        public int ItemCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public string FormattedSize { get { return DiskHelper.FormatBytes(TotalSizeBytes); } }
        public string StatusText { get; set; }
        public List<string> TargetPaths { get; set; }
        public List<string> DirectFiles { get; set; }
        public List<string> ConflictingProcesses { get; set; }

        public CleanCategoryItem()
        {
            TargetPaths = new List<string>();
            DirectFiles = new List<string>();
            ConflictingProcesses = new List<string>();
            IsSelected = true;
            StatusText = "Pendiente";
            Group = CategoryGroup.System;
        }
    }

    public class ProgressUpdate
    {
        public string CategoryId { get; set; }
        public string CurrentCategoryName { get; set; }
        public string CurrentItemPath { get; set; }
        public double Percentage { get; set; }
        public long BytesCleanedSoFar { get; set; }
        public int ItemsCleanedSoFar { get; set; }
        public int ItemsSkippedSoFar { get; set; }
        public string FormattedBytesCleaned { get { return DiskHelper.FormatBytes(BytesCleanedSoFar); } }
    }

    public class SummaryResult
    {
        public long TotalBytesFreed { get; set; }
        public int TotalItemsDeleted { get; set; }
        public int TotalItemsSkipped { get; set; }
        public TimeSpan Duration { get; set; }
        public string FormattedBytesFreed { get { return DiskHelper.FormatBytes(TotalBytesFreed); } }
    }
}
