using Newtonsoft.Json;
using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Highlights;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Stories;
using OF_DL.Enumurations;
using OF_DL.Helpers;
using Spectre.Console;
using System.Text.RegularExpressions;
using static OF_DL.Entities.Lists.UserList;

namespace OF_DL
{
	public class Program
	{
		private readonly APIHelper apiHelper;
		private readonly DownloadHelper downloadHelper;
		private readonly DBHelper dBHelper;
		public Program(APIHelper _aPIHelper, DownloadHelper _downloadHelper, DBHelper _dBHelper)
		{
			apiHelper = _aPIHelper;
			downloadHelper = _downloadHelper;
			dBHelper = _dBHelper;
		}

		public Auth auth = JsonConvert.DeserializeObject<Auth>(File.ReadAllText("auth.json"));
		public int MAX_AGE = 0;
		public List<long> paid_post_ids = new List<long>();
		public async static Task Main()
		{
			try
			{
				AnsiConsole.Write(new FigletText("Welcome to OF-DL").Color(Color.Red));
				Program program = new Program(new APIHelper(), new DownloadHelper(), new DBHelper());
				DateTime startTime = DateTime.Now;
                bool exitProgram = false;

                if (ValidateFilePath(program.auth.YTDLP_PATH))
                {
                    if (!File.Exists(program.auth.YTDLP_PATH))
                    {
                        AnsiConsole.Markup($"[red]Cannot locate yt-dlp.exe with specified path {program.auth.YTDLP_PATH}, please modify auth.json with the correct path, press any key to exit[/]");
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
                    AnsiConsole.Markup(@$"[red]Specified path {program.auth.YTDLP_PATH} does not match the required format, please remove any \ from the path and replace them with / and make sure the path does not have a / at the end, press any key to exit[/]");
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                if (ValidateFilePath(program.auth.FFMPEG_PATH))
                {
                    if (!File.Exists(program.auth.FFMPEG_PATH))
                    {
                        AnsiConsole.Markup($"[red]Cannot locate ffmpeg.exe with specified path {program.auth.FFMPEG_PATH}, please modify auth.json with the correct path, press any key to exit[/]");
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
                    AnsiConsole.Markup(@$"[red]Specified path {program.auth.FFMPEG_PATH} does not match the required format, please remove any \ from the path and replace them with / and make sure the path does not have a / at the end, press any key to exit[/]");
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                if (ValidateFilePath(program.auth.MP4DECRYPT_PATH))
                {
                    if (!File.Exists(program.auth.MP4DECRYPT_PATH))
                    {
                        AnsiConsole.Markup($"[red]Cannot locate mp4decrypt.exe with specified path {program.auth.MP4DECRYPT_PATH}, please modify auth.json with the correct path, press any key to exit[/]");
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
                    AnsiConsole.Markup(@$"[red]Specified path {program.auth.MP4DECRYPT_PATH} does not match the required format, please remove any \ from the path and replace them with / and make sure the path does not have a / at the end, press any key to exit[/]");
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                //Check if auth is valid
                Entities.User validate = await program.apiHelper.GetUserInfo($"/users/me");
                if (validate.name == null && validate.username == null)
                {
                    AnsiConsole.Markup($"[red]Auth failed, please check the values in auth.json are correct, press any key to exit[/]");
                    Console.ReadKey();
                }
                else
                {
                    AnsiConsole.Markup($"[green]Logged In successfully as {validate.name} {validate.username}\n[/]");
                    Dictionary<string, int> users = await program.apiHelper.GetSubscriptions("/subscriptions/subscribes");
                    Dictionary<string, int> lists = await program.apiHelper.GetLists("/lists");
                    Dictionary<string, int> selectedUsers = new Dictionary<string, int>();
                    do
                    {
                        // Call the HandleUserSelection method to handle user selection and processing
                        KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP = await HandleUserSelection(selectedUsers, users, lists);

                        if (hasSelectedUsersKVP.Key)
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

                                string path = $"__user_data__/sites/OnlyFans/{user.Key}"; // specify the path for the new folder

                                if (!Directory.Exists(path)) // check if the folder already exists
                                {
                                    Directory.CreateDirectory(path); // create the new folder
                                    AnsiConsole.Markup($"[red]Created folder for {user.Key}\n[/]");
                                }
                                else
                                {
                                    AnsiConsole.Markup($"[red]Folder for {user.Key} already created\n[/]");
                                }

                                Entities.User user_info = await program.apiHelper.GetUserInfo($"/users/{user.Key}");

                                await program.dBHelper.CreateDB(path);

                                if (program.auth.DownloadAvatarHeaderPhoto)
                                {
                                    await program.downloadHelper.DownloadAvatarHeader(user_info.avatar, user_info.header, path);
                                }

                                if (program.auth.DownloadPaidPosts)
                                {
                                    AnsiConsole.Markup($"[red]Getting Paid Posts\n[/]");
                                    Dictionary<long, string> purchasedPosts = await program.apiHelper.GetMedia(MediaType.PaidPosts, "/posts/paid", user.Key, path);

                                    int oldPaidPostCount = 0;
                                    int newPaidPostCount = 0;
                                    if (purchasedPosts != null && purchasedPosts.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {purchasedPosts.Count} Paid Posts\n[/]");
                                        paidPostCount = purchasedPosts.Count;
                                        long totalSize = await program.downloadHelper.CalculateTotalFileSize(purchasedPosts.Values.ToList());
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
                                                    string? pssh = await program.apiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
                                                    if (pssh != null)
                                                    {
                                                        DateTime lastModified = await program.apiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
                                                        Dictionary<string, string> drmHeaders = await program.apiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine");
                                                        string decryptionKey = await program.apiHelper.GetDecryptionKey(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
                                                        isNew = await program.downloadHelper.DownloadPurchasedPostDRMVideo(program.auth.YTDLP_PATH, program.auth.MP4DECRYPT_PATH, program.auth.FFMPEG_PATH, program.auth.USER_AGENT, policy, signature, kvp, program.auth.COOKIE, mpdURL, decryptionKey, path, lastModified, purchasedPostKVP.Key, task);
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
                                                    isNew = await program.downloadHelper.DownloadPurchasedPostMedia(purchasedPostKVP.Value, path, purchasedPostKVP.Key, task);
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

                                if (program.auth.DownloadPosts)
                                {
                                    AnsiConsole.Markup($"[red]Getting Posts\n[/]");
                                    Dictionary<long, string> posts = await program.apiHelper.GetMedia(MediaType.Posts, $"/users/{user.Value}/posts", null, path);
                                    int oldPostCount = 0;
                                    int newPostCount = 0;
                                    if (posts != null && posts.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {posts.Count} Posts\n[/]");
                                        postCount = posts.Count;
                                        long totalSize = await program.downloadHelper.CalculateTotalFileSize(posts.Values.ToList());
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
                                                    string? pssh = await program.apiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
                                                    if (pssh != null)
                                                    {
                                                        DateTime lastModified = await program.apiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
                                                        Dictionary<string, string> drmHeaders = await program.apiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine");
                                                        string decryptionKey = await program.apiHelper.GetDecryptionKey(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
                                                        isNew = await program.downloadHelper.DownloadPostDRMVideo(program.auth.YTDLP_PATH, program.auth.MP4DECRYPT_PATH, program.auth.FFMPEG_PATH, program.auth.USER_AGENT, policy, signature, kvp, program.auth.COOKIE, mpdURL, decryptionKey, path, lastModified, postKVP.Key, task);
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
                                                    isNew = await program.downloadHelper.DownloadPostMedia(postKVP.Value, path, postKVP.Key, task);
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

                                if (program.auth.DownloadArchived)
                                {
                                    AnsiConsole.Markup($"[red]Getting Archived Posts\n[/]");
                                    Dictionary<long, string> archived = await program.apiHelper.GetMedia(MediaType.Archived, $"/users/{user.Value}/posts/archived", null, path);

                                    int oldArchivedCount = 0;
                                    int newArchivedCount = 0;
                                    if (archived != null && archived.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {archived.Count} Archived Posts\n[/]");
                                        archivedCount = archived.Count;
                                        long totalSize = await program.downloadHelper.CalculateTotalFileSize(archived.Values.ToList());
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
                                                bool isNew = await program.downloadHelper.DownloadArchivedMedia(archivedKVP.Value, path, archivedKVP.Key, task);
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

                                if (program.auth.DownloadStories)
                                {
                                    AnsiConsole.Markup($"[red]Getting Stories\n[/]");
                                    Dictionary<long, string> stories = await program.apiHelper.GetMedia(MediaType.Stories, $"/users/{user.Value}/stories", null, path);
                                    int oldStoriesCount = 0;
                                    int newStoriesCount = 0;
                                    if (stories != null && stories.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {stories.Count} Stories\n[/]");
                                        storiesCount = stories.Count;
                                        long totalSize = await program.downloadHelper.CalculateTotalFileSize(stories.Values.ToList());
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
                                                bool isNew = await program.downloadHelper.DownloadStoryMedia(storyKVP.Value, path, storyKVP.Key, task);
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

                                if (program.auth.DownloadHighlights)
                                {
                                    AnsiConsole.Markup($"[red]Getting Highlights\n[/]");
                                    Dictionary<long, string> highlights = await program.apiHelper.GetMedia(MediaType.Highlights, $"/users/{user.Value}/stories/highlights", null, path);
                                    int oldHighlightsCount = 0;
                                    int newHighlightsCount = 0;
                                    if (highlights != null && highlights.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {highlights.Count} Highlights\n[/]");
                                        highlightsCount = highlights.Count;
                                        long totalSize = await program.downloadHelper.CalculateTotalFileSize(highlights.Values.ToList());
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
                                                bool isNew = await program.downloadHelper.DownloadStoryMedia(highlightKVP.Value, path, highlightKVP.Key, task);
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

                                if (program.auth.DownloadMessages)
                                {
                                    AnsiConsole.Markup($"[red]Getting Messages\n[/]");
                                    Dictionary<long, string> messages = await program.apiHelper.GetMedia(MediaType.Messages, $"/chats/{user.Value}/messages", null, path);
                                    int oldMessagesCount = 0;
                                    int newMessagesCount = 0;
                                    if (messages != null && messages.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {messages.Count} Messages\n[/]");
                                        messagesCount = messages.Count;
                                        long totalSize = await program.downloadHelper.CalculateTotalFileSize(messages.Values.ToList());
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
                                                    string? pssh = await program.apiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
                                                    if (pssh != null)
                                                    {
                                                        DateTime lastModified = await program.apiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
                                                        Dictionary<string, string> drmHeaders = await program.apiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/message/{messageId}", "?type=widevine");
                                                        string decryptionKey = await program.apiHelper.GetDecryptionKey(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh);
                                                        isNew = await program.downloadHelper.DownloadMessageDRMVideo(program.auth.YTDLP_PATH, program.auth.MP4DECRYPT_PATH, program.auth.FFMPEG_PATH, program.auth.USER_AGENT, policy, signature, kvp, program.auth.COOKIE, mpdURL, decryptionKey, path, lastModified, messageKVP.Key, task);
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
                                                    isNew = await program.downloadHelper.DownloadMessageMedia(messageKVP.Value, path, messageKVP.Key, task);
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

                                if (program.auth.DownloadPaidMessages)
                                {
                                    AnsiConsole.Markup($"[red]Getting Paid Messages\n[/]");
                                    Dictionary<long, string> purchased = await program.apiHelper.GetMedia(MediaType.PaidMessages, "/posts/paid", user.Key, path);

                                    int oldPaidMessagesCount = 0;
                                    int newPaidMessagesCount = 0;
                                    if (purchased != null && purchased.Count > 0)
                                    {
                                        AnsiConsole.Markup($"[red]Found {purchased.Count} Paid Messages\n[/]");
                                        paidMessagesCount = purchased.Count;
                                        long totalSize = await program.downloadHelper.CalculateTotalFileSize(purchased.Values.ToList());
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
                                                    string? pssh = await program.apiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
                                                    if (pssh != null)
                                                    {
                                                        DateTime lastModified = await program.apiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
                                                        Dictionary<string, string> drmHeaders = await program.apiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/message/{messageId}", "?type=widevine");
                                                        string decryptionKey = await program.apiHelper.GetDecryptionKey(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh);
                                                        isNew = await program.downloadHelper.DownloadPurchasedMessageDRMVideo(program.auth.YTDLP_PATH, program.auth.MP4DECRYPT_PATH, program.auth.FFMPEG_PATH, program.auth.USER_AGENT, policy, signature, kvp, program.auth.COOKIE, mpdURL, decryptionKey, path, lastModified, paidMessageKVP.Key, task);
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
                                                    isNew = await program.downloadHelper.DownloadPurchasedMedia(paidMessageKVP.Value, path, paidMessageKVP.Key, task);
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
				Program program = new Program(new APIHelper(), new DownloadHelper(), new DBHelper());
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
                                    List<string> usernames = await program.apiHelper.GetListUsers($"/lists/{listId}/users");
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
					"[red]Exit[/]"
				};
            }
            else
            {
                return new List<string>
				{
					"[red]Select All[/]",
					"[red]Custom[/]",
					"[red]Exit[/]"
				};
            }
        }
        static bool ValidateFilePath(string path)
        {
            // Regular expression pattern to validate file path
            string pattern = @"^[A-Za-z]:/(?:[^/\n]+/)*[^/:*?<>|]+\.[^/:*?<>|]+$";

            // Check if the path matches the pattern and doesn't end with a forward slash
            bool isMatch = Regex.IsMatch(path, pattern) && !path.EndsWith("/");

            return isMatch;
        }
    }
}