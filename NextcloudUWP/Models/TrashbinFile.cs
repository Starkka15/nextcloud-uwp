using System;

namespace NextcloudUWP.Models
{
    public class TrashbinFile
    {
        public string Name { get; set; }
        public string OriginalFilename { get; set; }
        public string OriginalLocation { get; set; }
        public string TrashbinPath { get; set; }
        public long Size { get; set; }
        public bool IsFolder { get; set; }
        public DateTime DeletionTime { get; set; }

        public string SizeText => IsFolder ? "" : FormatSize(Size);

        public string DetailText
        {
            get
            {
                var date = DeletionTime != DateTime.MinValue
                    ? $"Deleted {DeletionTime:MMM dd, yyyy}"
                    : "Deleted (unknown)";
                return string.IsNullOrEmpty(OriginalLocation)
                    ? date
                    : $"{date} - {OriginalLocation}";
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}
