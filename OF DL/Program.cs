using Newtonsoft.Json;
using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Purchased;
using OF_DL.Enumurations;
using OF_DL.Helpers;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace OF_DL;

public class Program
{
    public static Auth? Auth { get; set; } = null;
    public static Config? Config { get; set; } = null;
    public int MAX_AGE = 0;
    public static List<long> paid_post_ids = new();
    private static readonly IAPIHelper m_ApiHelper;
    private static readonly IDBHelper m_DBHelper;
    private static readonly IDownloadHelper m_DownloadHelper;


    static Program()
	{
        m_ApiHelper = new APIHelper();
        m_DBHelper = new DBHelper();
        m_DownloadHelper = new DownloadHelper();
    }

	public async static Task Main()
	{
		try
        {
            bool clientIdBlobMissing = false;
            bool devicePrivateKey = false;
            AnsiConsole.Write(new FigletText("Welcome to OF-DL").Color(Color.Red));

            if (File.Exists("auth.json"))
            {
                AnsiConsole.Markup("[green]auth.json located successfully!\n[/]");
                Auth = JsonConvert.DeserializeObject<Auth>(File.ReadAllText("auth.json"));
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
                Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
            }
            else
            {
                AnsiConsole.Markup("[red]config.json does not exist, please make sure it exists in the folder where you are running the program from[/]");
                Console.ReadKey();
                Environment.Exit(0);
            }

            if (ValidateFilePath(Auth.YTDLP_PATH))
            {
                if (!File.Exists(Auth.YTDLP_PATH))
                {
                    AnsiConsole.Markup($"[red]Cannot locate yt-dlp.exe with specified path {Auth.YTDLP_PATH}, please modify auth.json with the correct path, press any key to exit[/]");
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
                AnsiConsole.Markup(@$"[red]Specified path {Auth.YTDLP_PATH} does not match the required format, please remove any \ from the path and replace them with / and make sure the path does not have a / at the end, press any key to exit[/]");
                Console.ReadKey();
                Environment.Exit(0);
            }

            if (ValidateFilePath(Auth.FFMPEG_PATH))
            {
                if (!File.Exists(Auth.FFMPEG_PATH))
                {
                    AnsiConsole.Markup($"[red]Cannot locate ffmpeg.exe with specified path {Auth.FFMPEG_PATH}, please modify auth.json with the correct path, press any key to exit[/]");
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
                AnsiConsole.Markup(@$"[red]Specified path {Auth.FFMPEG_PATH} does not match the required format, please remove any \ from the path and replace them with / and make sure the path does not have a / at the end, press any key to exit[/]");
                Console.ReadKey();
                Environment.Exit(0);
            }

            if (ValidateFilePath(Auth.MP4DECRYPT_PATH))
            {
                if (!File.Exists(Auth.MP4DECRYPT_PATH))
                {
                    AnsiConsole.Markup($"[red]Cannot locate mp4decrypt.exe with specified path {Auth.MP4DECRYPT_PATH}, please modify auth.json with the correct path, press any key to exit[/]");
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
                AnsiConsole.Markup(@$"[red]Specified path {Auth.MP4DECRYPT_PATH} does not match the required format, please remove any \ from the path and replace them with / and make sure the path does not have a / at the end, press any key to exit[/]");
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
            Entities.User validate = await m_ApiHelper.GetUserInfo($"/users/me", Auth);
            if (validate.name == null && validate.username == null)
            {
                AnsiConsole.Markup($"[red]Auth failed, please check the values in auth.json are correct, press any key to exit[/]");
                Console.ReadKey();
                return;
            }


            AnsiConsole.Markup($"[green]Logged In successfully as {validate.name} {validate.username}\n[/]");
            await DownloadAllData();
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


    private static async Task DownloadAllData()
    {
        do
        {
            DateTime startTime = DateTime.Now;
            Dictionary<string, int> users = new Dictionary<string, int>();
            Dictionary<string, int> activeSubs = await m_ApiHelper.GetActiveSubscriptions("/subscriptions/subscribes", Auth);
            foreach (KeyValuePair<string, int> activeSub in activeSubs)
            {
                if (!users.ContainsKey(activeSub.Key))
                {
                    users.Add(activeSub.Key, activeSub.Value);
                }
            }
            if (Config!.IncludeExpiredSubscriptions)
            {
                Dictionary<string, int> expiredSubs = await m_ApiHelper.GetExpiredSubscriptions("/subscriptions/subscribes", Auth);
                foreach (KeyValuePair<string, int> expiredSub in expiredSubs)
                {
                    if (!users.ContainsKey(expiredSub.Key))
                    {
                        users.Add(expiredSub.Key, expiredSub.Value);
                    }
                }
            }
            Dictionary<string, int> lists = await m_ApiHelper.GetLists("/lists", Auth);
            Dictionary<string, int> selectedUsers = new();
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
                    if (!string.IsNullOrEmpty(Config.DownloadPath))
                    {
                        path = System.IO.Path.Combine(Config.DownloadPath, user.Key);
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

                    Entities.User user_info = await m_ApiHelper.GetUserInfo($"/users/{user.Key}", Auth);

                    await m_DBHelper.CreateDB(path);

                    if (Config.DownloadAvatarHeaderPhoto)
                    {
                        await m_DownloadHelper.DownloadAvatarHeader(user_info.avatar, user_info.header, path);
                    }

                    if (Config.DownloadPaidPosts)
                    {
                        paidPostCount = await DownloadPaidPosts(hasSelectedUsersKVP, user, paidPostCount, path);
                    }

                    if (Config.DownloadPosts)
                    {
                        postCount = await DownloadFreePosts(hasSelectedUsersKVP, user, postCount, path);
                    }

                    if (Config.DownloadArchived)
                    {
                        archivedCount = await DownloadArchived(hasSelectedUsersKVP, user, archivedCount, path);
                    }

                    if (Config.DownloadStories)
                    {
                        storiesCount = await DownloadStories(user, storiesCount, path);
                    }

                    if (Config.DownloadHighlights)
                    {
                        highlightsCount = await DownloadHighlights(user, highlightsCount, path);
                    }

                    if (Config.DownloadMessages)
                    {
                        messagesCount = await DownloadMessages(hasSelectedUsersKVP, user, messagesCount, path);
                    }

                    if (Config.DownloadPaidMessages)
                    {
                        paidMessagesCount = await DownloadPaidMessages(hasSelectedUsersKVP, user, paidMessagesCount, path);
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
                AnsiConsole.Markup($"[green]Scrape Completed in {totalTime.TotalMinutes:0.00} minutes\n[/]");
            }
            else if (hasSelectedUsersKVP.Key && hasSelectedUsersKVP.Value != null && hasSelectedUsersKVP.Value.ContainsKey("ConfigChanged"))
            {
                continue;
            }
            else
            {
                break;
            }
        } while (true);
    }

    private static async Task<int> DownloadPaidMessages(KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, KeyValuePair<string, int> user, int paidMessagesCount, string path)
    {
        AnsiConsole.Markup($"[red]Getting Paid Messages\n[/]");
        //Dictionary<long, string> purchased = await apiHelper.GetMedia(MediaType.PaidMessages, "/posts/paid", user.Key, path, auth, paid_post_ids);
        PaidMessageCollection paidMessageCollection = await m_ApiHelper.GetPaidMessages("/posts/paid", path, user.Key, Auth!, Config!);
        int oldPaidMessagesCount = 0;
        int newPaidMessagesCount = 0;
        if (paidMessageCollection != null && paidMessageCollection.PaidMessages.Count > 0)
        {
            AnsiConsole.Markup($"[red]Found {paidMessageCollection.PaidMessages.Count} Paid Messages\n[/]");
            paidMessagesCount = paidMessageCollection.PaidMessages.Count;
            long totalSize = await m_DownloadHelper.CalculateTotalFileSize(paidMessageCollection.PaidMessages.Values.ToList(), Auth);
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
                        string? pssh = await m_ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp, Auth);
                        if (pssh != null)
                        {
                            DateTime lastModified = await m_ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp, Auth);
                            Dictionary<string, string> drmHeaders = await m_ApiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/message/{messageId}", "?type=widevine", Auth);
                            string decryptionKey = await m_ApiHelper.GetDecryptionKeyNew(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh, Auth);

                            Purchased.Medium? mediaInfo = paidMessageCollection.PaidMessageMedia.FirstOrDefault(m => m.id == paidMessageKVP.Key);
                            Purchased.List? messageInfo = paidMessageCollection.PaidMessageObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

                            isNew = await m_DownloadHelper.DownloadPurchasedMessageDRMVideo(
                                ytdlppath: Auth.YTDLP_PATH,
                                mp4decryptpath: Auth.MP4DECRYPT_PATH,
                                ffmpegpath: Auth.FFMPEG_PATH,
                                user_agent: Auth.USER_AGENT,
                                policy: policy,
                                signature: signature,
                                kvp: kvp,
                                sess: Auth.COOKIE,
                                url: mpdURL,
                                decryptionKey: decryptionKey,
                                folder: path,
                                lastModified: lastModified,
                                media_id: paidMessageKVP.Key,
                                task: task,
                                filenameFormat: !string.IsNullOrEmpty(Config.PaidMessageFileNameFormat) ? Config.PaidMessageFileNameFormat : string.Empty,
                                messageInfo: messageInfo,
                                messageMedia: mediaInfo,
                                fromUser: messageInfo.fromUser,
                                users: hasSelectedUsersKVP.Value,
                                config: Config);

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
                        Purchased.Medium? mediaInfo = paidMessageCollection.PaidMessageMedia.FirstOrDefault(m => m.id == paidMessageKVP.Key);
                        Purchased.List messageInfo = paidMessageCollection.PaidMessageObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

                        isNew = await m_DownloadHelper.DownloadPurchasedMedia(
                            url: paidMessageKVP.Value,
                            folder: path,
                            media_id: paidMessageKVP.Key,
                            task: task,
                            filenameFormat: !string.IsNullOrEmpty(Config.PaidMessageFileNameFormat) ? Config.PaidMessageFileNameFormat : string.Empty,
                            messageInfo: messageInfo,
                            messageMedia: mediaInfo,
                            fromUser: messageInfo.fromUser,
                            users: hasSelectedUsersKVP.Value,
                            config: Config);
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

        return paidMessagesCount;
    }

    private static async Task<int> DownloadMessages(KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, KeyValuePair<string, int> user, int messagesCount, string path)
    {
        AnsiConsole.Markup($"[red]Getting Messages\n[/]");
        //Dictionary<long, string> messages = await apiHelper.GetMedia(MediaType.Messages, $"/chats/{user.Value}/messages", null, path, auth, paid_post_ids);
        MessageCollection messages = await m_ApiHelper.GetMessages($"/chats/{user.Value}/messages", path, Auth!, Config!);
        int oldMessagesCount = 0;
        int newMessagesCount = 0;
        if (messages != null && messages.Messages.Count > 0)
        {
            AnsiConsole.Markup($"[red]Found {messages.Messages.Count} Messages\n[/]");
            messagesCount = messages.Messages.Count;
            long totalSize = await m_DownloadHelper.CalculateTotalFileSize(messages.Messages.Values.ToList(), Auth!);
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
                        string? pssh = await m_ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp, Auth);
                        if (pssh != null)
                        {
                            DateTime lastModified = await m_ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp, Auth);
                            Dictionary<string, string> drmHeaders = await m_ApiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/message/{messageId}", "?type=widevine", Auth);
                            string decryptionKey = await m_ApiHelper.GetDecryptionKeyNew(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh, Auth);
                            Messages.Medium? mediaInfo = messages.MessageMedia.FirstOrDefault(m => m.id == messageKVP.Key);
                            Messages.List? messageInfo = messages.MessageObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

                            isNew = await m_DownloadHelper.DownloadMessageDRMVideo(
                                ytdlppath: Auth.YTDLP_PATH,
                                mp4decryptpath: Auth.MP4DECRYPT_PATH,
                                ffmpegpath: Auth.FFMPEG_PATH,
                                user_agent: Auth.USER_AGENT,
                                policy: policy,
                                signature: signature,
                                kvp: kvp,
                                sess: Auth.COOKIE,
                                url: mpdURL,
                                decryptionKey: decryptionKey,
                                folder: path,
                                lastModified: lastModified,
                                media_id: messageKVP.Key,
                                task: task,
                                filenameFormat: !string.IsNullOrEmpty(Config.MessageFileNameFormat) ? Config.MessageFileNameFormat : string.Empty,
                                messageInfo: messageInfo,
                                messageMedia: mediaInfo,
                                fromUser: messageInfo.fromUser,
                                users: hasSelectedUsersKVP.Value,
                                config: Config);


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
                        Messages.Medium? mediaInfo = messages.MessageMedia.FirstOrDefault(m => m.id == messageKVP.Key);
                        Messages.List? messageInfo = messages.MessageObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

                        isNew = await m_DownloadHelper.DownloadMessageMedia(
                            url: messageKVP.Value,
                            folder: path,
                            media_id: messageKVP.Key,
                            task: task,
                            filenameFormat: !string.IsNullOrEmpty(Config!.MessageFileNameFormat) ? Config.MessageFileNameFormat : string.Empty,
                            messageInfo: messageInfo,
                            messageMedia: mediaInfo,
                            fromUser: messageInfo.fromUser,
                            users: hasSelectedUsersKVP.Value,
                            config: Config);

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

        return messagesCount;
    }

    private static async Task<int> DownloadHighlights(KeyValuePair<string, int> user, int highlightsCount, string path)
    {
        AnsiConsole.Markup($"[red]Getting Highlights\n[/]");
        Dictionary<long, string> highlights = await m_ApiHelper.GetMedia(MediaType.Highlights, $"/users/{user.Value}/stories/highlights", null, path, Auth!, Config!, paid_post_ids);
        int oldHighlightsCount = 0;
        int newHighlightsCount = 0;
        if (highlights != null && highlights.Count > 0)
        {
            AnsiConsole.Markup($"[red]Found {highlights.Count} Highlights\n[/]");
            highlightsCount = highlights.Count;
            long totalSize = await m_DownloadHelper.CalculateTotalFileSize(highlights.Values.ToList(), Auth!);
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
                    bool isNew = await m_DownloadHelper.DownloadStoryMedia(highlightKVP.Value, path, highlightKVP.Key, task);
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

        return highlightsCount;
    }

    private static async Task<int> DownloadStories(KeyValuePair<string, int> user, int storiesCount, string path)
    {
        AnsiConsole.Markup($"[red]Getting Stories\n[/]");
        Dictionary<long, string> stories = await m_ApiHelper.GetMedia(MediaType.Stories, $"/users/{user.Value}/stories", null, path, Auth!, Config!, paid_post_ids);
        int oldStoriesCount = 0;
        int newStoriesCount = 0;
        if (stories != null && stories.Count > 0)
        {
            AnsiConsole.Markup($"[red]Found {stories.Count} Stories\n[/]");
            storiesCount = stories.Count;
            long totalSize = await m_DownloadHelper.CalculateTotalFileSize(stories.Values.ToList(), Auth);
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
                    bool isNew = await m_DownloadHelper.DownloadStoryMedia(storyKVP.Value, path, storyKVP.Key, task);
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

        return storiesCount;
    }

    private static async Task<int> DownloadArchived(KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, KeyValuePair<string, int> user, int archivedCount, string path)
    {
        AnsiConsole.Markup($"[red]Getting Archived Posts\n[/]");
        //Dictionary<long, string> archived = await apiHelper.GetMedia(MediaType.Archived, $"/users/{user.Value}/posts", null, path, auth, paid_post_ids);
        ArchivedCollection archived = await m_ApiHelper.GetArchived($"/users/{user.Value}/posts", path, Auth!, Config!);
        int oldArchivedCount = 0;
        int newArchivedCount = 0;
        if (archived != null && archived.ArchivedPosts.Count > 0)
        {
            AnsiConsole.Markup($"[red]Found {archived.ArchivedPosts.Count} Archived Posts\n[/]");
            archivedCount = archived.ArchivedPosts.Count;
            long totalSize = await m_DownloadHelper.CalculateTotalFileSize(archived.ArchivedPosts.Values.ToList(), Auth);
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
                        string? pssh = await m_ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp, Auth);
                        if (pssh != null)
                        {
                            DateTime lastModified = await m_ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp, Auth);
                            Dictionary<string, string> drmHeaders = await m_ApiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine", Auth);
                            string decryptionKey = await m_ApiHelper.GetDecryptionKeyNew(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh, Auth);
                            Archived.Medium? mediaInfo = archived.ArchivedPostMedia.FirstOrDefault(m => m.id == archivedKVP.Key);
                            Archived.List? postInfo = archived.ArchivedPostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

                            isNew = await m_DownloadHelper.DownloadArchivedPostDRMVideo(
                                ytdlppath: Auth.YTDLP_PATH,
                                mp4decryptpath: Auth.MP4DECRYPT_PATH,
                                ffmpegpath: Auth.FFMPEG_PATH,
                                user_agent: Auth.USER_AGENT,
                                policy: policy,
                                signature: signature,
                                kvp: kvp,
                                sess: Auth.COOKIE,
                                url: mpdURL,
                                decryptionKey: decryptionKey,
                                folder: path,
                                lastModified: lastModified,
                                media_id: archivedKVP.Key,
                                task: task,
                                filenameFormat: !string.IsNullOrEmpty(Config.PostFileNameFormat) ? Config.PostFileNameFormat : string.Empty,
                                postInfo: !string.IsNullOrEmpty(Config.PostFileNameFormat) ? postInfo : null,
                                postMedia: !string.IsNullOrEmpty(Config.PostFileNameFormat) ? mediaInfo : null,
                                author: !string.IsNullOrEmpty(Config.PostFileNameFormat) ? postInfo.author : null,
                                users: hasSelectedUsersKVP.Value,
                                config: Config);

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
                        Archived.Medium? mediaInfo = archived.ArchivedPostMedia.FirstOrDefault(m => m.id == archivedKVP.Key);
                        Archived.List? postInfo = archived.ArchivedPostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

                        isNew = await m_DownloadHelper.DownloadArchivedMedia(
                            url: archivedKVP.Value,
                            folder: path,
                            media_id: archivedKVP.Key,
                            task: task,
                            filenameFormat: !string.IsNullOrEmpty(Config.PostFileNameFormat) ? Config.PostFileNameFormat : string.Empty,
                            messageInfo: !string.IsNullOrEmpty(Config.PostFileNameFormat) ? postInfo : null,
                            messageMedia: !string.IsNullOrEmpty(Config.PostFileNameFormat) ? mediaInfo : null,
                            author: !string.IsNullOrEmpty(Config.PostFileNameFormat) ? postInfo.author : null,
                            users: hasSelectedUsersKVP.Value,
                            config: Config);

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

        return archivedCount;
    }

    private static async Task<int> DownloadFreePosts(KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, KeyValuePair<string, int> user, int postCount, string path)
    {
        AnsiConsole.Markup($"[red]Getting Posts\n[/]");
        //Dictionary<long, string> posts = await apiHelper.GetMedia(MediaType.Posts, $"/users/{user.Value}/posts", null, path, auth, paid_post_ids);
        PostCollection posts = await m_ApiHelper.GetPosts($"/users/{user.Value}/posts", path, Auth!, Config!, paid_post_ids);
        int oldPostCount = 0;
        int newPostCount = 0;
        if (posts == null || posts.Posts.Count <= 0)
        {
            AnsiConsole.Markup($"[red]Found 0 Posts\n[/]");
            return 0;
        }
      
        AnsiConsole.Markup($"[red]Found {posts.Posts.Count} Posts\n[/]");
        postCount = posts.Posts.Count;
        long totalSize = await m_DownloadHelper.CalculateTotalFileSize(posts.Posts.Values.ToList(), Auth!);
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
                    string? pssh = await m_ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp, Auth);
                    if (pssh == null)
                    {
                        continue;
                    }

                    DateTime lastModified = await m_ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp, Auth);
                    Dictionary<string, string> drmHeaders = await m_ApiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine", Auth);
                    string decryptionKey = await m_ApiHelper.GetDecryptionKeyNew(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh, Auth);
                    Post.Medium mediaInfo = posts.PostMedia.FirstOrDefault(m => m.id == postKVP.Key);
                    Post.List postInfo = posts.PostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

                    isNew = await m_DownloadHelper.DownloadPostDRMVideo(
                        ytdlppath: Auth.YTDLP_PATH,
                        mp4decryptpath: Auth.MP4DECRYPT_PATH,
                        ffmpegpath: Auth.FFMPEG_PATH,
                        user_agent: Auth.USER_AGENT,
                        policy: policy,
                        signature: signature,
                        kvp: kvp,
                        sess: Auth.COOKIE,
                        url: mpdURL,
                        decryptionKey: decryptionKey,
                        folder: path,
                        lastModified: lastModified,
                        media_id: postKVP.Key,
                        task: task,
                        filenameFormat: !string.IsNullOrEmpty(Config.PostFileNameFormat) ? Config.PostFileNameFormat : string.Empty,
                        postInfo: postInfo,
                        postMedia: mediaInfo,
                        author: postInfo?.author,
                        users: hasSelectedUsersKVP.Value,
                        config: Config);
                    if (isNew)
                    {
                        newPostCount++;
                    }
                    else
                    {
                        oldPostCount++;
                    }
                }
                else
                {
                    try
                    {
                        Post.Medium? mediaInfo = posts.PostMedia.FirstOrDefault(m => (m?.id == postKVP.Key) == true);
                        Post.List? postInfo = posts.PostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

                        isNew = await m_DownloadHelper.DownloadPostMedia(
                            url: postKVP.Value,
                            folder: path,
                            media_id: postKVP.Key,
                            task: task,
                            filenameFormat: !string.IsNullOrEmpty(Config.PostFileNameFormat) ? Config.PostFileNameFormat : string.Empty,
                            postInfo: postInfo,
                            postMedia: mediaInfo,
                            author: postInfo?.author,
                            users: hasSelectedUsersKVP.Value,
                            config: Config);
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

        return postCount;
    }

    private static async Task<int> DownloadPaidPosts(KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, KeyValuePair<string, int> user, int paidPostCount, string path)
    {
        AnsiConsole.Markup($"[red]Getting Paid Posts\n[/]");
        //Dictionary<long, string> purchasedPosts = await apiHelper.GetMedia(MediaType.PaidPosts, "/posts/paid", user.Key, path, auth, paid_post_ids);
        PaidPostCollection purchasedPosts = await m_ApiHelper.GetPaidPosts("/posts/paid", path, user.Key, Auth!, Config!, paid_post_ids);
        int oldPaidPostCount = 0;
        int newPaidPostCount = 0;
        if (purchasedPosts == null || purchasedPosts.PaidPosts.Count <= 0)
        {
            AnsiConsole.Markup($"[red]Found 0 Paid Posts\n[/]");
            return 0;
        }

        AnsiConsole.Markup($"[red]Found {purchasedPosts.PaidPosts.Count} Paid Posts\n[/]");
        paidPostCount = purchasedPosts.PaidPosts.Count;
        long totalSize = await m_DownloadHelper.CalculateTotalFileSize(purchasedPosts.PaidPosts.Values.ToList(), Auth);
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
                    string? pssh = await m_ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp, Auth);
                    if (pssh == null)
                    {
                        continue;
                    }

                    DateTime lastModified = await m_ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp, Auth);
                    Dictionary<string, string> drmHeaders = await m_ApiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine", Auth);
                    string decryptionKey = await m_ApiHelper.GetDecryptionKeyNew(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh, Auth);
                    Purchased.Medium? mediaInfo = purchasedPosts.PaidPostMedia.FirstOrDefault(m => m.id == purchasedPostKVP.Key);
                    Purchased.List? postInfo = purchasedPosts.PaidPostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

                    isNew = await m_DownloadHelper.DownloadPurchasedPostDRMVideo(
                        ytdlppath: Auth.YTDLP_PATH,
                        mp4decryptpath: Auth.MP4DECRYPT_PATH,
                        ffmpegpath: Auth.FFMPEG_PATH,
                        user_agent: Auth.USER_AGENT,
                        policy: policy,
                        signature: signature,
                        kvp: kvp,
                        sess: Auth.COOKIE,
                        url: mpdURL,
                        decryptionKey: decryptionKey,
                        folder: path,
                        lastModified: lastModified,
                        media_id: purchasedPostKVP.Key,
                        task: task,
                        filenameFormat: !string.IsNullOrEmpty(Config.PaidPostFileNameFormat) ? Config.PaidPostFileNameFormat : string.Empty,
                        postInfo: !string.IsNullOrEmpty(Config.PaidPostFileNameFormat) ? postInfo : null,
                        postMedia: !string.IsNullOrEmpty(Config.PaidPostFileNameFormat) ? mediaInfo : null,
                        fromUser: !string.IsNullOrEmpty(Config.PaidPostFileNameFormat) ? postInfo.fromUser : null,
                        users: hasSelectedUsersKVP.Value,
                        config: Config);
                    if (isNew)
                    {
                        newPaidPostCount++;
                    }
                    else
                    {
                        oldPaidPostCount++;
                    }
                }
                else
                {
                    Purchased.Medium mediaInfo = purchasedPosts.PaidPostMedia.FirstOrDefault(m => m.id == purchasedPostKVP.Key);
                    Purchased.List postInfo = purchasedPosts.PaidPostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

                    isNew = await m_DownloadHelper.DownloadPurchasedPostMedia(
                        url: purchasedPostKVP.Value,
                        folder: path,
                        media_id: purchasedPostKVP.Key,
                        task: task,
                        filenameFormat: !string.IsNullOrEmpty(Config.PaidPostFileNameFormat) ? Config.PaidPostFileNameFormat : string.Empty,
                        messageInfo: !string.IsNullOrEmpty(Config.PaidPostFileNameFormat) ? postInfo : null,
                        messageMedia: !string.IsNullOrEmpty(Config.PaidPostFileNameFormat) ? mediaInfo : null,
                        fromUser: !string.IsNullOrEmpty(Config.PaidPostFileNameFormat) ? postInfo.fromUser : null,
                        users: hasSelectedUsersKVP.Value,
                        config: Config);
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

        return paidPostCount;
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
                            List<string> listUsernames = new();
                            foreach (var item in listSelection)
                            {
                                int listId = lists[item.Replace("[red]", "").Replace("[/]", "")];
                                List<string> usernames = await m_ApiHelper.GetListUsers($"/lists/{listId}/users", Auth);
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
                            ( "[red]DownloadAvatarHeaderPhoto[/]", Config.DownloadAvatarHeaderPhoto),
                            ( "[red]DownloadPaidPosts[/]", Config.DownloadPaidPosts ),
                            ( "[red]DownloadPosts[/]",  Config.DownloadPosts ),
                            ( "[red]DownloadArchived[/]", Config.DownloadArchived ),
                            ( "[red]DownloadStories[/]", Config.DownloadStories ),
                            ( "[red]DownloadHighlights[/]", Config.DownloadHighlights ),
                            ( "[red]DownloadMessages[/]", Config.DownloadMessages ),
                            ( "[red]DownloadPaidMessages[/]", Config.DownloadPaidMessages ),
                            ( "[red]DownloadImages[/]", Config.DownloadImages ),
                            ( "[red]DownloadVideos[/]", Config.DownloadVideos ),
                            ( "[red]DownloadAudios[/]", Config.DownloadAudios ),
                            ( "[red]IncludeExpiredSubscriptions[/]", Config.IncludeExpiredSubscriptions ),
                            ( "[red]SkipAds[/]", Config.SkipAds ),
                            ( "[red]FolderPerPaidPost[/]", Config.FolderPerPaidPost ),
                            ( "[red]FolderPerPost[/]", Config.FolderPerPost ),
                            ( "[red]FolderPerPaidMessage[/]", Config.FolderPerPaidMessage ),
                            ( "[red]FolderPerMessage[/]", Config.FolderPerMessage )
                        });

                        MultiSelectionPrompt<string> multiSelectionPrompt = new MultiSelectionPrompt<string>()
                            .Title("[red]Edit config.json[/]")
                            .PageSize(18);

                        foreach(var choice in choices)
                        {
                            multiSelectionPrompt.AddChoices(choice.choice, (selectionItem) => { if (choice.isSelected) selectionItem.Select(); });
                        }

                        var configOptions = AnsiConsole.Prompt(multiSelectionPrompt);

                        if(configOptions.Contains("[red]Go Back[/]"))
                        {
                            break;
                        }

                        Config newConfig = new()
                        {
                            DownloadPath = Config.DownloadPath,
                            PostFileNameFormat = Config.PostFileNameFormat,
                            MessageFileNameFormat = Config.MessageFileNameFormat,
                            PaidPostFileNameFormat = Config.PaidPostFileNameFormat,
                            PaidMessageFileNameFormat = Config.PaidMessageFileNameFormat,
                            RenameExistingFilesWhenCustomFormatIsSelected = Config.RenameExistingFilesWhenCustomFormatIsSelected,
                            Timeout = Config.Timeout,
                            DownloadAvatarHeaderPhoto = configOptions.Contains("[red]DownloadAvatarHeaderPhoto[/]"),
                            DownloadPaidPosts = configOptions.Contains("[red]DownloadPaidPosts[/]"),
                            DownloadPosts = configOptions.Contains("[red]DownloadPosts[/]"),
                            DownloadArchived = configOptions.Contains("[red]DownloadArchived[/]"),
                            DownloadStories = configOptions.Contains("[red]DownloadStories[/]"),
                            DownloadHighlights = configOptions.Contains("[red]DownloadHighlights[/]"),
                            DownloadMessages = configOptions.Contains("[red]DownloadMessages[/]"),
                            DownloadPaidMessages = configOptions.Contains("[red]DownloadPaidMessages[/]"),
                            DownloadImages = configOptions.Contains("[red]DownloadImages[/]"),
                            DownloadVideos = configOptions.Contains("[red]DownloadVideos[/]"),
                            DownloadAudios = configOptions.Contains("[red]DownloadAudios[/]"),
                            IncludeExpiredSubscriptions = configOptions.Contains("[red]IncludeExpiredSubscriptions[/]"),
                            SkipAds = configOptions.Contains("[red]SkipAds[/]"),
                            FolderPerPaidPost = configOptions.Contains("[red]FolderPerPaidPost[/]"),
                            FolderPerPost = configOptions.Contains("[red]FolderPerPost[/]"),
                            FolderPerPaidMessage = configOptions.Contains("[red]FolderPerPaidMessage[/]"),
                            FolderPerMessage = configOptions.Contains("[red]FolderPerMessage[/]")
                        };


                        string newConfigString = JsonConvert.SerializeObject(newConfig, Formatting.Indented);
                        File.WriteAllText("config.json", newConfigString);
                        if (Config.IncludeExpiredSubscriptions != Config.IncludeExpiredSubscriptions)
                        {
                            Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
                            return new KeyValuePair<bool, Dictionary<string, int>>(true, new Dictionary<string, int> { { "ConfigChanged", 0 } });
                        }
                        Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
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
