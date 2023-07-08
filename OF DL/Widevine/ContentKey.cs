using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;

namespace WidevineClient.Widevine
{
    [Serializable]
    public class ContentKey
    {
        [JsonPropertyName("key_id")]
        public byte[] KeyID { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("bytes")]
        public byte[] Bytes { get; set; }

        [NotMapped]
        [JsonPropertyName("permissions")]
        public List<string> Permissions {
            get
            {
                return PermissionsString.Split(",").ToList();
            }
            set
            {
                PermissionsString = string.Join(",", value);
            }
        }

        [JsonIgnore]
        public string PermissionsString { get; set; }

        public override string ToString()
        {
            return $"{BitConverter.ToString(KeyID).Replace("-", "").ToLower()}:{BitConverter.ToString(Bytes).Replace("-", "").ToLower()}";
        }
    }
}
