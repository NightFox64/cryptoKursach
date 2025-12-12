using ChatServer.Models.DTO;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DiffieHellman;
using System.Numerics;
using System.Collections.Generic;

namespace ChatServer.Tests
{
    public class AuthAndKeyExchangeTests
    {
        private WebApplicationFactory<Program> _factory;
        private HttpClient _client;

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<Program>();
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task Register_And_Login_Success()
        {
            // Register
            var registerDto = new RegisterDto { Login = "testuser", Password = "password" };
            var content = new StringContent(JsonSerializer.Serialize(registerDto), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/Users/register", content);
            response.EnsureSuccessStatusCode();

            // Login
            var loginDto = new LoginDto { Login = "testuser", Password = "password" };
            content = new StringContent(JsonSerializer.Serialize(loginDto), Encoding.UTF8, "application/json");
            response = await _client.PostAsync("/Users/login", content);
            response.EnsureSuccessStatusCode();
        }

        [Test]
        public async Task RequestSessionKey_Success()
        {
            // Register a user
            var registerDto = new RegisterDto { Login = "dhuser", Password = "password" };
            var content = new StringContent(JsonSerializer.Serialize(registerDto), Encoding.UTF8, "application/json");
            await _client.PostAsync("/Users/register", content);

            // Create a chat
            content = new StringContent("", Encoding.UTF8, "application/json");
            var createChatResponse = await _client.PostAsync("/Chats/create?name=testchat&initialUserId=1", content);
            createChatResponse.EnsureSuccessStatusCode();
            
            // Client generates DH keys
            var clientDh = new DiffieHellman.DiffieHellman(1024);

            // Request session key
            var sessionKeyRequest = new SessionKeyRequestDto
            {
                ChatId = 1,
                UserId = 1,
                PublicKey = clientDh.PublicKey.ToString()
            };
            content = new StringContent(JsonSerializer.Serialize(sessionKeyRequest), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/Keys/requestSessionKey", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var serverKeyResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(responseContent);

            Assert.IsNotNull(serverKeyResponse);
            Assert.IsTrue(serverKeyResponse.ContainsKey("serverPublicKey"));
            Assert.IsTrue(serverKeyResponse.ContainsKey("p"));
            Assert.IsTrue(serverKeyResponse.ContainsKey("g"));

            var serverPublicKey = BigInteger.Parse(serverKeyResponse["serverPublicKey"]);
            var p = BigInteger.Parse(serverKeyResponse["p"]);
            var g = BigInteger.Parse(serverKeyResponse["g"]);

            var clientSharedSecret = clientDh.GetSharedSecret(serverPublicKey);
            
            // For now, we'll verify that the client can generate the shared secret.
            Assert.Greater(clientSharedSecret, BigInteger.Zero);
        }
    }
}


