using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WidevineClient.Widevine
{
    public class CDMDevice
    {
        public string DeviceName { get; set; }
        public ClientIdentification ClientID { get; set; }
        AsymmetricCipherKeyPair DeviceKeys { get; set; }

        public virtual bool IsAndroid { get; set; } = true;

        public CDMDevice(string deviceName, byte[] clientIdBlobBytes = null, byte[] privateKeyBytes = null, byte[] vmpBytes = null)
        {
            DeviceName = deviceName;

            string privateKeyPath = Path.Join(Constants.DEVICES_FOLDER, deviceName, "device_private_key");
            string vmpPath = Path.Join(Constants.DEVICES_FOLDER, deviceName, "device_vmp_blob");

            if (clientIdBlobBytes == null)
            {
                string clientIDPath = Path.Join(Constants.DEVICES_FOLDER, deviceName, "device_client_id_blob");

                if (!File.Exists(clientIDPath))
                    throw new Exception("No client id blob found");

                clientIdBlobBytes = File.ReadAllBytes(clientIDPath);
            }

            ClientID = Serializer.Deserialize<ClientIdentification>(new MemoryStream(clientIdBlobBytes));

            if (privateKeyBytes != null)
            {
                using var reader = new StringReader(Encoding.UTF8.GetString(privateKeyBytes));
                DeviceKeys = (AsymmetricCipherKeyPair)new PemReader(reader).ReadObject();
            }
            else if (File.Exists(privateKeyPath))
            {
                using var reader = File.OpenText(privateKeyPath);
                DeviceKeys = (AsymmetricCipherKeyPair)new PemReader(reader).ReadObject();
            }

            if (vmpBytes != null)
            {
                var vmp = Serializer.Deserialize<FileHashes>(new MemoryStream(vmpBytes));
                ClientID.FileHashes = vmp;
            }
            else if (File.Exists(vmpPath))
            {
                var vmp = Serializer.Deserialize<FileHashes>(new MemoryStream(File.ReadAllBytes(vmpPath)));
                ClientID.FileHashes = vmp;
            }
        }

        public virtual byte[] Decrypt(byte[] data)
        {
            OaepEncoding eng = new OaepEncoding(new RsaEngine());
            eng.Init(false, DeviceKeys.Private);

            int length = data.Length;
            int blockSize = eng.GetInputBlockSize();

            List<byte> plainText = new List<byte>();

            for (int chunkPosition = 0; chunkPosition < length; chunkPosition += blockSize)
            {
                int chunkSize = Math.Min(blockSize, length - chunkPosition);
                plainText.AddRange(eng.ProcessBlock(data, chunkPosition, chunkSize));
            }

            return plainText.ToArray();
        }

        public virtual byte[] Sign(byte[] data)
        {
            PssSigner eng = new PssSigner(new RsaEngine(), new Sha1Digest());

            eng.Init(true, DeviceKeys.Private);
            eng.BlockUpdate(data, 0, data.Length);
            return eng.GenerateSignature();
        }
    }
}
