using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OF_DL.Entities.Messages.Messages;

namespace OF_DL.Entities.Purchased
{
    public class Purchased
    {
        public string responseType { get; set; }
            public string text { get; set; }
            public object giphyId { get; set; }
            public bool lockedText { get; set; }
            public bool isFree { get; set; }
            public double price { get; set; }
            public bool isMediaReady { get; set; }
            public int mediaCount { get; set; }
            public List<Medium> media { get; set; }
            public List<object> previews { get; set; }
            public bool isTip { get; set; }
            public bool isReportedByMe { get; set; }
            public bool isCouplePeopleMedia { get; set; }
            public object queueId { get; set; }
            public FromUser fromUser { get; set; }
            public bool isFromQueue { get; set; }
            public bool canUnsendQueue { get; set; }
            public int unsendSecondsQueue { get; set; }
            public object id { get; set; }
            public bool isOpened { get; set; }
            public bool isNew { get; set; }
            public DateTime createdAt { get; set; }
            public DateTime changedAt { get; set; }
            public int cancelSeconds { get; set; }
            public bool isLiked { get; set; }
            public bool canPurchase { get; set; }
            public bool canReport { get; set; }
            public bool? isCanceled { get; set; }
            public DateTime? postedAt { get; set; }
            public string postedAtPrecise { get; set; }
            public object expiredAt { get; set; }
            public Author author { get; set; }
            public string rawText { get; set; }
            public bool? isFavorite { get; set; }
            public bool? canDelete { get; set; }
            public bool? canComment { get; set; }
            public bool? canEdit { get; set; }
            public bool? isPinned { get; set; }
            public int? favoritesCount { get; set; }
            public List<object> voting { get; set; }
            public bool? canToggleFavorite { get; set; }
            public object streamId { get; set; }
            public bool? hasVoting { get; set; }
            public bool? isAddedToBookmarks { get; set; }
            public bool? isArchived { get; set; }
            public bool? isPrivateArchived { get; set; }
            public bool? isDeleted { get; set; }
            public bool? hasUrl { get; set; }
            public string cantCommentReason { get; set; }
            public int? commentsCount { get; set; }
            public List<object> mentionedUsers { get; set; }
            public List<object> linkedUsers { get; set; }
            public List<object> linkedPosts { get; set; }
            public bool? canViewMedia { get; set; }
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
		public class Drm
		{
			public Manifest manifest { get; set; }
			public Signature signature { get; set; }
		}
		public class Dash
		{
			[JsonProperty("CloudFront-Policy")]
			public string CloudFrontPolicy { get; set; }

			[JsonProperty("CloudFront-Signature")]
			public string CloudFrontSignature { get; set; }

			[JsonProperty("CloudFront-Key-Pair-Id")]
			public string CloudFrontKeyPairId { get; set; }
		}
		public class Hls
		{
			[JsonProperty("CloudFront-Policy")]
			public string CloudFrontPolicy { get; set; }

			[JsonProperty("CloudFront-Signature")]
			public string CloudFrontSignature { get; set; }

			[JsonProperty("CloudFront-Key-Pair-Id")]
			public string CloudFrontKeyPairId { get; set; }
		}
		public class Manifest
		{
			public string hls { get; set; }
			public string dash { get; set; }
		}
		public class Signature
		{
			public Hls hls { get; set; }
			public Dash dash { get; set; }
		}
		public class Files
        {
            public Preview preview { get; set; }
			public Drm drm { get; set; }
		}

        public class FromUser
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
            public double subscribePrice { get; set; }
            public List<SubscriptionBundle> subscriptionBundles { get; set; }
            public bool isPaywallRequired { get; set; }
            public List<ListsState> listsStates { get; set; }
            public bool isMuted { get; set; }
            public bool isRestricted { get; set; }
            public bool canRestrict { get; set; }
            public bool? subscribedBy { get; set; }
            public bool? subscribedByExpire { get; set; }
            public DateTime subscribedByExpireDate { get; set; }
            public bool? subscribedByAutoprolong { get; set; }
            public bool subscribedIsExpiredNow { get; set; }
            public double? currentSubscribePrice { get; set; }
            public bool? subscribedOn { get; set; }
            public bool? subscribedOnExpiredNow { get; set; }
            public string subscribedOnDuration { get; set; }
            public int callPrice { get; set; }
            public DateTime? lastSeen { get; set; }
            public bool canReport { get; set; }
            public string displayName { get; set; }
            public string notice { get; set; }
            public bool? unprofitable { get; set; }
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
            public bool canView { get; set; }
            public string type { get; set; }
            public string src { get; set; }
            public string preview { get; set; }
            public string thumb { get; set; }
            public object locked { get; set; }
            public int duration { get; set; }
            public bool hasError { get; set; }
            public string squarePreview { get; set; }
            public Video video { get; set; }
            public VideoSources videoSources { get; set; }
            public Source source { get; set; }
            public Info info { get; set; }
            public bool? convertedToVideo { get; set; }
            public DateTime? createdAt { get; set; }
            public string full { get; set; }
            public Files files { get; set; }
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
            public int? width { get; set; }
            public int? height { get; set; }
            public int? size { get; set; }
            public int? duration { get; set; }
        }

        public class SubscriptionBundle
        {
            public int id { get; set; }
            public int discount { get; set; }
            public int duration { get; set; }
            public double price { get; set; }
            public bool canBuy { get; set; }
        }

        public class Video
        {
            public string mp4 { get; set; }
        }

        public class VideoSources
        {
            [JsonProperty("720")]
            public string _720 { get; set; }

            [JsonProperty("240")]
            public string _240 { get; set; }
        }
    }
}
