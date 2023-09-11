using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Entities
{
    public class Subscriptions
    {
        public List<List> list { get; set; }
        public bool hasMore { get; set; }
        public class AvatarThumbs
        {
            public string c50 { get; set; }
            public string c144 { get; set; }
        }

        public class HeaderSize
        {
            public int? width { get; set; }
            public int? height { get; set; }
        }

        public class HeaderThumbs
        {
            public string w480 { get; set; }
            public string w760 { get; set; }
        }

        public class List
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
            public string subscribedOnDuration { get; set; }
            public bool? canReport { get; set; }
            public bool? canReceiveChatMessage { get; set; }
            public bool? hideChat { get; set; }
            public DateTime? lastSeen { get; set; }
            public bool? isPerformer { get; set; }
            public bool? isRealPerformer { get; set; }
            public SubscribedByData subscribedByData { get; set; }
            public SubscribedOnData subscribedOnData { get; set; }
            public bool? canTrialSend { get; set; }
            public bool? isBlocked { get; set; }
            public string displayName { get; set; }
            public string notice { get; set; }
        }

        public class ListsState
        {
            public object id { get; set; }
            public string type { get; set; }
            public string name { get; set; }
            public bool? hasUser { get; set; }
            public bool? canAddUser { get; set; }
        }

        public class Subscribe
        {
            public object id { get; set; }
            public int? userId { get; set; }
            public int? subscriberId { get; set; }
            public DateTime? date { get; set; }
            public int? duration { get; set; }
            public DateTime? startDate { get; set; }
            public DateTime? expireDate { get; set; }
            public object cancelDate { get; set; }
            public string? price { get; set; }
            public string? regularPrice { get; set; }
            public string? discount { get; set; }
            public int? earningId { get; set; }
            public string action { get; set; }
            public string type { get; set; }
            public object offerStart { get; set; }
            public object offerEnd { get; set; }
            public bool? isCurrent { get; set; }
        }

        public class SubscribedByData
        {
            public string? price { get; set; }
            public string? newPrice { get; set; }
            public string? regularPrice { get; set; }
            public string? subscribePrice { get; set; }
            public int? discountPercent { get; set; }
            public int? discountPeriod { get; set; }
            public DateTime? subscribeAt { get; set; }
            public DateTime? expiredAt { get; set; }
            public DateTime? renewedAt { get; set; }
            public object discountFinishedAt { get; set; }
            public object discountStartedAt { get; set; }
            public string status { get; set; }
            public bool? isMuted { get; set; }
            public string unsubscribeReason { get; set; }
            public string duration { get; set; }
            public bool? showPostsInFeed { get; set; }
            public List<Subscribe> subscribes { get; set; }
            public bool? hasActivePaidSubscriptions { get; set; }
        }

        public class SubscribedOnData
        {
            public string? price { get; set; }
            public string? newPrice { get; set; }
            public string? regularPrice { get; set; }
            public string? subscribePrice { get; set; }
            public int? discountPercent { get; set; }
            public int? discountPeriod { get; set; }
            public DateTime? subscribeAt { get; set; }
            public DateTime? expiredAt { get; set; }
            public DateTime? renewedAt { get; set; }
            public object discountFinishedAt { get; set; }
            public object discountStartedAt { get; set; }
            public object status { get; set; }
            public bool? isMuted { get; set; }
            public string unsubscribeReason { get; set; }
            public string duration { get; set; }
            public string? tipsSumm { get; set; }
            public string? subscribesSumm { get; set; }
            public string? messagesSumm { get; set; }
            public string? postsSumm { get; set; }
            public string? streamsSumm { get; set; }
            public string? totalSumm { get; set; }
            public List<Subscribe> subscribes { get; set; }
            public bool? hasActivePaidSubscriptions { get; set; }
        }
    }
}
