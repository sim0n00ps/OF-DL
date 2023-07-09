using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using WidevineClient.Crypto;

namespace WidevineClient.Widevine
{
    public class CDM
    {
        static Dictionary<string, CDMDevice> Devices { get; } = new Dictionary<string, CDMDevice>()
        {
            ["chrome_1610"] = new CDMDevice("chrome_1610", null, null, null)
        };
        static Dictionary<string, Session> Sessions { get; set; } = new Dictionary<string, Session>();

        static byte[] CheckPSSH(string psshB64)
        {
            byte[] systemID = new byte[] { 237, 239, 139, 169, 121, 214, 74, 206, 163, 200, 39, 220, 213, 29, 33, 237 };

            if (psshB64.Length % 4 != 0)
            {
                psshB64 = psshB64.PadRight(psshB64.Length + (4 - (psshB64.Length % 4)), '=');
            }

            byte[] pssh = Convert.FromBase64String(psshB64);

            if (pssh.Length < 30)
                return pssh;

            if (!pssh[12..28].SequenceEqual(systemID))
            {
                List<byte> newPssh = new List<byte>() { 0, 0, 0 };
                newPssh.Add((byte)(32 + pssh.Length));
                newPssh.AddRange(Encoding.UTF8.GetBytes("pssh"));
                newPssh.AddRange(new byte[] { 0, 0, 0, 0 });
                newPssh.AddRange(systemID);
                newPssh.AddRange(new byte[] { 0, 0, 0, 0 });
                newPssh[31] = (byte)(pssh.Length);
                newPssh.AddRange(pssh);

                return newPssh.ToArray();
            }
            else
            {
                return pssh;
            }
        }

        public static string OpenSession(string initDataB64, string deviceName, bool offline = false, bool raw = false)
        {
            byte[] initData = CheckPSSH(initDataB64);

            var device = Devices[deviceName];

            byte[] sessionId = new byte[16];

            if (device.IsAndroid)
            {
                string randHex = "";

                Random rand = new Random();
                string choice = "ABCDEF0123456789";
                for (int i = 0; i < 16; i++)
                    randHex += choice[rand.Next(16)];

                string counter = "01";
                string rest = "00000000000000";
                sessionId = Encoding.ASCII.GetBytes(randHex + counter + rest);
            }
            else
            {
                Random rand = new Random();
                rand.NextBytes(sessionId);
            }

            Session session;
            dynamic parsedInitData = ParseInitData(initData);

            if (parsedInitData != null)
            {
                session = new Session(sessionId, parsedInitData, device, offline);
            }
            else if (raw)
            {
                session = new Session(sessionId, initData, device, offline);
            }
            else
            {
                return null;
            }

            Sessions.Add(Utils.BytesToHex(sessionId), session);

            return Utils.BytesToHex(sessionId);
        }

        static WidevineCencHeader ParseInitData(byte[] initData)
        {
            WidevineCencHeader cencHeader;

            try
            {
                cencHeader = Serializer.Deserialize<WidevineCencHeader>(new MemoryStream(initData[32..]));
            }
            catch
            {
                try
                {
                    //needed for HBO Max

                    PSSHBox psshBox = PSSHBox.FromByteArray(initData);
                    cencHeader = Serializer.Deserialize<WidevineCencHeader>(new MemoryStream(psshBox.Data));
                }
                catch
                {
                    //Logger.Verbose("Unable to parse, unsupported init data format");
                    return null;
                }
            }

            return cencHeader;
        }

        public static bool CloseSession(string sessionId)
        {
            //Logger.Debug($"CloseSession(session_id={Utils.BytesToHex(sessionId)})");
            //Logger.Verbose("Closing CDM session");

            if (Sessions.ContainsKey(sessionId))
            {
                Sessions.Remove(sessionId);
                //Logger.Verbose("CDM session closed");
                return true;
            }
            else
            {
                //Logger.Info($"Session {sessionId} not found");
                return false;
            }
        }

        public static bool SetServiceCertificate(string sessionId, byte[] certData)
        {
            //Logger.Debug($"SetServiceCertificate(sessionId={Utils.BytesToHex(sessionId)}, cert={certB64})");
            //Logger.Verbose($"Setting service certificate");

            if (!Sessions.ContainsKey(sessionId))
            {
                //Logger.Error("Session ID doesn't exist");
                return false;
            }

            SignedMessage signedMessage = new SignedMessage();

            try
            {
                signedMessage = Serializer.Deserialize<SignedMessage>(new MemoryStream(certData));
            }
            catch
            {
                //Logger.Warn("Failed to parse cert as SignedMessage");
            }

            SignedDeviceCertificate serviceCertificate;
            try
            {
                try
                {
                    //Logger.Debug("Service cert provided as signedmessage");
                    serviceCertificate = Serializer.Deserialize<SignedDeviceCertificate>(new MemoryStream(signedMessage.Msg));
                }
                catch
                {
                    //Logger.Debug("Service cert provided as signeddevicecertificate");
                    serviceCertificate = Serializer.Deserialize<SignedDeviceCertificate>(new MemoryStream(certData));
                }
            }
            catch
            {
                //Logger.Error("Failed to parse service certificate");
                return false;
            }

            Sessions[sessionId].ServiceCertificate = serviceCertificate;
            Sessions[sessionId].PrivacyMode = true;

            return true;
        }

