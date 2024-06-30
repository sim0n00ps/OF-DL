using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OF_DL.Entities.Messages.Messages;

namespace OF_DL.Entities.Purchased
{
    public class PaidMessageCollection
    {
        public Dictionary<long, string> PaidMessages = new Dictionary<long, string>();
        public List<Purchased.List> PaidMessageObjects = new List<Purchased.List>();
        public List<Medium> PaidMessageMedia = new List<Medium>();
    }
}
