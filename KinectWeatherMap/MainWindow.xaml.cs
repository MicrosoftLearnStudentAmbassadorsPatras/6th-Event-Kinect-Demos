using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace KinectWeatherMap
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        short[] depthData;
        byte[] rgbBytes;
        Skeleton[] skeletons;
        ColorImagePoint[] colorCoordinates;

        int currentBackground = -1;
        List<string> bgImages = new List<string>();
        bool isPoseLeft = false;
        bool isPoseRight = false;

        KinectSensor kinect;

        public MainWindow()
        {
            InitializeComponent();

            bgImages.Add("http://radar.weather.gov/ridge/Conus/Loop/NatLoop_Small.gif");
            bgImages.Add("http://radar.weather.gov/ridge/Conus/Loop/southeast_loop.gif");
            bgImages.Add("http://radar.weather.gov/ridge/Conus/Loop/northeast_loop.gif");
            bgImages.Add("http://radar.weather.gov/ridge/Conus/Loop/pacnorthwest_loop.gif");
            bgImages.Add("http://radar.weather.gov/ridge/Conus/Loop/pacsouthwest_loop.gif");

            //backup images in case weather is not available
            //bgImages.Add("http://i410.photobucket.com/albums/pp190/FindStuff2/Funny/Funny%20Gifs/monorail_cat_zoom.gif");
            //bgImages.Add("http://chzgifs.files.wordpress.com/2012/02/funny-gifs-kissies.gif");
            //bgImages.Add("http://chzgifs.files.wordpress.com/2012/02/funny-gifs-cat-and-the-kid.gif");

            CycleBackground();

            this.Loaded += (s, e) => 
            { 
                InitKinect(KinectSensor.KinectSensors.FirstOrDefault());
                KinectSensor.KinectSensors.StatusChanged += Kinects_StatusChanged;
            };

            this.MouseDown += (s, e) => { CycleBackground(); };
        }

        void Kinects_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case KinectStatus.Connected:
                    if (this.kinect == null)
                    {
                        InitKinect(e.Sensor);
                    }
                    break;
                case KinectStatus.Disconnected:
                    if (e.Sensor == kinect)
                    {
                        UnsubscribeKinect(e.Sensor);
                    }
                    break;
            }
        }
        
        void UnsubscribeKinect(KinectSensor sensor)
        {
            if (sensor == null)
                return;
            sensor.AllFramesReady -= kinect_AllFramesReady;
            sensor.Stop();
            this.kinect = null;
        }

        void InitKinect(KinectSensor sensor)
        {
            if (sensor == null)
                return;
            this.kinect = sensor;
            kinect.Start();

            kinect.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            kinect.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            kinect.SkeletonStream.Enable();

            kinect.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(kinect_AllFramesReady);
            kinect.ElevationAngle = 5;
        }

        void kinect_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (var depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame == null)
                    return;
                if (depthData == null || depthData.Length != depthFrame.PixelDataLength)
                {
                    depthData = new short[depthFrame.PixelDataLength];
                }
                depthFrame.CopyPixelDataTo(depthData);
            }

            using (var rgbFrame = e.OpenColorImageFrame())
            {
                if (rgbFrame == null)
                    return;

                if (rgbBytes == null || depthData.Length != rgbFrame.PixelDataLength)
                {
                    rgbBytes = new byte[rgbFrame.PixelDataLength];
                }
                rgbFrame.CopyPixelDataTo(rgbBytes);
            }

            using (var skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null)
                    return;

                if (skeletons == null || skeletons.Length != skeletonFrame.SkeletonArrayLength)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                }
                skeletonFrame.CopySkeletonDataTo(skeletons);
            }

            ProcessImage();
            ProcessSkeletons();
        }

        private void ProcessImage()
        {
            int rgbWidth = kinect.ColorStream.FrameWidth;
            int rgbHeight = kinect.ColorStream.FrameHeight;
            int depthWidth = kinect.DepthStream.FrameWidth;
            int depthHeight = kinect.DepthStream.FrameHeight;

            byte[] foregroundImage = new byte[depthWidth * depthHeight * 4];

            if (colorCoordinates == null || colorCoordinates.Length != depthData.Length)
            {
                colorCoordinates = new ColorImagePoint[depthData.Length];
            }

            kinect.MapDepthFrameToColorFrame(kinect.DepthStream.Format, depthData, kinect.ColorStream.Format, colorCoordinates);

            for (int y = 0; y < depthHeight; y++)
            {
                for (int x = 0; x < depthWidth; x++)
                {
                    int depthIndex = (x + y * depthWidth);

                    //We don't need it here, but this is how you would get the depth values
                    short depth = (short)(depthData[depthIndex] >> 3);

                    int playerIndex = depthData[depthIndex] & 7;
                    if (playerIndex == 0)
                    {
                        continue;
                    }

                    var coord = colorCoordinates[depthIndex];

                    int colorX = coord.X;
                    int colorY = coord.Y;

                    if (colorX < 0 || colorX >= rgbWidth ||
                        colorY < 0 || colorY >= rgbHeight)
                        continue;

                    int colorIndex = 4 * (colorX + colorY * rgbWidth);
                    int foregroundIndex = 4 * depthIndex;

                    foregroundImage[foregroundIndex + 0] = rgbBytes[colorIndex + 0];
                    foregroundImage[foregroundIndex + 1] = rgbBytes[colorIndex + 1];
                    foregroundImage[foregroundIndex + 2] = rgbBytes[colorIndex + 2];
                    foregroundImage[foregroundIndex + 3] = 255;
                }
            }

            var source = BitmapSource.Create(depthWidth, depthHeight,
                                                96, 96,
                                                PixelFormats.Bgra32, null,
                                                foregroundImage, depthWidth * 4);

            kinectImage.Source = source;
        }

        void ProcessSkeletons()
        {
            var skeleton = (from s in skeletons
                            where s.TrackingState == SkeletonTrackingState.Tracked
                            select s).OrderBy((s) => s.Position.Z * Math.Abs(s.Position.X))
                                            .FirstOrDefault();

            if (skeleton == null)
                return;

            var shoulderCenter = skeleton.Joints[JointType.ShoulderCenter];
            var shoulderLeft = skeleton.Joints[JointType.ShoulderLeft];
            var shoulderRight = skeleton.Joints[JointType.ShoulderRight];

            if (shoulderCenter.TrackingState != JointTrackingState.Tracked ||
                shoulderLeft.TrackingState != JointTrackingState.Tracked ||
                shoulderRight.TrackingState != JointTrackingState.Tracked)
            {
                return;
            }

            if (shoulderCenter.Position.X < -0.15 &&
                shoulderRight.Position.Z - shoulderLeft.Position.Z > .15)
            {
                isPoseLeft = true;
                if (isPoseRight)
                {
                    CycleBackground();
                    isPoseRight = false;
                }
            }
            else if (shoulderCenter.Position.X > 0.15 &&
                     shoulderLeft.Position.Z - shoulderRight.Position.Z > .15)
            {
                isPoseRight = true;
                if (isPoseLeft)
                {
                    CycleBackground();
                    isPoseLeft = false;
                }
            }
        }

        void CycleBackground()
        {
            currentBackground++;
            if (currentBackground > bgImages.Count - 1)
            {
                currentBackground = 0;
            }
            weatherImage.GifSource = bgImages[currentBackground];
        }
    }
}
