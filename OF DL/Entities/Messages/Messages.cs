using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities.Messages
{
    public class Messages
    {
        public List<List> list { get; set; }
        public bool hasMore { get; set; }
        public class AvatarThumbs
        {
            public string c50 { get; set; }
            public string c144 { get; set; }
        }

        public class FromUser
        {
            public string view { get; set; }
            public string avatar { get; set; }
            public AvatarThumbs avatarThumbs { get; set; }
            public string header { get; set; }
            public HeaderSize headerSize { get; set; }
            public HeaderThumbs headerThumbs { get; set; }
            public int? id { get; set; }
            public string? name { get; set; }
            public string? username { get; set; }
            public bool? canLookStory { get; set; }
            public bool? canCommentStory { get; set; }
            public bool? hasNotViewedStory { get; set; }
            public bool? isVerified { get; set; }
            public bool? canPayInternal { get; set; }
            public bool? hasScheduledStream { get; set; }
            public bool? hasStream { get; set; }
            public bool? hasStories { get; set; }
            public bool? tipsEnabled { get; set; }
            public bool? tipsTextEnabled { get; set; }
            public int? tipsMin { get; set; }
            public int? tipsMinInternal { get; set; }
            public int? tipsMax { get; set; }
            public bool? canEarn { get; set; }
            public bool? canAddSubscriber { get; set; }
            public string? subscribePrice { get; set; }
            public string? displayName { get; set; }
            public string? notice { get; set; }
            public bool? isPaywallRequired { get; set; }
            public bool? unprofitable { get; set; }
            public List<ListsState> listsStates { get; set; }
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
            public bool? subscribedOnExpiredNow { get; set; }
            public string? subscribedOnDuration { get; set; }
            public string? callPrice { get; set; }
            public DateTime? lastSeen { get; set; }
            public bool? canReport { get; set; }
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

        public class List
        {
            public string? responseType { get; set; }
            public string? text { get; set; }
            public object? giphyId { get; set; }
            public bool? lockedText { get; set; }
            public bool? isFree { get; set; }
            public string? price { get; set; }
            public bool? isMediaReady { get; set; }
            public int? mediaCount { get; set; }
            public List<Medium> media { get; set; }
            public List<object> previews { get; set; }
            public bool? isTip { get; set; }
            public bool? isReportedByMe { get; set; }
            public bool? isCouplePeopleMedia { get; set; }
            public object? queueId { get; set; }
            public FromUser fromUser { get; set; }
            public bool? isFromQueue { get; set; }
            public bool? canUnsendQueue { get; set; }
            public int? unsendSecondsQueue { get; set; }
            public long? id { get; set; }
            public bool? isOpened { get; set; }
            public bool? isNew { get; set; }
            public DateTime? createdAt { get; set; }
            public DateTime? changedAt { get; set; }
            public int? cancelSeconds { get; set; }
            public bool? isLiked { get; set; }
            public bool? canPurchase { get; set; }
            public string? canPurchaseReason { get; set; }
            public bool? canReport { get; set; }
            public bool? canBePinned { get; set; }
            public bool? isPinned { get; set; }
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
            public object id { get; set; }
            public bool canView { get; set; }
            public string type { get; set; }
            public object src { get; set; }
            public object preview { get; set; }
            public object thumb { get; set; }
            public object locked { get; set; }
            public int duration { get; set; }
            public bool hasError { get; set; }
            public VideoSources videoSources { get; set; }
            public Source source { get; set; }
            public Info info { get; set; }
        }

        public class Preview
        {
            public int width { get; set; }
            public int height { get; set; }
            public int size { get; set; }
        }

        public class Source
        {
            public object source { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public int size { get; set; }
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
