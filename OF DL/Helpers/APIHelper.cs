using Newtonsoft.Json;
using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Highlights;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Purchased;
using OF_DL.Entities.Stories;
using OF_DL.Enumurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static OF_DL.Entities.Messages.Messages;

namespace OF_DL.Helpers
{
	public class APIHelper
	{
		public async Task<Dictionary<string, string>> Headers(string path, string queryParams)
		{
			DynamicRules root = new DynamicRules();
			var client = new HttpClient();
			var request = new HttpRequestMessage
			{
				Method = HttpMethod.Get,
				RequestUri = new Uri("https://raw.githubusercontent.com/deviint/onlyfans-dynamic-rules/main/dynamicRules.json"),
			};
			using (var vresponse = client.Send(request))
			{
				vresponse.EnsureSuccessStatusCode();
				var body = await vresponse.Content.ReadAsStringAsync();
				root = JsonConvert.DeserializeObject<DynamicRules>(body);
			}

			long timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

			Program program = new Program(new APIHelper(), new DownloadHelper());

			string input = $"{root.static_param}\n{timestamp}\n{path + queryParams}\n{program.auth.USER_ID}";
			string hashString = string.Empty;
			using (SHA1 sha1 = SHA1.Create())
			{
				byte[] inputBytes = Encoding.UTF8.GetBytes(input);
				byte[] hashBytes = sha1.ComputeHash(inputBytes);
				hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
			}

			int checksum = 0;
			foreach (int number in root.checksum_indexes)
			{
				List<int> test = new List<int>
			{
				hashString[number]
			};
				checksum = checksum + test.Sum();
			}
			checksum = checksum + root.checksum_constant;
			string sign = $"{root.start}:{hashString}:{checksum.ToString("X").ToLower()}:{root.end}";

			Dictionary<string, string> headers = new Dictionary<string, string>
			{
				{ "accept", "application/json, text/plain, */*" },
				{ "app-token", root.app_token },
				{ "cookie", program.auth.COOKIE },
				{ "sign", sign },
				{ "time", timestamp.ToString() },
				{ "user-id", program.auth.USER_ID },
				{ "user-agent", program.auth.USER_AGENT },
				{ "x-bc", program.auth.X_BC }
			};
			return headers;
		}
		public async Task<User> GetUserInfo(string endpoint)
		{
			try
			{
				User user = new User();
				int post_limit = 50;
				Dictionary<string, string> GetParams = new Dictionary<string, string>
				{
					{ "limit", post_limit.ToString() },
					{ "order", "publish_date_asc" }
				};

				string queryParams = "?";
				foreach (KeyValuePair<string, string> kvp in GetParams)
				{
					if (kvp.Key == GetParams.Keys.Last())
					{
						queryParams += $"{kvp.Key}={kvp.Value}";
					}
					else
					{
						queryParams += $"{kvp.Key}={kvp.Value}&";
					}
				}

				Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams);

				HttpClient client = new HttpClient();

				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + queryParams);

