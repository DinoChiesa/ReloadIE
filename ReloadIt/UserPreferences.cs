using System;
using System.ComponentModel;

namespace ReloadIt
{
    public partial class MainWindow
    {
        private Microsoft.Win32.RegistryKey _appCuKey;
        private static string _AppRegyPath     = "Software\\Dino Chiesa\\ReloadIt";
        private static string _rvn_Geometry    = "Geometry";
        private static string _rvn_Runs        = "Runs";
        private static string _rvn_LastUrl     = "LastUrl";
        private static string _rvn_WantReloads = "WantReloads";
        private static string _rvn_ReloadIntervalInMs = "ReloadIntervalInMs";
        MruDictionary<String,BrowserTab> watchedUrlHistory;
        private const int HISTORY_ENTRIES = 15;

        private void LoadAndApplyUserPrefs()
        {
            LoadUserPrefs();
            SizeToFit();
            MoveIntoView();
            LoadUrlHistory();
        }

        private void SaveUserPrefs()
        {
            double leftColWidth = this.innerGrid.ColumnDefinitions[0].ActualWidth;
            AppCuKey.SetValue(_rvn_Geometry,
                              String.Format("{0},{1},{2},{3},{4},{5}",
                                            this.Left,
                                            this.Top,
                                            this.Width,
                                            this.Height,
                                            leftColWidth,
                                            (int)this.WindowState));

            int x = (Int32)AppCuKey.GetValue(_rvn_Runs, 0);
            x++;
            AppCuKey.SetValue(_rvn_Runs, x);

            var bt = this.listView1.SelectedItem as BrowserTab;

            if (bt != null)
                AppCuKey.SetValue(_rvn_LastUrl, bt.LocationUrl);

            StoreUrlHistory();
        }


        private void LoadUserPrefs()
        {
            string s = (string)AppCuKey.GetValue(_rvn_Geometry);
            if (!String.IsNullOrEmpty(s))
            {
                double[] p = Array.ConvertAll<string, double>(s.Split(','),
                                                             new Converter<string, double>((t) => { return Double.Parse(t); }));
                if (p != null && p.Length == 5)
                {
                    Left = p[0];
                    Top = p[1];
                    Width = p[2];
                    Height = p[3];
                    WindowState = (System.Windows.WindowState)((int)p[4]);
                }
                else if (p != null && p.Length == 6)
                {
                    Left = p[0];
                    Top = p[1];
                    Width = p[2];
                    Height = p[3];
                    var lcWidth = p[4];
                    WindowState = (System.Windows.WindowState)((int)p[5]);

                    this.innerGrid.ColumnDefinitions[0].Width =
                        new System.Windows.GridLength(lcWidth);
                }
            }

            LastUrlViewed = (string) AppCuKey.GetValue(_rvn_LastUrl);

            int x = (Int32)AppCuKey.GetValue(_rvn_ReloadIntervalInMs, 0);
            if ((x != 0) && (x > 1000) && (x < 180*1000))
            {
                ReasonableInterval = new TimeSpan(x * 10000);
            }
            else
            {
                ReasonableInterval = new TimeSpan(2800 * 10000 ); // 3.2s
                AppCuKey.SetValue(_rvn_ReloadIntervalInMs, 2800);
            }
        }

        /// <summary>
        /// If the saved window dimensions are larger than the current screen shrink the
        /// window to fit.
        /// </summary>
        private void SizeToFit()
        {
            if (Height > System.Windows.SystemParameters.VirtualScreenHeight)
                Height = System.Windows.SystemParameters.VirtualScreenHeight;

            if (Width > System.Windows.SystemParameters.VirtualScreenWidth)
                Width = System.Windows.SystemParameters.VirtualScreenWidth;
        }

        /// <summary>
        /// If the window is more than half off of the screen move it up and to the left
        /// so half the height and half the width are visible.
        /// </summary>
        private void MoveIntoView()
        {
            if (Top + Height / 2 > System.Windows.SystemParameters.VirtualScreenHeight)
                Top = System.Windows.SystemParameters.VirtualScreenHeight - Height;

            if (Left + Width / 2 > System.Windows.SystemParameters.VirtualScreenWidth)
                Left = System.Windows.SystemParameters.VirtualScreenWidth - Width;

            if (Top < 0) Top = 0;
            if (Left < 0) Left = 0;
        }



