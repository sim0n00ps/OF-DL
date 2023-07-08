using System;
using System.Collections.Generic;
using System.Linq;

namespace WidevineClient.Widevine
{
    class PSSHBox
    {
        static readonly byte[] PSSH_HEADER = new byte[] { 0x70, 0x73, 0x73, 0x68 };

        public List<byte[]> KIDs { get; set; } = new List<byte[]>();
        public byte[] Data { get; set; }

        PSSHBox(List<byte[]> kids, byte[] data)
        {
            KIDs = kids;
            Data = data;
        }

        public static PSSHBox FromByteArray(byte[] psshbox)
        {
            using var stream = new System.IO.MemoryStream(psshbox);

            stream.Seek(4, System.IO.SeekOrigin.Current);
            byte[] header = new byte[4];
            stream.Read(header, 0, 4);

            if (!header.SequenceEqual(PSSH_HEADER))
                throw new Exception("Not a pssh box");

            stream.Seek(20, System.IO.SeekOrigin.Current);
            byte[] kidCountBytes = new byte[4];
            stream.Read(kidCountBytes, 0, 4);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(kidCountBytes);
            uint kidCount = BitConverter.ToUInt32(kidCountBytes);

            List<byte[]> kids = new List<byte[]>();
            for (int i = 0; i < kidCount; i++)
            {
                byte[] kid = new byte[16];
                stream.Read(kid);
                kids.Add(kid);
            }

            byte[] dataLengthBytes = new byte[4];
            stream.Read(dataLengthBytes);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(dataLengthBytes);
            uint dataLength = BitConverter.ToUInt32(dataLengthBytes);

            if (dataLength == 0)
                return new PSSHBox(kids, null);

            byte[] data = new byte[dataLength];
            stream.Read(data);

            return new PSSHBox(kids, data);
        }
    }
}
