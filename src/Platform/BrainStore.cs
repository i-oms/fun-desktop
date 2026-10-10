using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IdleWalker.Domain;

namespace IdleWalker.Platform
{
    /// <summary>
    /// Keeps what the walkers learned between runs: each walker's taste, its
    /// friendships and grudges, its hangouts and its walk, in one small text
    /// file under %APPDATA%\IdleWalker. Moods (cheer, fear, energy) are the
    /// moment, not the character, and start fresh. A missing or corrupt file
    /// just means a fresh cast - startup never fails because of it.
    /// </summary>
    static class BrainStore
    {
        const string Header = "# IdleWalker brains v1";

        /// <summary>Test hook: a fixed file path instead of %APPDATA%. Null means the real one.</summary>
#pragma warning disable 0649
        internal static string OverridePath;
#pragma warning restore 0649

        /// <summary>Where the brains live on disk (null if no place is available).</summary>
        public static string StoredPath()
        {
            return FilePath();
        }

        /// <summary>How big the brain file is, in bytes; -1 when there is nothing saved yet.</summary>
        public static long FileSizeBytes()
        {
            try
            {
                string path = FilePath();
                if (path == null || !File.Exists(path)) return -1;
                return new FileInfo(path).Length;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>Forgets everything: clears the in-memory minds and deletes the file.</summary>
        public static void Forget(Troupe troupe)
        {
            try
            {
                troupe.ForgetBrains();
            }
            catch (Exception)
            {
            }
            try
            {
                string path = FilePath();
                if (path != null && File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        static string FilePath()
        {
            if (OverridePath != null) return OverridePath;
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (dir == null || dir.Length == 0) return null;
            return Path.Combine(Path.Combine(dir, "IdleWalker"), "brains.txt");
        }

        /// <summary>Loads lasting memories into the troupe; safe to call once at startup.</summary>
        public static void Load(Troupe troupe)
        {
            try
            {
                string path = FilePath();
                if (path == null || !File.Exists(path)) return;
                string text = File.ReadAllText(path, Encoding.UTF8);
                if (text == null || text.Length == 0) return;
                troupe.EnsureCast();
                Dictionary<string, Walker> byName = new Dictionary<string, Walker>();
                foreach (Walker walker in troupe.Walkers)
                    byName[walker.Personality.Name] = walker;
                Dictionary<string, string> fields = null;
                Walker current = null;
                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    if (line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        if (current != null && fields != null) current.UnpackMind(fields, byName);
                        fields = null;
                        current = null;
                        string name = line.Substring(1, line.Length - 2);
                        Walker found;
                        if (byName.TryGetValue(name, out found))
                        {
                            current = found;
                            fields = new Dictionary<string, string>();
                        }
                        continue;
                    }
                    if (current == null || fields == null) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    fields[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
                if (current != null && fields != null) current.UnpackMind(fields, byName);
            }
            catch (Exception)
            {
                // A fresh cast is always an acceptable fallback.
            }
        }

        /// <summary>Saves lasting memories; safe to call periodically and on exit.</summary>
        public static void Save(Troupe troupe)
        {
            try
            {
                string path = FilePath();
                if (path == null) return;
                StringBuilder sb = new StringBuilder();
                sb.Append(Header).Append("\n");
                foreach (Walker walker in troupe.Walkers)
                {
                    string pack;
                    try
                    {
                        pack = walker.PackMind();
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    sb.Append("[").Append(walker.Personality.Name).Append("]\n");
                    sb.Append(pack);
                }
                string dir = Path.GetDirectoryName(path);
                if (dir != null && dir.Length > 0 && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                File.Copy(tmp, path, true);
                try
                {
                    File.Delete(tmp);
                }
                catch (Exception)
                {
                }
            }
            catch (Exception)
            {
                // Saving must never interrupt the program; it just retries later.
            }
        }
    }
}
