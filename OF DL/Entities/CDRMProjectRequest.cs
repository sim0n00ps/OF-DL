using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities
{
    public class CDRMProjectRequest
    {
        [JsonProperty("pssh")]
        public string PSSH { get; set; } = "";

        [JsonProperty("licurl")]
        public string LicenseURL { get; set; } = "";
        [JsonProperty("headers")]
        public string Headers { get; set; } = "";
        [JsonProperty("cookies")]
        public string Cookies { get; set; } = "";
        [JsonProperty("data")]
        public string Data { get; set; } = "";
    }
}
