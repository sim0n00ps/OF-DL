# OF-DL
Scrape all the media from an OnlyFans account

Future Developments
1. Custom File Formats

# Prerequisites
Video Tutorial - https://gofile.io/d/RKC2EW

This app is written in .NET 7.0 so you will need to have the .NET runtime installed in order to run the program.
1. Get version 7.0.5 here https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-7.0.5-windows-x86-installer.
2. Download and run the installer.
3. Verfiy installation by opening Command Prompt or Powershell, change the directory to your C:\ drive by running `cd C:\`, then change directory to `Program Files (x86)/dotnet` by running `cd Program Files (x86)\dotnet` and finally run the `dotnet --list-runtimes` command, you should see that .NET 7.0.5 is listed.
4. Head to https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170#visual-studio-2015-2017-2019-and-2022 and download the C++ Redistributable Packages, run the installer. If you already have this installed then you can skip this step.

Next you need to download yt-dlp, ffmpeg and mp4decrypt in order to download DRM protected videos.
1. Download `yt-dlp.exe` from the latest release which you can find here https://github.com/yt-dlp/yt-dlp/releases.
2. Download ffmpeg from https://www.gyan.dev/ffmpeg/builds/ you need to download the `ffmpeg-release-essentials.zip`, unzip that file and ffmpeg.exe should be in the extracted folder.
3. Download the binaries from https://www.bento4.com/downloads/ extract the zip file and within the `bin` folder you should find `mp4decrypt.exe`.
4. Open the auth.json file in Notepad/Wordpad/Notepad++/VS Code

I would recommend copying all 3 of the .exe files somewhere safe where you can then add the path of each file to `auth.json` file. You can do this easily by holding `shift` when right clicking on the .exe file which should give you the option to `copy as path`, this will include `\` so you will need to replace them with `/`. In the auth.json file the lines should look something like this `"YTDLP_PATH": "C:/yt-dlp.exe"`, `"FFMPEG_PATH": "C:/ffmpeg.exe"` and `"MP4DECRYPT_PATH": "C:/mp4decrypt.exe"`

Finally you will need to get 2 files, device_client_id_blob and device_private_key. These are used to get the decryption keys needed for downloading DRM videos. You can find a tutorial on how to do this here https://forum.videohelp.com/threads/408031-Dumping-Your-own-L3-CDM-with-Android-Studio, you need to place device_client_id_blob and device_private_key files in `cdm/devices/chrome_1610/`.

# Running the program
Make sure you download the latest release from the [releases](https://github.com/sim0n00ps/OF-DL/releases) page and unzip the .zip file to a location where you want to download content to.
You should have 3 files in the folder you just created by unzipping the zip file, OF DL.exe, auth.json and e_sqlite3.dll. 

First you need to fill out the auth.json file.
1. Go to www.onlyfans.com and login.
2. Press F12 to open dev tools and select the 'Network' tab.
3. In the search box type 'api'

![image](https://user-images.githubusercontent.com/132307467/235547370-5ef8e273-ebf7-4783-a13a-225f5959c606.png)

4. Click on one of the requests (if nothing shows up refresh the page or click on one of the tabs such as messages to make something appear).
5. After clicking on a request, make sure the headers tab is selected and then scroll down to find the 'Request Headers' section, this is where you should be able to find the information you need.
6. Copy the values of `cookie`, `user-agent`, `user-id` (this should just be a number, do not include a `u`) and `x-bc` to the `auth.json` file where the paths to yt-dlp, ffmpeg and mp4decrypt should already be.
7. Save the file.

You should have something like this:

`"USER_ID": "123456789"` - Do NOT include the `u` that gets exported using the Onlyfans Cookie Helper

`"USER_AGENT": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/112.0.0.0 Safari/537.36"` - Make sure this is set to your user-agent value

`"X_BC": "2a9b28a68e7c03a9f0d3b98c28d70e8105e1f1df"` - Make sure this is set to your x-bc value

`"COOKIE": "auth_id=123456789; sess=k3s9tnzdc8vt2h47ljxpmwqy5r;"` - Make sure you set auth_id to the same value as `user-id` and that you set your `sess` to your actual `sess` value, everytime you log out of Onlyfans this value will change so make sure to update it after every login.

`"YTDLP_PATH": "C:/yt-dlp.exe"` - Make sure this is set to your location of yt-dlp.exe 

`"FFMPEG_PATH": "C:/ffmpeg.exe"` - Make sure this is set to your location of ffmpeg.exe 

`"MP4DECRYPT_PATH": "C:/mp4decrypt.exe"` - Make sure this is set to your location of mp4decrypt.exe 


After you have filled out the auth.json file you can double click on the OF DL.exe to run the program.
You should see something like this:
![image](https://user-images.githubusercontent.com/132307467/235548153-107f3f44-aa00-4946-8432-458329142007.png)

First of all the paths you entered for yt-dlp, ffmpeg and mp4decrypt are checked to see if they are valid. You will a see a green message if the path is valid and a red message if the path is not valid.

If the auth.json has been filled out correctly and yt-dlp, ffmpeg and mp4decrypt have been located, you should see a message in green text `Logged In successfully as {Your Username} {Your User Id}`.
However if the auth.json has been filled out but cannot log in successfully with the credentials provided then a message in red text will appear `Auth failed, please check the values in auth.json are correct, press any key to exit`. This means you need to go back and fill in the auth.json file again.

If you're logged in successfully then you will be greeted with a selection prompt. To navigate the menu the can use the &#8593; & &#8595; arrows and press `enter` to choose that option.

![image](https://user-images.githubusercontent.com/132307467/235548843-d6f46c78-7615-400a-820d-ef0dfcea4531.png)

The Select All option will go through every account you are currently subscribed to and grab all of the media from the users.

The List option will show you all of the lists you have created on OF and you can then select 1 or more lists to download the content of the users within those lists.

The Custom option allows you to select 1 or more accounts you want to scrape media from so if you only want to get media from a select number of accounts then you can do that. To navigate the menu the can use the &#8593; & &#8595; arrows. You can also press keys A-Z on the keyboard whilst in the menu to easily navigate the menu and for example pressing the letter 'c' on the keyboard will highlight the first user in the list who's username starts with the letter 'c'. To select/deselect an account, press the `space` key and after you are happy with your selection(s) press `enter` to start downloading.

The edit Auth.json option allows you to change what you do and don't want to download without having to leave the program.

After you have made your selection the content should start downloading.
Content is downloaded in this order:
1. Paid Posts
2. Posts
3. Archived
4. Stories
5. Highlights
6. Messages
7. Paid Messages

You can select what media you want to download from each account by changing the values in auth.json, by default the script downloads everything.

For instance, setting "DownloadPosts" to "false" will disable the download of media from a users main feed.

# Donations
If you would like to donate then here is a link to my ko-fi page https://ko-fi.com/sim0n00ps. Donations are not required but are very much appreciated:)
