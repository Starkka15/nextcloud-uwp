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

        public bool HasPreview { get; set; }

        public bool IsImage => MimeType?.StartsWith("image/") == true;
        public bool IsVideo => MimeType?.StartsWith("video/") == true;
        public bool IsAudio => MimeType?.StartsWith("audio/") == true;
        public bool IsText => MimeType != null && (MimeType.StartsWith("text/") || MimeType == "application/json" || MimeType == "application/xml");
        public bool IsPdf => MimeType == "application/pdf";
        public bool IsDocument => IsPdf || MimeType?.StartsWith("application/vnd") == true || IsText;

        // Segoe MDL2 Assets glyph for use in file list
        public string IconGlyph
        {
            get
            {
                if (IsFolder) return "\uE8B7";   // Folder
                if (IsImage)  return "\uEB9F";   // Picture
                if (IsVideo)  return "\uE8B2";   // Video
                if (IsAudio)  return "\uE8D6";   // MusicNote
                if (IsPdf)    return "\uEA90";   // PDF
                if (IsText)   return "\uE8A5";   // Document
                return "\uE7C3";                  // Page (generic)
            }
        }

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
        public string FavoriteGlyph => IsFavorite ? "\uE735" : "";

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}
