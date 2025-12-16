namespace ChatClient.Shared.DTO
{
    public class CreateChatDto
    {
        public string? Name { get; set; }
        public int UserId { get; set; }
        public int OtherUserId { get; set; }
        public string? CipherAlgorithm { get; set; }
        public string? CipherMode { get; set; }
        public string? PaddingMode { get; set; }
    }
}
