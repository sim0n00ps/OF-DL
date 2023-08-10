using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities.Messages
{
    public class MessageCollection
    {
        public Dictionary<long, string> Messages = new Dictionary<long, string>();
        public List<Messages.List> MessageObjects = new List<Messages.List>();
        public List<Messages.Medium> MessageMedia = new List<Messages.Medium>();
    }
}
