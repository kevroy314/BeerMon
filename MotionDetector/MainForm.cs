// Motion Detection sample application
// AForge.NET Framework
// http://www.aforgenet.com/framework/
//
// Copyright � AForge.NET, 2006-2012
// contacts@aforgenet.com
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;
using System.Threading;

using AForge;
using AForge.Imaging;
using AForge.Video;
using AForge.Video.VFW;
using AForge.Video.DirectShow;
using AForge.Vision.Motion;
using System.IO;
using System.Collections.Concurrent;

using Alchemy.Classes;
using Alchemy;

namespace MotionDetectorSample
{
    public partial class MainForm : Form
    {
        // opened video source
        private IVideoSource videoSource = null;
        // motion detector
        MotionDetector detector = new MotionDetector(
            new TwoFramesDifferenceDetector( ),
            new MotionAreaHighlighting( ) );
        // motion detection and processing algorithm
        private int motionDetectionType = 1;
        private int motionProcessingType = 1;

        // statistics length
        private const int statLength = 15;
        // current statistics index
        private int statIndex = 0;
        // ready statistics values
        private int statReady = 0;
        // statistics array
        private int[] statCount = new int[statLength];

        // counter used for flashing
        private int flash = 0;
        private float motionAlarmLevel = 0.015f;

        private List<float> motionHistory = new List<float>( );
        private int detectedObjectsCount = -1;

        private StreamWriter fileLogWriter = null;
        public static List<string> data = new List<string>();
        protected ConcurrentDictionary<string, Connection> OnlineConnections = new ConcurrentDictionary<string, Connection>();
        private WebSocketServer aServer;

        // Constructor
        public MainForm( )
        {
            InitializeComponent( );
            Application.Idle += new EventHandler( Application_Idle );

            aServer = new WebSocketServer(8100, System.Net.IPAddress.Any)
            {
                OnReceive = OnReceive,
                OnSend = OnSend,
                OnConnected = OnConnect,
                OnDisconnect = OnDisconnect,
                TimeOut = new TimeSpan(0, 5, 0)
            };
            aServer.Start();
        }

        // Application's main form is closing
        private void MainForm_FormClosing( object sender, FormClosingEventArgs e )
        {
            CloseVideoSource( );
            aServer.Stop();
        }

        // "Exit" menu item clicked
        private void exitToolStripMenuItem_Click( object sender, EventArgs e )
        {
            this.Close( );
        }

        // "About" menu item clicked
        private void aboutToolStripMenuItem_Click( object sender, EventArgs e )
        {
            AboutForm form = new AboutForm( );
            form.ShowDialog( );
        }

        // "Open" menu item clieck - open AVI file
        private void openToolStripMenuItem_Click( object sender, EventArgs e )
        {
            if ( openFileDialog.ShowDialog( ) == DialogResult.OK )
            {
                // create video source
                AVIFileVideoSource fileSource = new AVIFileVideoSource( openFileDialog.FileName );

                OpenVideoSource( fileSource );
            }
        }

        // Open JPEG URL
        private void openJPEGURLToolStripMenuItem_Click( object sender, EventArgs e )
        {
            URLForm form = new URLForm( );

            form.Description = "Enter URL of an updating JPEG from a web camera:";
            form.URLs = new string[]
				{
					"http://195.243.185.195/axis-cgi/jpg/image.cgi?camera=1"
				};

            if ( form.ShowDialog( this ) == DialogResult.OK )
            {
                // create video source
                JPEGStream jpegSource = new JPEGStream( form.URL );

                // open it
                OpenVideoSource( jpegSource );
            }
        }

        // Open MJPEG URL
        private void openMJPEGURLToolStripMenuItem_Click( object sender, EventArgs e )
        {
            URLForm form = new URLForm( );

            form.Description = "Enter URL of an MJPEG video stream:";
            form.URLs = new string[]
				{
					"http://195.243.185.195/axis-cgi/mjpg/video.cgi?camera=3",
					"http://195.243.185.195/axis-cgi/mjpg/video.cgi?camera=4",
				};

            if ( form.ShowDialog( this ) == DialogResult.OK )
            {
                // create video source
                MJPEGStream mjpegSource = new MJPEGStream( form.URL );

                // open it
                OpenVideoSource( mjpegSource );
            }
        }

