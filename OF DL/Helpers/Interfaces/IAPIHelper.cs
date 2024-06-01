using Newtonsoft.Json.Linq;
using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Purchased;
using OF_DL.Entities.Streams;
using OF_DL.Enumurations;

namespace OF_DL.Helpers
{
    public interface IAPIHelper
    {
        Task<string> GetDecryptionKey(Dictionary<string, string> drmHeaders, string licenceURL, string pssh);
        Task<string> GetDecryptionKeyNew(Dictionary<string, string> drmHeaders, string licenceURL, string pssh);
        Task<DateTime> GetDRMMPDLastModified(string mpdUrl, string policy, string signature, string kvp);
        Task<string> GetDRMMPDPSSH(string mpdUrl, string policy, string signature, string kvp);
        Task<Dictionary<string, int>> GetLists(string endpoint);
        Task<List<string>> GetListUsers(string endpoint);
        Task<Dictionary<long, string>> GetMedia(MediaType mediatype, string endpoint, string? username, string folder, IDownloadConfig config, List<long> paid_post_ids);
        Task<PaidPostCollection> GetPaidPosts(string endpoint, string folder, string username, IDownloadConfig config, List<long> paid_post_ids);
        Task<PostCollection> GetPosts(string endpoint, string folder, IDownloadConfig config, List<long> paid_post_ids);
        Task<SinglePostCollection> GetPost(string endpoint, string folder, IDownloadConfig config);
        Task<StreamsCollection> GetStreams(string endpoint, string folder, IDownloadConfig config, List<long> paid_post_ids);
        Task<ArchivedCollection> GetArchived(string endpoint, string folder, IDownloadConfig config);
        Task<MessageCollection> GetMessages(string endpoint, string folder, IDownloadConfig config);
        Task<PaidMessageCollection> GetPaidMessages(string endpoint, string folder, string username, IDownloadConfig config);
        Task<Dictionary<string, int>> GetPurchasedTabUsers(string endpoint, IDownloadConfig config, Dictionary<string, int> users);
        Task<List<PurchasedTabCollection>> GetPurchasedTab(string endpoint, string folder, IDownloadConfig config, Dictionary<string, int> users);
        Task<User> GetUserInfo(string endpoint);
        Task<JObject> GetUserInfoById(string endpoint);
        Task<Dictionary<string, string>> GetDynamicHeaders(string path, string queryParam);
        Task<Dictionary<string, int>> GetActiveSubscriptions(string endpoint, bool includeRestrictedSubscriptions);
        Task<Dictionary<string, int>> GetExpiredSubscriptions(string endpoint, bool includeRestrictedSubscriptions);
    }
}
