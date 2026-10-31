using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
        public static bool isDisplayChecked = false;
        public static bool isScoutMode = false;
        public static string currentMap = "DT_UR";
        public static ObservableCollection<Mob> nearbyCollection = new();
        public static ObservableCollection<Mob> trackedCollection = new();

        public static Queue<uint> diedBefore = new();

        Thread t;
        Thread huntThread;

        public HuntCounter huntCounter;
        private PopupDisplay DisplayWindow;

        private DispatcherTimer playerTimer;

        public MainWindow()
        {
            InitializeComponent();

            // Default map (Urqopacha)
            imageMap.Source = Utility.ByteToImage(Properties.Resources.Urqopacha_data);
            imageMap.Stretch = Stretch.Fill;

            huntCounter = new HuntCounter();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ParseLog();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            UpdateCanvas();

            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(new HwndSourceHook(WndProc));

            t = new Thread(new ThreadStart(SetupHuntCounter));
            t.Start();
        }

        // Parse objList and last UID from CONFIG.LOGFILE
        // Log format : uid,map,x,y,timestamp,marktype[,instance]
        private void ParseLog()
        {
            if (!System.IO.File.Exists(CONFIG.LOGFILE))
            {
                System.IO.File.Create(CONFIG.LOGFILE).Dispose();
                return;
            }

            var saved = System.IO.File.ReadAllLines(CONFIG.LOGFILE);

            foreach (string saves in saved)
            {

                string[] contents = saves.Split(",");

                if (contents.Length < 6 || !int.TryParse(contents[0], out int value))
                {
                    continue;
                }

                UID = Math.Max(UID, value + 1);

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

            if (treeview.SelectedItem is not TreeViewItem tvi)
                return;

            // Original positions of drawn dots, for overlap check
            List<Point> drawn = new();
            // Instance labels are added after all dots so they stay on top of overlapped dots
            List<TextBlock> labels = new();

            for (int i = 0; i < objList.Count; i++)
            {
                string saves = objList[i];
                string[] contents = saves.Split(",");

                if (contents[1] != tvi.Name)
                    continue;

                double xx = double.Parse(contents[2]);
                double yy = double.Parse(contents[3]);
                string textbox = contents[4];
                string markcur = contents[5];
                uint instance = GetLogInstance(contents);

                double xActual = canvas.ActualWidth;
                double yActual = canvas.ActualHeight;

                double xr = (xActual * xx / 100.0) - (CONFIG.PARAM_DOT_SIZE / 2.0);
                double yr = (yActual * yy / 100.0) - (CONFIG.PARAM_DOT_SIZE / 2.0);

                // Overlapped dots are shifted right by one dot, timestamp goes one line down
                Point origin = new(xr, yr);
                int overlapped = drawn.Count(p => (p - origin).Length < CONFIG.PARAM_DOT_SIZE);
                drawn.Add(origin);

                xr += overlapped * CONFIG.PARAM_DOT_SIZE;
                double yText = yr + overlapped * (CONFIG.PARAM_FONT_SIZE + 4.0);

                Ellipse dot = new();

                if (markcur == "u")
                {
                    dot = Utility.MakeDot(xr, yr, CONFIG.PARAM_DOT_SIZE, DotType.Flag, markcur);
                }
                else
                {
                    dot = Utility.MakeDot(xr, yr, CONFIG.PARAM_DOT_SIZE, DotType.Circle, markcur);
                }

                TextBlock tbx = Utility.MakeTextblock(xr, yText, CONFIG.PARAM_FONT_SIZE, textbox);

                dot.Name = CONFIG.OBJECT_PREFIX + contents[0];
                tbx.Name = CONFIG.OBJECT_PREFIX + contents[0];

                dot.MouseRightButtonUp += Dot_MouseRightButtonUp;

                canvas.Children.Add(dot);
                canvas.Children.Add(tbx);

                if (instance > 0 && markcur != "u")
                    labels.Add(Utility.MakeInstanceLabel(xr, yr, CONFIG.PARAM_DOT_SIZE, instance));
            }

            foreach (TextBlock label in labels)
                canvas.Children.Add(label);
        }

        // 7th column of log, 0 if old log or not instanced
        private static uint GetLogInstance(string[] contents)
        {
            if (contents.Length > 6 && uint.TryParse(contents[6], out uint instance))
                return instance;

            return 0;
        }

        // Instance selected in combobox (0 = none)
        private uint SelectedInstance => (uint)Math.Max(comboInstance.SelectedIndex, 0);

        private void treeview_CloseAll()
        {
            DT_Head.IsExpanded = false;
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

        #region Swap map code spam
        private void treeview_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem tvItem = (TreeViewItem)e.NewValue;

            SetCanvasMap(tvItem.Name);

            return;
        }

        private void SetCanvasMap(string mapName)
        {
            bool isHead = false;

            // Dirty but easy
            switch (mapName)
            {
                case "DT_Head":
                    treeview_CloseAll();
                    DT_Head.IsExpanded = true;
                    isHead = true;
                    break;
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

                case "DT_UR":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Urqopacha_data); break;
                case "DT_KO":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Kozama_uka_data); break;
                case "DT_YT":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Yak_Te_l_data); break;
                case "DT_SH":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Shaaloani_data); break;
                case "DT_HF":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Heritage_Found_data); break;
                case "DT_LM":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Living_Memory_data); break;

                case "EW_LA":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Labyrinthos_data); break;
                case "EW_GA":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Garlemald_data); break;
                case "EW_TH":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Thavnair_data); break;
                case "EW_MA":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Mare_Lamentorum_data); break;
                case "EW_EL":
                    imageMap.Source = Utility.ByteToImage(Properties.Resources.Elpis_data); break;
                case "EW_UL":
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
            {
                UpdateCanvas();
                UpdatePlayer();
            }
        }
        #endregion

        // Left click for Mark
        private void canvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(canvas);

            Mark(pos.X, pos.Y, String.Empty, false, SelectedInstance);

            return;
        }

        // X mark + Timestamp, also make log for each map
        private void Mark(double X, double Y, string customize, bool logskip, uint instance)
        {
            double x = X;
            double y = Y;

            var timestamp = DateTime.Now.ToString(CONFIG.TIMESTAMP_FORMAT);

            // Create information for save
            string currentCanvasMap = Utility.GetCurrentMap();
            string xx = (x / canvas.ActualWidth * 100.0).ToString("0.0");
            string yy = (y / canvas.ActualHeight * 100.0).ToString("0.0");
            string marktype = markCurrent;

            // is from Scout mode
            if (customize == "s" || customize == "a" || customize == "b" || customize == "u")
            {
                marktype = customize;

                SelectMap(currentMap);

                currentCanvasMap = currentMap;

            }

            // logskip used in SearchNearbyMobs() for duplicate check
            // No need to log again if already marked (in condition)
            if (!logskip)
            {
                string log = UID.ToString() + "," + currentCanvasMap + "," + xx + "," + yy + "," + timestamp + "," + marktype + "," + instance.ToString();
                objList.Add(log);
                log += Environment.NewLine;
                System.IO.File.AppendAllText(CONFIG.LOGFILE, log);
            }

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
            string objUID = dot.Name.Split("_")[1];
            string map = Utility.GetCurrentMap();

            Func<string[], bool> isTarget = c => c.Length > 1 && c[0] == objUID && c[1] == map;

            Utility.RemoveLogWhere(isTarget);
            objList.RemoveAll(l => isTarget(l.Split(",")));

            // Redraw, overlapped dots may move back
            UpdateCanvas();

            return;
        }

        // Right click, Not really need atm
        private void canvas_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            return;
        }

        // Clear current button means remove all canvas children (of current map)
        private void buttonClear_Click(object sender, RoutedEventArgs e)
        {
            string map = Utility.GetCurrentMap();
            Func<string[], bool> isTarget = c => c.Length > 1 && c[1] == map;

            // Rebuild objList
            objList.RemoveAll(l => isTarget(l.Split(",")));

            // Rebuild log file
            Utility.RemoveLogWhere(isTarget);

            UpdateCanvas();

            return;
        }

        // ClearAll button means clear all
        private void buttonClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Clear All Markers?", "Question",
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

        // Setting current type of Dot
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
            else if (radioButtonAE.IsChecked is true)
            {
                markCurrent = "u";
            }
        }

        // Redraw when window size change finished
        private const int WM_SIZING = 0x214;
        private const int WM_EXITSIZEMOVE = 0x232;
        private static bool WindowWasResized = false;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SIZING)
            {
                if (WindowWasResized == false)
                {
                    WindowWasResized = true;
                }
            }

            if (msg == WM_EXITSIZEMOVE)
            {
                if (WindowWasResized)
                {
                    UpdateCanvas();
                    WindowWasResized = false;
                }
            }

            return IntPtr.Zero;
        }

        // Search process
        // Try find FF14 5 times (15 seconds)
        private void SetupHuntCounter()
        {
            bool processExists = false;
            short retryCounter = 0;

            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
            {
                buttonHuntCounter.IsEnabled = false;
                buttonHuntCounterAdd.IsEnabled = false;
                checkboxCounterDisplay.IsEnabled = false;
                checkboxScoutMode.IsEnabled = false;
                this.Title = "FFXIVHuntMarker (Searching FF14...)";
            }));

            while (!processExists)
            {
                if (isClosing)
                    return;

                if (retryCounter > 5)
                    break;

                processExists = huntCounter.Setup();
                retryCounter++;

                if (!processExists)
                    Thread.Sleep(3000);
            }

            if (retryCounter > 5)
            {
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    this.Title = "FFXIVHuntMarker (Standalone Mode)";
                }));
            }
            else
            {
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    buttonHuntCounter.IsEnabled = true;
                    buttonHuntCounterAdd.IsEnabled = true;
                    checkboxCounterDisplay.IsEnabled = false;
                    checkboxScoutMode.IsEnabled = false;
                    this.Title = "FFXIVHuntMarker";

                    // Player position on map
                    if (playerTimer == null)
                    {
                        playerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CONFIG.PARAM_PLAYER_INTERVAL_MS) };
                        playerTimer.Tick += (s, e) => UpdatePlayer();
                    }
                    playerTimer.Start();
                }));

                isFF14Hooked = true;
            }
        }

        // Hunt Counter button, start searching thread
        private void buttonHuntCounter_Click(object sender, RoutedEventArgs e)
        {
            bool append = false;
            if (sender != null)
            {
                if ((sender as Button).Name == buttonHuntCounterAdd.Name)
                    append = true;
            }

            if (isFF14Hooked)
            {
                try
                {
                    if (!isHuntThreadWorking)
                    {
                        isHuntThreadWorking = true;
                        huntThread = new Thread(new ThreadStart(HuntTrackThread));
                        huntThread.Start();

                        Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                        {
                            checkboxCounterDisplay.IsEnabled = true;
                            checkboxScoutMode.IsEnabled = true;
                        }));
                    }

                    SearchNearbyMobs(append);
                }
                catch (Exception)
                {
                    isFF14Hooked = false;
                    isHuntThreadWorking = false;

                    t = new Thread(new ThreadStart(SetupHuntCounter));
                    t.Start();
                }
            }
            else
            {
                Console.WriteLine("FF14 Not Found");
                Trace.WriteLine("FF14 Not Found");
            }
        }

        // Select map on treeview, SetCanvasMap() follows by treeview_SelectedItemChanged
        private void SelectMap(string mapName)
        {
            if (FindName(mapName) is not TreeViewItem item || item.IsSelected)
                return;

            treeview_CloseAll();

            for (TreeViewItem parent = item.Parent as TreeViewItem; parent != null; parent = parent.Parent as TreeViewItem)
                parent.IsExpanded = true;

            item.IsSelected = true;
        }

        // Gather nearby Mobs and add to list
        public void SearchNearbyMobs(bool append)
        {
            // Get current map
            (uint mapID, uint mapIndex, uint mapTerritory) = huntCounter.GetMap();
            string mapcur = Utility.GetCurrentFF14Map(mapTerritory, mapIndex);
            uint instance = huntCounter.GetInstance();

            // Follow game map even if no S/A/B found
            if (mapcur != null)
            {
                currentMap = mapcur;
                SelectMap(mapcur);
                comboInstance.SelectedIndex = (int)Math.Min(instance, (uint)(comboInstance.Items.Count - 1));
            }

            // null while loading, previous zone mobs are dropped in GetMobs()
            var actors = huntCounter.GetMobs();

            if (actors == null)
            {
                return;
            }

            // Keep collection if append is true
            if (!append)
            {
                nearbyCollection.Clear();
                listviewHuntCounter.Items.Clear();
                listviewHuntCounterNumber.Items.Clear();
                trackedCollection.Clear();
            }
            else
            {
                nearbyCollection.Clear();
            }

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
                double percentage = mob.HPPercent;

                // Percentage on map
                double x1 = mob.Coordinates.X / CONFIG.FF14_MAP_SIZE;
                double y1 = mob.Coordinates.Y / CONFIG.FF14_MAP_SIZE;

                // Check already listed Mob
                foreach (ListViewItem listitem in listviewHuntCounter.Items)
                {
                    if (listitem is not null)
                    {
                        if (listitem.Content.ToString().Split('_')[0] == mob.Name)
                        {
                            skip = true;
                        }
                    }
                }

                // Add to list if new found Mob
                if (!skip)
                {
                    // If S/A/B ranked -> List and Add Mark+Log ONCE
                    // Else -> List only
                    string special = Utility.CheckSpecialMob(mob.Name);

                    // Mark only on known field map
                    if (special != "0" && mapcur != null)
                    {
                        bool occupied = false;
                        ListViewItem lvitem = new ListViewItem();
                        lvitem.Content = item;
                        lvitem.FontWeight = FontWeights.Bold;
                        lvitem.Background = CONFIG.COLOR_TEXT_BG;
                        if (special == "s")
                            lvitem.Foreground = CONFIG.COLOR_S_TEXT;
                        else if (special == "a")
                            lvitem.Foreground = CONFIG.COLOR_A_TEXT;
                        else if (special == "b")
                            lvitem.Foreground = CONFIG.COLOR_B_TEXT;
                        listviewHuntCounter.Items.Add(lvitem);

                        // skip if nearby Dot there (duplicate check)
                        for (int i = 0; i < objList.Count; i++)
                        {
                            string saves = objList[i];
                            string[] contents = saves.Split(",");
                            string mm = contents[1]; // Map

                            if (mm != mapcur || GetLogInstance(contents) != instance)
                                continue;

                            double dtl = Math.Sqrt(
                                Math.Pow(double.Parse(contents[2]) - (x1 * 100.0), 2) + 
                                Math.Pow(double.Parse(contents[3]) - (y1 * 100.0), 2)
                                );

                            DateTime dt1 = DateTime.ParseExact(contents[4], CONFIG.TIMESTAMP_FORMAT, null);
                            int dtd = Math.Abs((DateTime.Now - dt1).Hours);

                            // Check distance of current mob object and xy on objList AND
                            // difference between objList and DateTime.Now
                            if (dtl < CONFIG.PARAM_DUPLICATE_DISTANCE && 
                                dtd < CONFIG.PARAM_DUPLICATE_PERIOD_HOUR)
                            {
                                occupied = true;
                            }
                        }

                        Mark(x1 * canvas.ActualWidth,
                                y1 * canvas.ActualHeight,
                                special, occupied, instance);
                    }
                    else
                    {
                        ListViewItem lvitem = new ListViewItem();
                        lvitem.Content = item;
                        listviewHuntCounter.Items.Add(lvitem);
                    }
                }
            }

            listviewHuntCounter_SetList(append);

            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
            {
                buttonHuntCounter.Content = "Reload";
            }));
        }

        // Exit signal
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isClosing = true;
            isScoutMode = false;

            if (DisplayWindow != null)
                DisplayWindow.Close();
        }

        // Populate list with nearby mobs
        // It will not be fired before SearchNearbyMobs()
        private void listviewHuntCounter_SetList(bool append)
        {
            ListView lv = listviewHuntCounter;

            foreach (ListViewItem item in lv.Items)
            {
                string mobName = item.Content.ToString().Split("_")[0];

                if (!trackedCollection.Any(m => m.Name == mobName))
                {
                    trackedCollection.Add(new Mob(mobName, 0));
                }
            }
        }

        // Tracking thread
        private void HuntTrackThread()
        {
            diedBefore.Clear();

            while (!isClosing)
            {
                var actors = huntCounter.GetMobs();

                if (actors == null)
                {
                    // pass
                }
                else if (isScoutMode)
                {
                    Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                    {
                        SearchNearbyMobs(false);
                    }));
                }
                else
                {
                    foreach (var actor in actors)
                    {
                        // Every spawnd mob has different key
                        if (actor.Value.HPCurrent <= 0 && !diedBefore.Contains(actor.Key))
                        {
                            diedBefore.Enqueue(actor.Key);

                            foreach (var tc in trackedCollection)
                            {
                                // KEKW
                                if (actor.Value.Name == tc.Name ||
                                    ("System.Windows.Controls.ListViewItem: " + actor.Value.Name == tc.Name))
                                {
                                    tc.Count++;
                                }
                            }
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

        // Show Hunt Counter display
        private void checkboxCounterDisplay_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as CheckBox).IsChecked == true)
                isDisplayChecked = true;
            else
                isDisplayChecked = false;

            if (DisplayWindow == null && isDisplayChecked)
            {
                DisplayWindow = new PopupDisplay();
                DisplayWindow.Closed += (a, b) =>
                {
                    checkboxCounterDisplay.IsChecked = false;
                    isDisplayChecked = false;
                    DisplayWindow = null;
                };
                DisplayWindow.Show();
            }
            else
            {
                DisplayWindow.Close();
            }
        }

        // Scout mode, flow control in HuntTrackThread()
        private void checkboxScoutMode_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as CheckBox).IsChecked == true)
            {
                isScoutMode = true;
            }
            else
            {
                isScoutMode = false;
            }

            return;
        }

        // Update canvas when windows size changed
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCanvas();
        }

        // Remove right-clicked item
        private void listviewHuntCounter_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ListView lv = listviewHuntCounter;
            ListView lvc = sender as ListView;
            var item = lvc.SelectedItem;

            if (item is null)
            {
                return;
            }

            string selname = item.ToString().Split('_')[0];

            for (int i = 0; i < lvc.Items.Count; i++)
            {
                if (lv.Items[i].ToString().Split('_')[0] == selname)
                {
                    trackedCollection.RemoveAt(i);
                    lv.Items.RemoveAt(i);
                }
            }
        }

        private void sliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Needs AllowsTransparency="True" on window, otherwise WPF ignores it
            Opacity = e.NewValue;
        }

        private void buttonMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void buttonMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void sliderOpacity_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            sliderOpacity.Value = 1.0;
        }

        private void checkboxTopmost_Click(object sender, RoutedEventArgs e)
        {
            Topmost = checkboxTopmost.IsChecked == true;
        }

        // Draw player ▲ on map, rotated to facing direction
        private void UpdatePlayer()
        {
            if (!isFF14Hooked || playerMarker == null)
                return;

            var player = huntCounter.GetUser();
            (uint mapID, uint mapIndex, uint mapTerritory) = huntCounter.GetMap();
            string map = Utility.GetCurrentFF14Map(mapTerritory, mapIndex);

            // Show only when viewing the map player is in
            if (player == null || map == null || treeview.SelectedItem is not TreeViewItem tvi || tvi.Name != map)
            {
                playerMarker.Visibility = Visibility.Collapsed;
                return;
            }

            double x = Utility.ConvertPos(player.X) / CONFIG.FF14_MAP_SIZE * canvasPlayer.ActualWidth;
            double y = Utility.ConvertPos(player.Y) / CONFIG.FF14_MAP_SIZE * canvasPlayer.ActualHeight;

            Canvas.SetLeft(playerMarker, x - playerMarker.Width / 2.0);
            Canvas.SetTop(playerMarker, y - playerMarker.Height / 2.0);

            // Heading is radian, 0 = south, counter-clockwise. ▲ points north at 0 degree.
            playerRotate.Angle = 180.0 - player.Heading * 180.0 / Math.PI;
            playerMarker.Visibility = Visibility.Visible;
        }
    }
}
