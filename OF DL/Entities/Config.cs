using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OF_DL.Enumerations;

namespace OF_DL.Entities
{
    public class Config
    {
        public bool DownloadAvatarHeaderPhoto { get; set; }
        public bool DownloadPaidPosts { get; set; }
        public bool DownloadPosts { get; set; }
        public bool DownloadArchived { get; set; }
        public bool DownloadStreams { get; set; }
        public bool DownloadStories { get; set; }
        public bool DownloadHighlights { get; set; }
        public bool DownloadMessages { get; set; }
        public bool DownloadPaidMessages { get; set; }
        public bool DownloadImages { get; set; }
        public bool DownloadVideos { get; set; }
        public bool DownloadAudios { get; set; }
        public bool IncludeExpiredSubscriptions { get; set; }
        public bool IncludeRestrictedSubscriptions { get; set; }
        public bool SkipAds { get; set; } = false;
        public string? DownloadPath { get; set; } = string.Empty;
        public string? PaidPostFileNameFormat { get; set; } = string.Empty;
        public string? PostFileNameFormat { get; set; } = string.Empty;
        public string? PaidMessageFileNameFormat { get; set; } = string.Empty;
        public string? MessageFileNameFormat { get; set; } = string.Empty;
        public bool RenameExistingFilesWhenCustomFormatIsSelected { get; set; } = false;
        public int? Timeout { get; set; } = -1;
        public bool FolderPerPaidPost { get; set; } = false;
        public bool FolderPerPost { get; set; } = false;
        public bool FolderPerPaidMessage { get; set; } = false;
        public bool FolderPerMessage { get; set; } = false;
        public bool LimitDownloadRate { get; set; } = false;
        public int DownloadLimitInMbPerSec { get; set; } = 4;

        // Indicates if you want to download only on specific dates.
        public bool DownloadOnlySpecificDates { get; set; } = false;

        // This enum will define if we want data from before or after the CustomDate.
        [JsonConverter(typeof(StringEnumConverter))]
        public DownloadDateSelection DownloadDateSelection { get; set; } = DownloadDateSelection.before;
        // This is the specific date used in combination with the above enum.

        [JsonConverter(typeof(ShortDateConverter))]
        public DateTime? CustomDate { get; set; } = null;

        public bool ShowScrapeSize { get; set; } = true;

        public bool DownloadPostsIncrementally { get; set; } = false;
        public bool NonInteractiveMode { get; set; } = false;

        public string NonInteractiveModeListName { get; set; } = string.Empty;
        public bool NonInteractiveModePurchasedTab { get; set; } = false;
        public string? FFmpegPath { get; set; } = string.Empty;
    }

}
