using System;
using System.Security.Cryptography;

namespace CipherModes
{
    public class AesWrapper : IBlockCipher
    {
        private readonly Aes _aes;
        private ICryptoTransform _encryptor;
        private ICryptoTransform _decryptor;

        public AesWrapper()
        {
            _aes = Aes.Create();
            _aes.Mode = CipherMode.ECB; // The wrapper handles a single block, so ECB is appropriate here. The outer mode (CBC, etc.) will handle chaining.
            _aes.Padding = PaddingMode.None; // Padding is handled by the IPaddingMode implementation.
        }

        public void SetKey(byte[] key)
        {
            _aes.Key = key;
            // Create transforms once the key is set
            _encryptor = _aes.CreateEncryptor();
            _decryptor = _aes.CreateDecryptor();
        }

        public int GetBlockSize()
        {
            return _aes.BlockSize / 8; // BlockSize is in bits
        }

        public byte[] EncryptBlock(byte[] block)
        {
            if (_encryptor == null)
            {
                throw new InvalidOperationException("Key is not set.");
            }
            return _encryptor.TransformFinalBlock(block, 0, block.Length);
        }

        public byte[] DecryptBlock(byte[] block)
        {
            if (_decryptor == null)
            {
                throw new InvalidOperationException("Key is not set.");
            }
            return _decryptor.TransformFinalBlock(block, 0, block.Length);
        }
    }
}
