using System;
using CipherModes;

namespace RC6
{
    public class RC6 : IBlockCipher
    {
        private const int W = 32;           // Размер слова в битах
        private const int R = 20;           // Количество раундов
        private const int B = 16;           // Размер ключа в байтах
        private const int BLOCK_SIZE = 16;  // Размер блока в байтах (128 бит)
        
        private uint[] S = new uint[0];     // Массив раундовых ключей
        
        // Магические константы для RC6
        private const uint P32 = 0xB7E15163; // Odd((e-2) * 2^32)
        private const uint Q32 = 0x9E3779B9; // Odd((φ-1) * 2^32)
        
        public void SetKey(byte[] key)
        {
            if (key.Length != B)
                throw new ArgumentException($"Key must be {B} bytes");
            
            KeySchedule(key);
        }
        
        public int GetBlockSize() => BLOCK_SIZE;
        
        public byte[] EncryptBlock(byte[] block)
        {
            if (block.Length != BLOCK_SIZE)
                throw new ArgumentException($"Block must be {BLOCK_SIZE} bytes");
            
            // Преобразование байтов в слова
            uint A = BitConverter.ToUInt32(block, 0);
            uint B = BitConverter.ToUInt32(block, 4);
            uint C = BitConverter.ToUInt32(block, 8);
            uint D = BitConverter.ToUInt32(block, 12);
            
            // Предварительное добавление ключей
            B = ModularAdd(B, S[0]);
            D = ModularAdd(D, S[1]);
            
            // 20 раундов шифрования
            for (int i = 1; i <= R; i++)
            {
                uint t = Rotl(ModularMul(B, ModularMul(2, B) + 1), 5);
                uint u = Rotl(ModularMul(D, ModularMul(2, D) + 1), 5);
                A = Rotl(A ^ t, (int)(u & 31)) + S[2 * i];
                C = Rotl(C ^ u, (int)(t & 31)) + S[2 * i + 1];
                
                // Циклический сдвиг (A, B, C, D) = (B, C, D, A)
                uint temp = A;
                A = B;
                B = C;
                C = D;
                D = temp;
            }
            
            // Финальное добавление ключей
            A = ModularAdd(A, S[2 * R + 2]);
            C = ModularAdd(C, S[2 * R + 3]);
            
            // Преобразование слов обратно в байты
            byte[] result = new byte[BLOCK_SIZE];
            Array.Copy(BitConverter.GetBytes(A), 0, result, 0, 4);
            Array.Copy(BitConverter.GetBytes(B), 0, result, 4, 4);
            Array.Copy(BitConverter.GetBytes(C), 0, result, 8, 4);
            Array.Copy(BitConverter.GetBytes(D), 0, result, 12, 4);
            
            return result;
        }
        
        public byte[] DecryptBlock(byte[] block)
        {
            if (block.Length != BLOCK_SIZE)
                throw new ArgumentException($"Block must be {BLOCK_SIZE} bytes");
            
            // Преобразование байтов в слова
            uint A = BitConverter.ToUInt32(block, 0);
            uint B = BitConverter.ToUInt32(block, 4);
            uint C = BitConverter.ToUInt32(block, 8);
            uint D = BitConverter.ToUInt32(block, 12);
            
            // Обратное финальное добавление ключей
            C = ModularSub(C, S[2 * R + 3]);
            A = ModularSub(A, S[2 * R + 2]);
            
            // 20 раундов расшифрования (в обратном порядке)
            for (int i = R; i >= 1; i--)
            {
                // Обратный циклический сдвиг (A, B, C, D) = (D, A, B, C)
                uint temp = D;
                D = C;
                C = B;
                B = A;
                A = temp;
                
                uint u = Rotl(ModularMul(D, ModularMul(2, D) + 1), 5);
                uint t = Rotl(ModularMul(B, ModularMul(2, B) + 1), 5);
                C = Rotr(ModularSub(C, S[2 * i + 1]), (int)(t & 31)) ^ u;
                A = Rotr(ModularSub(A, S[2 * i]), (int)(u & 31)) ^ t;
            }
            
            // Обратное предварительное добавление ключей
            D = ModularSub(D, S[1]);
            B = ModularSub(B, S[0]);
            
            // Преобразование слов обратно в байты
            byte[] result = new byte[BLOCK_SIZE];
            Array.Copy(BitConverter.GetBytes(A), 0, result, 0, 4);
            Array.Copy(BitConverter.GetBytes(B), 0, result, 4, 4);
            Array.Copy(BitConverter.GetBytes(C), 0, result, 8, 4);
            Array.Copy(BitConverter.GetBytes(D), 0, result, 12, 4);
            
            return result;
        }
        
        // Расширение ключа (Key Schedule)
        private void KeySchedule(byte[] key)
        {
            int c = B / 4; // Количество слов в ключе
            uint[] L = new uint[c];
            
            // Преобразование ключа в массив слов L[]
            for (int i = 0; i < c; i++)
            {
                L[i] = BitConverter.ToUInt32(key, i * 4);
            }
            
            // Инициализация массива S[]
            S = new uint[2 * R + 4];
            S[0] = P32;
            for (int i = 1; i < S.Length; i++)
            {
                S[i] = ModularAdd(S[i - 1], Q32);
            }
            
            // Раундовое перемешивание
            uint keyA = 0, keyB = 0;
            int v = 3 * Math.Max(S.Length, c);
            
            for (int k = 0; k < v; k++)
            {
                keyA = S[k % S.Length] = Rotl(ModularAdd(S[k % S.Length], keyA + keyB), 3);
                keyB = L[k % c] = Rotl(ModularAdd(L[k % c], keyA + keyB), (int)((keyA + keyB) & 31));
            }
        }
        
        // Вспомогательные операции
        private uint Rotl(uint x, int y)
        {
            y &= 31; // Ограничиваем сдвиг до 31 бита
            return (x << y) | (x >> (32 - y));
        }
        
        private uint Rotr(uint x, int y)
        {
            y &= 31; // Ограничиваем сдвиг до 31 бита
            return (x >> y) | (x << (32 - y));
        }
        
        private uint ModularAdd(uint a, uint b)
        {
            return unchecked(a + b);
        }
        
        private uint ModularSub(uint a, uint b)
        {
            return unchecked(a - b);
        }
        private uint ModularMul(uint a, uint b)
        {
            return unchecked(a * b);
        }
    }
}