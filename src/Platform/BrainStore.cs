using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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
        const string Header = "# IdleWalker brains v4";   // v4: 81 situations, sparse table
        const string HeaderV1 = "# IdleWalker brains v1";
        const string HeaderV2 = "# IdleWalker brains v2";
        const string HeaderV3 = "# IdleWalker brains v3";
        static readonly object SaveLock = new object();

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
                // Upgrade v3: accept v1/v2 (migrate: missing exp/rew/trials/MLP default
                // to fresh) and v3 headers; unknown headers mean a foreign file.
                string first = null;
                string[] head = text.Split('\n');
                for (int i = 0; i < head.Length; i++)
                {
                    string t = head[i].Trim();
                    if (t.Length == 0) continue;
                    first = t;
                    break;
                }
                if (first != null && first.Length > 0 && first[0] == '#' &&
                    first != Header && first != HeaderV1 && first != HeaderV2 && first != HeaderV3)
                    return;
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

        /// <summary>Saves lasting memories on this thread; for exit, when the write must have finished.</summary>
        public static void Save(Troupe troupe)
        {
            string text = Pack(troupe);
            if (text != null) Write(text);
        }

        /// <summary>
        /// Copies the memories on this thread (the walkers' own - their state
        /// is read here; a few array copies) and turns them into text and
        /// writes the file on a pool thread, so the once-a-minute save never
        /// holds a frame for the formatting, the disk, or an antivirus taking
        /// an interest in the new file.
        /// </summary>
        public static void SaveInBackground(Troupe troupe)
        {
            List<KeyValuePair<string, Walker.MindCopy>> minds = Copy(troupe);
            if (minds == null) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string text = Format(minds);
                if (text != null) Write(text);
            });
        }

        /// <summary>Every walker's lasting memory as the file's text; null if it could not be built.</summary>
        static string Pack(Troupe troupe)
        {
            List<KeyValuePair<string, Walker.MindCopy>> minds = Copy(troupe);
            return minds == null ? null : Format(minds);
        }

        /// <summary>Every walker's lasting memory, copied out, by name; null if it could not be copied.</summary>
        static List<KeyValuePair<string, Walker.MindCopy>> Copy(Troupe troupe)
        {
            try
            {
                List<KeyValuePair<string, Walker.MindCopy>> minds = new List<KeyValuePair<string, Walker.MindCopy>>();
                foreach (Walker walker in troupe.Walkers)
                {
                    Walker.MindCopy mind;
                    try
                    {
                        mind = walker.CopyMind();
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    minds.Add(new KeyValuePair<string, Walker.MindCopy>(walker.Personality.Name, mind));
                }
                return minds;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The copied memories as the file's text; null if it could not be built.</summary>
        static string Format(List<KeyValuePair<string, Walker.MindCopy>> minds)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(Header).Append("\n");
                foreach (KeyValuePair<string, Walker.MindCopy> mind in minds)
                {
                    string pack;
                    try
                    {
                        pack = mind.Value.Pack();
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    sb.Append("[").Append(mind.Key).Append("]\n");
                    sb.Append(pack);
                }
                return sb.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void Write(string text)
        {
            lock (SaveLock)
            {
                try
                {
                    string path = FilePath();
                    if (path == null) return;
                    string dir = Path.GetDirectoryName(path);
                    if (dir != null && dir.Length > 0 && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    // Upgrade v2: atomic save - write tmp then replace, so a crash
                    // can never leave a half-written brains.txt behind.
                    string tmp = path + ".tmp";
                    File.WriteAllText(tmp, text, Encoding.UTF8);
                    try
                    {
                        if (File.Exists(path))
                            File.Replace(tmp, path, path + ".bak");
                        else
                            File.Move(tmp, path);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            if (File.Exists(tmp)) File.Copy(tmp, path, true);
                        }
                        catch (Exception)
                        {
                        }
                        try
                        {
                            if (File.Exists(tmp)) File.Delete(tmp);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                catch (Exception)
                {
                    // Saving must never interrupt the program; it just retries later.
                }
            }
        }
    }
}
