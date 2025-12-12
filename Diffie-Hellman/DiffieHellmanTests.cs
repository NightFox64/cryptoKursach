using NUnit.Framework;
using System.Numerics;

namespace DiffieHellman.Tests
{
    [TestFixture]
    public class DiffieHellmanTests
    {
        [Test]
        public void TestSharedSecret_PredefinedParameters()
        {
            var p = BigInteger.Parse("FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7EDEE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3DC2007CB8A163BF0598DA48361C55D39A69163FA8FD24CF5F83655D23DCA3AD961C62F356208552BB9ED529077096966D670C354E4ABC9804F1746C08CA18217C32905E462E36CE3BE39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9DE2BCBF6955817183995497CEA956AE515D2261898FA051015728E5A8AACAA68FFFFFFFFFFFFFFFF", System.Globalization.NumberStyles.HexNumber);
            var g = new BigInteger(2);

            var alice = new DiffieHellman(p, g);
            var bob = new DiffieHellman(p, g);

            var aliceSharedSecret = alice.GetSharedSecret(bob.PublicKey);
            var bobSharedSecret = bob.GetSharedSecret(alice.PublicKey);

            Assert.AreEqual(aliceSharedSecret, bobSharedSecret);
        }

        [Test]
        public void TestSharedSecret_GeneratedParameters_1024()
        {
            var dh = new DiffieHellman(1024);
            var p = dh.P;
            var g = dh.G;

            var alice = new DiffieHellman(p, g);
            var bob = new DiffieHellman(p, g);

            var aliceSharedSecret = alice.GetSharedSecret(bob.PublicKey);
            var bobSharedSecret = bob.GetSharedSecret(alice.PublicKey);

            Assert.AreEqual(aliceSharedSecret, bobSharedSecret);
        }

        [Test, Explicit]
        public void TestSharedSecret_GeneratedParameters_2048()
        {
            var dh = new DiffieHellman(2048);
            var p = dh.P;
            var g = dh.G;

            var alice = new DiffieHellman(p, g);
            var bob = new DiffieHellman(p, g);

            var aliceSharedSecret = alice.GetSharedSecret(bob.PublicKey);
            var bobSharedSecret = bob.GetSharedSecret(alice.PublicKey);

            Assert.AreEqual(aliceSharedSecret, bobSharedSecret);
        }
    }
}