        // Open local video capture device
        private void localVideoCaptureDeviceToolStripMenuItem_Click( object sender, EventArgs e )
        {
            VideoCaptureDeviceForm form = new VideoCaptureDeviceForm( );

            if ( form.ShowDialog( this ) == DialogResult.OK )
            {
                // create video source
                VideoCaptureDevice videoSource = new VideoCaptureDevice( form.VideoDevice );

                // open it
                OpenVideoSource( videoSource );
            }
        }

        // Open video file using DirectShow
        private void openVideoFileusingDirectShowToolStripMenuItem_Click( object sender, EventArgs e )
        {
            if ( openFileDialog.ShowDialog( ) == DialogResult.OK )
            {
                // create video source
                FileVideoSource fileSource = new FileVideoSource( openFileDialog.FileName );

                // open it
                OpenVideoSource( fileSource );
            }
        }

        // Open video source
        private void OpenVideoSource( IVideoSource source )
        {
            // set busy cursor
            this.Cursor = Cursors.WaitCursor;

            // close previous video source
            CloseVideoSource( );

            // start new video source
            videoSourcePlayer.VideoSource = new AsyncVideoSource( source );
            videoSourcePlayer.Start( );

            // reset statistics
            statIndex = statReady = 0;

            // start timers
            timer.Start( );
            alarmTimer.Start( );

            videoSource = source;

            this.Cursor = Cursors.Default;
        }

        // Close current video source
        private void CloseVideoSource( )
        {
            // set busy cursor
            this.Cursor = Cursors.WaitCursor;

            // stop current video source
            videoSourcePlayer.SignalToStop( );

            // wait 2 seconds until camera stops
            for ( int i = 0; ( i < 50 ) && ( videoSourcePlayer.IsRunning ); i++ )
            {
                Thread.Sleep( 100 );
            }
            if ( videoSourcePlayer.IsRunning )
                videoSourcePlayer.Stop( );

            // stop timers
            timer.Stop( );
            alarmTimer.Stop( );

            motionHistory.Clear( );

            // reset motion detector
            if ( detector != null )
                detector.Reset( );

            videoSourcePlayer.BorderColor = Color.Black;
            this.Cursor = Cursors.Default;
        }

