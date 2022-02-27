using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FLT_HuntMarker
{
    class Utility
    {
        // Make jpg to imagesource
        public static ImageSource ByteToImage(byte[] imageData)
        {
            BitmapImage biImg = new BitmapImage();
            MemoryStream ms = new MemoryStream(imageData);
            biImg.BeginInit();
            biImg.StreamSource = ms;
            biImg.EndInit();

            ImageSource imgSrc = biImg as ImageSource;

            return imgSrc;
        }

        // Make ellipse
        public static Ellipse MakeDot(double X, double Y, double dotSize, DotType type, string markType)
        {
            Ellipse dot = new Ellipse();

            if (type == DotType.Circle)
            {
                dot.Stroke = new SolidColorBrush(Colors.Black);
                dot.StrokeThickness = 2;
                dot.StrokeDashArray = new DoubleCollection() { 1 };

                if (markType == "b")
                    dot.Fill = CONFIG.COLOR_B;
                else if (markType == "a")
                    dot.Fill = CONFIG.COLOR_A;
                else if (markType == "s")
                    dot.Fill = CONFIG.COLOR_S;

                dot.Height = dotSize;
                dot.Width = dotSize;
                dot.Margin = new Thickness(X, Y, 0, 0);
            }

            return dot;
        }

        // Make textblock
        public static TextBlock MakeTextblock(double X, double Y, double fontSize, string text)
        {
            TextBlock tbx = new TextBlock();

            tbx.Text = text;
            tbx.FontSize = fontSize;
            tbx.TextAlignment = TextAlignment.Center;
            tbx.Background = new SolidColorBrush(Colors.White) { Opacity = 0.5 };
            tbx.Margin = new Thickness(X - 25, Y + 20, 0, 0);

            return tbx;
        }

        // Get Current map and return it as string
        public static string GetCurrentMap()
        {
            Window window = Application.Current.MainWindow;

            var tv = (window as MainWindow).treeview.SelectedItem;
            string tvName = (tv as TreeViewItem).Name;

            return tvName;
        }

        // Remove specific content from CONFIG.LOGFILE
        public static void RemoveLogContains(string content)
        {
            var linesToKeep = File.ReadLines(CONFIG.LOGFILE).Where(
                l => !l.Contains(content)
                );

            File.WriteAllLines(CONFIG.TMPFILE, linesToKeep);
            File.Delete(CONFIG.LOGFILE);
            File.Move(CONFIG.TMPFILE, CONFIG.LOGFILE);

            return;
        }

        // Convert position
        public static double ConvertPos(double num)
        {
            return ((Math.Floor((21.48 + (Convert.ToDouble(num) / 50)) * 100)) / 100);
        }
    }
}
