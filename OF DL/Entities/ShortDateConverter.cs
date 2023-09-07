using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace OF_DL.Entities
{
    public class ShortDateConverter : IsoDateTimeConverter
    {
        public ShortDateConverter()
        {
            DateTimeFormat = "yyyy-MM-dd";
        }
    }
}
