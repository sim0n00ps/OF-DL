using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities.Lists
{
	public class UserList
	{
		public List<List> list { get; set; }
		public bool? hasMore { get; set; }
		public class List
		{
			public string id { get; set; }
			public string type { get; set; }
			public string name { get; set; }
			public int? usersCount { get; set; }
			public int? postsCount { get; set; }
			public bool? canUpdate { get; set; }
			public bool? canDelete { get; set; }
			public bool? canManageUsers { get; set; }
			public bool? canAddUsers { get; set; }
			public bool? canPinnedToFeed { get; set; }
			public bool? isPinnedToFeed { get; set; }
			public bool? canPinnedToChat { get; set; }
			public bool? isPinnedToChat { get; set; }
			public string order { get; set; }
			public string direction { get; set; }
			public List<User> users { get; set; }
			public List<object> customOrderUsersIds { get; set; }
			public List<object> posts { get; set; }
		}

		public class User
		{
			public int? id { get; set; }
			public string _view { get; set; }
		}
	}
}
