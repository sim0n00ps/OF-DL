using Newtonsoft.Json;
using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Highlights;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Purchased;
using OF_DL.Entities.Stories;
using OF_DL.Enumurations;
using OF_DL.Helpers;
using Org.BouncyCastle.Asn1.Tsp;
using Spectre.Console;
using System.Text.RegularExpressions;
using static OF_DL.Entities.Lists.UserList;

namespace OF_DL
{
	public class Program
	{
        public static Auth auth { get; set; } = null;
        public static Config config { get; set; } = null;
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
                bool clientIdBlobMissing = false;
                bool devicePrivateKey = false;
				AnsiConsole.Write(new FigletText("Welcome to OF-DL").Color(Color.Red));

                if(File.Exists("auth.json"))
                {
                    AnsiConsole.Markup("[green]auth.json located successfully!\n[/]");
                    auth = JsonConvert.DeserializeObject<Auth>(File.ReadAllText("auth.json"));
                }
                else
                {
                    AnsiConsole.Markup("[red]auth.json does not exist, please make sure it exists in the folder where you are running the program from[/]");
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                if (File.Exists("config.json"))
                {
                    AnsiConsole.Markup("[green]config.json located successfully!\n[/]");
                    config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
                }
                else
                {
                    AnsiConsole.Markup("[red]config.json does not exist, please make sure it exists in the folder where you are running the program from[/]");
                    Console.ReadKey();
                    Environment.Exit(0);
                }

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

                if (!File.Exists("cdm/devices/chrome_1610/device_client_id_blob"))
                {
                    AnsiConsole.Markup("[yellow]WARNING No device_client_id_blob found, you will not be able to download DRM protected videos without this[/]\n");
                    clientIdBlobMissing = true;
                }
                else
                {
                    AnsiConsole.Markup($"[green]device_client_id_blob located successfully![/]\n");
                }

                if (!File.Exists("cdm/devices/chrome_1610/device_private_key"))
                {
                    AnsiConsole.Markup("[yellow]WARNING No device_private_key found, you will not be able to download DRM protected videos without this[/]\n");
                    devicePrivateKey = true;
                }
                else
                {
                    AnsiConsole.Markup($"[green]device_private_key located successfully![/]\n");
                }

                if (clientIdBlobMissing || devicePrivateKey)
                {
                    AnsiConsole.Markup("[yellow]A tutorial to get these 2 files can be found here: https://forum.videohelp.com/threads/408031-Dumping-Your-own-L3-CDM-with-Android-Studio\n[/]");
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
                        Dictionary<string, int> users = await apiHelper.GetSubscriptions("/subscriptions/subscribes", config.IncludeExpiredSubscriptions, auth);
                        Dictionary<string, int> lists = await apiHelper.GetLists("/lists", auth);
                        Dictionary<string, int> selectedUsers = new Dictionary<string, int>();
                        // Call the HandleUserSelection method to handle user selection and processing
                        KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP = await HandleUserSelection(selectedUsers, users, lists);

                        if (hasSelectedUsersKVP.Key && !hasSelectedUsersKVP.Value.ContainsKey("ConfigChanged"))
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
                                if (!string.IsNullOrEmpty(config.DownloadPath))
                                {
                                    path = System.IO.Path.Combine(config.DownloadPath, user.Key);
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

                                if (config.DownloadAvatarHeaderPhoto)
                                {
                                    await downloadHelper.DownloadAvatarHeader(user_info.avatar, user_info.header, path);
                                }

                                if (config.DownloadPaidPosts)
                                {
                                    AnsiConsole.Markup($"[red]Getting Paid Posts\n[/]");
                                    //Dictionary<long, string> purchasedPosts = await apiHelper.GetMedia(MediaType.PaidPosts, "/posts/paid", user.Key, path, auth, paid_post_ids);
                                    PaidPostCollection purchasedPosts = await apiHelper.GetPaidPosts("/posts/paid", path, user.Key, auth, config, paid_post_ids);
                                    int oldPaidPostCount = 0;
                                    int newPaidPostCount = 0;
                                    if (purchasedPosts != null && purchasedPosts.PaidPosts.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {purchasedPosts.PaidPosts.Count} Paid Posts\n[/]");
                                        paidPostCount = purchasedPosts.PaidPosts.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(purchasedPosts.PaidPosts.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            // Define tasks
                                            var task = ctx.AddTask($"[red]Downloading {purchasedPosts.PaidPosts.Count} Paid Posts[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> purchasedPostKVP in purchasedPosts.PaidPosts)
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
                                                        Purchased.Medium mediaInfo = purchasedPosts.PaidPostMedia.FirstOrDefault(m => m.id == purchasedPostKVP.Key);
                                                        Purchased.List postInfo = purchasedPosts.PaidPostObjects.FirstOrDefault(p => p.media.Contains(mediaInfo));
                                                        isNew = await downloadHelper.DownloadPurchasedPostDRMVideo(auth.YTDLP_PATH, auth.MP4DECRYPT_PATH, auth.FFMPEG_PATH, auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, mpdURL, decryptionKey, path, lastModified, purchasedPostKVP.Key, task, !string.IsNullOrEmpty(config.PaidPostFileNameFormat) ? config.PaidPostFileNameFormat : string.Empty, !string.IsNullOrEmpty(config.PaidPostFileNameFormat) ? postInfo : null, !string.IsNullOrEmpty(config.PaidPostFileNameFormat) ? mediaInfo : null, !string.IsNullOrEmpty(config.PaidPostFileNameFormat) ? postInfo.fromUser : null, hasSelectedUsersKVP.Value);
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
                                                    Purchased.Medium mediaInfo = purchasedPosts.PaidPostMedia.FirstOrDefault(m => m.id == purchasedPostKVP.Key);
                                                        Purchased.List postInfo = purchasedPosts.PaidPostObjects.FirstOrDefault(p => p.media.Contains(mediaInfo));
                                                    isNew = await downloadHelper.DownloadPurchasedPostMedia(purchasedPostKVP.Value, path, purchasedPostKVP.Key, task, !string.IsNullOrEmpty(config.PaidPostFileNameFormat) ? config.PaidPostFileNameFormat : string.Empty, !string.IsNullOrEmpty(config.PaidPostFileNameFormat) ? postInfo : null, !string.IsNullOrEmpty(config.PaidPostFileNameFormat) ? mediaInfo : null, !string.IsNullOrEmpty(config.PaidPostFileNameFormat) ? postInfo.fromUser : null, hasSelectedUsersKVP.Value);
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
                                            task.StopTask();
                                        });
                                        AnsiConsole.Markup($"[red]Paid Posts Already Downloaded: {oldPaidPostCount} New Paid Posts Downloaded: {newPaidPostCount}[/]\n");
                                    }
                                    else
                                    {
                                        AnsiConsole.Markup($"[red]Found 0 Paid Posts\n[/]");
                                    }
                                }

                                if (config.DownloadPosts)
                                {
                                    AnsiConsole.Markup($"[red]Getting Posts\n[/]");
                                    //Dictionary<long, string> posts = await apiHelper.GetMedia(MediaType.Posts, $"/users/{user.Value}/posts", null, path, auth, paid_post_ids);
                                    PostCollection posts = await apiHelper.GetPosts($"/users/{user.Value}/posts", path, auth, config, paid_post_ids);
                                    int oldPostCount = 0;
                                    int newPostCount = 0;
                                    if (posts != null && posts.Posts.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {posts.Posts.Count} Posts\n[/]");
                                        postCount = posts.Posts.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(posts.Posts.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            var task = ctx.AddTask($"[red]Downloading {posts.Posts.Count} Posts[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> postKVP in posts.Posts)
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
                                                        Post.Medium mediaInfo = posts.PostMedia.FirstOrDefault(m => m.id == postKVP.Key);
                                                        Post.List postInfo = posts.PostObjects.FirstOrDefault(p => p.media.Contains(mediaInfo));
                                                        isNew = await downloadHelper.DownloadPostDRMVideo(auth.YTDLP_PATH, auth.MP4DECRYPT_PATH, auth.FFMPEG_PATH, auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, mpdURL, decryptionKey, path, lastModified, postKVP.Key, task, !string.IsNullOrEmpty(config.PostFileNameFormat) ? config.PostFileNameFormat : string.Empty, !string.IsNullOrEmpty(config.PostFileNameFormat) ? postInfo : null, !string.IsNullOrEmpty(config.PostFileNameFormat) ? mediaInfo : null, !string.IsNullOrEmpty(config.PostFileNameFormat) ? postInfo?.author : null, hasSelectedUsersKVP.Value);
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
                                                    try
                                                    {
                                                        Post.Medium mediaInfo = posts.PostMedia.FirstOrDefault(m => m.id == postKVP.Key);
                                                        Post.List postInfo = posts.PostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);
                                                        isNew = await downloadHelper.DownloadPostMedia(postKVP.Value, path, postKVP.Key, task, !string.IsNullOrEmpty(config.PostFileNameFormat) ? config.PostFileNameFormat : string.Empty, !string.IsNullOrEmpty(config.PostFileNameFormat) ? postInfo : null, !string.IsNullOrEmpty(config.PostFileNameFormat) ? mediaInfo : null, !string.IsNullOrEmpty(config.PostFileNameFormat) ? postInfo?.author : null, hasSelectedUsersKVP.Value);
                                                        if (isNew)
                                                        {
                                                            newPostCount++;
                                                        }
                                                        else
                                                        {
                                                            oldPostCount++;
                                                        }
                                                    }
                                                    catch
                                                    {
                                                        Console.WriteLine("Media was null");
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

                                if (config.DownloadArchived)
                                {
                                    AnsiConsole.Markup($"[red]Getting Archived Posts\n[/]");
                                    //Dictionary<long, string> archived = await apiHelper.GetMedia(MediaType.Archived, $"/users/{user.Value}/posts", null, path, auth, paid_post_ids);
                                    ArchivedCollection archived = await apiHelper.GetArchived($"/users/{user.Value}/posts", path, auth, config);
                                    int oldArchivedCount = 0;
                                    int newArchivedCount = 0;
                                    if (archived != null && archived.ArchivedPosts.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {archived.ArchivedPosts.Count} Archived Posts\n[/]");
                                        archivedCount = archived.ArchivedPosts.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(archived.ArchivedPosts.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            // Define tasks
                                            var task = ctx.AddTask($"[red]Downloading {archived.ArchivedPosts.Count} Archived Posts[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> archivedKVP in archived.ArchivedPosts)
                                            {
                                                bool isNew;
                                                if (archivedKVP.Value.Contains("cdn3.onlyfans.com/dash/files"))
                                                {
                                                    string[] messageUrlParsed = archivedKVP.Value.Split(',');
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
                                                        Archived.Medium mediaInfo = archived.ArchivedPostMedia.FirstOrDefault(m => m.id == archivedKVP.Key);
                                                        Archived.List postInfo = archived.ArchivedPostObjects.FirstOrDefault(p => p.media.Contains(mediaInfo));
                                                        isNew = await downloadHelper.DownloadArchivedPostDRMVideo(auth.YTDLP_PATH, auth.MP4DECRYPT_PATH, auth.FFMPEG_PATH, auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, mpdURL, decryptionKey, path, lastModified, archivedKVP.Key, task, !string.IsNullOrEmpty(config.PostFileNameFormat) ? config.PostFileNameFormat : string.Empty, !string.IsNullOrEmpty(config.PostFileNameFormat) ? postInfo : null, !string.IsNullOrEmpty(config.PostFileNameFormat) ? mediaInfo : null, !string.IsNullOrEmpty(config.PostFileNameFormat) ? postInfo.author : null, hasSelectedUsersKVP.Value);
                                                        if (isNew)
                                                        {
                                                            newArchivedCount++;
                                                        }
                                                        else
                                                        {
                                                            oldArchivedCount++;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Archived.Medium mediaInfo = archived.ArchivedPostMedia.FirstOrDefault(m => m.id == archivedKVP.Key);
                                                    Archived.List postInfo = archived.ArchivedPostObjects.FirstOrDefault(p => p.media.Contains(mediaInfo));
                                                    isNew = await downloadHelper.DownloadArchivedMedia(archivedKVP.Value, path, archivedKVP.Key, task, !string.IsNullOrEmpty(config.PostFileNameFormat) ? config.PostFileNameFormat : string.Empty, !string.IsNullOrEmpty(config.PostFileNameFormat) ? postInfo : null, !string.IsNullOrEmpty(config.PostFileNameFormat) ? mediaInfo : null, !string.IsNullOrEmpty(config.PostFileNameFormat) ? postInfo.author : null, hasSelectedUsersKVP.Value);
                                                    if (isNew)
                                                    {
                                                        newArchivedCount++;
                                                    }
                                                    else
                                                    {
                                                        oldArchivedCount++;
                                                    }
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

                                if (config.DownloadStories)
                                {
                                    AnsiConsole.Markup($"[red]Getting Stories\n[/]");
                                    Dictionary<long, string> stories = await apiHelper.GetMedia(MediaType.Stories, $"/users/{user.Value}/stories", null, path, auth, config, paid_post_ids);
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

                                if (config.DownloadHighlights)
                                {
                                    AnsiConsole.Markup($"[red]Getting Highlights\n[/]");
                                    Dictionary<long, string> highlights = await apiHelper.GetMedia(MediaType.Highlights, $"/users/{user.Value}/stories/highlights", null, path, auth, config,  paid_post_ids);
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

                                if (config.DownloadMessages)
                                {
                                    AnsiConsole.Markup($"[red]Getting Messages\n[/]");
                                    //Dictionary<long, string> messages = await apiHelper.GetMedia(MediaType.Messages, $"/chats/{user.Value}/messages", null, path, auth, paid_post_ids);
                                    MessageCollection messages = await apiHelper.GetMessages($"/chats/{user.Value}/messages", path, auth, config);
                                    int oldMessagesCount = 0;
                                    int newMessagesCount = 0;
                                    if (messages != null && messages.Messages.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {messages.Messages.Count} Messages\n[/]");
                                        messagesCount = messages.Messages.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(messages.Messages.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            // Define tasks
                                            var task = ctx.AddTask($"[red]Downloading {messages.Messages.Count} Messages[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> messageKVP in messages.Messages)
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
                                                        Messages.Medium mediaInfo = messages.MessageMedia.FirstOrDefault(m => m.id == messageKVP.Key);
                                                        Messages.List messageInfo = messages.MessageObjects.FirstOrDefault(p => p.media.Contains(mediaInfo));
                                                        isNew = await downloadHelper.DownloadMessageDRMVideo(auth.YTDLP_PATH, auth.MP4DECRYPT_PATH, auth.FFMPEG_PATH, auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, mpdURL, decryptionKey, path, lastModified, messageKVP.Key, task, !string.IsNullOrEmpty(config.MessageFileNameFormat) ? config.MessageFileNameFormat : string.Empty, !string.IsNullOrEmpty(config.MessageFileNameFormat) ? messageInfo : null, !string.IsNullOrEmpty(config.MessageFileNameFormat) ? mediaInfo : null, !string.IsNullOrEmpty(config.MessageFileNameFormat) ? messageInfo.fromUser : null, hasSelectedUsersKVP.Value);
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
                                                    Messages.Medium mediaInfo = messages.MessageMedia.FirstOrDefault(m => m.id == messageKVP.Key);
                                                    Messages.List messageInfo = messages.MessageObjects.FirstOrDefault(p => p.media.Contains(mediaInfo));
                                                    isNew = await downloadHelper.DownloadMessageMedia(messageKVP.Value, path, messageKVP.Key, task, !string.IsNullOrEmpty(config.MessageFileNameFormat) ? config.MessageFileNameFormat : string.Empty, !string.IsNullOrEmpty(config.MessageFileNameFormat) ? messageInfo : null, !string.IsNullOrEmpty(config.MessageFileNameFormat) ? mediaInfo : null, !string.IsNullOrEmpty(config.MessageFileNameFormat) ? messageInfo.fromUser : null, hasSelectedUsersKVP.Value);
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
                                            task.StopTask();
                                        });
                                        AnsiConsole.Markup($"[red]Messages Already Downloaded: {oldMessagesCount} New Messages Downloaded: {newMessagesCount}[/]\n");
                                    }
                                    else
                                    {
                                        AnsiConsole.Markup($"[red]Found 0 Messages\n[/]");
                                    }
                                }

                                if (config.DownloadPaidMessages)
                                {
                                    AnsiConsole.Markup($"[red]Getting Paid Messages\n[/]");
                                    //Dictionary<long, string> purchased = await apiHelper.GetMedia(MediaType.PaidMessages, "/posts/paid", user.Key, path, auth, paid_post_ids);
                                    PaidMessageCollection paidMessageCollection = await apiHelper.GetPaidMessages("/posts/paid", path, user.Key, auth, config);
                                    int oldPaidMessagesCount = 0;
                                    int newPaidMessagesCount = 0;
                                    if (paidMessageCollection != null && paidMessageCollection.PaidMessages.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {paidMessageCollection.PaidMessages.Count} Paid Messages\n[/]");
                                        paidMessagesCount = paidMessageCollection.PaidMessages.Count;
                                        long totalSize = await downloadHelper.CalculateTotalFileSize(paidMessageCollection.PaidMessages.Values.ToList(), auth);
                                        await AnsiConsole.Progress()
                                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                                        .StartAsync(async ctx =>
                                        {
                                            // Define tasks
                                            var task = ctx.AddTask($"[red]Downloading {paidMessageCollection.PaidMessages.Count} Paid Messages[/]", autoStart: false);
                                            task.MaxValue = totalSize;
                                            task.StartTask();
                                            foreach (KeyValuePair<long, string> paidMessageKVP in paidMessageCollection.PaidMessages)
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
                                                        Purchased.Medium mediaInfo = paidMessageCollection.PaidMessageMedia.FirstOrDefault(m => m.id == paidMessageKVP.Key);
                                                        Purchased.List messageInfo = paidMessageCollection.PaidMessageObjects.FirstOrDefault(p => p.media.Contains(mediaInfo));
                                                        isNew = await downloadHelper.DownloadPurchasedMessageDRMVideo(auth.YTDLP_PATH, auth.MP4DECRYPT_PATH, auth.FFMPEG_PATH, auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, mpdURL, decryptionKey, path, lastModified, paidMessageKVP.Key, task, !string.IsNullOrEmpty(config.PaidMessageFileNameFormat) ? config.PaidMessageFileNameFormat : string.Empty, !string.IsNullOrEmpty(config.PaidMessageFileNameFormat) ? messageInfo : null, !string.IsNullOrEmpty(config.PaidMessageFileNameFormat) ? mediaInfo : null, !string.IsNullOrEmpty(config.PaidMessageFileNameFormat) ? messageInfo.fromUser : null, hasSelectedUsersKVP.Value);
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
                                                    Purchased.Medium mediaInfo = paidMessageCollection.PaidMessageMedia.FirstOrDefault(m => m.id == paidMessageKVP.Key);
                                                    Purchased.List messageInfo = paidMessageCollection.PaidMessageObjects.FirstOrDefault(p => p.media.Contains(mediaInfo));
                                                    isNew = await downloadHelper.DownloadPurchasedMedia(paidMessageKVP.Value, path, paidMessageKVP.Key, task,!string.IsNullOrEmpty(config.PaidMessageFileNameFormat) ? config.PaidMessageFileNameFormat : string.Empty, !string.IsNullOrEmpty(config.PaidMessageFileNameFormat) ? messageInfo : null, !string.IsNullOrEmpty(config.PaidMessageFileNameFormat) ? mediaInfo : null, !string.IsNullOrEmpty(config.PaidMessageFileNameFormat) ? messageInfo.fromUser : null, hasSelectedUsersKVP.Value);
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
                        else if (hasSelectedUsersKVP.Key && hasSelectedUsersKVP.Value != null ? hasSelectedUsersKVP.Value.ContainsKey("ConfigChanged") : false)
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
                    case "[red]Edit config.json[/]":
                        while (true)
                        {
                            var choices = new List<(string choice, bool isSelected)>();
                            choices.AddRange(new []
                            {
                                ( "[red]Go Back[/]", false ),
                                ( "[red]DownloadAvatarHeaderPhoto[/]", config.DownloadAvatarHeaderPhoto),
                                ( "[red]DownloadPaidPosts[/]", config.DownloadPaidPosts ),
                                ( "[red]DownloadPosts[/]",  config.DownloadPosts ),
                                ( "[red]DownloadArchived[/]", config.DownloadArchived ),
                                ( "[red]DownloadStories[/]", config.DownloadStories ),
                                ( "[red]DownloadHighlights[/]", config.DownloadHighlights ),
                                ( "[red]DownloadMessages[/]", config.DownloadMessages ),
                                ( "[red]DownloadPaidMessages[/]", config.DownloadPaidMessages ),
                                ( "[red]DownloadImages[/]", config.DownloadImages ),
                                ( "[red]DownloadVideos[/]", config.DownloadVideos ),
                                ( "[red]DownloadAudios[/]", config.DownloadAudios ),
                                ( "[red]IncludeExpiredSubscriptions[/]", config.IncludeExpiredSubscriptions ),
                                ( "[red]SkipAds[/]", config.SkipAds )
                            });

                            MultiSelectionPrompt<string> multiSelectionPrompt = new MultiSelectionPrompt<string>()
                                .Title("[red]Edit config.json[/]")
                                .PageSize(14);

                            foreach(var choice in choices)
                            {
                                multiSelectionPrompt.AddChoices(choice.choice, (selectionItem) => { if (choice.isSelected) selectionItem.Select(); });
                            }

                            var configOptions = AnsiConsole.Prompt(multiSelectionPrompt);

                            if(configOptions.Contains("[red]Go Back[/]"))
                            {
                                break;
                            }

                            Config newConfig = new Config();
                            newConfig.DownloadPath = config.DownloadPath;
                            newConfig.PostFileNameFormat = config.PostFileNameFormat;
                            newConfig.MessageFileNameFormat = config.MessageFileNameFormat;
                            newConfig.PaidPostFileNameFormat = config.PaidPostFileNameFormat;
                            newConfig.PaidMessageFileNameFormat = config.PaidMessageFileNameFormat;

                            if (configOptions.Contains("[red]DownloadAvatarHeaderPhoto[/]"))
                            {
                                newConfig.DownloadAvatarHeaderPhoto = true;
                            }
                            else
                            {
                                newConfig.DownloadAvatarHeaderPhoto = false;
                            }

                            if (configOptions.Contains("[red]DownloadPaidPosts[/]"))
                            {
                                newConfig.DownloadPaidPosts = true;
                            }
                            else
                            {
                                newConfig.DownloadPaidPosts = false;
                            }

                            if (configOptions.Contains("[red]DownloadPosts[/]"))
                            {
                                newConfig.DownloadPosts = true;
                            }
                            else
                            {
                                newConfig.DownloadPosts = false;
                            }

                            if (configOptions.Contains("[red]DownloadArchived[/]"))
                            {
                                newConfig.DownloadArchived = true;
                            }
                            else
                            {
                                newConfig.DownloadArchived = false;
                            }

                            if (configOptions.Contains("[red]DownloadStories[/]"))
                            {
                                newConfig.DownloadStories = true;
                            }
                            else
                            {
                                newConfig.DownloadStories = false;
                            }

                            if (configOptions.Contains("[red]DownloadHighlights[/]"))
                            {
                                newConfig.DownloadHighlights = true;
                            }
                            else
                            {
                                newConfig.DownloadHighlights = false;
                            }

                            if (configOptions.Contains("[red]DownloadMessages[/]"))
                            {
                                newConfig.DownloadMessages = true;
                            }
                            else
                            {
                                newConfig.DownloadMessages = false;
                            }

                            if (configOptions.Contains("[red]DownloadPaidMessages[/]"))
                            {
                                newConfig.DownloadPaidMessages = true;
                            }
                            else
                            {
                                newConfig.DownloadPaidMessages = false;
                            }

                            if (configOptions.Contains("[red]DownloadImages[/]"))
                            {
                                newConfig.DownloadImages = true;
                            }
                            else
                            {
                                newConfig.DownloadImages = false;
                            }

                            if (configOptions.Contains("[red]DownloadVideos[/]"))
                            {
                                newConfig.DownloadVideos = true;
                            }
                            else
                            {
                                newConfig.DownloadVideos = false;
                            }

                            if (configOptions.Contains("[red]DownloadAudios[/]"))
                            {
                                newConfig.DownloadAudios = true;
                            }
                            else
                            {
                                newConfig.DownloadAudios = false;
                            }

                            if (configOptions.Contains("[red]IncludeExpiredSubscriptions[/]"))
                            {
                                newConfig.IncludeExpiredSubscriptions = true;
                            }
                            else
                            {
                                newConfig.IncludeExpiredSubscriptions = false;
                            }

                            if (configOptions.Contains("[red]SkipAds[/]"))
                            {
                                newConfig.SkipAds = true;
                            }
                            else
                            {
                                newConfig.SkipAds = false;
                            }

                            string newConfigString = JsonConvert.SerializeObject(newConfig, Formatting.Indented);
                            File.WriteAllText("config.json", newConfigString);
                            if (config.IncludeExpiredSubscriptions != config.IncludeExpiredSubscriptions)
                            {
                                config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
                                return new KeyValuePair<bool, Dictionary<string, int>>(true, new Dictionary<string, int> { { "ConfigChanged", 0 } });
                            }
                            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
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
                    "[red]Edit config.json[/]",
					"[red]Exit[/]"
				};
            }
            else
            {
                return new List<string>
				{
					"[red]Select All[/]",
					"[red]Custom[/]",
                    "[red]Edit config.json[/]",
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
