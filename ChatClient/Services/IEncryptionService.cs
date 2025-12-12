using CipherModes;
using System.Numerics;

namespace ChatClient.Services
{
    public interface IEncryptionService
    {
        CipherAlgorithm CurrentAlgorithm { get; }
        CipherMode CurrentMode { get; }

        void SetAlgorithm(CipherAlgorithm algorithm);
        void SetMode(CipherMode mode);
        byte[] Encrypt(byte[] data, byte[] key, byte[]? iv);
        byte[] Decrypt(byte[] data, byte[] key, byte[]? iv);
        byte[] GenerateIV(int blockSize);
        (BigInteger p, BigInteger g) GetDhParameters(int keySize);
        (BigInteger publicKey, BigInteger privateKey) GenerateDhKeyPair(BigInteger p, BigInteger g);
        BigInteger GetSharedSecret(BigInteger privateKey, BigInteger publicKey, BigInteger p);
    }

    public enum CipherAlgorithm
    {
        LOKI97,
        RC6
    }

    public enum CipherMode
    {
        ECB,
        CBC,
        PCBC,
        CFB,
        OFB,
        CTR,
        RandomDelta
    }
}
