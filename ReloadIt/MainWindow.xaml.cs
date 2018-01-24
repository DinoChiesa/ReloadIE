using System;
using System.IO;                      // File, Directory
using System.Collections.Generic;     // List, Dictionary
using System.Collections.ObjectModel; // ObservableCollection
using System.ComponentModel;          // ListSortDirection
using System.Windows;
using System.Windows.Controls;        // SelectionChangedEventArgs, TextChangedEventArgs
using System.Windows.Input;           // KeyEventArgs
using System.Windows.Navigation;      // RequestNavigateEventArgs

namespace ReloadIt
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        class BrowserTab : System.ComponentModel.INotifyPropertyChanged
        {
            private bool wantReload;

            internal MainWindow Container       { get; set; }
            public SHDocVw.WebBrowser ComObject { get; set; }
            public String LocationUrl           { get; set; }
            public List<String> PathsToMonitor  { get; set; }
            public bool WantReload
            {
                get { return wantReload; }
                set
                {
                    wantReload = value;
                    NotifyPropertyChanged("WantReload");
                    NotifyPropertyChanged("IsMonitoring");
                }
            }

            public bool IsMonitoring
            {
                get { return wantReload && (Container != null) && Container.monitoring; }
            }

            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            internal void NotifyPropertyChanged(String info)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(info));
                }
            }
        }


        /// <summary>
        ///   Represents one FS Watcher, and its mapping to one or more browser tabs.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Each FileSystemWatcher works on a single directory, with an
        ///     optional filename mask or filter. It is mapped to one or more
        ///     BrowserTabs - in practice this means a change in a single
        ///     watched file will result in refreshing at least one browser
        ///     tab, but might result in refreshing multiple tabs.
        ///   </para>
        /// </remarks>
        class Watch
        {
            public DateTime LastChange { get; set; }
            public FileSystemWatcher Watcher { get; set; }
            public List<BrowserTab> Tabs { get; set; }
        }


        AppState appState;
        Dictionary<String,Watch> dirHash;
        Dictionary<String,String> rememberedPathErrors;
        BrowserTab current;
        bool monitoring;
        static System.DateTime beginningOfTime = new System.DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        System.TimeSpan ReasonableInterval = new TimeSpan(2800 * 10000 ); // 2.8s
        System.DateTime lastSearchChange;
        int handlingSearchEvent;
        readonly int DELAY_IN_MILLISECONDS = 700;
        ObservableCollection<BrowserTab> allBrowserTabs;
        System.ComponentModel.ICollectionView tabsCv;
        String LastUrlViewed;
        static string firstStatusText = "F12 begins monitoring, F5 to refresh list";
        bool warnedAboutHttpMonitoring;
        string versionString;

        public MainWindow()
        {
            InitializeComponent();

            this.appState = new AppState();
            allBrowserTabs = new ObservableCollection<BrowserTab>();
            AddInfo("Hello from ReloadIt :)");
            this.mainGrid.DataContext = appState;
            this.tabsCv = System.Windows.Data.CollectionViewSource.GetDefaultView(allBrowserTabs);
            this.listView1.ItemsSource = tabsCv;
            this.dockPanel2.Visibility = Visibility.Collapsed;
            this.lvMessages.ItemsSource = this.appState.MessagesCv;
            this.rememberedPathErrors = new Dictionary<String,String>();
            versionString = String.Format("v{0}",
                                      System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
            this.Title = "ReloadIt " + versionString;
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadIeTabs();
            this.status1.Text = firstStatusText;
            lastSearchChange = beginningOfTime;
        }


        private void LoadIeTabs()
        {
            SHDocVw.ShellWindows shellWindows = new SHDocVw.ShellWindows();

            current = this.listView1.SelectedItem as BrowserTab; // maybe null

            StoreConfigStateOfCurrentlyDisplayedUrls();

            this.allBrowserTabs.Clear();

            // Enumerate IE browser tabs.
            foreach (SHDocVw.WebBrowser ie in shellWindows)
            {
                try
                {
                    var filename = Path.GetFileNameWithoutExtension(ie.FullName).ToLower();
                    if (filename.Equals("iexplore"))
                    {
                        var url = ie.LocationURL.AbbrevUrl();
                        var bt = new BrowserTab
                        {
                            ComObject = ie,
                            LocationUrl = url
                        };
                        allBrowserTabs.Add(bt);

                        // restore state for this URL, if any
                        if (watchedUrlHistory.ContainsKey(url))
                        {
                            var cachedState = watchedUrlHistory[url];
                            bt.WantReload = cachedState.WantReload;
                            bt.PathsToMonitor = cachedState.PathsToMonitor;
                        }
                    }
                }
                catch (System.Runtime.InteropServices.COMException cexc1)
                {
                    // This can happen if IE is busy.
                    uint hresult = (uint)System.Runtime.InteropServices.Marshal.GetHRForException(cexc1);
                    var msg = String.Format("Loading a tab failed, exception: {0:X8}",
                                            hresult);
                    AddAlert(msg);
                }
            }

            // sort
            this.tabsCv.SortDescriptions.Add
                (new SortDescription("LocationUrl",
                                     ListSortDirection.Descending));

            ApplySearchString(false);

            // maybe select the prior item
            if (current == null || !SelectThisUrl(current.LocationUrl))
                // select the first url if none is selected
                if (this.listView1.Items.Count > 0 && this.listView1.SelectedItem == null)
                    this.listView1.SelectedIndex = 0;

            // remember current selection
            current = this.listView1.SelectedItem as BrowserTab;
        }


        // This is necessary to allow arrow navigation through the WPF
        // listview after it has been refreshed and one item has been
        // programmatically selected, as when selecting the item that was
        // previously highlighted before the refresh.  For full details, see
        // http://stackoverflow.com/questions/7363777/7364949#7364949
        void icg_StatusChanged(object sender, EventArgs e)
        {
            if (this.listView1.ItemContainerGenerator.Status
                == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                this.listView1.ItemContainerGenerator.StatusChanged
                    -= icg_StatusChanged;
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                                       new Action(()=> {
                                               var uielt = (UIElement)this.listView1.ItemContainerGenerator.ContainerFromItem(current);
                                               uielt.Focus();}));

            }
        }


        private bool SelectThisUrl(string url)
        {
            foreach (var item in this.tabsCv)
            {
                var bt = item as BrowserTab;
                if (bt.LocationUrl == url)
                {
                    this.listView1.ItemContainerGenerator.StatusChanged += icg_StatusChanged;
                    this.listView1.SelectedItem = bt;
                    current = bt;
                    return true;
                }
            }
            return false;
        }


        private void StoreConfigStateOfCurrentlyDisplayedUrls()
        {
            foreach (var bt in allBrowserTabs)
            {
                if (bt.PathsToMonitor != null && bt.PathsToMonitor.Count > 0)
                {
                    watchedUrlHistory.Store(bt.LocationUrl, bt);
                    bt.ComObject = null; // allow GC on this RCW
                }
            }
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCurrent();
            this.textBox1.Text = "";
            var bt = this.listView1.SelectedItem as BrowserTab;
            if (bt == null) return;
            current = bt;
            this.checkBox1.IsChecked = bt.WantReload;
            if (bt.PathsToMonitor != null && bt.PathsToMonitor.Count > 0)
                this.textBox1.Text = String.Join("\n", bt.PathsToMonitor.ToArray());
        }


        void UpdateCurrent()
        {
            if (current != null)
            {
                var text = this.textBox1.Text.Trim();
                if (!String.IsNullOrEmpty(text))
                {
                    var split = text.Split(new char[] { '\n', '\r' },
                                           StringSplitOptions.RemoveEmptyEntries);
                    var invalid = false;
                    foreach (var path in split)
                    {
                        if (path.StartsWith("http:") || path.StartsWith("https:"))
                        {
                            invalid = true;
                        }
                    }
                    current.PathsToMonitor = new List<string>(split);
                    if (current.PathsToMonitor != null && current.PathsToMonitor.Count > 0)
                        watchedUrlHistory.Store(current.LocationUrl, current);

                    if (invalid)
                    {
                        current.WantReload = false; // force
                        var warnedForThisUrl = rememberedPathErrors.ContainsKey(current.LocationUrl);
                        var wantWarning = !warnedForThisUrl;
                        if (warnedForThisUrl)
                        {
                            var storedValue = rememberedPathErrors[current.LocationUrl];
                            wantWarning = (storedValue != text);
                        }
                        if (wantWarning)
                        {
                            rememberedPathErrors[current.LocationUrl] = text;
                            var msg = String.Format("Error: for URL {0}, the path you provided is invalid.",
                                                    current.LocationUrl);
                            appState.Messages.AddAlert(msg);
                            if (!warnedAboutHttpMonitoring)
                            {
                                msg = "Notice: ReloadIt does not monitor http URLs for changes." +
                                    " You can monitor only filesystem paths.";
                                appState.Messages.AddAlert(msg);
                                warnedAboutHttpMonitoring = true;
                            }
                        }
                    }
                    else
                    {
                        rememberedPathErrors.Remove(current.LocationUrl);
                        current.WantReload = this.checkBox1.IsChecked.Value;
                    }
                }
                else
                {
                    current.PathsToMonitor = null;
                    current.WantReload = false;
                    watchedUrlHistory.Remove(current.LocationUrl); // no-op if not there.
                }
            }
        }


        private void checkBox1_CheckedChanged(object sender, RoutedEventArgs e)
        {
            var bt = this.listView1.SelectedItem as BrowserTab;
            if (bt == null) return;
            bt.WantReload = this.checkBox1.IsChecked.Value;
        }

        int CountWatched()
        {
            if (dirHash == null) return 0;
            return dirHash.Keys.Count;
        }


        private void RefreshAll()
        {
            if (monitoring)
                StopWatching();
            else
                UpdateCurrent();

            LoadIeTabs();

            if (monitoring)
                BeginWatching();
            else
                this.status1.Text = firstStatusText;
        }


        private void ToggleMonitoring()
        {
            monitoring = !monitoring;
            if (monitoring)
            {
                UpdateCurrent();
                BeginWatching();
            }
            else
            {
                StopWatching();
            }
        }


        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F5:
                    RefreshAll();
                    break;

                case Key.F12:
                    ToggleMonitoring();
                    break;
            }
        }

        private void StopWatching()
        {
            if (dirHash == null) return;
            foreach (var tab in allBrowserTabs)
            {
                tab.NotifyPropertyChanged("IsMonitoring");
            }

            foreach (var path in dirHash.Keys)
            {
                StopWatchingOne(dirHash[path]);
            }
            dirHash = null;

            this.status1.Text = firstStatusText;
            this.Title = "ReloadIt " + versionString;
        }

        private void StopWatchingOne(Watch w)
        {
            if (w == null) return;
            if (w.Watcher == null) return;
            w.Watcher.EnableRaisingEvents = false;
            w.Watcher = null;
        }



        private void AddAlert(string msg)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                // a thread other than the UI thread
                this.Dispatcher.BeginInvoke(new Action(()=> { AddAlert(msg);}));
            }
            else
            {
                appState.Messages.AddAlert(msg);
            }
        }

        private void AddInfo(string msg)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                // a thread other than the UI thread
                this.Dispatcher.BeginInvoke(new Action(()=> { AddInfo(msg);}));
            }
            else
            {
                appState.Messages.AddInfo(msg);
            }
        }


        private const int ReloadRetryLimit = 3;
        private void ReloadOneTab(Object arg)
        {
            BrowserTab tab = arg as BrowserTab;
            int retryLevel = 0;
            if (tab == null)
            {
                Object[] a = arg as Object[];
                tab = a[0] as BrowserTab;
                retryLevel = (int) a[1] ;
            }

            try
            {
                tab.ComObject.Refresh2(RefreshLevel.Completely);
            }
            catch (System.Runtime.InteropServices.COMException cexc1)
            {
                // This can happen if IE cannot refresh. For instance if
                // there's been a Javascript exception and IE is prompting
                // the user to debug the page, with a modal prompt. It can
                // also happen if IE is busy, already refreshing and
                // waiting for a response.

                uint hresult = (uint)System.Runtime.InteropServices.Marshal.GetHRForException(cexc1);
                // sleep and retry here, if IE is busy.
                // HRESULT: 0x8001010A (RPC_E_SERVERCALL_RETRYLATER)
                if (hresult == 0x8001010AU)
                {
                    var msg = String.Format("Refreshing {0} failed, exception: {1:8X}, {2}",
                                            tab.LocationUrl, hresult,
                                            (retryLevel < ReloadRetryLimit) ? "will retry" : "giving up");
                    AddAlert(msg);

                    if (retryLevel < ReloadRetryLimit)
                    {
                        var waitAndRetry = new System.Threading.WaitCallback( obj => {
                                System.Threading.Thread.Sleep(1000); // in ms
                                Object[] args = { obj, retryLevel+1 };
                                ReloadOneTab(args);
                            });
                        System.Threading.ThreadPool.QueueUserWorkItem(waitAndRetry, tab);
                    }
                }
                else
                {
                    var msg = String.Format("Refreshing {0} failed: exception: {1:8X}",
                                            tab.LocationUrl, hresult);
                    AddAlert(msg);
                }
            }
        }


        private void FileSystemChanged(Watch w)
        {
            // One "file save" action in the OS can result in many filesystem
            // change events as detected by the watcher. This logic ensures only one
            // refresh will occur in a given brief interval.
            var now = DateTime.UtcNow;
            if (now - w.LastChange > ReasonableInterval)
            {
                foreach (var tab in w.Tabs)
                {
                    appState.NumReloads++;
                    ReloadOneTab(tab);
                    NoteReload(tab);
                }
                w.LastChange = now;
            }
        }


        private void BeginWatchingOne(string path)
        {
            String filter = null, dir = null;
            if (Directory.Exists(path))
            {
                dir = path;
            }
            else
            {
                // split
                filter = Path.GetFileName(path);
                dir = Path.GetDirectoryName(path);
                if (!File.Exists(path) && filter.IndexOf('*') <0)
                    return;
            }

            if (!dirHash.ContainsKey(path)) return;

            Watch w = dirHash[path];

            var handler1 = new FileSystemEventHandler((source, e) =>
                { FileSystemChanged(w); });

            // var handler2 = new RenamedEventHandler((source, e) =>
            //     { FileSystemChanged(w); });

            w.Watcher = new FileSystemWatcher();
            w.Watcher.Path = dir;
            w.Watcher.NotifyFilter =
                NotifyFilters.LastAccess
                | NotifyFilters.LastWrite
                | NotifyFilters.FileName
                //| NotifyFilters.DirectoryName
                ;

            w.Watcher.Filter = filter ?? "*.*";
            if (filter == null)
                w.Watcher.IncludeSubdirectories = true;
            w.Watcher.Changed += handler1;
            w.Watcher.Created += handler1;
            w.Watcher.Deleted += handler1;
            w.Watcher.Renamed += (source, e) => FileSystemChanged(w);

            w.Watcher.EnableRaisingEvents = true;
        }


        private bool SpecIsOk(String path)
        {
            if (Directory.Exists(path)) return true;
            if (File.Exists(path)) return true;
            // split
            String filter = Path.GetFileName(path);
            String dir = Path.GetDirectoryName(path);
            if (filter.IndexOf('*') >= 0)
                return true;
            return false;
        }


        private void BeginWatching()
        {
            StopWatching();

            // There will be at most one watcher per directory.  Multiple IE
            // tabs may be updated per watcher, if the user configures a single
            // directory as "a watched directory" for multiple webapps. But
            // there's just one filesystemwatcher object.  When it fires, all
            // tagged IE Tabs will get updated.
            dirHash = new Dictionary<String,Watch>();

            foreach (var bt in allBrowserTabs)
            {
                if (bt.WantReload)
                {
                    if (bt.PathsToMonitor == null) continue;
                    watchedUrlHistory.Store(bt.LocationUrl,bt);
                    foreach (var path in bt.PathsToMonitor)
                    {
                        if (SpecIsOk(path))
                        {
                            Watch watch = null;
                            if (!dirHash.ContainsKey(path))
                            {
                                watch = new Watch();
                                watch.LastChange = beginningOfTime;
                                watch.Tabs = new List<BrowserTab>();
                                dirHash.Add(path, watch);
                            }
                            else
                            {
                                watch = dirHash[path];
                            }
                            watch.Tabs.Add(bt);
                        }
                        else
                        {
                            appState.Messages.AddAlert(String.Format("Path does not exist: {0}", path));
                        }
                    }

                    bt.Container = this;
                    bt.NotifyPropertyChanged("IsMonitoring");
                }
            }


            foreach (var path in dirHash.Keys)
            {
                BeginWatchingOne(path);
            }

            int n = CountWatched();
            if (n == 0)
            {
                monitoring = false;
                StopWatching();
                this.status1.Text = String.Format("Nothing to watch!  checkbox? dir exists?...F12 to start", n);
            }
            else
            {
                this.status1.Text = String.Format("Monitoring {0} paths...F12 to stop", n);
                this.Title = String.Format("ReloadIt {0}: Monitoring {1} paths", versionString, n);
            }
        }


        private void NoteReload(BrowserTab tab)
        {
            // Not sure I need to do this BeginInvoke stuff - just
            // updating the list does not update the UI.
            if (!this.Dispatcher.CheckAccess())
            {
                // a thread other than the UI thread
                this.Dispatcher.BeginInvoke(new Action(()=> { NoteReload(tab);}));
            }
            else
            {
                // var msg = String.Format("Reload {0}", tab.LocationUrl);
                // this.appState.Messages.AddInfo(msg);
            }
        }



        void BringToFore(SHDocVw.WebBrowser ietab)
        {
            try
            {
                IntPtr hwndParent = (IntPtr) ietab.HWND;
                if (IntPtr.Zero.Equals(hwndParent)) return;

                // Step 1: bring parent window to the front, or restore it.
                if (Win32.IsIconic(hwndParent))
                {
                    // the window is currently minimized
                    Win32.ShowWindow(hwndParent, Constants.SW_RESTORE);
                }

                // It is now, or was already, maximzed or restored.  Honestly
                // I don't know if I always need all of these next three
                // calls, but I also don't know how to test all the scenarios
                // to find out if I need them all.
                Win32.BringWindowToTop(hwndParent);
                Win32.SetForegroundWindow(hwndParent);
                Win32.SetActiveWindow(hwndParent);

                // Step 2: activate the particular tab within the window
                Win32.ActivateTab(ietab);
            }
            catch (System.Exception exc1)
            {
                var msg = String.Format("Exception bringing tab forward: {0}", exc1);
                appState.Messages.AddAlert(msg);
            }
        }

        private BrowserTab GetSelectedTab()
        {
            if (this.listView1.Items == null ||
                this.listView1.Items.Count <= 0 ||
                this.listView1.SelectedItem == null) return null;

            return this.listView1.SelectedItem as BrowserTab;
        }

        void bringToFront_click(object sender, RoutedEventArgs e)
        {
            var tab = GetSelectedTab();
            if (tab == null) return;
            BringToFore(tab.ComObject);
        }

        void refreshtab_click(object sender, RoutedEventArgs e)
        {
            var tab = GetSelectedTab();
            if (tab == null) return;
            ReloadOneTab(tab);
        }

        void closetab_click(object sender, RoutedEventArgs e)
        {
            var tab = GetSelectedTab();
            if (tab == null) return;

            tab.ComObject.Quit();
            int ix = this.listView1.SelectedIndex;

            allBrowserTabs.Remove(tab);

            // select the index closest to the one we removed:
            if (this.listView1.Items == null) return;
            if (this.listView1.Items.Count == 0) return;

            while (ix >= this.listView1.Items.Count) ix--;
            this.listView1.SelectedIndex = ix;
        }


        void btnMessages_click(object sender, RoutedEventArgs e)
        {
            var v = this.dockPanel2.Visibility;
            this.dockPanel2.Visibility = (v == Visibility.Visible)
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (this.dockPanel2.Visibility != Visibility.Visible)
                this.appState.AckAlerts();
        }


        private void MaybeDoSearch(Object ignored)
        {
            handlingSearchEvent++;
            System.Threading.Thread.Sleep(DELAY_IN_MILLISECONDS);
            DateTime now = DateTime.UtcNow;
            var delta = now - lastSearchChange;
            if (delta < new System.TimeSpan(0, 0, 0, 0, DELAY_IN_MILLISECONDS))
            {
                handlingSearchEvent--;
                return;  // still typing
            }

            // a thread other than the UI thread
            this.Dispatcher.BeginInvoke(new Action(()=> { ApplySearchString(true);}));
        }


        private void ApplySearchString(bool wantDecrement)
        {
            // actually apply the search and maybe select an item
            var searchString = this.textBox0.Text;
            if (String.IsNullOrEmpty(searchString))
            {
                this.tabsCv.Filter = null; // remove filtering
                if (LastUrlViewed != null)
                {
                    SelectThisUrl(LastUrlViewed);
                    LastUrlViewed = null; // do this once only, upon app start
                }
                if (wantDecrement) handlingSearchEvent--;
                return;
            }

            // Arriving here means there is a search string.
            // Apply a filter for it.
            this.tabsCv.Filter = item => ((BrowserTab)item).LocationUrl.IndexOf(searchString) >= 0;
            if (wantDecrement) handlingSearchEvent--;
        }


        private void textBox0_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (handlingSearchEvent > 0) return; // already looking
            lastSearchChange = System.DateTime.Now;

            System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(MaybeDoSearch));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateCurrent();
            StopWatching();
            SaveUserPrefs();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            LoadAndApplyUserPrefs();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }

    }
}
