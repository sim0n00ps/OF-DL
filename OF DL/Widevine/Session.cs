using System.Collections.Generic;

namespace WidevineClient.Widevine
{
    class Session
    {
        public byte[] SessionId { get; set; }
        public dynamic InitData { get; set; }
        public bool Offline { get; set; }
        public CDMDevice Device { get; set; }
        public byte[] SessionKey { get; set; }
        public DerivedKeys DerivedKeys { get; set; }
        public byte[] LicenseRequest { get; set; }
        public SignedLicense License { get; set; }
        public SignedDeviceCertificate ServiceCertificate { get; set; }
        public bool PrivacyMode { get; set; }
        public List<ContentKey> ContentKeys { get; set; } = new List<ContentKey>();

        public Session(byte[] sessionId, dynamic initData, CDMDevice device, bool offline)
        {
            SessionId = sessionId;
            InitData = initData;
            Offline = offline;
            Device = device;
        }
    }
}