using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Purchased;
using OF_DL.Entities.Streams;
using OF_DL.Enumerations;
using OF_DL.Enumurations;
using OF_DL.Helpers;
using Octokit;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Spectre.Console;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static OF_DL.Entities.Messages.Messages;
using Akka.Configuration;
using System.Text;

namespace OF_DL;

public class Program
{
	public int MAX_AGE = 0;
	public static List<long> paid_post_ids = new();

	private static bool clientIdBlobMissing = false;
	private static bool devicePrivateKeyMissing = false;
	private static Entities.Config? config = null;
	private static Auth? auth = null;
	private static LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch();

	private static async Task LoadAuthFromBrowser()
	{
		bool runningInDocker = Environment.GetEnvironmentVariable("OFDL_DOCKER") != null;

		try
		{
			AuthHelper authHelper = new();
			Task setupBrowserTask = authHelper.SetupBrowser(runningInDocker);

			Task.Delay(1000).Wait();
			if (!setupBrowserTask.IsCompleted)
			{
				AnsiConsole.MarkupLine($"[yellow]Downloading dependencies. Please wait ...[/]");
			}
			setupBrowserTask.Wait();

			Task<Auth?> getAuthTask = authHelper.GetAuthFromBrowser();
			Task.Delay(5000).Wait();
			if (!getAuthTask.IsCompleted)
			{
				if (runningInDocker)
				{
					AnsiConsole.MarkupLine(
						"[yellow]In your web browser, navigate to the port forwarded from your docker container.[/]");
					AnsiConsole.MarkupLine(
						"[yellow]For instance, if your docker run command included \"-p 8080:8080\", open your web browser to \"http://localhost:8080\".[/]");
					AnsiConsole.MarkupLine("[yellow]Once on that webpage, please use it to log in to your OF account. Do not navigate away from the page.[/]");
				}
				else
				{
                    AnsiConsole.MarkupLine($"[yellow]In the new window that has opened, please log in to your OF account. Do not close the window or tab. Do not navigate away from the page.[/]\n");
                    AnsiConsole.MarkupLine($"[yellow]Note: Some users have reported that \"Sign in with Google\" has not been working with the new authentication method.[/]");
                    AnsiConsole.MarkupLine($"[yellow]If you use this method or encounter other issues while logging in, use one of the legacy authentication methods documented here:[/]");
                    AnsiConsole.MarkupLine($"[link]https://sim0n00ps.github.io/OF-DL/docs/config/auth#legacy-methods[/]");
                }
			}
			auth = await getAuthTask;
		}
		catch (Exception e)
		{
			AnsiConsole.MarkupLine($"\n[red]Authentication failed. Be sure to log into to OF using the new window that opened automatically.[/]");
			AnsiConsole.MarkupLine($"[red]The window will close automatically when the authentication process is finished.[/]");
			AnsiConsole.MarkupLine($"[red]If the problem persists, you may want to try using a legacy authentication method documented here:[/]\n");
			AnsiConsole.MarkupLine($"[link]https://sim0n00ps.github.io/OF-DL/docs/config/auth#legacy-methods[/]\n");
			AnsiConsole.MarkupLine($"[red]Press any key to exit.[/]");
			Log.Error(e, "auth invalid after attempt to get auth from browser");

			Environment.Exit(2);
		}

		if (auth == null)
		{
			AnsiConsole.MarkupLine($"\n[red]Authentication failed. Be sure to log into to OF using the new window that opened automatically.[/]");
			AnsiConsole.MarkupLine($"[red]The window will close automatically when the authentication process is finished.[/]");
			AnsiConsole.MarkupLine($"[red]If the problem persists, you may want to try using a legacy authentication method documented here:[/]\n");
			AnsiConsole.MarkupLine($"[link]https://sim0n00ps.github.io/OF-DL/docs/config/auth#legacy-methods[/]\n");
			AnsiConsole.MarkupLine($"[red]Press any key to exit.[/]");
			Log.Error("auth invalid after attempt to get auth from browser");

			Environment.Exit(2);
		}
		else
		{
			await File.WriteAllTextAsync("auth.json", JsonConvert.SerializeObject(auth, Formatting.Indented));
		}
	}

