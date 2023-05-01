using System;
using System.Collections.Generic;
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
				Console.WriteLine(ex.Message);
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
				Console.WriteLine(ex.Message);
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
				Console.WriteLine(ex.Message);
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
				Console.WriteLine(ex.Message);
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
				Console.WriteLine(ex.Message);
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
				Console.WriteLine(ex.Message);
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
				Console.WriteLine(ex.Message);
			}
		}
	}
}
