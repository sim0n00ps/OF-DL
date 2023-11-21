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
using OF_DL.Entities.Streams;
using OF_DL.Enumurations;
using Serilog;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using WidevineClient.Widevine;
using static WidevineClient.HttpUtil;

namespace OF_DL.Helpers;

public class APIHelper : IAPIHelper
{
    private static readonly JsonSerializerSettings m_JsonSerializerSettings;
    private static readonly IDBHelper m_DBHelper;
    static APIHelper()
    {
        m_JsonSerializerSettings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore
        };
        m_DBHelper = new DBHelper();
    }


    public async Task<Dictionary<string, string>> GetDynamicHeaders(string path, string queryParams, Auth auth)
    {
        
        DynamicRules? root = new();
        var client = new HttpClient();
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(Constants.DYNAMIC_RULES),
        };
        using (var vresponse = client.Send(request))
        {
            vresponse.EnsureSuccessStatusCode();
            var body = await vresponse.Content.ReadAsStringAsync();
            root = JsonConvert.DeserializeObject<DynamicRules>(body);
        }

        long timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        string input = $"{root.static_param}\n{timestamp}\n{path + queryParams}\n{auth.USER_ID}";
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = SHA1.HashData(inputBytes);
        string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        int checksum = 0;
        foreach (int number in root.checksum_indexes)
        {
            List<int> test = new()
            {
            hashString[number]
        };
            checksum += test.Sum();
        }
        checksum += root.checksum_constant.Value;
        string sign = $"{root.start}:{hashString}:{checksum.ToString("X").ToLower()}:{root.end}";

        Dictionary<string, string> headers = new()
        {
            { "accept", "application/json, text/plain" },
            { "app-token", root.app_token },
            { "cookie", auth!.COOKIE! },
            { "sign", sign },
            { "time", timestamp.ToString() },
            { "user-id", auth!.USER_ID! },
            { "user-agent", auth!.USER_AGENT! },
            { "x-bc", auth!.X_BC! }
        };
        return headers;
    }


    private async Task<string?> BuildHeaderAndExecuteRequests(Dictionary<string, string> getParams, string endpoint, Auth auth, HttpClient client)
    {
        HttpRequestMessage request = await BuildHttpRequestMessage(getParams, endpoint, auth);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync();
        return body;
    }


    private async Task<HttpRequestMessage> BuildHttpRequestMessage(Dictionary<string, string> getParams, string endpoint, Auth auth)
    {
        string queryParams = "?" + string.Join("&", getParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        Dictionary<string, string> headers = await GetDynamicHeaders($"/api2/v2{endpoint}", queryParams, auth);

        HttpRequestMessage request = new(HttpMethod.Get, $"{Constants.API_URL}{endpoint}{queryParams}");

        foreach (KeyValuePair<string, string> keyValuePair in headers)
        {
            request.Headers.Add(keyValuePair.Key, keyValuePair.Value);
        }

        return request;
    }

    private static double ConvertToUnixTimestampWithMicrosecondPrecision(DateTime date)
    {
        DateTime origin = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        TimeSpan diff = date.ToUniversalTime() - origin;

        return diff.TotalSeconds;  // This gives the number of seconds. If you need milliseconds, use diff.TotalMilliseconds
    }

    public static bool IsStringOnlyDigits(string input)
    {
        return input.All(char.IsDigit);
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


    /// <summary>
    /// this one is used during initialization only
    /// if the config option is not available then no modificatiotns will be done on the getParams
    /// </summary>
    /// <param name="config"></param>
    /// <param name="getParams"></param>
    /// <param name="dt"></param>
    private static void UpdateGetParamsForDateSelection(Config config, ref Dictionary<string, string> getParams, DateTime? dt)
    {
        if (config.DownloadOnlySpecificDates && dt.HasValue)
        {
            UpdateGetParamsForDateSelection(
                config,
                ref getParams,
                ConvertToUnixTimestampWithMicrosecondPrecision(dt.Value).ToString("0.000000", CultureInfo.InvariantCulture)
            );
        }
    }


    private static void UpdateGetParamsForDateSelection(Config config, ref Dictionary<string, string> getParams, string unixTimeStampInMicrosec)
    {
        
        if (config.DownloadOnlySpecificDates)
        {
            switch (config.DownloadDateSelection)
            {
                case Enumerations.DownloadDateSelection.before:
                    getParams["beforePublishTime"] = unixTimeStampInMicrosec;
                    break;
                case Enumerations.DownloadDateSelection.after:
                    getParams["order"] = "publish_date_asc";
                    getParams["afterPublishTime"] = unixTimeStampInMicrosec;
                    break;
            }
        }
        else //if no
        {
            getParams["beforePublishTime"] = unixTimeStampInMicrosec;
        }
    }


    public async Task<User?> GetUserInfo(string endpoint, Auth auth)
    {
        try
        {
            Entities.User? user = new();
            int post_limit = 50;
            Dictionary<string, string> getParams = new()
            {
                { "limit", post_limit.ToString() },
                { "order", "publish_date_asc" }
            };

            HttpClient client = new();
            HttpRequestMessage request = await BuildHttpRequestMessage(getParams, endpoint, auth);

            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return user;
            }

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            user = JsonConvert.DeserializeObject<Entities.User>(body, m_JsonSerializerSettings);
            return user;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }


    public async Task<Dictionary<string, int>?> GetAllSubscriptions(Dictionary<string, string> getParams, string endpoint, Auth auth)
    {
        try
        {
            Dictionary<string, int> users = new();
            Subscriptions subscriptions = new();

            string? body = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, new HttpClient());

            subscriptions = JsonConvert.DeserializeObject<Subscriptions>(body);
            if (subscriptions != null && subscriptions.hasMore)
            {
                getParams["offset"] = subscriptions.list.Count.ToString();

                while (true)
                {
                    Subscriptions newSubscriptions = new();
                    string? loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, new HttpClient());

                    if (!string.IsNullOrEmpty(loopbody) && loopbody.Trim() != "[]")
                    {
                        newSubscriptions = JsonConvert.DeserializeObject<Subscriptions>(loopbody, m_JsonSerializerSettings);
                    }
                    else
                    {
                        break;
                    }

                    subscriptions.list.AddRange(newSubscriptions.list);
                    if (!newSubscriptions.hasMore)
                    {
                        break;
                    }
                    getParams["offset"] = subscriptions.list.Count.ToString();
                }
            }

            foreach (Subscriptions.List subscription in subscriptions.list)
            {
                if (!users.ContainsKey(subscription.username))
                {
                    users.Add(subscription.username, subscription.id);
                }
            }

            return users;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }


    public async Task<Dictionary<string, int>?> GetActiveSubscriptions(string endpoint, Auth auth)
    {
        Dictionary<string, string> getParams = new()
        {
            { "offset", "0" },
            { "limit", "50" },
            { "type", "active" },
            { "format", "infinite"}
        };

        return await GetAllSubscriptions(getParams, endpoint, auth);
    }


    public async Task<Dictionary<string, int>?> GetExpiredSubscriptions(string endpoint, Auth auth)
    {

        Dictionary<string, string> getParams = new()
        {
            { "offset", "0" },
            { "limit", "50" },
            { "type", "expired" },
            { "format", "infinite"}
        };

        return await GetAllSubscriptions(getParams, endpoint, auth); 
    }


    public async Task<Dictionary<string, int>> GetLists(string endpoint, Auth auth)
    {
        try
        {
            int offset = 0;
            Dictionary<string, string> getParams = new()
            {
                { "offset", offset.ToString() },
                { "skip_users", "all" },
                { "limit", "50" },
                { "format", "infinite" }
            };
            Dictionary<string, int> lists = new();
            while (true)
            {
                string? body = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, new HttpClient());

                if (body == null)
                {
                    break;
                }

                UserList userList = JsonConvert.DeserializeObject<UserList>(body);
                if (userList == null)
                {
                    break;
                }

                foreach (UserList.List l in userList.list)
                {
                    if (IsStringOnlyDigits(l.id) && !lists.ContainsKey(l.name))
                    {
                        lists.Add(l.name, Convert.ToInt32(l.id));
                    }
                }

                if (userList.hasMore.Value)
                {
                    offset += 50;
                    getParams["offset"] = Convert.ToString(offset);
                }
                else
                {
                    break;
                }

            }
            return lists;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }


    public async Task<List<string>?> GetListUsers(string endpoint, Auth auth)
    {
        try
        {
            int offset = 0;
            Dictionary<string, string> getParams = new()
            {
                { "offset", offset.ToString() },
                { "limit", "50" }
            };
            List<string> users = new();

            while (true)
            {
                var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, new HttpClient());
                if (body == null)
                {
                    break;
                }

                List<UsersList>? usersList = JsonConvert.DeserializeObject<List<UsersList>>(body);

                if (usersList == null || usersList.Count <= 0)
                {
                    break;
                }

                foreach (UsersList ul in usersList)
                {
                    users.Add(ul.username);
                }

                if (users.Count < 50)
                {
                    break;
                }

                offset += 50;
                getParams["offset"] = Convert.ToString(offset);

            }
            return users;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }


    public async Task<Dictionary<long, string>> GetMedia(MediaType mediatype,
                                                         string endpoint,
                                                         string? username,
                                                         string folder,
                                                         Auth auth,
                                                         Config config,
                                                         List<long> paid_post_ids)
    {
        try
        {
            Dictionary<long, string> return_urls = new();
            int post_limit = 50;
            int limit = 5;
            int offset = 0;
            Purchased paidposts = new();
            bool isPaidPosts = false;
            Post posts = new();
            PostCollection postsCollection = new();
            bool isPosts = false;
            Messages messages = new();
            bool isMessages = false;
            Archived archived = new();
            bool isArchived = false;
            List<Stories> stories = new();
            bool isStories = false;
            Highlights highlights = new();
            bool isHighlights = false;
            Purchased paidMessages = new();
            bool isPurchased = false;

            Dictionary<string, string> getParams = new();

            switch (mediatype)
            {
                case MediaType.PaidPosts:
                    isPaidPosts = true;
                    getParams = new Dictionary<string, string>
                    {
                        { "limit", post_limit.ToString() },
                        { "order", "publish_date_desc" },
                        { "format", "infinite" },
                        { "user_id", username }
                    };
                    break;

                case MediaType.Posts:
                    isPosts = true;
                    getParams = new Dictionary<string, string>
                    {
                        { "limit", post_limit.ToString() },
                        { "order", "publish_date_desc" },
                        { "format", "infinite" }
                    };

                    UpdateGetParamsForDateSelection(
                       config,
                       ref getParams,
                       config.CustomDate);
                    break;

                case MediaType.Archived:
                    isArchived = true;
                    getParams = new Dictionary<string, string>
                    {
                        { "limit", post_limit.ToString() },
                        { "order", "publish_date_desc" },
                        { "format", "infinite" },
                        { "label", "archived" }
                    };

                    UpdateGetParamsForDateSelection(
                       config,
                       ref getParams,
                       config.CustomDate);
                    break;

                case MediaType.Stories:
                    isStories = true;
                    getParams = new Dictionary<string, string>
                    {
                        { "limit", post_limit.ToString() },
                        { "order", "publish_date_desc" }
                    };
                    break;

                case MediaType.Highlights:
                    isHighlights = true;
                    getParams = new Dictionary<string, string>
                    {
                        { "limit", limit.ToString() },
                        { "offset", offset.ToString() }
                    };
                    break;

                case MediaType.Messages:
                    isMessages = true;
                    getParams = new Dictionary<string, string>
                    {
                        { "limit", post_limit.ToString() },
                        { "order", "desc" }
                    };
                    break;

                case MediaType.PaidMessages:
                    isPurchased = true;
                    getParams = new Dictionary<string, string>
                    {
                        { "limit", post_limit.ToString() },
                        { "order", "publish_date_desc" },
                        { "format", "infinite" },
                        { "user_id", username }
                    };
                    break;
            }

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, new HttpClient());

            if (isPaidPosts)
            {                   
                paidposts = JsonConvert.DeserializeObject<Purchased>(body, m_JsonSerializerSettings);
                if (paidposts != null && paidposts.hasMore)
                {
                    getParams["offset"] = paidposts.list.Count.ToString();
                    while (true)
                    {
                        Purchased newPaidPosts = new();
                        var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
                        newPaidPosts = JsonConvert.DeserializeObject<Purchased>(loopbody, m_JsonSerializerSettings);

                        paidposts.list.AddRange(newPaidPosts.list);
                        if (!newPaidPosts.hasMore)
                        {
                            break;
                        }
                        getParams["offset"] = Convert.ToString(Convert.ToInt32(getParams["offset"]) + post_limit);
                    }
                }

                foreach (Purchased.List purchase in paidposts.list)
                {
                    if (purchase.responseType == "post" && purchase.media != null && purchase.media.Count > 0)
                    {
                        List<long> previewids = new();
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
                        await m_DBHelper.AddPost(folder: folder,
                                               post_id: purchase.id,
                                               message_text: purchase.text ?? string.Empty,
                                               price: purchase.price != null ? purchase.price.ToString() : "0",
                                               is_paid: purchase.price != null && purchase.isOpened,
                                               is_archived: purchase.isArchived ?? false,
                                               created_at: purchase.createdAt != null ? purchase.createdAt.Value : purchase.postedAt.Value);
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
                                    await m_DBHelper.AddMedia(folder: folder,
                                                            media_id: medium.id,
                                                            post_id: purchase.id,
                                                            link: medium.source.source,
                                                            directory: null,
                                                            filename: null,
                                                            size: null,
                                                            api_type: "Posts",
                                                            media_type: medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)),
                                                            preview: previewids.Contains(medium.id),
                                                            downloaded: false,
                                                            created_at: null);
                                    if (!return_urls.ContainsKey(medium.id))
                                    {
                                        return_urls.Add(medium.id, medium.source.source);
                                    }
                                }
                                else if (!has && medium.canView && medium.files != null && medium.files.drm != null)
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
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
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                    if (!return_urls.ContainsKey(medium.id))
                                    {
                                        return_urls.Add(medium.id, medium.source.source);
                                    }
                                }
                                else if (medium.canView && medium.files != null && medium.files.drm != null)
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
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
                    
                posts = JsonConvert.DeserializeObject<Post>(body, m_JsonSerializerSettings);
                if (posts != null && posts.hasMore)
                {
                    UpdateGetParamsForDateSelection(
                       config,
                       ref getParams,
                       posts.tailMarker);
                    while (true)
                    {
                        Post newposts = new();
                        var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
                        newposts = JsonConvert.DeserializeObject<Post>(loopbody, m_JsonSerializerSettings);

                        posts.list.AddRange(newposts.list);
                        if (!newposts.hasMore)
                        {
                            break;
                        }
                        UpdateGetParamsForDateSelection(
                           config,
                           ref getParams,
                           newposts.tailMarker);
                    }
                }

                foreach (Post.List post in posts.list.Where(p => config.SkipAds == false || (!p.rawText.Contains("#ad") && !p.rawText.Contains("/trial/"))))
                {
                    List<long> postPreviewIds = new();
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
                    await m_DBHelper.AddPost(folder, post.id, post.text != null ? post.text : string.Empty, post.price != null ? post.price.ToString() : "0", post.price != null && post.isOpened ? true : false, post.isArchived, post.postedAt);
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
                                        await m_DBHelper.AddMedia(folder, medium.id, post.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
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
                                        await m_DBHelper.AddMedia(folder, medium.id, post.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
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
                    
                archived = JsonConvert.DeserializeObject<Archived>(body, m_JsonSerializerSettings);
                if (archived != null && archived.hasMore)
                {
                    UpdateGetParamsForDateSelection(
                       config,
                       ref getParams,
                       archived.tailMarker);
                    while (true)
                    {
                        Archived newarchived = new();

                        var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
                        newarchived = JsonConvert.DeserializeObject<Archived>(loopbody, m_JsonSerializerSettings);

                        archived.list.AddRange(newarchived.list);
                        if (!newarchived.hasMore)
                        {
                            break;
                        }
                        UpdateGetParamsForDateSelection(
                           config,
                           ref getParams,
                           newarchived.tailMarker);
                    }
                }

                foreach (Archived.List archive in archived.list)
                {
                    List<long> previewids = new();
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
                    await m_DBHelper.AddPost(folder, archive.id, archive.text != null ? archive.text : string.Empty, archive.price != null ? archive.price.ToString() : "0", archive.price != null && archive.isOpened ? true : false, archive.isArchived, archive.postedAt);
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
                                    await m_DBHelper.AddMedia(folder, medium.id, archive.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                    return_urls.Add(medium.id, medium.source.source);
                                }
                            }
                        }
                    }
                }
            }
            else if (isStories)
            {
                stories = JsonConvert.DeserializeObject<List<Stories>>(body, m_JsonSerializerSettings);
                stories = stories.OrderByDescending(x => x.createdAt).ToList();
                    
                foreach (Stories story in stories)
                {
                    if (story.createdAt != null)
                    {
                        await m_DBHelper.AddStory(folder, story.id, string.Empty, "0", false, false, story.createdAt);
                    }
                    else
                    {
                        await m_DBHelper.AddStory(folder, story.id, string.Empty, "0", false, false, story.media[0].createdAt);
                    }
                    if (story.media != null && story.media.Count > 0)
                    {
                        foreach (Stories.Medium medium in story.media)
                        {
                            await m_DBHelper.AddMedia(folder, medium.id, story.id, medium.files.source.url, null, null, null, "Stories", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), false, false, null);
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
                List<string> highlight_ids = new();
                highlights = JsonConvert.DeserializeObject<Highlights>(body, m_JsonSerializerSettings);
                    
                if (highlights.hasMore)
                {
                    offset += 5;
                    getParams["offset"] = offset.ToString();
                    while (true)
                    {
                        Highlights newhighlights = new();

                        var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
                        newhighlights = JsonConvert.DeserializeObject<Highlights>(loopbody, m_JsonSerializerSettings);

                        highlights.list.AddRange(newhighlights.list);
                        if (!newhighlights.hasMore)
                        {
                            break;
                        }
                        offset += 5;
                        getParams["offset"] = offset.ToString();
                    }
                }
                foreach (Highlights.List list in highlights.list)
                {
                    if (!highlight_ids.Contains(list.id.ToString()))
                    {
                        highlight_ids.Add(list.id.ToString());
                    }
                }

                foreach (string highlight_id in highlight_ids)
                {
                    HighlightMedia highlightMedia = new();
                    Dictionary<string, string> highlight_headers = await GetDynamicHeaders("/api2/v2/stories/highlights/" + highlight_id, string.Empty, auth);

                    HttpClient highlight_client = GetHttpClient(config);

                    HttpRequestMessage highlight_request = new(HttpMethod.Get, $"https://onlyfans.com/api2/v2/stories/highlights/{highlight_id}");

                    foreach (KeyValuePair<string, string> keyValuePair in highlight_headers)
                    {
                        highlight_request.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                    }

                    using var highlightResponse = await highlight_client.SendAsync(highlight_request);
                    highlightResponse.EnsureSuccessStatusCode();
                    var highlightBody = await highlightResponse.Content.ReadAsStringAsync();
                    highlightMedia = JsonConvert.DeserializeObject<HighlightMedia>(highlightBody, m_JsonSerializerSettings);
                    if (highlightMedia != null)
                    {
                        foreach (HighlightMedia.Story item in highlightMedia.stories)
                        {
                            await m_DBHelper.AddStory(folder, item.id, string.Empty, "0", false, false, item.createdAt);
                            if (item.media.Count > 0 && !item.media[0].files.source.url.Contains("upload"))
                            {
                                foreach (HighlightMedia.Medium medium in item.media)
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, item.id, item.media[0].files.source.url, null, null, null, "Stories", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), false, false, null);
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
            else if (isMessages)
            {
                messages = JsonConvert.DeserializeObject<Messages>(body, m_JsonSerializerSettings);
                    
                if (messages.hasMore)
                {
                    getParams["id"] = messages.list[^1].id.ToString();
                    while (true)
                    {
                        Messages newmessages = new();

                        var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
                        newmessages = JsonConvert.DeserializeObject<Messages>(loopbody, m_JsonSerializerSettings);

                        messages.list.AddRange(newmessages.list);
                        if (!newmessages.hasMore)
                        {
                            break;
                        }
                        getParams["id"] = newmessages.list[^1].id.ToString();
                    }
                }

                foreach (Messages.List list in messages.list.Where(p => config.SkipAds == false || (!p.text.Contains("#ad") && !p.text.Contains("/trial/"))))
                {
                    List<long> messagePreviewIds = new();
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
                    await m_DBHelper.AddMessage(folder, list.id, list.text != null ? list.text : string.Empty, list.price != null ? list.price.ToString() : "0", list.canPurchaseReason == "opened" ? true : list.canPurchaseReason != "opened" ? false : (bool?)null ?? false, false, list.createdAt.HasValue ? list.createdAt.Value : DateTime.Now, list.fromUser != null && list.fromUser.id != null ? list.fromUser.id.Value : int.MinValue);
                    if (list.canPurchaseReason != "opened" && list.media != null && list.media.Count > 0)
                    {
                        foreach (Messages.Medium medium in list.media)
                        {
                            if (medium.canView && medium.source.source != null && !medium.source.source.Contains("upload"))
                            {
                                await m_DBHelper.AddMedia(folder, medium.id, list.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);

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
                                await m_DBHelper.AddMedia(folder, medium.id, list.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
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
                paidMessages = JsonConvert.DeserializeObject<Purchased>(body, m_JsonSerializerSettings);
                    
                if (paidMessages != null && paidMessages.hasMore)
                {
                    getParams["offset"] = paidMessages.list.Count.ToString();
                    while (true)
                    {
                        Purchased newpaidMessages = new();

                        var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
                        newpaidMessages = JsonConvert.DeserializeObject<Purchased>(loopbody, m_JsonSerializerSettings);

                        paidMessages.list.AddRange(newpaidMessages.list);
                        if (!newpaidMessages.hasMore)
                        {
                            break;
                        }
                        getParams["offset"] = Convert.ToString(Convert.ToInt32(getParams["offset"]) + post_limit);
                    }
                }

                foreach (Purchased.List purchase in paidMessages.list.Where(p => p.responseType == "message").OrderByDescending(p => p.postedAt ?? p.createdAt))
                {
                    if (purchase.postedAt != null)
                    {
                        await m_DBHelper.AddMessage(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price : "0", true, false, purchase.postedAt.Value, purchase.fromUser.id);
                    }
                    else
                    {
                        await m_DBHelper.AddMessage(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price : "0", true, false, purchase.createdAt.Value, purchase.fromUser.id);
                    }

                    if (purchase.media != null && purchase.media.Count > 0)
                    {
                        List<long> previewids = new();
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
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
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
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
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
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
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
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
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

            return return_urls;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }


    public async Task<PaidPostCollection> GetPaidPosts(string endpoint, string folder, string username, Auth auth, Config config, List<long> paid_post_ids)
    {
        try
        {
            Purchased paidPosts = new();
            PaidPostCollection paidPostCollection = new();
            int post_limit = 50;
            Dictionary<string, string> getParams = new()
            {
                { "limit", post_limit.ToString() },
                { "order", "publish_date_desc" },
                { "format", "infinite" },
                { "user_id", username }
            };

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
            paidPosts = JsonConvert.DeserializeObject<Purchased>(body, m_JsonSerializerSettings);
            if (paidPosts != null && paidPosts.hasMore)
            {
                getParams["offset"] = paidPosts.list.Count.ToString();
                while (true)
                {

                    Purchased newPaidPosts = new();

                    var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
                    newPaidPosts = JsonConvert.DeserializeObject<Purchased>(loopbody, m_JsonSerializerSettings);

                    paidPosts.list.AddRange(newPaidPosts.list);
                    if (!newPaidPosts.hasMore)
                    {
                        break;
                    }
                    getParams["offset"] = Convert.ToString(Convert.ToInt32(getParams["offset"]) + post_limit);
                }

            }

            foreach (Purchased.List purchase in paidPosts.list)
            {
                if (purchase.responseType == "post" && purchase.media != null && purchase.media.Count > 0)
                {
                    List<long> previewids = new();
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
                    await m_DBHelper.AddPost(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price.ToString() : "0", purchase.price != null && purchase.isOpened ? true : false, purchase.isArchived.HasValue ? purchase.isArchived.Value : false, purchase.createdAt != null ? purchase.createdAt.Value : purchase.postedAt.Value);
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
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                    paidPostCollection.PaidPosts.Add(medium.id, medium.source.source);
                                    paidPostCollection.PaidPostMedia.Add(medium);
                                }
                            }
                            else if (!has && medium.canView && medium.files != null && medium.files.drm != null)
                            {
                                
                                if (!paidPostCollection.PaidPosts.ContainsKey(medium.id))
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
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
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                    paidPostCollection.PaidPosts.Add(medium.id, medium.source.source);
                                    paidPostCollection.PaidPostMedia.Add(medium);
                                }
                            }
                            else if (medium.canView && medium.files != null && medium.files.drm != null)
                            {
                                if (!paidPostCollection.PaidPosts.ContainsKey(medium.id))
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
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
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }


    public async Task<PostCollection> GetPosts(string endpoint, string folder, Auth auth, Config config, List<long> paid_post_ids)
    {
        try
        {
            Post posts = new();
            PostCollection postCollection = new();
            int post_limit = 50;
            Dictionary<string, string> getParams = new()
            {
                { "limit", post_limit.ToString() },
                { "order", "publish_date_desc" },
                { "format", "infinite" }
            };

            UpdateGetParamsForDateSelection(
                config,
                ref getParams,
                config.CustomDate);

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, new HttpClient());
            posts = JsonConvert.DeserializeObject<Post>(body, m_JsonSerializerSettings);
            if (posts != null && posts.hasMore)
            {

                UpdateGetParamsForDateSelection(
                    config,
                    ref getParams,
                    posts.tailMarker);

                while (true)
                {
                    Post newposts = new();
                    
                    var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
                    newposts = JsonConvert.DeserializeObject<Post>(loopbody, m_JsonSerializerSettings);

                    posts.list.AddRange(newposts.list);
                    if (!newposts.hasMore)
                    {
                        break;
                    }

                    UpdateGetParamsForDateSelection(
                        config,
                        ref getParams,
                        newposts.tailMarker);
                }
            }

            foreach (Post.List post in posts.list)
            {
                if (config.SkipAds)
                {
                    if(post.rawText != null && (post.rawText.Contains("#ad") || post.rawText.Contains("/trial/")))
                    {
                        continue;
                    }

                    if (post.text != null && (post.text.Contains("#ad") || post.text.Contains("/trial/")))
                    {
                        continue;
                    }
                }
                List<long> postPreviewIds = new();
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
                await m_DBHelper.AddPost(folder, post.id, post.text != null ? post.text : string.Empty, post.price != null ? post.price.ToString() : "0", post.price != null && post.isOpened ? true : false, post.isArchived, post.postedAt);
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
                            if(medium.source.source != null)
                            {
                                if (!has && !medium.source.source.Contains("upload"))
                                {
                                    if (!postCollection.Posts.ContainsKey(medium.id))
                                    {
                                        await m_DBHelper.AddMedia(folder, medium.id, post.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                        postCollection.Posts.Add(medium.id, medium.source.source);
                                        postCollection.PostMedia.Add(medium);
                                    }
                                }
                            }
                            else if(medium.preview != null && medium.source.source == null)
                            {
                                if (!has && !medium.preview.Contains("upload"))
                                {
                                    if (!postCollection.Posts.ContainsKey(medium.id))
                                    {
                                        await m_DBHelper.AddMedia(folder, medium.id, post.id, medium.preview, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                        postCollection.Posts.Add(medium.id, medium.preview);
                                        postCollection.PostMedia.Add(medium);
                                    }
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
                                    await m_DBHelper.AddMedia(folder, medium.id, post.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                    postCollection.Posts.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{post.id}");
                                    postCollection.PostMedia.Add(medium);
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
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }
    public async Task<SinglePostCollection> GetPost(string endpoint, string folder, Auth auth, Config config)
    {
        try
        {
            SinglePost singlePost = new();
            SinglePostCollection singlePostCollection = new();
            Dictionary<string, string> getParams = new()
            {
                { "skip_users", "all" }
            };

            UpdateGetParamsForDateSelection(
                config,
                ref getParams,
                config.CustomDate);

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, new HttpClient());
            singlePost = JsonConvert.DeserializeObject<SinglePost>(body, m_JsonSerializerSettings);

            if(singlePost != null)
            {
                List<long> postPreviewIds = new();
                if (singlePost.preview != null && singlePost.preview.Count > 0)
                {
                    foreach (var id in singlePost.preview)
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
                await m_DBHelper.AddPost(folder, singlePost.id, singlePost.text != null ? singlePost.text : string.Empty, singlePost.price != null ? singlePost.price.ToString() : "0", singlePost.price != null && singlePost.isOpened ? true : false, singlePost.isArchived, singlePost.postedAt);
                singlePostCollection.SinglePostObjects.Add(singlePost);
                if (singlePost.media != null && singlePost.media.Count > 0)
                {
                    foreach (SinglePost.Medium medium in singlePost.media)
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
                            if(medium.source.source != null)
                            {
                                if (!medium.source.source.Contains("upload"))
                                {
                                    if (!singlePostCollection.SinglePosts.ContainsKey(medium.id))
                                    {
                                        await m_DBHelper.AddMedia(folder, medium.id, singlePost.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                        singlePostCollection.SinglePosts.Add(medium.id, medium.source.source);
                                        singlePostCollection.SinglePostMedia.Add(medium);
                                    }
                                }
                            }
                            else if(medium.preview != null && medium.source.source == null)
                            {
                                if (!medium.preview.Contains("upload"))
                                {
                                    if (!singlePostCollection.SinglePosts.ContainsKey(medium.id))
                                    {
                                        await m_DBHelper.AddMedia(folder, medium.id, singlePost.id, medium.preview, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                        singlePostCollection.SinglePosts.Add(medium.id, medium.preview);
                                        singlePostCollection.SinglePostMedia.Add(medium);
                                    }
                                }
                            }
                        }
                        else if (medium.canView && medium.files != null && medium.files.drm != null)
                        {
                            if (medium.files != null && medium.files.drm != null)
                            {
                                if (!singlePostCollection.SinglePosts.ContainsKey(medium.id))
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, singlePost.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                    singlePostCollection.SinglePosts.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{singlePost.id}");
                                    singlePostCollection.SinglePostMedia.Add(medium);
                                }
                            }
                        }
                    }
                }
            }

            return singlePostCollection;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }

    public async Task<StreamsCollection> GetStreams(string endpoint, string folder, Auth auth, Config config, List<long> paid_post_ids)
    {
        try
        {
            Streams streams = new();
            StreamsCollection streamsCollection = new();
            int post_limit = 50;
            Dictionary<string, string> getParams = new()
            {
                { "limit", post_limit.ToString() },
                { "order", "publish_date_desc" },
                { "format", "infinite" }
            };

            UpdateGetParamsForDateSelection(
                config,
                ref getParams,
                config.CustomDate);

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, new HttpClient());
            streams = JsonConvert.DeserializeObject<Streams>(body, m_JsonSerializerSettings);
            if (streams != null && streams.hasMore)
            {

                UpdateGetParamsForDateSelection(
                    config,
                    ref getParams,
                    streams.tailMarker);

                while (true)
                {
                    Streams newstreams = new();

                    var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
                    newstreams = JsonConvert.DeserializeObject<Streams>(loopbody, m_JsonSerializerSettings);

                    streams.list.AddRange(newstreams.list);
                    if (!newstreams.hasMore)
                    {
                        break;
                    }

                    UpdateGetParamsForDateSelection(
                        config,
                        ref getParams,
                        newstreams.tailMarker);
                }
            }

            foreach (Streams.List stream in streams.list)
            {
                List<long> streamPreviewIds = new();
                if (stream.preview != null && stream.preview.Count > 0)
                {
                    foreach (var id in stream.preview)
                    {
                        if (id?.ToString() != "poll")
                        {
                            if (!streamPreviewIds.Contains(Convert.ToInt64(id)))
                            {
                                streamPreviewIds.Add(Convert.ToInt64(id));
                            }
                        }
                    }
                }
                await m_DBHelper.AddPost(folder, stream.id, stream.text != null ? stream.text : string.Empty, stream.price != null ? stream.price.ToString() : "0", stream.price != null && stream.isOpened ? true : false, stream.isArchived, stream.postedAt);
                streamsCollection.StreamObjects.Add(stream);
                if (stream.media != null && stream.media.Count > 0)
                {
                    foreach (Streams.Medium medium in stream.media)
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
                                if (!streamsCollection.Streams.ContainsKey(medium.id))
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, stream.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), streamPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                    streamsCollection.Streams.Add(medium.id, medium.source.source);
                                    streamsCollection.StreamMedia.Add(medium);
                                }
                            }
                        }
                        else if (medium.canView && medium.files != null && medium.files.drm != null)
                        {
                            bool has = paid_post_ids.Any(cus => cus.Equals(medium.id));
                            if (!has && medium.files != null && medium.files.drm != null)
                            {
                                if (!streamsCollection.Streams.ContainsKey(medium.id))
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, stream.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), streamPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                    streamsCollection.Streams.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{stream.id}");
                                    streamsCollection.StreamMedia.Add(medium);
                                }
                            }
                        }
                    }
                }
            }

            return streamsCollection;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }


    public async Task<ArchivedCollection> GetArchived(string endpoint, string folder, Auth auth, Config config)
    {
        try
        {
            Archived archived = new();
            ArchivedCollection archivedCollection = new();
            int post_limit = 50;
            Dictionary<string, string> getParams = new()
            {
                { "limit", post_limit.ToString() },
                { "order", "publish_date_desc" },
                { "skip_users", "all" },
                { "format", "infinite" },
                { "label", "archived" },
                { "counters", "1" }
            };

            UpdateGetParamsForDateSelection(
                config,
                ref getParams,
                config.CustomDate);

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
            archived = JsonConvert.DeserializeObject<Archived>(body, m_JsonSerializerSettings);
            if (archived != null && archived.hasMore)
            {
                UpdateGetParamsForDateSelection(
                   config,
                   ref getParams,
                   archived.tailMarker);
                while (true)
                {
                    Archived newarchived = new();

                    var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
                    newarchived = JsonConvert.DeserializeObject<Archived>(loopbody, m_JsonSerializerSettings);

                    archived.list.AddRange(newarchived.list);
                    if (!newarchived.hasMore)
                    {
                        break;
                    }
                    UpdateGetParamsForDateSelection(
                       config,
                       ref getParams,
                       newarchived.tailMarker);
                }
            }

            foreach (Archived.List archive in archived.list)
            {
                List<long> previewids = new();
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
                await m_DBHelper.AddPost(folder, archive.id, archive.text != null ? archive.text : string.Empty, archive.price != null ? archive.price.ToString() : "0", archive.price != null && archive.isOpened ? true : false, archive.isArchived, archive.postedAt);
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
                                await m_DBHelper.AddMedia(folder, medium.id, archive.id, medium.source.source, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                archivedCollection.ArchivedPosts.Add(medium.id, medium.source.source);
                                archivedCollection.ArchivedPostMedia.Add(medium);
                            }
                        }
                        else if (medium.canView && medium.files != null && medium.files.drm != null)
                        {
                            if (!archivedCollection.ArchivedPosts.ContainsKey(medium.id))
                            {
                                await m_DBHelper.AddMedia(folder, medium.id, archive.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                archivedCollection.ArchivedPosts.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{archive.id}");
                                archivedCollection.ArchivedPostMedia.Add(medium);
                            }
                        }
                    }
                }
            }

            return archivedCollection;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }


    public async Task<MessageCollection> GetMessages(string endpoint, string folder, Auth auth, Config config)
    {
        try
        {
            Messages messages = new();
            MessageCollection messageCollection = new();
            int post_limit = 50;
            Dictionary<string, string> getParams = new()
            {
                { "limit", post_limit.ToString() },
                { "order", "desc" }
            };

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
            messages = JsonConvert.DeserializeObject<Messages>(body, m_JsonSerializerSettings);
            if (messages.hasMore)
            {
                getParams["id"] = messages.list[^1].id.ToString();
                while (true)
                {
                    Messages newmessages = new();

                    var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
                    newmessages = JsonConvert.DeserializeObject<Messages>(loopbody, m_JsonSerializerSettings);

                    messages.list.AddRange(newmessages.list);
                    if (!newmessages.hasMore)
                    {
                        break;
                    }
                    getParams["id"] = newmessages.list[newmessages.list.Count - 1].id.ToString();
                }
            }

            foreach (Messages.List list in messages.list)
            {
                if (config.SkipAds)
                {
                    if (list.text != null && (list.text.Contains("#ad") || list.text.Contains("/trial/")))
                    {
                        continue;
                    }
                }
                List<long> messagePreviewIds = new();
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
                await m_DBHelper.AddMessage(folder, list.id, list.text != null ? list.text : string.Empty, list.price != null ? list.price.ToString() : "0", list.canPurchaseReason == "opened" ? true : list.canPurchaseReason != "opened" ? false : (bool?)null ?? false, false, list.createdAt.HasValue ? list.createdAt.Value : DateTime.Now, list.fromUser != null && list.fromUser.id != null ? list.fromUser.id.Value : int.MinValue);
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
                                await m_DBHelper.AddMedia(folder, medium.id, list.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
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
                                await m_DBHelper.AddMedia(folder, medium.id, list.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
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
                                await m_DBHelper.AddMedia(folder, medium.id, list.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
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
                                await m_DBHelper.AddMedia(folder, medium.id, list.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
                                messageCollection.Messages.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{list.id}");
                                messageCollection.MessageMedia.Add(medium);
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
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }


    public async Task<PaidMessageCollection> GetPaidMessages(string endpoint, string folder, string username, Auth auth, Config config)
    {
        try
        {
            Purchased paidMessages = new();
            PaidMessageCollection paidMessageCollection = new();
            int post_limit = 50;
            Dictionary<string, string> getParams = new()
            {
                { "limit", post_limit.ToString() },
                { "order", "publish_date_desc" },
                { "format", "infinite" },
                { "user_id", username }
            };

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, auth, GetHttpClient(config));
            paidMessages = JsonConvert.DeserializeObject<Purchased>(body, m_JsonSerializerSettings);
            if (paidMessages != null && paidMessages.hasMore)
            {
                getParams["offset"] = paidMessages.list.Count.ToString();
                while (true)
                {
                    string loopqueryParams = "?" + string.Join("&", getParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    Purchased newpaidMessages = new();
                    Dictionary<string, string> loopheaders = await GetDynamicHeaders("/api2/v2" + endpoint, loopqueryParams, auth);
                    HttpClient loopclient = GetHttpClient(config);

                    HttpRequestMessage looprequest = new(HttpMethod.Get, $"{Constants.API_URL}{endpoint}{loopqueryParams}");

                    foreach (KeyValuePair<string, string> keyValuePair in loopheaders)
                    {
                        looprequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                    using (var loopresponse = await loopclient.SendAsync(looprequest))
                    {
                        loopresponse.EnsureSuccessStatusCode();
                        var loopbody = await loopresponse.Content.ReadAsStringAsync();
                        newpaidMessages = JsonConvert.DeserializeObject<Purchased>(loopbody, m_JsonSerializerSettings);
                    }
                    paidMessages.list.AddRange(newpaidMessages.list);
                    if (!newpaidMessages.hasMore)
                    {
                        break;
                    }
                    getParams["offset"] = Convert.ToString(Convert.ToInt32(getParams["offset"]) + post_limit);
                }
            }

            if(paidMessages.list != null && paidMessages.list.Count > 0)
            {
                foreach (Purchased.List purchase in paidMessages.list.Where(p => p.responseType == "message").OrderByDescending(p => p.postedAt ?? p.createdAt))
                {
                    if (purchase.postedAt != null)
                    {
                        await m_DBHelper.AddMessage(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price : "0", true, false, purchase.postedAt.Value, purchase.fromUser.id);
                    }
                    else
                    {
                        await m_DBHelper.AddMessage(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price : "0", true, false, purchase.createdAt.Value, purchase.fromUser.id);
                    }
                    paidMessageCollection.PaidMessageObjects.Add(purchase);
                    if (purchase.media != null && purchase.media.Count > 0)
                    {
                        List<long> previewids = new();
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
                                        await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
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
                                        await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
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
                                        await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.source.source, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
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
                                        await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                        paidMessageCollection.PaidMessages.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                        paidMessageCollection.PaidMessageMedia.Add(medium);
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
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }


    public async Task<string> GetDRMMPDPSSH(string mpdUrl, string policy, string signature, string kvp, Auth auth)
    {
        try
        {
            string pssh = null;
            
            HttpClient client = new();
            HttpRequestMessage request = new(HttpMethod.Get, mpdUrl);
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
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }


    public async Task<DateTime> GetDRMMPDLastModified(string mpdUrl, string policy, string signature, string kvp, Auth auth)
    {
        try
        {
            DateTime lastmodified;
            
            HttpClient client = new();
            HttpRequestMessage request = new(HttpMethod.Get, mpdUrl);
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
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
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

            StringBuilder sb = new();
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
            sb.Remove(sb.Length - 2, 2);
            sb.Append("\",\n");
            sb.AppendFormat("  \"pssh\": \"{0}\",\n", pssh);
            sb.AppendFormat("  \"buildInfo\": \"{0}\",\n", buildInfo);
            sb.AppendFormat("  \"proxy\": \"{0}\",\n", proxy);
            sb.AppendFormat("  \"cache\": {0}\n", cache.ToString().ToLower());
            sb.Append('}');
            string json = sb.ToString();
            HttpClient client = new();

            HttpRequestMessage request = new(HttpMethod.Post, "https://cdrm-project.com/wv")
            {
                Content = new StringContent(json)
            };

            using var response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(body);

            // Find the <li> element containing the Decryption Key using XPath
            HtmlNode dcElement = htmlDoc.DocumentNode.SelectSingleNode("//li");

            // Get the text value of the <li> element
            dcValue = dcElement.InnerText;

            return dcValue;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
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
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return null;
    }
}
