using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Purchased;
using Spectre.Console;

namespace OF_DL.Helpers
{
    public interface IDownloadHelper
    {
        Task<long> CalculateTotalFileSize(List<string> urls, Auth auth);
        Task<bool> DownloadArchivedPostDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string filenameFormat, Archived.List postInfo, Archived.Medium postMedia, Archived.Author author, Dictionary<string, int> users);
        Task<bool> DownloadArchivedMedia(string url, string folder, long media_id, ProgressTask task, string filenameFormat, Archived.List messageInfo, Archived.Medium messageMedia, Archived.Author author, Dictionary<string, int> users);
        Task DownloadAvatarHeader(string? avatarUrl, string? headerUrl, string folder);
        Task<bool> DownloadMessageDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string filenameFormat, Messages.List messageInfo, Messages.Medium messageMedia, Messages.FromUser fromUser, Dictionary<string, int> users);
        Task<bool> DownloadMessageMedia(string url, string folder, long media_id, ProgressTask task, string filenameFormat, Messages.List messageInfo, Messages.Medium messageMedia, Messages.FromUser fromUser, Dictionary<string, int> users);
        Task<bool> DownloadPostDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string filenameFormat, Post.List postInfo, Post.Medium postMedia, Post.Author author, Dictionary<string, int> users);
        Task<bool> DownloadPostMedia(string url, string folder, long media_id, ProgressTask task, string filenameFormat, Post.List postInfo, Post.Medium postMedia, Post.Author author, Dictionary<string, int> users);
        Task<bool> DownloadPurchasedMedia(string url, string folder, long media_id, ProgressTask task, string filenameFormat, Purchased.List messageInfo, Purchased.Medium messageMedia, Purchased.FromUser fromUser, Dictionary<string, int> users);
        Task<bool> DownloadPurchasedMessageDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string filenameFormat, Purchased.List messageInfo, Purchased.Medium messageMedia, Purchased.FromUser fromUser, Dictionary<string, int> users);
        Task<bool> DownloadPurchasedPostDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string filenameFormat, Purchased.List postInfo, Purchased.Medium postMedia, Purchased.FromUser fromUser, Dictionary<string, int> users);
        Task<bool> DownloadPurchasedPostMedia(string url, string folder, long media_id, ProgressTask task, string filenameFormat, Purchased.List messageInfo, Purchased.Medium messageMedia, Purchased.FromUser fromUser, Dictionary<string, int> users);
        Task<bool> DownloadStoryMedia(string url, string folder, long media_id, ProgressTask task);
    }
}
