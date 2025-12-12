using System.Numerics;
using System.Security.Cryptography;

namespace DiffieHellman
{
    public class DiffieHellman
    {
        private BigInteger _p;
        private BigInteger _g;
        private BigInteger _privateKey;
        public BigInteger PublicKey { get; private set; }

        public BigInteger P => _p;
        public BigInteger G => _g;

        public DiffieHellman(int keySize)
        {
            GenerateParameters(keySize);
            _privateKey = GeneratePrivateKey();
            PublicKey = BigInteger.ModPow(_g, _privateKey, _p);
        }

        public DiffieHellman(BigInteger p, BigInteger g)
        {
            _p = p;
            _g = g;
            _privateKey = GeneratePrivateKey();
            PublicKey = BigInteger.ModPow(_g, _privateKey, _p);
        }

        public BigInteger GetSharedSecret(BigInteger otherPartyPublicKey)
        {
            return BigInteger.ModPow(otherPartyPublicKey, _privateKey, _p);
        }

        private void GenerateParameters(int keySize)
        {
            _p = GeneratePrime(keySize);
            _g = 2; // A common choice for g
        }

        private BigInteger GeneratePrime(int bitLength)
        {
            var rng = RandomNumberGenerator.Create();
            while (true)
            {
                var bytes = new byte[bitLength / 8];
                rng.GetBytes(bytes);
                var candidate = new BigInteger(bytes);

                // Ensure it's odd and has the correct bit length
                candidate = BigInteger.Abs(candidate);
                candidate |= BigInteger.One << (bitLength - 1);
                candidate |= BigInteger.One;
                
                if (PrimalityTests.IsPrime(candidate, 40))
                {
                    return candidate;
                }
            }
        }

        private BigInteger GeneratePrivateKey()
        {
            var rng = RandomNumberGenerator.Create();
            byte[] bytes = new byte[_p.ToByteArray().Length];
            BigInteger privateKey;
            do
            {
                rng.GetBytes(bytes);
                privateKey = new BigInteger(bytes);
            } while (privateKey <= 1 || privateKey >= _p - 1);

            return privateKey;
        }
    }
}