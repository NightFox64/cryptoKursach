using ChatServer.Services;
using DiffieHellman;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Numerics;
using ChatClient.Shared.Models.DTO;

namespace ChatServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class KeysController : ControllerBase
    {
        private readonly ISessionKeyService _sessionKeyService;

        public KeysController(ISessionKeyService sessionKeyService)
        {
            _sessionKeyService = sessionKeyService;
        }

        [HttpPost("requestSessionKey")]
        public IActionResult RequestSessionKey(SessionKeyRequestDto model)
        {
            try
            {
                // For simplicity, we'll use pre-defined DH parameters.
                // In a real application, the server should have its own long-term DH parameters
                // or they should be agreed upon in a secure way.
                // I will generate a new DH instance with 1024-bit key size.
                var serverDh = new DiffieHellman.DiffieHellman(1024);
                var clientPublicKey = BigInteger.Parse(model.PublicKey ?? throw new InvalidOperationException("Public key cannot be null."));

                var sharedSecret = serverDh.GetSharedSecret(clientPublicKey);
                _sessionKeyService.StoreSharedSecret(model.ChatId, model.UserId, sharedSecret, serverDh.P, serverDh.G);

                return Ok(new { serverPublicKey = serverDh.PublicKey.ToString(), p = serverDh.P.ToString(), g = serverDh.G.ToString() });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
