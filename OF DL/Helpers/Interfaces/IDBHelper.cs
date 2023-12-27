namespace OF_DL.Helpers
{
    public interface IDBHelper
    {
        Task AddMedia(string folder, long media_id, long post_id, string link, string? directory, string? filename, long? size, string api_type, string media_type, bool preview, bool downloaded, DateTime? created_at);
        Task AddMessage(string folder, long post_id, string message_text, string price, bool is_paid, bool is_archived, DateTime created_at, int user_id);
        Task AddPost(string folder, long post_id, string message_text, string price, bool is_paid, bool is_archived, DateTime created_at);
        Task AddStory(string folder, long post_id, string message_text, string price, bool is_paid, bool is_archived, DateTime created_at);
        Task<bool> CheckDownloaded(string folder, long media_id);
        Task CreateDB(string folder);
        Task CreateUsersDB(Dictionary<string, int> users);
        Task CheckUsername(KeyValuePair<string, int> user, string path);
        Task<long> GetStoredFileSize(string folder, long media_id);
        Task UpdateMedia(string folder, long media_id, string directory, string filename, long size, bool downloaded, DateTime created_at);
        Task<DateTime?> GetMostRecentPostDate(string folder);
    }
}
