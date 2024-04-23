using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities.Purchased
{
    public class PurchasedTabCollection
    {
        public long UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public PaidPostCollection PaidPosts { get; set; } = new PaidPostCollection();
        public PaidMessageCollection PaidMessages { get; set; } = new PaidMessageCollection();
    }
}
