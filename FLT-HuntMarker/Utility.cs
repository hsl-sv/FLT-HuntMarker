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

        public static Ellipse MakeDot(double X, double Y, double dotSize, DotType type)
        {
            Ellipse dot = new Ellipse();

            if (type == DotType.Circle)
            {
                dot.Stroke = new SolidColorBrush(Colors.Black);
                dot.StrokeThickness = 2;
                dot.Fill = new SolidColorBrush(Colors.Red);
                dot.Height = dotSize;
                dot.Width = dotSize;
                dot.Margin = new Thickness(X, Y, 0, 0);
            }

            return dot;
        }

        public static TextBlock MakeTextblock(double X, double Y, double fontSize, string text)
        {
            TextBlock tbx = new TextBlock();

            tbx.Text = text;
            tbx.FontSize = fontSize;
            tbx.TextAlignment = TextAlignment.Center;
            tbx.Background = new SolidColorBrush(Colors.White) { Opacity = 0.5 };
            tbx.Margin = new Thickness(X - 25, Y + fontSize, 0, 0);

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

        // Remove content from CONFIG.LOGFILE
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
    }
}
