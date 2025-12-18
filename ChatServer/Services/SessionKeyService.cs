using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ChatServer.Data;
using ChatServer.Models;
using DiffieHellman;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Services
{
    public class SessionKeyService : ISessionKeyService
    {
        private readonly ApplicationDbContext _context;

        public SessionKeyService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(BigInteger serverPublicKey, BigInteger p, BigInteger g, byte[] encryptedChatKey, byte[] encryptedChatIv)?> InitiateKeyExchange(int chatId, int userId, BigInteger clientPublicKey)
        {
            // Get or create chat's shared parameters
            var chat = await _context.Chats.FindAsync(chatId);
            if (chat == null)
            {
                return null;
            }

            BigInteger p, g;
            byte[] sharedSymmetricKey, sharedIv;

            // Check if chat already has shared parameters
            if (chat.SharedP != null && chat.SharedG != null && chat.SharedSymmetricKey != null && chat.SharedIv != null)
            {
                // Use existing shared parameters
                p = BigInteger.Parse(chat.SharedP);
                g = BigInteger.Parse(chat.SharedG);
                sharedSymmetricKey = chat.SharedSymmetricKey;
                sharedIv = chat.SharedIv;
                FileLogger.Log($"[SessionKey] Using existing shared key for chat {chatId}");
            }
            else
            {
                // Generate new shared parameters for the chat
                var initialDh = new DiffieHellman.DiffieHellman(512);
                p = initialDh.P;
                g = initialDh.G;

                // Generate shared symmetric key and IV for the entire chat
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    sharedSymmetricKey = new byte[32];
                    rng.GetBytes(sharedSymmetricKey);
                    
                    sharedIv = new byte[16];
                    rng.GetBytes(sharedIv);
                }

                // Save to chat
                chat.SharedP = p.ToString();
                chat.SharedG = g.ToString();
                chat.SharedSymmetricKey = sharedSymmetricKey;
                chat.SharedIv = sharedIv;
                _context.Chats.Update(chat);
                await _context.SaveChangesAsync();
                
                FileLogger.Log($"[SessionKey] Generated new shared key for chat {chatId}");
            }

            // Generate server's DH keys for this specific user (using shared p, g)
            var serverDh = new DiffieHellman.DiffieHellman(512, p, g);
            BigInteger serverPrivateKey = serverDh.PrivateKey;
            BigInteger serverPublicKey = serverDh.PublicKey;

            // Derive shared secret (for this specific user-server exchange)
            BigInteger sharedSecret = serverDh.GetSharedSecret(clientPublicKey);

            // Encrypt the chat's shared symmetric key using the DH shared secret
            // This allows each user to securely receive the same chat key
            byte[] sharedSecretBytes = sharedSecret.ToByteArray();
            byte[] encryptedChatKey;
            byte[] encryptedChatIv;
            
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] dhKey = sha256.ComputeHash(sharedSecretBytes);
                
                // Use AES to encrypt the chat's shared key with DH-derived key
                using (Aes aes = Aes.Create())
                {
                    aes.Key = dhKey;
                    aes.Mode = CipherMode.ECB;
                    aes.Padding = PaddingMode.PKCS7;
                    
                    using (var encryptor = aes.CreateEncryptor())
                    {
                        encryptedChatKey = encryptor.TransformFinalBlock(sharedSymmetricKey, 0, sharedSymmetricKey.Length);
                        encryptedChatIv = encryptor.TransformFinalBlock(sharedIv, 0, sharedIv.Length);
                    }
                }
            }

            // Store session key details in the database
            var sessionKey = new SessionKey
            {
                ChatId = chatId,
                UserId = userId,
                ServerPrivateKey = serverPrivateKey,
                ServerPublicKey = serverPublicKey,
                ClientPublicKey = clientPublicKey,
                SymmetricKey = encryptedChatKey, // Store encrypted version of shared key
                Iv = encryptedChatIv, // Store encrypted version of shared IV
                P = p,
                G = g
            };

            // Check if a SessionKey already exists for this chat and user to update it
            var existingSessionKey = await _context.SessionKeys
                .FirstOrDefaultAsync(sk => sk.ChatId == chatId && sk.UserId == userId);

            if (existingSessionKey != null)
            {
                existingSessionKey.ServerPrivateKey = sessionKey.ServerPrivateKey;
                existingSessionKey.ServerPublicKey = sessionKey.ServerPublicKey;
                existingSessionKey.ClientPublicKey = sessionKey.ClientPublicKey;
                existingSessionKey.SymmetricKey = sessionKey.SymmetricKey;
                existingSessionKey.Iv = sessionKey.Iv;
                existingSessionKey.P = sessionKey.P;
                existingSessionKey.G = sessionKey.G;
                _context.SessionKeys.Update(existingSessionKey);
                FileLogger.Log($"[SessionKey] Updated session for user {userId} in chat {chatId}");
            }
            else
            {
                _context.SessionKeys.Add(sessionKey);
                FileLogger.Log($"[SessionKey] Created new session for user {userId} in chat {chatId}");
            }
            
            await _context.SaveChangesAsync();

            // Return server public key, p, g, and encrypted chat key/IV
            return (serverPublicKey, p, g, encryptedChatKey, encryptedChatIv);
        }

        public async Task<SessionKey?> GetSessionKey(int chatId, int userId)
        {
            return await _context.SessionKeys.FirstOrDefaultAsync(sk => sk.ChatId == chatId && sk.UserId == userId);
        }
    }
}