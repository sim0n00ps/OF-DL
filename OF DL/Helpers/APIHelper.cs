using HtmlAgilityPack;
using Newtonsoft.Json;
using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Highlights;
using OF_DL.Entities.Lists;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Purchased;
using OF_DL.Entities.Stories;
using OF_DL.Enumurations;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using WidevineClient.Widevine;
using static WidevineClient.HttpUtil;

namespace OF_DL.Helpers
{
    public class APIHelper : IAPIHelper
    {
        public async Task<Dictionary<string, string>> Headers(string path, string queryParams, Auth auth)
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


            string input = $"{root.static_param}\n{timestamp}\n{path + queryParams}\n{auth.USER_ID}";
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
            checksum = checksum + root.checksum_constant.Value;
            string sign = $"{root.start}:{hashString}:{checksum.ToString("X").ToLower()}:{root.end}";

            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "accept", "application/json, text/plain" },
                { "app-token", root.app_token },
                { "cookie", auth.COOKIE },
                { "sign", sign },
                { "time", timestamp.ToString() },
                { "user-id", auth.USER_ID },
                { "user-agent", auth.USER_AGENT },
                { "x-bc", auth.X_BC }
            };
            return headers;
        }
        public async Task<Entities.User> GetUserInfo(string endpoint, Auth auth)
        {
            try
            {
                Entities.User user = new Entities.User();
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

                Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams, auth);

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
                        user = JsonConvert.DeserializeObject<Entities.User>(body, jsonSerializerSettings);
                    }

                }
                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public async Task<Dictionary<string, int>> GetSubscriptions(string endpoint, bool includeExpiredSubscriptions, Auth auth)
        {
            try
            {
                int post_limit = 50;
                Dictionary<string, string> GetParams = new Dictionary<string, string>();
                Subscriptions subscriptions = new Subscriptions();

                GetParams = new Dictionary<string, string>
                {
                    { "limit", post_limit.ToString() },
                    { "order", "publish_date_asc" },
                    { "type", "all" },
                    { "format", "infinite"}
                };

                Dictionary<string, int> users = new Dictionary<string, int>();
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

                Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams, auth);

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
                    subscriptions = JsonConvert.DeserializeObject<Subscriptions>(body);
                    if (subscriptions != null && subscriptions.hasMore)
                    {
                        GetParams["offset"] = subscriptions.list.Count.ToString();
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
                            Subscriptions newSubscriptions = new Subscriptions();
                            Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
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
                                newSubscriptions = JsonConvert.DeserializeObject<Subscriptions>(loopbody, jsonSerializerSettings);
                            }
                            subscriptions.list.AddRange(newSubscriptions.list);
                            if (!newSubscriptions.hasMore)
                            {
                                break;
                            }
                            GetParams["offset"] = Convert.ToString(Convert.ToInt32(GetParams["offset"]) + post_limit);
                        }
                    }

                    if (includeExpiredSubscriptions)
                    {
                        foreach (Subscriptions.List subscription in subscriptions.list)
                        {
                            if (!users.ContainsKey(subscription.username))
                            {
                                users.Add(subscription.username, subscription.id);
                            }
                        }
                    }
                    else
                    {
                        foreach (Subscriptions.List subscription in subscriptions.list.Where(s => s.subscribedBy.HasValue))
                        {
                            if (!users.ContainsKey(subscription.username))
                            {
                                users.Add(subscription.username, subscription.id);
                            }
                        }
                    }
                }
                return users.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public async Task<Dictionary<string, int>> GetLists(string endpoint, Auth auth)
        {
            try
            {
                int offset = 0;
                bool loop = true;
                Dictionary<string, string> GetParams = new Dictionary<string, string>
                {
                    { "offset", offset.ToString() },
                    { "skip_users", "all" },
                    { "limit", "50" },
                    { "format", "infinite" }
                };
                Dictionary<string, int> lists = new Dictionary<string, int>();
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

                    Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams, auth);

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
                        UserList userList = JsonConvert.DeserializeObject<UserList>(body);
                        if (userList != null)
                        {
                            foreach (UserList.List l in userList.list)
                            {
                                if (IsStringOnlyDigits(l.id) && !lists.ContainsKey(l.name))
                                {
                                    lists.Add(l.name, Convert.ToInt32(l.id));
                                }
                            }
                            if (userList.hasMore.Value)
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
                return lists;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public async Task<List<string>> GetListUsers(string endpoint, Auth auth)
        {
            try
            {
                int offset = 0;
                bool loop = true;
                Dictionary<string, string> GetParams = new Dictionary<string, string>
                {
                    { "offset", offset.ToString() },
                    { "limit", "50" }
                };
                List<string> users = new List<string>();
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

                    Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams, auth);

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
                        List<UsersList> usersList = JsonConvert.DeserializeObject<List<UsersList>>(body);
                        if (usersList != null && usersList.Count > 0)
                        {
                            foreach (UsersList ul in usersList)
                            {
                                users.Add(ul.username);
                            }
                            if (users.Count >= 50)
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
                return users;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public async Task<Dictionary<long, string>> GetMedia(MediaType mediatype, string endpoint, string? username, string folder, Auth auth, Config config, List<long> paid_post_ids)
        {
            try
            {
                Dictionary<long, string> return_urls = new Dictionary<long, string>();
                int post_limit = 50;
                int limit = 5;
                int offset = 0;
                Purchased paidposts = new Purchased();
                bool isPaidPosts = false;
                Post posts = new Post();
                PostCollection postsCollection = new PostCollection();
                bool isPosts = false;
                Messages messages = new Messages();
                bool isMessages = false;
                Archived archived = new Archived();
                bool isArchived = false;
                List<Stories> stories = new List<Stories>();
                bool isStories = false;
                Highlights highlights = new Highlights();
                bool isHighlights = false;
                Purchased paidMessages = new Purchased();
                bool isPurchased = false;

                Dictionary<string, string> GetParams = null;

                switch (mediatype)
                {
                    case MediaType.PaidPosts:
                        isPaidPosts = true;
                        GetParams = new Dictionary<string, string>
                        {
                            { "limit", post_limit.ToString() },
                            { "order", "publish_date_desc" },
                            { "format", "infinite" },
                            { "user_id", username }
                        };
                        break;

                    case MediaType.Posts:
                        isPosts = true;
                        GetParams = new Dictionary<string, string>
                        {
                            { "limit", post_limit.ToString() },
                            { "order", "publish_date_desc" },
                            { "format", "infinite" }
                        };
                        break;

                    case MediaType.Archived:
                        isArchived = true;
                        GetParams = new Dictionary<string, string>
                        {
                            { "limit", post_limit.ToString() },
                            { "order", "publish_date_desc" },
                            { "format", "infinite" },
                            { "label", "archived" }
                        };
                        break;

                    case MediaType.Stories:
                        isStories = true;
                        GetParams = new Dictionary<string, string>
                        {
                            { "limit", post_limit.ToString() },
                            { "order", "publish_date_desc" }
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
                            { "order", "publish_date_desc" },
                            { "format", "infinite" },
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

                Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams, auth);

                HttpClient client = GetHttpClient(config);

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
                        
                        paidposts = JsonConvert.DeserializeObject<Purchased>(body, jsonSerializerSettings);
                        if (paidposts != null && paidposts.hasMore)
                        {
                            GetParams["offset"] = paidposts.list.Count.ToString();
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
                                Purchased newPaidPosts = new Purchased();
                                Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
                                HttpClient loopclient = GetHttpClient(config);

                                HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

                                foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
                                {
                                    looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                                }
                                using (var loopresponse = await loopclient.SendAsync(looprequest))
                                {
                                    loopresponse.EnsureSuccessStatusCode();
                                    var loopbody = await loopresponse.Content.ReadAsStringAsync();
                                    newPaidPosts = JsonConvert.DeserializeObject<Purchased>(loopbody, jsonSerializerSettings);
                                }
                                paidposts.list.AddRange(newPaidPosts.list);
                                if (!newPaidPosts.hasMore)
                                {
                                    break;
                                }
                                GetParams["offset"] = Convert.ToString(Convert.ToInt32(GetParams["offset"]) + post_limit);
                            }
                        }

                        DBHelper dBHelper = new DBHelper();
                        foreach (Purchased.List purchase in paidposts.list)
                        {
                            if (purchase.responseType == "post" && purchase.media != null && purchase.media.Count > 0)
                            {
                                List<long> previewids = new List<long>();
                                if (purchase.previews != null)
                                {
                                    for (int i = 0; i < purchase.previews.Count; i++)
                                    {
                                        if (!previewids.Contains((long)purchase.previews[i]))
                                        {
                                            previewids.Add((long)purchase.previews[i]);
                                        }
                                    }
                                }
                                await dBHelper.AddPost(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price.ToString() : "0", purchase.price != null && purchase.isOpened ? true : false, purchase.isArchived.HasValue ? purchase.isArchived.Value : false, purchase.createdAt != null ? purchase.createdAt.Value : purchase.postedAt.Value);
                                foreach (Purchased.Medium medium in purchase.media)
                                {
                                    paid_post_ids.Add(medium.id);
                                    if (medium.type == "photo" && !config.DownloadImages)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "video" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "gif" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "audio" && !config.DownloadAudios)
                                    {
                                        continue;
                                    }
                                    if (previewids.Count > 0)
                                    {
                                        bool has = previewids.Any(cus => cus.Equals(medium.id));
                                        if (!has && medium.canView && medium.source != null && medium.source.source != null && !medium.source.source.Contains("upload"))
                                        {
                                            await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                            if (!return_urls.ContainsKey(medium.id))
                                            {
                                                return_urls.Add(medium.id, medium.source.source);
                                            }
                                        }
                                        else if (!has && medium.canView && medium.files != null && medium.files.drm != null)
                                        {
                                            await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                            if (!return_urls.ContainsKey(medium.id))
                                            {
                                                return_urls.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                            }

                                        }
                                    }
                                    else
                                    {
                                        if (medium.canView && medium.source != null && medium.source.source != null && !medium.source.source.Contains("upload"))
                                        {
                                            await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                            if (!return_urls.ContainsKey(medium.id))
                                            {
                                                return_urls.Add(medium.id, medium.source.source);
                                            }
                                        }
                                        else if (medium.canView && medium.files != null && medium.files.drm != null)
                                        {
                                            await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                            if (!return_urls.ContainsKey(medium.id))
                                            {
                                                return_urls.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (isPosts)
                    {
                        
                        posts = JsonConvert.DeserializeObject<Post>(body, jsonSerializerSettings);
                        if (posts != null && posts.hasMore)
                        {
                            GetParams["beforePublishTime"] = posts.tailMarker;
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
                                Post newposts = new Post();
                                Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
                                HttpClient loopclient = GetHttpClient(config);

                                HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

                                foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
                                {
                                    looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                                }
                                using (var loopresponse = await loopclient.SendAsync(looprequest))
                                {
                                    loopresponse.EnsureSuccessStatusCode();
                                    var loopbody = await loopresponse.Content.ReadAsStringAsync();
                                    newposts = JsonConvert.DeserializeObject<Post>(loopbody, jsonSerializerSettings);
                                }
                                posts.list.AddRange(newposts.list);
                                if (!newposts.hasMore)
                                {
                                    break;
                                }
                                GetParams["beforePublishTime"] = newposts.tailMarker;
                            }
                        }

                        DBHelper dBHelper = new DBHelper();
                        foreach (Post.List post in posts.list.Where(p => config.SkipAds == false || (!p.rawText.Contains("#ad") && !p.rawText.Contains("/trial/"))))
                        {
                            List<long> postPreviewIds = new List<long>();
                            if (post.preview != null && post.preview.Count > 0)
                            {
                                foreach (var id in post.preview)
                                {
                                    if (id?.ToString() != "poll")
                                    {
                                        if (!postPreviewIds.Contains(Convert.ToInt64(id)))
                                        {
                                            postPreviewIds.Add(Convert.ToInt64(id));
                                        }
                                    }
                                }
                            }
                            await dBHelper.AddPost(folder, post.id, post.text != null ? post.text : string.Empty, post.price != null ? post.price.ToString() : "0", post.price != null && post.isOpened ? true : false, post.isArchived, post.postedAt);
                            if (post.media != null && post.media.Count > 0)
                            {
                                foreach (Post.Medium medium in post.media)
                                {
                                    if (medium.type == "photo" && !config.DownloadImages)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "video" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "gif" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "audio" && !config.DownloadAudios)
                                    {
                                        continue;
                                    }
                                    if (medium.canView && medium.files.drm == null)
                                    {
                                        bool has = paid_post_ids.Any(cus => cus.Equals(medium.id));
                                        if (!has && !medium.source.source.Contains("upload"))
                                        {
                                            if (!return_urls.ContainsKey(medium.id))
                                            {
                                                await dBHelper.AddMedia(folder, medium.id, post.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                                return_urls.Add(medium.id, medium.source.source);
                                            }
                                        }
                                    }
                                    else if (medium.canView && medium.files != null && medium.files.drm != null)
                                    {
                                        bool has = paid_post_ids.Any(cus => cus.Equals(medium.id));
                                        if (!has && medium.files != null && medium.files.drm != null)
                                        {
                                            if (!return_urls.ContainsKey(medium.id))
                                            {
                                                await dBHelper.AddMedia(folder, medium.id, post.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                                return_urls.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{post.id}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (isArchived)
                    {
                        
                        archived = JsonConvert.DeserializeObject<Archived>(body, jsonSerializerSettings);
                        if (archived != null && archived.hasMore)
                        {
                            GetParams["beforePublishTime"] = archived.tailMarker;
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
                                Archived newarchived = new Archived();
                                Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
                                HttpClient loopclient = GetHttpClient(config);

                                HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

                                foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
                                {
                                    looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                                }
                                using (var loopresponse = await loopclient.SendAsync(looprequest))
                                {
                                    loopresponse.EnsureSuccessStatusCode();
                                    var loopbody = await loopresponse.Content.ReadAsStringAsync();
                                    newarchived = JsonConvert.DeserializeObject<Archived>(loopbody, jsonSerializerSettings);
                                }
                                archived.list.AddRange(newarchived.list);
                                if (!newarchived.hasMore)
                                {
                                    break;
                                }
                                GetParams["beforePublishTime"] = newarchived.tailMarker;
                            }
                        }

                        DBHelper dBHelper = new DBHelper();
                        foreach (Archived.List archive in archived.list)
                        {
                            List<long> previewids = new List<long>();
                            if (archive.preview != null)
                            {
                                for (int i = 0; i < archive.preview.Count; i++)
                                {
                                    if (archive.preview[i]?.ToString() != "poll")
                                    {
                                        if (!previewids.Contains((long)archive.preview[i]))
                                        {
                                            previewids.Add((long)archive.preview[i]);
                                        }
                                    }
                                }
                            }
                            await dBHelper.AddPost(folder, archive.id, archive.text != null ? archive.text : string.Empty, archive.price != null ? archive.price.ToString() : "0", archive.price != null && archive.isOpened ? true : false, archive.isArchived, archive.postedAt);
                            if (archive.media != null && archive.media.Count > 0)
                            {
                                foreach (Archived.Medium medium in archive.media)
                                {
                                    if (medium.type == "photo" && !config.DownloadImages)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "video" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "gif" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "audio" && !config.DownloadAudios)
                                    {
                                        continue;
                                    }
                                    if (medium.canView && !medium.source.source.Contains("upload"))
                                    {
                                        if (!return_urls.ContainsKey(medium.id))
                                        {
                                            await dBHelper.AddMedia(folder, medium.id, archive.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                            return_urls.Add(medium.id, medium.source.source);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (isStories)
                    {
                        stories = JsonConvert.DeserializeObject<List<Stories>>(body, jsonSerializerSettings);
                        stories = stories.OrderByDescending(x => x.createdAt).ToList();
                        
                        DBHelper dBHelper = new DBHelper();
                        foreach (Stories story in stories)
                        {
                            if (story.createdAt != null)
                            {
                                await dBHelper.AddStory(folder, story.id, string.Empty, "0", false, false, story.createdAt);
                            }
                            else
                            {
                                await dBHelper.AddStory(folder, story.id, string.Empty, "0", false, false, story.media[0].createdAt);
                            }
                            if (story.media != null && story.media.Count > 0)
                            {
                                foreach (Stories.Medium medium in story.media)
                                {
                                    await dBHelper.AddMedia(folder, medium.id, story.id, medium.files.source.url, null, null, null, "Stories", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), false, false, null);
                                    if (medium.type == "photo" && !config.DownloadImages)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "video" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "gif" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "audio" && !config.DownloadAudios)
                                    {
                                        continue;
                                    }
                                    if (medium.canView && !medium.files.source.url.Contains("upload"))
                                    {
                                        if (!return_urls.ContainsKey(medium.id))
                                        {
                                            return_urls.Add(medium.id, medium.files.source.url);
                                        }
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
                                Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
                                HttpClient loopclient = GetHttpClient(config);

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
                            if (!highlight_ids.Contains(list.id.ToString()))
                            {
                                highlight_ids.Add(list.id.ToString());
                            }
                        }
                        DBHelper dBHelper = new DBHelper();
                        foreach (string highlight_id in highlight_ids)
                        {
                            HighlightMedia highlightMedia = new HighlightMedia();
                            Dictionary<string, string> highlight_headers = await Headers("/api2/v2/stories/highlights/" + highlight_id, string.Empty, auth);

                            HttpClient highlight_client = GetHttpClient(config);

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
                                    foreach (HighlightMedia.Story item in highlightMedia.stories)
                                    {
                                        await dBHelper.AddStory(folder, item.id, string.Empty, "0", false, false, item.createdAt);
                                        if (item.media.Count > 0 && !item.media[0].files.source.url.Contains("upload"))
                                        {
                                            foreach (HighlightMedia.Medium medium in item.media)
                                            {
                                                await dBHelper.AddMedia(folder, medium.id, item.id, item.media[0].files.source.url, null, null, null, "Stories", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), false, false, null);
                                                if (medium.type == "photo" && !config.DownloadImages)
                                                {
                                                    continue;
                                                }
                                                if (medium.type == "video" && !config.DownloadVideos)
                                                {
                                                    continue;
                                                }
                                                if (medium.type == "gif" && !config.DownloadVideos)
                                                {
                                                    continue;
                                                }
                                                if (medium.type == "audio" && !config.DownloadAudios)
                                                {
                                                    continue;
                                                }
                                                if (!return_urls.ContainsKey(medium.id))
                                                {
                                                    return_urls.Add(medium.id, item.media[0].files.source.url);
                                                }
                                            }
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
                                Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
                                HttpClient loopclient = GetHttpClient(config);

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

                        DBHelper dBHelper = new DBHelper();
                        foreach (Messages.List list in messages.list.Where(p => config.SkipAds == false || (!p.text.Contains("#ad") && !p.text.Contains("/trial/"))))
                        {
                            List<long> messagePreviewIds = new List<long>();
                            if (list.previews != null && list.previews.Count > 0)
                            {
                                foreach (var id in list.previews)
                                {
                                    if (!messagePreviewIds.Contains((long)id))
                                    {
                                        messagePreviewIds.Add((long)id);
                                    }
                                }
                            }
                            await dBHelper.AddMessage(folder, list.id, list.text != null ? list.text : string.Empty, list.price != null ? list.price.ToString() : "0", list.canPurchaseReason == "opened" ? true : list.canPurchaseReason != "opened" ? false : (bool?)null ?? false, false, list.createdAt.HasValue ? list.createdAt.Value : DateTime.Now, list.fromUser != null && list.fromUser.id != null ? list.fromUser.id.Value : int.MinValue);
                            if (list.canPurchaseReason != "opened" && list.media != null && list.media.Count > 0)
                            {
                                foreach (Messages.Medium medium in list.media)
                                {
                                    if (medium.canView && medium.source.source != null && !medium.source.source.Contains("upload"))
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, list.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);

                                        if (medium.type == "photo" && !config.DownloadImages)
                                        {
                                            continue;
                                        }
                                        if (medium.type == "video" && !config.DownloadVideos)
                                        {
                                            continue;
                                        }
                                        if (medium.type == "gif" && !config.DownloadVideos)
                                        {
                                            continue;
                                        }
                                        if (medium.type == "audio" && !config.DownloadAudios)
                                        {
                                            continue;
                                        }
                                        if (!return_urls.ContainsKey(medium.id))
                                        {
                                            return_urls.Add(medium.id, medium.source.source.ToString());
                                        }
                                    }
                                    else if (medium.canView && medium.files != null && medium.files.drm != null)
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, list.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
                                        if (medium.type == "photo" && !config.DownloadImages)
                                        {
                                            continue;
                                        }
                                        if (medium.type == "video" && !config.DownloadVideos)
                                        {
                                            continue;
                                        }
                                        if (medium.type == "gif" && !config.DownloadVideos)
                                        {
                                            continue;
                                        }
                                        if (medium.type == "audio" && !config.DownloadAudios)
                                        {
                                            continue;
                                        }
                                        if (!return_urls.ContainsKey(medium.id))
                                        {
                                            return_urls.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{list.id}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (isPurchased)
                    {
                        paidMessages = JsonConvert.DeserializeObject<Purchased>(body, jsonSerializerSettings);
                        
                        if (paidMessages != null && paidMessages.hasMore)
                        {
                            GetParams["offset"] = paidMessages.list.Count.ToString();
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
                                Purchased newpaidMessages = new Purchased();
                                Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
                                HttpClient loopclient = GetHttpClient(config);

                                HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

                                foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
                                {
                                    looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                                }
                                using (var loopresponse = await loopclient.SendAsync(looprequest))
                                {
                                    loopresponse.EnsureSuccessStatusCode();
                                    var loopbody = await loopresponse.Content.ReadAsStringAsync();
                                    newpaidMessages = JsonConvert.DeserializeObject<Purchased>(loopbody, jsonSerializerSettings);
                                }
                                paidMessages.list.AddRange(newpaidMessages.list);
                                if (!newpaidMessages.hasMore)
                                {
                                    break;
                                }
                                GetParams["offset"] = Convert.ToString(Convert.ToInt32(GetParams["offset"]) + post_limit);
                            }
                        }

                        DBHelper dBHelper = new DBHelper();
                        foreach (Purchased.List purchase in paidMessages.list.Where(p => p.responseType == "message").OrderByDescending(p => p.postedAt ?? p.createdAt))
                        {
                            if (purchase.postedAt != null)
                            {
                                await dBHelper.AddMessage(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price : "0", true, false, purchase.postedAt.Value, purchase.fromUser.id);
                            }
                            else
                            {
                                await dBHelper.AddMessage(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price : "0", true, false, purchase.createdAt.Value, purchase.fromUser.id);
                            }

                            if (purchase.media != null && purchase.media.Count > 0)
                            {
                                List<long> previewids = new List<long>();
                                if (purchase.previews != null)
                                {
                                    for (int i = 0; i < purchase.previews.Count; i++)
                                    {
                                        if (!previewids.Contains((long)purchase.previews[i]))
                                        {
                                            previewids.Add((long)purchase.previews[i]);
                                        }
                                    }
                                }
                                else if (purchase.preview != null)
                                {
                                    for (int i = 0; i < purchase.preview.Count; i++)
                                    {
                                        if (!previewids.Contains((long)purchase.preview[i]))
                                        {
                                            previewids.Add((long)purchase.preview[i]);
                                        }
                                    }
                                }

                                foreach (Purchased.Medium medium in purchase.media)
                                {
                                    if (previewids.Count > 0)
                                    {
                                        bool has = previewids.Any(cus => cus.Equals(medium.id));
                                        if (!has && medium.canView && medium.source != null && medium.source.source != null && !medium.source.source.Contains("upload"))
                                        {
                                            await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                            if (medium.type == "photo" && !config.DownloadImages)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "video" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "gif" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "audio" && !config.DownloadAudios)
                                            {
                                                continue;
                                            }
                                            if (!return_urls.ContainsKey(medium.id))
                                            {
                                                return_urls.Add(medium.id, medium.source.source);
                                            }
                                        }
                                        else if (!has && medium.canView && medium.files != null && medium.files.drm != null)
                                        {
                                            await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                            if (medium.type == "photo" && !config.DownloadImages)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "video" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "gif" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "audio" && !config.DownloadAudios)
                                            {
                                                continue;
                                            }
                                            if (!return_urls.ContainsKey(medium.id))
                                            {
                                                return_urls.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (medium.canView && medium.source != null && medium.source.source != null && !medium.source.source.Contains("upload"))
                                        {
                                            await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                            if (medium.type == "photo" && !config.DownloadImages)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "video" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "gif" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "audio" && !config.DownloadAudios)
                                            {
                                                continue;
                                            }
                                            if (!return_urls.ContainsKey(medium.id))
                                            {
                                                return_urls.Add(medium.id, medium.source.source);
                                            }
                                        }
                                        else if (medium.canView && medium.files != null && medium.files.drm != null)
                                        {
                                            await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                            if (medium.type == "photo" && !config.DownloadImages)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "video" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "gif" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "audio" && !config.DownloadAudios)
                                            {
                                                continue;
                                            }
                                            if (!return_urls.ContainsKey(medium.id))
                                            {
                                                return_urls.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                            }
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
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }

        public async Task<PaidPostCollection> GetPaidPosts(string endpoint, string folder, string username, Auth auth, Config config, List<long> paid_post_ids)
        {
            try
            {
                Purchased paidPosts = new Purchased();
                PaidPostCollection paidPostCollection = new PaidPostCollection();
                int post_limit = 50;
                Dictionary<string, string> GetParams = new Dictionary<string, string>
                {
                    { "limit", post_limit.ToString() },
                    { "order", "publish_date_desc" },
                    { "format", "infinite" },
                    { "user_id", username }
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

                Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams, auth);

                HttpClient client = GetHttpClient(config);

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
                    paidPosts = JsonConvert.DeserializeObject<Purchased>(body, jsonSerializerSettings);
                    if (paidPosts != null && paidPosts.hasMore)
                    {
                        GetParams["offset"] = paidPosts.list.Count.ToString();
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
                            Purchased newPaidPosts = new Purchased();
                            Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
                            HttpClient loopclient = GetHttpClient(config);

                            HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

                            foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
                            {
                                looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                            }
                            using (var loopresponse = await loopclient.SendAsync(looprequest))
                            {
                                loopresponse.EnsureSuccessStatusCode();
                                var loopbody = await loopresponse.Content.ReadAsStringAsync();
                                newPaidPosts = JsonConvert.DeserializeObject<Purchased>(loopbody, jsonSerializerSettings);
                            }
                            paidPosts.list.AddRange(newPaidPosts.list);
                            if (!newPaidPosts.hasMore)
                            {
                                break;
                            }
                            GetParams["offset"] = Convert.ToString(Convert.ToInt32(GetParams["offset"]) + post_limit);
                        }
                    }
                }

                DBHelper dBHelper = new DBHelper();
                foreach (Purchased.List purchase in paidPosts.list)
                {
                    if (purchase.responseType == "post" && purchase.media != null && purchase.media.Count > 0)
                    {
                        List<long> previewids = new List<long>();
                        if (purchase.previews != null)
                        {
                            for (int i = 0; i < purchase.previews.Count; i++)
                            {
                                if (!previewids.Contains((long)purchase.previews[i]))
                                {
                                    previewids.Add((long)purchase.previews[i]);
                                }
                            }
                        }
                        else if(purchase.preview != null)
                        {
                            for (int i = 0; i < purchase.preview.Count; i++)
                            {
                                if (!previewids.Contains((long)purchase.preview[i]))
                                {
                                    previewids.Add((long)purchase.preview[i]);
                                }
                            }
                        }
                        await dBHelper.AddPost(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price.ToString() : "0", purchase.price != null && purchase.isOpened ? true : false, purchase.isArchived.HasValue ? purchase.isArchived.Value : false, purchase.createdAt != null ? purchase.createdAt.Value : purchase.postedAt.Value);
                        paidPostCollection.PaidPostObjects.Add(purchase);
                        foreach (Purchased.Medium medium in purchase.media)
                        {
                            if (!previewids.Contains(medium.id))
                            {
                                paid_post_ids.Add(medium.id);
                            }
                            
                            if (medium.type == "photo" && !config.DownloadImages)
                            {
                                continue;
                            }
                            if (medium.type == "video" && !config.DownloadVideos)
                            {
                                continue;
                            }
                            if (medium.type == "gif" && !config.DownloadVideos)
                            {
                                continue;
                            }
                            if (medium.type == "audio" && !config.DownloadAudios)
                            {
                                continue;
                            }
                            if (previewids.Count > 0)
                            {
                                bool has = previewids.Any(cus => cus.Equals(medium.id));
                                if (!has && medium.canView && medium.source != null && medium.source.source != null && !medium.source.source.Contains("upload"))
                                {
                                    
                                    if (!paidPostCollection.PaidPosts.ContainsKey(medium.id))
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                        paidPostCollection.PaidPosts.Add(medium.id, medium.source.source);
                                        paidPostCollection.PaidPostMedia.Add(medium);
                                    }
                                }
                                else if (!has && medium.canView && medium.files != null && medium.files.drm != null)
                                {
                                    
                                    if (!paidPostCollection.PaidPosts.ContainsKey(medium.id))
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                        paidPostCollection.PaidPosts.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                        paidPostCollection.PaidPostMedia.Add(medium);
                                    }

                                }
                            }
                            else
                            {
                                if (medium.canView && medium.source != null && medium.source.source != null && !medium.source.source.Contains("upload"))
                                {
                                    if (!paidPostCollection.PaidPosts.ContainsKey(medium.id))
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                        paidPostCollection.PaidPosts.Add(medium.id, medium.source.source);
                                        paidPostCollection.PaidPostMedia.Add(medium);
                                    }
                                }
                                else if (medium.canView && medium.files != null && medium.files.drm != null)
                                {
                                    if (!paidPostCollection.PaidPosts.ContainsKey(medium.id))
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                        paidPostCollection.PaidPosts.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                        paidPostCollection.PaidPostMedia.Add(medium);
                                    }
                                }
                            }
                        }
                    }
                }
                return paidPostCollection;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public async Task<PostCollection> GetPosts(string endpoint, string folder, Auth auth, Config config, List<long> paid_post_ids)
        {
            try
            {
                Post posts = new Post();
                PostCollection postCollection = new PostCollection();
                int post_limit = 50;
                Dictionary<string, string> GetParams = new Dictionary<string, string>
                {
                    { "limit", post_limit.ToString() },
                    { "order", "publish_date_desc" },
                    { "format", "infinite" }
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

                Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams, auth);

                HttpClient client = GetHttpClient(config);

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
                    posts = JsonConvert.DeserializeObject<Post>(body, jsonSerializerSettings);
                    if (posts != null && posts.hasMore)
                    {
                        GetParams["beforePublishTime"] = posts.tailMarker;
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
                            Post newposts = new Post();
                            Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
                            HttpClient loopclient = GetHttpClient(config);

                            HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

                            foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
                            {
                                looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                            }
                            using (var loopresponse = await loopclient.SendAsync(looprequest))
                            {
                                loopresponse.EnsureSuccessStatusCode();
                                var loopbody = await loopresponse.Content.ReadAsStringAsync();
                                newposts = JsonConvert.DeserializeObject<Post>(loopbody, jsonSerializerSettings);
                            }
                            posts.list.AddRange(newposts.list);
                            if (!newposts.hasMore)
                            {
                                break;
                            }
                            GetParams["beforePublishTime"] = newposts.tailMarker;
                        }
                    }

                    DBHelper dBHelper = new DBHelper();
                    foreach (Post.List post in posts.list.Where(p => !config.SkipAds || (!p.rawText.Contains("#ad") && !p.rawText.Contains("/trial/"))))
                    {
                        List<long> postPreviewIds = new List<long>();
                        if (post.preview != null && post.preview.Count > 0)
                        {
                            foreach (var id in post.preview)
                            {
                                if (id?.ToString() != "poll")
                                {
                                    if (!postPreviewIds.Contains(Convert.ToInt64(id)))
                                    {
                                        postPreviewIds.Add(Convert.ToInt64(id));
                                    }
                                }
                            }
                        }
                        await dBHelper.AddPost(folder, post.id, post.text != null ? post.text : string.Empty, post.price != null ? post.price.ToString() : "0", post.price != null && post.isOpened ? true : false, post.isArchived, post.postedAt);
                        postCollection.PostObjects.Add(post);
                        if (post.media != null && post.media.Count > 0)
                        {
                            foreach (Post.Medium medium in post.media)
                            {
                                if (medium.type == "photo" && !config.DownloadImages)
                                {
                                    continue;
                                }
                                if (medium.type == "video" && !config.DownloadVideos)
                                {
                                    continue;
                                }
                                if (medium.type == "gif" && !config.DownloadVideos)
                                {
                                    continue;
                                }
                                if (medium.type == "audio" && !config.DownloadAudios)
                                {
                                    continue;
                                }
                                if (medium.canView && medium.files?.drm == null)
                                {
                                    bool has = paid_post_ids.Any(cus => cus.Equals(medium.id));
                                    if (!has && !medium.source.source.Contains("upload"))
                                    {
                                        if (!postCollection.Posts.ContainsKey(medium.id))
                                        {
                                            await dBHelper.AddMedia(folder, medium.id, post.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                            postCollection.Posts.Add(medium.id, medium.source.source);
                                            postCollection.PostMedia.Add(medium);
                                        }
                                    }
                                }
                                else if (medium.canView && medium.files != null && medium.files.drm != null)
                                {
                                    bool has = paid_post_ids.Any(cus => cus.Equals(medium.id));
                                    if (!has && medium.files != null && medium.files.drm != null)
                                    {
                                        if (!postCollection.Posts.ContainsKey(medium.id))
                                        {
                                            await dBHelper.AddMedia(folder, medium.id, post.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                            postCollection.Posts.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{post.id}");
                                            postCollection.PostMedia.Add(medium);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                return postCollection;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public async Task<ArchivedCollection> GetArchived(string endpoint, string folder, Auth auth, Config config)
        {
            try
            {
                Archived archived = new Archived();
                ArchivedCollection archivedCollection = new ArchivedCollection();
                int post_limit = 50;
                Dictionary<string, string> GetParams = new Dictionary<string, string>
                {
                    { "limit", post_limit.ToString() },
                    { "order", "publish_date_desc" },
                    { "format", "infinite" },
                    { "label", "archived" }
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

                Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams, auth);

                HttpClient client = GetHttpClient(config);

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
                    archived = JsonConvert.DeserializeObject<Archived>(body, jsonSerializerSettings);
                    if (archived != null && archived.hasMore)
                    {
                        GetParams["beforePublishTime"] = archived.tailMarker;
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
                            Archived newarchived = new Archived();
                            Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
                            HttpClient loopclient = GetHttpClient(config);

                            HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

                            foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
                            {
                                looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                            }
                            using (var loopresponse = await loopclient.SendAsync(looprequest))
                            {
                                loopresponse.EnsureSuccessStatusCode();
                                var loopbody = await loopresponse.Content.ReadAsStringAsync();
                                newarchived = JsonConvert.DeserializeObject<Archived>(loopbody, jsonSerializerSettings);
                            }
                            archived.list.AddRange(newarchived.list);
                            if (!newarchived.hasMore)
                            {
                                break;
                            }
                            GetParams["beforePublishTime"] = newarchived.tailMarker;
                        }
                    }

                    DBHelper dBHelper = new DBHelper();
                    foreach (Archived.List archive in archived.list)
                    {
                        List<long> previewids = new List<long>();
                        if (archive.preview != null)
                        {
                            for (int i = 0; i < archive.preview.Count; i++)
                            {
                                if (archive.preview[i]?.ToString() != "poll")
                                {
                                    if (!previewids.Contains((long)archive.preview[i]))
                                    {
                                        previewids.Add((long)archive.preview[i]);
                                    }
                                }
                            }
                        }
                        await dBHelper.AddPost(folder, archive.id, archive.text != null ? archive.text : string.Empty, archive.price != null ? archive.price.ToString() : "0", archive.price != null && archive.isOpened ? true : false, archive.isArchived, archive.postedAt);
                        archivedCollection.ArchivedPostObjects.Add(archive);
                        if (archive.media != null && archive.media.Count > 0)
                        {
                            foreach (Archived.Medium medium in archive.media)
                            {
                                if (medium.type == "photo" && !config.DownloadImages)
                                {
                                    continue;
                                }
                                if (medium.type == "video" && !config.DownloadVideos)
                                {
                                    continue;
                                }
                                if (medium.type == "gif" && !config.DownloadVideos)
                                {
                                    continue;
                                }
                                if (medium.type == "audio" && !config.DownloadAudios)
                                {
                                    continue;
                                }
                                if (medium.canView && medium.files?.drm == null && !medium.source.source.Contains("upload"))
                                {
                                    if (!archivedCollection.ArchivedPosts.ContainsKey(medium.id))
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, archive.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                        archivedCollection.ArchivedPosts.Add(medium.id, medium.source.source);
                                        archivedCollection.ArchivedPostMedia.Add(medium);
                                    }
                                }
                                else if(medium.canView && medium.files != null && medium.files.drm != null)
                                {
                                    if (!archivedCollection.ArchivedPosts.ContainsKey(medium.id))
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, archive.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                        archivedCollection.ArchivedPosts.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{archive.id}");
                                        archivedCollection.ArchivedPostMedia.Add(medium);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public async Task<MessageCollection> GetMessages(string endpoint, string folder, Auth auth, Config config)
        {
            try
            {
                Messages messages = new Messages();
                MessageCollection messageCollection = new MessageCollection();
                int post_limit = 50;
                Dictionary<string, string> GetParams = new Dictionary<string, string>
                {
                    { "limit", post_limit.ToString() },
                    { "order", "desc" }
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

                Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams, auth);

                HttpClient client = GetHttpClient(config);

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
                            Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
                            HttpClient loopclient = GetHttpClient(config);

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

                    DBHelper dBHelper = new DBHelper();
                    foreach (Messages.List list in messages.list.Where(m => !config.SkipAds || (!m.text.Contains("#ad") && !m.text.Contains("/trial/"))))
                    {
                        List<long> messagePreviewIds = new List<long>();
                        if (list.previews != null && list.previews.Count > 0)
                        {
                            foreach (var id in list.previews)
                            {
                                if (!messagePreviewIds.Contains((long)id))
                                {
                                    messagePreviewIds.Add((long)id);
                                }
                            }
                        }
                        await dBHelper.AddMessage(folder, list.id, list.text != null ? list.text : string.Empty, list.price != null ? list.price.ToString() : "0", list.canPurchaseReason == "opened" ? true : list.canPurchaseReason != "opened" ? false : (bool?)null ?? false, false, list.createdAt.HasValue ? list.createdAt.Value : DateTime.Now, list.fromUser != null && list.fromUser.id != null ? list.fromUser.id.Value : int.MinValue);
                        messageCollection.MessageObjects.Add(list);
                        if (list.canPurchaseReason != "opened" && list.media != null && list.media.Count > 0)
                        {
                            foreach (Messages.Medium medium in list.media)
                            {
                                if (medium.canView && medium.source.source != null && !medium.source.source.Contains("upload"))
                                {
                                    if (medium.type == "photo" && !config.DownloadImages)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "video" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "gif" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "audio" && !config.DownloadAudios)
                                    {
                                        continue;
                                    }
                                    if (!messageCollection.Messages.ContainsKey(medium.id))
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, list.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
                                        messageCollection.Messages.Add(medium.id, medium.source.source.ToString());
                                        messageCollection.MessageMedia.Add(medium);
                                    }
                                }
                                else if (medium.canView && medium.files != null && medium.files.drm != null)
                                {
                                    if (medium.type == "photo" && !config.DownloadImages)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "video" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "gif" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "audio" && !config.DownloadAudios)
                                    {
                                        continue;
                                    }
                                    if (!messageCollection.Messages.ContainsKey(medium.id))
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, list.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
                                        messageCollection.Messages.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{list.id}");
                                        messageCollection.MessageMedia.Add(medium);
                                    }
                                }
                            }
                        }
                        else if(messagePreviewIds.Count > 0)
                        {
                            foreach (Messages.Medium medium in list.media)
                            {
                                if (medium.canView && medium.source.source != null && !medium.source.source.Contains("upload") && messagePreviewIds.Contains(medium.id))
                                {
                                    if (medium.type == "photo" && !config.DownloadImages)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "video" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "gif" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "audio" && !config.DownloadAudios)
                                    {
                                        continue;
                                    }
                                    if (!messageCollection.Messages.ContainsKey(medium.id))
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, list.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
                                        messageCollection.Messages.Add(medium.id, medium.source.source.ToString());
                                        messageCollection.MessageMedia.Add(medium);
                                    }
                                }
                                else if (medium.canView && medium.files != null && medium.files.drm != null && messagePreviewIds.Contains(medium.id))
                                {
                                    if (medium.type == "photo" && !config.DownloadImages)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "video" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "gif" && !config.DownloadVideos)
                                    {
                                        continue;
                                    }
                                    if (medium.type == "audio" && !config.DownloadAudios)
                                    {
                                        continue;
                                    }
                                    if (!messageCollection.Messages.ContainsKey(medium.id))
                                    {
                                        await dBHelper.AddMedia(folder, medium.id, list.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
                                        messageCollection.Messages.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{list.id}");
                                        messageCollection.MessageMedia.Add(medium);
                                    }
                                }
                            }
                        }
                    }
                }
                return messageCollection;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public async Task<PaidMessageCollection> GetPaidMessages(string endpoint, string folder, string username, Auth auth, Config config)
        {
            try
            {
                Purchased paidMessages = new Purchased();
                PaidMessageCollection paidMessageCollection = new PaidMessageCollection();
                int post_limit = 50;
                Dictionary<string, string> GetParams = new Dictionary<string, string>
                {
                    { "limit", post_limit.ToString() },
                    { "order", "publish_date_desc" },
                    { "format", "infinite" },
                    { "user_id", username }
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

                Dictionary<string, string> headers = await Headers("/api2/v2" + endpoint, queryParams, auth);

                HttpClient client = GetHttpClient(config);

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
                    paidMessages = JsonConvert.DeserializeObject<Purchased>(body, jsonSerializerSettings);
                    if (paidMessages != null && paidMessages.hasMore)
                    {
                        GetParams["offset"] = paidMessages.list.Count.ToString();
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
                            Purchased newpaidMessages = new Purchased();
                            Dictionary<string, string> loopheaders = await Headers("/api2/v2" + endpoint, loopqueryParams, auth);
                            HttpClient loopclient = GetHttpClient(config);

                            HttpRequestMessage looprequest = new HttpRequestMessage(HttpMethod.Get, "https://onlyfans.com/api2/v2" + endpoint + loopqueryParams);

                            foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
                            {
                                looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                            }
                            using (var loopresponse = await loopclient.SendAsync(looprequest))
                            {
                                loopresponse.EnsureSuccessStatusCode();
                                var loopbody = await loopresponse.Content.ReadAsStringAsync();
                                newpaidMessages = JsonConvert.DeserializeObject<Purchased>(loopbody, jsonSerializerSettings);
                            }
                            paidMessages.list.AddRange(newpaidMessages.list);
                            if (!newpaidMessages.hasMore)
                            {
                                break;
                            }
                            GetParams["offset"] = Convert.ToString(Convert.ToInt32(GetParams["offset"]) + post_limit);
                        }
                    }

                    DBHelper dBHelper = new DBHelper();
                    if(paidMessages.list != null && paidMessages.list.Count > 0)
                    {
                        foreach (Purchased.List purchase in paidMessages.list.Where(p => p.responseType == "message").OrderByDescending(p => p.postedAt ?? p.createdAt))
                        {
                            if (purchase.postedAt != null)
                            {
                                await dBHelper.AddMessage(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price : "0", true, false, purchase.postedAt.Value, purchase.fromUser.id);
                            }
                            else
                            {
                                await dBHelper.AddMessage(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price : "0", true, false, purchase.createdAt.Value, purchase.fromUser.id);
                            }
                            paidMessageCollection.PaidMessageObjects.Add(purchase);
                            if (purchase.media != null && purchase.media.Count > 0)
                            {
                                List<long> previewids = new List<long>();
                                if (purchase.previews != null)
                                {
                                    for (int i = 0; i < purchase.previews.Count; i++)
                                    {
                                        if (!previewids.Contains((long)purchase.previews[i]))
                                        {
                                            previewids.Add((long)purchase.previews[i]);
                                        }
                                    }
                                }
                                else if (purchase.preview != null)
                                {
                                    for (int i = 0; i < purchase.preview.Count; i++)
                                    {
                                        if (!previewids.Contains((long)purchase.preview[i]))
                                        {
                                            previewids.Add((long)purchase.preview[i]);
                                        }
                                    }
                                }

                                foreach (Purchased.Medium medium in purchase.media)
                                {
                                    if (previewids.Count > 0)
                                    {
                                        bool has = previewids.Any(cus => cus.Equals(medium.id));
                                        if (!has && medium.canView && medium.source != null && medium.source.source != null && !medium.source.source.Contains("upload"))
                                        {
                                            if (medium.type == "photo" && !config.DownloadImages)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "video" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "gif" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "audio" && !config.DownloadAudios)
                                            {
                                                continue;
                                            }
                                            if (!paidMessageCollection.PaidMessages.ContainsKey(medium.id))
                                            {
                                                await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                                paidMessageCollection.PaidMessages.Add(medium.id, medium.source.source);
                                                paidMessageCollection.PaidMessageMedia.Add(medium);
                                            }
                                        }
                                        else if (!has && medium.canView && medium.files != null && medium.files.drm != null)
                                        {
                                            if (medium.type == "photo" && !config.DownloadImages)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "video" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "gif" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "audio" && !config.DownloadAudios)
                                            {
                                                continue;
                                            }
                                            if (!paidMessageCollection.PaidMessages.ContainsKey(medium.id))
                                            {
                                                await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                                paidMessageCollection.PaidMessages.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                                paidMessageCollection.PaidMessageMedia.Add(medium);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (medium.canView && medium.source != null && medium.source.source != null && !medium.source.source.Contains("upload"))
                                        {
                                            if (medium.type == "photo" && !config.DownloadImages)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "video" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "gif" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "audio" && !config.DownloadAudios)
                                            {
                                                continue;
                                            }
                                            if (!paidMessageCollection.PaidMessages.ContainsKey(medium.id))
                                            {
                                                await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                                paidMessageCollection.PaidMessages.Add(medium.id, medium.source.source);
                                                paidMessageCollection.PaidMessageMedia.Add(medium);
                                            }
                                        }
                                        else if (medium.canView && medium.files != null && medium.files.drm != null)
                                        {
                                            if (medium.type == "photo" && !config.DownloadImages)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "video" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "gif" && !config.DownloadVideos)
                                            {
                                                continue;
                                            }
                                            if (medium.type == "audio" && !config.DownloadAudios)
                                            {
                                                continue;
                                            }
                                            if (!paidMessageCollection.PaidMessages.ContainsKey(medium.id))
                                            {
                                                await dBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                                paidMessageCollection.PaidMessages.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                                paidMessageCollection.PaidMessageMedia.Add(medium);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                return paidMessageCollection;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public async Task<string> GetDRMMPDPSSH(string mpdUrl, string policy, string signature, string kvp, Auth auth)
        {
            try
            {
                string pssh = null;
                
                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, mpdUrl);
                request.Headers.Add("user-agent", auth.USER_AGENT);
                request.Headers.Add("Accept", "*/*");
                request.Headers.Add("Cookie", $"CloudFront-Policy={policy}; CloudFront-Signature={signature}; CloudFront-Key-Pair-Id={kvp}; {auth.COOKIE};");
                using (var response = await client.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                    var body = await response.Content.ReadAsStringAsync();
                    XNamespace ns = "urn:mpeg:dash:schema:mpd:2011";
                    XNamespace cenc = "urn:mpeg:cenc:2013";
                    XDocument xmlDoc = XDocument.Parse(body);
                    var psshElements = xmlDoc.Descendants(cenc + "pssh");
                    pssh = psshElements.ElementAt(1).Value;
                }

                return pssh;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public async Task<DateTime> GetDRMMPDLastModified(string mpdUrl, string policy, string signature, string kvp, Auth auth)
        {
            try
            {
                DateTime lastmodified;
                
                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, mpdUrl);
                request.Headers.Add("user-agent", auth.USER_AGENT);
                request.Headers.Add("Accept", "*/*");
                request.Headers.Add("Cookie", $"CloudFront-Policy={policy}; CloudFront-Signature={signature}; CloudFront-Key-Pair-Id={kvp}; {auth.COOKIE};");
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    lastmodified = response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now;
                }
                return lastmodified;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return DateTime.Now;
        }
        public async Task<string> GetDecryptionKey(Dictionary<string, string> drmHeaders, string licenceURL, string pssh, Auth auth)
        {
            try
            {
                string dcValue = string.Empty;
                string buildInfo = "";
                string proxy = "";
                bool cache = true;

                StringBuilder sb = new StringBuilder();
                sb.Append("{\n");
                sb.AppendFormat("  \"license\": \"{0}\",\n", licenceURL);
                sb.Append("  \"headers\": \"");
                foreach (KeyValuePair<string, string> header in drmHeaders)
                {
                    if (header.Key == "time" || header.Key == "user-id")
                    {
                        sb.AppendFormat("{0}: '{1}'\\n", header.Key, header.Value);
                    }
                    else
                    {
                        sb.AppendFormat("{0}: {1}\\n", header.Key, header.Value);
                    }
                }
                sb.Remove(sb.Length - 2, 2); // remove the last \\n
                sb.Append("\",\n");
                sb.AppendFormat("  \"pssh\": \"{0}\",\n", pssh);
                sb.AppendFormat("  \"buildInfo\": \"{0}\",\n", buildInfo);
                sb.AppendFormat("  \"proxy\": \"{0}\",\n", proxy);
                sb.AppendFormat("  \"cache\": {0}\n", cache.ToString().ToLower());
                sb.Append("}");
                string json = sb.ToString();
                HttpClient client = new HttpClient();

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://cdrm-project.com/wv");
                request.Content = new StringContent(json);
                using (var response = await client.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                    var body = await response.Content.ReadAsStringAsync();
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(body);

                    // Find the <li> element containing the Decryption Key using XPath
                    HtmlNode dcElement = htmlDoc.DocumentNode.SelectSingleNode("//li");

                    // Get the text value of the <li> element
                    dcValue = dcElement.InnerText;
                }
                return dcValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public async Task<string> GetDecryptionKeyNew(Dictionary<string, string> drmHeaders, string licenceURL, string pssh, Auth auth)
        {
            try
            {
                var resp1 = PostData(licenceURL, drmHeaders, new byte[] { 0x08, 0x04 });
                var certDataB64 = Convert.ToBase64String(resp1);
                var cdm = new CDMApi();
                var challenge = cdm.GetChallenge(pssh, certDataB64, false, false);
                var resp2 = PostData(licenceURL, drmHeaders, challenge);
                var licenseB64 = Convert.ToBase64String(resp2);
                cdm.ProvideLicense(licenseB64);
                List<ContentKey> keys = cdm.GetKeys();
                if (keys.Count > 0)
                {
                    return keys[0].ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return null;
        }
        public static bool IsStringOnlyDigits(string input)
        {
            foreach (char c in input)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }
            return true;
        }

        private static HttpClient GetHttpClient(Config? config = null)
        {
            var client = new HttpClient();
            if (config?.Timeout != null && config.Timeout > 0)
            {
                client.Timeout = TimeSpan.FromSeconds(config.Timeout.Value);
            }
            return client;
        }
    }
}