        private void LoadUrlHistory()
        {
            watchedUrlHistory = new MruDictionary<String,BrowserTab>(HISTORY_ENTRIES);

            var mruPath = _AppRegyPath + "\\URLs";
            var dirPath = _AppRegyPath + "\\Dirs";

            var mruKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(mruPath, true);
            var dirKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(dirPath, true);
            if (mruKey == null || dirKey == null)
                return ;

            try
            {
                // Get the list of items for which we want reload. This
                // is a string of characters ABCD....  The presence of a
                // char indicates that we want reload for that
                // particular item in the URLs list.
                string reloads = (string)AppCuKey.GetValue(_rvn_WantReloads);

                // Restore the history, in reverse order, so the last one stored,
                // is treated as the most recent item.
                for (int i = HISTORY_ENTRIES; i >= 0; i--)
                {
                    string c = new String((char)(i + 65), 1); // ascii char A,B,C (etc)
                    string url = (string) mruKey.GetValue(c);

                    // Is there a URL associated with that letter?
                    if (!String.IsNullOrEmpty(url))
                    {
                        string j = (string) dirKey.GetValue(c);
                        // Is there a set of directories to watch associated
                        // with that letter?
                        if (!String.IsNullOrEmpty(j))
                        {
                            var split = j.Split(new char[] { ',' },
                                                StringSplitOptions.RemoveEmptyEntries);
                            var slist = new System.Collections.Generic.List<String>();
                            slist.AddRange(split);
                            var bt = new BrowserTab
                            {
                                PathsToMonitor = slist
                            };
                            if (reloads!= null && reloads.IndexOf(c)>=0)
                                bt.WantReload = true;
                            watchedUrlHistory.Store(url, bt);
                        }
                        else
                        {
                            // There are no directories to watch.  Therefore
                            // remove the entries for this URL, because they
                            // don't store any useful information. This happens
                            // only during development of this code.
                            mruKey.DeleteValue(c);
                            dirKey.DeleteValue(c, false);
                        }
                    }
                    else
                    {
                        // There's no URL for this letter.  Clean out the dir
                        // entry, just in case, for good registry hygiene.
                        // This happens only during development of this code.
                        dirKey.DeleteValue(c, false);
                    }
                }
            }
            finally
            {
                dirKey.Close();
                mruKey.Close();
            }
        }


        private void StoreUrlHistory()
        {
            var mruPath = _AppRegyPath + "\\URLs";
            var dirPath = _AppRegyPath + "\\Dirs";

            var mruKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(mruPath, true);
            if (mruKey == null)
                mruKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(mruPath);

            if (mruKey == null) return;

            var dirKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(dirPath, true);
            if (dirKey == null)
                dirKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(dirPath);

            if (dirKey == null)
            {
                mruKey.Close();
                return;
            }

            try
            {
                var wantReloads = new System.Collections.Generic.List<String>();
                var list = watchedUrlHistory.GetKeys();
                for (int i=0, C=list.Count; i<C; i++)
                {
                    string c = new String((char)(i+65),1);
                    mruKey.SetValue(c, list[i]);

                    // store the list of directories for that URL
                    var tab = watchedUrlHistory[list[i]];
                    if (tab.WantReload)
                        wantReloads.Add(c);

                    if (tab.PathsToMonitor != null && tab.PathsToMonitor.Count > 0)
                    {
                        var a = tab.PathsToMonitor.ToArray();
                        string j = String.Join(",", a);
                        dirKey.SetValue(c, j);
                    }
                    else
                    {
                        dirKey.SetValue(c, "");
                    }
                }

                if (wantReloads.Count>0)
                {
                    string reloads = String.Join("", wantReloads.ToArray());
                    AppCuKey.SetValue(_rvn_WantReloads, reloads);
                }
                else
                {
                    AppCuKey.SetValue(_rvn_WantReloads, "");
                }
            }
            finally
            {
                mruKey.Close();
                dirKey.Close();
            }
        }



        private Microsoft.Win32.RegistryKey AppCuKey
        {
            get
            {
                if (_appCuKey == null)
                {
                    _appCuKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(_AppRegyPath, true);
                    if (_appCuKey == null)
                        _appCuKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(_AppRegyPath);
                }
                return _appCuKey;
            }
            set { _appCuKey = null; }
        }


    }


}
