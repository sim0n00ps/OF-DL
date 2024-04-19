#!/bin/bash

mkdir -p /config/cdm/devices/chrome_1610

if [ ! -f /config/auth.json ]; then
	cat > /config/auth.json <<- EOF
		{
		  "USER_ID": "",
		  "USER_AGENT": "",
		  "X_BC": "",
		  "COOKIE": ""
		}
	EOF
fi

if [ ! -f /config/config.json ]; then
	cat > /config/config.json <<- EOF
		{
		  "DownloadAvatarHeaderPhoto": true,
		  "DownloadPaidPosts": true,
		  "DownloadPosts": true,
		  "DownloadArchived": true,
		  "DownloadStreams": true,
		  "DownloadStories": true,
		  "DownloadHighlights": true,
		  "DownloadMessages": true,
		  "DownloadPaidMessages": true,
		  "DownloadImages": true,
		  "DownloadVideos": true,
		  "DownloadAudios": true,
		  "IncludeExpiredSubscriptions": true,
		  "IncludeRestrictedSubscriptions": true,
		  "SkipAds": false,
		  "DownloadPath": "/data/",
		  "PaidPostFileNameFormat": "",
		  "PostFileNameFormat": "",
		  "PaidMessageFileNameFormat": "",
		  "MessageFileNameFormat": "",
		  "RenameExistingFilesWhenCustomFormatIsSelected": true,
		  "Timeout": null,
		  "FolderPerPaidPost": false,
		  "FolderPerPost": false,
		  "FolderPerPaidMessage": false,
		  "FolderPerMessage": false,
		  "LimitDownloadRate": false,
		  "DownloadLimitInMbPerSec": 4,
		  "DownloadOnlySpecificDates": false,
		  "DownloadDateSelection": "after",
		  "CustomDate": "",
		  "ShowScrapeSize": false,
		  "DownloadPostsIncrementally": false,
		  "NonInteractiveMode": false,
		  "FFmpegPath": "/usr/bin/ffmpeg"
		}
	EOF
fi

/app/OF\ DL

