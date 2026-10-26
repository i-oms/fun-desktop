using System.Drawing;
using System.IO;
using System.Reflection;

namespace IdleWalker.App
{
    /// <summary>
    /// The logo, assets\IdleWalker.ico. build.cmd embeds it twice: as the
    /// exe's Win32 icon (Explorer, taskbar, Alt-Tab) and as a managed
    /// resource, read here, so the tray and the dialogs can ask for the
    /// exact size they draw at instead of scaling one frame.
    /// </summary>
    static class AppIcon
    {
        const string ResourceName = "IdleWalker.ico";

        /// <summary>The frame closest to <paramref name="size"/>, or null
        /// when the exe was built without the resource.</summary>
        public static Icon Load(Size size)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                return stream == null ? null : new Icon(stream, size);
        }
    }
}
