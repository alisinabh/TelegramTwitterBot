using System;
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

namespace TGProxyDemo.TweetBot
{
    using Users;

    public class TweetBot
    {
        private readonly TelegramBotClient _bot;

        private static string _twitterApiKey, _twitterApiSecret;
        private readonly Dictionary<long, User> _tweetUsers = new Dictionary<long, User>();

        private readonly string _baseUsersPath;


        public TweetBot(string tgToken, string twitterApiKey, string twitterApiSecret, string baseUsersPath)
        {
            _twitterApiKey = twitterApiKey;
            _twitterApiSecret = twitterApiSecret;
            _bot = new TelegramBotClient(tgToken);
            _baseUsersPath = baseUsersPath;
        }

        public Telegram.Bot.Types.User StartBot()
        {
            _bot.OnMessage += Bot_OnMessage;
            _bot.OnCallbackQuery += Bot_OnCallbackQuery;

            var me = _bot.GetMeAsync().Result;

            _bot.StartReceiving();

            return me;
        }

        private async void Bot_OnCallbackQuery(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
        {
            if (_tweetUsers.ContainsKey(e.CallbackQuery.From.Id))
            {
                var user = _tweetUsers[e.CallbackQuery.From.Id];
                await
                    ProccessUserCommand(e.CallbackQuery.Data, user,
                        messageId: e.CallbackQuery.Message.MessageId);
            }
        }

        public void LoadBotUsers(string pathToUsers = null)
        {
            var users = Directory.GetFiles(pathToUsers ?? _baseUsersPath);

            foreach (var userFile in users.Where(s => s.ToLower().EndsWith(".tw")))
            {
                var user = JsonConvert.DeserializeObject<User>(File.ReadAllText(userFile));
                user.TwService = new TwitterService(_twitterApiKey, _twitterApiSecret);
                if (user.IsVerified)
                    user.TwService.AuthenticateWith(user.AccessToken.Token, user.AccessToken.TokenSecret);
                _tweetUsers.Add(user.ChatId, user);
            }
        }

        private async void Bot_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            try
            {
                var message = e.Message;

                await _bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                if (_tweetUsers.ContainsKey(message.Chat.Id) /*&& message.Text != "/start"*/)
                {
                    var user = _tweetUsers[message.Chat.Id];

                    if (!user.IsVerified)
                    {
                        user.AccessToken = user.TwService.GetAccessToken(user.RequestToken, message.Text);

                        if (user.AccessToken.UserId == 0)
                        {
                            await
                                _bot.SendTextMessageAsync(user.ChatId,
                                    "Incorrent PIN!\r\nPlease check your pin again.\r\nFor new url click on /start");
                            return;
                        }

                        user.IsVerified = true;
                        user.TwService.AuthenticateWith(user.AccessToken.Token, user.AccessToken.TokenSecret);

                        File.Create(Path.Combine(_baseUsersPath, $"{user.ChatId}.tw")).Close();
                        File.WriteAllText(Path.Combine(_baseUsersPath, $"{user.ChatId}.tw"),
                            JsonConvert.SerializeObject(user));

                    }

                    await ProccessUserCommand(message.Text, user);
                }
                else
                {

                    var user = new User()
                    {
                        ChatId = message.Chat.Id,
                        TwService = new TwitterService(_twitterApiKey, _twitterApiSecret)
                    };

                    _tweetUsers.Add(message.Chat.Id, user);

                    File.Create(Path.Combine(_baseUsersPath, $"{user.ChatId}.tw")).Close();
                    File.WriteAllText(Path.Combine(_baseUsersPath, $"{user.ChatId}.tw"),
                        JsonConvert.SerializeObject(this));

                    var reqToken = user.TwService.GetRequestToken();
                    user.RequestToken = reqToken;

                    var authUri = user.TwService.GetAuthorizationUri(reqToken);

                    await
                        _bot.SendTextMessageAsync(message.Chat.Id,
                            "Please click on the link below start authenticating your twitter account:\r\n" + authUri);
                    await
                        _bot.SendTextMessageAsync(message.Chat.Id, "After Authentication proccess please enter PIN here.");

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task ProccessUserCommand(string message, User user, long? messageId = null)
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
                            await _bot.SendTextMessageAsync(user.ChatId, "No trends found!", disableNotification: true);
                            return;
                        }

                        var trndStr = new StringBuilder();
                        foreach (var twitterTrend in trends.Value)
                        {
                            trndStr.Append($"#{twitterTrend.Name}");
                        }

                        await _bot.SendTextMessageAsync(user.ChatId, trndStr.ToString(), disableNotification: true);
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
                                IncludeEntities = true,
                                MaxId = maxId,
                                SinceId = null,
                                TrimUser = null
                            });

