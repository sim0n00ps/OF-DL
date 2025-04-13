using Newtonsoft.Json.Linq;
using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Purchased;
using OF_DL.Entities.Streams;
using OF_DL.Enumurations;
using Spectre.Console;

namespace OF_DL.Helpers
{
    public interface IAPIHelper
    {
        Task<string> GetDecryptionKeyCDRMProject(Dictionary<string, string> drmHeaders, string licenceURL, string pssh);
        Task<string> GetDecryptionKeyCDM(Dictionary<string, string> drmHeaders, string licenceURL, string pssh);
        Task<DateTime> GetDRMMPDLastModified(string mpdUrl, string policy, string signature, string kvp);
        Task<string> GetDRMMPDPSSH(string mpdUrl, string policy, string signature, string kvp);
        Task<Dictionary<string, int>> GetLists(string endpoint, IDownloadConfig config);
        Task<List<string>> GetListUsers(string endpoint, IDownloadConfig config);
        Task<Dictionary<long, string>> GetMedia(MediaType mediatype, string endpoint, string? username, string folder, IDownloadConfig config, List<long> paid_post_ids);
        Task<PaidPostCollection> GetPaidPosts(string endpoint, string folder, string username, IDownloadConfig config, List<long> paid_post_ids, StatusContext ctx);
        Task<PostCollection> GetPosts(string endpoint, string folder, IDownloadConfig config, List<long> paid_post_ids, StatusContext ctx);
        Task<SinglePostCollection> GetPost(string endpoint, string folder, IDownloadConfig config);
        Task<StreamsCollection> GetStreams(string endpoint, string folder, IDownloadConfig config, List<long> paid_post_ids, StatusContext ctx);
        Task<ArchivedCollection> GetArchived(string endpoint, string folder, IDownloadConfig config, StatusContext ctx);
        Task<MessageCollection> GetMessages(string endpoint, string folder, IDownloadConfig config, StatusContext ctx);
        Task<PaidMessageCollection> GetPaidMessages(string endpoint, string folder, string username, IDownloadConfig config, StatusContext ctx);
        Task<Dictionary<string, int>> GetPurchasedTabUsers(string endpoint, IDownloadConfig config, Dictionary<string, int> users);
        Task<List<PurchasedTabCollection>> GetPurchasedTab(string endpoint, string folder, IDownloadConfig config, Dictionary<string, int> users);
        Task<User> GetUserInfo(string endpoint);
        Task<JObject> GetUserInfoById(string endpoint);
        Dictionary<string, string> GetDynamicHeaders(string path, string queryParam);
        Task<Dictionary<string, int>> GetActiveSubscriptions(string endpoint, bool includeRestrictedSubscriptions, IDownloadConfig config);
        Task<Dictionary<string, int>> GetExpiredSubscriptions(string endpoint, bool includeRestrictedSubscriptions, IDownloadConfig config);
        Task<string> GetDecryptionKeyOFDL(Dictionary<string, string> drmHeaders, string licenceURL, string pssh);
    }
}
