using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities.Highlights
{
	public class Highlights
	{
		public List<List> list { get; set; }
		public bool hasMore { get; set; }
		public class List
		{
			public int id { get; set; }
			public int userId { get; set; }
			public string title { get; set; }
			public int coverStoryId { get; set; }
			public string cover { get; set; }
			public int storiesCount { get; set; }
			public DateTime createdAt { get; set; }
		}
	}
}