				foreach (KeyValuePair<string, string> keyValuePair in headers)
				{
					request.Headers.Add(keyValuePair.Key, keyValuePair.Value);
				}
				var jsonSerializerSettings = new JsonSerializerSettings();
				jsonSerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;
				using (var response = await client.SendAsync(request))
				{
					if (!response.IsSuccessStatusCode)
					{
						return user;
					}
					else
					{
						response.EnsureSuccessStatusCode();
						var body = await response.Content.ReadAsStringAsync();
						user = JsonConvert.DeserializeObject<User>(body, jsonSerializerSettings);
					}

				}
				return user;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
			return null;
		}
		public async Task<Dictionary<string, int>> GetSubscriptions(string endpoint)
		{
			try
			{
				int post_limit = 50;
				int offset = 0;
				bool loop = true;
				Dictionary<string, string> GetParams = new Dictionary<string, string>
				{
					{ "limit", post_limit.ToString() },
					{ "order", "publish_date_asc" },
					{ "type", "active" }
				};
				Dictionary<string, int> users = new Dictionary<string, int>();
				while (loop)
				{
					string queryParams = "?";
					foreach (KeyValuePair<string, string> kvp in GetParams)
					{
						if (kvp.Key == GetParams.Keys.Last())
						{
							queryParams += $"{kvp.Key}={kvp.Value}";
						}
						else
						{
							queryParams += $"{kvp.Key}={kvp.Value}&";
						}
					}

					Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams);

					HttpClient client = new HttpClient();

					HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + queryParams);

					foreach (KeyValuePair<string, string> keyValuePair in headers)
					{
						request.Headers.Add(keyValuePair.Key, keyValuePair.Value);
					}
					using (var response = await client.SendAsync(request))
					{
						response.EnsureSuccessStatusCode();
						var body = await response.Content.ReadAsStringAsync();
						List<Subscription> subscriptions = JsonConvert.DeserializeObject<List<Subscription>>(body);
						if (subscriptions != null)
						{
							foreach (Subscription sub in subscriptions)
							{
								users.Add(sub.username, sub.id);
							}
							if (subscriptions.Count >= 50)
							{
								offset = offset + 50;
								GetParams["offset"] = Convert.ToString(offset);
							}
							else
							{
								loop = false;
							}
						}
						else
						{
							loop = false;
						}
					}
				}
				return users.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
			return null;
		}
		public async Task<List<string>> GetMedia(MediaType mediatype, string endpoint, string? username)
		{
			try
			{
				List<string> return_urls = new List<string>();
				int post_limit = 50;
				int limit = 5;
				int offset = 0;
				List<Purchased> paidposts = new List<Purchased>();
				bool isPaidPosts = false;
				List<Post> posts = new List<Post>();
				bool isPosts = false;
				Messages messages = new Messages();
				bool isMessages = false;
				List<Archived> archived = new List<Archived>();
				bool isArchived = false;
				List<Stories> stories = new List<Stories>();
				bool isStories = false;
				Highlights highlights = new Highlights();
				bool isHighlights = false;
				List<Purchased> paidMessages = new List<Purchased>();
				bool isPurchased = false;

				Dictionary<string, string> GetParams = null;

				switch (mediatype)
				{
					case MediaType.PaidPosts:
						isPaidPosts = true;
						GetParams = new Dictionary<string, string>
						{
							{ "limit", post_limit.ToString() },
							{ "order", "publish_date_asc" },
							{ "user_id", username }
						};
						break;

					case MediaType.Posts:
						isPosts = true;
						GetParams = new Dictionary<string, string>
						{
							{ "limit", post_limit.ToString() },
							{ "order", "publish_date_asc" }
						};
						break;

					case MediaType.Archived:
						isArchived = true;
						GetParams = new Dictionary<string, string>
						{
							{ "limit", post_limit.ToString() },
							{ "order", "publish_date_asc" }
						};
						break;

					case MediaType.Stories:
						isStories = true;
						GetParams = new Dictionary<string, string>
						{
							{ "limit", post_limit.ToString() },
							{ "order", "publish_date_asc" }
						};
						break;

					case MediaType.Highlights:
						isHighlights = true;
						GetParams = new Dictionary<string, string>
						{
							{ "limit", limit.ToString() },
							{ "offset", offset.ToString() }
						};
						break;

					case MediaType.Messages:
						isMessages = true;
						GetParams = new Dictionary<string, string>
						{
							{ "limit", post_limit.ToString() },
							{ "order", "desc" }
						};
						break;

					case MediaType.PaidMessages:
						isPurchased = true;
						GetParams = new Dictionary<string, string>
						{
							{ "limit", post_limit.ToString() },
							{ "order", "publish_date_asc" },
							{ "user_id", username }
						};
						break;
				}

				string queryParams = "?";
				foreach (KeyValuePair<string, string> kvp in GetParams)
				{
					if (kvp.Key == GetParams.Keys.Last())
					{
						queryParams += $"{kvp.Key}={kvp.Value}";
					}
					else
					{
						queryParams += $"{kvp.Key}={kvp.Value}&";
					}
				}

				Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams);

				HttpClient client = new HttpClient();

				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + queryParams);

				foreach (KeyValuePair<string, string> keyValuePair in headers)
				{
					request.Headers.Add(keyValuePair.Key, keyValuePair.Value);
				}

				var jsonSerializerSettings = new JsonSerializerSettings();
				jsonSerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;
				using (var response = await client.SendAsync(request))
				{
					response.EnsureSuccessStatusCode();
					var body = await response.Content.ReadAsStringAsync();
					if (isPaidPosts)
					{
						Program program = new Program(new APIHelper(), new DownloadHelper());
						paidposts = JsonConvert.DeserializeObject<List<Purchased>>(body, jsonSerializerSettings);
						if (paidposts.Count >= post_limit)
						{
							GetParams["offset"] = post_limit.ToString();
							while (true)
							{
								string loopqueryParams = "?";
								foreach (KeyValuePair<string, string> kvp in GetParams)
								{
									if (kvp.Key == GetParams.Keys.Last())
									{
										loopqueryParams += $"{kvp.Key}={kvp.Value}";
									}
									else
									{
										loopqueryParams += $"{kvp.Key}={kvp.Value}&";
									}
								}
								List<Purchased> newPaidPosts = new List<Purchased>();
								Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams);
								HttpClient loopclient = new HttpClient();

								HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

								foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
								{
									looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
								}
								using (var loopresponse = await loopclient.SendAsync(looprequest))
								{
									loopresponse.EnsureSuccessStatusCode();
									var loopbody = await loopresponse.Content.ReadAsStringAsync();
									newPaidPosts = JsonConvert.DeserializeObject<List<Purchased>>(loopbody, jsonSerializerSettings);
								}
								paidposts.AddRange(newPaidPosts);
								if (newPaidPosts.Count < post_limit)
								{
									break;
								}
								GetParams["offset"] = Convert.ToString(Convert.ToInt32(GetParams["offset"]) + post_limit);
							}
						}

						foreach (Purchased purchase in paidposts)
						{
							if (purchase.responseType == "post" && purchase.media != null && purchase.media.Count > 0)
							{
								List<long> previewids = new List<long>();
								if (purchase.previews != null)
								{
									for (int i = 0; i < purchase.previews.Count; i++)
									{
										previewids.Add((long)purchase.previews[i]);
									}
								}
								else if (purchase.preview != null)
								{
									for (int i = 0; i < purchase.preview.Count; i++)
									{
										previewids.Add((long)purchase.preview[i]);
									}
								}

								foreach (Purchased.Medium medium in purchase.media)
								{
									program.paid_post_ids.Add(medium.id);
									if (previewids.Count > 0)
									{
										bool has = previewids.Any(cus => cus.Equals(medium.id));
										if (!has && medium.canView && !medium.source.source.Contains("upload"))
										{
											return_urls.Add(medium.source.source);
										}
									}
									else
									{
										if (medium.canView && !medium.source.source.Contains("upload"))
										{
											return_urls.Add(medium.source.source);
										}
									}
								}
							}
						}
					}
					else if (isPosts)
					{
						Program program = new Program(new APIHelper(), new DownloadHelper());
						posts = JsonConvert.DeserializeObject<List<Post>>(body, jsonSerializerSettings);
						if (posts.Count >= post_limit)
						{
							GetParams["afterPublishTime"] = posts[posts.Count - 1].postedAtPrecise;
							while (true)
							{
								string loopqueryParams = "?";
								foreach (KeyValuePair<string, string> kvp in GetParams)
								{
									if (kvp.Key == GetParams.Keys.Last())
									{
										loopqueryParams += $"{kvp.Key}={kvp.Value}";
									}
									else
									{
										loopqueryParams += $"{kvp.Key}={kvp.Value}&";
									}
								}
								List<Post> newposts = new List<Post>();
								Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams);
								HttpClient loopclient = new HttpClient();

								HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

								foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
								{
									looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
								}
								using (var loopresponse = await loopclient.SendAsync(looprequest))
								{
									loopresponse.EnsureSuccessStatusCode();
									var loopbody = await loopresponse.Content.ReadAsStringAsync();
									newposts = JsonConvert.DeserializeObject<List<Post>>(loopbody, jsonSerializerSettings);
								}
								posts.AddRange(newposts);
								if (newposts.Count < post_limit)
								{
									break;
								}
								GetParams["afterPublishTime"] = newposts[newposts.Count - 1].postedAtPrecise;
							}
						}

						foreach (Post post in posts)
						{
							if (post.media != null && post.media.Count > 0)
							{
								foreach (Post.Medium medium in post.media)
								{
									if (medium.canView)
									{
										bool has = program.paid_post_ids.Any(cus => cus.Equals(medium.id));
										if (!has && !medium.source.source.Contains("upload"))
										{
											return_urls.Add(medium.source.source);
										}
									}
								}
							}
						}
					}
					else if (isArchived)
					{
						archived = JsonConvert.DeserializeObject<List<Archived>>(body, jsonSerializerSettings);
						foreach (Archived archive in archived)
						{
							if (archive.media != null && archive.media.Count > 0)
							{
								foreach (Archived.Medium medium in archive.media)
								{
									if (medium.canView && !medium.source.source.Contains("upload"))
									{
										return_urls.Add(medium.source.source);
									}
								}
							}
						}
					}
					else if (isStories)
					{
						stories = JsonConvert.DeserializeObject<List<Stories>>(body, jsonSerializerSettings);
						foreach (Stories story in stories)
						{
							if (story.media != null && story.media.Count > 0)
							{
								foreach (Stories.Medium medium in story.media)
								{
									if (medium.canView && !medium.files.source.url.Contains("upload"))
									{
										return_urls.Add(medium.files.source.url);
									}
								}
							}
						}
					}
					else if (isHighlights)
					{
						List<string> highlight_ids = new List<string>();
						highlights = JsonConvert.DeserializeObject<Highlights>(body, jsonSerializerSettings);
						if (highlights.hasMore)
						{
							offset = offset + 5;
							GetParams["offset"] = offset.ToString();
							while (true)
							{
								string loopqueryParams = "?";
								foreach (KeyValuePair<string, string> kvp in GetParams)
								{
									if (kvp.Key == GetParams.Keys.Last())
									{
										loopqueryParams += $"{kvp.Key}={kvp.Value}";
									}
									else
									{
										loopqueryParams += $"{kvp.Key}={kvp.Value}&";
									}
								}
								Highlights newhighlights = new Highlights();
								Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams);
								HttpClient loopclient = new HttpClient();

								HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

								foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
								{
									looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
								}
								using (var loopresponse = await loopclient.SendAsync(looprequest))
								{
									loopresponse.EnsureSuccessStatusCode();
									var loopbody = await loopresponse.Content.ReadAsStringAsync();
									newhighlights = JsonConvert.DeserializeObject<Highlights>(loopbody, jsonSerializerSettings);
								}
								highlights.list.AddRange(newhighlights.list);
								if (!newhighlights.hasMore)
								{
									break;
								}
								offset = offset + 5;
								GetParams["offset"] = offset.ToString();
							}
						}
						foreach (Highlights.List list in highlights.list)
						{
							highlight_ids.Add(list.id.ToString());
						}

						foreach (string highlight_id in highlight_ids)
						{
							HighlightMedia highlightMedia = new HighlightMedia();
							Dictionary<string, string> highlight_headers = await Headers("/api2/v2/stories/highlights/" + highlight_id, string.Empty);

							HttpClient highlight_client = new HttpClient();

							HttpRequestMessage highlight_request = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2/stories/highlights/" + highlight_id);

							foreach (KeyValuePair<string, string> keyValuePair in highlight_headers)
							{
								highlight_request.Headers.Add(keyValuePair.Key, keyValuePair.Value);
							}

							var highlightJsonSerializerSettings = new JsonSerializerSettings();
							highlightJsonSerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;
							using (var highlightResponse = await highlight_client.SendAsync(highlight_request))
							{
								response.EnsureSuccessStatusCode();
								var highlightBody = await highlightResponse.Content.ReadAsStringAsync();
								highlightMedia = JsonConvert.DeserializeObject<HighlightMedia>(highlightBody, highlightJsonSerializerSettings);
								if (highlightMedia != null)
								{
									foreach (var item in highlightMedia.stories)
									{
										if (!item.media[0].files.source.url.Contains("upload"))
										{
											return_urls.Add(item.media[0].files.source.url);
										}
									}
								}
							}
						}
					}
					else if (isMessages)
					{
						messages = JsonConvert.DeserializeObject<Messages>(body, jsonSerializerSettings);
						if (messages.hasMore)
						{
							GetParams["id"] = messages.list[messages.list.Count - 1].id.ToString();
							while (true)
							{
								string loopqueryParams = "?";
								foreach (KeyValuePair<string, string> kvp in GetParams)
								{
									if (kvp.Key == GetParams.Keys.Last())
									{
										loopqueryParams += $"{kvp.Key}={kvp.Value}";
									}
									else
									{
										loopqueryParams += $"{kvp.Key}={kvp.Value}&";
									}
								}
								Messages newmessages = new Messages();
								Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams);
								HttpClient loopclient = new HttpClient();

								HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

								foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
								{
									looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
								}
								using (var loopresponse = await loopclient.SendAsync(looprequest))
								{
									loopresponse.EnsureSuccessStatusCode();
									var loopbody = await loopresponse.Content.ReadAsStringAsync();
									newmessages = JsonConvert.DeserializeObject<Messages>(loopbody, jsonSerializerSettings);
								}
								messages.list.AddRange(newmessages.list);
								if (!newmessages.hasMore)
								{
									break;
								}
								GetParams["id"] = newmessages.list[newmessages.list.Count - 1].id.ToString();
							}
						}
						foreach (List list in messages.list)
						{
							if (list.canPurchaseReason != "opened" && list.media != null && list.media.Count > 0)
							{
								foreach (Messages.Medium medium in list.media)
								{
									if (medium.canView && medium.source.source != null && !medium.source.source.Contains("upload"))
									{
										return_urls.Add(medium.source.source.ToString());
									}
								}
							}
						}
					}
					else if (isPurchased)
					{
						paidMessages = JsonConvert.DeserializeObject<List<Purchased>>(body, jsonSerializerSettings);
						if (paidMessages.Count >= post_limit)
						{
							GetParams["offset"] = post_limit.ToString();
							while (true)
							{
								string loopqueryParams = "?";
								foreach (KeyValuePair<string, string> kvp in GetParams)
								{
									if (kvp.Key == GetParams.Keys.Last())
									{
										loopqueryParams += $"{kvp.Key}={kvp.Value}";
									}
									else
									{
										loopqueryParams += $"{kvp.Key}={kvp.Value}&";
									}
								}
								List<Purchased> newpaidMessages = new List<Purchased>();
								Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams);
								HttpClient loopclient = new HttpClient();

								HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

								foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
								{
									looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
								}
								using (var loopresponse = await loopclient.SendAsync(looprequest))
								{
									loopresponse.EnsureSuccessStatusCode();
									var loopbody = await loopresponse.Content.ReadAsStringAsync();
									newpaidMessages = JsonConvert.DeserializeObject<List<Purchased>>(loopbody, jsonSerializerSettings);
								}
								paidMessages.AddRange(newpaidMessages);
								if (newpaidMessages.Count < post_limit)
								{
									break;
								}
								GetParams["offset"] = Convert.ToString(Convert.ToInt32(GetParams["offset"]) + post_limit);
							}
						}

						foreach (Purchased purchase in paidMessages)
						{
							if (purchase.media != null && purchase.media.Count > 0)
							{
								List<long> previewids = new List<long>();
								if (purchase.previews != null)
								{
									for (int i = 0; i < purchase.previews.Count; i++)
									{
										previewids.Add((long)purchase.previews[i]);
									}
								}
								else if (purchase.preview != null)
								{
									for (int i = 0; i < purchase.preview.Count; i++)
									{
										previewids.Add((long)purchase.preview[i]);
									}
								}

								foreach (Purchased.Medium medium in purchase.media)
								{
									if (previewids.Count > 0)
									{
										bool has = previewids.Any(cus => cus.Equals(medium.id));
										if (!has && medium.canView && !medium.source.source.Contains("upload"))
										{
											return_urls.Add(medium.source.source);
										}
									}
									else
									{
										if (medium.canView && !medium.source.source.Contains("upload"))
										{
											return_urls.Add(medium.source.source);
										}
									}
								}
							}
						}
					}
				}
				return return_urls;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
			return null;
		}
	}
}
