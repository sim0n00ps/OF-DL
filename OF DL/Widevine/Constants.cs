namespace WidevineClient.Widevine
{
    public class Constants
    {
        public static string WORKING_FOLDER { get; set; } = System.IO.Path.GetFullPath(System.IO.Path.Join(System.IO.Directory.GetCurrentDirectory(), "cdm"));
        public static string DEVICES_FOLDER { get; set; } = System.IO.Path.GetFullPath(System.IO.Path.Join(WORKING_FOLDER, "devices"));
    }
}
