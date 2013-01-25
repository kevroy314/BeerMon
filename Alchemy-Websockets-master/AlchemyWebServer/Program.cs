using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alchemy.Classes;
using Alchemy;
using System.Collections.Concurrent;
using System.Threading;

namespace AlchemyWebServer
{
    class Program
    {
        public static List<string> data = new List<string>();
        //Thread-safe collection of Online Connections.
        protected static ConcurrentDictionary<string, Connection> OnlineConnections = new ConcurrentDictionary<string, Connection>();
        private static WebSocketServer aServer;
        private static bool running = true;
        static void Main(string[] args)
        {
            Thread t = new Thread(new ThreadStart(makeData));

            aServer = new WebSocketServer(8100, System.Net.IPAddress.Any)
            {
                OnReceive = OnReceive,
                OnSend = OnSend,
                OnConnected = OnConnect,
                OnDisconnect = OnDisconnect,
                TimeOut = new TimeSpan(0, 5, 0)
            };
            aServer.Start();


            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Title = "Alchemy WebSocket Server";
            Console.WriteLine("Running Alchemy WebSocket Server ...");
            Console.WriteLine("[Type \"exit\" and hit enter to stop the server]");

            t.Start();

            // Accept commands on the console and keep it alive
            var command = string.Empty;
            while (command != "exit")
            {
                command = Console.ReadLine();
            }
            running = false;
            aServer.Stop();

            Console.WriteLine("Server Stopped");

            while (t.ThreadState != ThreadState.Stopped) ;

            System.Environment.Exit(0);
        }
        private static double x = 0.0;
        public static void makeData()
        {
            while (running)
            {
                data.Add(DateTime.Now.ToString("MMddyyyyHHmmssfff") + "\t" + ToLongString(Math.Sin(x) / 2 + 1));
                Thread.Sleep(1000);
                x += 0.5;
            }
            Console.WriteLine("Data Loop Stopped");
        }

        public static void OnConnect(UserContext aContext)
        {
            
            Console.WriteLine("Client Connected From : " + aContext.ClientAddress.ToString());
            
            // Create a new Connection Object to save client context information
            var conn= new Connection {Context=aContext};
 
            // Add a connection Object to thread-safe collection
            OnlineConnections.TryAdd(aContext.ClientAddress.ToString(), conn);
            
        }
 
       
 
        public static void OnReceive(UserContext aContext)
        {
            try
            {
                Console.WriteLine("Data Received From [" + aContext.ClientAddress.ToString() + "] - " + aContext.DataFrame.ToString());
 
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
            
        }
        public static void OnSend(UserContext aContext)
        {            
            Console.WriteLine("Data Sent To : " + aContext.ClientAddress.ToString());
        }
        public static void OnDisconnect(UserContext aContext)
        {
            Console.WriteLine("Client Disconnected : " + aContext.ClientAddress.ToString());
 
            // Remove the connection Object from the thread-safe collection
            Connection conn;
            OnlineConnections.TryRemove(aContext.ClientAddress.ToString(),out conn);
 
            // Dispose timer to stop sending messages to the client.
            conn.timer.Dispose();
        }
        private static string ToLongString(double input)
        {
            string str = input.ToString().ToUpper();

            // if string representation was collapsed from scientific notation, just return it:
            if (!str.Contains("E")) return str;

            bool negativeNumber = false;

            if (str[0] == '-')
            {
                str = str.Remove(0, 1);
                negativeNumber = true;
            }

            string sep = Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            char decSeparator = sep.ToCharArray()[0];

            string[] exponentParts = str.Split('E');
            string[] decimalParts = exponentParts[0].Split(decSeparator);

            // fix missing decimal point:
            if (decimalParts.Length == 1) decimalParts = new string[] { exponentParts[0], "0" };

            int exponentValue = int.Parse(exponentParts[1]);

            string newNumber = decimalParts[0] + decimalParts[1];

            string result;

            if (exponentValue > 0)
            {
                result =
                    newNumber +
                    GetZeros(exponentValue - decimalParts[1].Length);
            }
            else // negative exponent
            {
                result =
                    "0" +
                    decSeparator +
                    GetZeros(exponentValue + decimalParts[0].Length) +
                    newNumber;

                result = result.TrimEnd('0');
            }

            if (negativeNumber)
                result = "-" + result;

            return result;
        }

        private static string GetZeros(int zeroCount)
        {
            if (zeroCount < 0)
                zeroCount = Math.Abs(zeroCount);

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < zeroCount; i++) sb.Append("0");

            return sb.ToString();
        }
 
 
    
    }

    

    public class Connection
    {

        public System.Threading.Timer timer;
        public UserContext Context { get; set; }
        private int lastDataSent;
        public Connection()
        {
            this.timer = new System.Threading.Timer(this.TimerCallback, null, 0, 50);
            lastDataSent = 0;
        }

        private void TimerCallback(object state)
        {
            try
            {
                int lastIndex = Program.data.Count;
                for(int i = lastDataSent; i < lastIndex;i++)
                    Context.Send(Program.data[i]);
                lastDataSent = lastIndex;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        


    }
}
