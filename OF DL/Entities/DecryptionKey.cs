using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities
{
	public class DecryptionKey
	{
		public string pssh { get; set; }
		public string time { get; set; }
		public List<Key> keys { get; set; }
		public class Key
		{
			public string key { get; set; }
		}
	}
}
