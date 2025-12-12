using ChatServer.Models.DTO;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatServer.Tests
{
    public class ContactsControllerTests
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
        public async Task Send_And_Accept_Contact_Request_Success()
        {
            // Register two users
            var user1 = new RegisterDto { Login = "user1", Password = "password" };
            var content = new StringContent(JsonSerializer.Serialize(user1), Encoding.UTF8, "application/json");
            await _client.PostAsync("/Users/register", content);

            var user2 = new RegisterDto { Login = "user2", Password = "password" };
            content = new StringContent(JsonSerializer.Serialize(user2), Encoding.UTF8, "application/json");
            await _client.PostAsync("/Users/register", content);

            // user1 sends a contact request to user2
            content = new StringContent("", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/Contacts/request?userId=1&contactId=2", content);
            response.EnsureSuccessStatusCode();

            // user2 accepts the contact request from user1
            content = new StringContent("", Encoding.UTF8, "application/json");
            response = await _client.PostAsync("/Contacts/accept?userId=2&contactId=1", content);
            response.EnsureSuccessStatusCode();
        }
    }
}
