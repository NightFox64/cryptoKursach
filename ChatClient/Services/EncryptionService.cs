using CipherModes;
using CipherModes.Modes;
using DiffieHellman;
using LOKI97;
using RC6;
using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Diagnostics; // Kept for consistency, though Console.WriteLine is used
using System.Linq; // Added for string.Join

namespace ChatClient.Services
{
    public class EncryptionService : IEncryptionService
    {
        public CipherAlgorithm CurrentAlgorithm { get; private set; }
        public CipherMode CurrentMode { get; private set; }
        public PaddingMode CurrentPaddingMode { get; private set; }

        // New property to expose the required key size
        public int RequiredKeySize
        {
            get
            {
                switch (CurrentAlgorithm)
                {
                    case CipherAlgorithm.LOKI97:
                        return 32; // LOKI97 requires 32-byte key
                    case CipherAlgorithm.RC6:
                        return 16; // RC6 requires 16-byte key
                    default:
                        throw new NotSupportedException($"Cipher algorithm {CurrentAlgorithm} is not supported.");
                }
            }
        }

        // Property to expose the block size
        public int BlockSize
        {
            get
            {
                switch (CurrentAlgorithm)
                {
                    case CipherAlgorithm.LOKI97:
                        return 8; // LOKI97 has 8-byte (64-bit) block size
                    case CipherAlgorithm.RC6:
                        return 16; // RC6 has 16-byte (128-bit) block size
                    default:
                        throw new NotSupportedException($"Cipher algorithm {CurrentAlgorithm} is not supported.");
                }
            }
        }

        public EncryptionService()
        {
            CurrentAlgorithm = CipherAlgorithm.RC6; // Default
            CurrentMode = CipherMode.CBC; // Default
            CurrentPaddingMode = PaddingMode.PKCS7; // Default
        }

        public void SetAlgorithm(CipherAlgorithm algorithm)
        {
            CurrentAlgorithm = algorithm;
        }

        public void SetMode(CipherMode mode)
        {
            CurrentMode = mode;
        }
        
        public void SetCipherAlgorithm(string algorithm)
        {
            CurrentAlgorithm = algorithm switch
            {
                "LOKI97" => CipherAlgorithm.LOKI97,
                "RC6" => CipherAlgorithm.RC6,
                _ => CipherAlgorithm.RC6
            };
        }
        
        public void SetCipherMode(string mode)
        {
            CurrentMode = mode switch
            {
                "ECB" => CipherMode.ECB,
                "CBC" => CipherMode.CBC,
                "PCBC" => CipherMode.PCBC,
                "CFB" => CipherMode.CFB,
                "OFB" => CipherMode.OFB,
                "CTR" => CipherMode.CTR,
                "RandomDelta" => CipherMode.RandomDelta,
                _ => CipherMode.CBC
            };
        }
        
        public void SetPaddingMode(string padding)
        {
            CurrentPaddingMode = padding switch
            {
                "PKCS7" => PaddingMode.PKCS7,
                "Zeros" => PaddingMode.Zeros,
                "None" => PaddingMode.None,
                _ => PaddingMode.PKCS7
            };
        }

        public byte[] Encrypt(byte[] data, byte[] key, byte[]? iv)
        {
            Console.WriteLine($"EncryptionService Encrypt: Algorithm={CurrentAlgorithm}, Mode={CurrentMode}, data.Length={data.Length}");
            Console.WriteLine($"EncryptionService Encrypt: Key={BitConverter.ToString(key)}, IV={(iv != null ? BitConverter.ToString(iv) : "null")}");

            IBlockCipher blockCipher = GetBlockCipher(key);
            IPaddingMode padding = GetPaddingMode();
            IEncryptionMode encryptionMode = GetEncryptionMode(blockCipher, padding);

            Console.WriteLine($"Encrypt: Plain data length: {data.Length}, Cipher Block Size: {blockCipher.GetBlockSize()}"); // Added debug
            
            encryptionMode.Init(key, iv);
            byte[] encryptedData = encryptionMode.Encrypt(data);
            Console.WriteLine($"EncryptionService Encrypt: Encrypted data length={encryptedData.Length}");
            Console.WriteLine($"Encrypt: Encrypted data length after padding/encryption: {encryptedData.Length}"); // Added debug
            return encryptedData;
        }

        public byte[] Decrypt(byte[] data, byte[] key, byte[]? iv)
        {
            Console.WriteLine($"EncryptionService Decrypt: Algorithm={CurrentAlgorithm}, Mode={CurrentMode}, data.Length={data.Length}");
            Console.WriteLine($"EncryptionService Decrypt: Key={BitConverter.ToString(key)}, IV={(iv != null ? BitConverter.ToString(iv) : "null")}");

            IBlockCipher blockCipher = GetBlockCipher(key);
            IPaddingMode padding = GetPaddingMode();
            IEncryptionMode encryptionMode = GetEncryptionMode(blockCipher, padding);

            Console.WriteLine($"Decrypt: Encrypted data length received: {data.Length}, Cipher Block Size: {blockCipher.GetBlockSize()}"); // Added debug
            
            encryptionMode.Init(key, iv);
            try
            {
                byte[] decryptedData = encryptionMode.Decrypt(data);
                Console.WriteLine($"EncryptionService Decrypt: Decrypted data length={decryptedData.Length}");
                Console.WriteLine($"Decrypt: Decrypted data length after unpadding: {decryptedData.Length}"); // Added debug
                return decryptedData;
            }
            catch (ArgumentException ex) when (ex.Message == "Invalid padding")
            {
                Console.WriteLine($"EncryptionService Decrypt: Caught Invalid padding error during decryption. data.Length={data.Length}");
                throw; // Re-throw the exception after logging
            }
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

        private IPaddingMode? GetPaddingMode()
        {
            switch (CurrentPaddingMode)
            {
                case PaddingMode.PKCS7:
                    return new PKCS7Padding();
                case PaddingMode.Zeros:
                    return new ZerosPadding();
                case PaddingMode.None:
                    return null; // No padding
                default:
                    return new PKCS7Padding();
            }
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
