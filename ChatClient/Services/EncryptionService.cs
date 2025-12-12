using CipherModes;
using CipherModes.Modes;
using DiffieHellman;
using LOKI97;
using RC6;
using System;
using System.Numerics;
using System.Security.Cryptography;

namespace ChatClient.Services
{
    public class EncryptionService : IEncryptionService
    {
        public CipherAlgorithm CurrentAlgorithm { get; private set; }
        public CipherMode CurrentMode { get; private set; }

        public EncryptionService()
        {
            CurrentAlgorithm = CipherAlgorithm.LOKI97; // Default
            CurrentMode = CipherMode.CBC; // Default
        }

        public void SetAlgorithm(CipherAlgorithm algorithm)
        {
            CurrentAlgorithm = algorithm;
        }

        public void SetMode(CipherMode mode)
        {
            CurrentMode = mode;
        }

        public byte[] Encrypt(byte[] data, byte[] key, byte[]? iv)
        {
            IBlockCipher blockCipher = GetBlockCipher(key);
            IPaddingMode padding = GetPaddingMode();
            IEncryptionMode encryptionMode = GetEncryptionMode(blockCipher, padding);

            encryptionMode.Init(key, iv);
            return encryptionMode.Encrypt(data);
        }

        public byte[] Decrypt(byte[] data, byte[] key, byte[]? iv)
        {
            IBlockCipher blockCipher = GetBlockCipher(key);
            IPaddingMode padding = GetPaddingMode();
            IEncryptionMode encryptionMode = GetEncryptionMode(blockCipher, padding);

            encryptionMode.Init(key, iv);
            return encryptionMode.Decrypt(data);
        }

        public byte[] GenerateIV(int blockSize)
        {
            var iv = new byte[blockSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }
            return iv;
        }

        public (BigInteger p, BigInteger g) GetDhParameters(int keySize)
        {
            var dh = new DiffieHellman.DiffieHellman(keySize);
            return (dh.P, dh.G);
        }

        public (BigInteger publicKey, BigInteger privateKey) GenerateDhKeyPair(BigInteger p, BigInteger g)
        {
            var dh = new DiffieHellman.DiffieHellman(p, g);
            return (dh.PublicKey, dh.PrivateKey);
        }
        
        public BigInteger GetSharedSecret(BigInteger privateKey, BigInteger publicKey, BigInteger p)
        {
            var dh = new DiffieHellman.DiffieHellman(p, 2, privateKey); // g is always 2 in our implementation
            return dh.GetSharedSecret(publicKey);
        }

        private IBlockCipher GetBlockCipher(byte[] key)
        {
            switch (CurrentAlgorithm)
            {
                case CipherAlgorithm.LOKI97:
                    var loki97 = new LOKI97.LOKI97();
                    loki97.SetKey(key);
                    return loki97;
                case CipherAlgorithm.RC6:
                    var rc6 = new RC6.RC6();
                    rc6.SetKey(key);
                    return rc6;
                default:
                    throw new NotSupportedException($"Cipher algorithm {CurrentAlgorithm} is not supported.");
            }
        }

        private IPaddingMode GetPaddingMode()
        {
            // For block cipher modes, PKCS7 is generally a good default.
            // For stream cipher modes (CFB, OFB, CTR, RandomDelta), padding is often not strictly necessary,
            // but for consistency with the server's message structure, we might need a dummy padding or handle it carefully.
            // For now, always use PKCS7 and the modes should be adjusted to handle it if applicable.
            return new PKCS7Padding();
        }

        private IEncryptionMode GetEncryptionMode(IBlockCipher blockCipher, IPaddingMode? padding)
        {
            switch (CurrentMode)
            {
                case CipherMode.ECB:
                    return new ECB(blockCipher, padding);
                case CipherMode.CBC:
                    return new CBC(blockCipher, padding);
                case CipherMode.PCBC:
                    return new PCBC(blockCipher, padding);
                case CipherMode.CFB:
                    return new CFB(blockCipher, null); // CFB, OFB, CTR typically don't use padding
                case CipherMode.OFB:
                    return new OFB(blockCipher, null);
                case CipherMode.CTR:
                    return new CTR(blockCipher, null);
                case CipherMode.RandomDelta:
                    return new RandomDelta(blockCipher, padding); // Assuming RandomDelta uses padding
                default:
                    throw new NotSupportedException($"Cipher mode {CurrentMode} is not supported.");
            }
        }
    }
}
