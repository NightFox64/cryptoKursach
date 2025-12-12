using System;

namespace CipherModes
{
    public interface IBlockCipher
    {
        void SetKey(byte[] key);
        int GetBlockSize();
        byte[] EncryptBlock(byte[] block);
        byte[] DecryptBlock(byte[] block);
    }
}