using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                else if (markType == "u")
                    dot.Fill = CONFIG.COLOR_AETHERYTE;

                dot.Height = dotSize;
                dot.Width = dotSize;
                dot.Margin = new Thickness(X, Y, 0, 0);
            }
            else if (type == DotType.Flag)
            {
                ImageSource imageFlag = Utility.ByteToImage(Properties.Resources.Player_Icon47);

                dot.Fill = new ImageBrush(imageFlag);
                dot.Height = dotSize * 1.2;
                dot.Width = dotSize * 1.2;
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
            //return (Math.Floor((21.48 + (Convert.ToDouble(num) / 50)) * 100)) / 100;
            return Math.Floor(2.0 * Convert.ToDouble(num) + 2148.0) / 100;
        }

        // Check parameter name is in S/A/B list (English only)
        public static string CheckSpecialMob(string name)
        {
            var lstS = Properties.Resources.MobS.Split("\r\n");
            List<string> mobS = new List<string>(lstS);
            var lstA = Properties.Resources.MobA.Split("\r\n");
            List<string> mobA = new List<string>(lstA);
            var lstB = Properties.Resources.MobB.Split("\r\n");
            List<string> mobB = new List<string>(lstB);

            if (mobS.Contains(name))
            {
                return "s";
            }
            else if (mobA.Contains(name))
            {
                return "a";
            }
            else if (mobB.Contains(name))
            {
                return "b";
            }
            else
            {
                return "0";
            }
        }

        // Retreive map string for canvas with mapid
        public static string GetCurrentFF14Map(uint mapid, uint instance)
        {
            switch (mapid)
            {
                case 134:
                    return "ARR_ML";
                case 135:
                    return "ARR_LL";
                case 137:
                    return "ARR_EL";
                case 138:
                    return "ARR_WL";
                case 139:
                    return "ARR_UL";
                case 140:
                    return "ARR_WT";
                case 141:
                    return "ARR_CT";
                case 145:
                    return "ARR_ET";
                case 146:
                    return "ARR_ST";
                case 147:
                    return "ARR_NT";
                case 148:
                    return "ARR_CS";
                case 152:
                    return "ARR_ES";
                case 153:
                    return "ARR_SS";
                case 154:
                    return "ARR_NS";
                case 155:
                    return "ARR_CO";
                case 156:
                    return "ARR_MO";
                case 180:
                    return "ARR_OL";
                case 397:
                    return "HW_CO";
                case 401:
                    return "HW_SE";
                case 400:
                    return "HW_CH";
                case 398:
                    return "HW_DF";
                case 399:
                    return "HW_DH";
                case 402:
                    return "HW_AZ";
                case 612:
                    return "SB_FR";
                case 620:
                    return "SB_PE";
                case 621:
                    return "SB_LO";
                case 613:
                    return "SB_RU";
                case 614:
                    return "SB_YA";
                case 622:
                    return "SB_AZ";
                case 813:
                    return "ShB_LA";
                case 814:
                    return "ShB_KH";
                case 815:
                    return "ShB_AM";
                case 816:
                    return "ShB_IL";
                case 817:
                    return "ShB_RA";
                case 818:
                    return "ShB_TE";
                case 956:
                    return "EW_LA";
                case 957:
                    return "EW_TH1";
                case 958:
                    return "EW_GA";
                case 959:
                    return "EW_MA";
                case 961:
                    return "EW_EL";
                case 960:
                    return "EW_UL";
                default:
                    return "EW_GA";
            }
        }
    }
}
