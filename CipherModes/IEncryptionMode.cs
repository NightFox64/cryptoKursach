using System;

namespace CipherModes
{
    public interface IEncryptionMode
    {
        void Init(byte[] key, byte[]? iv = null);
        byte[] Encrypt(byte[]? data);
        byte[] Decrypt(byte[]? data);
    }
}