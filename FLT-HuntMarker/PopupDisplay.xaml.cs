using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace FLT_HuntMarker
{
    /// <summary>
    /// PopupDisplay.xaml
    /// </summary>
    public partial class PopupDisplay : Window
    {
        public PopupDisplay()
        {
            InitializeComponent();

            Left = Application.Current.MainWindow.Left;
            Top = Application.Current.MainWindow.Top;

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
                    listviewPopup.Items.Clear();

                    foreach (var tc in MainWindow.trackedCollection)
                    {
                        listviewPopup.Items.Add(tc.Count.ToString());
                    }

                    this.Height = 20 * listviewPopup.Items.Count + 10;
                }));

                Thread.Sleep(1000);
            }
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
