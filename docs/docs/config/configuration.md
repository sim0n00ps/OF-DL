---
sidebar_position: 2
---

# Configuration

The `config.conf` file contains all the options you can change, these options are listed below:

# Configuration - External Tools

## FFmpegPath

Type: `string`

Default: `""`

Allowed values: Any valid path or `""`

Description: This is the path to the FFmpeg executable (`ffmpeg.exe` on Windows and `ffmpeg` on Linux/macOS).
If the path is not set then the program will try to find it in both the same directory as the OF-DL executable as well
as the PATH environment variable.

:::note

If you are using a Windows path, you will need to escape the backslashes, e.g. `"C:\\ffmpeg\\bin\\ffmpeg.exe"`
For example, this is not valid: `"C:\some\path\ffmpeg.exe"`, but `"C:/some/path/ffmpeg.exe"` and `"C:\\some\\path\\ffmpeg.exe"` are both valid.

:::

# Configuration - Download Settings

## DownloadAvatarHeaderPhoto

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Avatar and header images will be downloaded if set to `true`

## DownloadPaidPosts

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Paid posts will be downloaded if set to `true`

## DownloadPosts

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Free posts will be downloaded if set to `true`

## DownloadArchived

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Posts in the "Archived" tab will be downloaded if set to `true`

## DownloadStreams

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Posts in the "Streams" tab will be downloaded if set to `true`

## DownloadStories

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Stories on a user's profile will be downloaded if set to `true`

## DownloadHighlights

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Highlights on a user's will be downloaded if set to `true`

## DownloadMessages

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Free media within messages (including paid message previews) will be downloaded if set to `true`

## DownloadPaidMessages

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Paid media within messages (excluding paid message previews) will be downloaded if set to `true`

## DownloadImages

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Images will be downloaded if set to `true`

## DownloadVideos

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Videos will be downloaded if set to `true`

## DownloadAudios

Type: `boolean`

Default: `true`

Allowed values: `true`, `false`

Description: Audios will be downloaded if set to `true`

## IgnoreOwnMessages

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: By default (or when set to `false`), messages that were sent by yourself will be added to the metadata DB and any media which has been sent by yourself will be downloaded. If set to `true`, the program will not add messages sent by yourself to the metadata DB and will not download any media which has been sent by yourself.

## DownloadPostsIncrementally

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: If set to `true`, only new posts will be downloaded from the date of the last post that was downloaded based off what's in the `user_data.db` file.
If set to `false`, the default behaviour will apply, and all posts will be gathered and compared against the database to see if they need to be downloaded or not.