                        if (timeLineItems != null && timeLineItems.Value.Any())
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
                                    await _bot.SendChatActionAsync(user.ChatId, ChatAction.Typing);
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
                            await _bot.SendTextMessageAsync(user.ChatId, "No feed in your timeline");
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
                        var tweet = await user.TwService.FavoriteTweetAsync(new FavoriteTweetOptions()
                        {
                            Id = tweetId
                        });

                        //var tweet = await user.TwService.GetTweetAsync(new GetTweetOptions() { Id = tweetId });

                        if (messageId != null)
                            await SendSingleTweet(tweet.Value, user, editMsgId: (int) messageId.Value);
                    }
                        break;
                    case "/dislike":
                    {
                        var tweetId = long.Parse(message.Substring(cmd.Length + 1));
                        var tweet =
                            await user.TwService.UnfavoriteTweetAsync(new UnfavoriteTweetOptions() {Id = tweetId});

                        //var tweet = await user.TwService.GetTweetAsync(new GetTweetOptions() { Id = tweetId });

                        if (messageId != null)
                            await SendSingleTweet(tweet.Value, user, editMsgId: (int) messageId.Value);
                    }
                        break;
                    case "/ret":
                    {
                        var tweetId = long.Parse(message.Substring(cmd.Length + 1));
                        var tweet = await user.TwService.GetTweetAsync(new GetTweetOptions() {Id = tweetId});
                        if (messageId != null)
                            await
                                SendSingleTweet(tweet.Value, user, editMsgId: (int) messageId,
                                    additionalButtons: new[]
                                    {
                                        new InlineKeyboardButton("Retweet", $"/retweet {tweetId}"),
                                        new InlineKeyboardButton("Quote Retweet",
                                            $"/qretweet {tweetId} {tweet.Value.Author.ScreenName}"),
                                    });


                    }
                        break;
                    case "/retweet":
                    {
                        var tweetId = long.Parse(message.Substring(cmd.Length + 1));
                        var tweet = await user.TwService.RetweetAsync(new RetweetOptions() {Id = tweetId});

                        await SendTweetToUser(tweet.Value, user);

                        tweet = await user.TwService.GetTweetAsync(new GetTweetOptions() {Id = tweetId});

                        if (messageId != null) await SendSingleTweet(tweet.Value, user, editMsgId: (int) messageId);
                    }
                        break;
                    case "/qretweet":
                    {
                        var props = message.Substring(cmd.Length + 1).Split(' ');
                        //var tweetId = long.Parse(props[0]);
                        //var tweet = await user.TwService.GetTweetAsync(new GetTweetOptions() { Id = tweetId });

                        user.Status = UserStatus.InQRetweet;
                        user.StatusProperty = props;

                        await
                            _bot.SendTextMessageAsync(user.ChatId,
                                "Please enter your tweet text and send\r\nto cancel qoute retweet enter /flush");
                    }
                        break;
                    case "/reply":
                    {
                        var props = message.Substring(cmd.Length + 1);
                        var tweetId = long.Parse(props);
                        var tweet = await user.TwService.GetTweetAsync(new GetTweetOptions() {Id = tweetId});

                        user.Status = UserStatus.InReply;
                        user.StatusProperty = long.Parse(props);

                        await
                            _bot.SendTextMessageAsync(user.ChatId,
                                $"Please enter your tweet text to reply to /u_{tweet.Value.Author.ScreenName} and send\r\nto cancel reply enter /flush");
                    }
                        break;
                    case "/flush":
                    {
                        if (user.Status != UserStatus.Na)
                        {
                            user.Status = UserStatus.Na;
                            user.StatusProperty = null;

                            await
                                _bot.SendTextMessageAsync(user.ChatId,
                                    "Action Cancelled!");
                        }
                        else
                        {
                            await
                                _bot.SendTextMessageAsync(user.ChatId,
                                    "No action to cancel!");
                        }
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
                                    new InlineKeyboardButton($"Tweets from @{screenName}",
                                        callbackData: $"/ut_{screenName}")
                                }
                            });

                            await
                                _bot.SendPhotoAsync(user.ChatId,
                                    twitterUser.Value.ProfileImageUrl.Replace("_normal", "_400x400"),
                                    $"/u_{twitterUser.Value.ScreenName}\r\n{twitterUser.Value.Description}\r\nFollowers: {twitterUser.Value.FollowersCount}\r\nFollowing: {twitterUser.Value.FriendsCount}\r\nTweets as now: {twitterUser.Value.StatusesCount}",
                                    replyMarkup: keyboard);
                        }
                        else if (cmd.StartsWith("/ut_"))
                        {
                            //User post view
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
                        else
                        {
                            switch (user.Status)
                            {
                                case UserStatus.Na:
                                    break;
                                case UserStatus.InReply:
                                {
                                    var statusProps = (long) user.StatusProperty;
                                    var usersNewStatus = await user.TwService.SendTweetAsync(new SendTweetOptions()
                                    {
                                        Status = message,
                                        InReplyToStatusId = statusProps,
                                    });

                                    await SendTweetToUser(usersNewStatus.Value, user);
                                    break;
                                }
                                case UserStatus.InQRetweet:
                                {
                                    var statusProps = (string[]) user.StatusProperty;
                                    var usersNewStatus = await user.TwService.SendTweetAsync(new SendTweetOptions()
                                    {
                                        Status =
                                            $"{message} https://twitter.com/{statusProps[1]}/status/{statusProps[0]}"
                                    });

                                    await SendSingleTweet(usersNewStatus.Value, user);
                                }
                                    break;
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

        private async Task<Message> SendTweetToUser(TwitterStatus twitterStatus, User user,
            InlineKeyboardButton[] additionalButtons = null)
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

        private async Task<Message> SendSingleTweet(TwitterStatus twitterStatus, User user, int? replMsgId = null,
            int? editMsgId = null, InlineKeyboardButton[] additionalButtons = null, bool notifyUser = false)
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
                        ? new InlineKeyboardButton(
                            "💔" +
                            ((twitterStatus.FavoriteCount > 0) ? $" {twitterStatus.FavoriteCount}" : string.Empty),
                            callbackData: $"/dislike {twitterStatus.Id}")
                        : new InlineKeyboardButton(
                            "❤️" +
                            ((twitterStatus.FavoriteCount > 0) ? $" {twitterStatus.FavoriteCount}" : string.Empty),
                            callbackData: $"/like {twitterStatus.Id}"), // right
                }
            };

            if (additionalButtons != null)
                keyboardData.Add(additionalButtons);

            var keyboard = new InlineKeyboardMarkup(keyboardData.ToArray());

            string text =
                $"{twitterStatus.Text.Replace("@", "/u_").Replace("/u_ ", "@")}\r\n{Helpers.DateTimeHelpers.GetElapsedSmallTime(twitterStatus.CreatedDate.ToLocalTime())} /u_{twitterStatus.User.ScreenName} {twitterStatus.User.Name}";

            if (editMsgId.HasValue)
                return await _bot.EditMessageTextAsync(user.ChatId, editMsgId.Value, text, replyMarkup: keyboard);
            else // this is for code waterfalling
                return await
                    _bot.SendTextMessageAsync(user.ChatId,
                        text, replyMarkup: keyboard,
                        replyToMessageId: replMsgId ?? 0, disableNotification: !notifyUser);
        }

        private static string GetCommandName(string messageText)
        {
            var spaceloc = messageText.IndexOf(" ", StringComparison.Ordinal);
            return spaceloc == -1 ? messageText.ToLower() : messageText.Substring(0, spaceloc);
        }
    }
}
