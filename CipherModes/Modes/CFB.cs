using System;

namespace CipherModes.Modes
{
    public class CFB : IEncryptionMode
    {
        private readonly IBlockCipher _cipher;
        private readonly IPaddingMode _padding;
        private byte[]? _iv;

        public CFB(IBlockCipher cipher, IPaddingMode padding)
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
            var result = new byte[data.Length];
            var previousBlock = new byte[_iv.Length];
            Array.Copy(_iv, previousBlock, _iv.Length);

            for (int i = 0; i < data.Length; i += _cipher.GetBlockSize())
            {
                var encryptedIv = _cipher.EncryptBlock(previousBlock);
                var len = Math.Min(_cipher.GetBlockSize(), data.Length - i);
                var block = new byte[len];
                Array.Copy(data, i, block, 0, len);
                var encryptedBlock = Helpers.XOR(block, encryptedIv);
                Array.Copy(encryptedBlock, 0, result, i, len);
                previousBlock = encryptedBlock;
            }

            return result;
        }

        public byte[] Decrypt(byte[] data)
        {
            var result = new byte[data.Length];
            var previousBlock = new byte[_iv.Length];
            Array.Copy(_iv, previousBlock, _iv.Length);

            for (int i = 0; i < data.Length; i += _cipher.GetBlockSize())
            {
                var encryptedIv = _cipher.EncryptBlock(previousBlock);
                var len = Math.Min(_cipher.GetBlockSize(), data.Length - i);
                var block = new byte[len];
                Array.Copy(data, i, block, 0, len);
                var plainTextBlock = Helpers.XOR(block, encryptedIv);
                Array.Copy(plainTextBlock, 0, result, i, len);
                previousBlock = block;
            }

            return result;
        }
    }
}