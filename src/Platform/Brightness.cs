using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace IdleWalker.Platform
{
    /// <summary>
    /// One display whose backlight can (perhaps) be set. External monitors
    /// are driven over DDC/CI (the monitor's own brightness, as its buttons
    /// would set it); a laptop's built-in panel, which has no DDC/CI, through
    /// the WMI brightness class the display driver provides. Writes go out on
    /// a worker thread and coalesce: a slider dragged quickly sends only the
    /// latest value once the previous write finishes, because a DDC/CI write
    /// can take a tenth of a second.
    /// </summary>
    sealed class BrightnessDisplay : IDisposable
    {
        public readonly string Name;
        public readonly bool Primary;

        /// <summary>False when neither DDC/CI nor WMI answered: the slider is shown greyed.</summary>
        public readonly bool Supported;

        readonly IntPtr physical;            // DDC/CI handle (zero is a valid handle, hence ddc)
        readonly bool ddc;                   // an external monitor, via DDC/CI
        readonly bool wmi;                   // the built-in panel, via WMI
        readonly uint min, max;              // the monitor's own range (DDC/CI); 0..100 for WMI
        readonly object gate = new object();
        int percent;
        int pending = -1;                    // the value waiting to be written, or -1
        bool writing;
        bool disposed;

        BrightnessDisplay(string name, bool primary, IntPtr physical, bool ddc, bool wmi, uint min, uint max, int percent)
        {
            Name = name;
            Primary = primary;
            this.physical = physical;
            this.ddc = ddc;
            this.wmi = wmi;
            this.min = min;
            this.max = max;
            this.percent = percent;
            Supported = ddc || wmi;
        }

        /// <summary>The brightness last read or asked for, 0..100.</summary>
        public int Percent
        {
            get { lock (gate) return percent; }
        }

        /// <summary>Asks for a new brightness, 0..100. Returns at once; the write happens in the background.</summary>
        public void Set(int value)
        {
            if (!Supported) return;
            value = Math.Max(0, Math.Min(100, value));
            lock (gate)
            {
                percent = value;
                pending = value;
                if (writing || disposed) return;
                writing = true;
            }
            ThreadPool.QueueUserWorkItem(delegate { Drain(); });
        }

        void Drain()
        {
            while (true)
            {
                int value;
                lock (gate)
                {
                    if (pending < 0 || disposed)
                    {
                        writing = false;
                        Monitor.PulseAll(gate);
                        return;
                    }
                    value = pending;
                    pending = -1;
                }
                try
                {
                    if (ddc)
                        SetMonitorBrightness(physical, (uint)(min + (max - min) * value / 100.0 + 0.5));
                    else if (wmi)
                        WmiSet(value);
                }
                catch (Exception)
                {
                }
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                // Let the write in flight finish before the handle goes.
                pending = -1;
                while (writing) Monitor.Wait(gate, 1000);
                if (disposed) return;
                disposed = true;
            }
            if (ddc) DestroyPhysicalMonitor(physical);
        }

        // ---- Finding the displays ----

        /// <summary>
        /// Every monitor, in Screen.AllScreens order, each with its current
        /// brightness. Slow (DDC/CI reads can take a while per monitor), so
        /// call it off the UI thread.
        /// </summary>
        public static List<BrightnessDisplay> Enumerate()
        {
            List<BrightnessDisplay> found = new List<BrightnessDisplay>();
            bool wmiUsed = false;
            int wmiPercent = WmiGet();
            Screen[] screens = Screen.AllScreens;
            for (int s = 0; s < screens.Length; s++)
            {
                Screen screen = screens[s];
                string label = "Display " + (s + 1);
                IntPtr hmonitor = Native.MonitorFromPoint(
                    new Native.POINT(screen.Bounds.Left + screen.Bounds.Width / 2, screen.Bounds.Top + screen.Bounds.Height / 2),
                    Native.MONITOR_DEFAULTTONEAREST);

                BrightnessDisplay display = TryDdc(hmonitor, label, screen.Primary);
                if (display == null && wmiPercent >= 0 && !wmiUsed)
                {
                    // A laptop has one built-in panel; the first monitor that
                    // does not speak DDC/CI is taken to be it.
                    wmiUsed = true;
                    display = new BrightnessDisplay(label + " - Built-in", screen.Primary, IntPtr.Zero, false, true, 0, 100, wmiPercent);
                }
                if (display == null)
                    display = new BrightnessDisplay(label, screen.Primary, IntPtr.Zero, false, false, 0, 100, 0);
                found.Add(display);
            }
            return found;
        }

        static BrightnessDisplay TryDdc(IntPtr hmonitor, string label, bool primary)
        {
            try
            {
                uint count;
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hmonitor, out count) || count == 0) return null;
                PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
                if (!GetPhysicalMonitorsFromHMONITOR(hmonitor, count, monitors)) return null;

                // Mirrored outputs share one HMONITOR: the first that answers
                // drives the slider, the rest are let go.
                BrightnessDisplay chosen = null;
                foreach (PHYSICAL_MONITOR m in monitors)
                {
                    uint lo, cur, hi;
                    if (chosen == null && GetMonitorBrightness(m.Handle, out lo, out cur, out hi) && hi > lo)
                    {
                        string description = (m.Description ?? "").Trim();
                        string name = description.Length > 0 && description != "Generic PnP Monitor"
                            ? label + " - " + description : label;
                        int percent = (int)Math.Round((cur - lo) * 100.0 / (hi - lo));
                        chosen = new BrightnessDisplay(name, primary, m.Handle, true, false, lo, hi, percent);
                    }
                    else
                    {
                        DestroyPhysicalMonitor(m.Handle);
                    }
                }
                return chosen;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- The built-in panel, through WMI ----

        /// <summary>The built-in panel's brightness, 0..100, or -1 when there is none.</summary>
        static int WmiGet()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject o in results)
                        using (o) return Convert.ToInt32(o["CurrentBrightness"]);
                }
            }
            catch (Exception)
            {
            }
            return -1;
        }

        static void WmiSet(int percent)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=TRUE"))
            using (ManagementObjectCollection results = searcher.Get())
            {
                foreach (ManagementObject o in results)
                    using (o) o.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)percent });
            }
        }

        // ---- dxva2: the monitor configuration API (DDC/CI) ----

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PHYSICAL_MONITOR
        {
            public IntPtr Handle;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
        }

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hmonitor, out uint count);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hmonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetMonitorBrightness(IntPtr monitor, out uint min, out uint current, out uint max);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool SetMonitorBrightness(IntPtr monitor, uint brightness);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool DestroyPhysicalMonitor(IntPtr monitor);
    }
}
