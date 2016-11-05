using System;
using System.Collections.Generic;
using System.IO;
using TGProxyDemo.Users;
using TweetSharp;

namespace TGProxyDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var twBot = new TweetBot.TweetBot("213125170:AAGTgyQ3NLOs_2O6nacchnEVkDTaXinO65E", "ZlutArK4D8yisGNV80MX2UdtW", "4KoD3fGKqLkDRwdYIJ3mJGgG2Lz6GoPIK93zgWnrCnKziy7XNO");

            twBot.LoadBotUsers();
            twBot.StartBot();

            while (true)
            {
                Console.ReadLine();
            }
        }
 
    }
}
