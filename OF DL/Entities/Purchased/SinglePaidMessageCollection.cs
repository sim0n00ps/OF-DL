using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OF_DL.Entities.Messages.Messages;

namespace OF_DL.Entities.Purchased
{
    public class SinglePaidMessageCollection
    {
        public Dictionary<long, string> SingleMessages = new Dictionary<long, string>();
        public List<SingleMessage> SingleMessageObjects = new List<SingleMessage>();
        public List<Medium> SingleMessageMedia = new List<Medium>();
    }
}
