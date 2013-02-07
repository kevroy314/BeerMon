using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alchemy.Classes;
using Alchemy;
using System.Collections.Concurrent;
using System.Threading;
using AlchemyWebsocketsGraph;

namespace AlchemyWebServer
{
    class Program
    {
        public static WebsocketGraph g = new WebsocketGraph(new TimeSpan(0,5,0),8100,1000);
        public static bool running = true;
        public static int sleepTime = 1000;
        static void Main(string[] args)
        {
            Thread t = new Thread(new ThreadStart(makeData));

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Type \"exit\" and hit enter to stop the server]");

            t.Start();

            // Accept commands on the console and keep it alive
            var command = string.Empty;
            while (command != "exit")
            {
                command = Console.ReadLine();
                try
                {
                    sleepTime = int.Parse(command);
                }
                catch (Exception) { };
            }
            g.StopServer();
            running = false;
            Console.WriteLine("Server Stopped");

            while (t.ThreadState != ThreadState.Stopped) ;

            System.Environment.Exit(0);
        }
        private static double x = 0.0;
        public static void makeData()
        {
            while (running)
            {
                if(g.DataCount < 1000)
                    g.addData(double.Parse(DateTime.Now.ToString("MMddyyyyHHmmssfff")), (double)Math.Sin(x) / 2 + 1);
                Thread.Sleep(sleepTime);
                x += 0.5;
            }
            Console.WriteLine("Data Loop Stopped");
        }
    }
}
