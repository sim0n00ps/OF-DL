using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities.Post
{
    public class PostCollection
    {
        public Dictionary<long, string> Posts = new Dictionary<long, string>();
        public List<Post.List> PostObjects = new List<Post.List>();
        public List<Post.Medium> PostMedia = new List<Post.Medium>();
    }
}
