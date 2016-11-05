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

            var me = Bot.GetMeAsync().Result;
            
            Bot.StartReceiving();

            return me;
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

                await ProccessUserCommand(message, user);
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
                File.WriteAllText(Path.Combine(BaseUsersPath, $"{user.ChatId}.tw"), JsonConvert.SerializeObject(this));

                var reqToken = user.TwService.GetRequestToken();
                user.RequestToken = reqToken;

                var authUri = user.TwService.GetAuthorizationUri(reqToken);

                await
                    Bot.SendTextMessageAsync(message.Chat.Id,
                        "Please click on the link below start authenticating your twitter account:\r\n" + authUri);
                await Bot.SendTextMessageAsync(message.Chat.Id, "After Authentication proccess please enter PIN here.");

            }
        }

        public async Task ProccessUserCommand(Message message,User user)
        {
            try
            {
                var cmd = GetCommandName(message.Text);

                switch (cmd)
                {
                    case "/trends":
                        var trends = user.TwService.ListLocalTrendsFor(new ListLocalTrendsForOptions());
                        if (trends == null)
                        {
                            await Bot.SendTextMessageAsync(user.ChatId, "No trends found!");
                            return;
                        }

                        var trndStr = new StringBuilder();
                        foreach (var twitterTrend in trends)
                        {
                            trndStr.Append($"#{twitterTrend.Name}");
                        }

                        await Bot.SendTextMessageAsync(user.ChatId, trndStr.ToString());
                        break;
                    case "/timeline":
                    {
                        var timeLineItems =
                            user.TwService.ListTweetsOnHomeTimeline(new ListTweetsOnHomeTimelineOptions()
                            {
                                ContributorDetails = true,
                                Count = 10,
                                ExcludeReplies = false,
                                IncludeEntities = false,
                                MaxId = null,
                                SinceId = null,
                                TrimUser = null
                            });

                        if (timeLineItems != null)
                            foreach (var twitterStatus in timeLineItems)
                            {
                                try
                                {
                                    await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                                    await SendTweetToUser(twitterStatus, user);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
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
                        user.TwService.SendTweet(new SendTweetOptions()
                        {
                            Status = message.Text.Substring(message.Text.IndexOf(" ", StringComparison.Ordinal))
                        });

                        await Bot.SendTextMessageAsync(user.ChatId, "your tweet has posted successfully!");
                    }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task<Message> SendTweetToUser(TwitterStatus twitterStatus, User user)
        {
            if (twitterStatus.InReplyToStatusId.HasValue)
            {
                var msg = await SendTweetToUser(
                    user.TwService.GetTweet(new GetTweetOptions() {Id = twitterStatus.InReplyToStatusId.Value}), user);

                await SendSingleTweet(twitterStatus, user, replMsgId: msg.MessageId);

                return msg;
            }
            else
            {
                return await SendSingleTweet(twitterStatus, user);
            }
        }

        private async Task<Message> SendSingleTweet(TwitterStatus twitterStatus, User user,int? replMsgId=null)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                    new[] // first row
                    {
                        new InlineKeyboardButton("⬅️",callbackData:$"/reply {twitterStatus.Id}"), // left
                        new InlineKeyboardButton("♻️",callbackData:$"/ret {twitterStatus.Id}"), // center
                        new InlineKeyboardButton("❤️",callbackData:$"/like {twitterStatus.Id}"), // right
                    }
                });

            return await
                Bot.SendTextMessageAsync(user.ChatId,
                    $"{twitterStatus.Text}\r\n/u_{twitterStatus.User.ScreenName}", replyMarkup: keyboard,
                    replyToMessageId: replMsgId ?? 0);
        }

        private static string GetCommandName(string messageText)
        {
            var spaceloc = messageText.IndexOf(" ", StringComparison.Ordinal);
            return spaceloc == -1 ? messageText.ToLower() : messageText.Substring(0, spaceloc);
        }
    }
}
