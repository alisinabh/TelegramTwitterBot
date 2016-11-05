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
            var twBot = new TweetBot.TweetBot("213125170:AAGTgyQ3NLOs_2O6nacchnEVkDTaXinO65E", "DSAp0Zy4p8J8fbAzcyuFndhR2", "mcK4QMKRp4IRJghvf6utv7eOO9ER8emTI9EGayqFH5XgMyFedA");

            twBot.LoadBotUsers();
            twBot.StartBot();

            while (true)
            {
                Console.ReadLine();
            }
        }
 
    }
}
