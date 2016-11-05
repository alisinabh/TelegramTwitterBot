﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TweetSharp;
using File = System.IO.File;
using User = TGProxyDemo.Users.User;

namespace TGProxyDemo.TweetBot
{
    public class TweetBot
    {
        public readonly TelegramBotClient Bot;// = new TelegramBotClient("213125170:AAGTgyQ3NLOs_2O6nacchnEVkDTaXinO65E");

        public static string TwitterApiKey, TwitterApiSecret;
        public Dictionary<long, User> TweetUsers = new Dictionary<long, User>();

        public static string BaseUsersPath = @"C:\Users\SoroushMehr\Documents\Visual Studio 2015\Projects\TGProxyDemo\TGProxyDemo\bin\Debug\users";


        public TweetBot(string tgToken,string twitterApiKey,string twitterApiSecret)
        {
            TwitterApiKey = twitterApiKey;
            TwitterApiSecret = twitterApiSecret;
            Bot = new TelegramBotClient(tgToken);
        }

        public Telegram.Bot.Types.User StartBot()
        {
            Bot.OnMessage += Bot_OnMessage;
            Bot.OnCallbackQuery += Bot_OnCallbackQuery;

            var me = Bot.GetMeAsync().Result;
            
            Bot.StartReceiving();

            return me;
        }

