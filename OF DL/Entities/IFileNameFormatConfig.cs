namespace OF_DL.Entities
{
    public interface IFileNameFormatConfig
    {
        string? PaidPostFileNameFormat { get; set; }
        string? PostFileNameFormat { get; set; }
        string? PaidMessageFileNameFormat { get; set; }
        string? MessageFileNameFormat { get; set; }
    }

}
