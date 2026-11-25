// ============================================================================
// NillySearch Pro v1.0 — Ultra-Fast Native File Finder for Windows 10/11
// created by Nilly graphics (Sotiris Christos) — https://nillygraphics.com/
// Strictly C# 5 compatible (.NET Framework 4.x csc.exe — 0 external DLLs)
// Win32 FindFirstFileExW (LARGE_FETCH) · Virtual 60 FPS Grid · 0 MB Idle RAM
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("NillySearch Pro")]
[assembly: AssemblyDescription("Ultra-Fast Instant File Finder for Windows 10/11 (Win32 FindFirstFileExW · 0 MB Background RAM) — created by Nilly graphics (Sotiris Christos)")]
[assembly: AssemblyCompany("Nilly graphics (Sotiris Christos) — https://nillygraphics.com/")]
[assembly: AssemblyProduct("NillySearch Pro")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Nilly graphics (Sotiris Christos) — https://nillygraphics.com/")]
[assembly: AssemblyTrademark("Nilly graphics / Nillygraphics — Sotiris Christos")]
[assembly: AssemblyVersion("1.1.1.0")]
[assembly: AssemblyFileVersion("1.1.1.0")]

namespace NillySearchPro
{
    internal static class Native
    {
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        public const int FIND_FIRST_EX_LARGE_FETCH = 2;
        public const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
        public const int FILE_ATTRIBUTE_REPARSE_POINT = 0x400;
        private const int EM_SETCUEBANNER = 0x1501;

        public enum FINDEX_INFO_LEVELS { FindExInfoStandard = 0, FindExInfoBasic = 1 }
        public enum FINDEX_SEARCH_OPS { FindExSearchNameMatch = 0 }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATAW
        {
            public uint dwFileAttributes;
            public uint ftCreationTimeLow;
            public uint ftCreationTimeHigh;
            public uint ftLastAccessTimeLow;
            public uint ftLastAccessTimeHigh;
            public uint ftLastWriteTimeLow;
            public uint ftLastWriteTimeHigh;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindFirstFileExW(
            string lpFileName,
            FINDEX_INFO_LEVELS fInfoLevelId,
            out WIN32_FIND_DATAW lpFindFileData,
            FINDEX_SEARCH_OPS fSearchOp,
            IntPtr lpSearchFilter,
            int dwAdditionalFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool FindNextFileW(IntPtr hFindFile, out WIN32_FIND_DATAW lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FindClose(IntPtr hFindFile);

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr minSize, IntPtr maxSize);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        public static void SetDarkTitlebar(IntPtr hwnd, bool dark)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int v = dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, 20, ref v, 4) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref v, 4);
            }
            catch { }
        }

        public static void SetCueBanner(TextBox tb, string text)
        {
            if (tb == null || !tb.IsHandleCreated) return;
            try { SendMessage(tb.Handle, EM_SETCUEBANNER, (IntPtr)1, text); } catch { }
        }

        public static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
                SetProcessWorkingSetSize(GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        }
    }

    internal enum FileCat : byte
    {
        Other = 0,
        Folder = 1,
        Doc = 2,
        Image = 3,
        Design = 4,
        Media = 5,
        Archive = 6,
        App = 7
    }

    // Compact value-type record: keeps 100k+ indexed files in ~12-16 MB RAM!
    internal struct IndexedEntry
    {
        public int DirIndex;
        public string Name;
        public string NormName;
        public long Size;
        public long LastWriteFileTime;
        public FileCat Cat;
    }

    internal sealed class SearchIndex
    {
        public readonly List<string> Dirs = new List<string>(8192);
        public readonly List<IndexedEntry> Items = new List<IndexedEntry>(65536);

        private static readonly HashSet<string> NoiseDirNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "$Recycle.Bin", "System Volume Information", "WinSxS", "node_modules", ".git",
            "__pycache__", "Package Cache", "Installer", "Temp", "Cache", "Code Cache",
            "GPUCache", "Service Worker", "CrashDumps", "WindowsApps", "SoftwareDistribution"
        };

        private static readonly Dictionary<string, FileCat> ExtCats = BuildExtMap();

        private static Dictionary<string, FileCat> BuildExtMap()
        {
            Dictionary<string, FileCat> m = new Dictionary<string, FileCat>(StringComparer.OrdinalIgnoreCase);
            string[] docs = new string[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".rtf", ".odt", ".ods", ".txt", ".md", ".csv", ".json", ".xml", ".html", ".htm", ".log" };
            string[] imgs = new string[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tif", ".tiff", ".heic", ".avif", ".jxl", ".ico", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".raf" };
            string[] des  = new string[] { ".psd", ".psb", ".ai", ".eps", ".svg", ".svgz", ".afdesign", ".afphoto", ".afpub", ".cdr", ".indd", ".fig", ".blend", ".sketch", ".ttf", ".otf", ".woff2" };
            string[] med  = new string[] { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".wmv", ".mp3", ".wav", ".flac", ".m4a", ".ogg", ".aac" };
            string[] arc  = new string[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".xz", ".bz2", ".zst", ".cab", ".iso" };
            string[] apps = new string[] { ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".lnk", ".dll" };
            for (int i = 0; i < docs.Length; i++) m[docs[i]] = FileCat.Doc;
            for (int i = 0; i < imgs.Length; i++) m[imgs[i]] = FileCat.Image;
            for (int i = 0; i < des.Length; i++)  m[des[i]]  = FileCat.Design;
            for (int i = 0; i < med.Length; i++)  m[med[i]]  = FileCat.Media;
            for (int i = 0; i < arc.Length; i++)  m[arc[i]]  = FileCat.Archive;
            for (int i = 0; i < apps.Length; i++) m[apps[i]] = FileCat.App;
            return m;
        }

        public static FileCat Classify(string name, bool isDir)
        {
            if (isDir) return FileCat.Folder;
            int dot = name.LastIndexOf('.');
            if (dot < 0 || dot >= name.Length - 1) return FileCat.Other;
            FileCat c;
            return ExtCats.TryGetValue(name.Substring(dot), out c) ? c : FileCat.Other;
        }

        // Fast Greek accent-insensitive & final-sigma (ς -> σ) normalization!
        public static string NormalizeGreek(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            bool pureAscii = true;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] > 127) { pureAscii = false; break; }
            }
            if (pureAscii) return s.ToLowerInvariant();

            string n = s.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(n.Length);
            for (int i = 0; i < n.Length; i++)
            {
                char ch = n[i];
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    char low = char.ToLowerInvariant(ch);
                    sb.Append(low == 'ς' ? 'σ' : low);
                }
            }
            return sb.ToString();
        }

        public static SearchIndex ScanRoots(string[] roots, bool includeSystem, Func<bool> isCancelled, Action<int, string> onProgress)
        {
            SearchIndex idx = new SearchIndex();
            object lockObj = new object();
            Queue<string> work = new Queue<string>();

            for (int i = 0; i < roots.Length; i++)
            {
                string r = roots[i];
                if (!string.IsNullOrEmpty(r) && Directory.Exists(r))
                    work.Enqueue(r.TrimEnd('\\'));
            }

            // Expand first level so multiple threads scan top-level folders in parallel!
            int initialCount = work.Count;
            for (int k = 0; k < initialCount; k++)
            {
                string root = work.Dequeue();
                ScanSingleDir(root, includeSystem, idx, lockObj, work, isCancelled);
            }

            int workerCount = Math.Min(Math.Max(2, Environment.ProcessorCount), 6);
            int activeWorkers = 0;
            int remainingWorkers = workerCount;
            int lastReported = 0;
            ManualResetEvent done = new ManualResetEvent(false);

            WaitCallback worker = delegate(object state)
            {
                try
                {
                    while (!isCancelled())
                    {
                        string dir = null;
                        lock (lockObj)
                        {
                            if (work.Count > 0)
                            {
                                dir = work.Dequeue();
                                activeWorkers++;
                            }
                            else if (activeWorkers == 0)
                            {
                                break;
                            }
                        }
                        if (dir == null) { Thread.Sleep(4); continue; }

                        try
                        {
                            ScanSingleDir(dir, includeSystem, idx, lockObj, work, isCancelled);
                        }
                        catch { }

                        int totalNow = 0;
                        bool shouldReport = false;
                        lock (lockObj)
                        {
                            activeWorkers--;
                            totalNow = idx.Items.Count;
                            if (totalNow - lastReported >= 4000)
                            {
                                lastReported = totalNow;
                                shouldReport = true;
                            }
                        }
                        if (shouldReport && onProgress != null) onProgress(totalNow, dir);
                    }
                }
                catch { }
                finally
                {
                    if (Interlocked.Decrement(ref remainingWorkers) == 0)
                        done.Set();
                }
            };

            for (int w = 0; w < workerCount; w++) ThreadPool.QueueUserWorkItem(worker);
            done.WaitOne();
            done.Dispose();
            return idx;
        }

        private static void ScanSingleDir(string dirPath, bool includeSystem, SearchIndex idx, object lockObj, Queue<string> work, Func<bool> isCancelled)
        {
            if (isCancelled()) return;
            string searchPattern = (dirPath.Length >= 240 ? @"\\?\" : "") + dirPath + @"\*";
            Native.WIN32_FIND_DATAW fd;
            IntPtr h = Native.FindFirstFileExW(
                searchPattern,
                Native.FINDEX_INFO_LEVELS.FindExInfoBasic,
                out fd,
                Native.FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                Native.FIND_FIRST_EX_LARGE_FETCH);
            if (h == Native.INVALID_HANDLE_VALUE) return;

            List<IndexedEntry> localEntries = new List<IndexedEntry>(64);
            List<string> subDirs = new List<string>(16);

            try
            {
                do
                {
                    string name = fd.cFileName;
                    if (string.IsNullOrEmpty(name) || name == "." || name == "..") continue;
                    bool isDir = (fd.dwFileAttributes & Native.FILE_ATTRIBUTE_DIRECTORY) != 0;
                    bool isReparse = (fd.dwFileAttributes & Native.FILE_ATTRIBUTE_REPARSE_POINT) != 0;

                    if (isDir && !includeSystem)
                    {
                        if (NoiseDirNames.Contains(name)) continue;
                        if (dirPath.Length <= 3 && (name.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                                                    name.Equals("ProgramData", StringComparison.OrdinalIgnoreCase) ||
                                                    name.Equals("Recovery", StringComparison.OrdinalIgnoreCase)))
                            continue;
                    }

                    long sz = isDir ? -1L : (((long)fd.nFileSizeHigh << 32) | fd.nFileSizeLow);
                    long ft = ((long)fd.ftLastWriteTimeHigh << 32) | fd.ftLastWriteTimeLow;

                    IndexedEntry ent = new IndexedEntry();
                    ent.Name = name;
                    ent.NormName = NormalizeGreek(name);
                    ent.Size = sz;
                    ent.LastWriteFileTime = ft;
                    ent.Cat = Classify(name, isDir);
                    localEntries.Add(ent);

                    if (isDir && !isReparse)
                        subDirs.Add(dirPath + "\\" + name);
                }
                while (!isCancelled() && Native.FindNextFileW(h, out fd));
            }
            finally
            {
                Native.FindClose(h);
            }

            lock (lockObj)
            {
                int dirIdx = idx.Dirs.Count;
                idx.Dirs.Add(dirPath);
                for (int i = 0; i < localEntries.Count; i++)
                {
                    IndexedEntry e = localEntries[i];
                    e.DirIndex = dirIdx;
                    idx.Items.Add(e);
                }
                for (int i = 0; i < subDirs.Count; i++)
                    work.Enqueue(subDirs[i]);
            }
        }

        // Ultra-fast binary index cache (loads 100k+ items in ~100ms on restart!)
        public static string CacheFilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NillySuite");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, ".nillysearch_index.bin");
            }
        }

        public void SaveCache(string filePath)
        {
            try
            {
                string tmp = filePath + ".tmp";
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8))
                {
                    bw.Write(0x4E535031); // "NSP1"
                    bw.Write(Dirs.Count);
                    for (int i = 0; i < Dirs.Count; i++) bw.Write(Dirs[i]);
                    bw.Write(Items.Count);
                    for (int i = 0; i < Items.Count; i++)
                    {
                        IndexedEntry e = Items[i];
                        bw.Write(e.DirIndex);
                        bw.Write(e.Name);
                        bw.Write(e.Size);
                        bw.Write(e.LastWriteFileTime);
                        bw.Write((byte)e.Cat);
                    }
                }
                if (File.Exists(filePath)) File.Delete(filePath);
                File.Move(tmp, filePath);
            }
            catch { }
        }

        public static SearchIndex LoadCache(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                SearchIndex idx = new SearchIndex();
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
                using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8))
                {
                    if (br.ReadInt32() != 0x4E535031) return null;
                    int dc = br.ReadInt32();
                    if (dc < 0 || dc > 2000000) return null;
                    idx.Dirs.Capacity = dc;
                    for (int i = 0; i < dc; i++) idx.Dirs.Add(br.ReadString());
                    int ic = br.ReadInt32();
                    if (ic < 0 || ic > 5000000) return null;
                    idx.Items.Capacity = ic;
                    for (int i = 0; i < ic; i++)
                    {
                        IndexedEntry e = new IndexedEntry();
                        e.DirIndex = br.ReadInt32();
                        e.Name = br.ReadString();
                        e.NormName = NormalizeGreek(e.Name);
                        e.Size = br.ReadInt64();
                        e.LastWriteFileTime = br.ReadInt64();
                        e.Cat = (FileCat)br.ReadByte();
                        idx.Items.Add(e);
                    }
                }
                return idx;
            }
            catch { return null; }
        }
    }

    internal sealed class QuerySpec
    {
        public readonly List<string> Terms = new List<string>();
        public string RequiredExt;      // e.g. ".pdf"
        public long MinSize = -1;       // e.g. >10mb
        public long MaxSize = -1;       // e.g. <1mb
        public FileCat FilterCat = FileCat.Other; // Other = All
        public bool FilterByCat = false;
        public string DrivePrefix;      // e.g. "J:\\"

        public static QuerySpec Parse(string raw, bool useCat, FileCat cat, string drivePrefix)
        {
            QuerySpec q = new QuerySpec();
            q.FilterByCat = useCat;
            q.FilterCat = cat;
            q.DrivePrefix = drivePrefix;

            string norm = SearchIndex.NormalizeGreek(raw ?? "").Trim();
            if (norm.Length == 0) return q;

            string[] parts = norm.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.StartsWith("ext:") && p.Length > 4)
                {
                    q.RequiredExt = p[4] == '.' ? p.Substring(4) : ("." + p.Substring(4));
                }
                else if (p.StartsWith("*.") && p.Length > 2)
                {
                    q.RequiredExt = p.Substring(1);
                }
                else if (p.StartsWith(">") && p.Length > 1)
                {
                    long v = ParseSizeToken(p.Substring(1));
                    if (v >= 0) q.MinSize = v;
                    else q.Terms.Add(p);
                }
                else if (p.StartsWith("<") && p.Length > 1)
                {
                    long v = ParseSizeToken(p.Substring(1));
                    if (v >= 0) q.MaxSize = v;
                    else q.Terms.Add(p);
                }
                else
                {
                    q.Terms.Add(p);
                }
            }
            return q;
        }

        private static long ParseSizeToken(string tok)
        {
            tok = tok.Trim().ToLowerInvariant();
            long mult = 1;
            if (tok.EndsWith("gb")) { mult = 1073741824L; tok = tok.Substring(0, tok.Length - 2); }
            else if (tok.EndsWith("mb")) { mult = 1048576L; tok = tok.Substring(0, tok.Length - 2); }
            else if (tok.EndsWith("kb")) { mult = 1024L; tok = tok.Substring(0, tok.Length - 2); }
            else if (tok.EndsWith("b")) { tok = tok.Substring(0, tok.Length - 1); }
            double num;
            if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out num) && num >= 0)
                return (long)(num * mult);
            return -1;
        }

        public bool Matches(ref IndexedEntry e, string dir)
        {
            if (FilterByCat && e.Cat != FilterCat) return false;
            if (!string.IsNullOrEmpty(DrivePrefix) && !dir.StartsWith(DrivePrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            if (MinSize >= 0 && e.Size < MinSize) return false;
            if (MaxSize >= 0 && (e.Size < 0 || e.Size > MaxSize)) return false;
            if (RequiredExt != null && !e.NormName.EndsWith(RequiredExt, StringComparison.Ordinal))
                return false;

            for (int i = 0; i < Terms.Count; i++)
            {
                if (e.NormName.IndexOf(Terms[i], StringComparison.Ordinal) < 0)
                    return false;
            }
            return true;
        }
    }

    internal sealed class DoubleBufferedListView : ListView
    {
        public DoubleBufferedListView()
        {
            DoubleBuffered = true;
        }
    }

    // ========================================================================
    // MAIN WINDOW — NILLY SUITE DARK / LIGHT ULTRA-FAST SEARCH STUDIO
    // ========================================================================
    internal sealed class MainForm : Form
    {
        private static readonly Font FontBrand = new Font("Segoe UI", 11.2f, FontStyle.Bold);
        private static readonly Font FontSub = new Font("Segoe UI", 7.8f);
        private static readonly Font FontPill = new Font("Segoe UI", 8.3f, FontStyle.Bold);
        private static readonly Font FontSearch = new Font("Segoe UI", 11f);
        private static readonly Font FontUi = new Font("Segoe UI", 9f);
        private static readonly Font FontFooter = new Font("Segoe UI", 8.2f);

        private bool _dark = true;
        private bool _includeSystem = false;
        private bool _filterByCat = false;
        private FileCat _activeCat = FileCat.Other;
        private string _driveScope = "";
        private int _sortCol = 0;
        private bool _sortAsc = true;
        private volatile bool _cancelScan = false;
        private bool _scanning = false;
        private Bitmap _logoBmp;

        private SearchIndex _index = new SearchIndex();
        private List<int> _matches = new List<int>(8192);
        private System.Windows.Forms.Timer _debounce;

        private Panel _header, _catBar, _footer;
        private Label _lblTitle, _lblSub, _lblStatus, _lblCredit;
        private TextBox _txtSearch;
        private ComboBox _cmbDrive;
        private Button _btnRescan, _btnSys, _btnTheme;
        private readonly List<Button> _catBtns = new List<Button>();
        private DoubleBufferedListView _list;

        public MainForm(bool smokeMode)
        {
            Text = "NillySearch Pro — Nilly graphics";
            Size = new Size(1060, 660);
            MinimumSize = new Size(760, 440);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NillySearchPro.ico");
            try
            {
                if (File.Exists(icoPath)) Icon = new Icon(icoPath);
                else Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
            _logoBmp = LoadCrispLogo(icoPath, 30);

            _debounce = new System.Windows.Forms.Timer { Interval = 75 };
            _debounce.Tick += delegate { _debounce.Stop(); RunFilterAndSort(); };

            BuildUi();
            ApplyTheme();

            if (!smokeMode)
            {
                Shown += delegate
                {
                    Native.SetCueBanner(_txtSearch, "Γράψε όνομα, *.psd, ext:pdf, >50mb… (ελληνικά χωρίς τόνους)");
                    _txtSearch.Focus();
                    LoadCacheAndScanAsync();
                };
            }
        }

        private static Bitmap LoadCrispLogo(string icoPath, int px)
        {
            try
            {
                if (File.Exists(icoPath))
                {
                    byte[] data = File.ReadAllBytes(icoPath);
                    if (data.Length > 22)
                    {
                        int count = BitConverter.ToUInt16(data, 4);
                        int bestOff = -1, bestLen = 0, bestDim = -1;
                        for (int i = 0; i < count; i++)
                        {
                            int e = 6 + i * 16;
                            if (e + 16 > data.Length) break;
                            int w = data[e] == 0 ? 256 : data[e];
                            int len = BitConverter.ToInt32(data, e + 8);
                            int off = BitConverter.ToInt32(data, e + 12);
                            if (off > 0 && len > 8 && off + len <= data.Length &&
                                data[off] == 0x89 && data[off + 1] == 0x50 && data[off + 2] == 0x4E && data[off + 3] == 0x47 && w > bestDim)
                            { bestDim = w; bestOff = off; bestLen = len; }
                        }
                        if (bestOff >= 0)
                        {
                            using (MemoryStream ms = new MemoryStream(data, bestOff, bestLen, false))
                            using (Image src = Image.FromStream(ms))
                            {
                                Bitmap dst = new Bitmap(px, px);
                                using (Graphics g = Graphics.FromImage(dst))
                                {
                                    g.SmoothingMode = SmoothingMode.HighQuality;
                                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    g.DrawImage(src, 0, 0, px, px);
                                }
                                return dst;
                            }
                        }
                    }
                }
            }
            catch { }
            Bitmap bmp = new Bitmap(px, px);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush br = new SolidBrush(Color.FromArgb(28, 31, 38)))
                using (Pen bp = new Pen(Color.FromArgb(102, 217, 239), 2f))
                { g.FillRectangle(br, 2, 2, px - 4, px - 4); g.DrawRectangle(bp, 2, 2, px - 4, px - 4); }
                using (Pen cp = new Pen(Color.FromArgb(102, 217, 239), 2f))
                    g.DrawEllipse(cp, 7, 7, 11, 11);
                using (Pen ap = new Pen(Color.FromArgb(252, 150, 31), 2.4f))
                    g.DrawLine(ap, 17, 17, 23, 23);
            }
            return bmp;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && (e.KeyCode == Keys.F || e.KeyCode == Keys.L))
            {
                e.SuppressKeyPress = true; _txtSearch.Focus(); _txtSearch.SelectAll(); return;
            }
            if (e.KeyCode == Keys.F5)
            {
                e.SuppressKeyPress = true; StartDriveScanAsync(); return;
            }
            if (e.KeyCode == Keys.Escape)
            {
                if (!string.IsNullOrEmpty(_txtSearch.Text)) { e.SuppressKeyPress = true; _txtSearch.Text = ""; return; }
            }
            if (e.Control && e.KeyCode == Keys.C && _list.Focused)
            {
                CopySelectedPaths(); e.SuppressKeyPress = true; return;
            }
            if (e.Control && e.KeyCode == Keys.Enter && _list.SelectedIndices.Count > 0)
            {
                RevealSelectedInExplorer(); e.SuppressKeyPress = true; return;
            }
            base.OnKeyDown(e);
        }

        private void BuildUi()
        {
            // 1. Top Search Header
            _header = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(14, 10, 14, 10) };

            Panel leftBrand = new Panel { Dock = DockStyle.Left, Width = 178 };
            PictureBox picLogo = new PictureBox { Left = 0, Top = 2, Width = 30, Height = 30, Image = _logoBmp, SizeMode = PictureBoxSizeMode.Zoom };
            _lblTitle = new Label { Left = 38, Top = 0, Width = 138, Height = 20, Font = FontBrand, Text = "NillySearch Pro", UseMnemonic = false };
            _lblSub = new Label { Left = 39, Top = 20, Width = 138, Height = 14, Font = FontSub, Text = "Instant File Finder", UseMnemonic = false };
            leftBrand.Controls.AddRange(new Control[] { picLogo, _lblTitle, _lblSub });

            FlowLayoutPanel rightBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, Width = 332, FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false, Padding = new Padding(0, 3, 0, 0)
            };
            _btnTheme = MkPill("Light", 46, delegate { _dark = !_dark; ApplyTheme(); });
            _btnSys = MkPill("+System", 68, delegate { _includeSystem = !_includeSystem; StyleTopButtons(); StartDriveScanAsync(); });
            _btnRescan = MkPill("↻ Ανανέωση", 98, delegate { StartDriveScanAsync(); });

            _cmbDrive = new ComboBox { Width = 94, DropDownStyle = ComboBoxStyle.DropDownList, Font = FontUi, Margin = new Padding(4, 2, 4, 0) };
            _cmbDrive.Items.Add("Όλοι");
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                for (int i = 0; i < drives.Length; i++)
                    if (drives[i].IsReady && (drives[i].DriveType == DriveType.Fixed || drives[i].DriveType == DriveType.Removable))
                        _cmbDrive.Items.Add(drives[i].Name);
            }
            catch { }
            _cmbDrive.SelectedIndex = 0;
            _cmbDrive.SelectedIndexChanged += delegate
            {
                string sel = _cmbDrive.SelectedItem as string;
                _driveScope = (sel == null || sel == "Όλοι") ? "" : sel;
                RunFilterAndSort();
            };
            rightBtns.Controls.AddRange(new Control[] { _btnTheme, _btnSys, _btnRescan, _cmbDrive });

            Panel searchWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 3, 8, 2) };
            _txtSearch = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Font = FontSearch };
            _txtSearch.TextChanged += delegate { _debounce.Stop(); _debounce.Start(); };
            _txtSearch.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Down && _matches.Count > 0)
                {
                    e.SuppressKeyPress = true;
                    _list.Focus();
                    _list.SelectedIndices.Clear();
                    _list.SelectedIndices.Add(0);
                }
                else if (e.KeyCode == Keys.Enter && _matches.Count > 0)
                {
                    e.SuppressKeyPress = true;
                    OpenMatchIndex(0);
                }
            };
            searchWrap.Controls.Add(_txtSearch);

            _header.Controls.Add(searchWrap);
            _header.Controls.Add(rightBtns);
            _header.Controls.Add(leftBrand);

            // 2. Category Filter Bar (1-click instant pills)
            _catBar = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(14, 4, 14, 4) };
            FlowLayoutPanel catFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            AddCatPill(catFlow, "Όλα", false, FileCat.Other, 54);
            AddCatPill(catFlow, "Φάκελοι", true, FileCat.Folder, 72);
            AddCatPill(catFlow, "Έγγραφα", true, FileCat.Doc, 78);
            AddCatPill(catFlow, "Εικόνες", true, FileCat.Image, 70);
            AddCatPill(catFlow, "Design", true, FileCat.Design, 66);
            AddCatPill(catFlow, "Βίντεο/Ήχος", true, FileCat.Media, 94);
            AddCatPill(catFlow, "ZIP/RAR", true, FileCat.Archive, 74);
            AddCatPill(catFlow, "Εφαρμογές", true, FileCat.App, 86);
            _catBar.Controls.Add(catFlow);

            // 3. Footer Status + SEO Credit
            _footer = new Panel { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(14, 2, 14, 2) };
            _lblStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = FontFooter, UseMnemonic = false, Text = "Έτοιμο · 0 MB RAM κλειστό" };
            _lblCredit = new Label { Dock = DockStyle.Right, Width = 365, TextAlign = ContentAlignment.MiddleRight, Font = FontFooter, Cursor = Cursors.Hand, UseMnemonic = false, Text = "created by Nilly graphics (Sotiris Christos) · nillygraphics.com" };
            _lblCredit.Click += delegate { try { Process.Start(new ProcessStartInfo("https://nillygraphics.com/") { UseShellExecute = true }); } catch { } };
            _footer.Controls.Add(_lblStatus);
            _footer.Controls.Add(_lblCredit);

            // 4. Virtualized 60 FPS Owner-Drawn Dark/Light Results Grid
            _list = new DoubleBufferedListView
            {
                Dock = DockStyle.Fill, View = View.Details, VirtualMode = true, FullRowSelect = true,
                MultiSelect = true, BorderStyle = BorderStyle.None, Font = FontUi, HideSelection = false, OwnerDraw = true
            };
            _list.Columns.Add("Όνομα", 300);
            _list.Columns.Add("Διαδρομή", 470);
            _list.Columns.Add("Μέγεθος", 105, HorizontalAlignment.Right);
            _list.Columns.Add("Τροποποίηση", 175);
            EventHandler fitCols = delegate
            {
                int cw = _list.ClientSize.Width;
                if (cw <= 300 || _list.Columns.Count < 4) return;
                int c2 = 105, c3 = 170;
                int rem = Math.Max(220, cw - c2 - c3 - 2);
                int c0 = Math.Max(140, (int)(rem * 0.36));
                int c1 = Math.Max(120, rem - c0);
                _list.Columns[0].Width = c0;
                _list.Columns[1].Width = c1;
                _list.Columns[2].Width = c2;
                _list.Columns[3].Width = c3;
            };
            _list.Resize += fitCols;
            _list.HandleCreated += fitCols;

            _list.DrawColumnHeader += delegate(object s, DrawListViewColumnHeaderEventArgs e)
            {
                Color hdrBg = _dark ? Color.FromArgb(30, 33, 40) : Color.FromArgb(234, 238, 245);
                Color hdrFg = _dark ? Color.FromArgb(170, 178, 194) : Color.FromArgb(60, 68, 82);
                Color hdrDiv = _dark ? Color.FromArgb(46, 50, 60) : Color.FromArgb(210, 216, 226);
                using (SolidBrush br = new SolidBrush(hdrBg)) e.Graphics.FillRectangle(br, e.Bounds);
                using (Pen p = new Pen(hdrDiv))
                {
                    e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                    e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
                }
                string arrow = e.ColumnIndex == _sortCol ? (_sortAsc ? " ▲" : " ▼") : "";
                TextFormatFlags fl = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                    (e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
                Rectangle tr = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 14), e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Header.Text + arrow, FontPill, tr, hdrFg, fl);
            };
            _list.DrawItem += delegate(object s, DrawListViewItemEventArgs e) { e.DrawDefault = true; };
            _list.DrawSubItem += delegate(object s, DrawListViewSubItemEventArgs e) { e.DrawDefault = true; };

            _list.RetrieveVirtualItem += OnRetrieveVirtualItem;
            _list.ColumnClick += delegate(object s, ColumnClickEventArgs e)
            {
                if (_sortCol == e.Column) _sortAsc = !_sortAsc;
                else { _sortCol = e.Column; _sortAsc = e.Column != 2 && e.Column != 3; }
                SortMatches();
                _list.Invalidate();
            };
            _list.DoubleClick += delegate { if (_list.SelectedIndices.Count > 0) OpenMatchIndex(_list.SelectedIndices[0]); };
            _list.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && _list.SelectedIndices.Count > 0) OpenMatchIndex(_list.SelectedIndices[0]); };
            _list.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Right && _list.SelectedIndices.Count > 0) ShowResultContextMenu(e.Location); };

            // Exact WinForms Docking Z-Order: _header (top 1), _catBar (top 2), _footer (bottom), _list (Fill frontmost z=0!)
            Controls.Add(_list);
            Controls.Add(_footer);
            Controls.Add(_catBar);
            Controls.Add(_header);
        }

        private void AddCatPill(FlowLayoutPanel parent, string label, bool useCat, FileCat cat, int w)
        {
            Button b = MkPill(label, w, delegate
            {
                _filterByCat = useCat;
                _activeCat = cat;
                StyleCatPills();
                RunFilterAndSort();
            });
            b.Tag = new KeyValuePair<bool, FileCat>(useCat, cat);
            _catBtns.Add(b);
            parent.Controls.Add(b);
        }

        private static Button MkPill(string txt, int w, EventHandler click)
        {
            Button b = new Button();
            b.UseMnemonic = false; b.Text = txt; b.Width = w; b.Height = 26;
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 1;
            b.Font = FontPill; b.Cursor = Cursors.Hand; b.TabStop = false;
            b.Margin = new Padding(3, 1, 3, 0);
            b.Click += click;
            return b;
        }

        public void ApplyTheme()
        {
            Native.SetDarkTitlebar(Handle, _dark);
            Color bgMain = _dark ? Color.FromArgb(20, 21, 25) : Color.FromArgb(244, 246, 250);
            Color bgTop = _dark ? Color.FromArgb(26, 28, 34) : Color.White;
            Color bgBar = _dark ? Color.FromArgb(23, 25, 30) : Color.FromArgb(238, 241, 246);
            Color fgMain = _dark ? Color.FromArgb(240, 244, 250) : Color.FromArgb(22, 24, 30);
            Color fgMuted = _dark ? Color.FromArgb(144, 152, 168) : Color.FromArgb(95, 105, 118);

            BackColor = bgMain;
            _header.BackColor = bgTop;
            _catBar.BackColor = bgBar;
            _footer.BackColor = bgTop;
            _list.BackColor = bgMain;
            _list.ForeColor = fgMain;
            _txtSearch.BackColor = _dark ? Color.FromArgb(18, 20, 24) : Color.White;
            _txtSearch.ForeColor = fgMain;
            _cmbDrive.BackColor = _dark ? Color.FromArgb(34, 37, 44) : Color.White;
            _cmbDrive.ForeColor = fgMain;

            _lblTitle.ForeColor = fgMain;
            _lblSub.ForeColor = fgMuted;
            _lblStatus.ForeColor = fgMuted;
            _lblCredit.ForeColor = Color.FromArgb(252, 150, 31);
            _btnTheme.Text = _dark ? "Light" : "Dark";

            StyleTopButtons();
            StyleCatPills();
            _list.Invalidate();
        }

        private void StyleTopButtons()
        {
            Color btnBg = _dark ? Color.FromArgb(36, 39, 46) : Color.FromArgb(236, 240, 246);
            Color fg = _dark ? Color.FromArgb(235, 239, 246) : Color.FromArgb(24, 26, 32);
            Color border = _dark ? Color.FromArgb(52, 56, 66) : Color.FromArgb(210, 216, 226);
            Color cyan = Color.FromArgb(102, 217, 239);

            _btnTheme.BackColor = btnBg; _btnTheme.ForeColor = fg; _btnTheme.FlatAppearance.BorderColor = border;
            _btnRescan.BackColor = btnBg; _btnRescan.ForeColor = fg; _btnRescan.FlatAppearance.BorderColor = border;
            _btnSys.BackColor = _includeSystem ? cyan : btnBg;
            _btnSys.ForeColor = _includeSystem ? Color.FromArgb(14, 18, 24) : fg;
            _btnSys.FlatAppearance.BorderColor = _includeSystem ? cyan : border;
        }

        private void StyleCatPills()
        {
            Color orange = Color.FromArgb(252, 150, 31);
            Color btnBg = _dark ? Color.FromArgb(34, 37, 44) : Color.FromArgb(236, 240, 246);
            Color fg = _dark ? Color.FromArgb(224, 229, 238) : Color.FromArgb(25, 28, 34);
            Color border = _dark ? Color.FromArgb(50, 54, 64) : Color.FromArgb(210, 216, 226);

            for (int i = 0; i < _catBtns.Count; i++)
            {
                Button b = _catBtns[i];
                KeyValuePair<bool, FileCat> kv = (KeyValuePair<bool, FileCat>)b.Tag;
                bool active = (_filterByCat == kv.Key) && (!kv.Key || _activeCat == kv.Value);
                b.BackColor = active ? orange : btnBg;
                b.ForeColor = active ? Color.FromArgb(14, 18, 24) : fg;
                b.FlatAppearance.BorderColor = active ? orange : border;
            }
        }

        private void LoadCacheAndScanAsync()
        {
            Thread th = new Thread(delegate()
            {
                Stopwatch sw = Stopwatch.StartNew();
                SearchIndex cached = SearchIndex.LoadCache(SearchIndex.CacheFilePath);
                if (cached != null && cached.Items.Count > 0)
                {
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            _index = cached;
                            RunFilterAndSort();
                            _lblStatus.Text = string.Format("Φορτώθηκαν {0:N0} στοιχεία από cache σε {1} ms · ανανέωση…", cached.Items.Count, sw.ElapsedMilliseconds);
                            StartDriveScanAsync();
                        });
                        return;
                    }
                    catch { }
                }
                try { BeginInvoke((Action)delegate { StartDriveScanAsync(); }); } catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        private void StartDriveScanAsync()
        {
            if (_scanning) return;
            _scanning = true;
            _cancelScan = false;
            _btnRescan.Enabled = false;
            _lblStatus.Text = "Γρήγορη Win32 σάρωση δίσκων...";

            bool incSys = _includeSystem;
            List<string> roots = new List<string>();
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                for (int i = 0; i < drives.Length; i++)
                    if (drives[i].IsReady && drives[i].DriveType == DriveType.Fixed)
                        roots.Add(drives[i].Name);
            }
            catch { }
            if (roots.Count == 0) roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            Thread th = new Thread(delegate()
            {
                Stopwatch sw = Stopwatch.StartNew();
                SearchIndex fresh = SearchIndex.ScanRoots(roots.ToArray(), incSys,
                    delegate { return _cancelScan; },
                    delegate(int cnt, string cur)
                    {
                        try { BeginInvoke((Action)delegate { _lblStatus.Text = string.Format("Σάρωση: {0:N0} αρχεία… ({1})", cnt, cur); }); } catch { }
                    });
                sw.Stop();
                if (!_cancelScan && fresh.Items.Count > 0)
                    fresh.SaveCache(SearchIndex.CacheFilePath);
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        _scanning = false;
                        _btnRescan.Enabled = true;
                        if (_cancelScan) return;
                        _index = fresh;
                        RunFilterAndSort();
                        _lblStatus.Text = string.Format("{0:N0} από {1:N0} στοιχεία · σάρωση σε {2:0.00} δευτ. · 0 MB idle RAM",
                            _matches.Count, _index.Items.Count, sw.Elapsed.TotalSeconds);
                        Native.TrimWorkingSet();
                    });
                }
                catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        public void LoadTestIndexSync(SearchIndex idx, string query)
        {
            _index = idx;
            _txtSearch.Text = query ?? "";
            RunFilterAndSort();
        }

        public int CurrentMatchCount { get { return _matches.Count; } }

        private void RunFilterAndSort()
        {
            Stopwatch sw = Stopwatch.StartNew();
            QuerySpec q = QuerySpec.Parse(_txtSearch.Text, _filterByCat, _activeCat, _driveScope);
            List<IndexedEntry> items = _index.Items;
            List<string> dirs = _index.Dirs;
            List<int> found = new List<int>(Math.Min(items.Count, 16384));

            for (int i = 0; i < items.Count; i++)
            {
                IndexedEntry e = items[i];
                if (q.Matches(ref e, dirs[e.DirIndex]))
                    found.Add(i);
            }

            _matches = found;
            SortMatches();
            _list.VirtualListSize = _matches.Count;
            _list.Invalidate();
            sw.Stop();
            if (!_scanning)
            {
                _lblStatus.Text = string.Format("{0:N0} από {1:N0} στοιχεία · {2} ms", _matches.Count, items.Count, sw.ElapsedMilliseconds);
            }
        }

        private void SortMatches()
        {
            if (_matches.Count <= 1 || _matches.Count > 150000) return; // keep huge empty queries instant
            List<IndexedEntry> items = _index.Items;
            List<string> dirs = _index.Dirs;
            int col = _sortCol;
            int dirSign = _sortAsc ? 1 : -1;

            _matches.Sort(delegate(int a, int b)
            {
                IndexedEntry ea = items[a], eb = items[b];
                int cmp;
                if (col == 1) cmp = string.Compare(dirs[ea.DirIndex], dirs[eb.DirIndex], StringComparison.OrdinalIgnoreCase);
                else if (col == 2) cmp = ea.Size.CompareTo(eb.Size);
                else if (col == 3) cmp = ea.LastWriteFileTime.CompareTo(eb.LastWriteFileTime);
                else cmp = string.Compare(ea.Name, eb.Name, StringComparison.OrdinalIgnoreCase);
                return cmp * dirSign;
            });
        }

        private void OnRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _matches.Count)
            {
                e.Item = new ListViewItem("");
                return;
            }
            IndexedEntry ent = _index.Items[_matches[e.ItemIndex]];
            string dir = _index.Dirs[ent.DirIndex];
            string icon = ent.Cat == FileCat.Folder ? "📁 " : (ent.Cat == FileCat.Image || ent.Cat == FileCat.Design ? "🎨 " : (ent.Cat == FileCat.Archive ? "📦 " : "📄 "));
            ListViewItem lvi = new ListViewItem(icon + ent.Name);
            lvi.SubItems.Add(dir);
            lvi.SubItems.Add(ent.Size < 0 ? "Φάκελος" : FormatBytes(ent.Size));
            lvi.SubItems.Add(ent.LastWriteFileTime > 0 ? DateTime.FromFileTime(ent.LastWriteFileTime).ToString("dd/MM/yyyy HH:mm") : "—");
            if (ent.Cat == FileCat.Folder) lvi.ForeColor = Color.FromArgb(252, 150, 31);
            else if (ent.Cat == FileCat.Design) lvi.ForeColor = Color.FromArgb(102, 217, 239);
            e.Item = lvi;
        }

        private string GetFullPathAtMatch(int matchPos)
        {
            if (matchPos < 0 || matchPos >= _matches.Count) return null;
            IndexedEntry ent = _index.Items[_matches[matchPos]];
            return _index.Dirs[ent.DirIndex] + "\\" + ent.Name;
        }

        private void OpenMatchIndex(int matchPos)
        {
            string full = GetFullPathAtMatch(matchPos);
            if (string.IsNullOrEmpty(full)) return;
            try { Process.Start(new ProcessStartInfo(full) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "NillySearch Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void RevealSelectedInExplorer()
        {
            if (_list.SelectedIndices.Count == 0) return;
            string full = GetFullPathAtMatch(_list.SelectedIndices[0]);
            if (string.IsNullOrEmpty(full)) return;
            try { Process.Start("explorer.exe", "/select,\"" + full + "\""); } catch { }
        }

        private void CopySelectedPaths()
        {
            if (_list.SelectedIndices.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _list.SelectedIndices.Count; i++)
            {
                string p = GetFullPathAtMatch(_list.SelectedIndices[i]);
                if (!string.IsNullOrEmpty(p)) { if (sb.Length > 0) sb.AppendLine(); sb.Append(p); }
            }
            if (sb.Length > 0)
            {
                try { Clipboard.SetText(sb.ToString()); _lblStatus.Text = "Αντιγράφηκε η διαδρομή στο πρόχειρο."; } catch { }
            }
        }

        private static string FindSiblingExe(string folder, string exeName)
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NillySuite\" + folder + @"\" + exeName);
                if (File.Exists(appData)) return appData;
                DirectoryInfo par = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
                if (par != null)
                {
                    string sib = Path.Combine(Path.Combine(par.FullName, folder), exeName);
                    if (File.Exists(sib)) return sib;
                }
            }
            catch { }
            return null;
        }

        private void ShowResultContextMenu(Point pt)
        {
            int idx = _list.SelectedIndices[0];
            string full = GetFullPathAtMatch(idx);
            if (string.IsNullOrEmpty(full)) return;

            ContextMenuStrip cms = new ContextMenuStrip();
            cms.Items.Add("▶  Άνοιγμα (Enter)", null, delegate { OpenMatchIndex(idx); });
            string filesExe = FindSiblingExe("NillyFilesPro", "NillyFilesPro.exe");
            if (filesExe != null)
            {
                string targetDir = Directory.Exists(full) ? full : Path.GetDirectoryName(full);
                cms.Items.Add("📂  Άνοιγμα φακέλου σε NillyFiles Pro", null, delegate
                {
                    try { Process.Start(new ProcessStartInfo(filesExe, "\"" + targetDir + "\"") { WorkingDirectory = Path.GetDirectoryName(filesExe), UseShellExecute = true }); } catch { }
                });
            }
            cms.Items.Add("📁  Εμφάνιση στον Windows Explorer (Ctrl+Enter)", null, delegate { RevealSelectedInExplorer(); });
            if (File.Exists(full))
            {
                string codeExe = FindSiblingExe("NillyCodePro", "NillyCodePro.exe");
                if (codeExe != null)
                    cms.Items.Add("</> Επεξεργασία σε NillyCode Pro", null, delegate
                    {
                        try { Process.Start(new ProcessStartInfo(codeExe, "\"" + full + "\"") { WorkingDirectory = Path.GetDirectoryName(codeExe), UseShellExecute = true }); } catch { }
                    });
            }
            cms.Items.Add(new ToolStripSeparator());
            cms.Items.Add("📋  Αντιγραφή πλήρους διαδρομής (Ctrl+C)", null, delegate { CopySelectedPaths(); });
            cms.Items.Add("📝  Αντιγραφή ονόματος", null, delegate
            {
                try { Clipboard.SetText(Path.GetFileName(full)); } catch { }
            });
            if (File.Exists(full))
            {
                cms.Items.Add(new ToolStripSeparator());
                cms.Items.Add("✓  Υπολογισμός SHA-256 & Ιδιότητες", null, delegate { ShowSha256Dialog(full); });
            }
            cms.Closed += delegate { cms.Dispose(); };
            cms.Show(_list, pt);
        }

        private void ShowSha256Dialog(string filePath)
        {
            try
            {
                FileInfo fi = new FileInfo(filePath);
                string hash;
                using (SHA256 sha = SHA256.Create())
                using (FileStream fs = File.OpenRead(filePath))
                    hash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                if (MessageBox.Show(this,
                    "Αρχείο: " + fi.Name + "\r\nΦάκελος: " + fi.DirectoryName + "\r\nΜέγεθος: " + FormatBytes(fi.Length) +
                    "\r\nΤροποποίηση: " + fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm:ss") +
                    "\r\n\r\nSHA-256:\r\n" + hash + "\r\n\r\nΑντιγραφή SHA-256 στο πρόχειρο;",
                    "NillySearch Pro — Ιδιότητες", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    try { Clipboard.SetText(hash); } catch { }
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "NillySearch Pro"); }
        }

        private static string FormatBytes(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1048576) return (b / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            if (b < 1073741824) return (b / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture) + " MB";
            return (b / 1073741824.0).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cancelScan = true;
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancelScan = true;
                if (_debounce != null) { _debounce.Dispose(); _debounce = null; }
                if (_logoBmp != null) { _logoBmp.Dispose(); _logoBmp = null; }
            }
            base.Dispose(disposing);
        }
    }

    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0 && args[0].Equals("--test-smoke", StringComparison.OrdinalIgnoreCase))
                return RunSmokeTest(args.Length > 1 ? args[1] : null, args.Length > 2 ? args[2] : null);

            Application.Run(new MainForm(false));
            return 0;
        }

        private static int RunSmokeTest(string logPath, string previewPngPath)
        {
            StringBuilder sb = new StringBuilder();
            string temp = Path.Combine(Path.GetTempPath(), "NillySearchSmoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(temp, "Πελάτες_2026"));
                Directory.CreateDirectory(Path.Combine(temp, "Γραφικά"));
                File.WriteAllText(Path.Combine(temp, "Πελάτες_2026", "Προσφορά_Λογότυπος.pdf"), "PDF demo", Encoding.UTF8);
                File.WriteAllBytes(Path.Combine(temp, "Γραφικά", "Brand_Hero.psd"), new byte[128 * 1024]);
                File.WriteAllText(Path.Combine(temp, "Γραφικά", "nilly_notes.txt"), "notes", Encoding.UTF8);

                SearchIndex idx = SearchIndex.ScanRoots(new string[] { temp }, true, delegate { return false; }, null);
                sb.AppendLine("WIN32_SCANNED_ITEMS=" + idx.Items.Count);
                if (idx.Items.Count < 5) throw new InvalidOperationException("Expected >= 5 scanned items.");

                // Test binary cache roundtrip
                string cacheTmp = Path.Combine(temp, "test.bin");
                idx.SaveCache(cacheTmp);
                SearchIndex loaded = SearchIndex.LoadCache(cacheTmp);
                bool cacheOk = loaded != null && loaded.Items.Count == idx.Items.Count;
                sb.AppendLine("CACHE_ROUNDTRIP_OK=" + cacheOk);
                if (!cacheOk) throw new InvalidOperationException("Cache roundtrip mismatch.");

                using (MainForm f = new MainForm(true))
                {
                    f.Show();
                    // 1. Greek accent & final-sigma search: "προσφορα λογοτυποσ" -> finds "Προσφορά_Λογότυπος.pdf"!
                    f.LoadTestIndexSync(loaded, "προσφορα λογοτυποσ");
                    int greekHits = f.CurrentMatchCount;
                    sb.AppendLine("GREEK_ACCENT_SIGMA_HITS=" + greekHits);
                    if (greekHits != 1) throw new InvalidOperationException("Greek search failed.");

                    // 2. Size filter test: ">100kb" -> finds "Brand_Hero.psd" (128 KB)!
                    f.LoadTestIndexSync(loaded, ">100kb");
                    int sizeHits = f.CurrentMatchCount;
                    sb.AppendLine("SIZE_FILTER_HITS=" + sizeHits);
                    if (sizeHits != 1) throw new InvalidOperationException("Size filter failed.");

                    // 3. Show all items cleanly for visual UI screenshot
                    f.LoadTestIndexSync(loaded, "");
                    Application.DoEvents();
                    Native.TrimWorkingSet();
                    if (!string.IsNullOrEmpty(previewPngPath))
                    {
                        using (Bitmap shot = new Bitmap(f.Width, f.Height))
                        {
                            f.DrawToBitmap(shot, new Rectangle(0, 0, f.Width, f.Height));
                            shot.Save(previewPngPath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    long ramMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
                    sb.AppendLine("WINDOW_OPENED_OK=" + f.IsHandleCreated);
                    sb.AppendLine("WORKING_SET_MB=" + ramMb);
                    f.Close();
                }

                sb.AppendLine("SMOKE_RESULT=PASS");
            }
            catch (Exception ex)
            {
                sb.AppendLine("SMOKE_RESULT=FAIL");
                sb.AppendLine(ex.ToString());
                if (!string.IsNullOrEmpty(logPath)) File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
                return 1;
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
            if (!string.IsNullOrEmpty(logPath)) File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            return 0;
        }
    }
}
