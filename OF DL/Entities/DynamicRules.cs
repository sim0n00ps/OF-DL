using Newtonsoft.Json;

namespace OF_DL.Entities
{
    public class DynamicRules
    {
        [JsonProperty(PropertyName="app-token")]
        public string? AppToken { get; set; }

        [JsonProperty(PropertyName="static_param")]
        public string? StaticParam { get; set; }

        [JsonProperty(PropertyName="prefix")]
        public string? Prefix { get; set; }

        [JsonProperty(PropertyName="suffix")]
        public string? Suffix { get; set; }

        [JsonProperty(PropertyName="checksum_constant")]
        public int? ChecksumConstant { get; set; }

        [JsonProperty(PropertyName = "checksum_indexes")]
        public List<int> ChecksumIndexes { get; set; }
    }
}
