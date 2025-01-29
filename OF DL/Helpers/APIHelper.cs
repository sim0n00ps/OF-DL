using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using System.Text.Json;
using System.Xml.Linq;
using WidevineClient.Widevine;
using static WidevineClient.HttpUtil;

namespace OF_DL.Helpers;

public class APIHelper : IAPIHelper
{
    private static readonly JsonSerializerSettings m_JsonSerializerSettings;
    private readonly IDBHelper m_DBHelper;
    private readonly Auth auth;

    static APIHelper()
    {
        m_JsonSerializerSettings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore
        };
    }

    public APIHelper(Auth auth, IDownloadConfig downloadConfig)
    {
        this.auth = auth;
        m_DBHelper = new DBHelper(downloadConfig);
    }


    public Dictionary<string, string> GetDynamicHeaders(string path, string queryParams)
    {
        Log.Debug("Calling GetDynamicHeaders");
        Log.Debug($"Path: {path}");
        Log.Debug($"Query Params: {queryParams}");

        DynamicRules? root;

        //Get rules from GitHub and fallback to local file
        string? dynamicRulesJSON = GetDynamicRules();
        if (!string.IsNullOrEmpty(dynamicRulesJSON))
        {
            root = JsonConvert.DeserializeObject<DynamicRules>(dynamicRulesJSON);
        }
        else
        {
            root = JsonConvert.DeserializeObject<DynamicRules>(File.ReadAllText("rules.json"));
        }

        DateTimeOffset dto = (DateTimeOffset)DateTime.UtcNow;
        long timestamp = dto.ToUnixTimeMilliseconds();

        string input = $"{root!.StaticParam}\n{timestamp}\n{path + queryParams}\n{auth.USER_ID}";
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = SHA1.HashData(inputBytes);
        string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        var checksum = root.ChecksumIndexes.Aggregate(0, (current, number) => current + hashString[number]) + root.ChecksumConstant!.Value;
        var sign = $"{root.Prefix}:{hashString}:{checksum.ToString("X").ToLower()}:{root.Suffix}";

        Dictionary<string, string> headers = new()
        {
            { "accept", "application/json, text/plain" },
            { "app-token", root.AppToken! },
            { "cookie", auth!.COOKIE! },
            { "sign", sign },
            { "time", timestamp.ToString() },
            { "user-id", auth!.USER_ID! },
            { "user-agent", auth!.USER_AGENT! },
            { "x-bc", auth!.X_BC! }
        };
        return headers;
    }


    private async Task<string?> BuildHeaderAndExecuteRequests(Dictionary<string, string> getParams, string endpoint, HttpClient client)
    {
        Log.Debug("Calling BuildHeaderAndExecuteRequests");

        HttpRequestMessage request = await BuildHttpRequestMessage(getParams, endpoint);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync();

        Log.Debug(body);

        return body;
    }


    private async Task<HttpRequestMessage> BuildHttpRequestMessage(Dictionary<string, string> getParams, string endpoint)
    {
        Log.Debug("Calling BuildHttpRequestMessage");

        string queryParams = "?" + string.Join("&", getParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        Dictionary<string, string> headers = GetDynamicHeaders($"/api2/v2{endpoint}", queryParams);

        HttpRequestMessage request = new(HttpMethod.Get, $"{Constants.API_URL}{endpoint}{queryParams}");

        Log.Debug($"Full request URL: {Constants.API_URL}{endpoint}{queryParams}");

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


    private static HttpClient GetHttpClient(IDownloadConfig? config = null)
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
    private static void UpdateGetParamsForDateSelection(Enumerations.DownloadDateSelection downloadDateSelection, ref Dictionary<string, string> getParams, DateTime? dt)
    {
        //if (config.DownloadOnlySpecificDates && dt.HasValue)
        //{
        if (dt.HasValue)
        {
            UpdateGetParamsForDateSelection(
                downloadDateSelection,
                ref getParams,
                ConvertToUnixTimestampWithMicrosecondPrecision(dt.Value).ToString("0.000000", CultureInfo.InvariantCulture)
            );
        }
        //}
    }

    private static void UpdateGetParamsForDateSelection(Enumerations.DownloadDateSelection downloadDateSelection, ref Dictionary<string, string> getParams, string unixTimeStampInMicrosec)
    {
        switch (downloadDateSelection)
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


    public async Task<User?> GetUserInfo(string endpoint)
    {
        Log.Debug($"Calling GetUserInfo: {endpoint}");

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
            HttpRequestMessage request = await BuildHttpRequestMessage(getParams, endpoint);

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

    public async Task<JObject> GetUserInfoById(string endpoint)
    {
        try
        {
            HttpClient client = new();
            HttpRequestMessage request = await BuildHttpRequestMessage(new Dictionary<string, string>(), endpoint);

            using var response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();

            //if the content creator doesnt exist, we get a 200 response, but the content isnt usable
            //so let's not throw an exception, since "content creator no longer exists" is handled elsewhere
            //which means we wont get loads of exceptions
            if (body.Equals("[]"))
                return null;

            JObject jObject = JObject.Parse(body);

            return jObject;
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


    public async Task<Dictionary<string, int>?> GetAllSubscriptions(Dictionary<string, string> getParams, string endpoint, bool includeRestricted, IDownloadConfig config)
    {
        try
        {
            Dictionary<string, int> users = new();
            Subscriptions subscriptions = new();

            Log.Debug("Calling GetAllSubscrptions");

            string? body = await BuildHeaderAndExecuteRequests(getParams, endpoint, new HttpClient());

            subscriptions = JsonConvert.DeserializeObject<Subscriptions>(body);
            if (subscriptions != null && subscriptions.hasMore)
            {
                getParams["offset"] = subscriptions.list.Count.ToString();

                while (true)
                {
                    Subscriptions newSubscriptions = new();
                    string? loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, new HttpClient());

                    if (!string.IsNullOrEmpty(loopbody) && (!loopbody.Contains("[]") || loopbody.Trim() != "[]"))
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
                if ((!(subscription.isRestricted ?? false) || ((subscription.isRestricted ?? false) && includeRestricted))
                    && !users.ContainsKey(subscription.username))
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

    public async Task<Dictionary<string, int>?> GetActiveSubscriptions(string endpoint, bool includeRestricted, IDownloadConfig config)
    {
        Dictionary<string, string> getParams = new()
        {
            { "offset", "0" },
            { "limit", "50" },
            { "type", "active" },
            { "format", "infinite"}
        };

        return await GetAllSubscriptions(getParams, endpoint, includeRestricted, config);
    }


    public async Task<Dictionary<string, int>?> GetExpiredSubscriptions(string endpoint, bool includeRestricted, IDownloadConfig config)
    {

        Dictionary<string, string> getParams = new()
        {
            { "offset", "0" },
            { "limit", "50" },
            { "type", "expired" },
            { "format", "infinite"}
        };

        Log.Debug("Calling GetExpiredSubscriptions");

        return await GetAllSubscriptions(getParams, endpoint, includeRestricted, config);
    }


    public async Task<Dictionary<string, int>> GetLists(string endpoint, IDownloadConfig config)
    {
        Log.Debug("Calling GetLists");

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
                string? body = await BuildHeaderAndExecuteRequests(getParams, endpoint, new HttpClient());

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


    public async Task<List<string>?> GetListUsers(string endpoint, IDownloadConfig config)
    {
        Log.Debug($"Calling GetListUsers - {endpoint}");

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
                var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, new HttpClient());
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
                                                         IDownloadConfig config,
                                                         List<long> paid_post_ids)
    {

        Log.Debug($"Calling GetMedia - {username}");

        try
        {
            Dictionary<long, string> return_urls = new();
            int post_limit = 50;
            int limit = 5;
            int offset = 0;

            Dictionary<string, string> getParams = new();

            switch (mediatype)
            {

                case MediaType.Stories:
                    getParams = new Dictionary<string, string>
                    {
                        { "limit", post_limit.ToString() },
                        { "order", "publish_date_desc" }
                    };
                    break;

                case MediaType.Highlights:
                    getParams = new Dictionary<string, string>
                    {
                        { "limit", limit.ToString() },
                        { "offset", offset.ToString() }
                    };
                    break;
            }

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, new HttpClient());


            if (mediatype == MediaType.Stories)
            {
                Log.Debug("Media Stories - " + endpoint);

                var stories = JsonConvert.DeserializeObject<List<Stories>>(body, m_JsonSerializerSettings) ?? new List<Stories>();

                foreach (Stories story in stories)
                {
                    if (story.media[0].createdAt.HasValue)
                    {
                        await m_DBHelper.AddStory(folder, story.id, string.Empty, "0", false, false, story.media[0].createdAt.Value);
                    }
                    else if (story.createdAt.HasValue)
                    {
                        await m_DBHelper.AddStory(folder, story.id, string.Empty, "0", false, false, story.createdAt.Value);
                    }
                    else
                    {
                        await m_DBHelper.AddStory(folder, story.id, string.Empty, "0", false, false, DateTime.Now);
                    }
                    if (story.media != null && story.media.Count > 0)
                    {
                        foreach (Stories.Medium medium in story.media)
                        {
                            await m_DBHelper.AddMedia(folder, medium.id, story.id, medium.files.full.url, null, null, null, "Stories", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), false, false, null);
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
                            if (medium.canView && !medium.files.full.url.Contains("upload"))
                            {
                                if (!return_urls.ContainsKey(medium.id))
                                {
                                    return_urls.Add(medium.id, medium.files.full.url);
                                }
                            }
                        }
                    }
                }
            }
            else if (mediatype == MediaType.Highlights)
            {
                List<string> highlight_ids = new();
                var highlights = JsonConvert.DeserializeObject<Highlights>(body, m_JsonSerializerSettings) ?? new Highlights();

                if (highlights.hasMore)
                {
                    offset += 5;
                    getParams["offset"] = offset.ToString();
                    while (true)
                    {
                        Highlights newhighlights = new();

                        Log.Debug("Media Highlights - " + endpoint);

                        var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
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
                    Dictionary<string, string> highlight_headers = GetDynamicHeaders("/api2/v2/stories/highlights/" + highlight_id, string.Empty);

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
                            if (item.media[0].createdAt.HasValue)
                            {
                                await m_DBHelper.AddStory(folder, item.id, string.Empty, "0", false, false, item.media[0].createdAt.Value);
                            }
                            else if (item.createdAt.HasValue)
                            {
                                await m_DBHelper.AddStory(folder, item.id, string.Empty, "0", false, false, item.createdAt.Value);
                            }
                            else
                            {
                                await m_DBHelper.AddStory(folder, item.id, string.Empty, "0", false, false, DateTime.Now);
                            }
                            if (item.media.Count > 0 && item.media[0].canView)
                            {
                                foreach (HighlightMedia.Medium medium in item.media)
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, item.id, item.media[0].files.full.url, null, null, null, "Stories", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), false, false, null);
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
                                        return_urls.Add(medium.id, item.media[0].files.full.url);
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


    public async Task<PaidPostCollection> GetPaidPosts(string endpoint, string folder, string username, IDownloadConfig config, List<long> paid_post_ids)
    {
        Log.Debug($"Calling GetPaidPosts - {username}");

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

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
            paidPosts = JsonConvert.DeserializeObject<Purchased>(body, m_JsonSerializerSettings);
            if (paidPosts != null && paidPosts.hasMore)
            {
                getParams["offset"] = paidPosts.list.Count.ToString();
                while (true)
                {

                    Purchased newPaidPosts = new();

                    var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
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
                            if (purchase.previews[i] is long previewId)
                            {
                                if (!previewids.Contains(previewId))
                                {
                                    previewids.Add(previewId);
                                }
                            }
                        }
                    }
                    else if (purchase.preview != null)
                    {
                        for (int i = 0; i < purchase.preview.Count; i++)
                        {
                            if (purchase.preview[i] is long previewId)
                            {
                                if (!previewids.Contains(previewId))
                                {
                                    previewids.Add(previewId);
                                }
                            }
                        }
                    }
                    await m_DBHelper.AddPost(folder, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price.ToString() : "0", purchase.price != null && purchase.isOpened ? true : false, purchase.isArchived.HasValue ? purchase.isArchived.Value : false, purchase.createdAt != null ? purchase.createdAt.Value : purchase.postedAt.Value);
                    paidPostCollection.PaidPostObjects.Add(purchase);
                    foreach (Messages.Medium medium in purchase.media)
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
                            if (!has && medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
                            {
                                if (!paidPostCollection.PaidPosts.ContainsKey(medium.id))
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.full.url, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                    paidPostCollection.PaidPosts.Add(medium.id, medium.files.full.url);
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
                            if (medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
                            {
                                if (!paidPostCollection.PaidPosts.ContainsKey(medium.id))
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.full.url, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                    paidPostCollection.PaidPosts.Add(medium.id, medium.files.full.url);
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


    public async Task<PostCollection> GetPosts(string endpoint, string folder, IDownloadConfig config, List<long> paid_post_ids)
    {
        Log.Debug($"Calling GetPosts - {endpoint}");

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

            Enumerations.DownloadDateSelection downloadDateSelection = Enumerations.DownloadDateSelection.before;
            DateTime? downloadAsOf = null;

            if (config.DownloadOnlySpecificDates && config.CustomDate.HasValue)
            {
                downloadDateSelection = config.DownloadDateSelection;
                downloadAsOf = config.CustomDate;
            }
            else if (config.DownloadPostsIncrementally)
            {
                var mostRecentPostDate = await m_DBHelper.GetMostRecentPostDate(folder);
                if (mostRecentPostDate.HasValue)
                {
                    downloadDateSelection = Enumerations.DownloadDateSelection.after;
                    downloadAsOf = mostRecentPostDate.Value.AddMinutes(-5); // Back track a little for a margin of error
                }
            }

            UpdateGetParamsForDateSelection(
                downloadDateSelection,
                ref getParams,
                downloadAsOf);

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, new HttpClient());
            posts = JsonConvert.DeserializeObject<Post>(body, m_JsonSerializerSettings);
            if (posts != null && posts.hasMore)
            {

                UpdateGetParamsForDateSelection(
                    downloadDateSelection,
                    ref getParams,
                    posts.tailMarker);

                while (true)
                {
                    Post newposts = new();

                    var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
                    newposts = JsonConvert.DeserializeObject<Post>(loopbody, m_JsonSerializerSettings);

                    posts.list.AddRange(newposts.list);
                    if (!newposts.hasMore)
                    {
                        break;
                    }

                    UpdateGetParamsForDateSelection(
                        downloadDateSelection,
                        ref getParams,
                        newposts.tailMarker);
                }
            }

            foreach (Post.List post in posts.list)
            {
                if (config.SkipAds)
                {
                    if (post.rawText != null && (post.rawText.Contains("#ad") || post.rawText.Contains("/trial/") || post.rawText.Contains("#announcement")))
                    {
                        continue;
                    }

                    if (post.text != null && (post.text.Contains("#ad") || post.text.Contains("/trial/") || post.text.Contains("#announcement")))
                    {
                        continue;
                    }
                }
                List<long> postPreviewIds = new();
                if (post.preview != null && post.preview.Count > 0)
                {
                    for (int i = 0; i < post.preview.Count; i++)
                    {
                        if (post.preview[i] is long previewId)
                        {
                            if (!postPreviewIds.Contains(previewId))
                            {
                                postPreviewIds.Add(previewId);
                            }
                        }
                    }
                }
                await m_DBHelper.AddPost(folder, post.id, post.rawText != null ? post.rawText : string.Empty, post.price != null ? post.price.ToString() : "0", post.price != null && post.isOpened ? true : false, post.isArchived, post.postedAt);
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
                            if (medium.files!.full != null && !string.IsNullOrEmpty(medium.files!.full.url))
                            {
                                if (!has && !medium.files!.full.url.Contains("upload"))
                                {
                                    if (!postCollection.Posts.ContainsKey(medium.id))
                                    {
                                        await m_DBHelper.AddMedia(folder, medium.id, post.id, medium.files!.full.url, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                        postCollection.Posts.Add(medium.id, medium.files!.full.url);
                                        postCollection.PostMedia.Add(medium);
                                    }
                                }
                            }
                            else if (medium.files.preview != null && medium.files!.full == null)
                            {
                                if (!has && !medium.files.preview.url.Contains("upload"))
                                {
                                    if (!postCollection.Posts.ContainsKey(medium.id))
                                    {
                                        await m_DBHelper.AddMedia(folder, medium.id, post.id, medium.files.preview.url, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                        postCollection.Posts.Add(medium.id, medium.files.preview.url);
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
    public async Task<SinglePostCollection> GetPost(string endpoint, string folder, IDownloadConfig config)
    {
        Log.Debug($"Calling GetPost - {endpoint}");

        try
        {
            SinglePost singlePost = new();
            SinglePostCollection singlePostCollection = new();
            Dictionary<string, string> getParams = new()
            {
                { "skip_users", "all" }
            };

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, new HttpClient());
            singlePost = JsonConvert.DeserializeObject<SinglePost>(body, m_JsonSerializerSettings);

            if (singlePost != null)
            {
                List<long> postPreviewIds = new();
                if (singlePost.preview != null && singlePost.preview.Count > 0)
                {
                    for (int i = 0; i < singlePost.preview.Count; i++)
                    {
                        if (singlePost.preview[i] is long previewId)
                        {
                            if (!postPreviewIds.Contains(previewId))
                            {
                                postPreviewIds.Add(previewId);
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
                            if (medium.files!.full != null && !string.IsNullOrEmpty(medium.files!.full.url))
                            {
                                if (!medium.files!.full.url.Contains("upload"))
                                {
                                    if (!singlePostCollection.SinglePosts.ContainsKey(medium.id))
                                    {
                                        await m_DBHelper.AddMedia(folder, medium.id, singlePost.id, medium.files!.full.url, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                        singlePostCollection.SinglePosts.Add(medium.id, medium.files!.full.url);
                                        singlePostCollection.SinglePostMedia.Add(medium);
                                    }
                                }
                            }
                            else if (medium.files.preview != null && medium.files!.full == null)
                            {
                                if (!medium.files.preview.url.Contains("upload"))
                                {
                                    if (!singlePostCollection.SinglePosts.ContainsKey(medium.id))
                                    {
                                        await m_DBHelper.AddMedia(folder, medium.id, singlePost.id, medium.files.preview.url, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), postPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                        singlePostCollection.SinglePosts.Add(medium.id, medium.files.preview.url);
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

    public async Task<StreamsCollection> GetStreams(string endpoint, string folder, IDownloadConfig config, List<long> paid_post_ids)
    {
        Log.Debug($"Calling GetStreams - {endpoint}");

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

            Enumerations.DownloadDateSelection downloadDateSelection = Enumerations.DownloadDateSelection.before;
            if (config.DownloadOnlySpecificDates && config.CustomDate.HasValue)
            {
                downloadDateSelection = config.DownloadDateSelection;
            }

            UpdateGetParamsForDateSelection(
                downloadDateSelection,
                ref getParams,
                config.CustomDate);

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, new HttpClient());
            streams = JsonConvert.DeserializeObject<Streams>(body, m_JsonSerializerSettings);
            if (streams != null && streams.hasMore)
            {

                UpdateGetParamsForDateSelection(
                    downloadDateSelection,
                    ref getParams,
                    streams.tailMarker);

                while (true)
                {
                    Streams newstreams = new();

                    var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
                    newstreams = JsonConvert.DeserializeObject<Streams>(loopbody, m_JsonSerializerSettings);

                    streams.list.AddRange(newstreams.list);
                    if (!newstreams.hasMore)
                    {
                        break;
                    }

                    UpdateGetParamsForDateSelection(
                        downloadDateSelection,
                        ref getParams,
                        newstreams.tailMarker);
                }
            }

            foreach (Streams.List stream in streams.list)
            {
                List<long> streamPreviewIds = new();
                if (stream.preview != null && stream.preview.Count > 0)
                {
                    for (int i = 0; i < stream.preview.Count; i++)
                    {
                        if (stream.preview[i] is long previewId)
                        {
                            if (!streamPreviewIds.Contains(previewId))
                            {
                                streamPreviewIds.Add(previewId);
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
                            if (!has && medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
                            {
                                if (!streamsCollection.Streams.ContainsKey(medium.id))
                                {
                                    await m_DBHelper.AddMedia(folder, medium.id, stream.id, medium.files.full.url, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), streamPreviewIds.Contains((long)medium.id) ? true : false, false, null);
                                    streamsCollection.Streams.Add(medium.id, medium.files.full.url);
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


    public async Task<ArchivedCollection> GetArchived(string endpoint, string folder, IDownloadConfig config)
    {
        Log.Debug($"Calling GetArchived - {endpoint}");

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

            Enumerations.DownloadDateSelection downloadDateSelection = Enumerations.DownloadDateSelection.before;
            if (config.DownloadOnlySpecificDates && config.CustomDate.HasValue)
            {
                downloadDateSelection = config.DownloadDateSelection;
            }

            UpdateGetParamsForDateSelection(
                downloadDateSelection,
                ref getParams,
                config.CustomDate);

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
            archived = JsonConvert.DeserializeObject<Archived>(body, m_JsonSerializerSettings);
            if (archived != null && archived.hasMore)
            {
                UpdateGetParamsForDateSelection(
                   downloadDateSelection,
                   ref getParams,
                   archived.tailMarker);
                while (true)
                {
                    Archived newarchived = new();

                    var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
                    newarchived = JsonConvert.DeserializeObject<Archived>(loopbody, m_JsonSerializerSettings);

                    archived.list.AddRange(newarchived.list);
                    if (!newarchived.hasMore)
                    {
                        break;
                    }
                    UpdateGetParamsForDateSelection(
                       downloadDateSelection,
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
                        if (archive.preview[i] is long previewId)
                        {
                            if (!previewids.Contains(previewId))
                            {
                                previewids.Add(previewId);
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
                        if (medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
                        {
                            if (!archivedCollection.ArchivedPosts.ContainsKey(medium.id))
                            {
                                await m_DBHelper.AddMedia(folder, medium.id, archive.id, medium.files.full.url, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                archivedCollection.ArchivedPosts.Add(medium.id, medium.files.full.url);
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


    public async Task<MessageCollection> GetMessages(string endpoint, string folder, IDownloadConfig config)
    {
        Log.Debug($"Calling GetMessages - {endpoint}");

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

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
            messages = JsonConvert.DeserializeObject<Messages>(body, m_JsonSerializerSettings);
            if (messages.hasMore)
            {
                getParams["id"] = messages.list[^1].id.ToString();
                while (true)
                {
                    Messages newmessages = new();

                    var loopbody = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
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
                    for (int i = 0; i < list.previews.Count; i++)
                    {
                        if (list.previews[i] is long previewId)
                        {
                            if (!messagePreviewIds.Contains(previewId))
                            {
                                messagePreviewIds.Add(previewId);
                            }
                        }
                    }
                }
                if (!config.IgnoreOwnMessages || list.fromUser.id != Convert.ToInt32(auth.USER_ID))
                {
                    await m_DBHelper.AddMessage(folder, list.id, list.text != null ? list.text : string.Empty, list.price != null ? list.price.ToString() : "0", list.canPurchaseReason == "opened" ? true : list.canPurchaseReason != "opened" ? false : (bool?)null ?? false, false, list.createdAt.HasValue ? list.createdAt.Value : DateTime.Now, list.fromUser != null && list.fromUser.id != null ? list.fromUser.id.Value : int.MinValue);
                    messageCollection.MessageObjects.Add(list);
                    if (list.canPurchaseReason != "opened" && list.media != null && list.media.Count > 0)
                    {
                        foreach (Messages.Medium medium in list.media)
                        {
                            if (medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
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
                                    await m_DBHelper.AddMedia(folder, medium.id, list.id, medium.files.full.url, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
                                    messageCollection.Messages.Add(medium.id, medium.files.full.url);
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
                    else if (messagePreviewIds.Count > 0)
                    {
                        foreach (Messages.Medium medium in list.media)
                        {
                            if (medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload") && messagePreviewIds.Contains(medium.id))
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
                                    await m_DBHelper.AddMedia(folder, medium.id, list.id, medium.files.full.url, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
                                    messageCollection.Messages.Add(medium.id, medium.files.full.url);
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

    public async Task<SinglePaidMessageCollection> GetPaidMessage(string endpoint, string folder, IDownloadConfig config)
    {
        Log.Debug($"Calling GetPaidMessage - {endpoint}");

        try
        {
            SingleMessage message = new();
            SinglePaidMessageCollection singlePaidMessageCollection = new();
            int post_limit = 50;
            Dictionary<string, string> getParams = new()
            {
                { "limit", post_limit.ToString() },
                { "order", "desc" }
            };

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
            message = JsonConvert.DeserializeObject<SingleMessage>(body, m_JsonSerializerSettings);

            if (!config.IgnoreOwnMessages || message.fromUser.id != Convert.ToInt32(auth.USER_ID))
            {
                await m_DBHelper.AddMessage(folder, message.id, message.text != null ? message.text : string.Empty, message.price != null ? message.price.ToString() : "0", true, false, message.createdAt.HasValue ? message.createdAt.Value : DateTime.Now, message.fromUser != null && message.fromUser.id != null ? message.fromUser.id.Value : int.MinValue);
                singlePaidMessageCollection.SingleMessageObjects.Add(message);
                List<long> messagePreviewIds = new();
                if (message.previews != null && message.previews.Count > 0)
                {
                    for (int i = 0; i < message.previews.Count; i++)
                    {
                        if (message.previews[i] is long previewId)
                        {
                            if (!messagePreviewIds.Contains(previewId))
                            {
                                messagePreviewIds.Add(previewId);
                            }
                        }
                    }
                }

                if (message.media != null && message.media.Count > 0)
                {
                    foreach (Messages.Medium medium in message.media)
                    {
                        if (!messagePreviewIds.Contains(medium.id) && medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
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

                            if (!singlePaidMessageCollection.SingleMessages.ContainsKey(medium.id))
                            {
                                await m_DBHelper.AddMedia(folder, medium.id, message.id, medium.files.full.url, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
                                singlePaidMessageCollection.SingleMessages.Add(medium.id, medium.files.full.url.ToString());
                                singlePaidMessageCollection.SingleMessageMedia.Add(medium);
                            }
                        }
                        else if (!messagePreviewIds.Contains(medium.id) && medium.canView && medium.files != null && medium.files.drm != null)
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

                            if (!singlePaidMessageCollection.SingleMessages.ContainsKey(medium.id))
                            {
                                await m_DBHelper.AddMedia(folder, medium.id, message.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), messagePreviewIds.Contains(medium.id) ? true : false, false, null);
                                singlePaidMessageCollection.SingleMessages.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{message.id}");
                                singlePaidMessageCollection.SingleMessageMedia.Add(medium);
                            }
                        }
                    }
                }
            }

            return singlePaidMessageCollection;
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


    public async Task<PaidMessageCollection> GetPaidMessages(string endpoint, string folder, string username, IDownloadConfig config)
    {
        Log.Debug($"Calling GetPaidMessages - {username}");

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

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
            paidMessages = JsonConvert.DeserializeObject<Purchased>(body, m_JsonSerializerSettings);
            if (paidMessages != null && paidMessages.hasMore)
            {
                getParams["offset"] = paidMessages.list.Count.ToString();
                while (true)
                {
                    string loopqueryParams = "?" + string.Join("&", getParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    Purchased newpaidMessages = new();
                    Dictionary<string, string> loopheaders = GetDynamicHeaders("/api2/v2" + endpoint, loopqueryParams);
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

            if (paidMessages.list != null && paidMessages.list.Count > 0)
            {
                foreach (Purchased.List purchase in paidMessages.list.Where(p => p.responseType == "message").OrderByDescending(p => p.postedAt ?? p.createdAt))
                {
                    if (!config.IgnoreOwnMessages || purchase.fromUser.id != Convert.ToInt32(auth.USER_ID))
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
                                    if (purchase.previews[i] is long previewId)
                                    {
                                        if (!previewids.Contains(previewId))
                                        {
                                            previewids.Add(previewId);
                                        }
                                    }
                                }
                            }
                            else if (purchase.preview != null)
                            {
                                for (int i = 0; i < purchase.preview.Count; i++)
                                {
                                    if (purchase.preview[i] is long previewId)
                                    {
                                        if (!previewids.Contains(previewId))
                                        {
                                            previewids.Add(previewId);
                                        }
                                    }
                                }
                            }

                            foreach (Messages.Medium medium in purchase.media)
                            {
                                if (previewids.Count > 0)
                                {
                                    bool has = previewids.Any(cus => cus.Equals(medium.id));
                                    if (!has && medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
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
                                            await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.full.url, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                            paidMessageCollection.PaidMessages.Add(medium.id, medium.files.full.url);
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
                                    if (medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
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
                                            await m_DBHelper.AddMedia(folder, medium.id, purchase.id, medium.files.full.url, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                            paidMessageCollection.PaidMessages.Add(medium.id, medium.files.full.url);
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

    public async Task<Dictionary<string, int>> GetPurchasedTabUsers(string endpoint, IDownloadConfig config, Dictionary<string, int> users)
    {
        Log.Debug($"Calling GetPurchasedTabUsers - {endpoint}");

        try
        {
            Dictionary<string, int> purchasedTabUsers = new();
            Purchased purchased = new();
            int post_limit = 50;
            Dictionary<string, string> getParams = new()
            {
                { "limit", post_limit.ToString() },
                { "order", "publish_date_desc" },
                { "format", "infinite" }
            };

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
            purchased = JsonConvert.DeserializeObject<Purchased>(body, m_JsonSerializerSettings);
            if (purchased != null && purchased.hasMore)
            {
                getParams["offset"] = purchased.list.Count.ToString();
                while (true)
                {
                    string loopqueryParams = "?" + string.Join("&", getParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    Purchased newPurchased = new();
                    Dictionary<string, string> loopheaders = GetDynamicHeaders("/api2/v2" + endpoint, loopqueryParams);
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
                        newPurchased = JsonConvert.DeserializeObject<Purchased>(loopbody, m_JsonSerializerSettings);
                    }
                    purchased.list.AddRange(newPurchased.list);
                    if (!newPurchased.hasMore)
                    {
                        break;
                    }
                    getParams["offset"] = Convert.ToString(Convert.ToInt32(getParams["offset"]) + post_limit);
                }
            }

            if (purchased.list != null && purchased.list.Count > 0)
            {
                foreach (Purchased.List purchase in purchased.list.OrderByDescending(p => p.postedAt ?? p.createdAt))
                {
                    if (purchase.fromUser != null)
                    {
                        if (users.Values.Contains(purchase.fromUser.id))
                        {
                            if (!string.IsNullOrEmpty(users.FirstOrDefault(x => x.Value == purchase.fromUser.id).Key))
                            {
                                if (!purchasedTabUsers.ContainsKey(users.FirstOrDefault(x => x.Value == purchase.fromUser.id).Key))
                                {
                                    purchasedTabUsers.Add(users.FirstOrDefault(x => x.Value == purchase.fromUser.id).Key, purchase.fromUser.id);
                                }
                            }
                            else
                            {
                                if (!purchasedTabUsers.ContainsKey($"Deleted User - {purchase.fromUser.id}"))
                                {
                                    purchasedTabUsers.Add($"Deleted User - {purchase.fromUser.id}", purchase.fromUser.id);
                                }
                            }
                        }
                        else
                        {
                            JObject user = await GetUserInfoById($"/users/list?x[]={purchase.fromUser.id}");

                            if(user is null)
                            {
                                if (!config.BypassContentForCreatorsWhoNoLongerExist)
                                {
                                    if(!purchasedTabUsers.ContainsKey($"Deleted User - {purchase.fromUser.id}"))
                                    {
                                        purchasedTabUsers.Add($"Deleted User - {purchase.fromUser.id}", purchase.fromUser.id);
                                    }
                                }
                                Log.Debug("Content creator not longer exists - {0}", purchase.fromUser.id);
                            }
                            else if (!string.IsNullOrEmpty(user[purchase.fromUser.id.ToString()]["username"].ToString()))
                            {
                                if (!purchasedTabUsers.ContainsKey(user[purchase.fromUser.id.ToString()]["username"].ToString()))
                                {
                                    purchasedTabUsers.Add(user[purchase.fromUser.id.ToString()]["username"].ToString(), purchase.fromUser.id);
                                }
                            }
                            else
                            {
                                if (!purchasedTabUsers.ContainsKey($"Deleted User - {purchase.fromUser.id}"))
                                {
                                    purchasedTabUsers.Add($"Deleted User - {purchase.fromUser.id}", purchase.fromUser.id);
                                }
                            }
                        }
                    }
                    else if (purchase.author != null)
                    {
                        if (users.Values.Contains(purchase.author.id))
                        {
                            if (!string.IsNullOrEmpty(users.FirstOrDefault(x => x.Value == purchase.author.id).Key))
                            {
                                if (!purchasedTabUsers.ContainsKey(users.FirstOrDefault(x => x.Value == purchase.author.id).Key) && users.ContainsKey(users.FirstOrDefault(x => x.Value == purchase.author.id).Key))
                                {
                                    purchasedTabUsers.Add(users.FirstOrDefault(x => x.Value == purchase.author.id).Key, purchase.author.id);
                                }
                            }
                            else
                            {
                                if (!purchasedTabUsers.ContainsKey($"Deleted User - {purchase.author.id}"))
                                {
                                    purchasedTabUsers.Add($"Deleted User - {purchase.author.id}", purchase.author.id);
                                }
                            }
                        }
                        else
                        {
                            JObject user = await GetUserInfoById($"/users/list?x[]={purchase.author.id}");

                            if (user is null)
                            {
                                if (!config.BypassContentForCreatorsWhoNoLongerExist)
                                {
                                    if(!purchasedTabUsers.ContainsKey($"Deleted User - {purchase.author.id}"))
                                    {
                                        purchasedTabUsers.Add($"Deleted User - {purchase.author.id}", purchase.author.id);
                                    }
                                }
                                Log.Debug("Content creator not longer exists - {0}", purchase.author.id);
                            }
                            else if (!string.IsNullOrEmpty(user[purchase.author.id.ToString()]["username"].ToString()))
                            {
                                if (!purchasedTabUsers.ContainsKey(user[purchase.author.id.ToString()]["username"].ToString()) && users.ContainsKey(user[purchase.author.id.ToString()]["username"].ToString()))
                                {
                                    purchasedTabUsers.Add(user[purchase.author.id.ToString()]["username"].ToString(), purchase.author.id);
                                }
                            }
                            else
                            {
                                if (!purchasedTabUsers.ContainsKey($"Deleted User - {purchase.author.id}"))
                                {
                                    purchasedTabUsers.Add($"Deleted User - {purchase.author.id}", purchase.author.id);
                                }
                            }
                        }
                    }
                }
            }

            return purchasedTabUsers;
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

    public async Task<List<PurchasedTabCollection>> GetPurchasedTab(string endpoint, string folder, IDownloadConfig config, Dictionary<string, int> users)
    {
        Log.Debug($"Calling GetPurchasedTab - {endpoint}");

        try
        {
            Dictionary<long, List<Purchased.List>> userPurchases = new Dictionary<long, List<Purchased.List>>();
            List<PurchasedTabCollection> purchasedTabCollections = new();
            Purchased purchased = new();
            int post_limit = 50;
            Dictionary<string, string> getParams = new()
            {
                { "limit", post_limit.ToString() },
                { "order", "publish_date_desc" },
                { "format", "infinite" }
            };

            var body = await BuildHeaderAndExecuteRequests(getParams, endpoint, GetHttpClient(config));
            purchased = JsonConvert.DeserializeObject<Purchased>(body, m_JsonSerializerSettings);
            if (purchased != null && purchased.hasMore)
            {
                getParams["offset"] = purchased.list.Count.ToString();
                while (true)
                {
                    string loopqueryParams = "?" + string.Join("&", getParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    Purchased newPurchased = new();
                    Dictionary<string, string> loopheaders = GetDynamicHeaders("/api2/v2" + endpoint, loopqueryParams);
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
                        newPurchased = JsonConvert.DeserializeObject<Purchased>(loopbody, m_JsonSerializerSettings);
                    }
                    purchased.list.AddRange(newPurchased.list);
                    if (!newPurchased.hasMore)
                    {
                        break;
                    }
                    getParams["offset"] = Convert.ToString(Convert.ToInt32(getParams["offset"]) + post_limit);
                }
            }

            if (purchased.list != null && purchased.list.Count > 0)
            {
                foreach (Purchased.List purchase in purchased.list.OrderByDescending(p => p.postedAt ?? p.createdAt))
                {
                    if (purchase.fromUser != null)
                    {
                        if (!userPurchases.ContainsKey(purchase.fromUser.id))
                        {
                            userPurchases.Add(purchase.fromUser.id, new List<Purchased.List>());
                        }
                        userPurchases[purchase.fromUser.id].Add(purchase);
                    }
                    else if (purchase.author != null)
                    {
                        if (!userPurchases.ContainsKey(purchase.author.id))
                        {
                            userPurchases.Add(purchase.author.id, new List<Purchased.List>());
                        }
                        userPurchases[purchase.author.id].Add(purchase);
                    }
                }
            }

            foreach (KeyValuePair<long, List<Purchased.List>> user in userPurchases)
            {
                PurchasedTabCollection purchasedTabCollection = new PurchasedTabCollection();
                JObject userObject = await GetUserInfoById($"/users/list?x[]={user.Key}");
                purchasedTabCollection.UserId = user.Key;
                purchasedTabCollection.Username = userObject is not null && !string.IsNullOrEmpty(userObject[user.Key.ToString()]["username"].ToString()) ? userObject[user.Key.ToString()]["username"].ToString() : $"Deleted User - {user.Key}";
                string path = System.IO.Path.Combine(folder, purchasedTabCollection.Username);
                if (Path.Exists(path))
                {
                    foreach (Purchased.List purchase in user.Value)
                    {
                        switch (purchase.responseType)
                        {
                            case "post":
                                List<long> previewids = new();
                                if (purchase.previews != null)
                                {
                                    for (int i = 0; i < purchase.previews.Count; i++)
                                    {
                                        if (purchase.previews[i] is long previewId)
                                        {
                                            if (!previewids.Contains(previewId))
                                            {
                                                previewids.Add(previewId);
                                            }
                                        }
                                    }
                                }
                                else if (purchase.preview != null)
                                {
                                    for (int i = 0; i < purchase.preview.Count; i++)
                                    {
                                        if (purchase.preview[i] is long previewId)
                                        {
                                            if (!previewids.Contains(previewId))
                                            {
                                                previewids.Add(previewId);
                                            }
                                        }
                                    }
                                }
                                await m_DBHelper.AddPost(path, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price.ToString() : "0", purchase.price != null && purchase.isOpened ? true : false, purchase.isArchived.HasValue ? purchase.isArchived.Value : false, purchase.createdAt != null ? purchase.createdAt.Value : purchase.postedAt.Value);
                                purchasedTabCollection.PaidPosts.PaidPostObjects.Add(purchase);
                                foreach (Messages.Medium medium in purchase.media)
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
                                    if (previewids.Count > 0)
                                    {
                                        bool has = previewids.Any(cus => cus.Equals(medium.id));
                                        if (!has && medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
                                        {

                                            if (!purchasedTabCollection.PaidPosts.PaidPosts.ContainsKey(medium.id))
                                            {
                                                await m_DBHelper.AddMedia(path, medium.id, purchase.id, medium.files.full.url, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                                purchasedTabCollection.PaidPosts.PaidPosts.Add(medium.id, medium.files.full.url);
                                                purchasedTabCollection.PaidPosts.PaidPostMedia.Add(medium);
                                            }
                                        }
                                        else if (!has && medium.canView && medium.files != null && medium.files.drm != null)
                                        {

                                            if (!purchasedTabCollection.PaidPosts.PaidPosts.ContainsKey(medium.id))
                                            {
                                                await m_DBHelper.AddMedia(path, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                                purchasedTabCollection.PaidPosts.PaidPosts.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                                purchasedTabCollection.PaidPosts.PaidPostMedia.Add(medium);
                                            }

                                        }
                                    }
                                    else
                                    {
                                        if (medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
                                        {
                                            if (!purchasedTabCollection.PaidPosts.PaidPosts.ContainsKey(medium.id))
                                            {
                                                await m_DBHelper.AddMedia(path, medium.id, purchase.id, medium.files.full.url, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                                purchasedTabCollection.PaidPosts.PaidPosts.Add(medium.id, medium.files.full.url);
                                                purchasedTabCollection.PaidPosts.PaidPostMedia.Add(medium);
                                            }
                                        }
                                        else if (medium.canView && medium.files != null && medium.files.drm != null)
                                        {
                                            if (!purchasedTabCollection.PaidPosts.PaidPosts.ContainsKey(medium.id))
                                            {
                                                await m_DBHelper.AddMedia(path, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Posts", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), previewids.Contains(medium.id) ? true : false, false, null);
                                                purchasedTabCollection.PaidPosts.PaidPosts.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                                purchasedTabCollection.PaidPosts.PaidPostMedia.Add(medium);
                                            }
                                        }
                                    }
                                }
                                break;
                            case "message":
                                if (purchase.postedAt != null)
                                {
                                    await m_DBHelper.AddMessage(path, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price : "0", true, false, purchase.postedAt.Value, purchase.fromUser.id);
                                }
                                else
                                {
                                    await m_DBHelper.AddMessage(path, purchase.id, purchase.text != null ? purchase.text : string.Empty, purchase.price != null ? purchase.price : "0", true, false, purchase.createdAt.Value, purchase.fromUser.id);
                                }
                                purchasedTabCollection.PaidMessages.PaidMessageObjects.Add(purchase);
                                if (purchase.media != null && purchase.media.Count > 0)
                                {
                                    List<long> paidMessagePreviewids = new();
                                    if (purchase.previews != null)
                                    {
                                        for (int i = 0; i < purchase.previews.Count; i++)
                                        {
                                            if (purchase.previews[i] is long previewId)
                                            {
                                                if (!paidMessagePreviewids.Contains(previewId))
                                                {
                                                    paidMessagePreviewids.Add(previewId);
                                                }
                                            }
                                        }
                                    }
                                    else if (purchase.preview != null)
                                    {
                                        for (int i = 0; i < purchase.preview.Count; i++)
                                        {
                                            if (purchase.preview[i] is long previewId)
                                            {
                                                if (!paidMessagePreviewids.Contains(previewId))
                                                {
                                                    paidMessagePreviewids.Add(previewId);
                                                }
                                            }
                                        }
                                    }

                                    foreach (Messages.Medium medium in purchase.media)
                                    {
                                        if (paidMessagePreviewids.Count > 0)
                                        {
                                            bool has = paidMessagePreviewids.Any(cus => cus.Equals(medium.id));
                                            if (!has && medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
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
                                                if (!purchasedTabCollection.PaidMessages.PaidMessages.ContainsKey(medium.id))
                                                {
                                                    await m_DBHelper.AddMedia(path, medium.id, purchase.id, medium.files.full.url, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), paidMessagePreviewids.Contains(medium.id) ? true : false, false, null);
                                                    purchasedTabCollection.PaidMessages.PaidMessages.Add(medium.id, medium.files.full.url);
                                                    purchasedTabCollection.PaidMessages.PaidMessageMedia.Add(medium);
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
                                                if (!purchasedTabCollection.PaidMessages.PaidMessages.ContainsKey(medium.id))
                                                {
                                                    await m_DBHelper.AddMedia(path, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), paidMessagePreviewids.Contains(medium.id) ? true : false, false, null);
                                                    purchasedTabCollection.PaidMessages.PaidMessages.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                                    purchasedTabCollection.PaidMessages.PaidMessageMedia.Add(medium);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (medium.canView && medium.files != null && medium.files.full != null && !string.IsNullOrEmpty(medium.files.full.url) && !medium.files.full.url.Contains("upload"))
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
                                                if (!purchasedTabCollection.PaidMessages.PaidMessages.ContainsKey(medium.id))
                                                {
                                                    await m_DBHelper.AddMedia(path, medium.id, purchase.id, medium.files.full.url, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), paidMessagePreviewids.Contains(medium.id) ? true : false, false, null);
                                                    purchasedTabCollection.PaidMessages.PaidMessages.Add(medium.id, medium.files.full.url);
                                                    purchasedTabCollection.PaidMessages.PaidMessageMedia.Add(medium);
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
                                                if (!purchasedTabCollection.PaidMessages.PaidMessages.ContainsKey(medium.id))
                                                {
                                                    await m_DBHelper.AddMedia(path, medium.id, purchase.id, medium.files.drm.manifest.dash, null, null, null, "Messages", medium.type == "photo" ? "Images" : (medium.type == "video" || medium.type == "gif" ? "Videos" : (medium.type == "audio" ? "Audios" : null)), paidMessagePreviewids.Contains(medium.id) ? true : false, false, null);
                                                    purchasedTabCollection.PaidMessages.PaidMessages.Add(medium.id, $"{medium.files.drm.manifest.dash},{medium.files.drm.signature.dash.CloudFrontPolicy},{medium.files.drm.signature.dash.CloudFrontSignature},{medium.files.drm.signature.dash.CloudFrontKeyPairId},{medium.id},{purchase.id}");
                                                    purchasedTabCollection.PaidMessages.PaidMessageMedia.Add(medium);
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                    }
                    purchasedTabCollections.Add(purchasedTabCollection);
                }
            }
            return purchasedTabCollections;
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


    public async Task<string> GetDRMMPDPSSH(string mpdUrl, string policy, string signature, string kvp)
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


    public async Task<DateTime> GetDRMMPDLastModified(string mpdUrl, string policy, string signature, string kvp)
    {
        Log.Debug("Calling GetDRMMPDLastModified");
        Log.Debug($"mpdUrl: {mpdUrl}");
        Log.Debug($"policy: {policy}");
        Log.Debug($"signature: {signature}");
        Log.Debug($"kvp: {kvp}");

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

                Log.Debug($"Last modified: {lastmodified}");
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


    public async Task<string> GetDecryptionKey(Dictionary<string, string> drmHeaders, string licenceURL, string pssh)
    {
        Log.Debug("Calling GetDecryptionKey");

        const int maxAttempts = 30;
        const int delayBetweenAttempts = 3000;
        int attempt = 0;

        try
        {
            string dcValue = string.Empty;
            HttpClient client = new();

            CDRMProjectRequest cdrmProjectRequest = new CDRMProjectRequest
            {
                PSSH = pssh,
                LicenseURL = licenceURL,
                Headers = JsonConvert.SerializeObject(drmHeaders),
                Cookies = "",
                Data = "",
            };

            string json = JsonConvert.SerializeObject(cdrmProjectRequest);

            Log.Debug($"Posting to CDRM Project: {json}");

            while (attempt < maxAttempts)
            {
                attempt++;

                HttpRequestMessage request = new(HttpMethod.Post, "https://cdrm-project.com/api/decrypt")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                using var response = await client.SendAsync(request);

                Log.Debug($"CDRM Project Response (Attempt {attempt}): {response.Content.ReadAsStringAsync().Result}");

                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();

                if (!body.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    var doc = JsonDocument.Parse(body);
                    dcValue = doc.RootElement.GetProperty("message").GetString().Trim();
                    return dcValue;
                }
                else
                {
                    Log.Debug($"Response contains 'error'. Retrying... Attempt {attempt} of {maxAttempts}");
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(delayBetweenAttempts); 
                    }
                }
            }

            throw new Exception("Maximum retry attempts reached. Unable to get a valid decryption key.");
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

    public async Task<string> GetDecryptionKeyNew(Dictionary<string, string> drmHeaders, string licenceURL, string pssh)
    {
        Log.Debug("Calling GetDecryptionKeyNew");

        try
        {
            var resp1 = PostData(licenceURL, drmHeaders, new byte[] { 0x08, 0x04 });
            var certDataB64 = Convert.ToBase64String(resp1);
            var cdm = new CDMApi();
            var challenge = cdm.GetChallenge(pssh, certDataB64, false, false);
            var resp2 = PostData(licenceURL, drmHeaders, challenge);
            var licenseB64 = Convert.ToBase64String(resp2);
            Log.Debug($"resp1: {resp1}");
            Log.Debug($"certDataB64: {certDataB64}");
            Log.Debug($"challenge: {challenge}");
            Log.Debug($"resp2: {resp2}");
            Log.Debug($"licenseB64: {licenseB64}");
            cdm.ProvideLicense(licenseB64);
            List<ContentKey> keys = cdm.GetKeys();
            if (keys.Count > 0)
            {
                Log.Debug($"GetDecryptionKeyNew Key: {keys[0].ToString()}");
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

    public static string? GetDynamicRules()
    {
        Log.Debug("Calling GetDynamicRules");
        try
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://raw.githubusercontent.com/deviint/onlyfans-dynamic-rules/main/dynamicRules.json");
            using var response = client.Send(request);

            if (!response.IsSuccessStatusCode)
            {
                Log.Debug("GetDynamicRules did not return a Success Status Code");
                return null;
            }

            var body = response.Content.ReadAsStringAsync().Result;

            Log.Debug("GetDynamicRules Response: ");
            Log.Debug(body);

            return body;
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
