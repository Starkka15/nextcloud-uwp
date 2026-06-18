using System;
using NextcloudUWP.Services;

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

        public string SizeText => IsFolder ? "" : FormatHelper.FormatSize(Size);

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
    }
}
