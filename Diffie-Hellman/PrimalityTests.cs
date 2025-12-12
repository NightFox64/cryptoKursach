using System.Numerics;
using System.Security.Cryptography;

namespace DiffieHellman
{
    public static class PrimalityTests
    {
        public static bool IsPrime(BigInteger n, int k)
        {
            if (n < 2) return false;
            if (n == 2 || n == 3) return true;
            if (n % 2 == 0) return false;

            BigInteger d = n - 1;
            int s = 0;
            while (d % 2 == 0)
            {
                d /= 2;
                s++;
            }

            for (int i = 0; i < k; i++)
            {
                BigInteger a = GenerateRandomBigInteger(2, n - 2);
                BigInteger x = BigInteger.ModPow(a, d, n);
                if (x == 1 || x == n - 1) continue;

                for (int r = 1; r < s; r++)
                {
                    x = BigInteger.ModPow(x, 2, n);
                    if (x == 1) return false;
                    if (x == n - 1) break;
                }

                if (x != n - 1) return false;
            }

            return true;
        }
        
        private static BigInteger GenerateRandomBigInteger(BigInteger minValue, BigInteger maxValue)
        {
            if (minValue > maxValue) throw new ArgumentException("minValue must be less than or equal to maxValue");

            var rng = RandomNumberGenerator.Create();
            var range = maxValue - minValue;
            int length = range.ToByteArray().Length;

            // Generate a random number that is almost certainly less than or equal to `range`
            byte[] bytes = new byte[length];
            rng.GetBytes(bytes);
            bytes[bytes.Length - 1] &= (byte)0x7F; // Ensure positive
            var result = new BigInteger(bytes);

            // If it's still too large, we can just try again
            while (result >= range)
            {
                rng.GetBytes(bytes);
                bytes[bytes.Length - 1] &= (byte)0x7F; // Ensure positive
                result = new BigInteger(bytes);
            }

            return result + minValue;
        }
    }
}
