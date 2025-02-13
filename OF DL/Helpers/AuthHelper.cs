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
        DefaultViewport = new ViewPortOptions
        {
            Width = 1280,
            Height = 720
        },
        Args = ["--no-sandbox", "--disable-setuid-sandbox"]
    };

    private readonly string[] _desiredCookies =
    [
        "auth_id",
        "sess"
    ];

    private readonly int LOGIN_TIMEOUT = 180000;
    private readonly int FEED_LOAD_TIMEOUT = 45000;

    private async Task LoadCookies(IPage page)
    {
        if (File.Exists("cookies.json"))
        {
            Log.Information("Loading cookies from cookies.json");
            var cookies = JsonConvert.DeserializeObject<CookieParam[]>(await File.ReadAllTextAsync("cookies.json"));
            await page.SetCookieAsync(cookies);
        }
        else
        {
            Log.Information("No cookies.json found");
        }
    }

    private async Task SaveCookies(IPage page)
    {
        Log.Information("Saving cookies to cookies.json");
        var cookies = await page.GetCookiesAsync();
        await File.WriteAllTextAsync("cookies.json", JsonConvert.SerializeObject(cookies));
    }

    private async Task<string> GetBcToken(IPage page)
    {
        return await page.EvaluateExpressionAsync<string>("window.localStorage.getItem('bcTokenSha') || ''");
    }

    public async Task<Auth?> GetAuthFromBrowser()
    {
        try
        {
            Log.Information("Downloading browser (if needed) ...");
            Console.WriteLine("Downloading browser (if needed) ...");
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            await using var browser = await Puppeteer.LaunchAsync(_options);
            await using var page = await browser.NewPageAsync();

            await LoadCookies(page);

            Log.Information("Navigating to onlyfans.com");
            await page.GoToAsync("https://onlyfans.com");

            Console.WriteLine("Login to OnlyFans with your credentials ...");

            try
            {
                await page.WaitForSelectorAsync(".g-avatar", new WaitForSelectorOptions { Timeout = LOGIN_TIMEOUT });
                Console.WriteLine("Logged in to OnlyFans");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            await SaveCookies(page);

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
