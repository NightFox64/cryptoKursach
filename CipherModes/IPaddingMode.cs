using System;

namespace CipherModes
{
    public interface IPaddingMode
    {
        byte[] AddPadding(byte[] data, int blockSize);
        byte[] RemovePadding(byte[] data, int blockSize);
    }
}