namespace NextcloudUWP.Models
{
    public class OperationEntity
    {
        public int    Id            { get; set; }
        public string OperationType { get; set; }  // "upload", "delete", "rename", "move", "mkdir"
        public string SourcePath    { get; set; }
        public string DestPath      { get; set; }
        public string LocalFilePath { get; set; }
        public string Status        { get; set; }  // "pending", "failed"
        public long   QueuedAt      { get; set; }
        public string ErrorMessage  { get; set; }
    }
}
