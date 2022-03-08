using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FLT_HuntMarker
{
    /// <summary>
    /// PopupDisplay.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PopupDisplay : Window
    {
        public PopupDisplay()
        {
            InitializeComponent();
            
            Thread disp = new Thread(new ThreadStart(DisplayThread));
            disp.Start();
        }

        private void DisplayThread()
        {
            while (!MainWindow.isClosing)
            {
                if (MainWindow.isDisplayChecked == false)
                    break;

                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    //listviewPopup.Items.Clear();
                    listviewPopup.Items.Add("Test");

                    foreach (var tc in MainWindow.trackedCollection)
                    {
                        listviewPopup.Items.Add(tc.Count.ToString());
                    }
                }));

                Console.WriteLine("disp thread is running");
                Trace.WriteLine("disp thread is running");

                Thread.Sleep(1000);
            }
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
