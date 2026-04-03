using System;

namespace NextcloudUWP.Models
{
    public class CloudFile
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string RemoteId { get; set; }
        public long Size { get; set; }
        public string MimeType { get; set; }
        public bool IsFolder { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string ETag { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsShared { get; set; }
        public string Permissions { get; set; }
        public string ThumbnailUrl { get; set; }
        public bool IsAvailableOffline { get; set; }
        public string LocalPath { get; set; }

        public bool IsImage => MimeType?.StartsWith("image/") == true;
        public bool IsVideo => MimeType?.StartsWith("video/") == true;
        public bool IsAudio => MimeType?.StartsWith("audio/") == true;
        public bool IsDocument => MimeType != null && (
            MimeType.StartsWith("application/pdf") ||
            MimeType.StartsWith("application/vnd") ||
            MimeType.StartsWith("text/"));

        public string DetailText
        {
            get
            {
                var date = ModifiedDate.ToString("MMM dd, yyyy");
                var size = IsFolder ? $"{date}" : $"{FormatSize(Size)} - {date}";
                return size;
            }
        }

        public string SizeText => IsFolder ? "" : FormatSize(Size);

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}