## BypassContentForCreatorsWhoNoLongerExist

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: When a creator no longer exists (their account has been deleted), most of their content will be inaccessible.
Purchased content, however, will still be accessible by downloading media usisng the "Download Purchased Tab" menu option
or with the [NonInteractiveModePurchasedTab](#noninteractivemodepurchasedtab) config option when downloading media in non-interactive mode.

## DownloadDuplicatedMedia

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: By default (or when set to `false`), the program will not download duplicated media. If set to `true`, duplicated media will be downloaded.

## SkipAds

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: Posts and messages that contain #ad or free trial links will be ignored if set to `true`

## DownloadPath

Type: `string`

Default: `""`

Allowed values: Any valid path

Description: If left blank then content will be downloaded to `__user_data__/sites/OnlyFans/{username}`.
If you set the download path to `"S:/"`, then content will be downloaded to `S:/{username}`

:::note

If you are using a Windows path, you will need to escape the backslashes, e.g. `"C:\\Users\\user\\Downloads\\OnlyFans\\"`
Please make sure your path ends with a `/`

:::

## DownloadOnlySpecificDates

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: If set to `true`, posts will be downloaded based on the [DownloadDateSelection](#downloaddateselection) and [CustomDate](#customdate) config options.
If set to `false`, all posts will be downloaded.

## DownloadDateSelection

Type: `string`

Default: `"before"`

Allowed values: `"before"`, `"after"`

Description: [DownloadOnlySpecificDates](#downloadonlyspecificdates) needs to be set to `true` for this to work. This will get all posts from before
the date if set to `"before"`, and all posts from the date you specify up until the current date if set to `"after"`.
The date you specify will be in the [CustomDate](#customdate) config option.

## CustomDate

Type: `string`

Default: `null`

Allowed values: Any date in `yyyy-mm-dd` format or `null`

Description: [DownloadOnlySpecificDates](#downloadonlyspecificdates) needs to be set to `true` for this to work.
This date will be used when you are trying to download between/after a certain date. See [DownloadOnlySpecificDates](#downloadonlyspecificdates) and
[DownloadDateSelection](#downloaddateselection) for more information.

# Configuration - File Settings

## PaidPostFileNameFormat

Type: `string`

Default: `""`

Allowed values: Any valid string

Description: Please refer to [custom filename formats](/docs/config/custom-filename-formats#paidpostfilenameformat) page to see what fields you can use.

## PostFileNameFormat

Type: `string`

Default: `""`

Allowed values: Any valid string

Description: Please refer to the [custom filename formats](/docs/config/custom-filename-formats#postfilenameformat) page to see what fields you can use.

## PaidMessageFileNameFormat

Type: `string`

Default: `""`

Allowed values: Any valid string

Description: Please refer to [custom filename formats](/docs/config/custom-filename-formats#paidmessagefilenameformat) page to see what fields you can use.

## MessageFileNameFormat

Type: `string`

Default: `""`

Allowed values: Any valid string

Description: Please refer to [custom filename formats](/docs/config/custom-filename-formats#messagefilenameformat) page to see what fields you can use.

## RenameExistingFilesWhenCustomFormatIsSelected

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: When `true`, any current files downloaded will have the current format applied to them.
When `false`, only new files will have the current format applied to them.

# Configuration - Creator-Specific Configurations

## CreatorConfigs

Type: `object`

Default: `{}`

Allowed values: An array of Creator Config objects

Description: This configuration options allows you to set file name formats for specific creators.
This is useful if you want to have different file name formats for different creators. The values set here will override the global values set in the config file
(see [PaidPostFileNameFormat](#paidpostfilenameformat), [PostFileNameFormat](#postfilenameformat),
[PaidMessageFileNAmeFormat](#paidmessagefilenameformat), and [MessageFileNameFormat](#messagefilenameformat)).
For more information on the file name formats, see the [custom filename formats](/docs/config/custom-filename-formats) page.

Example:
```
"CreatorConfigs": {
    "creator_one": {
        "PaidPostFileNameFormat": "{id}_{mediaid}_{filename}",
        "PostFileNameFormat": "{username}_{id}_{mediaid}_{mediaCreatedAt}",
        "PaidMessageFileNameFormat": "{id}_{mediaid}_{createdAt}",
        "MessageFileNameFormat": "{id}_{mediaid}_{filename}"
    },
    "creator_two": {
        "PaidPostFileNameFormat": "{id}_{mediaid}",
        "PostFileNameFormat": "{username}_{id}_{mediaid}",
        "PaidMessageFileNameFormat": "{id}_{mediaid}",
        "MessageFileNameFormat": "{id}_{mediaid}"
    }
}
```

# Configuration - Folder Settings

## FolderPerPaidPost

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: A folder will be created for each paid post (containing all the media for that post) if set to `true`.
When set to `false`, paid post media will be downloaded into the `Posts/Paid` folder.

## FolderPerPost

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: A folder will be created for each post (containing all the media for that post) if set to `true`.
When set to `false`, post media will be downloaded into the `Posts/Free` folder.

## FolderPerPaidMessage

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: A folder will be created for each paid message (containing all the media for that message) if set to `true`.
When set to `false`, paid message media will be downloaded into the `Messages/Paid` folder.

## FolderPerMessage

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: A folder will be created for each message (containing all the media for that message) if set to `true`.
When set to `false`, message media will be downloaded into the `Messages/Free` folder.

# Configuration - Subscription Settings

## IncludeExpiredSubscriptions

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: If set to `true`, expired subscriptions will appear in the user list under the "Custom" menu option.

## IncludeRestrictedSubscriptions

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: If set to `true`, media from restricted creators will be downloaded. If set to `false`, restricted creators will be ignored.

## IgnoredUsersListName

Type: `string`

Default: `""`

Allowed values: The name of a list of users you have created on OnlyFans or `""`

Description: When set to the name of a list, users in the list will be ignored when scraping content.
If set to `""` (or an invalid list name), no users will be ignored when scraping content.

# Configuration - Interaction Settings

## NonInteractiveMode

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: If set to `true`, the program will run without any input from the user. It will scrape all users automatically
(unless [NonInteractiveModeListName](#noninteractivemodelistname) or [NonInteractiveModePurchasedTab](#noninteractivemodepurchasedtab) are configured).
If set to `false`, the default behaviour will apply, and you will be able to choose an option from the menu.

:::warning

If NonInteractiveMode is enabled, you will be unable to authenticate OF-DL using the standard authentication method.
Before you can run OF-DL in NonInteractiveMode, you must either

1. Generate an auth.json file by running OF-DL with NonInteractiveMode disabled and authenticating OF-DL using the standard method **OR**
2. Generate an auth.json file by using a [legacy authentication method](/docs/config/auth#legacy-methods)

:::

## NonInteractiveModeListName

Type: `string`

Default: `""`

Allowed values: The name of a list of users you have created on OnlyFans or `""`

Description: When set to the name of a list, non-interactive mode will download media from the list of users instead of all
users (when [NonInteractiveMode](#noninteractivemode) is set to `true`). If set to `""`, all users will be scraped
(unless [NonInteractiveModePurchasedTab](#noninteractivemodepurchasedtab) is configured).

## NonInteractiveModePurchasedTab

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: When set to `true`, non-interactive mode will only download content from the Purchased tab
(when [NonInteractiveMode](#noninteractivemode) is set to `true`). If set to `false`, all users will be scraped
(unless [NonInteractiveModeListName](#noninteractivemodelistname) is configured).

# Configuration - Performance Settings

## Timeout

Type: `integer`

Default: `-1`

Allowed values: Any positive integer or `-1`

Description: You won't need to set this, but if you see errors about the configured timeout of 100 seconds elapsing then
you could set this to be more than 100. It is recommended that you leave this as the default value.

## LimitDownloadRate

Type: `boolean`

Default: `false`

Allowed values: `true`, `false`

Description: If set to `true`, the download rate will be limited to the value set in [DownloadLimitInMbPerSec](#downloadlimitinmbpersec).

## DownloadLimitInMbPerSec

Type: `integer`

Default: `4`

Allowed values: Any positive integer

Description: The download rate in MB per second. This will only be used if [LimitDownloadRate](#limitdownloadrate) is set to `true`.

# Configuration - Logging/Debug Settings

## LoggingLevel

Type: `string`

Default: `"Error"`

Allowed values: `"Verbose"`, `"Debug"`, `"Information"`, `"Warning"`, `"Error"`, `"Fatal"`

Description: The level of logging that will be saved to the log files in the `logs` folder.
When requesting help with an issue, it is recommended to set this to `"Verbose"` and provide the log file.
