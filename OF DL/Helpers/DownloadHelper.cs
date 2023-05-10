using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Helpers
{
	public class DownloadHelper
	{
		public async Task<bool> DownloadPostMedia(string url, string folder)
		{
			try
			{
				string path = "/Posts/Free";
				if (!Directory.Exists(folder + path)) // check if the folder already exists
				{
					Directory.CreateDirectory(folder + path); // create the new folder
				}
				string extension = Path.GetExtension(url.Split("?")[0]);
				switch (extension.ToLower())
				{
					case ".jpg":
					case ".jpeg":
					case ".png":
						path += "/Images";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp4":
					case ".avi":
					case ".wmv":
					case ".gif":
					case ".mov":
						path += "/Videos";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp3":
					case ".wav":
					case ".ogg":
						path += "/Audios";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
				}

				Uri uri = new Uri(url);
				string filename = System.IO.Path.GetFileName(uri.LocalPath);

				if (!File.Exists(folder + path + "/" + filename))
				{
					var client = new HttpClient();
					client.Timeout = TimeSpan.FromSeconds(5);
					var request = new HttpRequestMessage
					{
						Method = HttpMethod.Get,
						RequestUri = new Uri(url),

					};
					using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
					{
						response.EnsureSuccessStatusCode();
						var body = await response.Content.ReadAsStreamAsync();
						using (FileStream fileStream = File.Create(folder + path + "/" + filename))
						{
							await body.CopyToAsync(fileStream);
						}
						File.SetLastWriteTime(folder + path + "/" + filename, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
					}
					return true;
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

		public async Task<bool> DownloadMessageMedia(string url, string folder)
		{
			try
			{
				string path = "/Messages/Free";
				if (!Directory.Exists(folder + path)) // check if the folder already exists
				{
					Directory.CreateDirectory(folder + path); // create the new folder
				}
				string extension = Path.GetExtension(url.Split("?")[0]);
				switch (extension.ToLower())
				{
					case ".jpg":
					case ".jpeg":
					case ".png":
						path += "/Images";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp4":
					case ".avi":
					case ".wmv":
					case ".gif":
					case ".mov":
						path += "/Videos";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp3":
					case ".wav":
					case ".ogg":
						path += "/Audios";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
				}

				Uri uri = new Uri(url);
				string filename = System.IO.Path.GetFileName(uri.LocalPath);

				if (!File.Exists(folder + path + "/" + filename))
				{
					var client = new HttpClient();
					client.Timeout = TimeSpan.FromSeconds(5);
					var request = new HttpRequestMessage
					{
						Method = HttpMethod.Get,
						RequestUri = new Uri(url),

					};
					using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
					{
						response.EnsureSuccessStatusCode();
						var body = await response.Content.ReadAsStreamAsync();
						using (FileStream fileStream = File.Create(folder + path + "/" + filename))
						{
							await body.CopyToAsync(fileStream);
						}
						File.SetLastWriteTime(folder + path + "/" + filename, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
					}
					return true;
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

		public async Task<bool> DownloadArchivedMedia(string url, string folder)
		{
			try
			{
				string path = "/Archived/Posts/Free";
				if (!Directory.Exists(folder + path)) // check if the folder already exists
				{
					Directory.CreateDirectory(folder + path); // create the new folder
				}
				string extension = Path.GetExtension(url.Split("?")[0]);
				switch (extension.ToLower())
				{
					case ".jpg":
					case ".jpeg":
					case ".png":
						path += "/Images";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp4":
					case ".avi":
					case ".wmv":
					case ".gif":
					case ".mov":
						path += "/Videos";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp3":
					case ".wav":
					case ".ogg":
						path += "/Audios";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
				}

				Uri uri = new Uri(url);
				string filename = System.IO.Path.GetFileName(uri.LocalPath);

				if (!File.Exists(folder + path + "/" + filename))
				{
					var client = new HttpClient();
					client.Timeout = TimeSpan.FromSeconds(5);
					var request = new HttpRequestMessage
					{
						Method = HttpMethod.Get,
						RequestUri = new Uri(url),

					};
					using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
					{
						response.EnsureSuccessStatusCode();
						var body = await response.Content.ReadAsStreamAsync();
						using (FileStream fileStream = File.Create(folder + path + "/" + filename))
						{
							await body.CopyToAsync(fileStream);
						}
						File.SetLastWriteTime(folder + path + "/" + filename, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
					}
					return true;
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

		public async Task<bool> DownloadStoryMedia(string url, string folder)
		{
			try
			{
				string path = "/Stories/Free";
				if (!Directory.Exists(folder + path)) // check if the folder already exists
				{
					Directory.CreateDirectory(folder + path); // create the new folder
				}
				string extension = Path.GetExtension(url.Split("?")[0]);
				switch (extension.ToLower())
				{
					case ".jpg":
					case ".jpeg":
					case ".png":
						path += "/Images";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp4":
					case ".avi":
					case ".wmv":
					case ".gif":
					case ".mov":
						path += "/Videos";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp3":
					case ".wav":
					case ".ogg":
						path += "/Audios";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
				}

				Uri uri = new Uri(url);
				string filename = System.IO.Path.GetFileName(uri.LocalPath);

				if (!File.Exists(folder + path + "/" + filename))
				{
					var client = new HttpClient();
					client.Timeout = TimeSpan.FromSeconds(5);
					var request = new HttpRequestMessage
					{
						Method = HttpMethod.Get,
						RequestUri = new Uri(url),

					};
					using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
					{
						response.EnsureSuccessStatusCode();
						var body = await response.Content.ReadAsStreamAsync();
						using (FileStream fileStream = File.Create(folder + path + "/" + filename))
						{
							await body.CopyToAsync(fileStream);
						}
						File.SetLastWriteTime(folder + path + "/" + filename, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
					}
					return true;
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

		public async Task<bool> DownloadPurchasedMedia(string url, string folder)
		{
			try
			{
				string path = "/Messages/Paid";
				if (!Directory.Exists(folder + path)) // check if the folder already exists
				{
					Directory.CreateDirectory(folder + path); // create the new folder
				}
				string extension = Path.GetExtension(url.Split("?")[0]);
				switch (extension.ToLower())
				{
					case ".jpg":
					case ".jpeg":
					case ".png":
						path += "/Images";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp4":
					case ".avi":
					case ".wmv":
					case ".gif":
					case ".mov":
						path += "/Videos";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp3":
					case ".wav":
					case ".ogg":
						path += "/Audios";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
				}

				Uri uri = new Uri(url);
				string filename = System.IO.Path.GetFileName(uri.LocalPath);

				if (!File.Exists(folder + path + "/" + filename))
				{
					var client = new HttpClient();
					client.Timeout = TimeSpan.FromSeconds(5);
					var request = new HttpRequestMessage
					{
						Method = HttpMethod.Get,
						RequestUri = new Uri(url),

					};
					using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
					{
						response.EnsureSuccessStatusCode();
						var body = await response.Content.ReadAsStreamAsync();
						using (FileStream fileStream = File.Create(folder + path + "/" + filename))
						{
							await body.CopyToAsync(fileStream);
						}
						File.SetLastWriteTime(folder + path + "/" + filename, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
					}
					return true;
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

		public async Task<bool> DownloadPurchasedPostMedia(string url, string folder)
		{
			try
			{
				string path = "/Posts/Paid";
				if (!Directory.Exists(folder + path)) // check if the folder already exists
				{
					Directory.CreateDirectory(folder + path); // create the new folder
				}
				string extension = Path.GetExtension(url.Split("?")[0]);
				switch (extension.ToLower())
				{
					case ".jpg":
					case ".jpeg":
					case ".png":
						path += "/Images";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp4":
					case ".avi":
					case ".wmv":
					case ".gif":
					case ".mov":
						path += "/Videos";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
					case ".mp3":
					case ".wav":
					case ".ogg":
						path += "/Audios";
						if (!Directory.Exists(folder + path)) // check if the folder already exists
						{
							Directory.CreateDirectory(folder + path); // create the new folder
						}
						break;
				}

				Uri uri = new Uri(url);
				string filename = System.IO.Path.GetFileName(uri.LocalPath);

				if (!File.Exists(folder + path + "/" + filename))
				{
					var client = new HttpClient();
					client.Timeout = TimeSpan.FromSeconds(5);
					var request = new HttpRequestMessage
					{
						Method = HttpMethod.Get,
						RequestUri = new Uri(url),

					};
					using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
					{
						response.EnsureSuccessStatusCode();
						var body = await response.Content.ReadAsStreamAsync();
						using (FileStream fileStream = File.Create(folder + path + "/" + filename))
						{
							await body.CopyToAsync(fileStream);
						}
						File.SetLastWriteTime(folder + path + "/" + filename, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
					}
					return true;
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

					Uri uri = new Uri(avatarUrl);
					string filename = System.IO.Path.GetFileName(uri.LocalPath);

					var client = new HttpClient();
					client.Timeout = TimeSpan.FromSeconds(5);
					var request = new HttpRequestMessage
					{
						Method = HttpMethod.Get,
						RequestUri = uri

					};
					using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
					{
						response.EnsureSuccessStatusCode();
						var body = await response.Content.ReadAsStreamAsync();
						using (FileStream fileStream = File.Create(folder + avatarpath + "/" + filename))
						{
							await body.CopyToAsync(fileStream);
						}
						File.SetLastWriteTime(folder + avatarpath + "/" + filename, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
					}
				}

				if (!string.IsNullOrEmpty(headerUrl))
				{
					string headerpath = $"{path}/Headers";
					if (!Directory.Exists(folder + headerpath)) // check if the folder already exists
					{
						Directory.CreateDirectory(folder + headerpath); // create the new folder
					}

					Uri uri = new Uri(headerUrl);
					string filename = System.IO.Path.GetFileName(uri.LocalPath);

					var client = new HttpClient();
					client.Timeout = TimeSpan.FromSeconds(5);
					var request = new HttpRequestMessage
					{
						Method = HttpMethod.Get,
						RequestUri = uri

					};
					using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
					{
						response.EnsureSuccessStatusCode();
						var body = await response.Content.ReadAsStreamAsync();
						using (FileStream fileStream = File.Create(folder + headerpath + "/" + filename))
						{
							await body.CopyToAsync(fileStream);
						}
						File.SetLastWriteTime(folder + headerpath + "/" + filename, response.Content.Headers.LastModified?.LocalDateTime ?? DateTime.Now);
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
		public async Task<bool> DownloadMessageDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified)
		{
			try
			{
				Uri uri = new Uri(url);
				string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
				string path = "/Messages/Free/Videos";
				if (!Directory.Exists(folder + path)) // check if the folder already exists
				{
					Directory.CreateDirectory(folder + path); // create the new folder
				}

				if (!File.Exists(folder + path + "/" + filename + "_source.mp4"))
				{
					//Use ytdl-p to download the MPD as a M4A and MP4 file
					ProcessStartInfo ytdlpstartInfo = new ProcessStartInfo();
					ytdlpstartInfo.FileName = ytdlppath;
					ytdlpstartInfo.Arguments = $"--allow-u --no-part --restrict-filenames -N 4 --user-agent \"{user_agent}\" --add-header \"Cookie:CloudFront-Policy={policy}; CloudFront-Signature={signature}; CloudFront-Key-Pair-Id={kvp}; {sess}\" --referer \"https://onlyfans.com/\" -o \"{folder + path + "/"}%(title)s.%(ext)s\" \"{url}\"";
					ytdlpstartInfo.CreateNoWindow = true;

					Process ytdlpprocess = new Process();
					ytdlpprocess.StartInfo = ytdlpstartInfo;
					ytdlpprocess.Start();
					ytdlpprocess.WaitForExit();

					//Use mp4decrypt to decrypt the MP4 and M4A files
					ProcessStartInfo mp4decryptStartInfoVideo = new ProcessStartInfo();
					mp4decryptStartInfoVideo.FileName = mp4decryptpath;
					mp4decryptStartInfoVideo.Arguments = $"--key {decryptionKey} {folder + path + "/" + filename + ".f1"}.mp4 {folder + path + "/" + filename}_vdec.mp4";
					mp4decryptStartInfoVideo.CreateNoWindow = true;

					Process mp4decryptVideoProcess = new Process();
					mp4decryptVideoProcess.StartInfo = mp4decryptStartInfoVideo;
					mp4decryptVideoProcess.Start();
					mp4decryptVideoProcess.WaitForExit();

					ProcessStartInfo mp4decryptStartInfoAudio = new ProcessStartInfo();
					mp4decryptStartInfoAudio.FileName = mp4decryptpath;
					mp4decryptStartInfoAudio.Arguments = $"--key {decryptionKey} {folder + path + "/" + filename + ".f4"}.m4a {folder + path + "/" + filename}_adec.mp4";
					mp4decryptStartInfoAudio.CreateNoWindow = true;

					Process mp4decryptAudioProcess = new Process();
					mp4decryptAudioProcess.StartInfo = mp4decryptStartInfoAudio;
					mp4decryptAudioProcess.Start();
					mp4decryptAudioProcess.WaitForExit();

					//Finally use FFMPEG to merge the 2 together
					ProcessStartInfo ffmpegStartInfo = new ProcessStartInfo();
					ffmpegStartInfo.FileName = ffmpegpath;
					ffmpegStartInfo.Arguments = $"-i {folder + path + "/" + filename}_vdec.mp4 -i {folder + path + "/" + filename}_adec.mp4 -c copy {folder + path + "/" + filename}_source.mp4";
					ffmpegStartInfo.CreateNoWindow = true;

					Process ffmpegProcess = new Process();
					ffmpegProcess.StartInfo = ffmpegStartInfo;
					ffmpegProcess.Start();
					ffmpegProcess.WaitForExit();

					//Cleanup Files
					File.SetLastWriteTime($"{folder + path + "/" + filename}_source.mp4", lastModified);
					File.Delete($"{folder + path + "/" + filename + ".f1"}.mp4");
					File.Delete($"{folder + path + "/" + filename + ".f4"}.m4a");
					File.Delete($"{folder + path + "/" + filename}_adec.mp4");
					File.Delete($"{folder + path + "/" + filename}_vdec.mp4");

					return true;
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

		public async Task<bool> DownloadPurchasedMessageDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified)
		{
			try
			{
				Uri uri = new Uri(url);
				string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
				string path = "/Messages/Paid/Videos";
				if (!Directory.Exists(folder + path)) // check if the folder already exists
				{
					Directory.CreateDirectory(folder + path); // create the new folder
				}

				if (!File.Exists(folder + path + "/" + filename + "_source.mp4"))
				{
					//Use ytdl-p to download the MPD as a M4A and MP4 file
					ProcessStartInfo ytdlpstartInfo = new ProcessStartInfo();
					ytdlpstartInfo.FileName = ytdlppath;
					ytdlpstartInfo.Arguments = $"--allow-u --no-part --restrict-filenames -N 4 --user-agent \"{user_agent}\" --add-header \"Cookie:CloudFront-Policy={policy}; CloudFront-Signature={signature}; CloudFront-Key-Pair-Id={kvp}; {sess}\" --referer \"https://onlyfans.com/\" -o \"{folder + path + "/"}%(title)s.%(ext)s\" \"{url}\"";
					ytdlpstartInfo.CreateNoWindow = true;

					Process ytdlpprocess = new Process();
					ytdlpprocess.StartInfo = ytdlpstartInfo;
					ytdlpprocess.Start();
					ytdlpprocess.WaitForExit();

					//Use mp4decrypt to decrypt the MP4 and M4A files
					ProcessStartInfo mp4decryptStartInfoVideo = new ProcessStartInfo();
					mp4decryptStartInfoVideo.FileName = mp4decryptpath;
					mp4decryptStartInfoVideo.Arguments = $"--key {decryptionKey} {folder + path + "/" + filename + ".f1"}.mp4 {folder + path + "/" + filename}_vdec.mp4";
					mp4decryptStartInfoVideo.CreateNoWindow = true;

					Process mp4decryptVideoProcess = new Process();
					mp4decryptVideoProcess.StartInfo = mp4decryptStartInfoVideo;
					mp4decryptVideoProcess.Start();
					mp4decryptVideoProcess.WaitForExit();

					ProcessStartInfo mp4decryptStartInfoAudio = new ProcessStartInfo();
					mp4decryptStartInfoAudio.FileName = mp4decryptpath;
					mp4decryptStartInfoAudio.Arguments = $"--key {decryptionKey} {folder + path + "/" + filename + ".f4"}.m4a {folder + path + "/" + filename}_adec.mp4";
					mp4decryptStartInfoAudio.CreateNoWindow = true;

					Process mp4decryptAudioProcess = new Process();
					mp4decryptAudioProcess.StartInfo = mp4decryptStartInfoAudio;
					mp4decryptAudioProcess.Start();
					mp4decryptAudioProcess.WaitForExit();

					//Finally use FFMPEG to merge the 2 together
					ProcessStartInfo ffmpegStartInfo = new ProcessStartInfo();
					ffmpegStartInfo.FileName = ffmpegpath;
					ffmpegStartInfo.Arguments = $"-i {folder + path + "/" + filename}_vdec.mp4 -i {folder + path + "/" + filename}_adec.mp4 -c copy {folder + path + "/" + filename}_source.mp4";
					ffmpegStartInfo.CreateNoWindow = true;

					Process ffmpegProcess = new Process();
					ffmpegProcess.StartInfo = ffmpegStartInfo;
					ffmpegProcess.Start();
					ffmpegProcess.WaitForExit();

					//Cleanup Files
					File.SetLastWriteTime($"{folder + path + "/" + filename}_source.mp4", lastModified);
					File.Delete($"{folder + path + "/" + filename + ".f1"}.mp4");
					File.Delete($"{folder + path + "/" + filename + ".f4"}.m4a");
					File.Delete($"{folder + path + "/" + filename}_adec.mp4");
					File.Delete($"{folder + path + "/" + filename}_vdec.mp4");

					return true;
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
		public async Task<bool> DownloadPostDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified)
		{
			try
			{
				Uri uri = new Uri(url);
				string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
				string path = "/Posts/Free/Videos";
				if (!Directory.Exists(folder + path)) // check if the folder already exists
				{
					Directory.CreateDirectory(folder + path); // create the new folder
				}

				if (!File.Exists(folder + path + "/" + filename + "_source.mp4"))
				{
					//Use ytdl-p to download the MPD as a M4A and MP4 file
					ProcessStartInfo ytdlpstartInfo = new ProcessStartInfo();
					ytdlpstartInfo.FileName = ytdlppath;
					ytdlpstartInfo.Arguments = $"--allow-u --no-part --restrict-filenames -N 4 --user-agent \"{user_agent}\" --add-header \"Cookie:CloudFront-Policy={policy}; CloudFront-Signature={signature}; CloudFront-Key-Pair-Id={kvp}; {sess}\" --referer \"https://onlyfans.com/\" -o \"{folder + path + "/"}%(title)s.%(ext)s\" \"{url}\"";
					ytdlpstartInfo.CreateNoWindow = true;

					Process ytdlpprocess = new Process();
					ytdlpprocess.StartInfo = ytdlpstartInfo;
					ytdlpprocess.Start();
					ytdlpprocess.WaitForExit();

					//Use mp4decrypt to decrypt the MP4 and M4A files
					ProcessStartInfo mp4decryptStartInfoVideo = new ProcessStartInfo();
					mp4decryptStartInfoVideo.FileName = mp4decryptpath;
					mp4decryptStartInfoVideo.Arguments = $"--key {decryptionKey} {folder + path + "/" + filename + ".f1"}.mp4 {folder + path + "/" + filename}_vdec.mp4";
					mp4decryptStartInfoVideo.CreateNoWindow = true;

					Process mp4decryptVideoProcess = new Process();
					mp4decryptVideoProcess.StartInfo = mp4decryptStartInfoVideo;
					mp4decryptVideoProcess.Start();
					mp4decryptVideoProcess.WaitForExit();

					ProcessStartInfo mp4decryptStartInfoAudio = new ProcessStartInfo();
					mp4decryptStartInfoAudio.FileName = mp4decryptpath;
					mp4decryptStartInfoAudio.Arguments = $"--key {decryptionKey} {folder + path + "/" + filename + ".f4"}.m4a {folder + path + "/" + filename}_adec.mp4";
					mp4decryptStartInfoAudio.CreateNoWindow = true;

					Process mp4decryptAudioProcess = new Process();
					mp4decryptAudioProcess.StartInfo = mp4decryptStartInfoAudio;
					mp4decryptAudioProcess.Start();
					mp4decryptAudioProcess.WaitForExit();

					//Finally use FFMPEG to merge the 2 together
					ProcessStartInfo ffmpegStartInfo = new ProcessStartInfo();
					ffmpegStartInfo.FileName = ffmpegpath;
					ffmpegStartInfo.Arguments = $"-i {folder + path + "/" + filename}_vdec.mp4 -i {folder + path + "/" + filename}_adec.mp4 -c copy {folder + path + "/" + filename}_source.mp4";
					ffmpegStartInfo.CreateNoWindow = true;

					Process ffmpegProcess = new Process();
					ffmpegProcess.StartInfo = ffmpegStartInfo;
					ffmpegProcess.Start();
					ffmpegProcess.WaitForExit();

					//Cleanup Files
					File.SetLastWriteTime($"{folder + path + "/" + filename}_source.mp4", lastModified);
					File.Delete($"{folder + path + "/" + filename + ".f1"}.mp4");
					File.Delete($"{folder + path + "/" + filename + ".f4"}.m4a");
					File.Delete($"{folder + path + "/" + filename}_adec.mp4");
					File.Delete($"{folder + path + "/" + filename}_vdec.mp4");

					return true;
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

		public async Task<bool> DownloadPurchasedPostDRMVideo(string ytdlppath, string mp4decryptpath, string ffmpegpath, string user_agent, string policy, string signature, string kvp, string sess, string url, string decryptionKey, string folder, DateTime lastModified)
		{
			try
			{
				Uri uri = new Uri(url);
				string filename = System.IO.Path.GetFileName(uri.LocalPath).Split(".")[0];
				string path = "/Posts/Paid/Videos";
				if (!Directory.Exists(folder + path)) // check if the folder already exists
				{
					Directory.CreateDirectory(folder + path); // create the new folder
				}

				if (!File.Exists(folder + path + "/" + filename + "_source.mp4"))
				{
					//Use ytdl-p to download the MPD as a M4A and MP4 file
					ProcessStartInfo ytdlpstartInfo = new ProcessStartInfo();
					ytdlpstartInfo.FileName = ytdlppath;
					ytdlpstartInfo.Arguments = $"--allow-u --no-part --restrict-filenames -N 4 --user-agent \"{user_agent}\" --add-header \"Cookie:CloudFront-Policy={policy}; CloudFront-Signature={signature}; CloudFront-Key-Pair-Id={kvp}; {sess}\" --referer \"https://onlyfans.com/\" -o \"{folder + path + "/"}%(title)s.%(ext)s\" \"{url}\"";
					ytdlpstartInfo.CreateNoWindow = true;

					Process ytdlpprocess = new Process();
					ytdlpprocess.StartInfo = ytdlpstartInfo;
					ytdlpprocess.Start();
					ytdlpprocess.WaitForExit();

					//Use mp4decrypt to decrypt the MP4 and M4A files
					ProcessStartInfo mp4decryptStartInfoVideo = new ProcessStartInfo();
					mp4decryptStartInfoVideo.FileName = mp4decryptpath;
					mp4decryptStartInfoVideo.Arguments = $"--key {decryptionKey} {folder + path + "/" + filename + ".f1"}.mp4 {folder + path + "/" + filename}_vdec.mp4";
					mp4decryptStartInfoVideo.CreateNoWindow = true;

					Process mp4decryptVideoProcess = new Process();
					mp4decryptVideoProcess.StartInfo = mp4decryptStartInfoVideo;
					mp4decryptVideoProcess.Start();
					mp4decryptVideoProcess.WaitForExit();

					ProcessStartInfo mp4decryptStartInfoAudio = new ProcessStartInfo();
					mp4decryptStartInfoAudio.FileName = mp4decryptpath;
					mp4decryptStartInfoAudio.Arguments = $"--key {decryptionKey} {folder + path + "/" + filename + ".f4"}.m4a {folder + path + "/" + filename}_adec.mp4";
					mp4decryptStartInfoAudio.CreateNoWindow = true;

					Process mp4decryptAudioProcess = new Process();
					mp4decryptAudioProcess.StartInfo = mp4decryptStartInfoAudio;
					mp4decryptAudioProcess.Start();
					mp4decryptAudioProcess.WaitForExit();

					//Finally use FFMPEG to merge the 2 together
					ProcessStartInfo ffmpegStartInfo = new ProcessStartInfo();
					ffmpegStartInfo.FileName = ffmpegpath;
					ffmpegStartInfo.Arguments = $"-i {folder + path + "/" + filename}_vdec.mp4 -i {folder + path + "/" + filename}_adec.mp4 -c copy {folder + path + "/" + filename}_source.mp4";
					ffmpegStartInfo.CreateNoWindow = true;

					Process ffmpegProcess = new Process();
					ffmpegProcess.StartInfo = ffmpegStartInfo;
					ffmpegProcess.Start();
					ffmpegProcess.WaitForExit();

					//Cleanup Files
					File.SetLastWriteTime($"{folder + path + "/" + filename}_source.mp4", lastModified);
					File.Delete($"{folder + path + "/" + filename + ".f1"}.mp4");
					File.Delete($"{folder + path + "/" + filename + ".f4"}.m4a");
					File.Delete($"{folder + path + "/" + filename}_adec.mp4");
					File.Delete($"{folder + path + "/" + filename}_vdec.mp4");

					return true;
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
	}
}
