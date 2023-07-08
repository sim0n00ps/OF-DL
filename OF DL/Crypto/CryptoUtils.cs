using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;

namespace WidevineClient.Crypto
{
    public class CryptoUtils
    {
        public static byte[] GetHMACSHA256Digest(byte[] data, byte[] key)
        {
            return new HMACSHA256(key).ComputeHash(data);
        }

        public static byte[] GetCMACDigest(byte[] data, byte[] key)
        {
            IBlockCipher cipher = new AesEngine();
            IMac mac = new CMac(cipher, 128);

            KeyParameter keyParam = new KeyParameter(key);

            mac.Init(keyParam);

            mac.BlockUpdate(data, 0, data.Length);

            byte[] outBytes = new byte[16];

            mac.DoFinal(outBytes, 0);
            return outBytes;
        }
    }
}
