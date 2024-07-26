---
sidebar_position: 1
---

# Windows

## Requirements

### FFmpeg

You will need to download FFmpeg. You can download it from [here](https://www.gyan.dev/ffmpeg/builds/).
Make sure you download `ffmpeg-release-essentials.zip`. Unzip it anywhere on your computer. You only need `ffmpeg.exe`, and you can ignore the rest.
Move `ffmpeg.exe` to the same folder as `OF DL.exe` (downloaded in the installation steps below). If you choose to move `ffmpeg.exe` to a different folder,
you will need to specify the path to `ffmpeg.exe` in the config file (see the `FFmpegPath` [config option](/docs/config/configuration#ffmpegpath)).

## Installation

1. Navigate to the OF-DL [releases page](https://github.com/sim0n00ps/OF-DL/releases), and download the latest release zip file. The zip file will be named `OFDLVx.x.x.zip` where `x.x.x` is the version number.
2. Unzip the downloaded file. The destination folder can be anywhere on your computer, preferably somewhere where you want to download content to/already have content downloaded.
3. Your folder should contain a folder named `cdm` as well as the following files:
   - OF DL.exe
   - config.json
   - auth.json
   - rules.json
   - e_sqlite3.dll
   - ffmpeg.exe
4. Once you have done this, please head to the [Authentication](/docs/config/auth) page to fill out the `auth.json` file.
