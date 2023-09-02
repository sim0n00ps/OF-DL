using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Purchased;
using OF_DL.Entities.Stories;
using Org.BouncyCastle.Asn1.Tsp;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static OF_DL.Entities.Lists.UserList;

namespace OF_DL.Helpers;

public class DownloadHelper : IDownloadHelper
{
    private readonly IFileNameHelper _FileNameHelper;

    public DownloadHelper()
    {
        _FileNameHelper = new FileNameHelper();
    }

    #region common
    /// <summary>
    /// 
    /// </summary>
    /// <param name="path"></param>
    /// <param name="url"></param>
    /// <param name="folder"></param>
    /// <param name="media_id"></param>
    /// <param name="task"></param>
    /// <param name="filenameFormat"></param>
    /// <param name="generalInfo"></param>
    /// <param name="generalMedia"></param>
    /// <param name="generalAuthor"></param>
    /// <param name="users"></param>
    /// <returns></returns>
    public static async Task<bool> CreateDirectoriesAndDownloadMedia(string path, string url, string folder, long media_id, ProgressTask task, string filename)
    {
        try
        {
            string customFileName = string.Empty;
            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }
            string extension = Path.GetExtension(url.Split("?")[0]);

            path = UpdatePathBasedOnExtension(folder, path, extension);

            string fullPath = $"{folder}{path}/{filename}{extension}";

            return await ProcessMediaDownload(folder, media_id, fullPath, url, path, filename, extension, task);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }


    /// <summary>
    /// Updates the given path based on the file extension.
    /// </summary>
    /// <param name="folder">The parent folder.</param>
    /// <param name="path">The initial relative path.</param>
    /// <param name="extension">The file extension.</param>
    /// <returns>A string that represents the updated path based on the file extension.</returns>
    private static string UpdatePathBasedOnExtension(string folder, string path, string extension)
    {
        string subdirectory = string.Empty;

        switch (extension.ToLower())
        {
            case ".jpg":
            case ".jpeg":
            case ".png":
                subdirectory = "/Images";
                break;
            case ".mp4":
            case ".avi":
            case ".wmv":
            case ".gif":
            case ".mov":
                subdirectory = "/Videos";
                break;
            case ".mp3":
            case ".wav":
            case ".ogg":
                subdirectory = "/Audios";
                break;
        }

        if (!string.IsNullOrEmpty(subdirectory))
        {
            path += subdirectory;
            string fullPath = folder + path;

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
        }

        return path;
    }


    /// <summary>
    /// Generates a custom filename based on the given format and properties.
    /// </summary>
    /// <param name="filenameFormat">The format string for the filename.</param>
    /// <param name="postInfo">General information about the post.</param>
    /// <param name="postMedia">Media associated with the post.</param>
    /// <param name="author">Author of the post.</param>
    /// <param name="users">Dictionary containing user-related data.</param>
    /// <param name="fileNameHelper">Helper class for filename operations.</param>
    /// <returns>A Task resulting in a string that represents the custom filename.</returns>
    private static async Task<string> GenerateCustomFileName(string filename, string? filenameFormat, object? postInfo, object? postMedia, object? author, Dictionary<string, int> users, IFileNameHelper fileNameHelper)
    {
        if (string.IsNullOrEmpty(filenameFormat) || postInfo == null || postMedia == null || author == null)
        {
            //no custom filename. return the original name
            return filename;
        }

        List<string> properties = new();
        string pattern = @"\{(.*?)\}";
        MatchCollection matches = Regex.Matches(filenameFormat, pattern);
        properties.AddRange(matches.Select(match => match.Groups[1].Value));

        Dictionary<string, string> values = await fileNameHelper.GetFilename(postInfo, postMedia, author, properties, users);
        return await fileNameHelper.BuildFilename(filenameFormat, values);
    }


