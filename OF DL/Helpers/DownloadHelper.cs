using OF_DL.Entities;
using OF_DL.Entities.Archived;
using OF_DL.Entities.Messages;
using OF_DL.Entities.Post;
using OF_DL.Entities.Purchased;
using OF_DL.Entities.Stories;
using OF_DL.Entities.Streams;
using OF_DL.Enumerations;
using OF_DL.Utils;
using Org.BouncyCastle.Asn1.Tsp;
using Org.BouncyCastle.Asn1.X509;
using Serilog;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static OF_DL.Entities.Lists.UserList;

namespace OF_DL.Helpers;

public class DownloadHelper : IDownloadHelper
{
    private readonly Auth auth;
    private readonly IDBHelper m_DBHelper;
    private readonly IFileNameHelper _FileNameHelper;
    private readonly IDownloadConfig downloadConfig;
    private readonly IFileNameFormatConfig fileNameFormatConfig;

    public DownloadHelper(Auth auth, IDownloadConfig downloadConfig, IFileNameFormatConfig fileNameFormatConfig)
    {
        this.auth = auth;
        this.m_DBHelper = new DBHelper();
        this._FileNameHelper = new FileNameHelper(auth);
        this.downloadConfig = downloadConfig;
        this.fileNameFormatConfig = fileNameFormatConfig;
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
    protected async Task<bool> CreateDirectoriesAndDownloadMedia(string path,
                                                                     string url,
                                                                     string folder,
                                                                     long media_id,
                                                                     ProgressTask task,
                                                                     string serverFileName,
                                                                     string resolvedFileName)
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

            return await ProcessMediaDownload(folder, media_id, url, path, serverFileName, resolvedFileName, extension, task);
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
    private string UpdatePathBasedOnExtension(string folder, string path, string extension)
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
    private async Task<string> GenerateCustomFileName(string filename,
                                                             string? filenameFormat,
                                                             object? postInfo,
                                                             object? postMedia,
                                                             object? author,
                                                             string username,
                                                             Dictionary<string, int> users,
                                                             IFileNameHelper fileNameHelper,
                                                             CustomFileNameOption option)
    {
        if (string.IsNullOrEmpty(filenameFormat) || postInfo == null || postMedia == null || author == null)
        {
            return option switch
            {
                CustomFileNameOption.ReturnOriginal => filename,
                CustomFileNameOption.ReturnEmpty => string.Empty,
                _ => filename,
            };
        }

        List<string> properties = new();
        string pattern = @"\{(.*?)\}";
        MatchCollection matches = Regex.Matches(filenameFormat, pattern);
        properties.AddRange(matches.Select(match => match.Groups[1].Value));

        Dictionary<string, string> values = await fileNameHelper.GetFilename(postInfo, postMedia, author, properties, username, users);
        return await fileNameHelper.BuildFilename(filenameFormat, values);
    }


    private async Task<long> GetFileSizeAsync(string url, Auth auth)
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

    public static async Task<DateTime> GetDRMVideoLastModified(string url, Auth auth)
    {
        Uri uri = new(url);

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
            return response.Content.Headers.LastModified.Value.DateTime;
        }
        return DateTime.Now;
    }
    public static async Task<DateTime> GetMediaLastModified(string url)
    {
        using HttpClient client = new();

        using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (response.IsSuccessStatusCode)
        {
            return response.Content.Headers.LastModified.Value.DateTime;
        }
        return DateTime.Now;
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
    public async Task<bool> ProcessMediaDownload(string folder,
                                                        long media_id,
                                                        string url,
                                                        string path,
                                                        string serverFilename,
                                                        string resolvedFilename,
                                                        string extension,
                                                        ProgressTask task)
    {

        try
        {
            if (!await m_DBHelper.CheckDownloaded(folder, media_id))
            {
                return await HandleNewMedia(folder: folder,
                                            media_id: media_id,
                                            url: url,
                                            path: path,
                                            serverFilename: serverFilename,
                                            resolvedFilename: resolvedFilename,
                                            extension: extension,
                                            task: task);
            }
            else
            {
                bool status = await HandlePreviouslyDownloadedMediaAsync(folder, media_id, task);
                if (downloadConfig.RenameExistingFilesWhenCustomFormatIsSelected && (serverFilename != resolvedFilename))
                {
                    await HandleRenamingOfExistingFilesAsync(folder, media_id, path, serverFilename, resolvedFilename, extension);
                }
                return status;
            }
        }
        catch (Exception ex)
        {
            // Handle exception (e.g., log it)
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }


    private async Task<bool> HandleRenamingOfExistingFilesAsync(string folder,
                                                                       long media_id,
                                                                       string path,
                                                                       string serverFilename,
                                                                       string resolvedFilename,
                                                                       string extension)
    {
        string fullPathWithTheServerFileName = $"{folder}{path}/{serverFilename}{extension}";
        string fullPathWithTheNewFileName = $"{folder}{path}/{resolvedFilename}{extension}";
        if (!File.Exists(fullPathWithTheServerFileName))
        {
            return false;
        }

        try
        {
            File.Move(fullPathWithTheServerFileName, fullPathWithTheNewFileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }

        long size = await m_DBHelper.GetStoredFileSize(folder, media_id);
        var lastModified = File.GetLastWriteTime(fullPathWithTheNewFileName);
        await m_DBHelper.UpdateMedia(folder, media_id, folder + path, resolvedFilename + extension, size, true, lastModified);
        return true;
    }


    /// <summary>
    /// Handles new media by downloading and updating the database.
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="media_id"></param>
    /// <param name="url"></param>
    /// <param name="path"></param>
    /// <param name="resolvedFilename"></param>
    /// <param name="extension"></param>
    /// <param name="task"></param>
    /// <param name="dBHelper"></param>
    /// <returns>A Task resulting in a boolean indicating whether the media is newly downloaded or not.</returns>
    private async Task<bool> HandleNewMedia(string folder,
                                                   long media_id,
                                                   string url,
                                                   string path,
                                                   string serverFilename,
                                                   string resolvedFilename,
                                                   string extension,
                                                   ProgressTask task)
    {
        long fileSizeInBytes;
        DateTime lastModified;
        bool status;

        string fullPathWithTheServerFileName = $"{folder}{path}/{serverFilename}{extension}";
        string fullPathWithTheNewFileName = $"{folder}{path}/{resolvedFilename}{extension}";

        //there are a few possibilities here.
        //1.file has been downloaded in the past but it has the server filename
        //    in that case it should be set as existing and it should be renamed
        //2.file has been downloaded in the past but it has custom filename.
        //    it should be set as existing and nothing else.
        // of coures 1 and 2 depends in the fact that there may be a difference in the resolved file name
        // (ie user has selected a custom format. If he doesn't then the resolved name will be the same as the server filename
        //3.file doesn't exist and it should be downloaded.

        // Handle the case where the file has been downloaded in the past with the server filename
        //but it has downloaded outsite of this application so it doesn't exist in the database
        if (File.Exists(fullPathWithTheServerFileName))
        {
            string finalPath;
            if (fullPathWithTheServerFileName != fullPathWithTheNewFileName)
            {
                finalPath = fullPathWithTheNewFileName;
                //rename.
                try
                {
                    File.Move(fullPathWithTheServerFileName, fullPathWithTheNewFileName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
            else
            {
                finalPath = fullPathWithTheServerFileName;
            }

            fileSizeInBytes = GetLocalFileSize(finalPath);
            lastModified = File.GetLastWriteTime(finalPath);
            if (downloadConfig.ShowScrapeSize)
            {
                task.Increment(fileSizeInBytes);
            }
            else
            {
                task.Increment(1);
            }
            status = false;
        }
        // Handle the case where the file has been downloaded in the past with a custom filename.
        //but it has downloaded outsite of this application so it doesn't exist in the database
        // this is a bit improbable but we should check for that.
        else if (File.Exists(fullPathWithTheNewFileName))
        {
            fileSizeInBytes = GetLocalFileSize(fullPathWithTheNewFileName);
            lastModified = File.GetLastWriteTime(fullPathWithTheNewFileName);
            if (downloadConfig.ShowScrapeSize)
            {
                task.Increment(fileSizeInBytes);
            }
            else
            {
                task.Increment(1);
            }
            status = false;
        }
        else //file doesn't exist and we should download it.
        {
            lastModified = await DownloadFile(url, fullPathWithTheNewFileName, task);
            fileSizeInBytes = GetLocalFileSize(fullPathWithTheNewFileName);
            status = true;
        }

        //finaly check which filename we should use. Custom or the server one.
        //if a custom is used, then the servefilename will be different from the resolved filename.
        string finalName = serverFilename == resolvedFilename ? serverFilename : resolvedFilename;
        await m_DBHelper.UpdateMedia(folder, media_id, folder + path, finalName + extension, fileSizeInBytes, true, lastModified);
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
    private async Task<bool> HandlePreviouslyDownloadedMediaAsync(string folder, long media_id, ProgressTask task)
    {
        if (downloadConfig.ShowScrapeSize)
        {
            long size = await m_DBHelper.GetStoredFileSize(folder, media_id);
            task.Increment(size);
        }
        else
        {
            task.Increment(1);
        }
        return false;
    }


    /// <summary>
    /// Gets the file size of the media.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>The file size in bytes.</returns>
    private long GetLocalFileSize(string filePath)
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

    private async Task<DateTime> DownloadFile(string url, string destinationPath, ProgressTask task)
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

        // Wrap the body stream with the ThrottledStream to limit read rate.
        using (ThrottledStream throttledStream = new(body, downloadConfig.DownloadLimitInMbPerSec * 1_000_000, downloadConfig.LimitDownloadRate))
        {
            using FileStream fileStream = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true);
            var buffer = new byte[16384];
            int read;
            while ((read = await throttledStream.ReadAsync(buffer, CancellationToken.None)) > 0)
            {
                if (downloadConfig.ShowScrapeSize)
                {
                    task.Increment(read);
                }
                await fileStream.WriteAsync(buffer.AsMemory(0, read), CancellationToken.None);
            }
        }

        File.SetLastWriteTime(destinationPath, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
        if (!downloadConfig.ShowScrapeSize)
        {
            task.Increment(1);
        }
        return response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now;
    }

    public async Task<long> CalculateTotalFileSize(List<string> urls)
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

    private async Task<bool> DownloadDrmMedia(string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string customFileName, string filename, string path)
    {
        int pos1 = decryptionKey.IndexOf(':');
        string decKey = "";
        if (pos1 >= 0)
        {
            decKey = decryptionKey.Substring(pos1 + 1);
        }

        string tempFilename = $"{folder}{path}/{filename}_source.mp4";

        ProcessStartInfo ffmpegStartInfo = new()
        {
            FileName = downloadConfig.FFmpegPath,
            Arguments = $"-cenc_decryption_key {decKey} -headers \"Cookie:CloudFront-Policy={policy}; CloudFront-Signature={signature}; CloudFront-Key-Pair-Id={kvp}; {sess}\r\nOrigin: https://onlyfans.com\r\nReferer: https://onlyfans.com\r\nUser-Agent: {user_agent}\r\n\r\n\" -i \"{url}\" -codec copy \"{tempFilename}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = true
        };

        Process ffmpegProcess = new()
        {
            StartInfo = ffmpegStartInfo
        };
        ffmpegProcess.Start();
        var ffmpegErrors = ffmpegProcess.StandardError.ReadToEnd();
        ffmpegProcess.WaitForExit();

        if (ffmpegProcess.ExitCode != 0)
        {
            Console.WriteLine("\nFFmpeg failed to download {0}", url);
            Log.Error(ffmpegErrors);
        }

        if (File.Exists(tempFilename))
        {
            File.SetLastWriteTime(tempFilename, lastModified);
        }
        if (!string.IsNullOrEmpty(customFileName))
        {
            File.Move(tempFilename, $"{folder + path + "/" + customFileName + ".mp4"}");
        }
        //Cleanup Files
        long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : tempFilename).Length;
        if (downloadConfig.ShowScrapeSize)
        {
            task.Increment(fileSizeInBytes);
        }
        else
        {
            task.Increment(1);
        }
        await m_DBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);

        return true;
    }
    #endregion

    #region normal posts
    public async Task<bool> DownloadPostMedia(string url, string folder, long media_id, ProgressTask task, string? filenameFormat, Post.List? postInfo, Post.Medium? postMedia, Post.Author? author, Dictionary<string, int> users)
    {
        string path;
        if (downloadConfig.FolderPerPost && postInfo != null && postInfo?.id is not null && postInfo?.postedAt is not null)
        {
            path = $"/Posts/Free/{postInfo.id} {postInfo.postedAt:yyyy-MM-dd HH-mm-ss}";
        }
        else
        {
            path = "/Posts/Free";
        }

        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, postInfo, postMedia, author, folder.Split("/")[^1], users, _FileNameHelper, CustomFileNameOption.ReturnOriginal);

        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, filename, resolvedFilename);
    }
    public async Task<bool> DownloadPostMedia(string url, string folder, long media_id, ProgressTask task, string? filenameFormat, SinglePost? postInfo, SinglePost.Medium? postMedia, SinglePost.Author? author, Dictionary<string, int> users)
    {
        string path;
        if (downloadConfig.FolderPerPost && postInfo != null && postInfo?.id is not null && postInfo?.postedAt is not null)
        {
            path = $"/Posts/Free/{postInfo.id} {postInfo.postedAt:yyyy-MM-dd HH-mm-ss}";
        }
        else
        {
            path = "/Posts/Free";
        }

        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, postInfo, postMedia, author, folder.Split("/")[^1], users, _FileNameHelper, CustomFileNameOption.ReturnOriginal);

        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, filename, resolvedFilename);
    }
    public async Task<bool> DownloadStreamMedia(string url, string folder, long media_id, ProgressTask task, string? filenameFormat, Streams.List? streamInfo, Streams.Medium? streamMedia, Streams.Author? author, Dictionary<string, int> users)
    {
        string path;
        if (downloadConfig.FolderPerPost && streamInfo != null && streamInfo?.id is not null && streamInfo?.postedAt is not null)
        {
            path = $"/Posts/Free/{streamInfo.id} {streamInfo.postedAt:yyyy-MM-dd HH-mm-ss}";
        }
        else
        {
            path = "/Posts/Free";
        }

        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, streamInfo, streamMedia, author, folder.Split("/")[^1], users, _FileNameHelper, CustomFileNameOption.ReturnOriginal);

        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, filename, resolvedFilename);
    }


    public async Task<bool> DownloadMessageMedia(string url, string folder, long media_id, ProgressTask task, string? filenameFormat, Messages.List? messageInfo, Messages.Medium? messageMedia, Messages.FromUser? fromUser, Dictionary<string, int> users)
    {
        string path;
        if (downloadConfig.FolderPerMessage && messageInfo != null && messageInfo?.id is not null && messageInfo?.createdAt is not null)
        {
            path = $"/Messages/Free/{messageInfo.id} {messageInfo.createdAt.Value:yyyy-MM-dd HH-mm-ss}";
        }
        else
        {
            path = "/Messages/Free";
        }
        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, messageInfo, messageMedia, fromUser, folder.Split("/")[^1], users, _FileNameHelper, CustomFileNameOption.ReturnOriginal);
        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, filename, resolvedFilename);
    }


    public async Task<bool> DownloadArchivedMedia(string url, string folder, long media_id, ProgressTask task, string? filenameFormat, Archived.List? messageInfo, Archived.Medium? messageMedia, Archived.Author? author, Dictionary<string, int> users)
    {
        string path = "/Archived/Posts/Free";
        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, messageInfo, messageMedia, author, folder.Split("/")[^1], users, _FileNameHelper, CustomFileNameOption.ReturnOriginal);
        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, filename, resolvedFilename);
    }



    public async Task<bool> DownloadStoryMedia(string url, string folder, long media_id, ProgressTask task)
    {
        string path = "/Stories/Free";
        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, filename, filename);
    }

    public async Task<bool> DownloadPurchasedMedia(string url, string folder, long media_id, ProgressTask task, string? filenameFormat, Purchased.List? messageInfo, Purchased.Medium? messageMedia, Purchased.FromUser? fromUser, Dictionary<string, int> users)
    {
        string path;
        if (downloadConfig.FolderPerPaidMessage && messageInfo != null && messageInfo?.id is not null && messageInfo?.createdAt is not null)
        {
            path = $"/Messages/Paid/{messageInfo.id} {messageInfo.createdAt.Value:yyyy-MM-dd HH-mm-ss}";
        }
        else
        {
            path = "/Messages/Paid";
        }
        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, messageInfo, messageMedia, fromUser, folder.Split("/")[^1], users, _FileNameHelper, CustomFileNameOption.ReturnOriginal);
        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, filename, resolvedFilename);
    }

    public async Task<bool> DownloadPurchasedPostMedia(string url,
                                                       string folder,
                                                       long media_id,
                                                       ProgressTask task,
                                                       string? filenameFormat,
                                                       Purchased.List? messageInfo,
                                                       Purchased.Medium? messageMedia,
                                                       Purchased.FromUser? fromUser,
                                                       Dictionary<string, int> users)
    {
        string path;
        if (downloadConfig.FolderPerPaidPost && messageInfo != null && messageInfo?.id is not null && messageInfo?.postedAt is not null)
        {
            path = $"/Posts/Paid/{messageInfo.id} {messageInfo.postedAt.Value:yyyy-MM-dd HH-mm-ss}";
        }
        else
        {
            path = "/Posts/Paid";
        }
        Uri uri = new(url);
        string filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
        string resolvedFilename = await GenerateCustomFileName(filename, filenameFormat, messageInfo, messageMedia, fromUser, folder.Split("/")[^1], users, _FileNameHelper, CustomFileNameOption.ReturnOriginal);
        return await CreateDirectoriesAndDownloadMedia(path, url, folder, media_id, task, filename, resolvedFilename);
    }

    #endregion
    public async Task DownloadAvatarHeader(string? avatarUrl, string? headerUrl, string folder, string username)
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

                List<string> avatarMD5Hashes = WidevineClient.Utils.CalculateFolderMD5(folder + avatarpath);

                Uri uri = new(avatarUrl);
                string destinationPath = $"{folder}{avatarpath}/";

                var client = new HttpClient();

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = uri

                };
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var memoryStream = new MemoryStream();
                await response.Content.CopyToAsync(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                MD5 md5 = MD5.Create();
                byte[] hash = md5.ComputeHash(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                if (!avatarMD5Hashes.Contains(BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()))
                {
                    destinationPath = destinationPath + string.Format("{0} {1}.jpg", username, response.Content.Headers.LastModified.HasValue ? response.Content.Headers.LastModified.Value.LocalDateTime.ToString("dd-MM-yyyy") : DateTime.Now.ToString("dd-MM-yyyy"));

                    using (FileStream fileStream = File.Create(destinationPath))
                    {
                        await memoryStream.CopyToAsync(fileStream);
                    }
                    File.SetLastWriteTime(destinationPath, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
                }
            }

            if (!string.IsNullOrEmpty(headerUrl))
            {
                string headerpath = $"{path}/Headers";
                if (!Directory.Exists(folder + headerpath)) // check if the folder already exists
                {
                    Directory.CreateDirectory(folder + headerpath); // create the new folder
                }

                List<string> headerMD5Hashes = WidevineClient.Utils.CalculateFolderMD5(folder + headerpath);

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

                using var memoryStream = new MemoryStream();
                await response.Content.CopyToAsync(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                MD5 md5 = MD5.Create();
                byte[] hash = md5.ComputeHash(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                if (!headerMD5Hashes.Contains(BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()))
                {
                    destinationPath = destinationPath + string.Format("{0} {1}.jpg", username, response.Content.Headers.LastModified.HasValue ? response.Content.Headers.LastModified.Value.LocalDateTime.ToString("dd-MM-yyyy") : DateTime.Now.ToString("dd-MM-yyyy"));

                    using (FileStream fileStream = File.Create(destinationPath))
                    {
                        await memoryStream.CopyToAsync(fileStream);
                    }
                    File.SetLastWriteTime(destinationPath, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
                }

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
    public async Task<bool> DownloadMessageDRMVideo(string policy, string signature, string kvp, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string? filenameFormat, Messages.List? messageInfo, Messages.Medium? messageMedia, Messages.FromUser? fromUser, Dictionary<string, int> users)
    {
        try
        {
            string customFileName = string.Empty;
            string path;
            Uri uri = new(url);
            string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
            if (downloadConfig.FolderPerMessage && messageInfo != null && messageInfo?.id is not null && messageInfo?.createdAt is not null)
            {
                path = $"/Messages/Free/{messageInfo.id} {messageInfo.createdAt.Value:yyyy-MM-dd HH-mm-ss}/Videos";
            }
            else
            {
                path = "/Messages/Free/Videos";
            }
            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }


            if (!string.IsNullOrEmpty(filenameFormat) && messageInfo != null && messageMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(messageInfo, messageMedia, fromUser, properties, folder.Split("/")[^1],users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await m_DBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    if (downloadConfig.ShowScrapeSize)
                    {
                        task.Increment(fileSizeInBytes);
                    }
                    else
                    {
                        task.Increment(1);
                    }
                    await m_DBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                if (downloadConfig.ShowScrapeSize)
                {
                    long size = await m_DBHelper.GetStoredFileSize(folder, media_id);
                    task.Increment(size);
                }
                else
                {
                    task.Increment(1);
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }


    public async Task<bool> DownloadPurchasedMessageDRMVideo(string policy, string signature, string kvp, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string? filenameFormat, Purchased.List? messageInfo, Purchased.Medium? messageMedia, Purchased.FromUser? fromUser, Dictionary<string, int> users)
    {
        try
        {
            string customFileName = string.Empty;
            string path;
            Uri uri = new(url);
            string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
            if (downloadConfig.FolderPerPaidMessage && messageInfo != null && messageInfo?.id is not null && messageInfo?.createdAt is not null)
            {
                path = $"/Messages/Paid/{messageInfo.id} {messageInfo.createdAt.Value:yyyy-MM-dd HH-mm-ss}/Videos";
            }
            else
            {
                path = "/Messages/Paid/Videos";
            }
            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }

            if (!string.IsNullOrEmpty(filenameFormat) && messageInfo != null && messageMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(messageInfo, messageMedia, fromUser, properties, folder.Split("/")[^1], users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await m_DBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    if (downloadConfig.ShowScrapeSize)
                    {
                        task.Increment(fileSizeInBytes);
                    }
                    else
                    {
                        task.Increment(1);
                    }
                    await m_DBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                if (downloadConfig.ShowScrapeSize)
                {
                    long size = await m_DBHelper.GetStoredFileSize(folder, media_id);
                    task.Increment(size);
                }
                else
                {
                    task.Increment(1);
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }


    public async Task<bool> DownloadPostDRMVideo(string policy, string signature, string kvp, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string? filenameFormat, Post.List? postInfo, Post.Medium? postMedia, Post.Author? author, Dictionary<string, int> users)
    {
        try
        {
            string customFileName = string.Empty;
            string path;
            Uri uri = new(url);
            string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
            if (downloadConfig.FolderPerPost && postInfo != null && postInfo?.id is not null && postInfo?.postedAt is not null)
            {
                path = $"/Posts/Free/{postInfo.id} {postInfo.postedAt:yyyy-MM-dd HH-mm-ss}/Videos";
            }
            else
            {
                path = "/Posts/Free/Videos";
            }
            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }

            if (!string.IsNullOrEmpty(filenameFormat) && postInfo != null && postMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(postInfo, postMedia, author, properties, folder.Split("/")[^1], users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await m_DBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    if (downloadConfig.ShowScrapeSize)
                    {
                        task.Increment(fileSizeInBytes);
                    }
                    else
                    {
                        task.Increment(1);
                    }
                    await m_DBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                if (downloadConfig.ShowScrapeSize)
                {
                    long size = await m_DBHelper.GetStoredFileSize(folder, media_id);
                    task.Increment(size);
                }
                else
                {
                    task.Increment(1);
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }
    public async Task<bool> DownloadPostDRMVideo(string policy, string signature, string kvp, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string filenameFormat, SinglePost postInfo, SinglePost.Medium postMedia, SinglePost.Author author, Dictionary<string, int> users)
    {
        try
        {
            string customFileName = string.Empty;
            string path;
            Uri uri = new(url);
            string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
            if (downloadConfig.FolderPerPost && postInfo != null && postInfo?.id is not null && postInfo?.postedAt is not null)
            {
                path = $"/Posts/Free/{postInfo.id} {postInfo.postedAt:yyyy-MM-dd HH-mm-ss}/Videos";
            }
            else
            {
                path = "/Posts/Free/Videos";
            }
            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }

            if (!string.IsNullOrEmpty(filenameFormat) && postInfo != null && postMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(postInfo, postMedia, author, properties, folder.Split("/")[^1], users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await m_DBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    if (downloadConfig.ShowScrapeSize)
                    {
                        task.Increment(fileSizeInBytes);
                    }
                    else
                    {
                        task.Increment(1);
                    }
                    await m_DBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                if (downloadConfig.ShowScrapeSize)
                {
                    long size = await m_DBHelper.GetStoredFileSize(folder, media_id);
                    task.Increment(size);
                }
                else
                {
                    task.Increment(1);
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }
    public async Task<bool> DownloadStreamsDRMVideo(string policy, string signature, string kvp, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string? filenameFormat, Streams.List? streamInfo, Streams.Medium? streamMedia, Streams.Author? author, Dictionary<string, int> users)
    {
        try
        {
            string customFileName = string.Empty;
            string path;
            Uri uri = new(url);
            string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
            if (downloadConfig.FolderPerPost && streamInfo != null && streamInfo?.id is not null && streamInfo?.postedAt is not null)
            {
                path = $"/Posts/Free/{streamInfo.id} {streamInfo.postedAt:yyyy-MM-dd HH-mm-ss}/Videos";
            }
            else
            {
                path = "/Posts/Free/Videos";
            }
            if (!Directory.Exists(folder + path))
            {
                Directory.CreateDirectory(folder + path);
            }

            if (!string.IsNullOrEmpty(filenameFormat) && streamInfo != null && streamMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(streamInfo, streamMedia, author, properties, folder.Split("/")[^1], users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await m_DBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    if (downloadConfig.ShowScrapeSize)
                    {
                        task.Increment(fileSizeInBytes);
                    }
                    else
                    {
                        task.Increment(1);
                    }
                    await m_DBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                if (downloadConfig.ShowScrapeSize)
                {
                    long size = await m_DBHelper.GetStoredFileSize(folder, media_id);
                    task.Increment(size);
                }
                else
                {
                    task.Increment(1);
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }

    public async Task<bool> DownloadPurchasedPostDRMVideo(string policy, string signature, string kvp, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string? filenameFormat, Purchased.List? postInfo, Purchased.Medium? postMedia, Purchased.FromUser? fromUser, Dictionary<string, int> users)
    {
        try
        {
            string customFileName = string.Empty;
            string path;
            Uri uri = new(url);
            string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
            if (downloadConfig.FolderPerPaidPost && postInfo != null && postInfo?.id is not null && postInfo?.postedAt is not null)
            {
                path = $"/Posts/Paid/{postInfo.id} {postInfo.postedAt.Value:yyyy-MM-dd HH-mm-ss}/Videos";
            }
            else
            {
                path = "/Posts/Paid/Videos";
            }
            if (!Directory.Exists(folder + path)) // check if the folder already exists
            {
                Directory.CreateDirectory(folder + path); // create the new folder
            }


            if (!string.IsNullOrEmpty(filenameFormat) && postInfo != null && postMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(postInfo, postMedia, fromUser, properties, folder.Split("/")[^1], users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await m_DBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    if (downloadConfig.ShowScrapeSize)
                    {
                        task.Increment(fileSizeInBytes);
                    }
                    else
                    {
                        task.Increment(1);
                    }
                    await m_DBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                if (downloadConfig.ShowScrapeSize)
                {
                    long size = await m_DBHelper.GetStoredFileSize(folder, media_id);
                    task.Increment(size);
                }
                else
                {
                    task.Increment(1);
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }


    public async Task<bool> DownloadArchivedPostDRMVideo(string policy, string signature, string kvp, string url, string decryptionKey, string folder, DateTime lastModified, long media_id, ProgressTask task, string? filenameFormat, Archived.List? postInfo, Archived.Medium? postMedia, Archived.Author? author, Dictionary<string, int> users)
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

            if (!string.IsNullOrEmpty(filenameFormat) && postInfo != null && postMedia != null)
            {
                List<string> properties = new();
                string pattern = @"\{(.*?)\}";
                MatchCollection matches = Regex.Matches(filenameFormat, pattern);
                foreach (Match match in matches)
                {
                    properties.Add(match.Groups[1].Value);
                }
                Dictionary<string, string> values = await _FileNameHelper.GetFilename(postInfo, postMedia, author, properties, folder.Split("/")[^1], users);
                customFileName = await _FileNameHelper.BuildFilename(filenameFormat, values);
            }

            if (!await m_DBHelper.CheckDownloaded(folder, media_id))
            {
                if (!string.IsNullOrEmpty(customFileName) ? !File.Exists(folder + path + "/" + customFileName + ".mp4") : !File.Exists(folder + path + "/" + filename + "_source.mp4"))
                {
                    return await DownloadDrmMedia(auth.USER_AGENT, policy, signature, kvp, auth.COOKIE, url, decryptionKey, folder, lastModified, media_id, task, customFileName, filename, path);
                }
                else
                {
                    long fileSizeInBytes = new FileInfo(!string.IsNullOrEmpty(customFileName) ? folder + path + "/" + customFileName + ".mp4" : folder + path + "/" + filename + "_source.mp4").Length;
                    if (downloadConfig.ShowScrapeSize)
                    {
                        task.Increment(fileSizeInBytes);
                    }
                    else
                    {
                        task.Increment(1);
                    }
                    await m_DBHelper.UpdateMedia(folder, media_id, folder + path, !string.IsNullOrEmpty(customFileName) ? customFileName + "mp4" : filename + "_source.mp4", fileSizeInBytes, true, lastModified);
                }
            }
            else
            {
                if (downloadConfig.ShowScrapeSize)
                {
                    long size = await m_DBHelper.GetStoredFileSize(folder, media_id);
                    task.Increment(size);
                }
                else
                {
                    task.Increment(1);
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("\nInner Exception:");
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
            }
        }
        return false;
    }
    #endregion
}
