namespace OF_DL.Entities
{
    public class FileNameFormatConfig : IFileNameFormatConfig
    {
        public string? PaidPostFileNameFormat { get; set; }
        public string? PostFileNameFormat { get; set; }
        public string? PaidMessageFileNameFormat { get; set; }
        public string? MessageFileNameFormat { get; set; }
    }

}
