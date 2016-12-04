using System;
using System.Threading;
using System.IO;


namespace TGProxyDemo
{
    static class Program
    {
        static void Main()
        {
            StartApp();
        }

        private const string ConfigFileName = "tweetbot.conf";
        private static void StartApp()
        {
            try
            {
                if (!File.Exists(ConfigFileName))
                    ThrowMisconfigException();

                var config = File.ReadAllLines(ConfigFileName);

                if (config.Length < 3)
                    ThrowMisconfigException();

                var twBot = new TweetBot.TweetBot(config[0],
                    config[1], config[2],
                    config[3]);

                twBot.LoadBotUsers();
                twBot.StartBot();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tweet Bot initiation failed! \r\n{ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Press [Enter] to Retry...");
                Console.ReadLine();
                StartApp();
                return;
            }

            while (true)
            {
                Thread.Sleep(100);
                if (Console.ReadLine()?.ToLower() == "exit")
                    return;
            }
        }

        private static void ThrowMisconfigException()
        {
            throw new Exception($"tweetbot.conf not found!\r\nPlease provide a plain text config file named '{ConfigFileName}' in assembly startup directory with following format:\r\n"
                                    + "First line: Telegram bot token\r\nSecond Line: twitter api key\r\nThird line: twitter api secret\r\nFourth line: directory of user data to store");
        }
    }
}
