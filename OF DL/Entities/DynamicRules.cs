using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OF_DL.Entities
{
    public class DynamicRules
    {
        public string? static_param { get; set; }
        public string? prefix { get; set; }
        public string? suffix { get; set; }
        public int? checksum_constant { get; set; }
        public List<int>? checksum_indexes { get; set; }

        [JsonProperty("app-token")]
        private string? app_token2 { get; set; }
        private string? app_token { get; set; }
        public List<string>? remove_headers { get; set; }
        public string? revision { get; set; }
        public bool? is_current { get; set; }

        /// <summary>
        /// Some JSON return app_token, others have app-token
        /// </summary>
        public string? AppToken
        {
            get
            {
                return string.IsNullOrWhiteSpace(app_token) ? app_token2 : app_token;
            }
        }
    }
}
