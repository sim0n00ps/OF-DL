using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities.Post
{
    public class SinglePostCollection
    {
        public Dictionary<long, string> SinglePosts = new Dictionary<long, string>();
        public List<SinglePost> SinglePostObjects = new List<SinglePost>();
        public List<SinglePost.Medium> SinglePostMedia = new List<SinglePost.Medium>();
    }
}
