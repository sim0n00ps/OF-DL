---
sidebar_position: 4
---

# Custom Filename Formats

In the config.conf file you can now specify some custom filename formats that will be used when downloading files. I have had to add 4 new fields to the auth.json file, these are:

- PaidPostFileNameFormat
- PostFileNameFormat
- PaidMessageFileNameFormat
- MessageFileNameFormat

I have had to do it this way as the names of fields from the API responses are different in some places
so it would become a mess using 1 file format for everything, besides having separate formats can be useful if you only
want posts to have a custom format and the rest just use the default filename.

Below are the names of the fields you can use in each format:

## PaidPostFileNameFormat

`id` - Id of the post

`postedAt` - The date when the post was made yyyy-mm-dd

`mediaId` - Id of the media

`mediaCreatedAt` - The date when the media was uploaded to OnlyFans yyyy-mm-dd

`filename` - The original filename e.g 0gy8cmw5jjjs5pt487b9g_source.mp4 or 914x1706_6b211f68a4e315125ecf70137bb75d8e.jpg

`username` - The username of the creator e.g onlyfans

`text` - The text of the post

## PostFileNameFormat

`id` - Id of the post

`postedAt` - The date when the post was made yyyy-mm-dd

`mediaId` - Id of the media

`mediaCreatedAt` - The date when the media was uploaded to OnlyFans yyyy-mm-dd

`filename` - The original filename e.g 0gy8cmw5jjjs5pt487b9g_source.mp4 or 914x1706_6b211f68a4e315125ecf70137bb75d8e.jpg

`username` - The username of the creator e.g onlyfans

`text` - The text of the post

`rawText` - The text of the post

## PaidMessageFileNameFormat

`id` - Id of the message

`createdAt` - The date when the message was sent yyyy-mm-dd

`mediaId` - Id of the media

`mediaCreatedAt` - The date when the media was uploaded to OnlyFans yyyy-mm-dd

`filename` - The original filename e.g 0gy8cmw5jjjs5pt487b9g_source.mp4 or 914x1706_6b211f68a4e315125ecf70137bb75d8e.jpg

`username` - The username of the creator e.g onlyfans

`text` - The text of the message

## MessageFileNameFormat

`id` - Id of the message

`createdAt` - The date when the message was sent yyyy-mm-dd

`mediaId` - Id of the media

`mediaCreatedAt` - The date when the media was uploaded to OnlyFans yyyy-mm-dd

`filename` - The original filename e.g 0gy8cmw5jjjs5pt487b9g_source.mp4 or 914x1706_6b211f68a4e315125ecf70137bb75d8e.jpg

`username` - The username of the creator e.g onlyfans

`text` - The text of the message

## Examples

`"PaidPostFileNameFormat": "{id}_{mediaid}_{filename}"`

`"PostFileNameFormat": "{username}_{id}_{mediaid}_{mediaCreatedAt}"`

`"PaidMessageFileNameFormat": "{id}_{mediaid}_{createdAt}"`

`"MessageFileNameFormat": "{id}_{mediaid}_{filename}"`
