namespace NextcloudUWP.Models
{
    public class ShareInfo
    {
        public int    Id                   { get; set; }
        public string Path                 { get; set; }
        public int    ShareType            { get; set; }  // 0=user, 1=group, 3=public link
        public string ShareWith            { get; set; }
        public string ShareWithDisplayName { get; set; }
        public string Token                { get; set; }
        public string Url                  { get; set; }
        public int    Permissions          { get; set; }  // 1=read 2=update 4=create 8=delete 16=share
        public string Expiration           { get; set; }

        public string ShareTypeLabel
        {
            get
            {
                switch (ShareType)
                {
                    case 0:  return $"User: {ShareWithDisplayName ?? ShareWith}";
                    case 1:  return $"Group: {ShareWith}";
                    case 3:  return "Public link";
                    default: return ShareWith ?? "Unknown";
                }
            }
        }

        public string PermissionsText
        {
            get
            {
                if ((Permissions & 2) != 0) return "Can edit";
                return "Read only";
            }
        }
    }
}
