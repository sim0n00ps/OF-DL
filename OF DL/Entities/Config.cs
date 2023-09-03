using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities
{
    public class Config
    {
        public bool DownloadAvatarHeaderPhoto { get; set; }
        public bool DownloadPaidPosts { get; set; }
        public bool DownloadPosts { get; set; }
        public bool DownloadArchived { get; set; }
        public bool DownloadStories { get; set; }
        public bool DownloadHighlights { get; set; }
        public bool DownloadMessages { get; set; }
        public bool DownloadPaidMessages { get; set; }
        public bool DownloadImages { get; set; }
        public bool DownloadVideos { get; set; }
        public bool DownloadAudios { get; set; }
        public bool IncludeExpiredSubscriptions { get; set; }
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
    }
}
