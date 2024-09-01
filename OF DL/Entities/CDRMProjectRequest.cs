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
        public string PSSH { get; set; } = "";

        [JsonProperty("License URL")]
        public string LicenseURL { get; set; } = "";
        public string Headers { get; set; } = "";
        public string JSON { get; set; } = "";
        public string Cookies { get; set; } = "";
        public string Data { get; set; } = "";
        public string Proxy { get; set; } = "";
    }
}
