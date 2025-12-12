using ChatClient.Models.DTO; // Use client DTOs
using ChatServer.Models.DTO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Numerics;
using System.Collections.Generic;
using ChatServer.Models;
using ChatClient.Shared; // Use IChatApiClient from shared project

namespace ChatClient.Services
{
    public class ChatApiClient : IChatApiClient
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "http://localhost:5280"; // Or https://localhost:7252 if using HTTPS

        public ChatApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new System.Uri(BaseUrl);
        }

        public async Task<bool> Register(string login, string password)
        {
            var registerDto = new RegisterDto { Login = login, Password = password };
            var content = new StringContent(JsonSerializer.Serialize(registerDto), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/Users/register", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<int?> Login(string login, string password)
        {
            var loginDto = new LoginDto { Login = login, Password = password };
            var content = new StringContent(JsonSerializer.Serialize(loginDto), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/Users/login", content);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var loginResponse = JsonSerializer.Deserialize<Dictionary<string, int>>(responseContent);
                if (loginResponse != null && loginResponse.ContainsKey("UserId"))
                {
                    return loginResponse["UserId"];
                }
            }
            return null;
        }

        public async Task<bool> SendContactRequest(int userId, int contactId)
        {
            var response = await _httpClient.PostAsync($"/Contacts/request?userId={userId}&contactId={contactId}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AcceptContactRequest(int userId, int contactId)
        {
            var response = await _httpClient.PostAsync($"/Contacts/accept?userId={userId}&contactId={contactId}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeclineContactRequest(int userId, int contactId)
        {
            var response = await _httpClient.PostAsync($"/Contacts/decline?userId={userId}&contactId={contactId}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> RemoveContact(int userId, int contactId)
        {
            var response = await _httpClient.PostAsync($"/Contacts/remove?userId={userId}&contactId={contactId}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<int?> CreateChat(string name, int initialUserId)
        {
            var createChatDto = new CreateChatDto { Name = name, InitialUserId = initialUserId };
            var content = new StringContent(JsonSerializer.Serialize(createChatDto), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/Chats/create", content);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var chat = JsonSerializer.Deserialize<ChatServer.Models.Chat>(responseContent);
            return chat?.Id;
        }

        public async Task<bool> CloseChat(int chatId)
        {
            var response = await _httpClient.PostAsync($"/Chats/close?chatId={chatId}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> JoinChat(int chatId, int userId)
        {
            var response = await _httpClient.PostAsync($"/Chats/join?chatId={chatId}&userId={userId}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> LeaveChat(int chatId, int userId)
        {
            var response = await _httpClient.PostAsync($"/Chats/leave?chatId={chatId}&userId={userId}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<(BigInteger serverPublicKey, BigInteger p, BigInteger g)?> RequestSessionKey(int chatId, int userId, BigInteger clientPublicKey)
        {
            var sessionKeyRequest = new SessionKeyRequestDto
            {
                ChatId = chatId,
                UserId = userId,
                PublicKey = clientPublicKey.ToString()
            };
            var content = new StringContent(JsonSerializer.Serialize(sessionKeyRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/Keys/requestSessionKey", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var serverKeyResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(responseContent);

            if (serverKeyResponse != null && serverKeyResponse.ContainsKey("serverPublicKey") && serverKeyResponse.ContainsKey("p") && serverKeyResponse.ContainsKey("g"))
            {
                return (BigInteger.Parse(serverKeyResponse["serverPublicKey"]), BigInteger.Parse(serverKeyResponse["p"]), BigInteger.Parse(serverKeyResponse["g"]));
            }
            return null;
        }

        public async Task<bool> SendEncryptedFragment(int chatId, int senderId, string encryptedContent)
        {
            var message = new Message
            {
                ChatId = chatId,
                SenderId = senderId,
                Content = encryptedContent
            };
            var content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/Messages/send", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<ChatServer.Models.Message?> ReceiveEncryptedFragment(int chatId, long lastDeliveryId)
        {
            var response = await _httpClient.GetAsync($"/Messages/receive?chatId={chatId}&lastDeliveryId={lastDeliveryId}");
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<Message>(responseContent);
        }
    }
}
