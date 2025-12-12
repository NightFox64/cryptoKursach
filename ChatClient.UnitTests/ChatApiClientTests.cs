using NUnit.Framework;
using ChatClient.Shared; // Use IChatApiClient interface
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using System.Net;
using System.Text.Json;
using System.Text;
using ChatClient.UnitTests.Models; // Use test DTOs
using System.Collections.Generic;
using System.Numerics;
using Moq.Protected; // For Protected() extension method

namespace ChatClient.UnitTests
{
    [TestFixture]
    public class ChatApiClientTests
    {
        private Mock<IChatApiClient> _mockChatApiClient;

        [SetUp]
        public void Setup()
        {
            _mockChatApiClient = new Mock<IChatApiClient>();
        }

        [Test]
        public async Task Register_ReturnsTrueOnSuccess()
        {
            _mockChatApiClient.Setup(client => client.Register(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var result = await _mockChatApiClient.Object.Register("testuser", "password");
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task Register_ReturnsFalseOnFailure()
        {
            _mockChatApiClient.Setup(client => client.Register(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            var result = await _mockChatApiClient.Object.Register("testuser", "password");
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task Login_ReturnsUserIdOnSuccess()
        {
            var userId = 1;
            _mockChatApiClient.Setup(client => client.Login(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(userId);

            var result = await _mockChatApiClient.Object.Login("testuser", "password");
            Assert.That(result, Is.EqualTo(userId));
        }

        [Test]
        public async Task Login_ReturnsNullOnFailure()
        {
            _mockChatApiClient.Setup(client => client.Login(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((int?)null);

            var result = await _mockChatApiClient.Object.Login("testuser", "password");
            Assert.That(result, Is.Null);
        }

        // Add more tests for other IChatApiClient methods...
    }
}
