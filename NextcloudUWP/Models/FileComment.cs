using System;

namespace NextcloudUWP.Models
{
    public class FileComment
    {
        public int      Id               { get; set; }
        public string   Message          { get; set; }
        public string   ActorDisplayName { get; set; }
        public string   ActorId          { get; set; }
        public DateTime CreationDateTime { get; set; }

        public string TimeText => CreationDateTime == DateTime.MinValue
            ? "" : CreationDateTime.ToString("MMM dd HH:mm");
    }
}