        public static byte[] GetLicenseRequest(string sessionId)
        {
            //Logger.Debug($"GetLicenseRequest(sessionId={Utils.BytesToHex(sessionId)})");
            //Logger.Verbose($"Getting license request");

            if (!Sessions.ContainsKey(sessionId))
            {
                //Logger.Error("Session ID doesn't exist");
                return null;
            }

            var session = Sessions[sessionId];

            //Logger.Debug("Building license request");

            dynamic licenseRequest;

            if (session.InitData is WidevineCencHeader)
            {
                licenseRequest = new SignedLicenseRequest
                {
                    Type = SignedLicenseRequest.MessageType.LicenseRequest,
                    Msg = new LicenseRequest
                    {
                        Type = LicenseRequest.RequestType.New,
                        KeyControlNonce = 1093602366,
                        ProtocolVersion = ProtocolVersion.Current,
                        ContentId = new LicenseRequest.ContentIdentification
                        {
                            CencId = new LicenseRequest.ContentIdentification.Cenc
                            {
                                LicenseType = session.Offline ? LicenseType.Offline : LicenseType.Default,
                                RequestId = session.SessionId,
                                Pssh = session.InitData
                            }
                        }
                    }
                };
            }
            else
            {
                licenseRequest = new SignedLicenseRequestRaw
                {
                    Type = SignedLicenseRequestRaw.MessageType.LicenseRequest,
                    Msg = new LicenseRequestRaw
                    {
                        Type = LicenseRequestRaw.RequestType.New,
                        KeyControlNonce = 1093602366,
                        ProtocolVersion = ProtocolVersion.Current,
                        ContentId = new LicenseRequestRaw.ContentIdentification
                        {
                            CencId = new LicenseRequestRaw.ContentIdentification.Cenc
                            {
                                LicenseType = session.Offline ? LicenseType.Offline : LicenseType.Default,
                                RequestId = session.SessionId,
                                Pssh = session.InitData
                            }
                        }
                    }
                };
            }

            if (session.PrivacyMode)
            {
                //Logger.Debug("Privacy mode & serivce certificate loaded, encrypting client id");

                EncryptedClientIdentification encryptedClientIdProto = new EncryptedClientIdentification();

                //Logger.Debug("Unencrypted client id " + Utils.SerializeToString(clientId));

                using var memoryStream = new MemoryStream();
                Serializer.Serialize(memoryStream, session.Device.ClientID);
                byte[] data = Padding.AddPKCS7Padding(memoryStream.ToArray(), 16);

                using AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider
                {
                    BlockSize = 128,
                    Padding = PaddingMode.PKCS7,
                    Mode = CipherMode.CBC
                };
                aesProvider.GenerateKey();
                aesProvider.GenerateIV();

                using MemoryStream mstream = new MemoryStream();
                using CryptoStream cryptoStream = new CryptoStream(mstream, aesProvider.CreateEncryptor(aesProvider.Key, aesProvider.IV), CryptoStreamMode.Write);
                cryptoStream.Write(data, 0, data.Length);
                encryptedClientIdProto.EncryptedClientId = mstream.ToArray();

                using RSACryptoServiceProvider RSA = new RSACryptoServiceProvider();
                RSA.ImportRSAPublicKey(session.ServiceCertificate.DeviceCertificate.PublicKey, out int bytesRead);
                encryptedClientIdProto.EncryptedPrivacyKey = RSA.Encrypt(aesProvider.Key, RSAEncryptionPadding.OaepSHA1);
                encryptedClientIdProto.EncryptedClientIdIv = aesProvider.IV;
                encryptedClientIdProto.ServiceId = Encoding.UTF8.GetString(session.ServiceCertificate.DeviceCertificate.ServiceId);
                encryptedClientIdProto.ServiceCertificateSerialNumber = session.ServiceCertificate.DeviceCertificate.SerialNumber;

                licenseRequest.Msg.EncryptedClientId = encryptedClientIdProto;
            }
            else
            {
                licenseRequest.Msg.ClientId = session.Device.ClientID;
            }

            //Logger.Debug("Signing license request");

            using (var memoryStream = new MemoryStream())
            {
                Serializer.Serialize(memoryStream, licenseRequest.Msg);
                byte[] data = memoryStream.ToArray();
                session.LicenseRequest = data;

                licenseRequest.Signature = session.Device.Sign(data);
            }

            //Logger.Verbose("License request created");

            byte[] requestBytes;
            using (var memoryStream = new MemoryStream())
            {
                Serializer.Serialize(memoryStream, licenseRequest);
                requestBytes = memoryStream.ToArray();
            }

            Sessions[sessionId] = session;

            //Logger.Debug($"license request b64: {Convert.ToBase64String(requestBytes)}");
            return requestBytes;
        }

