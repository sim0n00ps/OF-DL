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

    private const int LoginTimeout = 180000;
    private const int FeedLoadTimeout = 45000;

    public async Task SetupBrowser(bool runningInDocker)
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

        if (runningInDocker)
        {
            Log.Information("Running in Docker. Disabling sandbox and GPU.");
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

            await using IBrowser? browser = await Puppeteer.LaunchAsync(_options);

            if (browser == null)
            {
                throw new Exception("Could not get browser");
            }

            IPage[]? pages = await browser.PagesAsync();
            IPage? page = pages.First();

            if (page == null)
            {
                throw new Exception("Could not get page");
            }

            Log.Debug("Navigating to OnlyFans.");
            await page.GoToAsync("https://onlyfans.com");

            Log.Debug("Waiting for user to login");
            await page.WaitForSelectorAsync(".b-feed", new WaitForSelectorOptions { Timeout = LoginTimeout });
            Log.Debug("Feed element detected (user logged in)");

            await page.ReloadAsync();

            await page.WaitForNavigationAsync(new NavigationOptions {
                WaitUntil = [WaitUntilNavigation.Networkidle2],
                Timeout = FeedLoadTimeout
            });
            Log.Debug("DOM loaded. Getting BC token and cookies ...");

            string xBc;
            try
            {
                xBc = await GetBcToken(page);
            }
            catch (Exception e)
            {
                throw new Exception("Error getting bcToken");
            }

            Dictionary<string, string> mappedCookies = (await page.GetCookiesAsync())
                .Where(cookie => cookie.Domain.Contains("onlyfans.com"))
                .ToDictionary(cookie => cookie.Name, cookie => cookie.Value);

            mappedCookies.TryGetValue("auth_id", out string? userId);
            if (userId == null)
            {
                throw new Exception("Could not find 'auth_id' cookie");
            }

            mappedCookies.TryGetValue("sess", out string? sess);
            if (sess == null)
            {
                throw new Exception("Could not find 'sess' cookie");
            }

            string? userAgent = await browser.GetUserAgentAsync();
            if (userAgent == null)
            {
                throw new Exception("Could not get user agent");
            }

            string cookies = string.Join(" ", mappedCookies.Keys.Where(key => _desiredCookies.Contains(key))
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
            return null;
        }
    }
}
