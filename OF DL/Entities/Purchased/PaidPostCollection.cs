using static OF_DL.Entities.Messages.Messages;

namespace OF_DL.Entities.Purchased
{
    public class PaidPostCollection
    {
        public Dictionary<long, string> PaidPosts = new Dictionary<long, string>();
        public List<Purchased.List> PaidPostObjects = new List<Purchased.List>();
        public List<Medium> PaidPostMedia = new List<Medium>();
    }
}