    private static async Task<long> GetFileSizeAsync(string url, Auth auth)
    {
        long fileSize = 0;

        try
        {
            Uri uri = new(url);

            if (uri.Host == "cdn3.onlyfans.com" && uri.LocalPath.Contains("/dash/files"))
            {
                string[] messageUrlParsed = url.Split(',');
                string mpdURL = messageUrlParsed[0];
                string policy = messageUrlParsed[1];
                string signature = messageUrlParsed[2];
                string kvp = messageUrlParsed[3];

                mpdURL = mpdURL.Replace(".mpd", "_source.mp4");

                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("Cookie", $"CloudFront-Policy={policy}; CloudFront-Signature={signature}; CloudFront-Key-Pair-Id={kvp}; {auth.COOKIE}");
                client.DefaultRequestHeaders.Add("User-Agent", auth.USER_AGENT);

                using HttpResponseMessage response = await client.GetAsync(mpdURL, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    fileSize = response.Content.Headers.ContentLength ?? 0;
                }
            }
            else
            {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", auth.USER_AGENT);
                using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    fileSize = response.Content.Headers.ContentLength ?? 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting file size for URL '{url}': {ex.Message}");
        }

        return fileSize;
    }


    /// <summary>
    /// Processes the download and database update of media.
    /// </summary>
    /// <param name="folder">The folder where the media is stored.</param>
    /// <param name="media_id">The ID of the media.</param>
    /// <param name="fullPath">The full path to the media.</param>
    /// <param name="url">The URL from where to download the media.</param>
    /// <param name="path">The relative path to the media.</param>
    /// <param name="resolvedFilename">The filename after any required manipulations.</param>
    /// <param name="extension">The file extension.</param>
    /// <param name="task">The task object for tracking progress.</param>
    /// <returns>A Task resulting in a boolean indicating whether the media is newly downloaded or not.</returns>
    public static async Task<bool> ProcessMediaDownload(string folder, long media_id, string fullPath, string url, string path, string resolvedFilename, string extension, ProgressTask task)
    {
        DBHelper dBHelper = new();

        try
        {
            if (!await dBHelper.CheckDownloaded(folder, media_id))
            {
                return await HandleNewMedia(folder, media_id, fullPath, url, path, resolvedFilename, extension, task, dBHelper);
            }
            else
            {
                return await HandlePreviouslyDownloadedMediaAsync(folder, media_id, task, dBHelper);
            }
        }
        catch (Exception ex)
        {
            // Handle exception (e.g., log it)
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }


    /// <summary>
    /// Handles new media by downloading and updating the database.
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="media_id"></param>
    /// <param name="fullPath"></param>
    /// <param name="url"></param>
    /// <param name="path"></param>
    /// <param name="resolvedFilename"></param>
    /// <param name="extension"></param>
    /// <param name="task"></param>
    /// <param name="dBHelper"></param>
    /// <returns>A Task resulting in a boolean indicating whether the media is newly downloaded or not.</returns>
    private static async Task<bool> HandleNewMedia(string folder, long media_id, string fullPath, string url, string path, string resolvedFilename, string extension, ProgressTask task, DBHelper dBHelper)
    {
        long fileSizeInBytes;
        DateTime lastModified;
        bool status;

        if (!File.Exists(fullPath))
        {
            lastModified = await DownloadFile(url, fullPath, task);
            fileSizeInBytes = GetLocalFileSize(fullPath);
            task.Increment(fileSizeInBytes);
            status = true;
        }
        else
        {
            fileSizeInBytes = GetLocalFileSize(fullPath);
            lastModified = File.GetLastWriteTime(fullPath);
            task.Increment(fileSizeInBytes);
            status = false;
        }

        await dBHelper.UpdateMedia(folder, media_id, folder + path, resolvedFilename + extension, fileSizeInBytes, true, lastModified);
        return status;
    }


    /// <summary>
    /// Handles media that has been previously downloaded and updates the task accordingly.
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="media_id"></param>
    /// <param name="task"></param>
    /// <param name="dBHelper"></param>
    /// <returns>A boolean indicating whether the media is newly downloaded or not.</returns>
    private static async Task<bool> HandlePreviouslyDownloadedMediaAsync(string folder, long media_id, ProgressTask task, DBHelper dBHelper)
    {
        long size = await dBHelper.GetStoredFileSize(folder, media_id);
        task.Increment(size);
        return false;
    }


    /// <summary>
    /// Gets the file size of the media.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>The file size in bytes.</returns>
    private static long GetLocalFileSize(string filePath)
    {
        return new FileInfo(filePath).Length;
    }


    /// <summary>
    /// Downloads a file from the given URL and saves it to the specified destination path.
    /// </summary>
    /// <param name="url">The URL to download the file from.</param>
    /// <param name="destinationPath">The path where the downloaded file will be saved.</param>
    /// <param name="task">Progress tracking object.</param>
    /// <returns>A Task resulting in a DateTime indicating the last modified date of the downloaded file.</returns>

    private static async Task<DateTime> DownloadFile(string url, string destinationPath, ProgressTask task)
    {
        using var client = new HttpClient();
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url)
        };

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStreamAsync();

        using (FileStream fileStream = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
        {
            var buffer = new byte[16384];
            int read;
            while ((read = await body.ReadAsync(buffer)) > 0)
            {
                task.Increment(read);
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
            }
        }
        File.SetLastWriteTime(destinationPath, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
        return response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now;
    }

    public async Task<long> CalculateTotalFileSize(List<string> urls, Auth auth)
    {
        long totalFileSize = 0;
        if (urls.Count > 250)
        {
            int batchSize = 250;

            var tasks = new List<Task<long>>();

            for (int i = 0; i < urls.Count; i += batchSize)
            {
                var batchUrls = urls.Skip(i).Take(batchSize).ToList();

                var batchTasks = batchUrls.Select(url => GetFileSizeAsync(url, auth));
                tasks.AddRange(batchTasks);

                await Task.WhenAll(batchTasks);

                await Task.Delay(5000);
            }

            long[] fileSizes = await Task.WhenAll(tasks);
            foreach (long fileSize in fileSizes)
            {
                totalFileSize += fileSize;
            }
        }
        else
        {
            var tasks = new List<Task<long>>();

            foreach (string url in urls)
            {
                tasks.Add(GetFileSizeAsync(url, auth));
            }

            long[] fileSizes = await Task.WhenAll(tasks);
            foreach (long fileSize in fileSizes)
            {
                totalFileSize += fileSize;
            }
        }

        return totalFileSize;
    }
    #endregion

    #region drm common

    private static async Task<bool> DownloadDrmMedia(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string customFileName, string filename, string path, DBHelper dBHelper)
    {
        //Use ytdl-p to download the MPD as a M4A and MP4 file
        ProcessStartInfo ytdlpstartInfo = new()
        {
            FileName = ytdlppath,
            Arguments = $"--allow-u --no-part --restrict-filenames -N 4 --user-agent \"{user_agent}\" --add-header \"Cookie:CloudFront-Policy={policy}; CloudFront-Signature={signature}; CloudFront-Key-Pair-Id={kvp}; {sess}\" --referer \"https://onlyfans.com/\" -o \"{folder + path + "/"}%(title)s.%(ext)s\" --format \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best[ext=m4a]\" \"{url}\"",
            CreateNoWindow = true
        };

        Process ytdlpprocess = new()
        {
            StartInfo = ytdlpstartInfo
        };
        ytdlpprocess.Start();
        ytdlpprocess.WaitForExit();

        string tempFilename = $"{folder}{path}/{filename}";

        //Remove .fx from filenames
        if (File.Exists($"{tempFilename}.f1.mp4"))
        {
            File.Move($"{tempFilename}.f1.mp4", $"{tempFilename}.mp4");
        }
        else if (File.Exists($"{tempFilename}.f2.mp4"))
        {
            File.Move($"{tempFilename}.f2.mp4", $"{tempFilename}.mp4");
        }
        else if (File.Exists($"{tempFilename}.f3.mp4"))
        {
            File.Move($"{tempFilename}.f3.mp4", $"{tempFilename}.mp4");
        }

        if (File.Exists($"{tempFilename}.f3.m4a"))
        {
            File.Move($"{tempFilename}.f3.m4a", $"{tempFilename}.m4a");
        }
        else if (File.Exists($"{tempFilename}.f4.m4a"))
        {
            File.Move($"{tempFilename}.f4.m4a", $"{tempFilename}.m4a");
        }

        //Use mp4decrypt to decrypt the MP4 and M4A files
        ProcessStartInfo mp4decryptStartInfoVideo = new()
        {
            FileName = mp4decryptpath,
            Arguments = $"--key {decryptionKey} {$"{tempFilename}"}.mp4 {$"{tempFilename}"}_vdec.mp4",
            CreateNoWindow = true
        };

        Process mp4decryptVideoProcess = new()
        {
            StartInfo = mp4decryptStartInfoVideo
        };
        mp4decryptVideoProcess.Start();
        mp4decryptVideoProcess.WaitForExit();

        ProcessStartInfo mp4decryptStartInfoAudio = new()
        {
            FileName = mp4decryptpath,
            Arguments = $"--key {decryptionKey} {$"{tempFilename}"}.m4a {$"{tempFilename}"}_adec.mp4",
            CreateNoWindow = true
        };

        Process mp4decryptAudioProcess = new()
        {
            StartInfo = mp4decryptStartInfoAudio
        };
        mp4decryptAudioProcess.Start();
        mp4decryptAudioProcess.WaitForExit();

        //Finally use FFMPEG to merge the 2 together
        ProcessStartInfo ffmpegStartInfo = new()
        {
            FileName = ffmpegpath,
            Arguments = $"-i {$"{tempFilename}"}_vdec.mp4 -i {$"{tempFilename}"}_adec.mp4 -c copy {$"{tempFilename}"}_source.mp4",
            CreateNoWindow = true
        };

        Process ffmpegProcess = new()
        {
            StartInfo = ffmpegStartInfo
        };
        ffmpegProcess.Start();
        ffmpegProcess.WaitForExit();
        File.SetLastWriteTime($"{tempFilename}_source.mp4", lastModified);
        if (!string.IsNullOrEmpty(customFileName))
        {
            File.Move($"{$"{tempFilename}"}_source.mp4", $"{folder + path + "/" + customFileName + ".mp4"}");
        }
        //Cleanup Files
        long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : tempFilename + "_source.mp4").Length;
        task.Increment(fileSizeInBytes);
        await dBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
        File.Delete($"{tempFilename}.mp4");
        File.Delete($"{tempFilename}.m4a");
        File.Delete($"{tempFilename}_adec.mp4");
        File.Delete($"{tempFilename}_vdec.mp4");

        return true;
    }
    #endregion

    #region normal posts
    public async Task<bool> DownloadPostMedia(string url, string folder, long media_id, ProgressTask task, string? filenameFormat, Post.List? postInfo, Post.Medium? postMedia, Post.Author? author, Dictionary<string, int> users, Config config)
    {
        string path;
        if (config.FolderPerPost && postInfo != null)
        {
            path = $"/Posts/Free/{postInfo.id} {postInfo.postedAt.ToString("yyyy-MM-dd HH-mm-ss")}";
        }
        else
        {
            path = "/Posts/Free";
        }
        
        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, postInfo, postMedia, author, users, _FileNameHelper);

        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, resolvedFilename);
    }


    public async Task<bool> DownloadMessageMedia(string url, string folder, long media_id, ProgressTask task, string filenameFormat, Messages.List messageInfo, Messages.Medium messageMedia, Messages.FromUser fromUser, Dictionary<string, int> users, Config config)
    {
        string path;
        if (config.FolderPerMessage && messageInfo != null)
        {
            path = $"/Messages/Free/{messageInfo.id} {messageInfo.createdAt.Value.ToString("yyyy-MM-dd HH-mm-ss")}";
        }
        else
        {
            path = "/Messages/Free";
        }
        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, messageInfo, messageMedia, fromUser, users, _FileNameHelper);
        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, resolvedFilename);
    }


    public async Task<bool> DownloadArchivedMedia(string url, string folder, long media_id, ProgressTask task, string filenameFormat, Archived.List messageInfo, Archived.Medium messageMedia, Archived.Author author, Dictionary<string, int> users)
    {
        string path = "/Archived/Posts/Free";
        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, messageInfo, messageMedia, author, users, _FileNameHelper);
        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, resolvedFilename);
    }



    public async Task<bool> DownloadStoryMedia(string url, string folder, long media_id, ProgressTask task)
    {
        string path = "/Stories/Free";
        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, filename);
    }

    public async Task<bool> DownloadPurchasedMedia(string url, string folder, long media_id, ProgressTask task, string filenameFormat, Purchased.List messageInfo, Purchased.Medium messageMedia, Purchased.FromUser fromUser, Dictionary<string, int> users, Config config)
    {
        string path;
        if (config.FolderPerPaidMessage && messageInfo != null)
        {
            path = $"/Messages/Paid/{messageInfo.id} {messageInfo.createdAt.Value.ToString("yyyy-MM-dd HH-mm-ss")}";
        }
        else
        {
            path = "/Messages/Paid";
        }
        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, messageInfo, messageMedia, fromUser, users, _FileNameHelper);
        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, resolvedFilename);
    }

    public async Task<bool> DownloadPurchasedPostMedia(string url, string folder, long media_id, ProgressTask task, string filenameFormat, Purchased.List messageInfo, Purchased.Medium messageMedia, Purchased.FromUser fromUser, Dictionary<string, int> users, Config config)
    {
        string path;
        if (config.FolderPerPaidPost && messageInfo != null)
        {
            path = $"/Posts/Paid/{messageInfo.id} {messageInfo.postedAt.Value.ToString("yyyy-MM-dd HH-mm-ss")}";
        }
        else
        {
            path = "/Posts/Paid";
        }
        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, messageInfo, messageMedia, fromUser, users, _FileNameHelper);
        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, resolvedFilename);
    }

    #endregion
    public async Task DownloadAvatarHeader(string? avatarUrl, string? headerUrl, string folder)
    {
        try
        {
            string path = $"/Profile"; // specify the path for the new folder

            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }

            if (!string.IsNullOrEmpty(avatarUrl))
            {
                string avatarpath = $"{path}/Avatars";
                if (!Directory.Exists(folder + avatarpath)) // check if the folder already exists
                {
                    Directory.CreateDirectory(folder + avatarpath); // create the new folder
                }

                Uri uri = new(avatarUrl);
                string filename = System.IO.Path.GetFileName(uri.LocalPath);
                string destinationPath = $"{folder}{avatarpath}/{filename}";

                var client = new HttpClient();

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = uri

                };
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStreamAsync();
                using (FileStream fileStream = File.Create(destinationPath))
                {
                    await body.CopyToAsync(fileStream);
                }
                File.SetLastWriteTime(destinationPath, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
            }

            if (!string.IsNullOrEmpty(headerUrl))
            {
                string headerpath = $"{path}/Headers";
                if (!Directory.Exists(folder + headerpath)) // check if the folder already exists
                {
                    Directory.CreateDirectory(folder + headerpath); // create the new folder
                }

                Uri uri = new(headerUrl);
                string filename = System.IO.Path.GetFileName(uri.LocalPath);
                string destinationPath = $"{folder}{headerpath}/{filename}";

                var client = new HttpClient();

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = uri

                };
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStreamAsync();
                using (FileStream fileStream = File.Create(destinationPath))
                {
                    await body.CopyToAsync(fileStream);
                }
                File.SetLastWriteTime(destinationPath, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
    }

    #region drm posts
    public async Task<bool> DownloadMessageDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string filenameFormat, Messages.List messageInfo, Messages.Medium messageMedia, Messages.FromUser fromUser, Dictionary<string, int> users, Config config)
    {
        try
        {
            string customFileName = string.Empty;
            string path;
            Uri uri = new(url);
            string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
            if (config.FolderPerMessage && messageInfo != null)
            {
                path = $"/Messages/Free/{messageInfo.id} {messageInfo.createdAt.Value.ToString("yyyy-MM-dd HH-mm-ss")}/Videos";
            }
            else
            {
                path = "/Messages/Free/Videos";
            }
            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }
            DBHelper dBHelper = new();

            if (!string.IsNullOrEmpty(filenameFormat) && messageInfo != null && messageMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(messageInfo, messageMedia, fromUser, properties, users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await dBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(ytdlppath, mp4decryptpath, ffmpegpath, user_agent, policy, signature, kvp, sess, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path, dBHelper);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    task.Increment(fileSizeInBytes);
                    await dBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                long size = await dBHelper.GetStoredFileSize(folder, media_id);
                task.Increment(size);
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }


    public async Task<bool> DownloadPurchasedMessageDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string filenameFormat, Purchased.List messageInfo, Purchased.Medium messageMedia, Purchased.FromUser fromUser, Dictionary<string, int> users, Config config)
    {
        try
        {
            string customFileName = string.Empty;
            string path;
            Uri uri = new(url);
            string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
            if (config.FolderPerPaidMessage && messageInfo != null)
            {
                path = $"/Messages/Paid/{messageInfo.id} {messageInfo.createdAt.Value.ToString("yyyy-MM-dd HH-mm-ss")}/Videos";
            }
            else
            {
                path = "/Messages/Paid/Videos";
            }
            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }
            DBHelper dBHelper = new();
            if (!string.IsNullOrEmpty(filenameFormat) && messageInfo != null && messageMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(messageInfo, messageMedia, fromUser, properties, users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await dBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(ytdlppath, mp4decryptpath, ffmpegpath, user_agent, policy, signature, kvp, sess, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path, dBHelper);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    task.Increment(fileSizeInBytes);
                    await dBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                long size = await dBHelper.GetStoredFileSize(folder, media_id);
                task.Increment(size);
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }


    public async Task<bool> DownloadPostDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string filenameFormat, Post.List postInfo, Post.Medium postMedia, Post.Author author, Dictionary<string, int> users, Config config)
    {
        try
        {
            string customFileName = string.Empty;
            string path;
            Uri uri = new(url);
            string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
            if (config.FolderPerPost && postInfo != null)
            {
                path = $"/Posts/Free/{postInfo.id} {postInfo.postedAt.ToString("yyyy-MM-dd HH-mm-ss")}/Videos";
            }
            else
            {
                path = "/Posts/Free/Videos";
            }
            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }
            DBHelper dBHelper = new();
            if (!string.IsNullOrEmpty(filenameFormat) && postInfo != null && postMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(postInfo, postMedia, author, properties, users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await dBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(ytdlppath, mp4decryptpath, ffmpegpath, user_agent, policy, signature, kvp, sess, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path, dBHelper);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    task.Increment(fileSizeInBytes);
                    await dBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                long size = await dBHelper.GetStoredFileSize(folder, media_id);
                task.Increment(size);
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }

    public async Task<bool> DownloadPurchasedPostDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string filenameFormat, Purchased.List postInfo, Purchased.Medium postMedia, Purchased.FromUser fromUser, Dictionary<string, int> users, Config config)
    {
        try
        {
            string customFileName = string.Empty;
            string path;
            Uri uri = new(url);
            string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
            if (config.FolderPerPaidPost && postInfo != null)
            {
                path = $"/Posts/Paid/{postInfo.id} {postInfo.postedAt.Value.ToString("yyyy-MM-dd HH-mm-ss")}/Videos";
            }
            else
            {
                path = "/Posts/Paid/Videos";
            }
            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }
            DBHelper dBHelper = new();
            if (!string.IsNullOrEmpty(filenameFormat) && postInfo != null && postMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(postInfo, postMedia, fromUser, properties, users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await dBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(ytdlppath, mp4decryptpath, ffmpegpath, user_agent, policy, signature, kvp, sess, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path, dBHelper);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    task.Increment(fileSizeInBytes);
                    await dBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                long size = await dBHelper.GetStoredFileSize(folder, media_id);
                task.Increment(size);
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }


    public async Task<bool> DownloadArchivedPostDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string filenameFormat, Archived.List postInfo, Archived.Medium postMedia, Archived.Author author, Dictionary<string, int> users)
    {
        try
        {
            string customFileName = string.Empty;
            Uri uri = new(url);
            string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
            string path = "/Archived/Posts/Free/Videos";
            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }
            DBHelper dBHelper = new();
            if (!string.IsNullOrEmpty(filenameFormat) && postInfo != null && postMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(postInfo, postMedia, author, properties, users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await dBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(ytdlppath, mp4decryptpath, ffmpegpath, user_agent, policy, signature, kvp, sess, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path, dBHelper);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    task.Increment(fileSizeInBytes);
                    await dBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                long size = await dBHelper.GetStoredFileSize(folder, media_id);
                task.Increment(size);
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);

            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }
    #endregion
}
