using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OF_DL.Utils
{
    internal static class XmlUtils
    {
        public static string EvaluateInnerText(string xmlValue)
        {
            try
            {
                var parsedText = XElement.Parse($"<root>{xmlValue}</root>");
                return parsedText.Value;
            }
            catch
            { }

            return string.Empty;
        }
    }
}
