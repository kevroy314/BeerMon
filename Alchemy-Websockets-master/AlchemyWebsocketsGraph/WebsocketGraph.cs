using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using Alchemy;
using System.Threading;
using Alchemy.Classes;

namespace AlchemyWebsocketsGraph
{
    public class WebsocketGraph
    {
        private List<object> m_data = new List<object>();
        //Thread-safe collection of Online Connections.
        private ConcurrentDictionary<string, Connection> m_onlineConnections = new ConcurrentDictionary<string, Connection>();
        private WebSocketServer m_aServer;
        private int? m_historySendOnConnectCount;
        #region Data Function

        public void addData(double x, double y)
        {
            m_data.Add(new Tuple<double, double>(x, y));
        }

        public int DataCount
        {
            get { return m_data.Count; }
        }
        #endregion

        #region Websocket Code

        public WebsocketGraph(TimeSpan timeout, int port, int? historySendOnConnectCount)
        {
            m_aServer = new WebSocketServer(port, System.Net.IPAddress.Any)
            {
                OnReceive = OnReceive,
                OnSend = OnSend,
                OnConnected = OnConnect,
                OnDisconnect = OnDisconnect,
                TimeOut = timeout
            };
            m_historySendOnConnectCount = historySendOnConnectCount;
            m_aServer.Start();
        }

        public void OnConnect(UserContext aContext)
        {

            //Console.WriteLine("Client Connected From : " + aContext.ClientAddress.ToString());

            // Create a new Connection Object to save client context information
            Connection conn;
            if(m_historySendOnConnectCount.HasValue)
                conn = new Connection(ref m_data, renderPoint, true, m_historySendOnConnectCount.Value);
            else
                conn = new Connection(ref m_data, renderPoint, false, null);
            conn.Context = aContext;

            // Add a connection Object to thread-safe collection
            m_onlineConnections.TryAdd(aContext.ClientAddress.ToString(), conn);

        }

        public void OnReceive(UserContext aContext)
        {
            /*try
            {
                //Console.WriteLine("Data Received From [" + aContext.ClientAddress.ToString() + "] - " + aContext.DataFrame.ToString());

            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message.ToString());
            }*/

        }

        public void OnSend(UserContext aContext)
        {
            //Console.WriteLine("Data Sent To : " + aContext.ClientAddress.ToString());
        }

        public void OnDisconnect(UserContext aContext)
        {
            //Console.WriteLine("Client Disconnected : " + aContext.ClientAddress.ToString());

            // Remove the connection Object from the thread-safe collection
            Connection conn;
            m_onlineConnections.TryRemove(aContext.ClientAddress.ToString(), out conn);

            // Dispose timer to stop sending messages to the client.
            conn.StopConnectionThread();
        }

        public void StopServer()
        {
            m_aServer.Stop();
        }

        #endregion

        #region Render Function

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

        Connection.RenderFunc renderFunc = new Connection.RenderFunc(renderPoint);

        private static string renderPoint(object o)
        {
            Tuple<double, double> point = (Tuple<double, double>)o;
            return ToLongString(point.Item1) + "\t" + ToLongString(point.Item2);
        }

        #endregion
    }
}
