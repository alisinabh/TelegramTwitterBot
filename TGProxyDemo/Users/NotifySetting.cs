namespace TGProxyDemo.Users
{
    public class NotifySetting
    {
        public bool IsEnabled { get; set; }

        public int Interval { get; set; }

        public NotifyCategory Category { get; set; }

        public string CustomCommand { get; set; }
    }

    public enum NotifyCategory
    {
        TimelineNews = 1,
        MentionNews = 2,
    }
}
