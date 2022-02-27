﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FLT_HuntMarker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public int UID = 0;
        public List<string> objList = new();
        public string markCurrent = "b";
        
        public static bool isClosing = false;
        public static bool isFF14Hooked = false;
        public static bool isHuntThreadWorking = false;
        private ObservableCollection<Mob> nearbyCollection = new();
        private ObservableCollection<Mob> trackedCollection = new();
        
        Thread t;
        Thread huntThread;

        public HuntCounter huntCounter;

        public MainWindow()
        {
            InitializeComponent();

            // Default map (Garlemald)
            imageMap.Source = Utility.ByteToImage(Properties.Resources.Garlemald_data);
            imageMap.Stretch = Stretch.Fill;

            UID = SetUID();
            huntCounter = new HuntCounter();

            Loaded += MainWindow_Loaded;
        }

        // Return last uid
        private int SetUID()
        {
            int uc = 0;

            if (String.IsNullOrEmpty(System.IO.File.ReadAllText(CONFIG.LOGFILE)))
            {
                uc = 0;
            }
            else
            {
                var last = System.IO.File.ReadLines(CONFIG.LOGFILE).Last();

                try
                {
                    uc = int.Parse(last.Split(",")[0]);
                    uc++;
                }
                catch
                {
                    uc = 0;
                }
            }

            return uc;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ParseLog();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            UpdateCanvas();

            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(new HwndSourceHook(WndProc));

            t = new Thread(new ThreadStart(SetupHuntCounter));
            t.Start();
        }

        // Parse objList from CONFIG.LOGFILE
        private void ParseLog()
        {
            var saved = System.IO.File.ReadLines(CONFIG.LOGFILE);

            foreach (string saves in saved)
            {

                string[] contents = saves.Split(",");

                if(!int.TryParse(contents[0], out int value))
                {
                    continue;
                }

                string mapid = contents[1];

                if (mapid.Length is not (5 or 6 or 7))
                {
                    continue;
                }

                objList.Add(saves);
            }
        }

        // Redraw / Update dots based on current map
        private void UpdateCanvas()
        {
            canvas.Children.Clear();
            canvas.UpdateLayout();

            for (int i = 0; i < objList.Count; i++)
            {
                string saves = objList[i];
                string[] contents = saves.Split(",");
                var tvi = treeview.SelectedItem;

                if (contents[1] != (tvi as TreeViewItem).Name)
                    continue;

                double xx = double.Parse(contents[2]);
                double yy = double.Parse(contents[3]);
                string textbox = contents[4];
                string markcur = contents[5];

                double xActual = canvas.ActualWidth;
                double yActual = canvas.ActualHeight;

                double dotSize = 20.0;
                double xr = (xActual * xx / 100.0) - (dotSize / 2.0);
                double yr = (yActual * yy / 100.0) - (dotSize / 2.0);

                Ellipse dot = Utility.MakeDot(xr, yr, dotSize, DotType.Circle, markcur);
                TextBlock tbx = Utility.MakeTextblock(xr, yr, 11, textbox);

                dot.Name = CONFIG.OBJECT_PREFIX + contents[0];
                tbx.Name = CONFIG.OBJECT_PREFIX + contents[0];

                dot.MouseRightButtonUp += Dot_MouseRightButtonUp;

                canvas.Children.Add(dot);
                canvas.Children.Add(tbx);
            }
        }

        private void treeview_CloseAll()
        {
            EW_Head.IsExpanded = false;
            ShB_Head.IsExpanded = false;
            SB_Head.IsExpanded = false;
            HW_Head.IsExpanded = false;
            ARR_Head.IsExpanded = false;
            ARR_LN_Head.IsExpanded = false;
            ARR_SH_Head.IsExpanded = false;
            ARR_TH_Head.IsExpanded = false;

            return;
        }

        private void treeview_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem tvItem = (TreeViewItem)e.NewValue;
            bool isHead = false;

            // Change map and...
            switch (tvItem.Name)
            {
                case "EW_Head":
                    treeview_CloseAll();
                    EW_Head.IsExpanded = true;
                    isHead = true;
                    break;
                case "ShB_Head":
                    treeview_CloseAll();
                    ShB_Head.IsExpanded = true;
                    isHead = true;
                    break;
                case "SB_Head":
                    treeview_CloseAll();
                    SB_Head.IsExpanded = true;
                    isHead = true;
                    break;
                case "HW_Head":
                    treeview_CloseAll();
                    HW_Head.IsExpanded = true;
                    isHead = true;
                    break;
                case "ARR_Head":
                    treeview_CloseAll();
                    ARR_Head.IsExpanded = true;
                    isHead = true;
                    break;
                case "ARR_LN_Head":
                    if (!ARR_Head.IsExpanded)
                        treeview_CloseAll();
                    ARR_LN_Head.IsExpanded = true;
                    isHead = true;
                    break;
                case "ARR_SH_Head":
                    if (!ARR_Head.IsExpanded)
                        treeview_CloseAll();
                    ARR_SH_Head.IsExpanded = true;
                    isHead = true;
                    break;
                case "ARR_TH_Head":
                    if (!ARR_Head.IsExpanded)
                        treeview_CloseAll();
                    ARR_TH_Head.IsExpanded = true;
                    isHead = true;
                    break;

                case "EW_LA1":
                case "EW_LA2":
                case "EW_LA3":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Labyrinthos_data); break;
                case "EW_GA1":
                case "EW_GA2":
                case "EW_GA3":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Garlemald_data); break;
                case "EW_TH1":
                case "EW_TH2":
                case "EW_TH3":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Thavnair_data); break;
                case "EW_MA1":
                case "EW_MA2":
                case "EW_MA3":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Mare_Lamentorum_data); break;
                case "EW_EL1":
                case "EW_EL2":
                case "EW_EL3":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Elpis_data); break;
                case "EW_UL1":
                case "EW_UL2":
                case "EW_UL3":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Ultima_Thule_data); break;

                case "ShB_LA":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Lakeland_data);
                    break;
                case "ShB_KH":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Kholusia_data);
                    break;
                case "ShB_AM":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Amh_Araeng_data);
                    break;
                case "ShB_IL":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Il_Mheg_data);
                    break;
                case "ShB_RA":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.The_Rak_tika_Greatwood_data);
                    break;
                case "ShB_TE":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.The_Tempest_data);
                    break;

                case "SB_FR":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.The_Fringes_data);
                    break;
                case "SB_PE":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.The_Peaks_data);
                    break;
                case "SB_LO":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.The_Lochs_data);
                    break;
                case "SB_RU":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.The_Ruby_Sea_data);
                    break;
                case "SB_YA":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Yanxia_data);
                    break;
                case "SB_AZ":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.The_Azim_Steppe_data);
                    break;

                case "HW_CO":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Coerthas_Western_Highlands_data);
                    break;
                case "HW_SE":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.The_Sea_of_Clouds_data);
                    break;
                case "HW_CH":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.The_Churning_Mists_data);
                    break;
                case "HW_DF":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.The_Dravanian_Forelands_data);
                    break;
                case "HW_DH":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.The_Dravanian_Hinterlands_data);
                    break;
                case "HW_AZ":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Azys_Lla_data);
                    break;

                case "ARR_CO":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Coerthas_Central_Highlands_data);
                    break;
                case "ARR_MO":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Mor_Dhona_data);
                    break;
                case "ARR_ML":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Middle_La_Noscea_data);
                    break;
                case "ARR_LL":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Lower_La_Noscea_data);
                    break;
                case "ARR_EL":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Eastern_La_Noscea_data);
                    break;
                case "ARR_WL":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Western_La_Noscea_data);
                    break;
                case "ARR_UL":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Upper_La_Noscea_data);
                    break;
                case "ARR_OL":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Outer_La_Noscea_data);
                    break;
                case "ARR_CS":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Central_Shroud_data);
                    break;
                case "ARR_ES":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.East_Shroud_data);
                    break;
                case "ARR_SS":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.South_Shroud_data);
                    break;
                case "ARR_NS":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.North_Shroud_data);
                    break;
                case "ARR_WT":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Western_Thanalan_data);
                    break;
                case "ARR_CT":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Central_Thanalan_data);
                    break;
                case "ARR_ET":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Eastern_Thanalan_data);
                    break;
                case "ARR_ST":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Southern_Thanalan_data);
                    break;
                case "ARR_NT":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Northern_Thanalan_data);
                    break;
                default:
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Garlemald_data);
                    break;
            }

            imageMap.Stretch = Stretch.Fill;

            // Parse exist child objects from log file
            if (isHead is false)
                UpdateCanvas();
        }

        // Left click means X mark + Timestamp
        // Also make log for each map
        private void canvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(null);
            double x = pos.X;
            double y = pos.Y;

            double dotSize = 10.0;
            double xr = x - (dotSize / 2.0);
            double yr = y - (dotSize / 2.0);

            var timestamp = DateTime.Now.ToString(CONFIG.TIMESTAMP_FORMAT);

            // Create information for save
            string currentMap = Utility.GetCurrentMap();
            string xx = (x / canvas.ActualWidth * 100.0).ToString("0.0");
            string yy = (y / canvas.ActualHeight * 100.0).ToString("0.0");

            string log = UID.ToString() + "," + currentMap + "," + xx + "," + yy + "," + timestamp + "," + markCurrent;
            objList.Add(log);
            log += Environment.NewLine;
            System.IO.File.AppendAllText(CONFIG.LOGFILE, log);

            // Update canvas
            UpdateCanvas();

            // Increase UID
            UID++;

            return;
        }

        // Right click exact dot position means delete dot and timestamp
        private void Dot_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Ellipse dot = (Ellipse)sender;

            for (int i = 0; i < canvas.Children.Count; i++)
            {
                Ellipse child = new();

                if (canvas.Children[i] is Ellipse)
                    child = (Ellipse)canvas.Children[i];
                else
                    continue;

                if (dot.Name == child.Name)
                {
                    string[] objName = dot.Name.Split("_");
                    int objUID = int.Parse(objName[1]);

                    canvas.Children.RemoveAt(i + 1); // is timestamp (textblock)
                    canvas.Children.RemoveAt(i); // is dot (ellipse)

                    Utility.RemoveLogContains(objUID.ToString() + "," + Utility.GetCurrentMap());

                    for (int j = 0; j < objList.Count; j++)
                    { 
                        if (objList[j].Contains(objUID.ToString() + "," + Utility.GetCurrentMap()))
                        {
                            objList.RemoveAt(j);
                        }
                    }
                }
            }

            return;
        }

        // TODO: Right click CANVAS means drop down menu
        private void canvas_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(null);
            double x = pos.X;
            double y = pos.Y;
        }

        // Clear current button means remove all canvas children (of current map)
        private void buttonClear_Click(object sender, RoutedEventArgs e)
        {
            // Rebuild objList
            for (int i = 0; i < objList.Count; i++)
            {
                if (objList[i].Contains(Utility.GetCurrentMap()))
                {
                    objList.RemoveAt(i);
                    i--;
                }
            }

            // Rebuild log file
            Utility.RemoveLogContains(Utility.GetCurrentMap());

            UpdateCanvas();

            return;
        }

        // ClearAll button means clear all
        private void buttonClearAll_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show("Clear All Markers?", "Question",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
            {
                canvas.Children.Clear();
                canvas.UpdateLayout();
                objList.Clear();
                System.IO.File.WriteAllText(CONFIG.LOGFILE, "");

                UpdateCanvas();
            }

            return;
        }

        private void radioButton_Click(object sender, RoutedEventArgs e)
        {
            if (radioButtonS.IsChecked is true)
            {
                markCurrent = "s";
            }
            else if (radioButtonA.IsChecked is true)
            {
                markCurrent = "a";
            }
            else if (radioButtonB.IsChecked is true)
            {
                markCurrent = "b";
            }
        }

        // Redraw when window size changed
        const int WM_SIZING = 0x214;
        const int WM_EXITSIZEMOVE = 0x232;
        private static bool WindowWasResized = false;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SIZING)
                if (WindowWasResized == false)
                    WindowWasResized = true;

            if (msg == WM_EXITSIZEMOVE)
            {        
                if (WindowWasResized == true)
                {
                    UpdateCanvas();
                    WindowWasResized = false;
                }
            }

            return IntPtr.Zero;
        }


        private void SetupHuntCounter()
        {
            var processExists = huntCounter.Setup();

            while (!processExists)
            {
                if (isClosing == true)
                    return;

                Console.WriteLine("Process not found... Trying again");
                Trace.WriteLine("Process not found... Trying again");

                processExists = huntCounter.Setup();

                Thread.Sleep(1000);
            }

            isFF14Hooked = true;

            Console.WriteLine("Process found!");
            Trace.WriteLine("Process found!");
        }

        private void buttonHuntCounter_Click(object sender, RoutedEventArgs e)
        {
            if (isFF14Hooked)
            {
                try
                {
                    if (!isHuntThreadWorking)
                    {
                        huntThread = new Thread(new ThreadStart(HuntTrackThread));
                        huntThread.Start();
                    }

                    SearchNearbyMobs();
                }
                catch (Exception)
                {
                    // TODO: Is it work?
                    t.Start();
                    isFF14Hooked = false;
                }
            }
            else
            {
                Console.WriteLine("FF14 Cannot Found");
                Trace.WriteLine("FF14 Cannot Found");
            }
        }

        public void SearchNearbyMobs()
        {
            var actors = huntCounter.GetMobs();

            if (actors == null)
            {
                return;
            }

            nearbyCollection.Clear();
            listviewHuntCounter.Items.Clear();

            foreach (var actor in actors)
            {
                var mob = new Mob
                {
                    Name = actor.Value.Name,
                    Key = actor.Key,
                    HP = actor.Value.HPCurrent,
                    IsTracking = false,
                    Coordinates = new Coords(Utility.ConvertPos(actor.Value.X), Utility.ConvertPos(actor.Value.Y)),
                };

                nearbyCollection.Add(mob);
            }

            foreach (var mob in nearbyCollection)
            {
                string item = mob.Name;
                bool skip = false;
                item += "_" + mob.Coordinates.X.ToString("0.0") + "_" + mob.Coordinates.Y.ToString("0.0");

                foreach (var listitem in listviewHuntCounter.Items)
                {
                    if (listitem.ToString().Contains(mob.Name))
                    {
                        skip = true;
                    }
                }

                if (!skip)
                    listviewHuntCounter.Items.Add(item);
            }

            listviewHuntCounter_SetList();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isClosing = true;
        }

        // It will not be fired before SearchNearbyMobs()
        private void listviewHuntCounter_SetList()
        {
            ListView lv = listviewHuntCounter;

            trackedCollection.Clear();

            foreach (var item in lv.Items)
            {
                string mobName = item.ToString().Split("_")[0];

                if (!trackedCollection.Any(m => m.Name == mobName))
                {
                    trackedCollection.Add(new Mob(mobName, 0));
                }
            }
        }

        private void HuntTrackThread()
        {
            ObservableCollection<uint> diedBefore = new();

            isHuntThreadWorking = true;

            while(!isClosing)
            {
                var actors = huntCounter.GetMobs();

                if (actors == null)
                {
                    
                }
                else
                {
                    foreach (var actor in actors)
                    {
                        var mob = new Mob
                        {
                            Name = actor.Value.Name,
                            Key = actor.Key,
                            HP = actor.Value.HPCurrent,
                            IsTracking = false,
                            Coordinates = new Coords(Utility.ConvertPos(actor.Value.X), Utility.ConvertPos(actor.Value.Y)),
                        };

                        // Every spawnd mob has different key
                        if (mob.HP == 0 && !diedBefore.Contains(mob.Key))
                        {
                            diedBefore.Add(mob.Key);

                            foreach (var tc in trackedCollection)
                            {
                                if (mob.Name == tc.Name)
                                {
                                    tc.Count++;
                                }
                            }
                        
                            Trace.WriteLine("dead -> " + mob.Key.ToString());
                        }
                    }
                }

                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    listviewHuntCounterNumber.Items.Clear();

                    foreach (var tc in trackedCollection)
                    {
                        listviewHuntCounterNumber.Items.Add(tc.Count.ToString());
                    }

                }));

                Thread.Sleep(1000);
            }
        }
    }
}