	public static async Task Main(string[] args)
	{
		bool cliNonInteractive = false;

		try
		{
			levelSwitch.MinimumLevel = LogEventLevel.Error; //set initial level (until we've read from config)

			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.ControlledBy(levelSwitch)
				.WriteTo.File("logs/OFDL.txt", rollingInterval: RollingInterval.Day)
				.CreateLogger();

			AnsiConsole.Write(new FigletText("Welcome to OF-DL").Color(Color.Red));

            //Remove config.json and convert to config.conf
            if (File.Exists("config.json"))
            {
                AnsiConsole.Markup("[green]config.json located successfully!\n[/]");
                try
                {
                    string jsonText = File.ReadAllText("config.json");
                    var jsonConfig = JsonConvert.DeserializeObject<Entities.Config>(jsonText);

                    if (jsonConfig != null)
                    {
                        var hoconConfig = new StringBuilder();
                        hoconConfig.AppendLine("# External Tools");
                        hoconConfig.AppendLine("External {");
                        hoconConfig.AppendLine($"  FFmpegPath = \"{jsonConfig.FFmpegPath}\"");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Download Settings");
                        hoconConfig.AppendLine("Download {");
                        hoconConfig.AppendLine("  Media {");
                        hoconConfig.AppendLine($"    DownloadAvatarHeaderPhoto = {jsonConfig.DownloadAvatarHeaderPhoto.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadPaidPosts = {jsonConfig.DownloadPaidPosts.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadPosts = {jsonConfig.DownloadPosts.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadArchived = {jsonConfig.DownloadArchived.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadStreams = {jsonConfig.DownloadStreams.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadStories = {jsonConfig.DownloadStories.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadHighlights = {jsonConfig.DownloadHighlights.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadMessages = {jsonConfig.DownloadMessages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadPaidMessages = {jsonConfig.DownloadPaidMessages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadImages = {jsonConfig.DownloadImages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadVideos = {jsonConfig.DownloadVideos.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadAudios = {jsonConfig.DownloadAudios.ToString().ToLower()}");
                        hoconConfig.AppendLine("  }");
                        hoconConfig.AppendLine($"  IgnoreOwnMessages = {jsonConfig.IgnoreOwnMessages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadPostsIncrementally = {jsonConfig.DownloadPostsIncrementally.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  BypassContentForCreatorsWhoNoLongerExist = {jsonConfig.BypassContentForCreatorsWhoNoLongerExist.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadDuplicatedMedia = {jsonConfig.DownloadDuplicatedMedia.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  SkipAds = {jsonConfig.SkipAds.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadPath = \"{jsonConfig.DownloadPath}\"");
                        hoconConfig.AppendLine($"  DownloadOnlySpecificDates = {jsonConfig.DownloadOnlySpecificDates.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadDateSelection = \"{jsonConfig.DownloadDateSelection.ToString().ToLower()}\"");
                        hoconConfig.AppendLine($"  CustomDate = \"{jsonConfig.CustomDate?.ToString("yyyy-MM-dd")}\"");
                        hoconConfig.AppendLine($"  ShowScrapeSize = {jsonConfig.ShowScrapeSize.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# File Settings");
                        hoconConfig.AppendLine("File {");
                        hoconConfig.AppendLine($"  PaidPostFileNameFormat = \"{jsonConfig.PaidPostFileNameFormat}\"");
                        hoconConfig.AppendLine($"  PostFileNameFormat = \"{jsonConfig.PostFileNameFormat}\"");
                        hoconConfig.AppendLine($"  PaidMessageFileNameFormat = \"{jsonConfig.PaidMessageFileNameFormat}\"");
                        hoconConfig.AppendLine($"  MessageFileNameFormat = \"{jsonConfig.MessageFileNameFormat}\"");
                        hoconConfig.AppendLine($"  RenameExistingFilesWhenCustomFormatIsSelected = {jsonConfig.RenameExistingFilesWhenCustomFormatIsSelected.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Creator-Specific Configurations");
                        hoconConfig.AppendLine("CreatorConfigs {");
                        foreach (var creatorConfig in jsonConfig.CreatorConfigs)
                        {
                            hoconConfig.AppendLine($"  \"{creatorConfig.Key}\" {{");
                            hoconConfig.AppendLine($"    PaidPostFileNameFormat = \"{creatorConfig.Value.PaidPostFileNameFormat}\"");
                            hoconConfig.AppendLine($"    PostFileNameFormat = \"{creatorConfig.Value.PostFileNameFormat}\"");
                            hoconConfig.AppendLine($"    PaidMessageFileNameFormat = \"{creatorConfig.Value.PaidMessageFileNameFormat}\"");
                            hoconConfig.AppendLine($"    MessageFileNameFormat = \"{creatorConfig.Value.MessageFileNameFormat}\"");
                            hoconConfig.AppendLine("  }");
                        }
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Folder Settings");
                        hoconConfig.AppendLine("Folder {");
                        hoconConfig.AppendLine($"  FolderPerPaidPost = {jsonConfig.FolderPerPaidPost.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  FolderPerPost = {jsonConfig.FolderPerPost.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  FolderPerPaidMessage = {jsonConfig.FolderPerPaidMessage.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  FolderPerMessage = {jsonConfig.FolderPerMessage.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Subscription Settings");
                        hoconConfig.AppendLine("Subscriptions {");
                        hoconConfig.AppendLine($"  IncludeExpiredSubscriptions = {jsonConfig.IncludeExpiredSubscriptions.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  IncludeRestrictedSubscriptions = {jsonConfig.IncludeRestrictedSubscriptions.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  IgnoredUsersListName = \"{jsonConfig.IgnoredUsersListName}\"");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Interaction Settings");
                        hoconConfig.AppendLine("Interaction {");
                        hoconConfig.AppendLine($"  NonInteractiveMode = {jsonConfig.NonInteractiveMode.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  NonInteractiveModeListName = \"{jsonConfig.NonInteractiveModeListName}\"");
                        hoconConfig.AppendLine($"  NonInteractiveModePurchasedTab = {jsonConfig.NonInteractiveModePurchasedTab.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Performance Settings");
                        hoconConfig.AppendLine("Performance {");
						hoconConfig.AppendLine($"  Timeout = {(jsonConfig.Timeout.HasValue ? jsonConfig.Timeout.Value : -1)}");
                        hoconConfig.AppendLine($"  LimitDownloadRate = {jsonConfig.LimitDownloadRate.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadLimitInMbPerSec = {jsonConfig.DownloadLimitInMbPerSec}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Logging/Debug Settings");
                        hoconConfig.AppendLine("Logging {");
                        hoconConfig.AppendLine($"  LoggingLevel = \"{jsonConfig.LoggingLevel.ToString().ToLower()}\"");
                        hoconConfig.AppendLine("}");

                        File.WriteAllText("config.conf", hoconConfig.ToString());
                        File.Delete("config.json");
                        AnsiConsole.Markup("[green]config.conf created successfully from config.json!\n[/]");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    AnsiConsole.MarkupLine($"\n[red]config.conf is not valid, check your syntax![/]\n");
                    AnsiConsole.MarkupLine($"[red]Press any key to exit.[/]");
                    Log.Error("config.conf processing failed.", e.Message);

                    if (!cliNonInteractive)
                    {
                        Console.ReadKey();
                    }
                    Environment.Exit(3);
                }
            }

            //I dont like it... but I needed to move config here, otherwise the logging level gets changed too late after we missed a whole bunch of important info
            if (File.Exists("config.conf"))
			{
				AnsiConsole.Markup("[green]config.conf located successfully!\n[/]");
				try
				{
					string hoconText = File.ReadAllText("config.conf");

					var hoconConfig = ConfigurationFactory.ParseString(hoconText);

					config = new Entities.Config
					{
                        // FFmpeg Settings
                        FFmpegPath = hoconConfig.GetString("External.FFmpegPath"),

						// Download Settings
						DownloadAvatarHeaderPhoto = hoconConfig.GetBoolean("Download.Media.DownloadAvatarHeaderPhoto"),
						DownloadPaidPosts = hoconConfig.GetBoolean("Download.Media.DownloadPaidPosts"),
						DownloadPosts = hoconConfig.GetBoolean("Download.Media.DownloadPosts"),
						DownloadArchived = hoconConfig.GetBoolean("Download.Media.DownloadArchived"),
						DownloadStreams = hoconConfig.GetBoolean("Download.Media.DownloadStreams"),
						DownloadStories = hoconConfig.GetBoolean("Download.Media.DownloadStories"),
						DownloadHighlights = hoconConfig.GetBoolean("Download.Media.DownloadHighlights"),
						DownloadMessages = hoconConfig.GetBoolean("Download.Media.DownloadMessages"),
						DownloadPaidMessages = hoconConfig.GetBoolean("Download.Media.DownloadPaidMessages"),
						DownloadImages = hoconConfig.GetBoolean("Download.Media.DownloadImages"),
						DownloadVideos = hoconConfig.GetBoolean("Download.Media.DownloadVideos"),
						DownloadAudios = hoconConfig.GetBoolean("Download.Media.DownloadAudios"),
						IgnoreOwnMessages = hoconConfig.GetBoolean("Download.IgnoreOwnMessages"),
						DownloadPostsIncrementally = hoconConfig.GetBoolean("Download.DownloadPostsIncrementally"),
						BypassContentForCreatorsWhoNoLongerExist = hoconConfig.GetBoolean("Download.BypassContentForCreatorsWhoNoLongerExist"),
						DownloadDuplicatedMedia = hoconConfig.GetBoolean("Download.DownloadDuplicatedMedia"),
						SkipAds = hoconConfig.GetBoolean("Download.SkipAds"),
						DownloadPath = hoconConfig.GetString("Download.DownloadPath"),
						DownloadOnlySpecificDates = hoconConfig.GetBoolean("Download.DownloadOnlySpecificDates"),
						DownloadDateSelection = Enum.Parse<DownloadDateSelection>(hoconConfig.GetString("Download.DownloadDateSelection"), true),
						CustomDate = !string.IsNullOrWhiteSpace(hoconConfig.GetString("Download.CustomDate")) ? DateTime.Parse(hoconConfig.GetString("Download.CustomDate")) : null,
						ShowScrapeSize = hoconConfig.GetBoolean("Download.ShowScrapeSize"),

						// File Settings
						PaidPostFileNameFormat = hoconConfig.GetString("File.PaidPostFileNameFormat"),
						PostFileNameFormat = hoconConfig.GetString("File.PostFileNameFormat"),
						PaidMessageFileNameFormat = hoconConfig.GetString("File.PaidMessageFileNameFormat"),
						MessageFileNameFormat = hoconConfig.GetString("File.MessageFileNameFormat"),
						RenameExistingFilesWhenCustomFormatIsSelected = hoconConfig.GetBoolean("File.RenameExistingFilesWhenCustomFormatIsSelected"),

						// Folder Settings
						FolderPerPaidPost = hoconConfig.GetBoolean("Folder.FolderPerPaidPost"),
						FolderPerPost = hoconConfig.GetBoolean("Folder.FolderPerPost"),
						FolderPerPaidMessage = hoconConfig.GetBoolean("Folder.FolderPerPaidMessage"),
						FolderPerMessage = hoconConfig.GetBoolean("Folder.FolderPerMessage"),

						// Subscription Settings
						IncludeExpiredSubscriptions = hoconConfig.GetBoolean("Subscriptions.IncludeExpiredSubscriptions"),
						IncludeRestrictedSubscriptions = hoconConfig.GetBoolean("Subscriptions.IncludeRestrictedSubscriptions"),
						IgnoredUsersListName = hoconConfig.GetString("Subscriptions.IgnoredUsersListName"),

						// Interaction Settings
						NonInteractiveMode = hoconConfig.GetBoolean("Interaction.NonInteractiveMode"),
						NonInteractiveModeListName = hoconConfig.GetString("Interaction.NonInteractiveModeListName"),
						NonInteractiveModePurchasedTab = hoconConfig.GetBoolean("Interaction.NonInteractiveModePurchasedTab"),

                        // Performance Settings
                        Timeout = string.IsNullOrWhiteSpace(hoconConfig.GetString("Performance.Timeout")) ? -1 : hoconConfig.GetInt("Performance.Timeout"),
                        LimitDownloadRate = hoconConfig.GetBoolean("Performance.LimitDownloadRate"),
						DownloadLimitInMbPerSec = hoconConfig.GetInt("Performance.DownloadLimitInMbPerSec"),

						// Logging/Debug Settings
						LoggingLevel = Enum.Parse<LoggingLevel>(hoconConfig.GetString("Logging.LoggingLevel"), true)
					};

                    ValidateFileNameFormat(config.PaidPostFileNameFormat, "PaidPostFileNameFormat");
                    ValidateFileNameFormat(config.PostFileNameFormat, "PostFileNameFormat");
                    ValidateFileNameFormat(config.PaidMessageFileNameFormat, "PaidMessageFileNameFormat");
                    ValidateFileNameFormat(config.MessageFileNameFormat, "MessageFileNameFormat");

                    var creatorConfigsSection = hoconConfig.GetConfig("CreatorConfigs");
                    if (creatorConfigsSection != null)
                    {
                        foreach (var key in creatorConfigsSection.AsEnumerable())
                        {
                            var creatorKey = key.Key;
                            var creatorHocon = creatorConfigsSection.GetConfig(creatorKey);
                            if (!config.CreatorConfigs.ContainsKey(creatorKey) && creatorHocon != null)
                            {
                                config.CreatorConfigs.Add(key.Key, new CreatorConfig
                                {
                                    PaidPostFileNameFormat = creatorHocon.GetString("PaidPostFileNameFormat"),
                                    PostFileNameFormat = creatorHocon.GetString("PostFileNameFormat"),
                                    PaidMessageFileNameFormat = creatorHocon.GetString("PaidMessageFileNameFormat"),
                                    MessageFileNameFormat = creatorHocon.GetString("MessageFileNameFormat")
                                });

                                ValidateFileNameFormat(config.CreatorConfigs[key.Key].PaidPostFileNameFormat, $"{key.Key}.PaidPostFileNameFormat");
                                ValidateFileNameFormat(config.CreatorConfigs[key.Key].PostFileNameFormat, $"{key.Key}.PostFileNameFormat");
                                ValidateFileNameFormat(config.CreatorConfigs[key.Key].PaidMessageFileNameFormat, $"{key.Key}.PaidMessageFileNameFormat");
                                ValidateFileNameFormat(config.CreatorConfigs[key.Key].MessageFileNameFormat, $"{key.Key}.MessageFileNameFormat");
                            }
                        }
                    }

					levelSwitch.MinimumLevel = (LogEventLevel)config.LoggingLevel;      //set the logging level based on config
					Log.Debug("Configuration:");
					string configString = JsonConvert.SerializeObject(config, Formatting.Indented);
					Log.Debug(configString);
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					AnsiConsole.MarkupLine($"\n[red]config.conf is not valid, check your syntax![/]\n");
					AnsiConsole.MarkupLine($"[red]Press any key to exit.[/]");
					Log.Error("config.conf processing failed.", e.Message);

					if (!cliNonInteractive)
					{
						Console.ReadKey();
					}
					Environment.Exit(3);
				}
			}
			else
			{
                Entities.Config jsonConfig = new Entities.Config();
                var hoconConfig = new StringBuilder();
                hoconConfig.AppendLine("# External Tools");
                hoconConfig.AppendLine("External {");
                hoconConfig.AppendLine($"  FFmpegPath = \"{jsonConfig.FFmpegPath}\"");
                hoconConfig.AppendLine("}");

                hoconConfig.AppendLine("# Download Settings");
                hoconConfig.AppendLine("Download {");
                hoconConfig.AppendLine("  Media {");
                hoconConfig.AppendLine($"    DownloadAvatarHeaderPhoto = {jsonConfig.DownloadAvatarHeaderPhoto.ToString().ToLower()}");
                hoconConfig.AppendLine($"    DownloadPaidPosts = {jsonConfig.DownloadPaidPosts.ToString().ToLower()}");
                hoconConfig.AppendLine($"    DownloadPosts = {jsonConfig.DownloadPosts.ToString().ToLower()}");
                hoconConfig.AppendLine($"    DownloadArchived = {jsonConfig.DownloadArchived.ToString().ToLower()}");
                hoconConfig.AppendLine($"    DownloadStreams = {jsonConfig.DownloadStreams.ToString().ToLower()}");
                hoconConfig.AppendLine($"    DownloadStories = {jsonConfig.DownloadStories.ToString().ToLower()}");
                hoconConfig.AppendLine($"    DownloadHighlights = {jsonConfig.DownloadHighlights.ToString().ToLower()}");
                hoconConfig.AppendLine($"    DownloadMessages = {jsonConfig.DownloadMessages.ToString().ToLower()}");
                hoconConfig.AppendLine($"    DownloadPaidMessages = {jsonConfig.DownloadPaidMessages.ToString().ToLower()}");
                hoconConfig.AppendLine($"    DownloadImages = {jsonConfig.DownloadImages.ToString().ToLower()}");
                hoconConfig.AppendLine($"    DownloadVideos = {jsonConfig.DownloadVideos.ToString().ToLower()}");
                hoconConfig.AppendLine($"    DownloadAudios = {jsonConfig.DownloadAudios.ToString().ToLower()}");
                hoconConfig.AppendLine("  }");
                hoconConfig.AppendLine($"  IgnoreOwnMessages = {jsonConfig.IgnoreOwnMessages.ToString().ToLower()}");
                hoconConfig.AppendLine($"  DownloadPostsIncrementally = {jsonConfig.DownloadPostsIncrementally.ToString().ToLower()}");
                hoconConfig.AppendLine($"  BypassContentForCreatorsWhoNoLongerExist = {jsonConfig.BypassContentForCreatorsWhoNoLongerExist.ToString().ToLower()}");
                hoconConfig.AppendLine($"  DownloadDuplicatedMedia = {jsonConfig.DownloadDuplicatedMedia.ToString().ToLower()}");
                hoconConfig.AppendLine($"  SkipAds = {jsonConfig.SkipAds.ToString().ToLower()}");
                hoconConfig.AppendLine($"  DownloadPath = \"{jsonConfig.DownloadPath}\"");
                hoconConfig.AppendLine($"  DownloadOnlySpecificDates = {jsonConfig.DownloadOnlySpecificDates.ToString().ToLower()}");
                hoconConfig.AppendLine($"  DownloadDateSelection = \"{jsonConfig.DownloadDateSelection.ToString().ToLower()}\"");
                hoconConfig.AppendLine($"  CustomDate = \"{jsonConfig.CustomDate?.ToString("yyyy-MM-dd")}\"");
                hoconConfig.AppendLine($"  ShowScrapeSize = {jsonConfig.ShowScrapeSize.ToString().ToLower()}");
                hoconConfig.AppendLine("}");

                hoconConfig.AppendLine("# File Settings");
                hoconConfig.AppendLine("File {");
                hoconConfig.AppendLine($"  PaidPostFileNameFormat = \"{jsonConfig.PaidPostFileNameFormat}\"");
                hoconConfig.AppendLine($"  PostFileNameFormat = \"{jsonConfig.PostFileNameFormat}\"");
                hoconConfig.AppendLine($"  PaidMessageFileNameFormat = \"{jsonConfig.PaidMessageFileNameFormat}\"");
                hoconConfig.AppendLine($"  MessageFileNameFormat = \"{jsonConfig.MessageFileNameFormat}\"");
                hoconConfig.AppendLine($"  RenameExistingFilesWhenCustomFormatIsSelected = {jsonConfig.RenameExistingFilesWhenCustomFormatIsSelected.ToString().ToLower()}");
                hoconConfig.AppendLine("}");

                hoconConfig.AppendLine("# Creator-Specific Configurations");
                hoconConfig.AppendLine("CreatorConfigs {");
                foreach (var creatorConfig in jsonConfig.CreatorConfigs)
                {
                    hoconConfig.AppendLine($"  \"{creatorConfig.Key}\" {{");
                    hoconConfig.AppendLine($"    PaidPostFileNameFormat = \"{creatorConfig.Value.PaidPostFileNameFormat}\"");
                    hoconConfig.AppendLine($"    PostFileNameFormat = \"{creatorConfig.Value.PostFileNameFormat}\"");
                    hoconConfig.AppendLine($"    PaidMessageFileNameFormat = \"{creatorConfig.Value.PaidMessageFileNameFormat}\"");
                    hoconConfig.AppendLine($"    MessageFileNameFormat = \"{creatorConfig.Value.MessageFileNameFormat}\"");
                    hoconConfig.AppendLine("  }");
                }
                hoconConfig.AppendLine("}");

                hoconConfig.AppendLine("# Folder Settings");
                hoconConfig.AppendLine("Folder {");
                hoconConfig.AppendLine($"  FolderPerPaidPost = {jsonConfig.FolderPerPaidPost.ToString().ToLower()}");
                hoconConfig.AppendLine($"  FolderPerPost = {jsonConfig.FolderPerPost.ToString().ToLower()}");
                hoconConfig.AppendLine($"  FolderPerPaidMessage = {jsonConfig.FolderPerPaidMessage.ToString().ToLower()}");
                hoconConfig.AppendLine($"  FolderPerMessage = {jsonConfig.FolderPerMessage.ToString().ToLower()}");
                hoconConfig.AppendLine("}");

                hoconConfig.AppendLine("# Subscription Settings");
                hoconConfig.AppendLine("Subscriptions {");
                hoconConfig.AppendLine($"  IncludeExpiredSubscriptions = {jsonConfig.IncludeExpiredSubscriptions.ToString().ToLower()}");
                hoconConfig.AppendLine($"  IncludeRestrictedSubscriptions = {jsonConfig.IncludeRestrictedSubscriptions.ToString().ToLower()}");
                hoconConfig.AppendLine($"  IgnoredUsersListName = \"{jsonConfig.IgnoredUsersListName}\"");
                hoconConfig.AppendLine("}");

                hoconConfig.AppendLine("# Interaction Settings");
                hoconConfig.AppendLine("Interaction {");
                hoconConfig.AppendLine($"  NonInteractiveMode = {jsonConfig.NonInteractiveMode.ToString().ToLower()}");
                hoconConfig.AppendLine($"  NonInteractiveModeListName = \"{jsonConfig.NonInteractiveModeListName}\"");
                hoconConfig.AppendLine($"  NonInteractiveModePurchasedTab = {jsonConfig.NonInteractiveModePurchasedTab.ToString().ToLower()}");
                hoconConfig.AppendLine("}");

                hoconConfig.AppendLine("# Performance Settings");
                hoconConfig.AppendLine("Performance {");
                hoconConfig.AppendLine($"  Timeout = {(jsonConfig.Timeout.HasValue ? jsonConfig.Timeout.Value : -1)}");
                hoconConfig.AppendLine($"  LimitDownloadRate = {jsonConfig.LimitDownloadRate.ToString().ToLower()}");
                hoconConfig.AppendLine($"  DownloadLimitInMbPerSec = {jsonConfig.DownloadLimitInMbPerSec}");
                hoconConfig.AppendLine("}");

                hoconConfig.AppendLine("# Logging/Debug Settings");
                hoconConfig.AppendLine("Logging {");
                hoconConfig.AppendLine($"  LoggingLevel = \"{jsonConfig.LoggingLevel.ToString().ToLower()}\"");
                hoconConfig.AppendLine("}");

                File.WriteAllText("config.conf", hoconConfig.ToString());
                AnsiConsole.Markup("[red]config.conf does not exist, a default file has been created in the folder you are running the program from[/]");
				Log.Error("config.conf does not exist");

				if (!cliNonInteractive)
				{
					Console.ReadKey();
				}
				Environment.Exit(3);
			}


			if (args is not null && args.Length > 0)
			{
				const string NON_INTERACTIVE_ARG = "--non-interactive";

				if (args.Any(a => NON_INTERACTIVE_ARG.Equals(NON_INTERACTIVE_ARG, StringComparison.OrdinalIgnoreCase)))
				{
					cliNonInteractive = true;
					Log.Debug("NonInteractiveMode set via command line");
				}

				Log.Debug("Additional arguments:");
				foreach (string argument in args)
				{
					Log.Debug(argument);
				}
			}

			var os = Environment.OSVersion;

			Log.Debug($"Operating system information: {os.VersionString}");

			if (os.Platform == PlatformID.Win32NT)
			{
				// check if this is windows 10+
				if (os.Version.Major < 10)
				{
					Console.Write("This appears to be running on an older version of Windows which is not supported.\n\n");
					Console.Write("OF-DL requires Windows 10 or higher when being run on Windows. Your reported version is: {0}\n\n", os.VersionString);
					Console.Write("Press any key to continue.\n");
					Log.Error("Windows version prior to 10.x: {0}", os.VersionString);

					if (!cliNonInteractive)
					{
						Console.ReadKey();
					}
					Environment.Exit(1);
				}
				else
				{
					AnsiConsole.Markup("[green]Valid version of Windows found.\n[/]");
				}
			}

			try
			{
				// Only run the version check if not in DEBUG mode
				#if !DEBUG
				Version localVersion = Assembly.GetEntryAssembly()?.GetName().Version; //Only tested with numeric values.

				// Get all releases from GitHub
				GitHubClient client = new GitHubClient(new ProductHeaderValue("SomeName"));
				IReadOnlyList<Release> releases = await client.Repository.Release.GetAll("sim0n00ps", "OF-DL");

				// Setup the versions
				Version latestGitHubVersion = new Version(releases[0].TagName.Replace("OFDLV", ""));

				// Compare the Versions
				int versionComparison = localVersion.CompareTo(latestGitHubVersion);
				if (versionComparison < 0)
				{
					// The version on GitHub is more up to date than this local release.
					AnsiConsole.Markup("[red]You are running OF-DL version " + $"{localVersion.Major}.{localVersion.Minor}.{localVersion.Build}\n[/]");
					AnsiConsole.Markup("[red]Please update to the current release on GitHub, " + $"{latestGitHubVersion.Major}.{latestGitHubVersion.Minor}.{latestGitHubVersion.Build}: {releases[0].HtmlUrl}\n[/]");
					Log.Debug("Detected outdated client running version " + $"{localVersion.Major}.{localVersion.Minor}.{localVersion.Build}");
					Log.Debug("Latest GitHub release version " + $"{latestGitHubVersion.Major}.{latestGitHubVersion.Minor}.{latestGitHubVersion.Build}");
				}
				else
				{
					// This local version is greater than the release version on GitHub.
					AnsiConsole.Markup("[green]You are running OF-DL version " + $"{localVersion.Major}.{localVersion.Minor}.{localVersion.Build}\n[/]");
					AnsiConsole.Markup("[green]Latest GitHub Release version: " + $"{latestGitHubVersion.Major}.{latestGitHubVersion.Minor}.{latestGitHubVersion.Build}\n[/]");
					Log.Debug("Detected client running version " + $"{localVersion.Major}.{localVersion.Minor}.{localVersion.Build}");
					Log.Debug("Latest GitHub release version " + $"{latestGitHubVersion.Major}.{latestGitHubVersion.Minor}.{latestGitHubVersion.Build}");
				}
				#else
				AnsiConsole.Markup("[yellow]Running in Debug/Local mode. Version check skipped.\n[/]");
				Log.Debug("Running in Debug/Local mode. Version check skipped.");
				#endif
			}
			catch (Exception e)
			{
				AnsiConsole.Markup("[red]Error checking latest release on GitHub:\n[/]");
				Console.WriteLine(e);
				Log.Error("Error checking latest release on GitHub.", e.Message);
			}

			if (File.Exists("auth.json"))
			{
				AnsiConsole.Markup("[green]auth.json located successfully!\n[/]");
				Log.Debug("Auth file found");
				try
				{
					auth = JsonConvert.DeserializeObject<Auth>(await File.ReadAllTextAsync("auth.json"));
					Log.Debug("Auth file found and deserialized");
				}
				catch (Exception _)
				{
					Log.Information("Auth file found but could not be deserialized");
					Log.Debug("Deleting auth.json");
					File.Delete("auth.json");

					if (cliNonInteractive)
					{
						AnsiConsole.MarkupLine($"\n[red]auth.json has invalid JSON syntax. The file can be generated automatically when OF-DL is run in the standard, interactive mode.[/]\n");
						AnsiConsole.MarkupLine($"[red]You may also want to try using the browser extension which is documented here:[/]\n");
						AnsiConsole.MarkupLine($"[link]https://sim0n00ps.github.io/OF-DL/docs/config/auth#browser-extension[/]\n");
						AnsiConsole.MarkupLine($"[red]Press any key to exit.[/]");

						Console.ReadKey();
						Environment.Exit(2);
					}

					await LoadAuthFromBrowser();
				}
			}
			else
			{
				if (cliNonInteractive)
				{
					AnsiConsole.MarkupLine($"\n[red]auth.json is missing. The file can be generated automatically when OF-DL is run in the standard, interactive mode.[/]\n");
					AnsiConsole.MarkupLine($"[red]You may also want to try using the browser extension which is documented here:[/]\n");
					AnsiConsole.MarkupLine($"[link]https://sim0n00ps.github.io/OF-DL/docs/config/auth#browser-extension[/]\n");
					AnsiConsole.MarkupLine($"[red]Press any key to exit.[/]");

					Console.ReadKey();
					Environment.Exit(2);
				}

				await LoadAuthFromBrowser();
			}

			//Added to stop cookie being filled with un-needed headers
			ValidateCookieString();

			if (File.Exists("rules.json"))
			{
				AnsiConsole.Markup("[green]rules.json located successfully!\n[/]");
				try
				{
					JsonConvert.DeserializeObject<DynamicRules>(File.ReadAllText("rules.json"));
					Log.Debug($"Rules.json: ");
					Log.Debug(JsonConvert.SerializeObject(File.ReadAllText("rules.json"), Formatting.Indented));
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					AnsiConsole.MarkupLine($"\n[red]rules.json is not valid, check your JSON syntax![/]\n");
					AnsiConsole.MarkupLine($"[red]Please ensure you are using the latest version of the software.[/]\n");
					AnsiConsole.MarkupLine($"[red]Press any key to exit.[/]");
					Log.Error("rules.json processing failed.", e.Message);

					if (!cliNonInteractive)
					{
						Console.ReadKey();
					}
					Environment.Exit(2);
				}
			}

			if(cliNonInteractive)
			{
				// CLI argument overrides configuration
				config!.NonInteractiveMode = true;
				Log.Debug("NonInteractiveMode = true");
			}

			if(config!.NonInteractiveMode)
			{
				cliNonInteractive = true; // If it was set in the config, reset the cli value so exception handling works
				Log.Debug("NonInteractiveMode = true (set via config)");
			}

			var ffmpegFound = false;
			var pathAutoDetected = false;
			if (!string.IsNullOrEmpty(config!.FFmpegPath) && ValidateFilePath(config.FFmpegPath))
			{
				// FFmpeg path is set in config.json and is valid
				ffmpegFound = true;
				Log.Debug($"FFMPEG found: {config.FFmpegPath}");
				Log.Debug("FFMPEG path set in config.conf");
			}
			else if (!string.IsNullOrEmpty(auth!.FFMPEG_PATH) && ValidateFilePath(auth.FFMPEG_PATH))
			{
				// FFmpeg path is set in auth.json and is valid (config.conf takes precedence and auth.json is only available for backward compatibility)
				ffmpegFound = true;
				config.FFmpegPath = auth.FFMPEG_PATH;
				Log.Debug($"FFMPEG found: {config.FFmpegPath}");
				Log.Debug("FFMPEG path set in auth.json");
			}
			else if (string.IsNullOrEmpty(config.FFmpegPath))
			{
				// FFmpeg path is not set in config.conf, so we will try to locate it in the PATH or current directory
				var ffmpegPath = GetFullPath("ffmpeg");
				if (ffmpegPath != null)
				{
					// FFmpeg is found in the PATH or current directory
					ffmpegFound = true;
					pathAutoDetected = true;
					config.FFmpegPath = ffmpegPath;
					Log.Debug($"FFMPEG found: {ffmpegPath}");
					Log.Debug("FFMPEG path found via PATH or current directory");
				}
				else
				{
					// FFmpeg is not found in the PATH or current directory, so we will try to locate the windows executable
					ffmpegPath = GetFullPath("ffmpeg.exe");
					if (ffmpegPath != null)
					{
						// FFmpeg windows executable is found in the PATH or current directory
						ffmpegFound = true;
						pathAutoDetected = true;
						config.FFmpegPath = ffmpegPath;
						Log.Debug($"FFMPEG found: {ffmpegPath}");
						Log.Debug("FFMPEG path found in windows excutable directory");
					}
				}
			}

			if (ffmpegFound)
			{
				if (pathAutoDetected)
				{
					AnsiConsole.Markup($"[green]FFmpeg located successfully. Path auto-detected: {config.FFmpegPath}\n[/]");
				}
				else
				{
					AnsiConsole.Markup($"[green]FFmpeg located successfully\n[/]");
				}

				// Escape backslashes in the path for Windows
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && config.FFmpegPath!.Contains(@":\") && !config.FFmpegPath.Contains(@":\\"))
				{
					config.FFmpegPath = config.FFmpegPath.Replace(@"\", @"\\");
				}
			}
			else
			{
				AnsiConsole.Markup("[red]Cannot locate FFmpeg; please modify config.conf with the correct path. Press any key to exit.[/]");
				Log.Error($"Cannot locate FFmpeg with path: {config.FFmpegPath}");
				if (!config.NonInteractiveMode)
				{
					Console.ReadKey();
				}
				Environment.Exit(4);
			}

			if (!File.Exists(Path.Join(WidevineClient.Widevine.Constants.DEVICES_FOLDER, WidevineClient.Widevine.Constants.DEVICE_NAME, "device_client_id_blob")))
			{
				clientIdBlobMissing = true;
				Log.Debug("clientIdBlobMissing missing");
			}
			else
			{
				AnsiConsole.Markup($"[green]device_client_id_blob located successfully![/]\n");
				Log.Debug("clientIdBlobMissing found: " + File.Exists(Path.Join(WidevineClient.Widevine.Constants.DEVICES_FOLDER, WidevineClient.Widevine.Constants.DEVICE_NAME, "device_client_id_blob")));
			}

			if (!File.Exists(Path.Join(WidevineClient.Widevine.Constants.DEVICES_FOLDER, WidevineClient.Widevine.Constants.DEVICE_NAME, "device_private_key")))
			{
				devicePrivateKeyMissing = true;
				Log.Debug("devicePrivateKeyMissing missing");
			}
			else
			{
				AnsiConsole.Markup($"[green]device_private_key located successfully![/]\n");
				Log.Debug("devicePrivateKeyMissing found: " + File.Exists(Path.Join(WidevineClient.Widevine.Constants.DEVICES_FOLDER, WidevineClient.Widevine.Constants.DEVICE_NAME, "device_private_key")));
			}

			if (clientIdBlobMissing || devicePrivateKeyMissing)
			{
				AnsiConsole.Markup("[yellow]device_client_id_blob and/or device_private_key missing, https://ofdl.tools/ or https://cdrm-project.com/ will be used instead for DRM protected videos\n[/]");
			}

			//Check if auth is valid
			var apiHelper = new APIHelper(auth, config);

			Entities.User? validate = await apiHelper.GetUserInfo($"/users/me");
			if (validate == null || (validate?.name == null && validate?.username == null))
			{
				Log.Error("Auth failed");

				auth = null;
				if (File.Exists("auth.json"))
				{
					File.Delete("auth.json");
				}

				if (!cliNonInteractive)
				{
					await LoadAuthFromBrowser();
				}

				if (auth == null)
				{
					AnsiConsole.MarkupLine($"\n[red]Auth failed. Please try again or use other authentication methods detailed here:[/]\n");
					AnsiConsole.MarkupLine($"[link]https://sim0n00ps.github.io/OF-DL/docs/config/auth[/]\n");
					Console.ReadKey();
					Environment.Exit(2);
				}
			}

			AnsiConsole.Markup($"[green]Logged In successfully as {validate.name} {validate.username}\n[/]");
			await DownloadAllData(apiHelper, auth, config);
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
			Console.WriteLine("\nPress any key to exit.");
			if (!cliNonInteractive)
			{
				Console.ReadKey();
			}
			Environment.Exit(5);
		}
	}


	private static async Task DownloadAllData(APIHelper m_ApiHelper, Auth Auth, Entities.Config Config)
	{
		DBHelper dBHelper = new DBHelper(Config);

		Log.Debug("Calling DownloadAllData");

		do
		{
			DateTime startTime = DateTime.Now;
			Dictionary<string, int> users = new();
			Dictionary<string, int> activeSubs = await m_ApiHelper.GetActiveSubscriptions("/subscriptions/subscribes", Config.IncludeRestrictedSubscriptions, Config);

			Log.Debug("Subscriptions: ");

			foreach (KeyValuePair<string, int> activeSub in activeSubs)
			{
				if (!users.ContainsKey(activeSub.Key))
				{
					users.Add(activeSub.Key, activeSub.Value);
					Log.Debug($"Name: {activeSub.Key} ID: {activeSub.Value}");
				}
			}
			if (Config!.IncludeExpiredSubscriptions)
			{
				Log.Debug("Inactive Subscriptions: ");

				Dictionary<string, int> expiredSubs = await m_ApiHelper.GetExpiredSubscriptions("/subscriptions/subscribes", Config.IncludeRestrictedSubscriptions, Config);
				foreach (KeyValuePair<string, int> expiredSub in expiredSubs)
				{
					if (!users.ContainsKey(expiredSub.Key))
					{
						users.Add(expiredSub.Key, expiredSub.Value);
						Log.Debug($"Name: {expiredSub.Key} ID: {expiredSub.Value}");
					}
				}
			}

			Dictionary<string, int> lists = await m_ApiHelper.GetLists("/lists", Config);

			// Remove users from the list if they are in the ignored list
			if (!string.IsNullOrEmpty(Config.IgnoredUsersListName))
			{
				if (!lists.TryGetValue(Config.IgnoredUsersListName, out var ignoredUsersListId))
				{
					AnsiConsole.Markup($"[red]Ignored users list '{Config.IgnoredUsersListName}' not found\n[/]");
					Log.Error($"Ignored users list '{Config.IgnoredUsersListName}' not found");
				}
				else
				{
					var ignoredUsernames = await m_ApiHelper.GetListUsers($"/lists/{ignoredUsersListId}/users", Config) ?? [];
					users = users.Where(x => !ignoredUsernames.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
				}
			}

			await dBHelper.CreateUsersDB(users);
			KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP;
			if(Config.NonInteractiveMode && Config.NonInteractiveModePurchasedTab)
			{
				hasSelectedUsersKVP = new KeyValuePair<bool, Dictionary<string, int>>(true, new Dictionary<string, int> { { "PurchasedTab", 0 } });
			}
			else if (Config.NonInteractiveMode && string.IsNullOrEmpty(Config.NonInteractiveModeListName))
			{
				hasSelectedUsersKVP = new KeyValuePair<bool, Dictionary<string, int>>(true, users);
			}
			else if (Config.NonInteractiveMode && !string.IsNullOrEmpty(Config.NonInteractiveModeListName))
			{
				var listId = lists[Config.NonInteractiveModeListName];
				var listUsernames = await m_ApiHelper.GetListUsers($"/lists/{listId}/users", Config) ?? [];
				var selectedUsers = users.Where(x => listUsernames.Contains(x.Key)).Distinct().ToDictionary(x => x.Key, x => x.Value);
				hasSelectedUsersKVP = new KeyValuePair<bool, Dictionary<string, int>>(true, selectedUsers);
			}
			else
			{
				var userSelectionResult = await HandleUserSelection(m_ApiHelper, Config, users, lists);

				Config = userSelectionResult.updatedConfig;
				hasSelectedUsersKVP = new KeyValuePair<bool, Dictionary<string, int>>(userSelectionResult.IsExit, userSelectionResult.selectedUsers);
			}

			if (hasSelectedUsersKVP.Key && hasSelectedUsersKVP.Value != null && hasSelectedUsersKVP.Value.ContainsKey("SinglePost"))
			{
				AnsiConsole.Markup("[red]To find an individual post URL, click on the ... at the top right corner of the post and select 'Copy link to post'.\n\nTo return to the main menu, enter 'back' or 'exit' when prompted for the URL.\n\n[/]");
				string postUrl = AnsiConsole.Prompt(
						new TextPrompt<string>("[red]Please enter a post URL: [/]")
							.ValidationErrorMessage("[red]Please enter a valid post URL[/]")
							.Validate(url =>
							{
								Log.Debug($"Single Post URL: {url}");
								Regex regex = new Regex("https://onlyfans\\.com/[0-9]+/[A-Za-z0-9]+", RegexOptions.IgnoreCase);
								if (regex.IsMatch(url))
								{
									return ValidationResult.Success();
								}
								if (url == "" || url == "exit" || url == "back") {
									return ValidationResult.Success();
								}
								Log.Error("Post URL invalid");
								return ValidationResult.Error("[red]Please enter a valid post URL[/]");
							}));

				if (postUrl != "" && postUrl != "exit" && postUrl != "back") {
					long post_id = Convert.ToInt64(postUrl.Split("/")[3]);
					string username = postUrl.Split("/")[4];

					Log.Debug($"Single Post ID: {post_id.ToString()}");
					Log.Debug($"Single Post Creator: {username}");

					if (users.ContainsKey(username))
					{
						string path = "";
						if (!string.IsNullOrEmpty(Config.DownloadPath))
						{
							path = System.IO.Path.Combine(Config.DownloadPath, username);
						}
						else
						{
							path = $"__user_data__/sites/OnlyFans/{username}";
						}

						Log.Debug($"Download path: {path}");

						if (!Directory.Exists(path))
						{
							Directory.CreateDirectory(path);
							AnsiConsole.Markup($"[red]Created folder for {username}\n[/]");
							Log.Debug($"Created folder for {username}");
						}
						else
						{
							AnsiConsole.Markup($"[red]Folder for {username} already created\n[/]");
						}

						await dBHelper.CreateDB(path);

						var downloadContext = new DownloadContext(Auth, Config, GetCreatorFileNameFormatConfig(Config, username), m_ApiHelper, dBHelper);

						await DownloadSinglePost(downloadContext, post_id, path, users);
					}
				}
			}
			else if (hasSelectedUsersKVP.Key && hasSelectedUsersKVP.Value != null && hasSelectedUsersKVP.Value.ContainsKey("PurchasedTab"))
			{
				Dictionary<string, int> purchasedTabUsers = await m_ApiHelper.GetPurchasedTabUsers("/posts/paid", Config, users);
				AnsiConsole.Markup($"[red]Checking folders for Users in Purchased Tab\n[/]");
				foreach (KeyValuePair<string, int> user in purchasedTabUsers)
				{
					string path = "";
					if (!string.IsNullOrEmpty(Config.DownloadPath))
					{
						path = System.IO.Path.Combine(Config.DownloadPath, user.Key);
					}
					else
					{
						path = $"__user_data__/sites/OnlyFans/{user.Key}";
					}

					Log.Debug($"Download path: {path}");

					await dBHelper.CheckUsername(user, path);

					if (!Directory.Exists(path))
					{
						Directory.CreateDirectory(path);
						AnsiConsole.Markup($"[red]Created folder for {user.Key}\n[/]");
						Log.Debug($"Created folder for {user.Key}");
					}
					else
					{
						AnsiConsole.Markup($"[red]Folder for {user.Key} already created\n[/]");
						Log.Debug($"Folder for {user.Key} already created");
					}

					Entities.User user_info = await m_ApiHelper.GetUserInfo($"/users/{user.Key}");

					await dBHelper.CreateDB(path);
				}

				string p = "";
				if (!string.IsNullOrEmpty(Config.DownloadPath))
				{
					p = Config.DownloadPath;
				}
				else
				{
					p = $"__user_data__/sites/OnlyFans/";
				}

				Log.Debug($"Download path: {p}");

				List<PurchasedTabCollection> purchasedTabCollections = await m_ApiHelper.GetPurchasedTab("/posts/paid", p, Config, users);
				foreach(PurchasedTabCollection purchasedTabCollection in purchasedTabCollections)
				{
					AnsiConsole.Markup($"[red]\nScraping Data for {purchasedTabCollection.Username}\n[/]");
					string path = "";
					if (!string.IsNullOrEmpty(Config.DownloadPath))
					{
						path = System.IO.Path.Combine(Config.DownloadPath, purchasedTabCollection.Username);
					}
					else
					{
						path = $"__user_data__/sites/OnlyFans/{purchasedTabCollection.Username}";
					}


					Log.Debug($"Download path: {path}");

					var downloadContext = new DownloadContext(Auth, Config, GetCreatorFileNameFormatConfig(Config, purchasedTabCollection.Username), m_ApiHelper, dBHelper);

					int paidPostCount = 0;
					int paidMessagesCount = 0;
					paidPostCount = await DownloadPaidPostsPurchasedTab(downloadContext, purchasedTabCollection.PaidPosts, users.FirstOrDefault(u => u.Value == purchasedTabCollection.UserId), paidPostCount, path, users);
					paidMessagesCount = await DownloadPaidMessagesPurchasedTab(downloadContext, purchasedTabCollection.PaidMessages, users.FirstOrDefault(u => u.Value == purchasedTabCollection.UserId), paidMessagesCount, path, users);

					AnsiConsole.Markup("\n");
					AnsiConsole.Write(new BreakdownChart()
					.FullSize()
					.AddItem("Paid Posts", paidPostCount, Color.Red)
					.AddItem("Paid Messages", paidMessagesCount, Color.Aqua));
					AnsiConsole.Markup("\n");
				}
				DateTime endTime = DateTime.Now;
				TimeSpan totalTime = endTime - startTime;
				AnsiConsole.Markup($"[green]Scrape Completed in {totalTime.TotalMinutes:0.00} minutes\n[/]");
				Log.Debug($"Scrape Completed in {totalTime.TotalMinutes:0.00} minutes");
			}
			else if (hasSelectedUsersKVP.Key && hasSelectedUsersKVP.Value != null && hasSelectedUsersKVP.Value.ContainsKey("SingleMessage"))
			{
				AnsiConsole.Markup("[red]To find an individual message URL, note that you can only do so for PPV messages that you have unlocked. Go the main OnlyFans timeline, click on the Purchased tab, find the relevant message, click on the ... at the top right corner of the message, and select 'Copy link to message'. For all other messages, you cannot scrape them individually, you must scrape all messages from that creator.\n\nTo return to the main menu, enter 'back' or 'exit' when prompted for the URL.\n\n[/]");
				string messageUrl = AnsiConsole.Prompt(
					new TextPrompt<string>("[red]Please enter a message URL: [/]")
						.ValidationErrorMessage("[red]Please enter a valid message URL[/]")
						.Validate(url =>
						{
							Log.Debug($"Single Paid Message URL: {url}");
							Regex regex = new Regex("https://onlyfans\\.com/my/chats/chat/[0-9]+/\\?firstId=[0-9]+$", RegexOptions.IgnoreCase);
							if (regex.IsMatch(url))
							{
								return ValidationResult.Success();
							}
							if (url == "" || url == "back" || url == "exit")
							{
								return ValidationResult.Success();
							}
							Log.Error("Message URL invalid");
							return ValidationResult.Error("[red]Please enter a valid message URL[/]");
						}));

				if (messageUrl != "" && messageUrl != "exit" && messageUrl != "back")
				{
					long message_id = Convert.ToInt64(messageUrl.Split("?firstId=")[1]);
					long user_id = Convert.ToInt64(messageUrl.Split("/")[6]);
					JObject user = await m_ApiHelper.GetUserInfoById($"/users/list?x[]={user_id.ToString()}");
					string username = string.Empty;

					Log.Debug($"Message ID: {message_id}");
					Log.Debug($"User ID: {user_id}");

					if (user is null)
					{
						username = $"Deleted User - {user_id.ToString()}";
						Log.Debug("Content creator not longer exists - ", user_id.ToString());
					}
					else if (!string.IsNullOrEmpty(user[user_id.ToString()]["username"].ToString()))
					{
						username = user[user_id.ToString()]["username"].ToString();
						Log.Debug("Content creator: ", username);
					}

					string path = "";
					if (!string.IsNullOrEmpty(Config.DownloadPath))
					{
						path = System.IO.Path.Combine(Config.DownloadPath, username);
					}
					else
					{
						path = $"__user_data__/sites/OnlyFans/{username}";
					}

					Log.Debug("Download path: ", path);

					if (!Directory.Exists(path))
					{
						Directory.CreateDirectory(path);
						AnsiConsole.Markup($"[red]Created folder for {username}\n[/]");
						Log.Debug($"Created folder for {username}");
					}
					else
					{
						AnsiConsole.Markup($"[red]Folder for {username} already created\n[/]");
						Log.Debug($"Folder for {username} already created");
					}

					await dBHelper.CreateDB(path);

					var downloadContext = new DownloadContext(Auth, Config, GetCreatorFileNameFormatConfig(Config, username), m_ApiHelper, dBHelper);

					await DownloadPaidMessage(downloadContext, hasSelectedUsersKVP, username, 1, path, message_id);
				}
			}
			else if (hasSelectedUsersKVP.Key && !hasSelectedUsersKVP.Value.ContainsKey("ConfigChanged"))
			{
				//Iterate over each user in the list of users
				foreach (KeyValuePair<string, int> user in hasSelectedUsersKVP.Value)
				{
					int paidPostCount = 0;
					int postCount = 0;
					int archivedCount = 0;
					int streamsCount = 0;
					int storiesCount = 0;
					int highlightsCount = 0;
					int messagesCount = 0;
					int paidMessagesCount = 0;
					AnsiConsole.Markup($"[red]\nScraping Data for {user.Key}\n[/]");

					Log.Debug($"Scraping Data for {user.Key}");

					string path = "";
					if (!string.IsNullOrEmpty(Config.DownloadPath))
					{
						path = System.IO.Path.Combine(Config.DownloadPath, user.Key);
					}
					else
					{
						path = $"__user_data__/sites/OnlyFans/{user.Key}";
					}

					Log.Debug("Download path: ", path);

					await dBHelper.CheckUsername(user, path);

					if (!Directory.Exists(path))
					{
						Directory.CreateDirectory(path);
						AnsiConsole.Markup($"[red]Created folder for {user.Key}\n[/]");
						Log.Debug($"Created folder for {user.Key}");
					}
					else
					{
						AnsiConsole.Markup($"[red]Folder for {user.Key} already created\n[/]");
						Log.Debug($"Folder for {user.Key} already created");
					}

					await dBHelper.CreateDB(path);

					var downloadContext = new DownloadContext(Auth, Config, GetCreatorFileNameFormatConfig(Config, user.Key), m_ApiHelper, dBHelper);

					if (Config.DownloadAvatarHeaderPhoto)
					{
						Entities.User? user_info = await m_ApiHelper.GetUserInfo($"/users/{user.Key}");
						if (user_info != null)
						{
							await downloadContext.DownloadHelper.DownloadAvatarHeader(user_info.avatar, user_info.header, path, user.Key);
						}
					}

					if (Config.DownloadPaidPosts)
					{
						paidPostCount = await DownloadPaidPosts(downloadContext, hasSelectedUsersKVP, user, paidPostCount, path);
					}

					if (Config.DownloadPosts)
					{
						postCount = await DownloadFreePosts(downloadContext, hasSelectedUsersKVP, user, postCount, path);
					}

					if (Config.DownloadArchived)
					{
						archivedCount = await DownloadArchived(downloadContext, hasSelectedUsersKVP, user, archivedCount, path);
					}

					if (Config.DownloadStreams)
					{
						streamsCount = await DownloadStreams(downloadContext, hasSelectedUsersKVP, user, streamsCount, path);
					}

					if (Config.DownloadStories)
					{
						storiesCount = await DownloadStories(downloadContext, user, storiesCount, path);
					}

					if (Config.DownloadHighlights)
					{
						highlightsCount = await DownloadHighlights(downloadContext, user, highlightsCount, path);
					}

					if (Config.DownloadMessages)
					{
						messagesCount = await DownloadMessages(downloadContext, hasSelectedUsersKVP, user, messagesCount, path);
					}

					if (Config.DownloadPaidMessages)
					{
						paidMessagesCount = await DownloadPaidMessages(downloadContext, hasSelectedUsersKVP, user, paidMessagesCount, path);
					}

					AnsiConsole.Markup("\n");
					AnsiConsole.Write(new BreakdownChart()
					.FullSize()
					.AddItem("Paid Posts", paidPostCount, Color.Red)
					.AddItem("Posts", postCount, Color.Blue)
					.AddItem("Archived", archivedCount, Color.Green)
					.AddItem("Streams", streamsCount, Color.Purple)
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
		} while (!Config.NonInteractiveMode);
	}

	private static IFileNameFormatConfig GetCreatorFileNameFormatConfig(Entities.Config config, string userName)
	{
		FileNameFormatConfig combinedConfig = new FileNameFormatConfig();

		Func<string?, string?, string?> func = (val1, val2) =>
		{
			if (string.IsNullOrEmpty(val1))
				return val2;
			else
				return val1;
		};

		if(config.CreatorConfigs.ContainsKey(userName))
		{
			CreatorConfig creatorConfig = config.CreatorConfigs[userName];
			if(creatorConfig != null)
			{
				combinedConfig.PaidMessageFileNameFormat = creatorConfig.PaidMessageFileNameFormat;
				combinedConfig.PostFileNameFormat = creatorConfig.PostFileNameFormat;
				combinedConfig.MessageFileNameFormat = creatorConfig.MessageFileNameFormat;
				combinedConfig.PaidPostFileNameFormat = creatorConfig.PaidPostFileNameFormat;
			}
		}

		combinedConfig.PaidMessageFileNameFormat = func(combinedConfig.PaidMessageFileNameFormat, config.PaidMessageFileNameFormat);
		combinedConfig.PostFileNameFormat = func(combinedConfig.PostFileNameFormat, config.PostFileNameFormat);
		combinedConfig.MessageFileNameFormat = func(combinedConfig.MessageFileNameFormat, config.MessageFileNameFormat);
		combinedConfig.PaidPostFileNameFormat = func(combinedConfig.PaidPostFileNameFormat, config.PaidPostFileNameFormat);

		Log.Debug($"PaidMessageFilenameFormat: {combinedConfig.PaidMessageFileNameFormat}");
		Log.Debug($"PostFileNameFormat: {combinedConfig.PostFileNameFormat}");
		Log.Debug($"MessageFileNameFormat: {combinedConfig.MessageFileNameFormat}");
		Log.Debug($"PaidPostFileNameFormatt: {combinedConfig.PaidPostFileNameFormat}");

		return combinedConfig;
	}

	private static async Task<int> DownloadPaidMessages(IDownloadContext downloadContext, KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, KeyValuePair<string, int> user, int paidMessagesCount, string path)
	{
		Log.Debug($"Calling DownloadPaidMessages - {user.Key}");

		AnsiConsole.Markup($"[red]Getting Paid Messages\n[/]");
		//Dictionary<long, string> purchased = await apiHelper.GetMedia(MediaType.PaidMessages, "/posts/paid", user.Key, path, auth, paid_post_ids);
		PaidMessageCollection paidMessageCollection = await downloadContext.ApiHelper.GetPaidMessages("/posts/paid", path, user.Key, downloadContext.DownloadConfig!);
		int oldPaidMessagesCount = 0;
		int newPaidMessagesCount = 0;
		if (paidMessageCollection != null && paidMessageCollection.PaidMessages.Count > 0)
		{
			AnsiConsole.Markup($"[red]Found {paidMessageCollection.PaidMessages.Count} Media from {paidMessageCollection.PaidMessageObjects.Count} Paid Messages\n[/]");
			Log.Debug($"Found {paidMessageCollection.PaidMessages.Count} Media from {paidMessageCollection.PaidMessageObjects.Count} Paid Messages");
			paidMessagesCount = paidMessageCollection.PaidMessages.Count;
			long totalSize = 0;
			if (downloadContext.DownloadConfig.ShowScrapeSize)
			{
				totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(paidMessageCollection.PaidMessages.Values.ToList());
			}
			else
			{
				totalSize = paidMessagesCount;
			}
			await AnsiConsole.Progress()
			.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
			.StartAsync(async ctx =>
			{
				// Define tasks
				var task = ctx.AddTask($"[red]Downloading {paidMessageCollection.PaidMessages.Count} Paid Messages[/]", autoStart: false);
				Log.Debug($"Downloading {paidMessageCollection.PaidMessages.Count} Paid Messages");
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
						string? pssh = await downloadContext.ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
						if (pssh != null)
						{
							DateTime lastModified = await downloadContext.ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
							Dictionary<string, string> drmHeaders = downloadContext.ApiHelper.GetDynamicHeaders($"/api2/v2/users/media/{mediaId}/drm/message/{messageId}", "?type=widevine");
							string decryptionKey;
							if (clientIdBlobMissing || devicePrivateKeyMissing)
							{
								decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyOFDL(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh);
							}
							else
							{
								decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyCDM(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh);
							}


							Medium? mediaInfo = paidMessageCollection.PaidMessageMedia.FirstOrDefault(m => m.id == paidMessageKVP.Key);
							Purchased.List? messageInfo = paidMessageCollection.PaidMessageObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

							isNew = await downloadContext.DownloadHelper.DownloadPurchasedMessageDRMVideo(
								policy: policy,
								signature: signature,
								kvp: kvp,
								url: mpdURL,
								decryptionKey: decryptionKey,
								folder: path,
								lastModified: lastModified,
								media_id: paidMessageKVP.Key,
								api_type: "Messages",
								task: task,
								filenameFormat: downloadContext.FileNameFormatConfig.PaidMessageFileNameFormat ?? string.Empty,
								messageInfo: messageInfo,
								messageMedia: mediaInfo,
								fromUser: messageInfo?.fromUser,
								users: hasSelectedUsersKVP.Value);

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
						Medium? mediaInfo = paidMessageCollection.PaidMessageMedia.FirstOrDefault(m => m.id == paidMessageKVP.Key);
						Purchased.List messageInfo = paidMessageCollection.PaidMessageObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

						isNew = await downloadContext.DownloadHelper.DownloadPurchasedMedia(
							url: paidMessageKVP.Value,
							folder: path,
							media_id: paidMessageKVP.Key,
							api_type: "Messages",
							task: task,
							filenameFormat: downloadContext.FileNameFormatConfig.PaidMessageFileNameFormat ?? string.Empty,
							messageInfo: messageInfo,
							messageMedia: mediaInfo,
							fromUser: messageInfo?.fromUser,
							users: hasSelectedUsersKVP.Value);
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

	private static async Task<int> DownloadMessages(IDownloadContext downloadContext, KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, KeyValuePair<string, int> user, int messagesCount, string path)
	{
		Log.Debug($"Calling DownloadMessages - {user.Key}");

		AnsiConsole.Markup($"[red]Getting Messages\n[/]");
		MessageCollection messages = await downloadContext.ApiHelper.GetMessages($"/chats/{user.Value}/messages", path, downloadContext.DownloadConfig!);
		int oldMessagesCount = 0;
		int newMessagesCount = 0;
		if (messages != null && messages.Messages.Count > 0)
		{
			AnsiConsole.Markup($"[red]Found {messages.Messages.Count} Media from {messages.MessageObjects.Count} Messages\n[/]");
			Log.Debug($"[red]Found {messages.Messages.Count} Media from {messages.MessageObjects.Count} Messages");
			messagesCount = messages.Messages.Count;
			long totalSize = 0;
			if (downloadContext.DownloadConfig.ShowScrapeSize)
			{
				totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(messages.Messages.Values.ToList());
			}
			else
			{
				totalSize = messagesCount;
			}
			await AnsiConsole.Progress()
			.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
			.StartAsync(async ctx =>
			{
				// Define tasks
				var task = ctx.AddTask($"[red]Downloading {messages.Messages.Count} Messages[/]", autoStart: false);
				Log.Debug($"Downloading {messages.Messages.Count} Messages");
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
						string? pssh = await downloadContext.ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
						if (pssh != null)
						{
							DateTime lastModified = await downloadContext.ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
							Dictionary<string, string> drmHeaders = downloadContext.ApiHelper.GetDynamicHeaders($"/api2/v2/users/media/{mediaId}/drm/message/{messageId}", "?type=widevine");
							string decryptionKey;
							if (clientIdBlobMissing || devicePrivateKeyMissing)
							{
								decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyOFDL(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh);
							}
							else
							{
								decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyCDM(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh);
							}
							Messages.Medium? mediaInfo = messages.MessageMedia.FirstOrDefault(m => m.id == messageKVP.Key);
							Messages.List? messageInfo = messages.MessageObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

							isNew = await downloadContext.DownloadHelper.DownloadMessageDRMVideo(
								policy: policy,
								signature: signature,
								kvp: kvp,
								url: mpdURL,
								decryptionKey: decryptionKey,
								folder: path,
								lastModified: lastModified,
								media_id: messageKVP.Key,
								api_type: "Messages",
								task: task,
								filenameFormat: downloadContext.FileNameFormatConfig.MessageFileNameFormat ?? string.Empty,
								messageInfo: messageInfo,
								messageMedia: mediaInfo,
								fromUser: messageInfo?.fromUser,
								users: hasSelectedUsersKVP.Value);


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

						isNew = await downloadContext.DownloadHelper.DownloadMessageMedia(
							url: messageKVP.Value,
							folder: path,
							media_id: messageKVP.Key,
							api_type: "Messages",
							task: task,
							filenameFormat: downloadContext.FileNameFormatConfig!.MessageFileNameFormat ?? string.Empty,
							messageInfo: messageInfo,
							messageMedia: mediaInfo,
							fromUser: messageInfo?.fromUser,
							users: hasSelectedUsersKVP.Value);

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

	private static async Task<int> DownloadHighlights(IDownloadContext downloadContext, KeyValuePair<string, int> user, int highlightsCount, string path)
	{
		Log.Debug($"Calling DownloadHighlights - {user.Key}");

		AnsiConsole.Markup($"[red]Getting Highlights\n[/]");
		Dictionary<long, string> highlights = await downloadContext.ApiHelper.GetMedia(MediaType.Highlights, $"/users/{user.Value}/stories/highlights", null, path, downloadContext.DownloadConfig!, paid_post_ids);
		int oldHighlightsCount = 0;
		int newHighlightsCount = 0;
		if (highlights != null && highlights.Count > 0)
		{
			AnsiConsole.Markup($"[red]Found {highlights.Count} Highlights\n[/]");
			Log.Debug($"Found {highlights.Count} Highlights");
			highlightsCount = highlights.Count;
			long totalSize = 0;
			if (downloadContext.DownloadConfig.ShowScrapeSize)
			{
				totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(highlights.Values.ToList());
			}
			else
			{
				totalSize = highlightsCount;
			}
			await AnsiConsole.Progress()
			.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
			.StartAsync(async ctx =>
			{
				// Define tasks
				var task = ctx.AddTask($"[red]Downloading {highlights.Count} Highlights[/]", autoStart: false);
				Log.Debug($"Downloading {highlights.Count} Highlights");
				task.MaxValue = totalSize;
				task.StartTask();
				foreach (KeyValuePair<long, string> highlightKVP in highlights)
				{
					bool isNew = await downloadContext.DownloadHelper.DownloadStoryMedia(highlightKVP.Value, path, highlightKVP.Key, "Stories", task);
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
			Log.Debug($"Highlights Already Downloaded: {oldHighlightsCount} New Highlights Downloaded: {newHighlightsCount}");
		}
		else
		{
			AnsiConsole.Markup($"[red]Found 0 Highlights\n[/]");
			Log.Debug($"Found 0 Highlights");
		}

		return highlightsCount;
	}

	private static async Task<int> DownloadStories(IDownloadContext downloadContext, KeyValuePair<string, int> user, int storiesCount, string path)
	{
		Log.Debug($"Calling DownloadStories - {user.Key}");

		AnsiConsole.Markup($"[red]Getting Stories\n[/]");
		Dictionary<long, string> stories = await downloadContext.ApiHelper.GetMedia(MediaType.Stories, $"/users/{user.Value}/stories", null, path, downloadContext.DownloadConfig!, paid_post_ids);
		int oldStoriesCount = 0;
		int newStoriesCount = 0;
		if (stories != null && stories.Count > 0)
		{
			AnsiConsole.Markup($"[red]Found {stories.Count} Stories\n[/]");
			Log.Debug($"Found {stories.Count} Stories");
			storiesCount = stories.Count;
			long totalSize = 0;
			if (downloadContext.DownloadConfig.ShowScrapeSize)
			{
				totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(stories.Values.ToList());
			}
			else
			{
				totalSize = storiesCount;
			}
			await AnsiConsole.Progress()
			.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
			.StartAsync(async ctx =>
			{
				// Define tasks
				var task = ctx.AddTask($"[red]Downloading {stories.Count} Stories[/]", autoStart: false);
				Log.Debug($"Downloading {stories.Count} Stories");
				task.MaxValue = totalSize;
				task.StartTask();
				foreach (KeyValuePair<long, string> storyKVP in stories)
				{
					bool isNew = await downloadContext.DownloadHelper.DownloadStoryMedia(storyKVP.Value, path, storyKVP.Key, "Stories", task);
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
			Log.Debug($"Stories Already Downloaded: {oldStoriesCount} New Stories Downloaded: {newStoriesCount}");
		}
		else
		{
			AnsiConsole.Markup($"[red]Found 0 Stories\n[/]");
			Log.Debug($"Found 0 Stories");
		}

		return storiesCount;
	}

	private static async Task<int> DownloadArchived(IDownloadContext downloadContext, KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, KeyValuePair<string, int> user, int archivedCount, string path)
	{
		Log.Debug($"Calling DownloadArchived - {user.Key}");

		AnsiConsole.Markup($"[red]Getting Archived Posts\n[/]");
		//Dictionary<long, string> archived = await apiHelper.GetMedia(MediaType.Archived, $"/users/{user.Value}/posts", null, path, auth, paid_post_ids);
		ArchivedCollection archived = await downloadContext.ApiHelper.GetArchived($"/users/{user.Value}/posts", path, downloadContext.DownloadConfig!);
		int oldArchivedCount = 0;
		int newArchivedCount = 0;
		if (archived != null && archived.ArchivedPosts.Count > 0)
		{
			AnsiConsole.Markup($"[red]Found {archived.ArchivedPosts.Count} Media from {archived.ArchivedPostObjects.Count} Archived Posts\n[/]");
			Log.Debug($"Found {archived.ArchivedPosts.Count} Media from {archived.ArchivedPostObjects.Count} Archived Posts");
			archivedCount = archived.ArchivedPosts.Count;
			long totalSize = 0;
			if (downloadContext.DownloadConfig.ShowScrapeSize)
			{
				totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(archived.ArchivedPosts.Values.ToList());
			}
			else
			{
				totalSize = archivedCount;
			}
			await AnsiConsole.Progress()
			.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
			.StartAsync(async ctx =>
			{
				// Define tasks
				var task = ctx.AddTask($"[red]Downloading {archived.ArchivedPosts.Count} Archived Posts[/]", autoStart: false);
				Log.Debug($"Downloading {archived.ArchivedPosts.Count} Archived Posts");
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
						string? pssh = await downloadContext.ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
						if (pssh != null)
						{
							DateTime lastModified = await downloadContext.ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
							Dictionary<string, string> drmHeaders = downloadContext.ApiHelper.GetDynamicHeaders($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine");
							string decryptionKey;
							if (clientIdBlobMissing || devicePrivateKeyMissing)
							{
								decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyOFDL(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
							}
							else
							{
								decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyCDM(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
							}
							Archived.Medium? mediaInfo = archived.ArchivedPostMedia.FirstOrDefault(m => m.id == archivedKVP.Key);
							Archived.List? postInfo = archived.ArchivedPostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

							isNew = await downloadContext.DownloadHelper.DownloadArchivedPostDRMVideo(
								policy: policy,
								signature: signature,
								kvp: kvp,
								url: mpdURL,
								decryptionKey: decryptionKey,
								folder: path,
								lastModified: lastModified,
								media_id: archivedKVP.Key,
								api_type: "Posts",
								task: task,
								filenameFormat: downloadContext.FileNameFormatConfig.PostFileNameFormat ?? string.Empty,
								postInfo: postInfo,
								postMedia: mediaInfo,
								author: postInfo?.author,
								users: hasSelectedUsersKVP.Value);

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

						isNew = await downloadContext.DownloadHelper.DownloadArchivedMedia(
							url: archivedKVP.Value,
							folder: path,
							media_id: archivedKVP.Key,
							api_type: "Posts",
							task: task,
							filenameFormat: downloadContext.FileNameFormatConfig.PostFileNameFormat ?? string.Empty,
							messageInfo: postInfo,
							messageMedia: mediaInfo,
							author: postInfo?.author,
							users: hasSelectedUsersKVP.Value);

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

	private static async Task<int> DownloadFreePosts(IDownloadContext downloadContext, KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, KeyValuePair<string, int> user, int postCount, string path)
	{
		Log.Debug($"Calling DownloadFreePosts - {user.Key}");

		AnsiConsole.Markup($"[red]Getting Posts\n[/]");
		//Dictionary<long, string> posts = await apiHelper.GetMedia(MediaType.Posts, $"/users/{user.Value}/posts", null, path, auth, paid_post_ids);
		PostCollection posts = await downloadContext.ApiHelper.GetPosts($"/users/{user.Value}/posts", path, downloadContext.DownloadConfig!, paid_post_ids);
		int oldPostCount = 0;
		int newPostCount = 0;
		if (posts == null || posts.Posts.Count <= 0)
		{
			AnsiConsole.Markup($"[red]Found 0 Posts\n[/]");
			Log.Debug($"Found 0 Posts");
			return 0;
		}

		AnsiConsole.Markup($"[red]Found {posts.Posts.Count} Media from {posts.PostObjects.Count} Posts\n[/]");
		Log.Debug($"Found {posts.Posts.Count} Media from {posts.PostObjects.Count} Posts");
		postCount = posts.Posts.Count;
		long totalSize = 0;
		if (downloadContext.DownloadConfig.ShowScrapeSize)
		{
			totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(posts.Posts.Values.ToList());
		}
		else
		{
			totalSize = postCount;
		}
		await AnsiConsole.Progress()
		.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
		.StartAsync(async ctx =>
		{
			var task = ctx.AddTask($"[red]Downloading {posts.Posts.Count} Posts[/]", autoStart: false);
			Log.Debug($"Downloading {posts.Posts.Count} Posts");
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
					string? pssh = await downloadContext.ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
					if (pssh == null)
					{
						continue;
					}

					DateTime lastModified = await downloadContext.ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
					Dictionary<string, string> drmHeaders = downloadContext.ApiHelper.GetDynamicHeaders($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine");
					string decryptionKey;
					if (clientIdBlobMissing || devicePrivateKeyMissing)
					{
						decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyOFDL(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
					}
					else
					{
						decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyCDM(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
					}
					Post.Medium mediaInfo = posts.PostMedia.FirstOrDefault(m => m.id == postKVP.Key);
					Post.List postInfo = posts.PostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

					isNew = await downloadContext.DownloadHelper.DownloadPostDRMVideo(
						policy: policy,
						signature: signature,
						kvp: kvp,
						url: mpdURL,
						decryptionKey: decryptionKey,
						folder: path,
						lastModified: lastModified,
						media_id: postKVP.Key,
						api_type: "Posts",
						task: task,
						filenameFormat: downloadContext.FileNameFormatConfig.PostFileNameFormat ?? string.Empty,
						postInfo: postInfo,
						postMedia: mediaInfo,
						author: postInfo?.author,
						users: hasSelectedUsersKVP.Value);
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

						isNew = await downloadContext.DownloadHelper.DownloadPostMedia(
							url: postKVP.Value,
							folder: path,
							media_id: postKVP.Key,
							api_type: "Posts",
							task: task,
							filenameFormat: downloadContext.FileNameFormatConfig.PostFileNameFormat ?? string.Empty,
							postInfo: postInfo,
							postMedia: mediaInfo,
							author: postInfo?.author,
							users: hasSelectedUsersKVP.Value);
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
		Log.Debug("Posts Already Downloaded: {oldPostCount} New Posts Downloaded: {newPostCount}");

		return postCount;
	}

	private static async Task<int> DownloadPaidPosts(IDownloadContext downloadContext, KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, KeyValuePair<string, int> user, int paidPostCount, string path)
	{
		AnsiConsole.Markup($"[red]Getting Paid Posts\n[/]");

		Log.Debug($"Calling DownloadPaidPosts - {user.Key}");

		//Dictionary<long, string> purchasedPosts = await apiHelper.GetMedia(MediaType.PaidPosts, "/posts/paid", user.Key, path, auth, paid_post_ids);
		PaidPostCollection purchasedPosts = await downloadContext.ApiHelper.GetPaidPosts("/posts/paid", path, user.Key, downloadContext.DownloadConfig!, paid_post_ids);
		int oldPaidPostCount = 0;
		int newPaidPostCount = 0;
		if (purchasedPosts == null || purchasedPosts.PaidPosts.Count <= 0)
		{
			AnsiConsole.Markup($"[red]Found 0 Paid Posts\n[/]");
			Log.Debug("Found 0 Paid Posts");
			return 0;
		}

		AnsiConsole.Markup($"[red]Found {purchasedPosts.PaidPosts.Count} Media from {purchasedPosts.PaidPostObjects.Count} Paid Posts\n[/]");
		Log.Debug($"Found {purchasedPosts.PaidPosts.Count} Media from {purchasedPosts.PaidPostObjects.Count} Paid Posts");
		paidPostCount = purchasedPosts.PaidPosts.Count;
		long totalSize = 0;
		if (downloadContext.DownloadConfig.ShowScrapeSize)
		{
			totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(purchasedPosts.PaidPosts.Values.ToList());
		}
		else
		{
			totalSize = paidPostCount;
		}
		await AnsiConsole.Progress()
		.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
		.StartAsync(async ctx =>
		{
			// Define tasks
			var task = ctx.AddTask($"[red]Downloading {purchasedPosts.PaidPosts.Count} Paid Posts[/]", autoStart: false);
			Log.Debug($"Downloading {purchasedPosts.PaidPosts.Count} Paid Posts");
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
					string? pssh = await downloadContext.ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
					if (pssh == null)
					{
						continue;
					}

					DateTime lastModified = await downloadContext.ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
					Dictionary<string, string> drmHeaders = downloadContext.ApiHelper.GetDynamicHeaders($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine");
					string decryptionKey;
					if (clientIdBlobMissing || devicePrivateKeyMissing)
					{
						decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyOFDL(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
					}
					else
					{
						decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyCDM(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
					}
					Medium? mediaInfo = purchasedPosts.PaidPostMedia.FirstOrDefault(m => m.id == purchasedPostKVP.Key);
					Purchased.List? postInfo = purchasedPosts.PaidPostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

					isNew = await downloadContext.DownloadHelper.DownloadPurchasedPostDRMVideo(
						policy: policy,
						signature: signature,
						kvp: kvp,
						url: mpdURL,
						decryptionKey: decryptionKey,
						folder: path,
						lastModified: lastModified,
						media_id: purchasedPostKVP.Key,
						api_type: "Posts",
						task: task,
						filenameFormat: downloadContext.FileNameFormatConfig.PaidPostFileNameFormat ?? string.Empty,
						postInfo: postInfo,
						postMedia: mediaInfo,
						fromUser: postInfo?.fromUser,
						users: hasSelectedUsersKVP.Value);
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
					Medium mediaInfo = purchasedPosts.PaidPostMedia.FirstOrDefault(m => m.id == purchasedPostKVP.Key);
					Purchased.List postInfo = purchasedPosts.PaidPostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

					isNew = await downloadContext.DownloadHelper.DownloadPurchasedPostMedia(
						url: purchasedPostKVP.Value,
						folder: path,
						media_id: purchasedPostKVP.Key,
						api_type: "Posts",
						task: task,
						filenameFormat: downloadContext.FileNameFormatConfig.PaidPostFileNameFormat ?? string.Empty,
						messageInfo: postInfo,
						messageMedia: mediaInfo,
						fromUser: postInfo?.fromUser,
						users: hasSelectedUsersKVP.Value);
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
		Log.Debug($"Paid Posts Already Downloaded: {oldPaidPostCount} New Paid Posts Downloaded: {newPaidPostCount}");
		return paidPostCount;
	}

	private static async Task<int> DownloadPaidPostsPurchasedTab(IDownloadContext downloadContext, PaidPostCollection purchasedPosts, KeyValuePair<string, int> user, int paidPostCount, string path, Dictionary<string, int> users)
	{
		int oldPaidPostCount = 0;
		int newPaidPostCount = 0;
		if (purchasedPosts == null || purchasedPosts.PaidPosts.Count <= 0)
		{
			AnsiConsole.Markup($"[red]Found 0 Paid Posts\n[/]");
			Log.Debug("Found 0 Paid Posts");
			return 0;
		}

		AnsiConsole.Markup($"[red]Found {purchasedPosts.PaidPosts.Count} Media from {purchasedPosts.PaidPostObjects.Count} Paid Posts\n[/]");
		Log.Debug($"Found {purchasedPosts.PaidPosts.Count} Media from {purchasedPosts.PaidPostObjects.Count} Paid Posts");

		paidPostCount = purchasedPosts.PaidPosts.Count;
		long totalSize = 0;
		if (downloadContext.DownloadConfig.ShowScrapeSize)
		{
			totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(purchasedPosts.PaidPosts.Values.ToList());
		}
		else
		{
			totalSize = paidPostCount;
		}
		await AnsiConsole.Progress()
		.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
		.StartAsync(async ctx =>
		{
			// Define tasks
			var task = ctx.AddTask($"[red]Downloading {purchasedPosts.PaidPosts.Count} Paid Posts[/]", autoStart: false);
			Log.Debug($"Downloading {purchasedPosts.PaidPosts.Count} Paid Posts");
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
					string? pssh = await downloadContext.ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
					if (pssh == null)
					{
						continue;
					}

					DateTime lastModified = await downloadContext.ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
					Dictionary<string, string> drmHeaders = downloadContext.ApiHelper.GetDynamicHeaders($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine");
					string decryptionKey;
					if (clientIdBlobMissing || devicePrivateKeyMissing)
					{
						decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyOFDL(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
					}
					else
					{
						decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyCDM(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
					}
					Medium? mediaInfo = purchasedPosts?.PaidPostMedia?.FirstOrDefault(m => m.id == purchasedPostKVP.Key);
					Purchased.List? postInfo = mediaInfo != null ? purchasedPosts?.PaidPostObjects?.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true) : null;

					isNew = await downloadContext.DownloadHelper.DownloadPurchasedPostDRMVideo(
						policy: policy,
						signature: signature,
						kvp: kvp,
						url: mpdURL,
						decryptionKey: decryptionKey,
						folder: path,
						lastModified: lastModified,
						media_id: purchasedPostKVP.Key,
						api_type: "Posts",
						task: task,
						filenameFormat: downloadContext.FileNameFormatConfig.PaidPostFileNameFormat ?? string.Empty,
						postInfo: postInfo,
						postMedia: mediaInfo,
						fromUser: postInfo?.fromUser,
						users: users);
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
					Medium? mediaInfo = purchasedPosts?.PaidPostMedia?.FirstOrDefault(m => m.id == purchasedPostKVP.Key);
					Purchased.List? postInfo = mediaInfo != null ? purchasedPosts?.PaidPostObjects?.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true) : null;

					isNew = await downloadContext.DownloadHelper.DownloadPurchasedPostMedia(
						url: purchasedPostKVP.Value,
						folder: path,
						media_id: purchasedPostKVP.Key,
						api_type: "Posts",
						task: task,
						filenameFormat: downloadContext.FileNameFormatConfig.PaidPostFileNameFormat ?? string.Empty,
						messageInfo: postInfo,
						messageMedia: mediaInfo,
						fromUser: postInfo?.fromUser,
						users: users);
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
		Log.Debug($"Paid Posts Already Downloaded: {oldPaidPostCount} New Paid Posts Downloaded: {newPaidPostCount}");
		return paidPostCount;
	}

	private static async Task<int> DownloadPaidMessagesPurchasedTab(IDownloadContext downloadContext, PaidMessageCollection paidMessageCollection, KeyValuePair<string, int> user, int paidMessagesCount, string path, Dictionary<string, int> users)
	{
		int oldPaidMessagesCount = 0;
		int newPaidMessagesCount = 0;
		if (paidMessageCollection != null && paidMessageCollection.PaidMessages.Count > 0)
		{
			AnsiConsole.Markup($"[red]Found {paidMessageCollection.PaidMessages.Count} Media from {paidMessageCollection.PaidMessageObjects.Count} Paid Messages\n[/]");
			Log.Debug($"Found {paidMessageCollection.PaidMessages.Count} Media from {paidMessageCollection.PaidMessageObjects.Count} Paid Messages");
			paidMessagesCount = paidMessageCollection.PaidMessages.Count;
			long totalSize = 0;
			if (downloadContext.DownloadConfig.ShowScrapeSize)
			{
				totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(paidMessageCollection.PaidMessages.Values.ToList());
			}
			else
			{
				totalSize = paidMessagesCount;
			}
			await AnsiConsole.Progress()
			.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
			.StartAsync(async ctx =>
			{
				// Define tasks
				var task = ctx.AddTask($"[red]Downloading {paidMessageCollection.PaidMessages.Count} Paid Messages[/]", autoStart: false);
				Log.Debug($"Downloading {paidMessageCollection.PaidMessages.Count} Paid Messages");
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
						string? pssh = await downloadContext.ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
						if (pssh != null)
						{
							DateTime lastModified = await downloadContext.ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
							Dictionary<string, string> drmHeaders = downloadContext.ApiHelper.GetDynamicHeaders($"/api2/v2/users/media/{mediaId}/drm/message/{messageId}", "?type=widevine");
							string decryptionKey;
							if (clientIdBlobMissing || devicePrivateKeyMissing)
							{
								decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyOFDL(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh);
							}
							else
							{
								decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyCDM(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh);
							}

							Medium? mediaInfo = paidMessageCollection.PaidMessageMedia.FirstOrDefault(m => m.id == paidMessageKVP.Key);
							Purchased.List? messageInfo = paidMessageCollection.PaidMessageObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

							isNew = await downloadContext.DownloadHelper.DownloadPurchasedMessageDRMVideo(
								policy: policy,
								signature: signature,
								kvp: kvp,
								url: mpdURL,
								decryptionKey: decryptionKey,
								folder: path,
								lastModified: lastModified,
								media_id: paidMessageKVP.Key,
								api_type: "Messages",
								task: task,
								filenameFormat: downloadContext.FileNameFormatConfig.PaidMessageFileNameFormat ?? string.Empty,
								messageInfo: messageInfo,
								messageMedia: mediaInfo,
								fromUser: messageInfo?.fromUser,
								users: users);

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
						Medium? mediaInfo = paidMessageCollection.PaidMessageMedia.FirstOrDefault(m => m.id == paidMessageKVP.Key);
						Purchased.List messageInfo = paidMessageCollection.PaidMessageObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

						isNew = await downloadContext.DownloadHelper.DownloadPurchasedMedia(
							url: paidMessageKVP.Value,
							folder: path,
							media_id: paidMessageKVP.Key,
							api_type: "Messages",
							task: task,
							filenameFormat: downloadContext.FileNameFormatConfig.PaidMessageFileNameFormat ?? string.Empty,
							messageInfo: messageInfo,
							messageMedia: mediaInfo,
							fromUser: messageInfo?.fromUser,
							users: users);
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
			Log.Debug($"[red]Paid Messages Already Downloaded: {oldPaidMessagesCount} New Paid Messages Downloaded: {newPaidMessagesCount}");
		}
		else
		{
			AnsiConsole.Markup($"[red]Found 0 Paid Messages\n[/]");
			Log.Debug($"Found 0 Paid Messages");
		}

		return paidMessagesCount;
	}

	private static async Task<int> DownloadStreams(IDownloadContext downloadContext, KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, KeyValuePair<string, int> user, int streamsCount, string path)
	{
		Log.Debug($"Calling DownloadStreams - {user.Key}");

		AnsiConsole.Markup($"[red]Getting Streams\n[/]");
		StreamsCollection streams = await downloadContext.ApiHelper.GetStreams($"/users/{user.Value}/posts/streams", path, downloadContext.DownloadConfig!, paid_post_ids);
		int oldStreamsCount = 0;
		int newStreamsCount = 0;
		if (streams == null || streams.Streams.Count <= 0)
		{
			AnsiConsole.Markup($"[red]Found 0 Streams\n[/]");
			Log.Debug($"Found 0 Streams");
			return 0;
		}

		AnsiConsole.Markup($"[red]Found {streams.Streams.Count} Media from {streams.StreamObjects.Count} Streams\n[/]");
		Log.Debug($"Found {streams.Streams.Count} Media from {streams.StreamObjects.Count} Streams");
		streamsCount = streams.Streams.Count;
		long totalSize = 0;
		if (downloadContext.DownloadConfig.ShowScrapeSize)
		{
			totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(streams.Streams.Values.ToList());
		}
		else
		{
			totalSize = streamsCount;
		}
		await AnsiConsole.Progress()
		.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
		.StartAsync(async ctx =>
		{
			var task = ctx.AddTask($"[red]Downloading {streams.Streams.Count} Streams[/]", autoStart: false);
			Log.Debug($"Downloading {streams.Streams.Count} Streams");
			task.MaxValue = totalSize;
			task.StartTask();
			foreach (KeyValuePair<long, string> streamKVP in streams.Streams)
			{
				bool isNew;
				if (streamKVP.Value.Contains("cdn3.onlyfans.com/dash/files"))
				{
					string[] messageUrlParsed = streamKVP.Value.Split(',');
					string mpdURL = messageUrlParsed[0];
					string policy = messageUrlParsed[1];
					string signature = messageUrlParsed[2];
					string kvp = messageUrlParsed[3];
					string mediaId = messageUrlParsed[4];
					string postId = messageUrlParsed[5];
					string? licenseURL = null;
					string? pssh = await downloadContext.ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
					if (pssh == null)
					{
						continue;
					}

					DateTime lastModified = await downloadContext.ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
					Dictionary<string, string> drmHeaders = downloadContext.ApiHelper.GetDynamicHeaders($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine");
					string decryptionKey;
					if (clientIdBlobMissing || devicePrivateKeyMissing)
					{
						decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyOFDL(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
					}
					else
					{
						decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyCDM(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
					}
					Streams.Medium mediaInfo = streams.StreamMedia.FirstOrDefault(m => m.id == streamKVP.Key);
					Streams.List streamInfo = streams.StreamObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

					isNew = await downloadContext.DownloadHelper.DownloadStreamsDRMVideo(
						policy: policy,
						signature: signature,
						kvp: kvp,
						url: mpdURL,
						decryptionKey: decryptionKey,
						folder: path,
						lastModified: lastModified,
						media_id: streamKVP.Key,
						api_type: "Posts",
						task: task,
						filenameFormat: downloadContext.FileNameFormatConfig.PostFileNameFormat ?? string.Empty,
						streamInfo: streamInfo,
						streamMedia: mediaInfo,
						author: streamInfo?.author,
						users: hasSelectedUsersKVP.Value);
					if (isNew)
					{
						newStreamsCount++;
					}
					else
					{
						oldStreamsCount++;
					}
				}
				else
				{
					try
					{
						Streams.Medium? mediaInfo = streams.StreamMedia.FirstOrDefault(m => (m?.id == streamKVP.Key) == true);
						Streams.List? streamInfo = streams.StreamObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

						isNew = await downloadContext.DownloadHelper.DownloadStreamMedia(
							url: streamKVP.Value,
							folder: path,
							media_id: streamKVP.Key,
							api_type: "Posts",
							task: task,
							filenameFormat: downloadContext.FileNameFormatConfig.PostFileNameFormat ?? string.Empty,
							streamInfo: streamInfo,
							streamMedia: mediaInfo,
							author: streamInfo?.author,
							users: hasSelectedUsersKVP.Value);
						if (isNew)
						{
							newStreamsCount++;
						}
						else
						{
							oldStreamsCount++;
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
		AnsiConsole.Markup($"[red]Streams Already Downloaded: {oldStreamsCount} New Streams Downloaded: {newStreamsCount}[/]\n");
		Log.Debug($"Streams Already Downloaded: {oldStreamsCount} New Streams Downloaded: {newStreamsCount}");
		return streamsCount;
	}

	private static async Task<int> DownloadPaidMessage(IDownloadContext downloadContext, KeyValuePair<bool, Dictionary<string, int>> hasSelectedUsersKVP, string username, int paidMessagesCount, string path, long message_id)
	{
		Log.Debug($"Calling DownloadPaidMessage - {username}");

		AnsiConsole.Markup($"[red]Getting Paid Message\n[/]");

		SinglePaidMessageCollection singlePaidMessageCollection = await downloadContext.ApiHelper.GetPaidMessage($"/messages/{message_id.ToString()}", path, downloadContext.DownloadConfig!);
		int oldPaidMessagesCount = 0;
		int newPaidMessagesCount = 0;
		if (singlePaidMessageCollection != null && singlePaidMessageCollection.SingleMessages.Count > 0)
		{
			AnsiConsole.Markup($"[red]Found {singlePaidMessageCollection.SingleMessages.Count} Media from {singlePaidMessageCollection.SingleMessageObjects.Count} Paid Messages\n[/]");
			Log.Debug($"Found {singlePaidMessageCollection.SingleMessages.Count} Media from {singlePaidMessageCollection.SingleMessageObjects.Count} Paid Messages");
			paidMessagesCount = singlePaidMessageCollection.SingleMessages.Count;
			long totalSize = 0;
			if (downloadContext.DownloadConfig.ShowScrapeSize)
			{
				totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(singlePaidMessageCollection.SingleMessages.Values.ToList());
			}
			else
			{
				totalSize = paidMessagesCount;
			}
			await AnsiConsole.Progress()
			.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
			.StartAsync(async ctx =>
			{
				// Define tasks
				var task = ctx.AddTask($"[red]Downloading {singlePaidMessageCollection.SingleMessages.Count} Paid Messages[/]", autoStart: false);
				Log.Debug($"Downloading {singlePaidMessageCollection.SingleMessages.Count} Paid Messages");
				task.MaxValue = totalSize;
				task.StartTask();
				foreach (KeyValuePair<long, string> paidMessageKVP in singlePaidMessageCollection.SingleMessages)
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
						string? pssh = await downloadContext.ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
						if (pssh != null)
						{
							DateTime lastModified = await downloadContext.ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
							Dictionary<string, string> drmHeaders = downloadContext.ApiHelper.GetDynamicHeaders($"/api2/v2/users/media/{mediaId}/drm/message/{messageId}", "?type=widevine");
							string decryptionKey;
							if (clientIdBlobMissing || devicePrivateKeyMissing)
							{
								decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyOFDL(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh);
							}
							else
							{
								decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyCDM(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/message/{messageId}?type=widevine", pssh);
							}

							Medium? mediaInfo = singlePaidMessageCollection.SingleMessageMedia.FirstOrDefault(m => m.id == paidMessageKVP.Key);
							SingleMessage? messageInfo = singlePaidMessageCollection.SingleMessageObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

							isNew = await downloadContext.DownloadHelper.DownloadSinglePurchasedMessageDRMVideo(
								policy: policy,
								signature: signature,
								kvp: kvp,
								url: mpdURL,
								decryptionKey: decryptionKey,
								folder: path,
								lastModified: lastModified,
								media_id: paidMessageKVP.Key,
								api_type: "Messages",
								task: task,
								filenameFormat: downloadContext.FileNameFormatConfig.PaidMessageFileNameFormat ?? string.Empty,
								messageInfo: messageInfo,
								messageMedia: mediaInfo,
								fromUser: messageInfo?.fromUser,
								users: hasSelectedUsersKVP.Value);

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
						Medium? mediaInfo = singlePaidMessageCollection.SingleMessageMedia.FirstOrDefault(m => m.id == paidMessageKVP.Key);
						SingleMessage? messageInfo = singlePaidMessageCollection.SingleMessageObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

						isNew = await downloadContext.DownloadHelper.DownloadSinglePurchasedMedia(
							url: paidMessageKVP.Value,
							folder: path,
							media_id: paidMessageKVP.Key,
							api_type: "Messages",
							task: task,
							filenameFormat: downloadContext.FileNameFormatConfig.PaidMessageFileNameFormat ?? string.Empty,
							messageInfo: messageInfo,
							messageMedia: mediaInfo,
							fromUser: messageInfo?.fromUser,
							users: hasSelectedUsersKVP.Value);
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
			Log.Debug($"Paid Messages Already Downloaded: {oldPaidMessagesCount} New Paid Messages Downloaded: {newPaidMessagesCount}");
		}
		else
		{
			AnsiConsole.Markup($"[red]Found 0 Paid Messages\n[/]");
			Log.Debug($"Found 0 Paid Messages");
		}

		return paidMessagesCount;
	}

	private static async Task DownloadSinglePost(IDownloadContext downloadContext, long post_id, string path, Dictionary<string, int> users)
	{
		Log.Debug($"Calling DownloadSinglePost - {post_id.ToString()}");

		AnsiConsole.Markup($"[red]Getting Post\n[/]");
		SinglePostCollection post = await downloadContext.ApiHelper.GetPost($"/posts/{post_id.ToString()}", path, downloadContext.DownloadConfig!);
		if (post == null)
		{
			AnsiConsole.Markup($"[red]Couldn't find post\n[/]");
			Log.Debug($"Couldn't find post");
			return;
		}

		long totalSize = 0;
		if (downloadContext.DownloadConfig.ShowScrapeSize)
		{
			totalSize = await downloadContext.DownloadHelper.CalculateTotalFileSize(post.SinglePosts.Values.ToList());
		}
		else
		{
			totalSize = post.SinglePosts.Count;
		}
		bool isNew = false;
		await AnsiConsole.Progress()
		.Columns(GetProgressColumns(downloadContext.DownloadConfig.ShowScrapeSize))
		.StartAsync(async ctx =>
		{
			var task = ctx.AddTask($"[red]Downloading Post[/]", autoStart: false);
			task.MaxValue = totalSize;
			task.StartTask();
			foreach (KeyValuePair<long, string> postKVP in post.SinglePosts)
			{
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
					string? pssh = await downloadContext.ApiHelper.GetDRMMPDPSSH(mpdURL, policy, signature, kvp);
					if (pssh == null)
					{
						continue;
					}

					DateTime lastModified = await downloadContext.ApiHelper.GetDRMMPDLastModified(mpdURL, policy, signature, kvp);
					Dictionary<string, string> drmHeaders = downloadContext.ApiHelper.GetDynamicHeaders($"/api2/v2/users/media/{mediaId}/drm/post/{postId}", "?type=widevine");
					string decryptionKey;
					if (clientIdBlobMissing || devicePrivateKeyMissing)
					{
						decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyOFDL(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
					}
					else
					{
						decryptionKey = await downloadContext.ApiHelper.GetDecryptionKeyCDM(drmHeaders, $"https://onlyfans.com/api2/v2/users/media/{mediaId}/drm/post/{postId}?type=widevine", pssh);
					}
					SinglePost.Medium mediaInfo = post.SinglePostMedia.FirstOrDefault(m => m.id == postKVP.Key);
					SinglePost postInfo = post.SinglePostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

					isNew = await downloadContext.DownloadHelper.DownloadPostDRMVideo(
						policy: policy,
						signature: signature,
						kvp: kvp,
						url: mpdURL,
						decryptionKey: decryptionKey,
						folder: path,
						lastModified: lastModified,
						media_id: postKVP.Key,
						api_type: "Posts",
						task: task,
						filenameFormat: downloadContext.FileNameFormatConfig.PostFileNameFormat ?? string.Empty,
						postInfo: postInfo,
						postMedia: mediaInfo,
						author: postInfo?.author,
						users: users);
				}
				else
				{
					try
					{
						SinglePost.Medium? mediaInfo = post.SinglePostMedia.FirstOrDefault(m => (m?.id == postKVP.Key) == true);
						SinglePost? postInfo = post.SinglePostObjects.FirstOrDefault(p => p?.media?.Contains(mediaInfo) == true);

						isNew = await downloadContext.DownloadHelper.DownloadPostMedia(
							url: postKVP.Value,
							folder: path,
							media_id: postKVP.Key,
							api_type: "Posts",
							task: task,
							filenameFormat: downloadContext.FileNameFormatConfig.PostFileNameFormat ?? string.Empty,
							postInfo: postInfo,
							postMedia: mediaInfo,
							author: postInfo?.author,
							users: users);
					}
					catch
					{
						Console.WriteLine("Media was null");
					}
				}
			}
			task.StopTask();
		});
		if (isNew)
		{
			AnsiConsole.Markup($"[red]Post {post_id} downloaded\n[/]");
			Log.Debug($"Post {post_id} downloaded");
		}
		else
		{
			AnsiConsole.Markup($"[red]Post {post_id} already downloaded\n[/]");
			Log.Debug($"Post {post_id} already downloaded");
		}
	}

	public static async Task<(bool IsExit, Dictionary<string, int>? selectedUsers, Entities.Config? updatedConfig)> HandleUserSelection(APIHelper apiHelper, Entities.Config currentConfig, Dictionary<string, int> users, Dictionary<string, int> lists)
	{
		bool hasSelectedUsers = false;
		Dictionary<string, int> selectedUsers = new Dictionary<string, int>();

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
								List<string> usernames = await apiHelper.GetListUsers($"/lists/{listId}/users", config);
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
						foreach (string key in users.Keys.OrderBy(k => k).Select(k => $"[red]{k}[/]").ToList())
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
				case "[red]Download Single Post[/]":
					return (true, new Dictionary<string, int> { { "SinglePost", 0 } }, currentConfig);
				case "[red]Download Single Paid Message[/]":
					return (true, new Dictionary<string, int> { { "SingleMessage", 0 } }, currentConfig);
				case "[red]Download Purchased Tab[/]":
					return (true, new Dictionary<string, int> { { "PurchasedTab", 0 } }, currentConfig);
				case "[red]Edit config.conf[/]":
					while (true)
					{
						if (currentConfig == null)
							currentConfig = new Entities.Config();

						var choices = new List<(string choice, bool isSelected)>
						{
							("[red]Go Back[/]", false)
						};

						foreach(var propInfo in typeof(Entities.Config).GetProperties())
						{
							var attr = propInfo.GetCustomAttribute<ToggleableConfigAttribute>();
							if(attr != null)
							{
								string itemLabel = $"[red]{propInfo.Name}[/]";
								choices.Add(new(itemLabel, (bool)propInfo.GetValue(currentConfig)!));
							}
						}

						MultiSelectionPrompt<string> multiSelectionPrompt = new MultiSelectionPrompt<string>()
							.Title("[red]Edit config.conf[/]")
							.PageSize(25);

						foreach (var choice in choices)
						{
							multiSelectionPrompt.AddChoices(choice.choice, (selectionItem) => { if (choice.isSelected) selectionItem.Select(); });
						}

						var configOptions = AnsiConsole.Prompt(multiSelectionPrompt);

						if (configOptions.Contains("[red]Go Back[/]"))
						{
							break;
						}

						bool configChanged = false;

						Entities.Config newConfig = new Entities.Config();
						foreach (var propInfo in typeof(Entities.Config).GetProperties())
						{
							var attr = propInfo.GetCustomAttribute<ToggleableConfigAttribute>();
							if (attr != null)
							{
								//
								// Get the new choice from the selection
								//
								string itemLabel = $"[red]{propInfo.Name}[/]";
								var newValue = configOptions.Contains(itemLabel);
								var oldValue = choices.Where(c => c.choice == itemLabel).Select(c => c.isSelected).First();
								propInfo.SetValue(newConfig, newValue);

								if (newValue != oldValue)
									configChanged = true;
							}
							else
							{
								//
								// Reassign any non toggleable values
								//
								propInfo.SetValue(newConfig, propInfo.GetValue(currentConfig));
							}
						}

                        var hoconConfig = new StringBuilder();
                        hoconConfig.AppendLine("# External Tools");
                        hoconConfig.AppendLine("External {");
                        hoconConfig.AppendLine($"  FFmpegPath = \"{newConfig.FFmpegPath}\"");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Download Settings");
                        hoconConfig.AppendLine("Download {");
                        hoconConfig.AppendLine("  Media {");
                        hoconConfig.AppendLine($"    DownloadAvatarHeaderPhoto = {newConfig.DownloadAvatarHeaderPhoto.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadPaidPosts = {newConfig.DownloadPaidPosts.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadPosts = {newConfig.DownloadPosts.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadArchived = {newConfig.DownloadArchived.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadStreams = {newConfig.DownloadStreams.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadStories = {newConfig.DownloadStories.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadHighlights = {newConfig.DownloadHighlights.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadMessages = {newConfig.DownloadMessages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadPaidMessages = {newConfig.DownloadPaidMessages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadImages = {newConfig.DownloadImages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadVideos = {newConfig.DownloadVideos.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadAudios = {newConfig.DownloadAudios.ToString().ToLower()}");
                        hoconConfig.AppendLine("  }");
                        hoconConfig.AppendLine($"  IgnoreOwnMessages = {newConfig.IgnoreOwnMessages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadPostsIncrementally = {newConfig.DownloadPostsIncrementally.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  BypassContentForCreatorsWhoNoLongerExist = {newConfig.BypassContentForCreatorsWhoNoLongerExist.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadDuplicatedMedia = {newConfig.DownloadDuplicatedMedia.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  SkipAds = {newConfig.SkipAds.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadPath = \"{newConfig.DownloadPath}\"");
                        hoconConfig.AppendLine($"  DownloadOnlySpecificDates = {newConfig.DownloadOnlySpecificDates.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadDateSelection = \"{newConfig.DownloadDateSelection.ToString().ToLower()}\"");
                        hoconConfig.AppendLine($"  CustomDate = \"{newConfig.CustomDate?.ToString("yyyy-MM-dd")}\"");
                        hoconConfig.AppendLine($"  ShowScrapeSize = {newConfig.ShowScrapeSize.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# File Settings");
                        hoconConfig.AppendLine("File {");
                        hoconConfig.AppendLine($"  PaidPostFileNameFormat = \"{newConfig.PaidPostFileNameFormat}\"");
                        hoconConfig.AppendLine($"  PostFileNameFormat = \"{newConfig.PostFileNameFormat}\"");
                        hoconConfig.AppendLine($"  PaidMessageFileNameFormat = \"{newConfig.PaidMessageFileNameFormat}\"");
                        hoconConfig.AppendLine($"  MessageFileNameFormat = \"{newConfig.MessageFileNameFormat}\"");
                        hoconConfig.AppendLine($"  RenameExistingFilesWhenCustomFormatIsSelected = {newConfig.RenameExistingFilesWhenCustomFormatIsSelected.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Creator-Specific Configurations");
                        hoconConfig.AppendLine("CreatorConfigs {");
                        foreach (var creatorConfig in newConfig.CreatorConfigs)
                        {
                            hoconConfig.AppendLine($"  \"{creatorConfig.Key}\" {{");
                            hoconConfig.AppendLine($"    PaidPostFileNameFormat = \"{creatorConfig.Value.PaidPostFileNameFormat}\"");
                            hoconConfig.AppendLine($"    PostFileNameFormat = \"{creatorConfig.Value.PostFileNameFormat}\"");
                            hoconConfig.AppendLine($"    PaidMessageFileNameFormat = \"{creatorConfig.Value.PaidMessageFileNameFormat}\"");
                            hoconConfig.AppendLine($"    MessageFileNameFormat = \"{creatorConfig.Value.MessageFileNameFormat}\"");
                            hoconConfig.AppendLine("  }");
                        }
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Folder Settings");
                        hoconConfig.AppendLine("Folder {");
                        hoconConfig.AppendLine($"  FolderPerPaidPost = {newConfig.FolderPerPaidPost.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  FolderPerPost = {newConfig.FolderPerPost.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  FolderPerPaidMessage = {newConfig.FolderPerPaidMessage.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  FolderPerMessage = {newConfig.FolderPerMessage.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Subscription Settings");
                        hoconConfig.AppendLine("Subscriptions {");
                        hoconConfig.AppendLine($"  IncludeExpiredSubscriptions = {newConfig.IncludeExpiredSubscriptions.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  IncludeRestrictedSubscriptions = {newConfig.IncludeRestrictedSubscriptions.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  IgnoredUsersListName = \"{newConfig.IgnoredUsersListName}\"");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Interaction Settings");
                        hoconConfig.AppendLine("Interaction {");
                        hoconConfig.AppendLine($"  NonInteractiveMode = {newConfig.NonInteractiveMode.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  NonInteractiveModeListName = \"{newConfig.NonInteractiveModeListName}\"");
                        hoconConfig.AppendLine($"  NonInteractiveModePurchasedTab = {newConfig.NonInteractiveModePurchasedTab.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Performance Settings");
                        hoconConfig.AppendLine("Performance {");
                        hoconConfig.AppendLine($"  Timeout = {(newConfig.Timeout.HasValue ? newConfig.Timeout.Value : -1)}");
                        hoconConfig.AppendLine($"  LimitDownloadRate = {newConfig.LimitDownloadRate.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadLimitInMbPerSec = {newConfig.DownloadLimitInMbPerSec}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Logging/Debug Settings");
                        hoconConfig.AppendLine("Logging {");
                        hoconConfig.AppendLine($"  LoggingLevel = \"{newConfig.LoggingLevel.ToString().ToLower()}\"");
                        hoconConfig.AppendLine("}");

                        File.WriteAllText("config.conf", hoconConfig.ToString());

                        string newConfigString = JsonConvert.SerializeObject(newConfig, Formatting.Indented);

						Log.Debug($"Config changed:");
						Log.Debug(newConfigString);

						currentConfig = newConfig;
						if (configChanged)
						{
							return (true, new Dictionary<string, int> { { "ConfigChanged", 0 } }, currentConfig);
						}
						break;
					}
					break;
				case "[red]Change logging level[/]":
					while (true)
					{
						var choices = new List<(string choice, bool isSelected)>
						{
							("[red]Go Back[/]", false)
						};

						foreach (string name in typeof(LoggingLevel).GetEnumNames())
						{
								string itemLabel = $"[red]{name}[/]";
								choices.Add(new(itemLabel, name == levelSwitch.MinimumLevel.ToString()));
						}

						SelectionPrompt<string> selectionPrompt = new SelectionPrompt<string>()
							.Title("[red]Select logging level[/]")
							.PageSize(25);

						foreach (var choice in choices)
						{
							selectionPrompt.AddChoice(choice.choice);
						}

						string levelOption = AnsiConsole.Prompt(selectionPrompt);

						if (levelOption.Contains("[red]Go Back[/]"))
						{
							break;
						}

						levelOption = levelOption.Replace("[red]", "").Replace("[/]", "");
						LoggingLevel newLogLevel = (LoggingLevel)Enum.Parse(typeof(LoggingLevel), levelOption, true);
						levelSwitch.MinimumLevel = (LogEventLevel)newLogLevel;

						Log.Debug($"Logging level changed to: {levelOption}");

						bool configChanged = false;

						Entities.Config newConfig = new Entities.Config();

						newConfig = currentConfig;

						newConfig.LoggingLevel = newLogLevel;

						currentConfig = newConfig;

                        var hoconConfig = new StringBuilder();
                        hoconConfig.AppendLine("# External Tools");
                        hoconConfig.AppendLine("External {");
                        hoconConfig.AppendLine($"  FFmpegPath = \"{newConfig.FFmpegPath}\"");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Download Settings");
                        hoconConfig.AppendLine("Download {");
                        hoconConfig.AppendLine("  Media {");
                        hoconConfig.AppendLine($"    DownloadAvatarHeaderPhoto = {newConfig.DownloadAvatarHeaderPhoto.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadPaidPosts = {newConfig.DownloadPaidPosts.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadPosts = {newConfig.DownloadPosts.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadArchived = {newConfig.DownloadArchived.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadStreams = {newConfig.DownloadStreams.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadStories = {newConfig.DownloadStories.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadHighlights = {newConfig.DownloadHighlights.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadMessages = {newConfig.DownloadMessages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadPaidMessages = {newConfig.DownloadPaidMessages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadImages = {newConfig.DownloadImages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadVideos = {newConfig.DownloadVideos.ToString().ToLower()}");
                        hoconConfig.AppendLine($"    DownloadAudios = {newConfig.DownloadAudios.ToString().ToLower()}");
                        hoconConfig.AppendLine("  }");
                        hoconConfig.AppendLine($"  IgnoreOwnMessages = {newConfig.IgnoreOwnMessages.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadPostsIncrementally = {newConfig.DownloadPostsIncrementally.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  BypassContentForCreatorsWhoNoLongerExist = {newConfig.BypassContentForCreatorsWhoNoLongerExist.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadDuplicatedMedia = {newConfig.DownloadDuplicatedMedia.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  SkipAds = {newConfig.SkipAds.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadPath = \"{newConfig.DownloadPath}\"");
                        hoconConfig.AppendLine($"  DownloadOnlySpecificDates = {newConfig.DownloadOnlySpecificDates.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadDateSelection = \"{newConfig.DownloadDateSelection.ToString().ToLower()}\"");
                        hoconConfig.AppendLine($"  CustomDate = \"{newConfig.CustomDate?.ToString("yyyy-MM-dd")}\"");
                        hoconConfig.AppendLine($"  ShowScrapeSize = {newConfig.ShowScrapeSize.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# File Settings");
                        hoconConfig.AppendLine("File {");
                        hoconConfig.AppendLine($"  PaidPostFileNameFormat = \"{newConfig.PaidPostFileNameFormat}\"");
                        hoconConfig.AppendLine($"  PostFileNameFormat = \"{newConfig.PostFileNameFormat}\"");
                        hoconConfig.AppendLine($"  PaidMessageFileNameFormat = \"{newConfig.PaidMessageFileNameFormat}\"");
                        hoconConfig.AppendLine($"  MessageFileNameFormat = \"{newConfig.MessageFileNameFormat}\"");
                        hoconConfig.AppendLine($"  RenameExistingFilesWhenCustomFormatIsSelected = {newConfig.RenameExistingFilesWhenCustomFormatIsSelected.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Creator-Specific Configurations");
                        hoconConfig.AppendLine("CreatorConfigs {");
                        foreach (var creatorConfig in newConfig.CreatorConfigs)
                        {
                            hoconConfig.AppendLine($"  \"{creatorConfig.Key}\" {{");
                            hoconConfig.AppendLine($"    PaidPostFileNameFormat = \"{creatorConfig.Value.PaidPostFileNameFormat}\"");
                            hoconConfig.AppendLine($"    PostFileNameFormat = \"{creatorConfig.Value.PostFileNameFormat}\"");
                            hoconConfig.AppendLine($"    PaidMessageFileNameFormat = \"{creatorConfig.Value.PaidMessageFileNameFormat}\"");
                            hoconConfig.AppendLine($"    MessageFileNameFormat = \"{creatorConfig.Value.MessageFileNameFormat}\"");
                            hoconConfig.AppendLine("  }");
                        }
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Folder Settings");
                        hoconConfig.AppendLine("Folder {");
                        hoconConfig.AppendLine($"  FolderPerPaidPost = {newConfig.FolderPerPaidPost.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  FolderPerPost = {newConfig.FolderPerPost.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  FolderPerPaidMessage = {newConfig.FolderPerPaidMessage.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  FolderPerMessage = {newConfig.FolderPerMessage.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Subscription Settings");
                        hoconConfig.AppendLine("Subscriptions {");
                        hoconConfig.AppendLine($"  IncludeExpiredSubscriptions = {newConfig.IncludeExpiredSubscriptions.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  IncludeRestrictedSubscriptions = {newConfig.IncludeRestrictedSubscriptions.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  IgnoredUsersListName = \"{newConfig.IgnoredUsersListName}\"");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Interaction Settings");
                        hoconConfig.AppendLine("Interaction {");
                        hoconConfig.AppendLine($"  NonInteractiveMode = {newConfig.NonInteractiveMode.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  NonInteractiveModeListName = \"{newConfig.NonInteractiveModeListName}\"");
                        hoconConfig.AppendLine($"  NonInteractiveModePurchasedTab = {newConfig.NonInteractiveModePurchasedTab.ToString().ToLower()}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Performance Settings");
                        hoconConfig.AppendLine("Performance {");
                        hoconConfig.AppendLine($"  Timeout = {(newConfig.Timeout.HasValue ? newConfig.Timeout.Value : -1)}");
                        hoconConfig.AppendLine($"  LimitDownloadRate = {newConfig.LimitDownloadRate.ToString().ToLower()}");
                        hoconConfig.AppendLine($"  DownloadLimitInMbPerSec = {newConfig.DownloadLimitInMbPerSec}");
                        hoconConfig.AppendLine("}");

                        hoconConfig.AppendLine("# Logging/Debug Settings");
                        hoconConfig.AppendLine("Logging {");
                        hoconConfig.AppendLine($"  LoggingLevel = \"{newConfig.LoggingLevel.ToString().ToLower()}\"");
                        hoconConfig.AppendLine("}");

                        File.WriteAllText("config.conf", hoconConfig.ToString());

						if (configChanged)
						{
							return (true, new Dictionary<string, int> { { "ConfigChanged", 0 } }, currentConfig);
						}

						break;
					}
					break;
				case "[red]Logout and Exit[/]":
					if (Directory.Exists("chrome-data"))
					{
						Log.Information("Deleting chrome-data folder");
						Directory.Delete("chrome-data", true);
					}
					if (File.Exists("auth.json"))
					{
						Log.Information("Deleting auth.json");
						File.Delete("auth.json");
					}
					return (false, null, currentConfig); // Return false to indicate exit
				case "[red]Exit[/]":
					return (false, null, currentConfig); // Return false to indicate exit
			}
		}

		return (true, selectedUsers, currentConfig); // Return true to indicate selected users
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
				"[red]Download Single Post[/]",
				"[red]Download Single Paid Message[/]",
				"[red]Download Purchased Tab[/]",
				"[red]Edit config.conf[/]",
				"[red]Change logging level[/]",
				"[red]Logout and Exit[/]",
				"[red]Exit[/]"
			};
		}
		else
		{
			return new List<string>
			{
				"[red]Select All[/]",
				"[red]Custom[/]",
				"[red]Download Single Post[/]",
				"[red]Download Single Paid Message[/]",
				"[red]Download Purchased Tab[/]",
				"[red]Edit config.conf[/]",
				"[red]Change logging level[/]",
				"[red]Logout and Exit[/]",
				"[red]Exit[/]"
			};
		}
	}

	static bool ValidateFilePath(string path)
	{
		char[] invalidChars = System.IO.Path.GetInvalidPathChars();
		char[] foundInvalidChars = path.Where(c => invalidChars.Contains(c)).ToArray();

		if (foundInvalidChars.Any())
		{
			AnsiConsole.Markup($"[red]Invalid characters found in path {path}:[/] {string.Join(", ", foundInvalidChars)}\n");
			return false;
		}

		if (!System.IO.File.Exists(path))
		{
			if (System.IO.Directory.Exists(path))
			{
				AnsiConsole.Markup($"[red]The provided path {path} improperly points to a directory and not a file.[/]\n");
			}
			else
			{
				AnsiConsole.Markup($"[red]The provided path {path} does not exist or is not accessible.[/]\n");
			}
			return false;
		}

		return true;
	}
	static ProgressColumn[] GetProgressColumns(bool showScrapeSize)
	{
		List<ProgressColumn> progressColumns;
		if (showScrapeSize)
		{
			progressColumns = new List<ProgressColumn>()
			{
				new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new RemainingTimeColumn()
			};
		}
		else
		{
			progressColumns = new List<ProgressColumn>()
			{
				new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()
			};
		}
		return progressColumns.ToArray();
	}

	public static string? GetFullPath(string filename)
	{
		if (File.Exists(filename))
		{
			return Path.GetFullPath(filename);
		}

		var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		foreach (var path in pathEnv.Split(Path.PathSeparator))
		{
			var fullPath = Path.Combine(path, filename);
			if (File.Exists(fullPath))
			{
				return fullPath;
			}
		}
		return null;
	}

	public static void ValidateCookieString()
	{
		string pattern = @"(auth_id=\d+)|(sess=[^;]+)";
		var matches = Regex.Matches(auth.COOKIE, pattern);

		string output = string.Join("; ", matches);

		if (!output.EndsWith(";"))
		{
			output += ";";
		}

		if(auth.COOKIE.Trim() != output.Trim())
		{
			auth.COOKIE = output;
			string newAuthString = JsonConvert.SerializeObject(auth, Formatting.Indented);
			File.WriteAllText("auth.json", newAuthString);
		}
	}

    public static void ValidateFileNameFormat(string? format, string settingName)
    {
        if(!string.IsNullOrEmpty(format) && !format.Contains("{mediaId}", StringComparison.OrdinalIgnoreCase) && !format.Contains("{filename}", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.Markup($"[red]{settingName} is not unique enough, please make sure you include either '{{mediaId}}' or '{{filename}}' to ensure that files are not overwritten with the same filename.[/]\n");
            AnsiConsole.Markup("[red]Press any key to continue.[/]\n");
            Console.ReadKey();
            Environment.Exit(2);
        }
    }
}
