using NUnit.Framework;
using System;
using System.Linq;
using System.Text;
using CipherModes;
using CipherModes.Modes;
using RC6;

namespace CipherTests
{
    [TestFixture]
    public class RC6CipherModeTests
    {
        private readonly byte[] _key = new byte[16];
        private readonly byte[] _iv = new byte[16];
        private readonly IBlockCipher _cipher = new RC6.RC6();

        public RC6CipherModeTests()
        {
            // Initialize with some dummy key and iv
            for (int i = 0; i < _key.Length; i++) _key[i] = (byte)i;
            for (int i = 0; i < _iv.Length; i++) _iv[i] = (byte)i;
        }

        [Test]
        public void ECB_Test()
        {
            var ecb = new ECB(_cipher, new PKCS7Padding());
            ecb.Init(_key);
            var plainText = "Hello World!";
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ecb.Encrypt(plainTextBytes);
            var decrypted = ecb.Decrypt(encrypted);
            CollectionAssert.AreEqual(plainTextBytes, decrypted);
        }

        [Test]
        public void CBC_Test()
        {
            var cbc = new CBC(_cipher, new PKCS7Padding());
            cbc.Init(_key, _iv);
            var plainText = "Hello World!";
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = cbc.Encrypt(plainTextBytes);
            var decrypted = cbc.Decrypt(encrypted);
            CollectionAssert.AreEqual(plainTextBytes, decrypted);
        }

        [Test]
        public void PCBC_Test()
        {
            var pcbc = new PCBC(_cipher, new PKCS7Padding());
            pcbc.Init(_key, _iv);
            var plainText = "Hello World!";
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = pcbc.Encrypt(plainTextBytes);
            var decrypted = pcbc.Decrypt(encrypted);
            CollectionAssert.AreEqual(plainTextBytes, decrypted);
        }

        [Test]
        public void CFB_Test()
        {
            var cfb = new CFB(_cipher, null);
            cfb.Init(_key, _iv);
            var plainText = "This is a longer text to test the cipher modes.";
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = cfb.Encrypt(plainTextBytes);
            var decrypted = cfb.Decrypt(encrypted);
            CollectionAssert.AreEqual(plainTextBytes, decrypted);
        }

        [Test]
        public void OFB_Test()
        {
            var ofb = new OFB(_cipher, null);
            ofb.Init(_key, _iv);
            var plainText = "This is a longer text to test the cipher modes.";
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ofb.Encrypt(plainTextBytes);
            var decrypted = ofb.Decrypt(encrypted);
            CollectionAssert.AreEqual(plainTextBytes, decrypted);
        }

        [Test]
        public void CTR_Test()
        {
            var ctr = new CTR(_cipher, null);
            ctr.Init(_key, _iv);
            var plainText = "This is a longer text to test the cipher modes.";
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ctr.Encrypt(plainTextBytes);
            var decrypted = ctr.Decrypt(encrypted);
            CollectionAssert.AreEqual(plainTextBytes, decrypted);
        }

        [Test]
        public void RandomDelta_Test()
        {
            var randomDelta = new RandomDelta(_cipher, new PKCS7Padding());
            randomDelta.Init(_key, _iv);
            var plainText = "Hello World!";
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = randomDelta.Encrypt(plainTextBytes);
            var decrypted = randomDelta.Decrypt(encrypted);
            CollectionAssert.AreEqual(plainTextBytes, decrypted);
        }
    }
}