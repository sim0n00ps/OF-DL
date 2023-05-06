using Newtonsoft.Json;
using OF_DL.Entities;
using OF_DL.Enumurations;
using OF_DL.Helpers;
using Spectre.Console;

namespace OF_DL
{
	public class Program
	{
		private readonly APIHelper apiHelper;
		private readonly DownloadHelper downloadHelper;
		public Program(APIHelper _aPIHelper, DownloadHelper _downloadHelper)
		{
			apiHelper = _aPIHelper;
			downloadHelper = _downloadHelper;
		}

		public Auth auth = JsonConvert.DeserializeObject<Auth>(File.ReadAllText("auth.json"));
		public int MAX_AGE = 0;
		public List<long> paid_post_ids = new List<long>();
		public async static Task Main()
		{
			AnsiConsole.Write(new FigletText("Welcome to OF-DL").Color(Color.Red));
			Program program = new Program(new APIHelper(), new DownloadHelper());
			DateTime startTime = DateTime.Now;

			//Check if yt-dlp, ffmpeg and mp4decrypt paths are valid
			if(!File.Exists(program.auth.YTDLP_PATH))
			{
				AnsiConsole.Markup($"[red]Cannot locate yt-dlp.exe with specified path {program.auth.YTDLP_PATH}, please modify auth.json with the correct path, press any key to exit[/]");
				Console.ReadKey();
				Environment.Exit(0);
			}
			else
			{
				AnsiConsole.Markup($"[green]yt-dlp.exe located successfully![/]\n");
			}
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

			//Check if auth is valid
			User validate = await program.apiHelper.GetUserInfo($"/users/me");
			if (validate.name == null && validate.username == null)
			{
				AnsiConsole.Markup($"[red]Auth failed, please check the values in auth.json are correct, press any key to exit[/]");
				Console.ReadKey();
			}
			else
			{
				AnsiConsole.Markup($"[green]Logged In successfully as {validate.name} {validate.username}\n[/]");
				Dictionary<string, int> users = await program.apiHelper.GetSubscriptions("/subscriptions/subscribes");

				//User Selection
				var options = new List<string>
				{
					"[red]Select All[/]",
					"[red]Custom[/]"
				};

				var selectedOption = AnsiConsole.Prompt(
					new SelectionPrompt<string>()
						.Title("[red]Select Accounts to Scrape | Select All = All Accounts | Custom = Specific Account(s)[/]")
						.AddChoices(options)
				);

				Dictionary<string, int> selectedUsers = new Dictionary<string, int>();

				if (selectedOption == "[red]Select All[/]")
				{
					selectedUsers = users;
				}
				else if (selectedOption == "[red]Custom[/]")
				{
					var selectedNames = AnsiConsole.Prompt(
						new MultiSelectionPrompt<string>()
							.Title("[red]Select users[/]")
							.PageSize(10)
							.AddChoices(users.Keys.Select(k => $"[red]{k}[/]").ToList())
					);
					selectedUsers = users.Where(x => selectedNames.Contains($"[red]{x.Key}[/]")).ToDictionary(x => x.Key, x => x.Value);
				}

				//Iterate over each user in the list of users
				foreach (KeyValuePair<string, int> user in selectedUsers)
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

					User user_info = await program.apiHelper.GetUserInfo($"/users/{user.Key}");
					await program.downloadHelper.DownloadAvatarHeader(user_info.avatar, user_info.header, path);

					AnsiConsole.Markup($"[red]Getting Paid Posts\n[/]");
					List<string> purchasedPosts = await program.apiHelper.GetMedia(MediaType.PaidPosts, "/posts/paid", user.Key);

					int oldPaidPostCount = 0;
					int newPaidPostCount = 0;
					if (purchasedPosts != null && purchasedPosts.Count > 0)
					{
						AnsiConsole.Markup($"[red]Found {purchasedPosts.Count} Paid Posts\n[/]");
						paidPostCount = purchasedPosts.Count;
						await AnsiConsole.Progress()
						.Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
						.StartAsync(async ctx =>
						{
							// Define tasks
							var task = ctx.AddTask($"[red]Downloading {purchasedPosts.Count} Paid Posts[/]", autoStart: false);
							task.MaxValue = purchasedPosts.Count;
							task.StartTask();
							foreach (string purchasedPosturl in purchasedPosts)
							{
								bool isNew;
								if (purchasedPosturl.Contains("cdn3.onlyfans.com/dash/files"))
								{
									string[] messageUrlParsed = purchasedPosturl.Split(',');
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
										isNew = await program.downloadHelper.DownloadPurchasedPostDRMVideo(program.auth.YTDLP_PATH, program.auth.MP4DECRYPT_PATH, program.auth.FFMPEG_PATH, program.auth.USER_AGENT, policy, signature, kvp, program.auth.COOKIE, mpdURL, decryptionKey, path, lastModified);
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
									isNew = await program.downloadHelper.DownloadPurchasedPostMedia(purchasedPosturl, path);
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

					AnsiConsole.Markup($"[red]Getting Posts\n[/]");
					List<string> posts = await program.apiHelper.GetMedia(MediaType.Posts, $"/users/{user.Value}/posts", null);
					int oldPostCount = 0;
					int newPostCount = 0;
					if (posts != null && posts.Count > 0)
					{
						AnsiConsole.Markup($"[red]Found {posts.Count} Posts\n[/]");
						postCount = posts.Count;
						await AnsiConsole.Progress()
						.Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
						.StartAsync(async ctx =>
						{
							var task = ctx.AddTask($"[red]Downloading {posts.Count} Posts[/]", autoStart: false);
							task.MaxValue = posts.Count;
							task.StartTask();
							foreach (string postUrl in posts)
							{
								bool isNew;
								if (postUrl.Contains("cdn3.onlyfans.com/dash/files"))
								{
									string[] messageUrlParsed = postUrl.Split(',');
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
										isNew = await program.downloadHelper.DownloadPostDRMVideo(program.auth.YTDLP_PATH, program.auth.MP4DECRYPT_PATH, program.auth.FFMPEG_PATH, program.auth.USER_AGENT, policy, signature, kvp, program.auth.COOKIE, mpdURL, decryptionKey, path, lastModified);
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
									isNew = await program.downloadHelper.DownloadPostMedia(postUrl, path);
									if (isNew)
									{
										newPostCount++;
									}
									else
									{
										oldPostCount++;
									}
								}
								task.Increment(1.0);
							}
							task.StopTask();
						});
						AnsiConsole.Markup($"[red]Posts Already Downloaded: {oldPostCount} New Posts Downloaded: {newPostCount}[/]\n");
					}
					else
					{
						AnsiConsole.Markup($"[red]Found 0 Posts\n[/]");
					}

					AnsiConsole.Markup($"[red]Getting Archived Posts\n[/]");
					List<string> archived = await program.apiHelper.GetMedia(MediaType.Archived, $"/users/{user.Value}/posts/archived", null);

					int oldArchivedCount = 0;
					int newArchivedCount = 0;
					if (archived != null && archived.Count > 0)
					{
						AnsiConsole.Markup($"[red]Found {archived.Count} Archived Posts\n[/]");
						archivedCount = archived.Count;
						await AnsiConsole.Progress()
						.Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
						.StartAsync(async ctx =>
						{
							// Define tasks
							var task = ctx.AddTask($"[red]Downloading {archived.Count} Archived Posts[/]", autoStart: false);
							task.MaxValue = archived.Count;
							task.StartTask();
							foreach (string archivedUrl in archived)
							{
								bool isNew = await program.downloadHelper.DownloadArchivedMedia(archivedUrl, path);
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

					AnsiConsole.Markup($"[red]Getting Stories\n[/]");
					List<string> stories = await program.apiHelper.GetMedia(MediaType.Stories, $"/users/{user.Value}/stories", null);
					int oldStoriesCount = 0;
					int newStoriesCount = 0;
					if (stories != null && stories.Count > 0)
					{
						AnsiConsole.Markup($"[red]Found {stories.Count} Stories\n[/]");
						storiesCount = stories.Count;
						await AnsiConsole.Progress()
						.Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
						.StartAsync(async ctx =>
						{
							// Define tasks
							var task = ctx.AddTask($"[red]Downloading {stories.Count} Stories[/]", autoStart: false);
							task.MaxValue = stories.Count;
							task.StartTask();
							foreach (string storyUrl in stories)
							{
								bool isNew = await program.downloadHelper.DownloadStoryMedia(storyUrl, path);
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

					AnsiConsole.Markup($"[red]Getting Highlights\n[/]");
					List<string> highlights = await program.apiHelper.GetMedia(MediaType.Highlights, $"/users/{user.Value}/stories/highlights", null);
					int oldHighlightsCount = 0;
					int newHighlightsCount = 0;
					if (highlights != null && highlights.Count > 0)
					{
						AnsiConsole.Markup($"[red]Found {highlights.Count} Highlights\n[/]");
						highlightsCount = highlights.Count;
						await AnsiConsole.Progress()
						.Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
						.StartAsync(async ctx =>
						{
							// Define tasks
							var task = ctx.AddTask($"[red]Downloading {highlights.Count} Highlights[/]", autoStart: false);
							task.MaxValue = highlights.Count;
							task.StartTask();
							foreach (string highlightUrl in highlights)
							{
								bool isNew = await program.downloadHelper.DownloadStoryMedia(highlightUrl, path);
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

					AnsiConsole.Markup($"[red]Getting Messages\n[/]");
					List<string> messages = await program.apiHelper.GetMedia(MediaType.Messages, $"/chats/{user.Value}/messages", null);
					int oldMessagesCount = 0;
					int newMessagesCount = 0;
					if (messages != null && messages.Count > 0)
					{
						AnsiConsole.Markup($"[red]Found {messages.Count} Messages\n[/]");
						messagesCount = messages.Count;
						await AnsiConsole.Progress()
						.Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
						.StartAsync(async ctx =>
						{
							// Define tasks
							var task = ctx.AddTask($"[red]Downloading {messages.Count} Messages[/]", autoStart: false);
							task.MaxValue = messages.Count;
							task.StartTask();
							foreach (string messageUrl in messages)
							{
								bool isNew;
								if (messageUrl.Contains("cdn3.onlyfans.com/dash/files"))
								{
									string[] messageUrlParsed = messageUrl.Split(',');
									string mpdURL = messageUrlParsed[0];
									string policy = messageUrlParsed[1];
									string signature = messageUrlParsed[2];
									string kvp = messageUrlParsed[3];
									string mediaId = messageUrlParsed[4];
									string messageId = messageUrlParsed[5];
									string? licenseURL = null;
									string? pssh = await program.apiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
									if(pssh != null)
									{
										DateTime lastModified = await program.apiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
										Dictionary<string, string> drmHeaders = await program.apiHelper.Headers($"/api2/v2/users/media/{mediaId}/drm/message/{messageId}", "?type=widevine");
										string decryptionKey = await program.apiHelper.GetDecryptionKey(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh);
										isNew = await program.downloadHelper.DownloadMessageDRMVideo(program.auth.YTDLP_PATH, program.auth.MP4DECRYPT_PATH, program.auth.FFMPEG_PATH, program.auth.USER_AGENT, policy, signature, kvp, program.auth.COOKIE, mpdURL, decryptionKey, path, lastModified);
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
									isNew = await program.downloadHelper.DownloadMessageMedia(messageUrl, path);
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

					AnsiConsole.Markup($"[red]Getting Paid Messages\n[/]");
					List<string> purchased = await program.apiHelper.GetMedia(MediaType.PaidMessages, "/posts/paid", user.Key);

					int oldPaidMessagesCount = 0;
					int newPaidMessagesCount = 0;
					if (purchased != null && purchased.Count > 0)
					{
						AnsiConsole.Markup($"[red]Found {purchased.Count} Paid Messages\n[/]");
						paidMessagesCount = purchased.Count;
						await AnsiConsole.Progress()
						.Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
						.StartAsync(async ctx =>
						{
							// Define tasks
							var task = ctx.AddTask($"[red]Downloading {purchased.Count} Paid Messages[/]", autoStart: false);
							task.MaxValue = purchased.Count;
							task.StartTask();
							foreach (string paidmessagesUrl in purchased)
							{
								bool isNew;
								if (paidmessagesUrl.Contains("cdn3.onlyfans.com/dash/files"))
								{
									string[] messageUrlParsed = paidmessagesUrl.Split(',');
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
										isNew = await program.downloadHelper.DownloadPurchasedMessageDRMVideo(program.auth.YTDLP_PATH, program.auth.MP4DECRYPT_PATH, program.auth.FFMPEG_PATH, program.auth.USER_AGENT, policy, signature, kvp, program.auth.COOKIE, mpdURL, decryptionKey, path, lastModified);
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
									isNew = await program.downloadHelper.DownloadPurchasedMedia(paidmessagesUrl, path);
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
			}
			DateTime endTime = DateTime.Now;
			TimeSpan totalTime = endTime - startTime;
			AnsiConsole.Markup($"[green]Scrape Completed in {totalTime.TotalMinutes.ToString("0.00")} minutes, Press any key to exit![/]");
			Console.ReadKey();
		}
	}
}