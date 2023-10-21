using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities.Streams
{
    public class StreamsCollection
    {
        public Dictionary<long, string> Streams = new Dictionary<long, string>();
        public List<Streams.List> StreamObjects = new List<Streams.List>();
        public List<Streams.Medium> StreamMedia = new List<Streams.Medium>();
    }
}
