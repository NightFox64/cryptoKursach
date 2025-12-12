using System;

namespace CipherModes.Modes
{
    public class RandomDelta : IEncryptionMode
    {
        private readonly IBlockCipher _cipher;
        private readonly IPaddingMode _padding;
        private byte[]? _iv;

        public RandomDelta(IBlockCipher cipher, IPaddingMode padding)
        {
            _cipher = cipher;
            _padding = padding;
        }

        public void Init(byte[] key, byte[]? iv = null)
        {
            _cipher.SetKey(key);
            _iv = iv ?? new byte[_cipher.GetBlockSize()];
        }

        public byte[] Encrypt(byte[] data)
        {
            data = _padding.AddPadding(data, _cipher.GetBlockSize());
            var result = new byte[data.Length];
            var delta = _iv;

            for (int i = 0; i < data.Length; i += _cipher.GetBlockSize())
            {
                var block = new byte[_cipher.GetBlockSize()];
                Array.Copy(data, i, block, 0, _cipher.GetBlockSize());
                var blockToEncrypt = Helpers.XOR(block, delta);
                var encryptedBlock = _cipher.EncryptBlock(blockToEncrypt);
                Array.Copy(encryptedBlock, 0, result, i, _cipher.GetBlockSize());
                delta = _cipher.EncryptBlock(encryptedBlock);
            }

            return result;
        }

        public byte[] Decrypt(byte[] data)
        {
            var result = new byte[data.Length];
            var delta = _iv;

            for (int i = 0; i < data.Length; i += _cipher.GetBlockSize())
            {
                var block = new byte[_cipher.GetBlockSize()];
                Array.Copy(data, i, block, 0, _cipher.GetBlockSize());
                var decryptedBlock = _cipher.DecryptBlock(block);
                var plainTextBlock = Helpers.XOR(decryptedBlock, delta);
                Array.Copy(plainTextBlock, 0, result, i, _cipher.GetBlockSize());
                delta = _cipher.EncryptBlock(block);
            }

            return _padding.RemovePadding(result, _cipher.GetBlockSize());
        }
    }
}