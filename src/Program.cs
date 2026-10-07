using System;
using System.Threading;
using System.Windows.Forms;
using IdleWalker.App;
using IdleWalker.Platform;

namespace IdleWalker
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "IdleWalker.SingleInstance", out createdNew))
            {
                if (!createdNew) return;

                // Must happen before any window exists so that monitor
                // coordinates are real pixels on every display.
                Native.EnablePerMonitorDpi();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (WalkerApp app = new WalkerApp())
                    Application.Run(app);
            }
        }
    }
}
