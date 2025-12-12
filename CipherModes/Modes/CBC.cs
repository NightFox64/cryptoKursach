using System;

namespace CipherModes.Modes
{
    public class CBC : IEncryptionMode
    {
        private readonly IBlockCipher _cipher;
        private readonly IPaddingMode? _padding;
        private byte[]? _iv;

        public CBC(IBlockCipher cipher, IPaddingMode? padding)
        {
            _cipher = cipher;
            _padding = padding;
        }

        public void Init(byte[] key, byte[]? iv = null)
        {
            _cipher.SetKey(key);
            _iv = iv ?? new byte[_cipher.GetBlockSize()];
        }

        public byte[] Encrypt(byte[]? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var dataToEncrypt = data;
            if (_padding != null)
            {
                dataToEncrypt = _padding.AddPadding(data, _cipher.GetBlockSize());
            }
            
            var result = new byte[dataToEncrypt.Length];
            var previousBlock = _iv;
            if (previousBlock == null) throw new InvalidOperationException("IV is not set.");


            for (int i = 0; i < dataToEncrypt.Length; i += _cipher.GetBlockSize())
            {
                var block = new byte[_cipher.GetBlockSize()];
                Array.Copy(dataToEncrypt, i, block, 0, _cipher.GetBlockSize());
                var blockToEncrypt = Helpers.XOR(block, previousBlock);
                var encryptedBlock = _cipher.EncryptBlock(blockToEncrypt);
                Array.Copy(encryptedBlock, 0, result, i, _cipher.GetBlockSize());
                previousBlock = encryptedBlock;
            }

            return result;
        }

        public byte[] Decrypt(byte[]? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var result = new byte[data.Length];
            var previousBlock = _iv;
            if (previousBlock == null) throw new InvalidOperationException("IV is not set.");

            for (int i = 0; i < data.Length; i += _cipher.GetBlockSize())
            {
                var block = new byte[_cipher.GetBlockSize()];
                Array.Copy(data, i, block, 0, _cipher.GetBlockSize());
                var decryptedBlock = _cipher.DecryptBlock(block);
                var plainTextBlock = Helpers.XOR(decryptedBlock, previousBlock);
                Array.Copy(plainTextBlock, 0, result, i, _cipher.GetBlockSize());
                previousBlock = block;
            }

            if (_padding != null)
            {
                return _padding.RemovePadding(result, _cipher.GetBlockSize());
            }
            return result;
        }
    }
}