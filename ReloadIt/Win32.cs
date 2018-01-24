using System;
using System.Collections.Generic;
using Accessibility;
using System.Runtime.InteropServices;

namespace ReloadIt
{
    public static class Win32
    {
        [DllImport("user32.dll")]
        internal static extern IntPtr BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent,
                                                  IntPtr hwndChildAfter,
                                                  string lpszClass,
                                                  string lpszWindow);

        [DllImport("user32.dll",
                   EntryPoint="SetForegroundWindow",
                   CallingConvention=CallingConvention.StdCall,
                   CharSet=CharSet.Unicode, SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr handle);

        [DllImport("user32.dll",
                   EntryPoint="ShowWindow",
                   CallingConvention=CallingConvention.StdCall,
                   CharSet=CharSet.Unicode, SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr handle, Int32 nCmd) ;

        [DllImport("user32.dll",
                   EntryPoint="IsIconic",
                   CallingConvention=CallingConvention.StdCall,
                   CharSet=CharSet.Unicode,
                   SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr hWnd);


        [DllImport("user32.dll",
                   EntryPoint="IsIconic",
                   CallingConvention=CallingConvention.StdCall,
                   CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("oleacc.dll")]
        internal static extern int AccessibleObjectFromWindow
            (IntPtr hwnd, uint id, ref Guid iid,
             [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object ppvObject);


        private static IAccessible AccessibleObjectFromWindow(IntPtr hwnd)
        {
            Guid guid = new Guid(Constants.IID_IAccessible);
            object obj = null;
            uint id = (uint) (OBJID.OBJID_WINDOW);
            int num = AccessibleObjectFromWindow(hwnd, id, ref guid, ref obj);
            var acc = obj as IAccessible;
            return acc;
        }


        private static IntPtr GetDirectUIHWND(IntPtr ieFrame)
        {
            // This is a glorious hack.
            //
            // In an IE instance, the control with the name "Tab Row" is the
            // thing that holds all the various tabs. Given an IAccessible
            // corresponding to that thing, it's easy to enumerate the children,
            // and get the tabs. An automation app can inspect the URL on each
            // tab, and then call IAccessible.accDoDefaultAction(0), to activate
            // a tab by URL.  Nice.
            //
            // But I couldn't figure how to directly get the "Tab Row" control
            // of an IE instance, from a SHDocVw.WebBrowser COM object, which is
            // what I can get out of SHDocVw.ShellWindowsClass.  I tried it a
            // million ways, and finally the simplest way I could find to get it
            // to work is this: get the COM object, get the HWND from that
            // object, then traverse the OS Window hierarchy to find the HWND
            // with the name "DirectUIHWND".  From there, walk the IAccessible
            // tree , to find the "Tab Row", then select the tab and invoke the
            // method.
            //
            // It sounds ugly to describe, and it hurt to code it this way. I
            // could not figure out a way to do the traversal only in HWNDs or
            // only in IAccessible.  I have no idea why I needed to do both, but
            // asw I said I could not figure out how to succeed any other way.
            //
            // For now, it works.
            //

            // try IE 9 first:
            IntPtr intptr = FindWindowEx(ieFrame, IntPtr.Zero, "WorkerW", null);
            if (intptr == IntPtr.Zero)
            {
                // IE8 and IE7
                intptr = FindWindowEx(ieFrame, IntPtr.Zero, "CommandBarClass", null);
            }
            intptr = FindWindowEx(intptr, IntPtr.Zero, "ReBarWindow32", null);
            intptr = FindWindowEx(intptr, IntPtr.Zero, "TabBandClass", null);
            intptr = FindWindowEx(intptr, IntPtr.Zero, "DirectUIHWND", null);
            return intptr;
        }


        /// <summary>
        ///   Gets the URL associated to a browser tab, given the IAccessible
        ///   corresponding to that tab.
        /// </summary>
        /// <param name='tab'>an IAccessible corresponding to the
        /// IE browser tab.</param>
        /// <returns>a string, the URL being displayed in the tab.</returns>
        /// <remarks>
        ///   <para>
        ///     This is used to aid in determining which tab to activate.
        ///   </para>
        /// </remarks>
        private static string UrlForTab(IAccessible tab)
        {
            try
            {
                var desc = tab.get_accDescription(0);
                if (desc != null)
                {
                    if (desc.Contains("\n"))
                    {
                        string url = desc.Substring(desc.IndexOf("\n")).Trim();
                        return url;
                    }
                    else
                    {
                        return desc;
                    }
                }
            }
            catch { }
            return "??";
        }


        /// <summary>
        ///   Activate an existing tab in IE - bring the Window to the front, as
        ///   necessary, and then select the tab as necessary, among all the
        ///   other tabs in the Window.
        /// </summary>
        /// <param name='ietab'>the IE tab</param>
        /// <remarks>
        ///   <para>
        ///     Essentially we walk up the HWND hierarchy to get to the
        ///     DirectUIHWND thing, then walk *down* the IAccessible hierarchy
        ///     to get the tab, before selecting it. This seems a little
        ///     roundabout, but it's the best way I could figure to get this to
        ///     work.
        ///   </para>
        /// </remarks>
        internal static void ActivateTab(SHDocVw.WebBrowser ietab)
        {
            // unbelievably, I could not find a better way to do this.

            string urlOfTabToActivate = ietab.LocationURL;
            IntPtr hwnd = (IntPtr) ietab.HWND;

            // directUi: the hwnd for the thing that contains the
            // tab row.
            var directUi = GetDirectUIHWND(hwnd);

            // get the Accessibility object for that thing
            var iacc = AccessibleObjectFromWindow(directUi);

            // find the IAccessible for the tab row
            var tabRow = FindAccessibleDescendant(iacc, "Tab Row");

            // get the list of tabs
            var tabs = AccChildren(tabRow);
            int tc = tabs.Count;
            int k = 0;

            // walk through the tabs and tick the chosen one
            foreach (var candidateTab in tabs)
            {
                k++;
                // the last tab is "New Tab", which we don't want
                if (k == tc) continue;

                // the URL on *this* tab
                string localUrl = UrlForTab(candidateTab);

                // same? if so, tick it. This selects the given tab among all
                // the others, if any.
                if (urlOfTabToActivate != null
                    && localUrl.Equals(urlOfTabToActivate))
                {
                    candidateTab.accDoDefaultAction(0);
                    return;
                }
            }
        }


        [DllImport("oleacc.dll")]
        private static extern int AccessibleChildren(IAccessible paccContainer,
                                                     int iChildStart,
                                                     int cChildren,
                                                     [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] object[] rgvarChildren,
                                                     out int pcObtained);


        private static object[] GetAccessibleChildren(IAccessible a)
        {
            int nChildren = 0;
            object[] ret = null;
            int count = a.accChildCount;

            if (count > 0)
            {
                ret = new object[count];
                AccessibleChildren(a, 0, count, ret, out nChildren);
            }
            return ret;
        }


        private static List<IAccessible> AccChildren(IAccessible accessible)
        {
            object[] res = GetAccessibleChildren(accessible);
            var list = new List<IAccessible>();
            if (res == null) return list;

            foreach (object obj in res)
            {
                IAccessible acc = obj as IAccessible;
                if (acc != null) list.Add(acc);
            }
            return list;
        }


        private static IAccessible FindAccessibleDescendant(IAccessible parent, String strName)
        {
            int c = parent.accChildCount;
            if (c == 0)
                return null;

            var children = AccChildren(parent);

            foreach (var child in children)
            {
                if (child == null) continue;
                if (strName.Equals(child.get_accName(0)))
                    return child;

                var x = FindAccessibleDescendant(child, strName);
                if (x!=null) return x;
            }

            return null;
        }
    }



    public enum RefreshLevel
    {
        Normal     = 0,
        IfExpired  = 1,
        Completely = 3
    }

    enum OBJID : uint
    {
        OBJID_WINDOW = 0x00000000,
    }

    public class Constants
    {
        public const int SW_SHOW    = 5;
        public const int SW_RESTORE = 9;

        public const string IID_IAccessible    = "{618736e0-3c3d-11cf-810c-00aa00389b71}";
    }

}
