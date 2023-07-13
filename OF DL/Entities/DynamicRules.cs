using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities
{
    public class DynamicRules
    {
        public string? static_param { get; set; }
        public string? start { get; set; }
        public string? end { get; set; }
        public int? checksum_constant { get; set; }
        public List<int>? checksum_indexes { get; set; }
        public string? app_token { get; set; }
        public List<string>? remove_headers { get; set; }
        public string? revision { get; set; }
        public bool? is_current { get; set; }
    }
}
