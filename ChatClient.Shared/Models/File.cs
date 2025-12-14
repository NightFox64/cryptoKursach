namespace ChatClient.Shared.Models
{
    public class File
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
    }
}
