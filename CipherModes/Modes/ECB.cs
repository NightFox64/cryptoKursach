using System;

namespace CipherModes.Modes
{
    public class ECB : IEncryptionMode
    {
        private readonly IBlockCipher _cipher;
        private readonly IPaddingMode? _padding;

        public ECB(IBlockCipher cipher, IPaddingMode? padding)
        {
            _cipher = cipher;
            _padding = padding;
        }

        public void Init(byte[] key, byte[]? iv = null)
        {
            _cipher.SetKey(key);
        }

        public byte[] Encrypt(byte[]? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (_padding != null)
            {
                data = _padding.AddPadding(data, _cipher.GetBlockSize());
            }
            var result = new byte[data.Length];

            for (int i = 0; i < data.Length; i += _cipher.GetBlockSize())
            {
                var block = new byte[_cipher.GetBlockSize()];
                Array.Copy(data, i, block, 0, _cipher.GetBlockSize());
                var encryptedBlock = _cipher.EncryptBlock(block);
                Array.Copy(encryptedBlock, 0, result, i, _cipher.GetBlockSize());
            }

            return result;
        }

        public byte[] Decrypt(byte[]? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var result = new byte[data.Length];

            for (int i = 0; i < data.Length; i += _cipher.GetBlockSize())
            {
                var block = new byte[_cipher.GetBlockSize()];
                Array.Copy(data, i, block, 0, _cipher.GetBlockSize());
                var decryptedBlock = _cipher.DecryptBlock(block);
                Array.Copy(decryptedBlock, 0, result, i, _cipher.GetBlockSize());
            }

            if (_padding != null)
            {
                return _padding.RemovePadding(result, _cipher.GetBlockSize());
            }
            return result;
        }
    }
}