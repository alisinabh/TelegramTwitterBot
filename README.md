# TelegramTwitterBot
[![Build Status](https://travis-ci.org/alisinabh/TelegramTwitterBot.svg?branch=IntroAndHelps)](https://travis-ci.org/alisinabh/TelegramTwitterBot)

This bot is a demonstration of work, it shows how social media can be used as proxies for other services, not all of it is good, currently all messengers that are using end to end encryptions can potentially be used as proxies or they even can be used as a method of relays in Tor network.

A messenger with end-to-end encryption support can be used as a layer 2 tunnel (encoding data in HEX or some kinda string serialization) and no government can block the consumer without blocking the whole messenger down in censored area.

however this project is only using official APIs of social networks just as a demonstration.

##### UPDATE: demo version of tweetbot is now accessible for try https://telegram.me/irtweetbot/ 

### TeleramTwitterBot is currently capable of:

 * Posting text tweets
 * Viewing tweeter timeline
 * View tweeter users profiles and Follow/Unfollow
 * Viewing a users tweets
 * Retweeting a tweet
 * Qoute Retweeting a tweet
 * Reply to tweets
 * Favoriting tweets

### Will be capable of:

 * Posting tweets containing media
 * Search in twitter
 * Trends view
 * Notifications on mentions and timeline news
 * Handling twitter lists
 * Updating users profile

### This project is using the following dependencies:
* [tweetmoasharp](https://github.com/Yortw/tweetmoasharp)
* Newtonsoft.Json
* Telegram.Bot
* Microsoft.AspNet.WebApi.Client

Special Thanks to [@Yortw](https://github.com/Yortw) for [tweetsharp](https://github.com/shugonta/tweetsharp) bugfixes
