using Newtonsoft.Json;
using OF_DL.Entities;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;
using Serilog;

namespace OF_DL.Helpers;

public class AuthHelper
{
    private readonly LaunchOptions _options = new()
    {
        Headless = false,
        Channel = ChromeReleaseChannel.Stable,
        DefaultViewport = null,
        Args = ["--no-sandbox", "--disable-setuid-sandbox"],
        UserDataDir = Path.GetFullPath("chrome-data")
    };

    private readonly string[] _desiredCookies =
    [
        "auth_id",
        "sess"
    ];

    private readonly int LOGIN_TIMEOUT = 180000;
    private readonly int FEED_LOAD_TIMEOUT = 45000;

    public async Task SetupBrowser()
    {
        string? executablePath = Environment.GetEnvironmentVariable("OFDL_PUPPETEER_EXECUTABLE_PATH");
        if (executablePath != null)
        {
            Log.Information("OFDL_PUPPETEER_EXECUTABLE_PATH environment variable found. Using browser executable path: {executablePath}", executablePath);
            _options.ExecutablePath = executablePath;
        }
        else
        {
            var browserFetcher = new BrowserFetcher();
            var installedBrowsers = browserFetcher.GetInstalledBrowsers().ToList();
            if (installedBrowsers.Count == 0)
            {
                Log.Information("Downloading browser.");
                var downloadedBrowser = await browserFetcher.DownloadAsync();
                Log.Information("Browser downloaded. Path: {executablePath}",
                    downloadedBrowser.GetExecutablePath());
                _options.ExecutablePath = downloadedBrowser.GetExecutablePath();
            }
            else
            {
                _options.ExecutablePath = installedBrowsers.First().GetExecutablePath();
            }
        }

        string? dockerEnv = Environment.GetEnvironmentVariable("OFDL_DOCKER");
        if (dockerEnv != null)
        {
            Log.Information("OFDL_DOCKER environment variable found. Using headless mode.");
            _options.Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-gpu"];
        }
    }

    private async Task<string> GetBcToken(IPage page)
    {
        return await page.EvaluateExpressionAsync<string>("window.localStorage.getItem('bcTokenSha') || ''");
    }

    public async Task<Auth?> GetAuthFromBrowser(bool isDocker = false)
    {
        try
        {

            await using var browser = await Puppeteer.LaunchAsync(_options);
            var pages = await browser.PagesAsync();
            var page = pages.First();

            await page.GoToAsync("https://onlyfans.com");

            Console.WriteLine("Login to OnlyFans with your credentials ...");

            try
            {
                await page.WaitForSelectorAsync(".b-feed", new WaitForSelectorOptions { Timeout = LOGIN_TIMEOUT });
                Console.WriteLine("Logged in to OnlyFans");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            await page.ReloadAsync();

            await page.WaitForNavigationAsync(new NavigationOptions {
                WaitUntil = [WaitUntilNavigation.Networkidle2],
                Timeout = FEED_LOAD_TIMEOUT
            });
            Console.WriteLine("DOM loaded");

            var xBc = string.Empty;
            try
            {
                xBc = await GetBcToken(page);
            }
            catch (Exception e)
            {
                throw new Exception("Error getting bcToken");
            }

            var mappedCookies = (await page.GetCookiesAsync())
                .Where(cookie => cookie.Domain.Contains("onlyfans.com"))
                .ToDictionary(cookie => cookie.Name, cookie => cookie.Value);

            mappedCookies.TryGetValue("auth_id", out var userId);
            if (userId == null)
            {
                throw new Exception("Could not find 'auth_id' cookie");
            }

            mappedCookies.TryGetValue("sess", out var sess);
            if (sess == null)
            {
                throw new Exception("Could not find 'sess' cookie");
            }

            var userAgent = await browser.GetUserAgentAsync();
            var cookies = String.Join(" ", mappedCookies.Keys.Where(key => _desiredCookies.Contains(key))
                .Select(key => $"${key}={mappedCookies[key]};"));

            return new Auth()
            {
                COOKIE = cookies,
                USER_AGENT = userAgent,
                USER_ID = userId,
                X_BC = xBc
            };
        }
        catch (Exception e)
        {
            Log.Error(e, "Error getting auth from browser");
            Console.WriteLine(e);
            return null;
        }
    }
}
