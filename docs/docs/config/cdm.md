---
sidebar_position: 4
---

# CDM (optional)

:::warning

Please skip this if you are not very technical with computers.

:::

Two files can be generated called `device_client_id_blob` and `device_private_key`. These are used to get the decryption keys needed for downloading DRM videos.
You can find a tutorial on how to do this [here](https://forum.videohelp.com/threads/408031-Dumping-Your-own-L3-CDM-with-Android-Studio), you will need to place `device_client_id_blob` and `device_private_key` files in `cdm/devices/chrome_1610/`.
I have also made some batch scripts to run the commands included in the guide linked above (https://github.com/sim0n00ps/L3-Dumping) that can save you some time and makes the process a little simpler. You will still be able to download DRM protected videos without these files, all these files do is allow you to still be able to download DRM videos if cdrm-project (the website used if you don't have these files) goes down.
