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
        public List<List> list { get; set; }
        public bool hasMore { get; set; }
        public class Dash
        {
            [JsonProperty("CloudFront-Policy")]
            public string CloudFrontPolicy { get; set; }

            [JsonProperty("CloudFront-Signature")]
            public string CloudFrontSignature { get; set; }

            [JsonProperty("CloudFront-Key-Pair-Id")]
            public string CloudFrontKeyPairId { get; set; }
        }

        public class Drm
        {
            public Manifest manifest { get; set; }
            public Signature signature { get; set; }
        }

        public class Files
        {
            public Drm drm { get; set; }
        }

        public class FromUser
        {
            public int id { get; set; }
            public string _view { get; set; }
        }
        public class Author
        {
            public int id { get; set; }
            public string _view { get; set; }
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

        public class Info
        {
            public Source source { get; set; }
            public Preview preview { get; set; }
        }

        public class List
        {
            public string responseType { get; set; }
            public string text { get; set; }
            public object giphyId { get; set; }
            public bool? lockedText { get; set; }
            public bool? isFree { get; set; }
            public string? price { get; set; }
            public bool? isMediaReady { get; set; }
            public int? mediaCount { get; set; }
            public List<Medium> media { get; set; }
            public List<object> previews { get; set; }
            public List<object> preview { get; set; }
            public bool? isTip { get; set; }
            public bool? isReportedByMe { get; set; }
            public bool? isCouplePeopleMedia { get; set; }
            public object queueId { get; set; }
            public FromUser fromUser { get; set; }
            public Author author { get; set; }
            public bool? isFromQueue { get; set; }
            public bool? canUnsendQueue { get; set; }
            public int? unsendSecondsQueue { get; set; }
            public long id { get; set; }
            public bool isOpened { get; set; }
            public bool? isNew { get; set; }
            public DateTime? createdAt { get; set; }
            public DateTime? postedAt { get; set; }
            public DateTime? changedAt { get; set; }
            public int? cancelSeconds { get; set; }
            public bool? isLiked { get; set; }
            public bool? canPurchase { get; set; }
            public bool? canReport { get; set; }
            public bool? isCanceled { get; set; }
            public bool? isArchived { get; set; }
        }

        public class Manifest
        {
            public string hls { get; set; }
            public string dash { get; set; }
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
            public int? duration { get; set; }
            public bool? hasError { get; set; }
            public string squarePreview { get; set; }
            public VideoSources videoSources { get; set; }
            public Source source { get; set; }
            public Info info { get; set; }
            public Video video { get; set; }
            public Files files { get; set; }
        }

        public class Preview
        {
            public int? width { get; set; }
            public int? height { get; set; }
            public int? size { get; set; }
        }

        public class Signature
        {
            public Hls hls { get; set; }
            public Dash dash { get; set; }
        }

        public class Source
        {
            public string source { get; set; }
            public int? width { get; set; }
            public int? height { get; set; }
            public int? size { get; set; }
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
