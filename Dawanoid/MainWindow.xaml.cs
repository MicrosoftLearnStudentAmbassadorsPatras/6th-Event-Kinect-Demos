using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Dawanoïd
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Point ballPosition = new Point(0, 0);
#if KINECTMODE
        BeamManager beamManager;
        Vector ballDirection = new Vector(1, 1);
#else
        Vector ballDirection = new Vector(2, 2);
#endif
        double padPosition = 0;
        double padDirection = 0;
        const double padInertia = 0.80;
        const double padSpeed = 2;
        int score = 0;

        KinectSensor kinectSensor;

        void Kinects_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case KinectStatus.Connected:
                    if (kinectSensor == null)
                    {
                        kinectSensor = e.Sensor;
                        Initialize();
                    }
                    break;
                case KinectStatus.Disconnected:
                    if (kinectSensor == e.Sensor)
                    {
                        Clean();
                        MessageBox.Show("Kinect was disconnected");
                    }
                    break;
                case KinectStatus.NotReady:
                    break;
                case KinectStatus.NotPowered:
                    if (kinectSensor == e.Sensor)
                    {
                        Clean();
                        MessageBox.Show("Kinect is no more powered");
                    }
                    break;
                default:
                    MessageBox.Show("Unhandled Status: " + e.Status);
                    break;
            }
        }

        void Initialize()
        {
            if (kinectSensor == null)
                return;

            kinectSensor.Start();

            beamManager = new BeamManager(kinectSensor.AudioSource);
            audioBeamAngle.DataContext = beamManager;

            kinectSensor.AudioSource.Start();

            // Launching game
            StartGame();
        }

        void StartGame()
        {
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            KeyDown += MainWindow_KeyDown;
        }

        void Clean()
        {
            if (kinectSensor == null)
                return;
            
            kinectSensor.Stop();
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                    padDirection -= padSpeed;
                    break;
                case Key.Right:
                    padDirection += padSpeed;
                    break;
            }
        }

        void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            // Pad
#if KINECTMODE
            padDirection += beamManager.BeamAngle * 2.0 - 1.0;
#endif
            padPosition += padDirection;
            if (padPosition < 0)
            {
                padDirection = 0;
                padPosition *= -1;
            }
            else if (padPosition > playground.RenderSize.Width - pad.Width - 1)
            {
                padPosition = playground.RenderSize.Width - pad.Width - 1;
                padDirection *= -1;
            }

            padDirection *= padInertia;


            // Ball
            ballPosition += ballDirection;
            
            // Walls
            if (ballPosition.X < 0)
            {
                ballPosition.X = 0;
                ballDirection.X *= -1;
            }
            else if (ballPosition.X >= playground.RenderSize.Width - ball.Width)
            {
                ballPosition.X = playground.RenderSize.Width - ball.Width - 1;
                ballDirection.X *= -1;
            }

            if (ballPosition.Y < 0)
            {
                ballPosition.Y = 0; 
                ballDirection.Y *= -1;
            }
            else if (ballPosition.Y >= playground.RenderSize.Height - ball.Height)
            {
                CompositionTarget.Rendering -= CompositionTarget_Rendering;
                ScoreText.Text = "Final score: " + (score / 10).ToString();
                return;
            }

            // Collisions
            var padRect = new Rect(padPosition, playground.RenderSize.Height - 50, pad.Width, pad.Height);
            var ballRect = new Rect(ballPosition, new Size(ball.Width, ball.Height));

            if (padRect.IntersectsWith(ballRect))
            {
                ballPosition.Y = playground.RenderSize.Height - 50 - ball.Height;
                ballDirection.Y *= -1;
            }

            // Moving
            Canvas.SetLeft(ball, ballPosition.X);
            Canvas.SetTop(ball, ballPosition.Y);

            Canvas.SetTop(pad, playground.RenderSize.Height - 50);
            Canvas.SetLeft(pad, padPosition);

            // Score
            ScoreText.Text = (score / 10).ToString();
            score++;
        }

        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {
#if KINECTMODE
            try
            {
                //listen to any status change for Kinects
                KinectSensor.KinectSensors.StatusChanged += Kinects_StatusChanged;

                //loop through all the Kinects attached to this PC, and start the first that is connected without an error.
                foreach (KinectSensor kinect in KinectSensor.KinectSensors)
                {
                    if (kinect.Status == KinectStatus.Connected)
                    {
                        kinectSensor = kinect;
                        break;
                    }
                }

                if (KinectSensor.KinectSensors.Count == 0)
                    MessageBox.Show("No Kinect found");
                else
                    Initialize();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
#else
            StartGame();
#endif
        }

        private void Window_Unloaded_1(object sender, RoutedEventArgs e)
        {
            Clean();
        }
    }
}
