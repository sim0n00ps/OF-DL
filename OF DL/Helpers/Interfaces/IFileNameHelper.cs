namespace OF_DL.Helpers
{
    public interface IFileNameHelper
    {
        Task<string> BuildFilename(string fileFormat, Dictionary<string, string> values);
        Task<Dictionary<string, string>> GetFilename(object obj1, object obj2, object obj3, List<string> selectedProperties, string username, Dictionary<string, int> users = null);
    }
}