        // New frame received by the player
        private void videoSourcePlayer_NewFrame( object sender, ref Bitmap image )
        {
            lock ( this )
            {
                if ( detector != null )
                {
                    float motionLevel = detector.ProcessFrame( image );

                    if ( motionLevel > motionAlarmLevel )
                    {
                        // flash for 2 seconds
                        flash = (int) ( 2 * ( 1000 / alarmTimer.Interval ) );
                    }

                    // check objects' count
                    if ( detector.MotionProcessingAlgorithm is BlobCountingObjectsProcessing )
                    {
                        BlobCountingObjectsProcessing countingDetector = (BlobCountingObjectsProcessing) detector.MotionProcessingAlgorithm;
                        detectedObjectsCount = countingDetector.ObjectsCount;
                    }
                    else
                    {
                        detectedObjectsCount = -1;
                    }

                    // accumulate history
                    motionHistory.Add( motionLevel );
                    if ( motionHistory.Count > 300 )
                    {
                        motionHistory.RemoveAt( 0 );
                    }

                    if ( showMotionHistoryToolStripMenuItem.Checked )
                        DrawMotionHistory( image );

                    if (fileLogWriter != null && motionLevel != 0)
                    {
                        string outString = DateTime.Now.ToString("MMddyyyyHHmmssfff") + "\t" + ToLongString(motionLevel);
                        fileLogWriter.WriteLine(outString);
                        data.Add(outString);
                    }
                }
            }
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
        // Update some UI elements
        private void Application_Idle( object sender, EventArgs e )
        {
            objectsCountLabel.Text = ( detectedObjectsCount < 0 ) ? string.Empty : "Objects: " + detectedObjectsCount;
        }

        // Draw motion history
        private void DrawMotionHistory( Bitmap image )
        {
            Color greenColor  = Color.FromArgb( 128, 0, 255, 0);
            Color yellowColor = Color.FromArgb( 128, 255, 255, 0 );
            Color redColor    = Color.FromArgb( 128, 255, 0, 0 );

            BitmapData bitmapData = image.LockBits( new Rectangle( 0, 0, image.Width, image.Height ),
                ImageLockMode.ReadWrite, image.PixelFormat );

            int t1 = (int) ( motionAlarmLevel * 500 );
            int t2 = (int) ( 0.075 * 500 );

            for ( int i = 1, n = motionHistory.Count; i <= n; i++ )
            {
                int motionBarLength = (int) ( motionHistory[n - i] * 500 );

                if ( motionBarLength == 0 )
                    continue;

                if ( motionBarLength > 50 )
                    motionBarLength = 50;

                Drawing.Line( bitmapData,
                    new IntPoint( image.Width - i, image.Height - 1 ),
                    new IntPoint( image.Width - i, image.Height - 1 - motionBarLength ),
                    greenColor );

                if ( motionBarLength > t1 )
                {
                    Drawing.Line( bitmapData,
                        new IntPoint( image.Width - i, image.Height - 1 - t1 ),
                        new IntPoint( image.Width - i, image.Height - 1 - motionBarLength ),
                        yellowColor );
                }

                if ( motionBarLength > t2 )
                {
                    Drawing.Line( bitmapData,
                        new IntPoint( image.Width - i, image.Height - 1 - t2 ),
                        new IntPoint( image.Width - i, image.Height - 1 - motionBarLength ),
                        redColor );
                }
            }

            image.UnlockBits( bitmapData );
        }

        // On timer event - gather statistics
        private void timer_Tick( object sender, EventArgs e )
        {
            IVideoSource videoSource = videoSourcePlayer.VideoSource;

            if ( videoSource != null )
            {
                // get number of frames for the last second
                statCount[statIndex] = videoSource.FramesReceived;

                // increment indexes
                if ( ++statIndex >= statLength )
                    statIndex = 0;
                if ( statReady < statLength )
                    statReady++;

                float fps = 0;

                // calculate average value
                for ( int i = 0; i < statReady; i++ )
                {
                    fps += statCount[i];
                }
                fps /= statReady;

                statCount[statIndex] = 0;

                fpsLabel.Text = fps.ToString( "F2" ) + " fps";
            }
        }

        // Turn off motion detection
        private void noneToolStripMenuItem1_Click( object sender, EventArgs e )
        {
            motionDetectionType = 0;
            SetMotionDetectionAlgorithm( null );
        }

        // Set Two Frames Difference motion detection algorithm
        private void twoFramesDifferenceToolStripMenuItem_Click( object sender, EventArgs e )
        {
            motionDetectionType = 1;
            SetMotionDetectionAlgorithm( new TwoFramesDifferenceDetector( ) );
        }

        // Set Simple Background Modeling motion detection algorithm
        private void simpleBackgroundModelingToolStripMenuItem_Click( object sender, EventArgs e )
        {
            motionDetectionType = 2;
            SetMotionDetectionAlgorithm( new SimpleBackgroundModelingDetector( true, true ) );
        }

        // Turn off motion processing
        private void noneToolStripMenuItem2_Click( object sender, EventArgs e )
        {
            motionProcessingType = 0;
            SetMotionProcessingAlgorithm( null );
        }

        // Set motion area highlighting
        private void motionAreaHighlightingToolStripMenuItem_Click( object sender, EventArgs e )
        {
            motionProcessingType = 1;
            SetMotionProcessingAlgorithm( new MotionAreaHighlighting( ) );
        }

        // Set motion borders highlighting
        private void motionBorderHighlightingToolStripMenuItem_Click( object sender, EventArgs e )
        {
            motionProcessingType = 2;
            SetMotionProcessingAlgorithm( new MotionBorderHighlighting( ) );
        }

        // Set objects' counter
        private void blobCountingToolStripMenuItem_Click( object sender, EventArgs e )
        {
            motionProcessingType = 3;
            SetMotionProcessingAlgorithm( new BlobCountingObjectsProcessing( ) );
        }

        // Set grid motion processing
        private void gridMotionAreaProcessingToolStripMenuItem_Click( object sender, EventArgs e )
        {
            motionProcessingType = 4;
            SetMotionProcessingAlgorithm( new GridMotionAreaProcessing( 32, 32 ) );
        }

        // Set new motion detection algorithm
        private void SetMotionDetectionAlgorithm( IMotionDetector detectionAlgorithm )
        {
            lock ( this )
            {
                detector.MotionDetectionAlgorithm = detectionAlgorithm;
                motionHistory.Clear( );

                if ( detectionAlgorithm is TwoFramesDifferenceDetector )
                {
                    if (
                        ( detector.MotionProcessingAlgorithm is MotionBorderHighlighting ) ||
                        ( detector.MotionProcessingAlgorithm is BlobCountingObjectsProcessing ) )
                    {
                        motionProcessingType = 1;
                        SetMotionProcessingAlgorithm( new MotionAreaHighlighting( ) );
                    }
                }
            }
        }

        // Set new motion processing algorithm
        private void SetMotionProcessingAlgorithm( IMotionProcessing processingAlgorithm )
        {
            lock ( this )
            {
                detector.MotionProcessingAlgorithm = processingAlgorithm;
            }
        }

        // Motion menu is opening
        private void motionToolStripMenuItem_DropDownOpening( object sender, EventArgs e )
        {
            ToolStripMenuItem[] motionDetectionItems = new ToolStripMenuItem[]
            {
                noneToolStripMenuItem1, twoFramesDifferenceToolStripMenuItem,
                simpleBackgroundModelingToolStripMenuItem
            };
            ToolStripMenuItem[] motionProcessingItems = new ToolStripMenuItem[]
            {
                noneToolStripMenuItem2, motionAreaHighlightingToolStripMenuItem,
                motionBorderHighlightingToolStripMenuItem, blobCountingToolStripMenuItem,
                gridMotionAreaProcessingToolStripMenuItem
            };

            for ( int i = 0; i < motionDetectionItems.Length; i++ )
            {
                motionDetectionItems[i].Checked = ( i == motionDetectionType );
            }
            for ( int i = 0; i < motionProcessingItems.Length; i++ )
            {
                motionProcessingItems[i].Checked = ( i == motionProcessingType );
            }

            // enable/disable some motion processing algorithm depending on detection algorithm
            bool enabled = ( motionDetectionType != 1 );
            motionBorderHighlightingToolStripMenuItem.Enabled = enabled;
            blobCountingToolStripMenuItem.Enabled = enabled;
        }

        // On "Define motion regions" menu item selected
        private void defineMotionregionsToolStripMenuItem_Click( object sender, EventArgs e )
        {
            if ( videoSourcePlayer.VideoSource != null )
            {
                Bitmap currentVideoFrame = videoSourcePlayer.GetCurrentVideoFrame( );

                if ( currentVideoFrame != null )
                {
                    MotionRegionsForm form = new MotionRegionsForm( );
                    form.VideoFrame = currentVideoFrame;
                    form.MotionRectangles = detector.MotionZones;

                    // show the dialog
                    if ( form.ShowDialog( this ) == DialogResult.OK )
                    {
                        Rectangle[] rects = form.MotionRectangles;

                        if ( rects.Length == 0 )
                            rects = null;

                        detector.MotionZones = rects;
                    }

                    return;
                }
            }

            MessageBox.Show( "It is required to start video source and receive at least first video frame before setting motion zones.",
                "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
        }

        // On opening of Tools menu
        private void toolsToolStripMenuItem_DropDownOpening( object sender, EventArgs e )
        {
            localVideoCaptureSettingsToolStripMenuItem.Enabled =
                ( ( videoSource != null ) && ( videoSource is VideoCaptureDevice ) );
            crossbarVideoSettingsToolStripMenuItem.Enabled =
                ( ( videoSource != null ) && ( videoSource is VideoCaptureDevice ) && ( videoSource.IsRunning ) );
        }

        // Display properties of local capture device
        private void localVideoCaptureSettingsToolStripMenuItem_Click( object sender, EventArgs e )
        {
            if ( ( videoSource != null ) && ( videoSource is VideoCaptureDevice ) )
            {
                try
                {
                    ( (VideoCaptureDevice) videoSource ).DisplayPropertyPage( this.Handle );
                }
                catch ( NotSupportedException ex )
                {
                    MessageBox.Show( ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
                }
            }
        }

        // Display properties of crossbar filter
        private void crossbarVideoSettingsToolStripMenuItem_Click( object sender, EventArgs e )
        {
            if ( ( videoSource != null ) && ( videoSource is VideoCaptureDevice ) && ( videoSource.IsRunning ) )
            {
                Console.WriteLine( "Current input: " + ( (VideoCaptureDevice) videoSource ).CrossbarVideoInput );

                try
                {
                    ( (VideoCaptureDevice) videoSource ).DisplayCrossbarPropertyPage( this.Handle );
                }
                catch ( NotSupportedException ex )
                {
                    MessageBox.Show( ex.Message, "Error",  MessageBoxButtons.OK, MessageBoxIcon.Error );
                }
            }
        }

        // Timer used for flashing in the case if motion is detected
        private void alarmTimer_Tick( object sender, EventArgs e )
        {
            if ( flash != 0 )
            {
                videoSourcePlayer.BorderColor = ( flash % 2 == 1 ) ? Color.Black : Color.Red;
                flash--;
            }
        }

        // Change status of menu item when it is clicked
        private void showMotionHistoryToolStripMenuItem_Click( object sender, EventArgs e )
        {
            showMotionHistoryToolStripMenuItem.Checked = !showMotionHistoryToolStripMenuItem.Checked;
        }

        private void logMotionToFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (((ToolStripMenuItem)sender).Text == "Log Motion To File")
            {
                SaveFileDialog diag = new SaveFileDialog();
                if (diag.ShowDialog() == DialogResult.OK)
                {
                    fileLogWriter = new StreamWriter(diag.FileName);
                    fileLogWriter.AutoFlush = true;
                    fileLogWriter.WriteLine("time\tmotionlevel");
                }

                ((ToolStripMenuItem)sender).Text = "Stop Logging Motion To File";
            }
            else if (((ToolStripMenuItem)sender).Text == "Stop Logging Motion To File")
            {
                ((ToolStripMenuItem)sender).Text = "Log Motion To File";
                fileLogWriter.Close();
                fileLogWriter = null;
            }
        }


        public void OnConnect(UserContext aContext)
        {
            
            //Console.WriteLine("Client Connected From : " + aContext.ClientAddress.ToString());
            
            // Create a new Connection Object to save client context information
            var conn= new Connection {Context=aContext};
 
            // Add a connection Object to thread-safe collection
            OnlineConnections.TryAdd(aContext.ClientAddress.ToString(), conn);
            
        }
 
       
 
        public void OnReceive(UserContext aContext)
        {
            try
            {
                //Console.WriteLine("Data Received From [" + aContext.ClientAddress.ToString() + "] - " + aContext.DataFrame.ToString());
 
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message.ToString());
            }
            
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
            OnlineConnections.TryRemove(aContext.ClientAddress.ToString(),out conn);
 
            // Dispose timer to stop sending messages to the client.
            conn.timer.Dispose();
        }
    }

    public class Connection
    {
        public System.Threading.Timer timer;
        public UserContext Context { get; set; }
        private int lastDataSent;

        public Connection()
        {
            this.timer = new System.Threading.Timer(this.TimerCallback, null, 0, 1000);
            lastDataSent = 0;
        }

        private void TimerCallback(object state)
        {
            try
            {
                int lastIndex = MainForm.data.Count;
                for (int i = lastDataSent; i < lastIndex; i++)
                    Context.Send(MainForm.data[i]);
                lastDataSent = lastIndex;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}