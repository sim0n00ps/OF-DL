---
sidebar_position: 3
---

# Running the Program

Once you are happy you have filled everything in [auth.json](/docs/config/auth) correctly, you can double click OF-DL.exe and you should see a command prompt window appear, it should look something like this:

![CLI welcome banner](/img/welcome_banner.png)

It should locate `auth.json`, `config.json`, `rules.json` and FFmpeg successfully. If anything doesn't get located
successfully, then make sure the files exist or the path is correct.

If the auth info is correct then you should see a message in green text `Logged In successfully as {Your Username} {Your User Id}`.
However, if the `auth.json` file has been filled out but cannot log in successfully with the credentials provided,
then a message in red text will appear `Auth failed, please check the values in auth.json are correct, press any key to exit.`
This means you need to go back and fill in the `auth.json` file again, this will usually indicate that your `user-agent` has changed or you need to re-copy your `sess` value.

If you're logged in successfully then you will be greeted with a selection prompt. To navigate the menu the can use the ↑ & ↓ arrows and press `enter` to choose that option.

![CLI main menu](/img/cli_menu.png)

The `Select All` option will go through every account you are currently subscribed to and grab all of the media from the users.

The `List` option will show you all the lists you have created on OnlyFans and you can then select 1 or more lists to download the content of the users within those lists.

The `Custom` option allows you to select 1 or more accounts you want to scrape media from so if you only want to get media from a select number of accounts then you can do that.
To navigate the menu the can use the ↑ & ↓ arrows. You can also press keys A-Z on the keyboard whilst in the menu to easily navigate the menu and for example
pressing the letter 'c' on the keyboard will highlight the first user in the list whose username starts with the letter 'c'. To select/deselect an account,
press the space key, and after you are happy with your selection(s), press the enter key to start downloading.

The `Download Single Post` allows you to download a post from a URL, to get this URL go to any post and press the 3 dots, Copy link to post.

The `Download Single Message` allows you to download a message from a URL, to get this URL go to any message in the **purchased tab** and press the 3 dots, Copy link to message.

The `Download Purchased Tab` option will download all the media from the purchased tab in OnlyFans.

The `Edit config.json` option allows you to change the config from within the program.

The `Change logging level` option allows you to change the logging level that the program uses when writing logs to files in the `logs` folder.

After you have made your selection the content should start downloading. Content is downloaded in this order:

1. Paid Posts
2. Posts
3. Archived
4. Streams
5. Stories
6. Highlights
7. Messages
8. Paid Messages
