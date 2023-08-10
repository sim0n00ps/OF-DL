using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities.Purchased
{
    public class PaidMessageCollection
    {
        public Dictionary<long, string> PaidMessages = new Dictionary<long, string>();
        public List<Purchased.List> PaidMessageObjects = new List<Purchased.List>();
        public List<Purchased.Medium> PaidMessageMedia = new List<Purchased.Medium>();
    }
}
