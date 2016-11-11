using System;
using System.Threading;

namespace TGProxyDemo
{
    class Program
    {
        static void Main()
        {
            var twBot = new TweetBot.TweetBot("213125170:AAGTgyQ3NLOs_2O6nacchnEVkDTaXinO65E", "DSAp0Zy4p8J8fbAzcyuFndhR2", "mcK4QMKRp4IRJghvf6utv7eOO9ER8emTI9EGayqFH5XgMyFedA", @"C:\Users\Alisina\Source\Repos\TGProxyDemo\TGProxyDemo\bin\Debug\users");

            twBot.LoadBotUsers();
            twBot.StartBot();

            while (true)
            {
                Thread.Sleep(100);
                if (Console.ReadLine()?.ToLower() == "exit")
                    return;
            }
        }
 
    }
}
