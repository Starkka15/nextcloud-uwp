using System;

namespace NextcloudUWP.Models
{
    public class UserAccount
    {
        public string Id { get; set; }
        public string ServerUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string AccessToken { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public long QuotaTotal { get; set; }
        public long QuotaUsed { get; set; }
        public string QuotaDisplay
        {
            get
            {
                var used = FormatSize(QuotaUsed);
                var total = FormatSize(QuotaTotal);
                return $"{used} / {total}";
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
