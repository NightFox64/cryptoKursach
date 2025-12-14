namespace ChatClient.Shared.Models.DTO
{
    public class LoginResponseDto
    {
        public int UserId { get; set; }
        public string AuthToken { get; set; } = string.Empty;
    }
}
