using Newtonsoft.Json;
using TweetSharp;

namespace TGProxyDemo.Users
{
    public class User
    {
        public long ChatId { get; set; }

        [JsonIgnore]
        public TwitterService TwService { get; set; }

        public OAuthAccessToken AccessToken { get; set; }

        public OAuthRequestToken RequestToken { get; set; }

        public bool IsVerified { get; set; }

        public bool IsInProgress { get; set; }

        public UserStatus Status { get; set; }

        public dynamic StatusProperty { get; set; }
    }

    public enum UserStatus
    {
        Na=0,
        InReply=1,
        InRetweet=2,
    }
}
