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
        public static WebsocketGraph g = new WebsocketGraph();
        public static bool running = true;
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
                g.addData(double.Parse(DateTime.Now.ToString("MMddyyyyHHmmssfff")), (double)Math.Sin(x) / 2 + 1);
                Thread.Sleep(1000);
                x += 0.5;
            }
            Console.WriteLine("Data Loop Stopped");
        }
    }
}
