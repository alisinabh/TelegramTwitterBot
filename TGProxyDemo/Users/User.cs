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
    }
}