        private async void Bot_OnCallbackQuery(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
        {
            if (TweetUsers.ContainsKey(e.CallbackQuery.From.Id))
            {
                var user = TweetUsers[e.CallbackQuery.From.Id];
                await
                    ProccessUserCommand(e.CallbackQuery.Data, user,
                        messageId: e.CallbackQuery.Message.MessageId);
            }
        }

        public void LoadBotUsers(string pathToUsers = null)
        {
            var users = Directory.GetFiles(pathToUsers ?? BaseUsersPath);

            foreach (var userFile in users.Where(s => s.ToLower().EndsWith(".tw")))
            {
                var user = JsonConvert.DeserializeObject<User>(File.ReadAllText(userFile));
                user.TwService = new TwitterService(TwitterApiKey, TwitterApiSecret);
                if (user.IsVerified)
                    user.TwService.AuthenticateWith(user.AccessToken.Token, user.AccessToken.TokenSecret);
                TweetUsers.Add(user.ChatId, user);
            }
        }

        private async void Bot_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            try
            {
                var message = e.Message;

                await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                if (TweetUsers.ContainsKey(message.Chat.Id) /*&& message.Text != "/start"*/)
                {
                    var user = TweetUsers[message.Chat.Id];

                    if (!user.IsVerified)
                    {
                        user.AccessToken = user.TwService.GetAccessToken(user.RequestToken, message.Text);

                        if (user.AccessToken.UserId == 0)
                        {
                            await
                                Bot.SendTextMessageAsync(user.ChatId,
                                    "Incorrent PIN!\r\nPlease check your pin again.\r\nFor new url click on /start");
                            return;
                        }

                        user.IsVerified = true;
                        user.TwService.AuthenticateWith(user.AccessToken.Token, user.AccessToken.TokenSecret);

                        File.Create(Path.Combine(BaseUsersPath, $"{user.ChatId}.tw")).Close();
                        File.WriteAllText(Path.Combine(BaseUsersPath, $"{user.ChatId}.tw"),
                            JsonConvert.SerializeObject(user));

                    }

                    await ProccessUserCommand(message.Text, user);
                }
                else
                {

                    var user = new User()
                    {
                        ChatId = message.Chat.Id,
                        TwService = new TwitterService(TwitterApiKey, TwitterApiSecret)
                    };

                    TweetUsers.Add(message.Chat.Id, user);

                    File.Create(Path.Combine(BaseUsersPath, $"{user.ChatId}.tw")).Close();
                    File.WriteAllText(Path.Combine(BaseUsersPath, $"{user.ChatId}.tw"),
                        JsonConvert.SerializeObject(this));

                    var reqToken = user.TwService.GetRequestToken();
                    user.RequestToken = reqToken;

                    var authUri = user.TwService.GetAuthorizationUri(reqToken);

                    await
                        Bot.SendTextMessageAsync(message.Chat.Id,
                            "Please click on the link below start authenticating your twitter account:\r\n" + authUri);
                    await
                        Bot.SendTextMessageAsync(message.Chat.Id, "After Authentication proccess please enter PIN here.");

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public async Task ProccessUserCommand(string message, User user, long? messageId=null)
        {
            try
            {
                message = message.Trim();
                var cmd = GetCommandName(message);

                if (cmd.StartsWith("@"))
                    cmd = $"/u_{cmd.Substring(1)}";

                switch (cmd)
                {
                    case "/trends":
                        var trends = await user.TwService.ListLocalTrendsForAsync(new ListLocalTrendsForOptions());
                        if (trends == null)
                        {
                            await Bot.SendTextMessageAsync(user.ChatId, "No trends found!");
                            return;
                        }

                        var trndStr = new StringBuilder();
                        foreach (var twitterTrend in trends.Value)
                        {
                            trndStr.Append($"#{twitterTrend.Name}");
                        }

                        await Bot.SendTextMessageAsync(user.ChatId, trndStr.ToString());
                        break;
                    case "/timeline":
                    {
                        long? maxId = null;
                        if (message.Length > cmd.Length + 2)
                            maxId = long.Parse(message.Substring(cmd.Length + 1));
                        var timeLineItems =
                            await user.TwService.ListTweetsOnHomeTimelineAsync(new ListTweetsOnHomeTimelineOptions()
                            {
                                ContributorDetails = true,
                                Count = 10,
                                ExcludeReplies = false,
                                IncludeEntities = false,
                                MaxId = maxId,
                                SinceId = null,
                                TrimUser = null
                            });

                        if (timeLineItems != null)
                        {
                            var statuses = maxId.HasValue
                                ? timeLineItems.Value.Skip(1).Take(10).ToArray()
                                : timeLineItems.Value.Take(10).ToArray();
                            if (maxId.HasValue)
                            {
                                var timelineMoreTw =
                                    await user.TwService.GetTweetAsync(new GetTweetOptions() {Id = maxId.Value});
                                if (messageId != null)
                                    await SendSingleTweet(timelineMoreTw.Value, user, editMsgId: (int) messageId);
                            }
                            foreach (var twitterStatus in statuses)
                            {
                                try
                                {
                                    await Bot.SendChatActionAsync(user.ChatId, ChatAction.Typing);
                                    if (twitterStatus != statuses.Last())
                                        await SendTweetToUser(twitterStatus, user);
                                    else
                                    {
                                        var btns = new[]
                                        {
                                            new InlineKeyboardButton("Timeline more...", $"/timeline {twitterStatus.Id}"),
                                        };

                                        await SendTweetToUser(twitterStatus, user, btns);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                            }
                        }
                        else
                        {
                            await Bot.SendTextMessageAsync(user.ChatId, "No feed in timeline");
                        }
                    }
                        break;
                    case "/tweet":
                    {
                        var usersNewStatus = await user.TwService.SendTweetAsync(new SendTweetOptions()
                        {
                            Status = message.Substring(message.IndexOf(" ", StringComparison.Ordinal))
                        });

                        await SendSingleTweet(usersNewStatus.Value, user);
                    }
                        break;
                    case "/like":
                    {
                        var tweetId = long.Parse(message.Substring(cmd.Length + 1));
                        var rt = await user.TwService.FavoriteTweetAsync(new FavoriteTweetOptions()
                        {
                            Id = tweetId
                        });

                        var tweet = await user.TwService.GetTweetAsync(new GetTweetOptions() {Id = tweetId});

                        if (messageId != null)
                            await SendSingleTweet(tweet.Value, user, editMsgId: (int) messageId.Value);
                    }
                        break;
                    default:
                    {
                        if (cmd.StartsWith("/u_"))
                        {
// User profile view
                            var screenName = cmd.Substring(3);
                            var twitterUser = await
                                user.TwService.GetUserProfileForAsync(new GetUserProfileForOptions()
                                {
                                    ScreenName = screenName
                                });

                            var keyboard = new InlineKeyboardMarkup(new[]
                            {
                                new[] // first row
                                {
                                    (twitterUser.Value.Following ?? false)
                                        ? new InlineKeyboardButton("Unfollow ➖", callbackData: $"/unfollow {screenName}")
                                        : new InlineKeyboardButton("Follow ➕", callbackData: $"/follow {screenName}"),
                                    // left
                                },
                                new[] // second row
                                {
                                    new InlineKeyboardButton("Last 10 🕊", callbackData: $"/ut_{screenName}")
                                    // left
                                }
                            });

                            await
                                Bot.SendPhotoAsync(user.ChatId,
                                    twitterUser.Value.ProfileImageUrl.Replace("_normal", "_400x400"),
                                    $"/u_{twitterUser.Value.ScreenName}\r\n{twitterUser.Value.Description}\r\nFollowers: {twitterUser.Value.FollowersCount}\r\nFollowing: {twitterUser.Value.FriendsCount}\r\nTweets as now: {twitterUser.Value.StatusesCount}",
                                    replyMarkup: keyboard);
                        }
                        else if (cmd.StartsWith("/ut_"))
                        {
                            var screenName = cmd.Substring(4);
                            long? maxId = null;
                            if (message.Length > cmd.Length + 2)
                                maxId = long.Parse(message.Substring(cmd.Length + 1));
                            var tweets =
                                await
                                    user.TwService.ListTweetsOnUserTimelineAsync(new ListTweetsOnUserTimelineOptions()
                                    {
                                        ScreenName = screenName,
                                        Count = 5,
                                        MaxId = maxId
                                    });

                            var tweetsToShow = maxId.HasValue
                                ? tweets.Value.Skip(1).Take(10).ToArray()
                                : tweets.Value.Take(10).ToArray();
                            if (maxId.HasValue)
                            {
                                var timelineMoreTw =
                                    await user.TwService.GetTweetAsync(new GetTweetOptions() {Id = maxId.Value});
                                if (messageId != null)
                                    await SendSingleTweet(timelineMoreTw.Value, user, editMsgId: (int) messageId);
                            }
                            foreach (var tweet in tweetsToShow)
                            {
                                if (tweet != tweetsToShow.Last())
                                    await SendTweetToUser(tweet, user);
                                else
                                {
                                    var btns = new[]
                                    {
                                        new InlineKeyboardButton($"More from @{screenName}",
                                            $"/ut_{screenName} {tweet.Id}"),
                                    };

                                    await SendTweetToUser(tweet, user, btns);
                                }
                            }
                        }
                    }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task<Message> SendTweetToUser(TwitterStatus twitterStatus, User user, InlineKeyboardButton[] additionalButtons = null)
        {
            Message replMessage = null;
            if (twitterStatus.InReplyToStatusId.HasValue)
                replMessage = await SendTweetToUser(
                    user.TwService.GetTweet(new GetTweetOptions() {Id = twitterStatus.InReplyToStatusId.Value}), user);

            return
                await
                    SendSingleTweet(twitterStatus, user,
                        replMsgId: replMessage?.MessageId, additionalButtons: additionalButtons);
        }

        private async Task<Message> SendSingleTweet(TwitterStatus twitterStatus, User user,int? replMsgId=null,int? editMsgId=null, InlineKeyboardButton[] additionalButtons = null)
        {
            var keyboardData = new List<InlineKeyboardButton[]>()
            {
                new[] // first row
                {
                    new InlineKeyboardButton("⬅️", callbackData: $"/reply {twitterStatus.Id}"), // left
                    new InlineKeyboardButton(
                        "♻️" + ((twitterStatus.RetweetCount > 0) ? $" {twitterStatus.RetweetCount}" : string.Empty),
                        callbackData: $"/ret {twitterStatus.Id}"), // center
                    (twitterStatus.IsFavorited)
                        ? new InlineKeyboardButton("💔", callbackData: $"/dislike {twitterStatus.Id}")
                        : new InlineKeyboardButton("❤️", callbackData: $"/like {twitterStatus.Id}"), // right
                }
            };

            if (additionalButtons != null)
                keyboardData.Add(additionalButtons);

            var keyboard = new InlineKeyboardMarkup(keyboardData.ToArray());

            string text = $"{twitterStatus.Text.Replace("@","/u_").Replace("/u_ ","@")}\r\n{Helpers.DateTimeHelpers.GetElapsedSmallTime(twitterStatus.CreatedDate)} /u_{twitterStatus.User.ScreenName} {twitterStatus.User.Name}";

            if (editMsgId.HasValue)
                return await Bot.EditMessageTextAsync(user.ChatId, editMsgId.Value, text, replyMarkup: keyboard);
            else // this is for code waterfalling
                return await
                    Bot.SendTextMessageAsync(user.ChatId,
                        text, replyMarkup: keyboard,
                        replyToMessageId: replMsgId ?? 0);
        }

        private static string GetCommandName(string messageText)
        {
            var spaceloc = messageText.IndexOf(" ", StringComparison.Ordinal);
            return spaceloc == -1 ? messageText.ToLower() : messageText.Substring(0, spaceloc);
        }
    }
}
