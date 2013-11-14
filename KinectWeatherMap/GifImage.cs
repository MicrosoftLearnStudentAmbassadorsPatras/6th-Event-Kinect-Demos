using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Net;
using System.IO;

namespace KinectWeatherMap
{
    class GifImage : Grid
    {
        #region Fields

        GifBitmapDecoder decoder;
        Int32Animation animation;

        WebClient web = new WebClient();
        #endregion

        #region Properties

        #region FrameIndex

        public int FrameIndex
        {
            get { return (int)GetValue(FrameIndexProperty); }
            set { SetValue(FrameIndexProperty, value); }
        }

        public static readonly DependencyProperty FrameIndexProperty =
            DependencyProperty.Register("FrameIndex", typeof(int), typeof(GifImage), new UIPropertyMetadata(0, new PropertyChangedCallback(ChangingFrameIndex)));

        static void ChangingFrameIndex(DependencyObject obj, DependencyPropertyChangedEventArgs ev)
        {
            GifImage gif = obj as GifImage;
            int frame = (int)ev.NewValue;
            for (int i = 0; i < gif.Children.Count; i++)
            {
                if (i <= frame)
                {
                    ((Image)gif.Children[i]).Visibility = Visibility.Visible;
                }
                else
                {
                    ((Image)gif.Children[i]).Visibility = Visibility.Collapsed;
                }
            }
        }

        #endregion

        #region Constructors

        public GifImage()
        {
            web.DownloadDataCompleted += new DownloadDataCompletedEventHandler(web_DownloadDataCompleted);
        }

        #endregion

        #region GifSource

        public static readonly DependencyProperty GifSourceProperty = DependencyProperty.Register("GifSource", typeof(string), typeof(GifImage), new UIPropertyMetadata(String.Empty, new PropertyChangedCallback(GifSourceChanged)));

        public string GifSource
        {
            get { return (string)GetValue(GifSourceProperty); }
            set { SetValue(GifSourceProperty, value); }
        }

        private static void GifSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs ev)
        {
            GifImage gifImage = obj as GifImage;
            gifImage.GifSourceChanged(ev.NewValue.ToString());
        }

        private void GifSourceChanged(string newSource)
        {
            if (web.IsBusy)
            {
                web.CancelAsync();
            }
            web.DownloadDataAsync(new Uri(newSource));
        }

        void web_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
        {

            if (animation != null)
                BeginAnimation(FrameIndexProperty, null);

            var stream = new MemoryStream(e.Result);
                decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                var firstFrame = decoder.Frames[0];
                firstFrame.Freeze();

                int count = decoder.Frames.Count;

                this.Children.Clear();
                for (int i = 0; i < count; i++)
                {
                    var image = new Image();
                    image.Source = decoder.Frames[i];
                    if (i != 0)
                        image.Visibility = System.Windows.Visibility.Collapsed;

                    ushort top = (ushort)((BitmapMetadata)decoder.Frames[i].Metadata).GetQuery("/imgdesc/Top");
                    ushort left = (ushort)((BitmapMetadata)decoder.Frames[i].Metadata).GetQuery("/imgdesc/Left");
                    image.Margin = new Thickness(left, top, 0, 0);
                    this.Children.Add(image);
                }

                ushort delay = (ushort)((BitmapMetadata)firstFrame.Metadata).GetQuery("/grctlext/Delay");
                animation = new Int32Animation(0, count - 1, new Duration(TimeSpan.FromMilliseconds(count * delay * 10)));
                animation.RepeatBehavior = RepeatBehavior.Forever;
                BeginAnimation(FrameIndexProperty, animation);

        }

        #endregion

        #endregion
    }
}
