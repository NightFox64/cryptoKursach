using System.ComponentModel.DataAnnotations.Schema;

namespace ChatServer.Models
{
    public class File
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int MessageId { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; } // Or blob storage reference
    }
}
