namespace OF_DL.Entities.Messages
{
    public static class ConvertSingleMessageMediaToPurchaseMedia
    {
        public static Purchased.Purchased.Medium Convert(SingleMessageMedium singlemessagemedium)
        {
            return new Purchased.Purchased.Medium()
            {
                canView = singlemessagemedium.canView,
                duration = singlemessagemedium.duration,
                hasError = singlemessagemedium.hasError,
                id = singlemessagemedium.id,
                locked = singlemessagemedium.locked,
                preview = singlemessagemedium.preview,
                squarePreview = singlemessagemedium.squarePreview,
                src = singlemessagemedium.src,
                thumb = singlemessagemedium.thumb,
                type = singlemessagemedium.type,
                

                //video = singlemessagemedium.video,
                //source = singlemessagemedium.source,
                //videoSources = singlemessagemedium.videoSources,
                files = null,
                //info = singlemessagemedium.info
            };
        }

    }
}
