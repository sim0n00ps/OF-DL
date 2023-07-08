using System;
using System.Collections.Generic;

namespace WidevineClient.Widevine
{
    public class CDMApi
    {
        string SessionId { get; set; }

        public byte[] GetChallenge(string initDataB64, string certDataB64, bool offline = false, bool raw = false)
        {
            SessionId = CDM.OpenSession(initDataB64, "chrome_1610", offline, raw);
            CDM.SetServiceCertificate(SessionId, Convert.FromBase64String(certDataB64));
            return CDM.GetLicenseRequest(SessionId);
        }

        public bool ProvideLicense(string licenseB64)
        {
            CDM.ProvideLicense(SessionId, Convert.FromBase64String(licenseB64));
            return true;
        }

        public List<ContentKey> GetKeys()
        {
            return CDM.GetKeys(SessionId);
        }
    }
}
