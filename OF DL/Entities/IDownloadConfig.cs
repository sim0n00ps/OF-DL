using OF_DL.Enumerations;

namespace OF_DL.Entities
{
    public interface IDownloadConfig
    {
        bool DownloadAvatarHeaderPhoto { get; set; }
        bool DownloadPaidPosts { get; set; }
        bool DownloadPosts { get; set; }
        bool DownloadArchived { get; set; }
        bool DownloadStreams { get; set; }
        bool DownloadStories { get; set; }
        bool DownloadHighlights { get; set; }
        bool DownloadMessages { get; set; }
        bool DownloadPaidMessages { get; set; }
        bool DownloadImages { get; set; }
        bool DownloadVideos { get; set; }
        bool DownloadAudios { get; set; }

        int? Timeout { get; set; }
        bool FolderPerPaidPost { get; set; }
        bool FolderPerPost { get; set; }
        bool FolderPerPaidMessage { get; set; }
        bool FolderPerMessage { get; set; }

        bool RenameExistingFilesWhenCustomFormatIsSelected { get; set; }
        bool ShowScrapeSize { get; set; }
        bool LimitDownloadRate { get; set; }
        int DownloadLimitInMbPerSec { get; set; }
        string? FFmpegPath { get; set; }

        bool SkipAds { get; set; }

        #region Download Date Configurations

        bool DownloadOnlySpecificDates { get; set; }

        // This enum will define if we want data from before or after the CustomDate.
        DownloadDateSelection DownloadDateSelection { get; set; }

        // This is the specific date used in combination with the above enum.
        DateTime? CustomDate { get; set; }
        #endregion

        bool DownloadPostsIncrementally { get; set; }
    }

}
