---
sidebar_position: 4
---

# CDM (optional, but recommended)

Without Widevine/CDM keys, OF DL uses the 3rd party website cdrm-project.org for decrypting DRM videos. With keys, OF DL directly communicates with OnlyFans. It is highly recommended to use keys, both in case the cdrm-project site is having issues (which occur frequently, in our experience) and it will result in faster download speeds, too. However, this is optional, as things will work as long as cdrm-project is functional.

Two files need to be generated, called `device_client_id_blob` and `device_private_key`. In your main OF DL folder (where you have `config.json` and `auth.json`), create a folder called `cdm` (if it does not already exist). Inside it, create a folder called `devices` and inside that, create a folder called `chrome_1610`. Finally, inside this last folder (`chrome_1610`), place the two key files. (Note that this folder name is a legacy name and OFDL does not actually use Chrome itself.)

## Manual Generation Method

You can find a tutorial on how to do this [here](https://forum.videohelp.com/threads/408031-Dumping-Your-own-L3-CDM-with-Android-Studio).

I have also made some [batch scripts](https://github.com/sim0n00ps/L3-Dumping) to run the commands included in the guide linked above that can save you some time and makes the process a little simpler. 

## Discord Method

Generating these keys can be complicated, so the team (shout out to Masaki here) have set up a bot on the Discord server to help securely deliver these keys to users who need them. You can join the discord sever [here](https://discord.com/invite/6bUW8EJ53j)

After joining, visit the bot [here](https://discord.com/channels/1198332760947966094/1333835216313122887) (the pinned post in the `#ofdl` support forum) 

## After install

Restart OF DL, and you should no longer see the yellow warning message about cdrm-project and instead see two green messages like so:

```
device_client_id_blob located successfully!
device_private_key located successfully!
```

You are now independent of cdrm-project!
