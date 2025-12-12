using System;
using CipherModes;

namespace LOKI97
{
    public class LOKI97 : IBlockCipher
    {
        private const int BLOCK_SIZE = 8; // 64 bits
        private const int KEY_SIZE = 32;   // 256 bits
        private const int ROUNDS = 16;
        
        private uint[] roundKeys = new uint[ROUNDS];
        
        // S-блоки LOKI97
        private static readonly byte[] S1 = {
            0x0C, 0x05, 0x06, 0x0B, 0x09, 0x00, 0x0A, 0x0D, 0x03, 0x0E, 0x0F, 0x08, 0x04, 0x07, 0x01, 0x02
        };
        
        private static readonly byte[] S2 = {
            0x07, 0x0D, 0x0E, 0x03, 0x00, 0x06, 0x09, 0x0A, 0x01, 0x02, 0x08, 0x05, 0x0B, 0x0C, 0x04, 0x0F
        };

        public void SetKey(byte[] key)
        {
            if (key.Length != KEY_SIZE)
                throw new ArgumentException($"Key must be {KEY_SIZE} bytes");
            
            GenerateRoundKeys(key);
        }

        public int GetBlockSize() => BLOCK_SIZE;

        public byte[] EncryptBlock(byte[] block)
        {
            if (block.Length != BLOCK_SIZE)
                throw new ArgumentException($"Block must be {BLOCK_SIZE} bytes");
            
            uint L = BitConverter.ToUInt32(block, 0);
            uint R = BitConverter.ToUInt32(block, 4);
            
            for (int round = 0; round < ROUNDS; round++)
            {
                uint newR = L ^ F(R, roundKeys[round]);
                L = R;
                R = newR;
            }
            
            byte[] result = new byte[8];
            Array.Copy(BitConverter.GetBytes(L), 0, result, 0, 4);
            Array.Copy(BitConverter.GetBytes(R), 0, result, 4, 4);
            return result;
        }

        public byte[] DecryptBlock(byte[] block)
        {
            if (block.Length != BLOCK_SIZE)
                throw new ArgumentException($"Block must be {BLOCK_SIZE} bytes");
            
            uint L = BitConverter.ToUInt32(block, 0);
            uint R = BitConverter.ToUInt32(block, 4);
            
            for (int round = ROUNDS - 1; round >= 0; round--)
            {
                uint newL = R ^ F(L, roundKeys[round]);
                R = L;
                L = newL;
            }
            
            byte[] result = new byte[8];
            Array.Copy(BitConverter.GetBytes(L), 0, result, 0, 4);
            Array.Copy(BitConverter.GetBytes(R), 0, result, 4, 4);
            return result;
        }

        private void GenerateRoundKeys(byte[] key)
        {
            uint[] keyWords = new uint[8];
            for (int i = 0; i < 8; i++)
            {
                keyWords[i] = BitConverter.ToUInt32(key, i * 4);
            }
            
            for (int i = 0; i < ROUNDS; i++)
            {
                roundKeys[i] = keyWords[i % 8] ^ RotateLeft(keyWords[(i + 1) % 8], i + 1);
            }
        }

        private uint F(uint x, uint key)
        {
            x ^= key;
            
            // Применение S-блоков
            uint result = 0;
            for (int i = 0; i < 8; i++)
            {
                byte nibble = (byte)((x >> (i * 4)) & 0xF);
                byte sboxValue = (i % 2 == 0) ? S1[nibble] : S2[nibble];
                result |= (uint)(sboxValue << (i * 4));
            }
            
            // Циклический сдвиг
            return RotateLeft(result, 13);
        }

        private uint RotateLeft(uint value, int shift)
        {
            shift %= 32;
            return (value << shift) | (value >> (32 - shift));
        }
    }
}