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

        public async Task<(BigInteger serverPublicKey, BigInteger p, BigInteger g)?> InitiateKeyExchange(int chatId, int userId, BigInteger clientPublicKey)
        {
            // Generate server's DH keys and parameters (P, G are generated internally by constructor)
            // Using 512 bits for now. A larger size like 1024 or 2048 is better for production.
            var serverDh = new DiffieHellman.DiffieHellman(512); 
            BigInteger p = serverDh.P;
            BigInteger g = serverDh.G;
            BigInteger serverPrivateKey = serverDh.PrivateKey;
            BigInteger serverPublicKey = serverDh.PublicKey;

            // Derive shared secret
            BigInteger sharedSecret = serverDh.GetSharedSecret(clientPublicKey);

            // Derive symmetric key and IV from shared secret
            byte[] sharedSecretBytes = sharedSecret.ToByteArray();
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashedSecret = sha256.ComputeHash(sharedSecretBytes);

                // Symmetric Key: full 32 bytes (256 bits)
                byte[] symmetricKey = new byte[32];
                Array.Copy(hashedSecret, 0, symmetricKey, 0, 32);

                // IV: Derive IV by hashing shared secret again with a different context
                byte[] iv;
                using (SHA256 sha256_iv = SHA256.Create())
                {
                    byte[] iv_seed = Encoding.UTF8.GetBytes("IV_Seed_For_DH").Concat(sharedSecretBytes).ToArray();
                    iv = sha256_iv.ComputeHash(iv_seed).Take(16).ToArray(); // AES IV is 16 bytes
                }


                // Store session key details in the database
                var sessionKey = new SessionKey
                {
                    ChatId = chatId,
                    UserId = userId,
                    ServerPrivateKey = serverPrivateKey,
                    ServerPublicKey = serverPublicKey,
                    ClientPublicKey = clientPublicKey,
                    SymmetricKey = symmetricKey,
                    Iv = iv,
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
                }
                else
                {
                    _context.SessionKeys.Add(sessionKey);
                }
                
                await _context.SaveChangesAsync();
            }

            return (serverPublicKey, p, g);
        }

        public async Task<SessionKey?> GetSessionKey(int chatId, int userId)
        {
            return await _context.SessionKeys.FirstOrDefaultAsync(sk => sk.ChatId == chatId && sk.UserId == userId);
        }
    }
}