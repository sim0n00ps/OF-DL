using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace WidevineClient.Crypto
{
    public class Padding
    {
        public static byte[] AddPKCS7Padding(byte[] data, int k)
        {
            int m = k - (data.Length % k);

            byte[] padding = new byte[m];
            Array.Fill(padding, (byte)m);

            byte[] paddedBytes = new byte[data.Length + padding.Length];
            Buffer.BlockCopy(data, 0, paddedBytes, 0, data.Length);
            Buffer.BlockCopy(padding, 0, paddedBytes, data.Length, padding.Length);

            return paddedBytes;
        }

        public static byte[] RemovePKCS7Padding(byte[] paddedByteArray)
        {
            var last = paddedByteArray[^1];
            if (paddedByteArray.Length <= last)
            {
                return paddedByteArray;
            }

            return SubArray(paddedByteArray, 0, (paddedByteArray.Length - last));
        }

        public static T[] SubArray<T>(T[] arr, int start, int length)
        {
            var result = new T[length];
            Buffer.BlockCopy(arr, start, result, 0, length);

            return result;
        }

        public static byte[] AddPSSPadding(byte[] hash)
        {
            int modBits = 2048;
            int hLen = 20;
            int emLen = 256;

            int lmask = 0;
            for (int i = 0; i < 8 * emLen - (modBits - 1); i++)
                lmask = lmask >> 1 | 0x80;

            if (emLen < hLen + hLen + 2)
            {
                return null;
            }

            byte[] salt = new byte[hLen];
            new Random().NextBytes(salt);

            byte[] m_prime = Enumerable.Repeat((byte)0, 8).ToArray().Concat(hash).Concat(salt).ToArray();
            byte[] h = SHA1.Create().ComputeHash(m_prime);

            byte[] ps = Enumerable.Repeat((byte)0, emLen - hLen - hLen - 2).ToArray();
            byte[] db = ps.Concat(new byte[] { 0x01 }).Concat(salt).ToArray();

            byte[] dbMask = MGF1(h, emLen - hLen - 1);

            byte[] maskedDb = new byte[dbMask.Length];
            for (int i = 0; i < dbMask.Length; i++)
                maskedDb[i] = (byte)(db[i] ^ dbMask[i]);

            maskedDb[0] = (byte)(maskedDb[0] & ~lmask);

            byte[] padded = maskedDb.Concat(h).Concat(new byte[] { 0xBC }).ToArray();

            return padded;
        }

        public static byte[] RemoveOAEPPadding(byte[] data)
        {
            int k = 256;
            int hLen = 20;

            byte[] maskedSeed = data[1..(hLen + 1)];
            byte[] maskedDB = data[(hLen + 1)..];

            byte[] seedMask = MGF1(maskedDB, hLen);

            byte[] seed = new byte[maskedSeed.Length];
            for (int i = 0; i < maskedSeed.Length; i++)
                seed[i] = (byte)(maskedSeed[i] ^ seedMask[i]);

            byte[] dbMask = MGF1(seed, k - hLen - 1);

            byte[] db = new byte[maskedDB.Length];
            for (int i = 0; i < maskedDB.Length; i++)
                db[i] = (byte)(maskedDB[i] ^ dbMask[i]);

            int onePos = BitConverter.ToString(db[hLen..]).Replace("-", "").IndexOf("01") / 2;
            byte[] unpadded = db[(hLen + onePos + 1)..];

            return unpadded;
        }

        static byte[] MGF1(byte[] seed, int maskLen)
        {
            SHA1 hobj = SHA1.Create();
            int hLen = hobj.HashSize / 8;
            List<byte> T = new List<byte>();
            for (int i = 0; i < (int)Math.Ceiling(((double)maskLen / (double)hLen)); i++)
            {
                byte[] c = BitConverter.GetBytes(i);
                Array.Reverse(c);
                byte[] digest = hobj.ComputeHash(seed.Concat(c).ToArray());
                T.AddRange(digest);
            }
            return T.GetRange(0, maskLen).ToArray();
        }
    }
}