        public static void ProvideLicense(string sessionId, byte[] license)
        {
            //Logger.Debug($"ProvideLicense(sessionId={Utils.BytesToHex(sessionId)}, licenseB64={licenseB64})");
            //Logger.Verbose("Decrypting provided license");

            if (!Sessions.ContainsKey(sessionId))
            {
                throw new Exception("Session ID doesn't exist");
            }

            var session = Sessions[sessionId];

            if (session.LicenseRequest == null)
            {
                throw new Exception("Generate a license request first");
            }

            SignedLicense signedLicense;
            try
            {
                signedLicense = Serializer.Deserialize<SignedLicense>(new MemoryStream(license));
            }
            catch
            {
                throw new Exception("Unable to parse license");
            }

            //Logger.Debug("License: " + Utils.SerializeToString(signedLicense));

            session.License = signedLicense;

            //Logger.Debug($"Deriving keys from session key");

            try
            {
                var sessionKey = session.Device.Decrypt(session.License.SessionKey);

                if (sessionKey.Length != 16)
                {
                    throw new Exception("Unable to decrypt session key");
                }

                session.SessionKey = sessionKey;
            }
            catch
            {
                throw new Exception("Unable to decrypt session key");
            }

            //Logger.Debug("Session key: " + Utils.BytesToHex(session.SessionKey));

            session.DerivedKeys = DeriveKeys(session.LicenseRequest, session.SessionKey);

            //Logger.Debug("Verifying license signature");

            byte[] licenseBytes;
            using (var memoryStream = new MemoryStream())
            {
                Serializer.Serialize(memoryStream, signedLicense.Msg);
                licenseBytes = memoryStream.ToArray();
            }
            byte[] hmacHash = CryptoUtils.GetHMACSHA256Digest(licenseBytes, session.DerivedKeys.Auth1);

            if (!hmacHash.SequenceEqual(signedLicense.Signature))
            {
                throw new Exception("License signature mismatch");
            }

            foreach (License.KeyContainer key in signedLicense.Msg.Keys)
            {
                string type = key.Type.ToString();

                if (type == "Signing")
                    continue;

                byte[] keyId;
                byte[] encryptedKey = key.Key;
                byte[] iv = key.Iv;
                keyId = key.Id;
                if (keyId == null)
                {
                    keyId = Encoding.ASCII.GetBytes(key.Type.ToString());
                }

                byte[] decryptedKey;

                using MemoryStream mstream = new MemoryStream();
                using AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider
                {
                    Mode = CipherMode.CBC,
                    Padding = PaddingMode.PKCS7
                };
                using CryptoStream cryptoStream = new CryptoStream(mstream, aesProvider.CreateDecryptor(session.DerivedKeys.Enc, iv), CryptoStreamMode.Write);
                cryptoStream.Write(encryptedKey, 0, encryptedKey.Length);
                decryptedKey = mstream.ToArray();

                List<string> permissions = new List<string>();
                if (type == "OperatorSession")
                {
                    foreach (PropertyInfo perm in key._OperatorSessionKeyPermissions.GetType().GetProperties())
                    {
                        if ((uint)perm.GetValue(key._OperatorSessionKeyPermissions) == 1)
                        {
                            permissions.Add(perm.Name);
                        }
                    }
                }
                session.ContentKeys.Add(new ContentKey
                {
                    KeyID = keyId,
                    Type = type,
                    Bytes = decryptedKey,
                    Permissions = permissions
                });
            }

            //Logger.Debug($"Key count: {session.Keys.Count}");

            Sessions[sessionId] = session;

            //Logger.Verbose("Decrypted all keys");
        }

