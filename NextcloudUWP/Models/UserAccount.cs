using System;
using NextcloudUWP.Services;

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
        public bool IsActive { get; set; }
        public string QuotaDisplay
        {
            get
            {
                var used = FormatHelper.FormatSize(QuotaUsed);
                var total = FormatHelper.FormatSize(QuotaTotal);
                return $"{used} / {total}";
            }
        }
    }
}
