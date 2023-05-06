# OF-DL
Scrape all the media from an OnlyFans account
# Prerequisites
This app is written in .NET 7.0 so you will need to have the .NET runtime installed in order to run the program.
1. Get the latest version here https://dotnet.microsoft.com/en-us/download/dotnet/7.0, you need to download the .NET Runtime 7.X.X which is located on the right hand side of the screen (you may need to scroll down the page to find it).
2. Download and run the installer.
3. Verfiy installation by opening Command Prompt or Powershell and run the `dotnet --list-runtimes` command, you should see that .NET 7 is listed.

Next you need to download yt-dlp, ffmpeg and mp4decrypt in order to download DRM protected videos.
1. Download `yt-dlp.exe` from the latest release which you can find here https://github.com/yt-dlp/yt-dlp/releases.
2. Download ffmpeg from https://www.gyan.dev/ffmpeg/builds/ you need to download the `ffmpeg-release-essentials.zip`, unzip that file and ffmpeg.exe should be in the extracted folder.
3. Download the binaries from https://www.bento4.com/downloads/ extract the zip file and within the `bin` folder you should find `mp4decrypt.exe`.

I would recommend copying all 3 of the .exe files somewhere safe where you can then add the path of each file to `auth.json` file. You can do this easily by holding `shift` when right clicking on the .exe file which should give you the option to `copy as path`. In the auth.json file the lines should look something like this `"YTDLP_PATH": "C:/yt-dlp.exe"`, `"FFMPEG_PATH": "C:/ffmpeg.exe"` and `"MP4DECRYPT_PATH": "C:/mp4decrypt.exe"`
# Running the program
Make sure you download the latest release from the [releases](https://github.com/sim0n00ps/OF-DL/releases) page and unzip the .zip file to a location where you want to download content to.
You should have 2 files in the folder you just created by unzipping the zip file, OF DL.exe and auth.json. 

First you need to fill out the auth.json file.
1. Go to www.onlyfans.com and login.
2. Press F12 to open dev tools and select the 'Network' tab.
3. In the search box type 'api'

![image](https://user-images.githubusercontent.com/132307467/235547370-5ef8e273-ebf7-4783-a13a-225f5959c606.png)

4. Click on one of the requests (if nothing shows up refresh the page or click on one of the tabs such as messages to make something appear).
5. After clicking on a request, make sure the headers tab is selected and then scroll down to find the 'Request Headers' section, this is where you should be able to find the information you need.
6. Copy the values of `cookie`, `user-agent`, `user-id` and `x-bc` to the `auth.json` file where the paths to yt-dlp, ffmpeg and mp4decrypt should already be.
7. Save the file and you should be ready to go!

After you have filled out the auth.json file you can double click on the OF DL.exe to run the program.
You should see something like this:
![image](https://user-images.githubusercontent.com/132307467/235548153-107f3f44-aa00-4946-8432-458329142007.png)

First of all the paths you entered for yt-dlp, ffmpeg and mp4decrypt are checked to see if they are valid. You will a see a green message if the path is valid and a red message if the path is not valid.

If the auth.json has been filled out correctly and yt-dlp, ffmpeg and mp4decrypt have been located, you should see a message in green text `Logged In successfully as {Your Username} {Your User Id}`.
However if the auth.json has been filled out but cannot log in successfully with the credentials provided then a message in red text will appear `Auth failed, please check the values in auth.json are correct, press any key to exit`. This means you need to go back and fill in the auth.json file again.

If you're logged in successfully then you will be greeted with a selection prompt. To navigate the menu the can use the &#8593; & &#8595; arrows and press `enter` to choose that option.

![image](https://user-images.githubusercontent.com/132307467/235548843-d6f46c78-7615-400a-820d-ef0dfcea4531.png)

The Select All option will go through every account you are currently subscribed to and grab all of the media from the users.

The Custom option allows you to select 1 or more accounts you want to scrape media from so if you only want to get media from a select number of accounts then you can do that. To navigate the menu the can use the &#8593; & &#8595; arrows, to select/deselect an account press the `space` key and after you are happy with your selection(s) press `enter` to start downloading.

![image](https://user-images.githubusercontent.com/132307467/235549855-dd6efa98-24d5-479a-89c9-d89dbd3c01cc.png)

After you have made your selection the content should start downloading.
Content is downloaded in this order:
1. Paid Posts
2. Posts
3. Archived
4. Stories
5. Highlights
6. Messages
7. Paid Messages
