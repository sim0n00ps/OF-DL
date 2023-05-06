using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities.Archived
{
    public class Archived
    {
        public string responseType { get; set; }
        public int id { get; set; }
        public DateTime postedAt { get; set; }
        public string postedAtPrecise { get; set; }
        public object expiredAt { get; set; }
        public Author author { get; set; }
        public string text { get; set; }
        public string rawText { get; set; }
        public bool lockedText { get; set; }
        public bool isFavorite { get; set; }
        public bool canReport { get; set; }
        public bool canDelete { get; set; }
        public bool canComment { get; set; }
        public bool canEdit { get; set; }
        public bool isPinned { get; set; }
        public int favoritesCount { get; set; }
        public int mediaCount { get; set; }
        public bool isMediaReady { get; set; }
        public object voting { get; set; }
        public bool isOpened { get; set; }
        public bool canToggleFavorite { get; set; }
        public object streamId { get; set; }
        public string? price { get; set; }
        public bool hasVoting { get; set; }
        public bool isAddedToBookmarks { get; set; }
        public bool isArchived { get; set; }
        public bool isPrivateArchived { get; set; }
        public bool isDeleted { get; set; }
        public bool hasUrl { get; set; }
        public bool isCouplePeopleMedia { get; set; }
        public string cantCommentReason { get; set; }
        public int commentsCount { get; set; }
        public List<MentionedUser> mentionedUsers { get; set; }
        public List<object> linkedUsers { get; set; }
        public List<object> linkedPosts { get; set; }
        public List<Medium> media { get; set; }
        public bool canViewMedia { get; set; }
        public List<object> preview { get; set; }
        public class Author
        {
            public string view { get; set; }
            public string avatar { get; set; }
            public AvatarThumbs avatarThumbs { get; set; }
            public string header { get; set; }
            public HeaderSize headerSize { get; set; }
            public HeaderThumbs headerThumbs { get; set; }
            public int id { get; set; }
            public string name { get; set; }
            public string username { get; set; }
            public bool canLookStory { get; set; }
            public bool canCommentStory { get; set; }
            public bool hasNotViewedStory { get; set; }
            public bool isVerified { get; set; }
            public bool canPayInternal { get; set; }
            public bool hasScheduledStream { get; set; }
            public bool hasStream { get; set; }
            public bool hasStories { get; set; }
            public bool tipsEnabled { get; set; }
            public bool tipsTextEnabled { get; set; }
            public int tipsMin { get; set; }
            public int tipsMinInternal { get; set; }
            public int tipsMax { get; set; }
            public bool canEarn { get; set; }
            public bool canAddSubscriber { get; set; }
            public string? subscribePrice { get; set; }
            public List<SubscriptionBundle> subscriptionBundles { get; set; }
            public bool isPaywallRequired { get; set; }
            public bool unprofitable { get; set; }
            public bool? isMuted { get; set; }
            public bool? isRestricted { get; set; }
            public bool? canRestrict { get; set; }
            public bool? subscribedBy { get; set; }
            public bool? subscribedByExpire { get; set; }
            public DateTime? subscribedByExpireDate { get; set; }
            public bool? subscribedByAutoprolong { get; set; }
            public bool? subscribedIsExpiredNow { get; set; }
            public string? currentSubscribePrice { get; set; }
            public bool? subscribedOn { get; set; }
            public object? subscribedOnExpiredNow { get; set; }
            public object? subscribedOnDuration { get; set; }
            public bool? showPostsInFeed { get; set; }
            public bool? canTrialSend { get; set; }
        }

        public class AvatarThumbs
        {
            public string c50 { get; set; }
            public string c144 { get; set; }
        }

        public class Files
        {
            public Preview preview { get; set; }
        }

        public class HeaderSize
        {
            public int width { get; set; }
            public int height { get; set; }
        }

        public class HeaderThumbs
        {
            public string w480 { get; set; }
            public string w760 { get; set; }
        }

        public class Info
        {
            public Source source { get; set; }
            public Preview preview { get; set; }
        }

        public class ListsState
        {
            public int id { get; set; }
            public string type { get; set; }
            public string name { get; set; }
            public bool hasUser { get; set; }
            public bool canAddUser { get; set; }
        }

        public class Medium
        {
            public long id { get; set; }
            public string type { get; set; }
            public bool convertedToVideo { get; set; }
            public bool canView { get; set; }
            public bool hasError { get; set; }
            public DateTime createdAt { get; set; }
            public Info info { get; set; }
            public Source source { get; set; }
            public string squarePreview { get; set; }
            public string full { get; set; }
            public string preview { get; set; }
            public string thumb { get; set; }
            public Files files { get; set; }
            public VideoSources videoSources { get; set; }
        }

        public class MentionedUser
        {
            public string view { get; set; }
            public string avatar { get; set; }
            public AvatarThumbs avatarThumbs { get; set; }
            public string header { get; set; }
            public HeaderSize headerSize { get; set; }
            public HeaderThumbs headerThumbs { get; set; }
            public int id { get; set; }
            public string name { get; set; }
            public string? username { get; set; }
            public bool? canLookStory { get; set; }
            public bool? canCommentStory { get; set; }
            public bool? hasNotViewedStory { get; set; }
            public bool? isVerified { get; set; }
            public bool? canPayInternal { get; set; }
            public bool? hasScheduledStream { get; set; }
            public bool hasStream { get; set; }
            public bool? hasStories { get; set; }
            public bool? tipsEnabled { get; set; }
            public bool? tipsTextEnabled { get; set; }
            public int? tipsMin { get; set; }
            public int? tipsMinInternal { get; set; }
            public int? tipsMax { get; set; }
            public bool? canEarn { get; set; }
            public bool? canAddSubscriber { get; set; }
            public string? subscribePrice { get; set; }
            public bool? isPaywallRequired { get; set; }
            public bool? unprofitable { get; set; }
            public List<ListsState> listsStates { get; set; }
            public bool? isMuted { get; set; }
            public bool? isRestricted { get; set; }
            public bool? canRestrict { get; set; }
            public bool? subscribedBy { get; set; }
            public object? subscribedByExpire { get; set; }
            public object? subscribedByExpireDate { get; set; }
            public object? subscribedByAutoprolong { get; set; }
            public object? subscribedIsExpiredNow { get; set; }
            public string? currentSubscribePrice { get; set; }
            public object? subscribedOn { get; set; }
            public object? subscribedOnExpiredNow { get; set; }
            public object? subscribedOnDuration { get; set; }
            public int? callPrice { get; set; }
            public DateTime? lastSeen { get; set; }
            public bool canReport { get; set; }
            public List<object> subscriptionBundles { get; set; }
        }

        public class Preview
        {
            public int width { get; set; }
            public int height { get; set; }
            public int size { get; set; }
            public string url { get; set; }
        }

        public class Source
        {
            public string source { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public int size { get; set; }
            public int duration { get; set; }
        }

        public class SubscriptionBundle
        {
            public int id { get; set; }
            public int discount { get; set; }
            public int duration { get; set; }
            public string? price { get; set; }
            public bool canBuy { get; set; }
        }

        public class VideoSources
        {
            [JsonProperty("720")]
            public object _720 { get; set; }

            [JsonProperty("240")]
            public object _240 { get; set; }
        }
    }
}
