using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alchemy.Classes;

namespace AlchemyWebsocketsGraph
{
    class Connection
    {
        public delegate string RenderFunc(object o);

        //The function which renders the input data
        private RenderFunc m_renderFunc;

        //The Update Loop For the Connection
        private System.Threading.Timer m_timer;

        //The User Context set by the server
        public UserContext Context { get; set; }

        //The last index of the data that has been sent
        private int m_lastDataSent;

        //The data which is being send to the client
        private List<object> m_data;

        //Flag that tells the connection whether it should send the data history of the source
        private bool m_sendOldData;

        //Number of points which should be sent via historical data
        private int m_sendOldDataNumPoints;

        //Constructor takes a reference to a data source of any type of object and a render function
        //It will also allow for the sending of historical data if sendOldData is true.
        //To send all data, set sendOldData to true and sendOldDataNumPoints to null.
        //If sendOldDataNumPoints is greater than the number of total points in the data set, it will send all data.
        //The data will be sent ever resendTime milliseconds (default 100). Nothing will be sent if no new data is present.
        public Connection(ref List<object> dataSource, RenderFunc renderFunction, bool sendOldData, int? sendOldDataNumPoints, int resendTime = 100)
        {
            //Create the timer
            m_timer = new System.Threading.Timer(this.TimerCallback, null, 0, resendTime);
            //Set data source for stream
            m_data = dataSource;
            //Set sendOldData value to determine if we send any old data
            m_sendOldData = sendOldData; 
            //If we aren't sending old data, set the number of points to 0
            //If we are sending old data and the num points is not specified (or is larger than the data set), set the number of points to send to the entire data set
            //If we are sending old data and the num points is less than 0, send 0 points
            //If we are sending old data and the num points is between 0 and the number of available data points, send the number specified
            m_sendOldDataNumPoints = m_sendOldData ? !sendOldDataNumPoints.HasValue || sendOldDataNumPoints.Value > dataSource.Count ? dataSource.Count : sendOldDataNumPoints.Value < 0 ? 0 : sendOldDataNumPoints.Value : 0;
            //Set the render function
            m_renderFunc = renderFunction;

            //Set our data sending counter to the appropriate index in the data array
            m_lastDataSent = m_data.Count - m_sendOldDataNumPoints;
        }

        //Allows for the data pointer to be reset to some value (useful if the user clears the data source or reloads new data)
        //It is usually unwise to mess with the data source unless you are going to reform or reset the connection thread.
        public void setDataIndexToSend(int index)
        {
            m_lastDataSent = index;
        }

        private void TimerCallback(object state)
        {
            try
            {
                //Figure out the current size of the data source
                int lastIndex = m_data.Count;
                //Loop from the last data sent index (the size of the last index on the previous iteration)
                //to the current last index and send the render of each of those data points.
                for (int i = m_lastDataSent; i < lastIndex; i++)
                    Context.Send(m_renderFunc(m_data[i]));
                //Set the last data to the size of the data source we just used
                m_lastDataSent = lastIndex;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void StopConnectionThread()
        {
            m_timer.Dispose();
        }
    }
}