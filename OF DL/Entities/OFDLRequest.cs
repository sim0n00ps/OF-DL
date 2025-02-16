using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities
{
    public class OFDLRequest
    {
        [JsonProperty("pssh")]
        public string PSSH { get; set; } = "";

        [JsonProperty("licenceURL")]
        public string LicenseURL { get; set; } = "";

        [JsonProperty("headers")]
        public string Headers { get; set; } = "";
    }
}