        public static DerivedKeys DeriveKeys(byte[] message, byte[] key)
        {
            byte[] encKeyBase = Encoding.UTF8.GetBytes("ENCRYPTION").Concat(new byte[] { 0x0, }).Concat(message).Concat(new byte[] { 0x0, 0x0, 0x0, 0x80 }).ToArray();
            byte[] authKeyBase = Encoding.UTF8.GetBytes("AUTHENTICATION").Concat(new byte[] { 0x0, }).Concat(message).Concat(new byte[] { 0x0, 0x0, 0x2, 0x0 }).ToArray();

            byte[] encKey = new byte[] { 0x01 }.Concat(encKeyBase).ToArray();
            byte[] authKey1 = new byte[] { 0x01 }.Concat(authKeyBase).ToArray();
            byte[] authKey2 = new byte[] { 0x02 }.Concat(authKeyBase).ToArray();
            byte[] authKey3 = new byte[] { 0x03 }.Concat(authKeyBase).ToArray();
            byte[] authKey4 = new byte[] { 0x04 }.Concat(authKeyBase).ToArray();

            byte[] encCmacKey = CryptoUtils.GetCMACDigest(encKey, key);
            byte[] authCmacKey1 = CryptoUtils.GetCMACDigest(authKey1, key);
            byte[] authCmacKey2 = CryptoUtils.GetCMACDigest(authKey2, key);
            byte[] authCmacKey3 = CryptoUtils.GetCMACDigest(authKey3, key);
            byte[] authCmacKey4 = CryptoUtils.GetCMACDigest(authKey4, key);

            byte[] authCmacCombined1 = authCmacKey1.Concat(authCmacKey2).ToArray();
            byte[] authCmacCombined2 = authCmacKey3.Concat(authCmacKey4).ToArray();

            return new DerivedKeys
            {
                Auth1 = authCmacCombined1,
                Auth2 = authCmacCombined2,
                Enc = encCmacKey
            };
        }

        public static List<ContentKey> GetKeys(string sessionId)
        {
            if (Sessions.ContainsKey(sessionId))
                return Sessions[sessionId].ContentKeys;
            else
            {
                throw new Exception("Session not found");
            }
        }
    }
}



/*
        public static List<string> ProvideLicense(string requestB64, string licenseB64)
        {
            byte[] licenseRequest;

            var request = Serializer.Deserialize<SignedLicenseRequest>(new MemoryStream(Convert.FromBase64String(requestB64)));

            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, request.Msg);
                licenseRequest = ms.ToArray();
            }

            SignedLicense signedLicense;
            try
            {
                signedLicense = Serializer.Deserialize<SignedLicense>(new MemoryStream(Convert.FromBase64String(licenseB64)));
            }
            catch
            {
                return null;
            }

            byte[] sessionKey;
            try
            {

                sessionKey = Controllers.Adapter.OaepDecrypt(Convert.ToBase64String(signedLicense.SessionKey));

                if (sessionKey.Length != 16)
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }

            byte[] encKeyBase = Encoding.UTF8.GetBytes("ENCRYPTION").Concat(new byte[] { 0x0, }).Concat(licenseRequest).Concat(new byte[] { 0x0, 0x0, 0x0, 0x80 }).ToArray();

            byte[] encKey = new byte[] { 0x01 }.Concat(encKeyBase).ToArray();

            byte[] encCmacKey = GetCmacDigest(encKey, sessionKey);

            byte[] encryptionKey = encCmacKey;

            List<string> keys = new List<string>();
           
            foreach (License.KeyContainer key in signedLicense.Msg.Keys)
            {
                string type = key.Type.ToString();
                if (type == "Signing")
                {
                    continue;
                }

                byte[] keyId;
                byte[] encryptedKey = key.Key;
                byte[] iv = key.Iv;
                keyId = key.Id;
                if (keyId == null)
                {
                    keyId = Encoding.ASCII.GetBytes(key.Type.ToString());
                }

                byte[] decryptedKey;

                using MemoryStream mstream = new MemoryStream();
                using AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider
                {
                    Mode = CipherMode.CBC,
                    Padding = PaddingMode.PKCS7
                };
                using CryptoStream cryptoStream = new CryptoStream(mstream, aesProvider.CreateDecryptor(encryptionKey, iv), CryptoStreamMode.Write);
                cryptoStream.Write(encryptedKey, 0, encryptedKey.Length);
                decryptedKey = mstream.ToArray();

                List<string> permissions = new List<string>();
                if (type == "OPERATOR_SESSION")
                {
                    foreach (FieldInfo perm in key._OperatorSessionKeyPermissions.GetType().GetFields())
                    {
                        if ((uint)perm.GetValue(key._OperatorSessionKeyPermissions) == 1)
                        {
                            permissions.Add(perm.Name);
                        }
                    }
                }
                keys.Add(BitConverter.ToString(keyId).Replace("-","").ToLower() + ":" + BitConverter.ToString(decryptedKey).Replace("-", "").ToLower());
            }

            return keys;
        }*/
