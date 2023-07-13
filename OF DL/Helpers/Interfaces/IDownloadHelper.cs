using OF_DL.Entities;
using Spectre.Console;

namespace OF_DL.Helpers
{
    public interface IDownloadHelper
    {
        Task<long> CalculateTotalFileSize(List<string> urls, Auth auth);
        Task<bool> DownloadArchivedMedia(string url, string folder, long media_id, ProgressTask task);
        Task DownloadAvatarHeader(string? avatarUrl, string? headerUrl, string folder);
        Task<bool> DownloadMessageDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task);
        Task<bool> DownloadMessageMedia(string url, string folder, long media_id, ProgressTask task);
        Task<bool> DownloadPostDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task);
        Task<bool> DownloadPostMedia(string url, string folder, long media_id, ProgressTask task);
        Task<bool> DownloadPurchasedMedia(string url, string folder, long media_id, ProgressTask task);
        Task<bool> DownloadPurchasedMessageDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task);
        Task<bool> DownloadPurchasedPostDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task);
        Task<bool> DownloadPurchasedPostMedia(string url, string folder, long media_id, ProgressTask task);
        Task<bool> DownloadStoryMedia(string url, string folder, long media_id, ProgressTask task);
    }
}