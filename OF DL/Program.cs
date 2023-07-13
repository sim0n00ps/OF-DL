using Newtonsoft.Json;
using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Highlights;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Stories;
using OF_DL.Enumurations;
using OF_DL.Helpers;
using Org.BouncyCastle.Math.EC.Rfc7748;
using Spectre.Console;
using System.Text.RegularExpressions;
using static OF_DL.Entities.Lists.UserList;

namespace OF_DL
{
	public class Program
	{
        public static Auth auth { get; set; } = JsonConvert.DeserializeObject<Auth>(File.ReadAllText("auth.json"));
        public int MAX_AGE = 0;
        public static List<long> paid_post_ids = new List<long>();
        private static IAPIHelper apiHelper;
        private static IDBHelper dBHelper;
        private static IDownloadHelper downloadHelper;
        static Program()
		{
            apiHelper = new APIHelper();
            dBHelper = new DBHelper();
            downloadHelper = new DownloadHelper();
        }
		public async static Task Main()
		{
			try
			{
				AnsiConsole.Write(new FigletText("Welcome to OF-DL").Color(Color.Red));

                if (ValidateFilePath(auth.YTDLP_PATH))
                {
                    if (!File.Exists(auth.YTDLP_PATH))
                    {
                        AnsiConsole.Markup($"[red]Cannot locate yt-dlp.exe with specified path {auth.YTDLP_PATH}, please modify auth.json with the correct path, press any key to exit[/]");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                    else
                    {
                        AnsiConsole.Markup($"[green]yt-dlp.exe located successfully![/]\n");
                    }
                }
                else
                {
                    AnsiConsole.Markup(@$"[red]Specified path {auth.YTDLP_PATH} does not match the required format, please remove any \ from the path and replace them with / and make sure the path does not have a / at the end, press any key to exit[/]");
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                if (ValidateFilePath(auth.FFMPEG_PATH))
                {
                    if (!File.Exists(auth.FFMPEG_PATH))
                    {
                        AnsiConsole.Markup($"[red]Cannot locate ffmpeg.exe with specified path {auth.FFMPEG_PATH}, please modify auth.json with the correct path, press any key to exit[/]");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                    else
                    {
                        AnsiConsole.Markup($"[green]ffmpeg.exe located successfully![/]\n");
                    }
                }
                else
                {
                    AnsiConsole.Markup(@$"[red]Specified path {auth.FFMPEG_PATH} does not match the required format, please remove any \ from the path and replace them with / and make sure the path does not have a / at the end, press any key to exit[/]");
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                if (ValidateFilePath(auth.MP4DECRYPT_PATH))
                {
                    if (!File.Exists(auth.MP4DECRYPT_PATH))
                    {
                        AnsiConsole.Markup($"[red]Cannot locate mp4decrypt.exe with specified path {auth.MP4DECRYPT_PATH}, please modify auth.json with the correct path, press any key to exit[/]");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                    else
                    {
                        AnsiConsole.Markup($"[green]mp4decrypt.exe located successfully![/]\n");
                    }
                }
                else
                {
                    AnsiConsole.Markup(@$"[red]Specified path {auth.MP4DECRYPT_PATH} does not match the required format, please remove any \ from the path and replace them with / and make sure the path does not have a / at the end, press any key to exit[/]");
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                //Check if auth is valid
                Entities.User validate = await apiHelper.GetUserInfo($"/users/me", auth);
                if (validate.name == null && validate.username == null)
                {
                    AnsiConsole.Markup($"[red]Auth failed, please check the values in auth.json are correct, press any key to exit[/]");
                    Console.ReadKey();
                }
                else
                {
                    AnsiConsole.Markup($"[green]Logged In successfully as {validate.name} {validate.username}\n[/]");
                    do
                    {
                        DateTime startTime = DateTime.Now;
                        Dictionary<string, int> users = await apiHelper.GetSubscriptions("/subscriptions/subscribes", auth.IncludeExpiredSubscriptions, auth);
                        Dictionary<string, int> lists = await apiHelper.GetLists("/lists", auth);
                        Dictionary<string, int> selectedUsers = new Dictionary<string, int>();
                        // Call the HandleUserSelection method to handle user selection and processing
                        KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP = await HandleUserSelection(selectedUsers, users, lists);

                        if (hasSelectedUsersKVP.Key && !hasSelectedUsersKVP.Value.ContainsKey("AuthChanged"))
                        {
                            //Iterate over each user in the list of users
                            foreach (KeyValuePair<string, int> user in hasSelectedUsersKVP.Value)
                            {
                                int paidPostCount = 0;
                                int postCount = 0;
                                int archivedCount = 0;
                                int storiesCount = 0;
                                int highlightsCount = 0;
                                int messagesCount = 0;
                                int paidMessagesCount = 0;
                                AnsiConsole.Markup($"[red]\nScraping Data for {user.Key}\n[/]");

                                string path = "";
                                if (!string.IsNullOrEmpty(auth.DownloadPath))
                                {
                                    path = System.IO.Path.Combine(auth.DownloadPath, user.Key);
                                }
                                else
                                {
                                    path = $"__user_data__/sites/OnlyFans/{user.Key}"; // specify the path for the new folder
                                }

                                if (!Directory.Exists(path)) // check if the folder already exists
                                {
                                    Directory.CreateDirectory(path); // create the new folder
                                    AnsiConsole.Markup($"[red]Created folder for {user.Key}\n[/]");
                                }
                                else
                                {
                                    AnsiConsole.Markup($"[red]Folder for {user.Key} already created\n[/]");
                                }

                                Entities.User user_info = await apiHelper.GetUserInfo($"/users/{user.Key}", auth);

                                await dBHelper.CreateDB(path);

                                if (auth.DownloadAvatarHeaderPhoto)
                                {
                                    await downloadHelper.DownloadAvatarHeader(user_info.avatar, user_info.header, path);
                                }

                                if (auth.DownloadPaidPosts)
                                {
                                    AnsiConsole.Markup($"[red]Getting Paid Posts\n[/]");
                                    Dictionary<long, string> purchasedPosts = await apiHelper.GetMedia(MediaType.PaidPosts, "/posts/paid", user.Key, path, auth, paid_post_ids);

                                    int oldPaidPostCount = 0;
                                    int newPaidPostCount = 0;
                                    if (purchasedPosts != null && purchasedPosts.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {purchasedPosts.Count} Paid Posts\n[/]");
                                        paidPostCount = purchasedPosts.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(purchasedPosts.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            // Define tasks
                                            var task = ctx.AddTask($"[red]Downloading {purchasedPosts.Count} Paid Posts[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> purchasedPostKVP in purchasedPosts)
                                            {
                                                bool isNew;
                                                if (purchasedPostKVP.Value.Contains("cdn3.onlyfans.com/dash/files"))
                                                {
                                                    string[] messageUrlParsed = purchasedPostKVP.Value.Split(',');
                                                    string mpdURL = messageUrlParsed[0];
                                                    string policy = messageUrlParsed[1];
                                                    string signature = messageUrlParsed[2];
                                                    string kvp = messageUrlParsed[3];
                                                    string mediaId = messageUrlParsed[4];
                                                    string postId = messageUrlParsed[5];
                                                    string? licenseURL = null;
                                                    string? pssh = await apiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp, auth);
                                                    if (pssh != null)
                                                    {
                                                        DateTime lastModified = await apiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp, auth);
                                                        Dictionary<string, string> drmHeaders = await apiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine", auth);
                                                        string decryptionKey = await apiHelper.GetDecryptionKeyNew(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh, auth);
                                                        isNew = await downloadHelper.DownloadPurchasedPostDRMVideo(auth.YTDLP_PATH, auth.MP4DECRYPT_PATH, auth.FFMPEG_PATH, auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, mpdURL, decryptionKey, path, lastModified, purchasedPostKVP.Key, task);
                                                        if (isNew)
                                                        {
                                                            newPaidPostCount++;
                                                        }
                                                        else
                                                        {
                                                            oldPaidPostCount++;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    isNew = await downloadHelper.DownloadPurchasedPostMedia(purchasedPostKVP.Value, path, purchasedPostKVP.Key, task);
                                                    if (isNew)
                                                    {
                                                        newPaidPostCount++;
                                                    }
                                                    else
                                                    {
                                                        oldPaidPostCount++;
                                                    }
                                                }
                                                task.Increment(1.0);
                                            }
                                            task.StopTask();
                                        });
                                        AnsiConsole.Markup($"[red]Paid Posts Already Downloaded: {oldPaidPostCount} New Paid Posts Downloaded: {newPaidPostCount}[/]\n");
                                    }
                                    else
                                    {
                                        AnsiConsole.Markup($"[red]Found 0 Paid Posts\n[/]");
                                    }
                                }

                                if (auth.DownloadPosts)
                                {
                                    AnsiConsole.Markup($"[red]Getting Posts\n[/]");
                                    Dictionary<long, string> posts = await apiHelper.GetMedia(MediaType.Posts, $"/users/{user.Value}/posts", null, path, auth, paid_post_ids);
                                    int oldPostCount = 0;
                                    int newPostCount = 0;
                                    if (posts != null && posts.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {posts.Count} Posts\n[/]");
                                        postCount = posts.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(posts.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            var task = ctx.AddTask($"[red]Downloading {posts.Count} Posts[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> postKVP in posts)
                                            {
                                                bool isNew;
                                                if (postKVP.Value.Contains("cdn3.onlyfans.com/dash/files"))
                                                {
                                                    string[] messageUrlParsed = postKVP.Value.Split(',');
                                                    string mpdURL = messageUrlParsed[0];
                                                    string policy = messageUrlParsed[1];
                                                    string signature = messageUrlParsed[2];
                                                    string kvp = messageUrlParsed[3];
                                                    string mediaId = messageUrlParsed[4];
                                                    string postId = messageUrlParsed[5];
                                                    string? licenseURL = null;
                                                    string? pssh = await apiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp, auth);
                                                    if (pssh != null)
                                                    {
                                                        DateTime lastModified = await apiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp, auth);
                                                        Dictionary<string, string> drmHeaders = await apiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine", auth);
                                                        string decryptionKey = await apiHelper.GetDecryptionKeyNew(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh, auth);
                                                        isNew = await downloadHelper.DownloadPostDRMVideo(auth.YTDLP_PATH, auth.MP4DECRYPT_PATH, auth.FFMPEG_PATH, auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, mpdURL, decryptionKey, path, lastModified, postKVP.Key, task);
                                                        if (isNew)
                                                        {
                                                            newPostCount++;
                                                        }
                                                        else
                                                        {
                                                            oldPostCount++;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    isNew = await downloadHelper.DownloadPostMedia(postKVP.Value, path, postKVP.Key, task);
                                                    if (isNew)
                                                    {
                                                        newPostCount++;
                                                    }
                                                    else
                                                    {
                                                        oldPostCount++;
                                                    }
                                                }
                                            }
                                            task.StopTask();
                                        });
                                        AnsiConsole.Markup($"[red]Posts Already Downloaded: {oldPostCount} New Posts Downloaded: {newPostCount}[/]\n");
                                    }
                                    else
                                    {
                                        AnsiConsole.Markup($"[red]Found 0 Posts\n[/]");
                                    }
                                }

                                if (auth.DownloadArchived)
                                {
                                    AnsiConsole.Markup($"[red]Getting Archived Posts\n[/]");
                                    Dictionary<long, string> archived = await apiHelper.GetMedia(MediaType.Archived, $"/users/{user.Value}/posts", null, path, auth, paid_post_ids);

                                    int oldArchivedCount = 0;
                                    int newArchivedCount = 0;
                                    if (archived != null && archived.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {archived.Count} Archived Posts\n[/]");
                                        archivedCount = archived.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(archived.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            // Define tasks
                                            var task = ctx.AddTask($"[red]Downloading {archived.Count} Archived Posts[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> archivedKVP in archived)
                                            {
                                                bool isNew = await downloadHelper.DownloadArchivedMedia(archivedKVP.Value, path, archivedKVP.Key, task);
                                                task.Increment(1.0);
                                                if (isNew)
                                                {
                                                    newArchivedCount++;
                                                }
                                                else
                                                {
                                                    oldArchivedCount++;
                                                }
                                            }
                                            task.StopTask();
                                        });
                                        AnsiConsole.Markup($"[red]Archived Posts Already Downloaded: {oldArchivedCount} New Archived Posts Downloaded: {newArchivedCount}[/]\n");
                                    }
                                    else
                                    {
                                        AnsiConsole.Markup($"[red]Found 0 Archived Posts\n[/]");
                                    }
                                }

                                if (auth.DownloadStories)
                                {
                                    AnsiConsole.Markup($"[red]Getting Stories\n[/]");
                                    Dictionary<long, string> stories = await apiHelper.GetMedia(MediaType.Stories, $"/users/{user.Value}/stories", null, path, auth, paid_post_ids);
                                    int oldStoriesCount = 0;
                                    int newStoriesCount = 0;
                                    if (stories != null && stories.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {stories.Count} Stories\n[/]");
                                        storiesCount = stories.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(stories.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            // Define tasks
                                            var task = ctx.AddTask($"[red]Downloading {stories.Count} Stories[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> storyKVP in stories)
                                            {
                                                bool isNew = await downloadHelper.DownloadStoryMedia(storyKVP.Value, path, storyKVP.Key, task);
                                                task.Increment(1.0);
                                                if (isNew)
                                                {
                                                    newStoriesCount++;
                                                }
                                                else
                                                {
                                                    oldStoriesCount++;
                                                }
                                            }
                                            task.StopTask();
                                        });
                                        AnsiConsole.Markup($"[red]Stories Already Downloaded: {oldStoriesCount} New Stories Downloaded: {newStoriesCount}[/]\n");
                                    }
                                    else
                                    {
                                        AnsiConsole.Markup($"[red]Found 0 Stories\n[/]");
                                    }
                                }

                                if (auth.DownloadHighlights)
                                {
                                    AnsiConsole.Markup($"[red]Getting Highlights\n[/]");
                                    Dictionary<long, string> highlights = await apiHelper.GetMedia(MediaType.Highlights, $"/users/{user.Value}/stories/highlights", null, path, auth, paid_post_ids);
                                    int oldHighlightsCount = 0;
                                    int newHighlightsCount = 0;
                                    if (highlights != null && highlights.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {highlights.Count} Highlights\n[/]");
                                        highlightsCount = highlights.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(highlights.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            // Define tasks
                                            var task = ctx.AddTask($"[red]Downloading {highlights.Count} Highlights[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> highlightKVP in highlights)
                                            {
                                                bool isNew = await downloadHelper.DownloadStoryMedia(highlightKVP.Value, path, highlightKVP.Key, task);
                                                task.Increment(1.0);
                                                if (isNew)
                                                {
                                                    newHighlightsCount++;
                                                }
                                                else
                                                {
                                                    oldHighlightsCount++;
                                                }
                                            }
                                            task.StopTask();
                                        });
                                        AnsiConsole.Markup($"[red]Highlights Already Downloaded: {oldHighlightsCount} New Highlights Downloaded: {newHighlightsCount}[/]\n");
                                    }
                                    else
                                    {
                                        AnsiConsole.Markup($"[red]Found 0 Highlights\n[/]");
                                    }
                                }

                                if (auth.DownloadMessages)
                                {
                                    AnsiConsole.Markup($"[red]Getting Messages\n[/]");
                                    Dictionary<long, string> messages = await apiHelper.GetMedia(MediaType.Messages, $"/chats/{user.Value}/messages", null, path, auth, paid_post_ids);
                                    int oldMessagesCount = 0;
                                    int newMessagesCount = 0;
                                    if (messages != null && messages.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {messages.Count} Messages\n[/]");
                                        messagesCount = messages.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(messages.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            // Define tasks
                                            var task = ctx.AddTask($"[red]Downloading {messages.Count} Messages[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> messageKVP in messages)
                                            {
                                                bool isNew;
                                                if (messageKVP.Value.Contains("cdn3.onlyfans.com/dash/files"))
                                                {
                                                    string[] messageUrlParsed = messageKVP.Value.Split(',');
                                                    string mpdURL = messageUrlParsed[0];
                                                    string policy = messageUrlParsed[1];
                                                    string signature = messageUrlParsed[2];
                                                    string kvp = messageUrlParsed[3];
                                                    string mediaId = messageUrlParsed[4];
                                                    string messageId = messageUrlParsed[5];
                                                    string? licenseURL = null;
                                                    string? pssh = await apiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp, auth);
                                                    if (pssh != null)
                                                    {
                                                        DateTime lastModified = await apiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp, auth);
                                                        Dictionary<string, string> drmHeaders = await apiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/message/{messageId}", "?type=widevine", auth);
                                                        string decryptionKey = await apiHelper.GetDecryptionKeyNew(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh, auth);
                                                        isNew = await downloadHelper.DownloadMessageDRMVideo(auth.YTDLP_PATH, auth.MP4DECRYPT_PATH, auth.FFMPEG_PATH, auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, mpdURL, decryptionKey, path, lastModified, messageKVP.Key, task);
                                                        if (isNew)
                                                        {
                                                            newMessagesCount++;
                                                        }
                                                        else
                                                        {
                                                            oldMessagesCount++;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    isNew = await downloadHelper.DownloadMessageMedia(messageKVP.Value, path, messageKVP.Key, task);
                                                    if (isNew)
                                                    {
                                                        newMessagesCount++;
                                                    }
                                                    else
                                                    {
                                                        oldMessagesCount++;
                                                    }
                                                }
                                                task.Increment(1.0);
                                            }
                                            task.StopTask();
                                        });
                                        AnsiConsole.Markup($"[red]Messages Already Downloaded: {oldMessagesCount} New Messages Downloaded: {newMessagesCount}[/]\n");
                                    }
                                    else
                                    {
                                        AnsiConsole.Markup($"[red]Found 0 Messages\n[/]");
                                    }
                                }

                                if (auth.DownloadPaidMessages)
                                {
                                    AnsiConsole.Markup($"[red]Getting Paid Messages\n[/]");
                                    Dictionary<long, string> purchased = await apiHelper.GetMedia(MediaType.PaidMessages, "/posts/paid", user.Key, path, auth, paid_post_ids);

                                    int oldPaidMessagesCount = 0;
                                    int newPaidMessagesCount = 0;
                                    if (purchased != null && purchased.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {purchased.Count} Paid Messages\n[/]");
                                        paidMessagesCount = purchased.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(purchased.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            // Define tasks
                                            var task = ctx.AddTask($"[red]Downloading {purchased.Count} Paid Messages[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> paidMessageKVP in purchased)
                                            {
                                                bool isNew;
                                                if (paidMessageKVP.Value.Contains("cdn3.onlyfans.com/dash/files"))
                                                {
                                                    string[] messageUrlParsed = paidMessageKVP.Value.Split(',');
                                                    string mpdURL = messageUrlParsed[0];
                                                    string policy = messageUrlParsed[1];
                                                    string signature = messageUrlParsed[2];
                                                    string kvp = messageUrlParsed[3];
                                                    string mediaId = messageUrlParsed[4];
                                                    string messageId = messageUrlParsed[5];
                                                    string? licenseURL = null;
                                                    string? pssh = await apiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp, auth);
                                                    if (pssh != null)
                                                    {
                                                        DateTime lastModified = await apiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp, auth);
                                                        Dictionary<string, string> drmHeaders = await apiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/message/{messageId}", "?type=widevine", auth);
                                                        string decryptionKey = await apiHelper.GetDecryptionKeyNew(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh, auth);
                                                        isNew = await downloadHelper.DownloadPurchasedMessageDRMVideo(auth.YTDLP_PATH, auth.MP4DECRYPT_PATH, auth.FFMPEG_PATH, auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, mpdURL, decryptionKey, path, lastModified, paidMessageKVP.Key, task);
                                                        if (isNew)
                                                        {
                                                            newPaidMessagesCount++;
                                                        }
                                                        else
                                                        {
                                                            oldPaidMessagesCount++;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    isNew = await downloadHelper.DownloadPurchasedMedia(paidMessageKVP.Value, path, paidMessageKVP.Key, task);
                                                    if (isNew)
                                                    {
                                                        newPaidMessagesCount++;
                                                    }
                                                    else
                                                    {
                                                        oldPaidMessagesCount++;
                                                    }
                                                }
                                                task.Increment(1.0);
                                            }
                                            task.StopTask();
                                        });
                                        AnsiConsole.Markup($"[red]Paid Messages Already Downloaded: {oldPaidMessagesCount} New Paid Messages Downloaded: {newPaidMessagesCount}[/]\n");
                                    }
                                    else
                                    {
                                        AnsiConsole.Markup($"[red]Found 0 Paid Messages\n[/]");
                                    }
                                }

                                AnsiConsole.Markup("\n");
                                AnsiConsole.Write(new BreakdownChart()
                                .FullSize()
                                .AddItem("Paid Posts", paidPostCount, Color.Red)
                                .AddItem("Posts", postCount, Color.Blue)
                                .AddItem("Archived", archivedCount, Color.Green)
                                .AddItem("Stories", storiesCount, Color.Yellow)
                                .AddItem("Highlights", highlightsCount, Color.Orange1)
                                .AddItem("Messages", messagesCount, Color.LightGreen)
                                .AddItem("Paid Messages", paidMessagesCount, Color.Aqua));
                                AnsiConsole.Markup("\n");
                            }
                            DateTime endTime = DateTime.Now;
                            TimeSpan totalTime = endTime - startTime;
                            AnsiConsole.Markup($"[green]Scrape Completed in {totalTime.TotalMinutes.ToString("0.00")} minutes\n[/]");
                        }
                        else if (hasSelectedUsersKVP.Key && hasSelectedUsersKVP.Value != null ? hasSelectedUsersKVP.Value.ContainsKey("AuthChanged") : false)
                        {
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    } while (true);
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
		}

        public static async Task<KeyValuePair<bool, Dictionary<string, int>>> HandleUserSelection(Dictionary<string, int> selectedUsers, Dictionary<string, int> users, Dictionary<string, int> lists)
        {
            bool hasSelectedUsers = false;

            while (!hasSelectedUsers)
            {
                var mainMenuOptions = GetMainMenuOptions(users, lists);

                var mainMenuSelection = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[red]Select Accounts to Scrape | Select All = All Accounts | List = Download content from users on List | Custom = Specific Account(s)[/]")
                        .AddChoices(mainMenuOptions)
                );
				
                switch (mainMenuSelection)
                {
                    case "[red]Select All[/]":
                        selectedUsers = users;
                        hasSelectedUsers = true;
                        break;
                    case "[red]List[/]":
                        while (true)
                        {
                            var listSelectionPrompt = new MultiSelectionPrompt<string>();
                            listSelectionPrompt.Title = "[red]Select List[/]";
                            listSelectionPrompt.PageSize = 10;
                            listSelectionPrompt.AddChoice("[red]Go Back[/]");
                            foreach (string key in lists.Keys.Select(k => $"[red]{k}[/]").ToList())
                            {
                                listSelectionPrompt.AddChoice(key);
                            }
                            var listSelection = AnsiConsole.Prompt(listSelectionPrompt);

                            if (listSelection.Contains("[red]Go Back[/]"))
                            {
                                break; // Go back to the main menu
                            }
                            else
                            {
                                hasSelectedUsers = true;
                                List<string> listUsernames = new List<string>();
                                foreach (var item in listSelection)
                                {
                                    int listId = lists[item.Replace("[red]", "").Replace("[/]", "")];
                                    List<string> usernames = await apiHelper.GetListUsers($"/lists/{listId}/users", auth);
                                    foreach (string user in usernames)
                                    {
                                        listUsernames.Add(user);
                                    }
                                }
                                selectedUsers = users.Where(x => listUsernames.Contains($"{x.Key}")).Distinct().ToDictionary(x => x.Key, x => x.Value);
                                AnsiConsole.Markup(string.Format("[red]Downloading from List(s): {0}[/]", string.Join(", ", listSelection)));
                                break;
                            }
                        }
                        break;
                    case "[red]Custom[/]":
                        while (true)
                        {
                            var selectedNamesPrompt = new MultiSelectionPrompt<string>();
                            selectedNamesPrompt.MoreChoicesText("[grey](Move up and down to reveal more choices)[/]");
                            selectedNamesPrompt.InstructionsText("[grey](Press <space> to select, <enter> to accept)[/]\n[grey](Press A-Z to easily navigate the list)[/]");
                            selectedNamesPrompt.Title("[red]Select users[/]");
                            selectedNamesPrompt.PageSize(10);
                            selectedNamesPrompt.AddChoice("[red]Go Back[/]");
                            foreach (string key in users.Keys.Select(k => $"[red]{k}[/]").ToList())
                            {
                                selectedNamesPrompt.AddChoice(key);
                            }
                            var userSelection = AnsiConsole.Prompt(selectedNamesPrompt);
                            if (userSelection.Contains("[red]Go Back[/]"))
                            {
                                break; // Go back to the main menu
                            }
                            else
                            {
                                hasSelectedUsers = true;
                                selectedUsers = users.Where(x => userSelection.Contains($"[red]{x.Key}[/]")).ToDictionary(x => x.Key, x => x.Value);
                                break;
                            }
                        }
                        break;
                    case "[red]Edit Auth.json[/]":
                        while (true)
                        {
                            var choices = new List<(string choice, bool isSelected)>();
                            choices.AddRange(new []
                            {
                                ( "[red]Go Back[/]", false ),
                                ( "[red]DownloadAvatarHeaderPhoto[/]", auth.DownloadAvatarHeaderPhoto),
                                ( "[red]DownloadPaidPosts[/]", auth.DownloadPaidPosts ),
                                ( "[red]DownloadPosts[/]", auth.DownloadPosts ),
                                ( "[red]DownloadArchived[/]", auth.DownloadArchived ),
                                ( "[red]DownloadStories[/]", auth.DownloadStories ),
                                ( "[red]DownloadHighlights[/]", auth.DownloadHighlights ),
                                ( "[red]DownloadMessages[/]", auth.DownloadMessages ),
                                ( "[red]DownloadPaidMessages[/]", auth.DownloadPaidMessages ),
                                ( "[red]DownloadImages[/]", auth.DownloadImages ),
                                ( "[red]DownloadVideos[/]", auth.DownloadVideos ),
                                ( "[red]DownloadAudios[/]", auth.DownloadAudios ),
                                ( "[red]IncludeExpiredSubscriptions[/]", auth.IncludeExpiredSubscriptions )
                            });

                            MultiSelectionPrompt<string> multiSelectionPrompt = new MultiSelectionPrompt<string>()
                                .Title("[red]Edit Auth.json[/]")
                                .PageSize(13);

                            foreach(var choice in choices)
                            {
                                multiSelectionPrompt.AddChoices(choice.choice, (selectionItem) => { if (choice.isSelected) selectionItem.Select(); });
                            }

                            var authOptions = AnsiConsole.Prompt(multiSelectionPrompt);

                            if(authOptions.Contains("[red]Go Back[/]"))
                            {
                                break;
                            }

                            Auth newAuth = new Auth();
                            newAuth.USER_ID = auth.USER_ID;
                            newAuth.USER_AGENT = auth.USER_AGENT;
                            newAuth.X_BC = auth.X_BC;
                            newAuth.COOKIE = auth.COOKIE;
                            newAuth.YTDLP_PATH = auth.YTDLP_PATH;
                            newAuth.FFMPEG_PATH = auth.FFMPEG_PATH;
                            newAuth.MP4DECRYPT_PATH = auth.MP4DECRYPT_PATH;
                            newAuth.DownloadPath = auth.DownloadPath;

                            if (authOptions.Contains("[red]DownloadAvatarHeaderPhoto[/]"))
                            {
                                newAuth.DownloadAvatarHeaderPhoto = true;
                            }
                            else
                            {
                                newAuth.DownloadAvatarHeaderPhoto = false;
                            }

                            if (authOptions.Contains("[red]DownloadPaidPosts[/]"))
                            {
                                newAuth.DownloadPaidPosts = true;
                            }
                            else
                            {
                                newAuth.DownloadPaidPosts = false;
                            }

                            if (authOptions.Contains("[red]DownloadPosts[/]"))
                            {
                                newAuth.DownloadPosts = true;
                            }
                            else
                            {
                                newAuth.DownloadPosts = false;
                            }

                            if (authOptions.Contains("[red]DownloadArchived[/]"))
                            {
                                newAuth.DownloadArchived = true;
                            }
                            else
                            {
                                newAuth.DownloadArchived = false;
                            }

                            if (authOptions.Contains("[red]DownloadStories[/]"))
                            {
                                newAuth.DownloadStories = true;
                            }
                            else
                            {
                                newAuth.DownloadStories = false;
                            }

                            if (authOptions.Contains("[red]DownloadHighlights[/]"))
                            {
                                newAuth.DownloadHighlights = true;
                            }
                            else
                            {
                                newAuth.DownloadHighlights = false;
                            }

                            if (authOptions.Contains("[red]DownloadMessages[/]"))
                            {
                                newAuth.DownloadMessages = true;
                            }
                            else
                            {
                                newAuth.DownloadMessages = false;
                            }

                            if (authOptions.Contains("[red]DownloadPaidMessages[/]"))
                            {
                                newAuth.DownloadPaidMessages = true;
                            }
                            else
                            {
                                newAuth.DownloadPaidMessages = false;
                            }

                            if (authOptions.Contains("[red]DownloadImages[/]"))
                            {
                                newAuth.DownloadImages = true;
                            }
                            else
                            {
                                newAuth.DownloadImages = false;
                            }

                            if (authOptions.Contains("[red]DownloadVideos[/]"))
                            {
                                newAuth.DownloadVideos = true;
                            }
                            else
                            {
                                newAuth.DownloadVideos = false;
                            }

                            if (authOptions.Contains("[red]DownloadAudios[/]"))
                            {
                                newAuth.DownloadAudios = true;
                            }
                            else
                            {
                                newAuth.DownloadAudios = false;
                            }

                            if (authOptions.Contains("[red]IncludeExpiredSubscriptions[/]"))
                            {
                                newAuth.IncludeExpiredSubscriptions = true;
                            }
                            else
                            {
                                newAuth.IncludeExpiredSubscriptions = false;
                            }

                            string newAuthString = JsonConvert.SerializeObject(newAuth, Formatting.Indented);
                            File.WriteAllText("auth.json", newAuthString);
                            if (newAuth.IncludeExpiredSubscriptions != auth.IncludeExpiredSubscriptions)
                            {
                                auth = JsonConvert.DeserializeObject<Auth>(File.ReadAllText("auth.json"));
                                return new KeyValuePair<bool, Dictionary<string, int>>(true, new Dictionary<string, int> { { "AuthChanged", 0 } });
                            }
                            auth = JsonConvert.DeserializeObject<Auth>(File.ReadAllText("auth.json"));
                            break;
                        }
                        break;
                    case "[red]Exit[/]":
                        return new KeyValuePair<bool, Dictionary<string, int>>(false, null); // Return false to indicate exit
                }
            }

            return new KeyValuePair<bool, Dictionary<string, int>>(true, selectedUsers); // Return true to indicate selected users
        }

        public static List<string> GetMainMenuOptions(Dictionary<string, int> users, Dictionary<string, int> lists)
        {
            if (lists.Count > 0)
            {
                return new List<string>
				{
					"[red]Select All[/]",
					"[red]List[/]",
					"[red]Custom[/]",
                    "[red]Edit Auth.json[/]",
					"[red]Exit[/]"
				};
            }
            else
            {
                return new List<string>
				{
					"[red]Select All[/]",
					"[red]Custom[/]",
                    "[red]Edit Auth.json[/]",
                    "[red]Exit[/]"
				};
            }
        }
        static bool ValidateFilePath(string path)
        {
            // Regular expression pattern to validate file path
            string pattern = @"^(?:[A-Za-z]:/)?(?:[^/\n]+/)*[^/:*?<>|]+\.[^/:*?<>|]+$";

            // Check if the path matches the pattern and doesn't end with a forward slash
            bool isMatch = Regex.IsMatch(path, pattern) && !path.EndsWith("/");

            return isMatch;
        }
    }
}
