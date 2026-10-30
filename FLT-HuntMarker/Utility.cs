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
        private static ImageSource playerIcon;

        private static readonly Lazy<HashSet<string>> mobS = new(() => ToSet(Properties.Resources.MobS));
        private static readonly Lazy<HashSet<string>> mobA = new(() => ToSet(Properties.Resources.MobA));
        private static readonly Lazy<HashSet<string>> mobB = new(() => ToSet(Properties.Resources.MobB));

        // Make jpg to imagesource
        public static ImageSource ByteToImage(byte[] imageData)
        {
            BitmapImage biImg = new BitmapImage();
            using MemoryStream ms = new MemoryStream(imageData);
            biImg.BeginInit();
            biImg.CacheOption = BitmapCacheOption.OnLoad;
            biImg.StreamSource = ms;
            biImg.EndInit();
            biImg.Freeze();

            return biImg;
        }

        // Apply gamma to image (1.0 = original, higher is brighter)
        public static BitmapSource ApplyGamma(BitmapSource source, double gamma)
        {
            if (source == null || Math.Abs(gamma - 1.0) < 0.01)
                return source;

            FormatConvertedBitmap bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int stride = bgra.PixelWidth * 4;
            byte[] pixels = new byte[stride * bgra.PixelHeight];
            bgra.CopyPixels(pixels, stride, 0);

            byte[] lut = new byte[256];
            for (int i = 0; i < 256; i++)
                lut[i] = (byte)Math.Round(255.0 * Math.Pow(i / 255.0, 1.0 / gamma));

            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = lut[pixels[i]];
                pixels[i + 1] = lut[pixels[i + 1]];
                pixels[i + 2] = lut[pixels[i + 2]];
                // keep alpha
            }

            BitmapSource result = BitmapSource.Create(bgra.PixelWidth, bgra.PixelHeight,
                source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride);
            result.Freeze();

            return result;
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
                playerIcon ??= Utility.ByteToImage(Properties.Resources.Player_Icon47);

                dot.Fill = new ImageBrush(playerIcon);
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

        // Make instance number label centered on dot
        public static TextBlock MakeInstanceLabel(double X, double Y, double dotSize, uint instance)
        {
            TextBlock tbx = new TextBlock();

            tbx.Text = instance.ToString();
            tbx.FontSize = dotSize * 0.6;
            tbx.FontWeight = FontWeights.Bold;
            tbx.Width = dotSize;
            tbx.Height = dotSize;
            tbx.TextAlignment = TextAlignment.Center;
            tbx.LineHeight = dotSize;
            tbx.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            tbx.IsHitTestVisible = false; // Right click goes to the dot
            tbx.Margin = new Thickness(X, Y, 0, 0);

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

        // Remove lines matching predicate from CONFIG.LOGFILE
        public static void RemoveLogWhere(Func<string[], bool> predicate)
        {
            var linesToKeep = File.ReadAllLines(CONFIG.LOGFILE).Where(
                l => !predicate(l.Split(","))
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
            if (mobS.Value.Contains(name))
            {
                return "s";
            }
            else if (mobA.Value.Contains(name))
            {
                return "a";
            }
            else if (mobB.Value.Contains(name))
            {
                return "b";
            }
            else
            {
                return "0";
            }
        }

        private static HashSet<string> ToSet(string list)
        {
            return new HashSet<string>(list.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));
        }

        // Retreive map string for canvas with mapid
        public static string GetCurrentFF14Map(uint mapid, uint instance)
        {
            switch (mapid)
            {
                case 1187:
                    return "DT_UR";
                case 1188:
                    return "DT_KO";
                case 1189:
                    return "DT_YT";
                case 1190:
                    return "DT_SH";
                case 1191:
                    return "DT_HF";
                case 1192:
                    return "DT_LM";
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
                    return "EW_TH";
                case 958:
                    return "EW_GA";
                case 959:
                    return "EW_MA";
                case 961:
                    return "EW_EL";
                case 960:
                    return "EW_UL";
                default:
                    return null; // Not a hunt field map (town, duty...)
            }
        }
    }
}
