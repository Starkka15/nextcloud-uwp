using System;

namespace NextcloudUWP.Models
{
    public class NextcloudNotification
    {
        public int NotificationId { get; set; }
        public string App { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string Link { get; set; }
        public DateTime Datetime { get; set; }

        public string TimeText => Datetime == DateTime.MinValue
            ? string.Empty
            : Datetime.ToLocalTime().ToString("MMM dd, HH:mm");

        public string AppLabel => string.IsNullOrEmpty(App) ? "Nextcloud" : App;
    }
}
