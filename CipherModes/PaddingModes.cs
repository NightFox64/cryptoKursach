using System;
using System.Security.Cryptography;
using System.Diagnostics; // Kept for consistency, though Console.WriteLine is used
using System.Linq; // Added for string.Join

namespace CipherModes
{
    public class ZerosPadding : IPaddingMode
    {
        public byte[] AddPadding(byte[] data, int blockSize)
        {
            int paddingLength = blockSize - (data.Length % blockSize);
            if (paddingLength == 0) return data;
            
            byte[] padded = new byte[data.Length + paddingLength];
            Array.Copy(data, padded, data.Length);
            return padded;
        }

        public byte[] RemovePadding(byte[] data, int blockSize)
        {
            int i = data.Length - 1;
            while (i >= 0 && data[i] == 0) i--;
            
            byte[] result = new byte[i + 1];
            Array.Copy(data, result, i + 1);
            return result;
        }
    }

    public class ANSIX923Padding : IPaddingMode
    {
        public byte[] AddPadding(byte[] data, int blockSize)
        {
            int paddingLength = blockSize - (data.Length % blockSize);
            if (paddingLength == 0) paddingLength = blockSize;
            
            byte[] padded = new byte[data.Length + paddingLength];
            Array.Copy(data, padded, data.Length);
            
            padded[padded.Length - 1] = (byte)paddingLength;
            return padded;
        }

        public byte[] RemovePadding(byte[] data, int blockSize)
        {
            byte paddingLength = data[data.Length - 1];
            if (paddingLength > blockSize || paddingLength == 0)
                throw new ArgumentException("Invalid padding");

            for (int i = data.Length - paddingLength; i < data.Length - 1; i++)
            {
                if (data[i] != 0)
                    throw new ArgumentException("Invalid padding");
            }
            
            byte[] result = new byte[data.Length - paddingLength];
            Array.Copy(data, result, result.Length);
            return result;
        }
    }

    public class PKCS7Padding : IPaddingMode
    {
        public byte[] AddPadding(byte[] data, int blockSize)
        {
            Console.WriteLine($"PKCS7 AddPadding: data.Length={data.Length}, blockSize={blockSize}");
            int paddingLength = blockSize - (data.Length % blockSize);
            if (paddingLength == 0) paddingLength = blockSize;
            
            byte[] padded = new byte[data.Length + paddingLength];
            Array.Copy(data, padded, data.Length);
            
            for (int i = data.Length; i < padded.Length; i++)
                padded[i] = (byte)paddingLength;

            Console.WriteLine($"PKCS7 AddPadding: paddingLength={paddingLength}, padded.Length={padded.Length}");
            // Log padding bytes
            Console.WriteLine($"PKCS7 AddPadding: Padding bytes: {string.Join(", ", padded.Skip(data.Length).Select(b => b.ToString()))}");
            
            return padded;
        }

        public byte[] RemovePadding(byte[] data, int blockSize)
        {
            Console.WriteLine($"PKCS7 RemovePadding: data.Length={data.Length}, blockSize={blockSize}");
            byte paddingLength = data[data.Length - 1];

            Console.WriteLine($"PKCS7 RemovePadding: Detected paddingLength={paddingLength}");

            if (paddingLength > blockSize || paddingLength == 0)
            {
                Console.WriteLine($"PKCS7 RemovePadding: Invalid paddingLength detected: {paddingLength}");
                throw new ArgumentException("Invalid padding");
            }
            
            for (int i = data.Length - paddingLength; i < data.Length; i++)
                if (data[i] != paddingLength)
                {
                    Console.WriteLine($"PKCS7 RemovePadding: Mismatch in padding byte at index {i}. Expected {paddingLength}, Got {data[i]}");
                    throw new ArgumentException("Invalid padding");
                }
            
            byte[] result = new byte[data.Length - paddingLength];
            Array.Copy(data, result, result.Length);
            Console.WriteLine($"PKCS7 RemovePadding: Successfully removed padding. Result length={result.Length}");
            return result;
        }
    }

    public class ISO10126Padding : IPaddingMode
    {
        public byte[] AddPadding(byte[] data, int blockSize)
        {
            int paddingLength = blockSize - (data.Length % blockSize);
            if (paddingLength == 0) paddingLength = blockSize;
            
            byte[] padded = new byte[data.Length + paddingLength];
            Array.Copy(data, padded, data.Length);
            
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] randomBytes = new byte[paddingLength - 1];
                rng.GetBytes(randomBytes);
                Array.Copy(randomBytes, 0, padded, data.Length, paddingLength - 1);
            }
            
            padded[padded.Length - 1] = (byte)paddingLength;
            return padded;
        }

        public byte[] RemovePadding(byte[] data, int blockSize)
        {
            byte paddingLength = data[data.Length - 1];
            if (paddingLength > blockSize || paddingLength == 0)
                throw new ArgumentException("Invalid padding");
            
            byte[] result = new byte[data.Length - paddingLength];
            Array.Copy(data, result, result.Length);
            return result;
        }
    }
}