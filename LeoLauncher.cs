using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Leo Music")]
[assembly: AssemblyDescription("Leo Music Native Windows Studio - Auto-Synced BetterLyrics & Full Studio Panel")]
[assembly: AssemblyCompany("Leo Music Studio")]
[assembly: AssemblyProduct("Leo Music")]
[assembly: AssemblyVersion("10.1.0.0")]
[assembly: AssemblyFileVersion("10.1.0.0")]

namespace LeoMusicNative
{
    public class LyricLine
    {
        public double StartSec;
        public double EndSec;
        public string Text;
        public bool IsExactLrcTimestamp;

        public LyricLine(double start, double end, string text, bool isExact)
        {
            StartSec = start;
            EndSec = end;
            Text = text;
            IsExactLrcTimestamp = isExact;
        }
    }

    public class SongItem
    {
        public string Id;
        public string Title;
        public string Artist;
        public string Album;
        public int Duration;
        public string Mood;
        public string ThumbUrl;
        public string AudioUrl;
        public string FallbackUrl;
        public string FallbackUrl96;
        public string Lyrics;
        public List<LyricLine> SyncedLines;
        public double LastCalibratedDuration;

        public SongItem(string id, string title, string artist, string album, int duration, string mood, string thumbUrl, string audioUrl, string fallbackUrl, string lyrics)
        {
            Id = id;
            Title = title;
            Artist = artist;
            Album = album;
            Duration = duration;
            Mood = mood;
            ThumbUrl = thumbUrl;
            AudioUrl = audioUrl;
            FallbackUrl = fallbackUrl;
            FallbackUrl96 = (fallbackUrl ?? "").Replace("_160.mp4", "_96.mp4").Replace("_320.mp4", "_96.mp4");
            Lyrics = lyrics;
            SyncedLines = new List<LyricLine>();
            LastCalibratedDuration = duration;
        }
    }

    public class ThemeItem
    {
        public string Name;
        public Color Accent;
        public ThemeItem(string name, Color accent)
        {
            Name = name;
            Accent = accent;
        }
    }

    public class LeoMainForm : Form
    {
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        // Core Colors
        private Color bgMain = Color.FromArgb(9, 11, 16);
        private Color bgSidebar = Color.FromArgb(14, 17, 24);
        private Color bgCard = Color.FromArgb(20, 24, 34);
        private Color bgCardHover = Color.FromArgb(28, 33, 47);
        private Color bgPlayer = Color.FromArgb(12, 14, 21);
        private Color accentColor = Color.FromArgb(255, 193, 7);
        private Color textWhite = Color.FromArgb(248, 250, 252);
        private Color textSub = Color.FromArgb(148, 163, 184);
        private Color textMuted = Color.FromArgb(100, 116, 139);

        // Ambient Album-Art Color Interpolation (for BetterLyrics & Full View)
        private float ambR = 92f, ambG = 48f, ambB = 88f;
        private float targetAmbR = 92f, targetAmbG = 48f, targetAmbB = 88f;
        private Dictionary<string, Color> dominantColorCache = new Dictionary<string, Color>();

        // Smooth UI & Button Animations
        private float fullPlayerAnim = 0f; // 0.0 = closed, 1.0 = fully open
        private bool isFullPlayerExpanded = false;
        private float tabFadeAnim = 1f;
        private float playBtnPulse = 0f;
        private Dictionary<string, float> btnHoverAnim = new Dictionary<string, float>();
        private Dictionary<string, float> btnClickBounce = new Dictionary<string, float>();
        private string currentHoveredBtnKey = "";

        // BetterLyrics Smooth Scroll, Sub-Frame Sync Clock & State
        private float lyricsSmoothScrollY = 0f;
        private double smoothPosSec = 0.0;
        private double lyricSyncOffsetSec = 0.0;
        private string lyricsSyncSourceLabel = "Auto-Synced";
        private bool isLoadingBetterLyrics = false;
        private int lyricsFetchVersion = 0;
        private List<Rectangle> activeLyricHitRects = new List<Rectangle>();
        private List<double> activeLyricHitTimes = new List<double>();
        private Rectangle syncMinusBtnRect = Rectangle.Empty;
        private Rectangle syncResetBtnRect = Rectangle.Empty;
        private Rectangle syncPlusBtnRect = Rectangle.Empty;

        // Quick Play State (Unlocked after user plays songs; persisted & refreshed with new songs on startup)
        private bool quickPlayUnlocked = false;
        private int userPlayedSongsCount = 0;
        private List<SongItem> quickPlaySongs = new List<SongItem>();
        private List<string> favoritePlayedArtists = new List<string>();
        private int hoveredQuickPlayIndex = -1;

        // Catalog, Search, Queue, Downloads
        private List<SongItem> catalog = new List<SongItem>();
        private List<SongItem> displaySongs = new List<SongItem>();
        private List<SongItem> queueSongs = new List<SongItem>();
        private List<SongItem> downloadedSongs = new List<SongItem>();
        private List<SongItem> liveSearchResults = new List<SongItem>();
        private HashSet<string> likedIds = new HashSet<string>();
        private HashSet<string> manualQueuedIds = new HashSet<string>();
        private Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
        private HashSet<string> loadingImages = new HashSet<string>();

        private List<ThemeItem> themes = new List<ThemeItem>();
        private int activeThemeIdx = 0;

        // Navigation & Panel State
        private int activeTab = 0; // 0=Home, 1=Search, 2=Liked, 3=Downloads, 4=Settings & EQ
        private int rightPanelMode = 1; // Default 1 = BetterLyrics view in right panel, 0 = Queue
        private int fullViewSubMode = 1; // 1 = Studio Artwork + BetterLyrics Split, 2 = Cinema Full BetterLyrics, 0 = Studio + Queue
        private string selectedMood = "All";
        private string[] moods = new string[] { "All", "Party", "Energize", "Chill", "Relax", "Workout", "Focus" };

        // Playback State
        private SongItem currentSong = null;
        private int currentQueueIndex = 0;
        private bool isPlaying = false;
        private bool isBuffering = false;
        private bool isShuffle = false;
        private bool isRepeatOne = false;
        private bool isMuted = false;
        private int volumePercent = 85;
        private double currentPosSec = 0;
        private double currentDurSec = 226;

        // Equalizer, Crossfade & Studio Settings
        private bool eqEnabled = true;
        private string activeEqPreset = "Bass Boost";
        private string[] eqPresetNames = new string[] { "Flat", "Bass Boost", "Club", "Hip Hop", "Rock", "Pop", "Vocal Boost", "Acoustic", "Electronic" };
        private float[] eqBands = new float[] { 6.5f, 4.5f, 1.0f, 2.5f, 4.0f };
        private string[] eqBandLabels = new string[] { "60Hz", "230Hz", "910Hz", "3.6kHz", "14kHz" };
        private int bassBoostPct = 65;
        private int virtualizerPct = 50;
        private int loudnessGainPct = 40;

        private bool crossfadeEnabled = true;
        private int crossfadeSeconds = 3;
        private bool gaplessPlayback = true;
        private bool skipSilence = false;
        private bool autoMixEnabled = true;
        private bool oledPureBlack = false;
        private bool wavySliderEnabled = true;
        private int artworkShapeMode = 0;
        private string[] artworkShapeNames = new string[] { "Morph", "Flower", "Vinyl", "Squircle" };

        private int sleepTimerMinutes = 0;
        private int sleepTimerSecondsLeft = 0;
        private int sleepTickCounter = 0;

        // Dual-Deck WMP Audio Engine
        private object wmpPlayer;
        private Type wmpType;
        private object wmpControls;
        private object wmpSettings;

        private object wmpDeckB;
        private object wmpControlsB;
        private object wmpSettingsB;
        private int deckBVolume = 0;
        private int deckAFadeInProgress = 100;
        private bool isAutoEndCrossfadeTriggered = false;

        // Persistence & Folders
        private string statusMessage = "";
        private int statusTick = 0;
        private string downloadingSongId = "";
        private int downloadProgress = 0;
        private string downloadsFolder;
        private string cacheFolder;
        private string settingsFilePath;
        private string localSettingsBackupPath;

        // Animation Timers & Counters
        private float wavePhase = 0f;
        private float vinylAngle = 0f;
        private float morphPhase = 0f;
        private float spinnerAngle = 0f;
        private System.Windows.Forms.Timer uiTimer;
        private System.Windows.Forms.Timer searchDebounceTimer;
        private int searchRequestVersion = 0;
        private int playRequestVersion = 0;
        private int smartQueueVersion = 0;

        // v10.1.0 IN-APP AUTO-UPDATE ENGINE (Connected to umas8549/leo-music-updates) & PASSWORD LOCK ("Gourav123")
        private const string CURRENT_APP_VERSION = "10.1.0";
        private const string ADMIN_UPDATE_PASSWORD = "Gourav123";
        private const string DEFAULT_GITHUB_VERSION_URL = "https://raw.githubusercontent.com/umas8549/leo-music-updates/main/version.json";
        private const string DEFAULT_GITHUB_SOURCE_URL = "https://raw.githubusercontent.com/umas8549/leo-music-updates/main/LeoLauncher.cs";
        private const string DEFAULT_GITHUB_RELEASE_URL = "https://github.com/umas8549/leo-music-updates/releases/download/v10.1.0/Leo_Music_Windows_v10.1.0.zip";

        private string updateManifestUrl = "https://raw.githubusercontent.com/umas8549/leo-music-updates/main/version.json";
        private bool isUpdatePopupOpen = false;
        private float updatePopupAnim = 0f;
        private bool isCheckingUpdate = false;
        private bool isInstallingUpdate = false;
        private bool isPasswordPromptActive = false;
        private bool isAdminUpdateUnlocked = false;
        private TextBox adminPassBox;
        private int startupUpdateCheckTicks = 85;
        private string latestRemoteVersion = "10.1.0";
        private string latestUpdateTitle = "Leo Music Studio v10.1.0 Update";
        private string latestUpdateNotes = "• Connected Live Auto-Updater to umas8549/leo-music-updates\n• Automatic Version Stamping & Multi-Location EXE Updater\n• Enhanced BetterLyrics 35-FPS Sub-Frame Auto-Sync & Full Studio Panel";
        private string latestUpdateSourceUrl = "https://raw.githubusercontent.com/umas8549/leo-music-updates/main/LeoLauncher.cs";
        private string latestUpdateDownloadUrl = "https://github.com/umas8549/leo-music-updates/releases/download/v10.1.0/Leo_Music_Windows_v10.1.0.zip";
        private Rectangle updateInstallBtnRect = Rectangle.Empty;
        private Rectangle updateLaterBtnRect = Rectangle.Empty;
        private Rectangle updateEditUrlBtnRect = Rectangle.Empty;

        // Search Box
        private TextBox searchBox;
        private bool isSearchEditing = false;
        private string searchQueryText = "";
        private bool isSearchingNetwork = false;

        // Hit & Scroll State
        private int hoveredSongIndex = -1;
        private int hoveredQueueIndex = -1;
        private int hoveredNavIndex = -1;
        private int scrollOffset = 0;
        private int queueScrollOffset = 0;

        // Cached Fonts for High-FPS Optimization
        private Font fontBrand = new Font("Segoe UI", 15f, FontStyle.Bold);
        private Font fontBetterHeader = new Font("Georgia", 9.5f, FontStyle.Italic);
        private Font fontLyricsFullActive = new Font("Georgia", 20.5f, FontStyle.Bold);
        private Font fontLyricsFullIdle = new Font("Georgia", 18.5f, FontStyle.Bold);
        private Font fontLyricsSideActive = new Font("Georgia", 12.5f, FontStyle.Bold);
        private Font fontLyricsSideIdle = new Font("Georgia", 11.5f, FontStyle.Bold);
        private Font fontTitleBold = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        private Font fontSubReg = new Font("Segoe UI", 9f, FontStyle.Regular);
        private Font fontSmallBold = new Font("Segoe UI", 8.5f, FontStyle.Bold);

        // Window Drag
        private bool isDraggingWindow = false;
        private Point dragStartPoint;
        private Icon customAppIcon;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x00020000;
                cp.Style |= 0x00080000;
                return cp;
            }
        }

        public LeoMainForm()
        {
            try { SetCurrentProcessExplicitAppUserModelID("LeoMusic.NativeStudio.Desktop.v10_1"); } catch { }

            this.Text = "Leo Music";
            this.Size = new Size(1220, 770);
            this.MinimumSize = new Size(980, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = true;

            ApplyCustomTaskbarAndWindowIcon();

            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = bgMain;
            this.KeyPreview = true;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);

            string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeoMusic");
            try { if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir); } catch { }

            settingsFilePath = Path.Combine(appDataDir, "user_settings.cfg");
            localSettingsBackupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_settings.cfg");

            cacheFolder = Path.Combine(appDataDir, "AudioCache");
            try { if (!Directory.Exists(cacheFolder)) Directory.CreateDirectory(cacheFolder); } catch { }

            downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(downloadsFolder))
            {
                downloadsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            }

            InitThemes();
            InitCatalog();
            InitSearchInput();
            InitDualWmpAudioEngine();

            // Default initial queue before loading saved settings
            queueSongs.AddRange(catalog);
            currentSong = queueSongs[0];
            currentDurSec = currentSong.Duration;

            // Restore ALL saved settings, last played song, queue, and Quick Play state from disk!
            LoadPersistentSettings();
            LoadAndLockProtectedUpdateConfig();

            EnsureSyncedLyricsParsed(currentSong);
            FetchBetterLyricsAsync(currentSong);
            UpdateTargetAmbientColorFromSong(currentSong);

            FilterDisplaySongs();
            PrefetchThumbnails(catalog);
            if (quickPlaySongs.Count > 0) PrefetchThumbnails(quickPlaySongs);

            // If user already unlocked Quick Play in a previous session, fetch fresh new songs for Quick Play on startup!
            if (quickPlayUnlocked)
            {
                RefreshQuickPlayWithNewSongsOnStartup();
            }

            Application.ApplicationExit += OnApplicationExitSave;

            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 28; // ~35 FPS for smooth button animations, BetterLyrics karaoke fill & morphing artwork
            uiTimer.Tick += OnUiTimerTick;
            uiTimer.Start();

            searchDebounceTimer = new System.Windows.Forms.Timer();
            searchDebounceTimer.Interval = 360;
            searchDebounceTimer.Tick += OnSearchDebounceTick;
        }

        private void OnApplicationExitSave(object sender, EventArgs e)
        {
            SavePersistentSettings();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SavePersistentSettings();
            base.OnFormClosing(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (customAppIcon != null)
            {
                try
                {
                    SendMessage(this.Handle, WM_SETICON, (IntPtr)ICON_BIG, customAppIcon.Handle);
                    SendMessage(this.Handle, WM_SETICON, (IntPtr)ICON_SMALL, customAppIcon.Handle);
                }
                catch { }
            }
        }

        private string EncodeSongRecord(SongItem s)
        {
            if (s == null) return "";
            return Uri.EscapeDataString(s.Id ?? "") + "|" +
                   Uri.EscapeDataString(s.Title ?? "") + "|" +
                   Uri.EscapeDataString(s.Artist ?? "") + "|" +
                   Uri.EscapeDataString(s.Album ?? "") + "|" +
                   s.Duration.ToString(CultureInfo.InvariantCulture) + "|" +
                   Uri.EscapeDataString(s.Mood ?? "Chill") + "|" +
                   Uri.EscapeDataString(s.ThumbUrl ?? "") + "|" +
                   Uri.EscapeDataString(s.AudioUrl ?? "") + "|" +
                   Uri.EscapeDataString(s.FallbackUrl ?? "");
        }

        private SongItem DecodeSongRecord(string rec)
        {
            if (string.IsNullOrEmpty(rec)) return null;
            try
            {
                string[] p = rec.Split('|');
                if (p.Length < 9) return null;
                string id = Uri.UnescapeDataString(p[0]);
                string title = Uri.UnescapeDataString(p[1]);
                string artist = Uri.UnescapeDataString(p[2]);
                string album = Uri.UnescapeDataString(p[3]);
                int dur = 210;
                int.TryParse(p[4], out dur);
                string mood = Uri.UnescapeDataString(p[5]);
                string thumb = Uri.UnescapeDataString(p[6]);
                string audio = Uri.UnescapeDataString(p[7]);
                string fb = Uri.UnescapeDataString(p[8]);
                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(audio)) return null;

                // Check if catalog already has rich synced lyrics for this ID
                foreach (SongItem cs in catalog)
                {
                    if (cs.Id == id) return cs;
                }
                SongItem item = new SongItem(id, title, artist, album, dur, mood, thumb, audio, fb, "");
                EnsureSyncedLyricsParsed(item);
                return item;
            }
            catch { return null; }
        }

        // PERSISTENT SETTINGS ENGINE (Saves to BOTH LocalAppData and App BaseDirectory so settings NEVER reset!)
        private void SavePersistentSettings()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("themeIdx=" + activeThemeIdx);
                sb.AppendLine("updateUrl=" + updateManifestUrl);
                sb.AppendLine("eqEnabled=" + (eqEnabled ? "1" : "0"));
                sb.AppendLine("eqPreset=" + activeEqPreset);
                sb.AppendLine("eqBands=" + string.Format(CultureInfo.InvariantCulture, "{0:0.0},{1:0.0},{2:0.0},{3:0.0},{4:0.0}", eqBands[0], eqBands[1], eqBands[2], eqBands[3], eqBands[4]));
                sb.AppendLine("bassBoost=" + bassBoostPct);
                sb.AppendLine("virtualizer=" + virtualizerPct);
                sb.AppendLine("loudnessGain=" + loudnessGainPct);
                sb.AppendLine("crossfadeEnabled=" + (crossfadeEnabled ? "1" : "0"));
                sb.AppendLine("crossfadeSeconds=" + crossfadeSeconds);
                sb.AppendLine("gapless=" + (gaplessPlayback ? "1" : "0"));
                sb.AppendLine("skipSilence=" + (skipSilence ? "1" : "0"));
                sb.AppendLine("autoMix=" + (autoMixEnabled ? "1" : "0"));
                sb.AppendLine("oledBlack=" + (oledPureBlack ? "1" : "0"));
                sb.AppendLine("wavySlider=" + (wavySliderEnabled ? "1" : "0"));
                sb.AppendLine("artworkShape=" + artworkShapeMode);
                sb.AppendLine("volume=" + volumePercent);
                sb.AppendLine("muted=" + (isMuted ? "1" : "0"));
                sb.AppendLine("shuffle=" + (isShuffle ? "1" : "0"));
                sb.AppendLine("repeatOne=" + (isRepeatOne ? "1" : "0"));
                sb.AppendLine("rightPanelMode=" + rightPanelMode);
                sb.AppendLine("fullViewSubMode=" + fullViewSubMode);
                sb.AppendLine("sleepTimerMinutes=" + sleepTimerMinutes);
                sb.AppendLine("quickPlayUnlocked=" + (quickPlayUnlocked ? "1" : "0"));
                sb.AppendLine("userPlayedSongsCount=" + userPlayedSongsCount);

                List<string> likedArr = new List<string>(likedIds);
                sb.AppendLine("likedIds=" + string.Join(",", likedArr.ToArray()));

                if (favoritePlayedArtists.Count > 0)
                {
                    sb.AppendLine("favArtists=" + string.Join(";", favoritePlayedArtists.ToArray()));
                }

                if (currentSong != null)
                {
                    sb.AppendLine("lastSong=" + EncodeSongRecord(currentSong));
                }

                // Save Quick Play Songs (up to 10)
                List<string> qpEncoded = new List<string>();
                for (int i = 0; i < Math.Min(10, quickPlaySongs.Count); i++)
                {
                    qpEncoded.Add(EncodeSongRecord(quickPlaySongs[i]));
                }
                if (qpEncoded.Count > 0)
                {
                    sb.AppendLine("quickPlayList=" + string.Join(";", qpEncoded.ToArray()));
                }

                // Save Current Queue Songs (up to 18)
                List<string> qEncoded = new List<string>();
                for (int i = 0; i < Math.Min(18, queueSongs.Count); i++)
                {
                    qEncoded.Add(EncodeSongRecord(queueSongs[i]));
                }
                if (qEncoded.Count > 0)
                {
                    sb.AppendLine("savedQueue=" + string.Join(";", qEncoded.ToArray()));
                }

                string content = sb.ToString();
                try { File.WriteAllText(settingsFilePath, content, Encoding.UTF8); } catch { }
                try { File.WriteAllText(localSettingsBackupPath, content, Encoding.UTF8); } catch { }
            }
            catch { }
        }

        private void LoadPersistentSettings()
        {
            try
            {
                string pathToRead = settingsFilePath;
                if (!File.Exists(pathToRead) && File.Exists(localSettingsBackupPath))
                {
                    pathToRead = localSettingsBackupPath;
                }
                else if (File.Exists(pathToRead) && File.Exists(localSettingsBackupPath))
                {
                    DateTime t1 = File.GetLastWriteTimeUtc(settingsFilePath);
                    DateTime t2 = File.GetLastWriteTimeUtc(localSettingsBackupPath);
                    if (t2 > t1) pathToRead = localSettingsBackupPath;
                }

                if (!File.Exists(pathToRead)) return;

                string[] lines = File.ReadAllLines(pathToRead, Encoding.UTF8);
                List<SongItem> loadedQueue = new List<SongItem>();
                List<SongItem> loadedQuickPlay = new List<SongItem>();
                SongItem loadedLastSong = null;

                foreach (string rawLine in lines)
                {
                    if (string.IsNullOrEmpty(rawLine)) continue;
                    int eqIdx = rawLine.IndexOf('=');
                    if (eqIdx <= 0) continue;
                    string key = rawLine.Substring(0, eqIdx).Trim();
                    string val = rawLine.Substring(eqIdx + 1).Trim();

                    if (key == "themeIdx")
                    {
                        int idx = 0;
                        if (int.TryParse(val, out idx) && idx >= 0 && idx < themes.Count)
                        {
                            activeThemeIdx = idx;
                            accentColor = themes[idx].Accent;
                        }
                    }
                    else if (key == "updateUrl" && !string.IsNullOrEmpty(val) && !val.Contains("YOUR_GITHUB_USERNAME")) updateManifestUrl = val;
                    else if (key == "eqEnabled") eqEnabled = (val == "1");
                    else if (key == "eqPreset" && !string.IsNullOrEmpty(val)) activeEqPreset = val;
                    else if (key == "eqBands")
                    {
                        string[] parts = val.Split(',');
                        if (parts.Length == 5)
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                float parsed = 0f;
                                if (float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                                {
                                    eqBands[i] = Math.Max(-10f, Math.Min(10f, parsed));
                                }
                            }
                        }
                    }
                    else if (key == "bassBoost") int.TryParse(val, out bassBoostPct);
                    else if (key == "virtualizer") int.TryParse(val, out virtualizerPct);
                    else if (key == "loudnessGain") int.TryParse(val, out loudnessGainPct);
                    else if (key == "crossfadeEnabled") crossfadeEnabled = (val == "1");
                    else if (key == "crossfadeSeconds")
                    {
                        int cf = 3;
                        if (int.TryParse(val, out cf) && cf >= 1 && cf <= 15) crossfadeSeconds = cf;
                    }
                    else if (key == "gapless") gaplessPlayback = (val == "1");
                    else if (key == "skipSilence") skipSilence = (val == "1");
                    else if (key == "autoMix") autoMixEnabled = (val == "1");
                    else if (key == "oledBlack") oledPureBlack = (val == "1");
                    else if (key == "wavySlider") wavySliderEnabled = (val == "1");
                    else if (key == "artworkShape")
                    {
                        int sh = 0;
                        if (int.TryParse(val, out sh) && sh >= 0 && sh < artworkShapeNames.Length) artworkShapeMode = sh;
                    }
                    else if (key == "volume")
                    {
                        int v = 85;
                        if (int.TryParse(val, out v)) volumePercent = Math.Max(0, Math.Min(100, v));
                    }
                    else if (key == "muted") isMuted = (val == "1");
                    else if (key == "shuffle") isShuffle = (val == "1");
                    else if (key == "repeatOne") isRepeatOne = (val == "1");
                    else if (key == "rightPanelMode")
                    {
                        int rpm = 1;
                        if (int.TryParse(val, out rpm) && (rpm == 0 || rpm == 1)) rightPanelMode = rpm;
                    }
                    else if (key == "fullViewSubMode")
                    {
                        int fv = 1;
                        if (int.TryParse(val, out fv) && fv >= 0 && fv <= 2) fullViewSubMode = fv;
                    }
                    else if (key == "sleepTimerMinutes")
                    {
                        int stm = 0;
                        if (int.TryParse(val, out stm) && stm >= 0)
                        {
                            sleepTimerMinutes = stm;
                            sleepTimerSecondsLeft = stm * 60;
                        }
                    }
                    else if (key == "quickPlayUnlocked") quickPlayUnlocked = (val == "1");
                    else if (key == "userPlayedSongsCount") int.TryParse(val, out userPlayedSongsCount);
                    else if (key == "likedIds")
                    {
                        likedIds.Clear();
                        string[] ids = val.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string id in ids) likedIds.Add(id.Trim());
                    }
                    else if (key == "favArtists")
                    {
                        favoritePlayedArtists.Clear();
                        string[] arts = val.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string a in arts) favoritePlayedArtists.Add(a.Trim());
                    }
                    else if (key == "lastSong")
                    {
                        loadedLastSong = DecodeSongRecord(val);
                    }
                    else if (key == "quickPlayList")
                    {
                        string[] recs = val.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string r in recs)
                        {
                            SongItem s = DecodeSongRecord(r);
                            if (s != null && !ContainsSongByTitleOrId(loadedQuickPlay, s)) loadedQuickPlay.Add(s);
                        }
                    }
                    else if (key == "savedQueue")
                    {
                        string[] recs = val.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string r in recs)
                        {
                            SongItem s = DecodeSongRecord(r);
                            if (s != null && !ContainsSongByTitleOrId(loadedQueue, s)) loadedQueue.Add(s);
                        }
                    }
                }

                if (loadedQuickPlay.Count > 0)
                {
                    quickPlaySongs = loadedQuickPlay;
                    quickPlayUnlocked = true;
                }

                if (loadedQueue.Count > 0)
                {
                    queueSongs.Clear();
                    queueSongs.AddRange(loadedQueue);
                }

                if (loadedLastSong != null)
                {
                    currentSong = loadedLastSong;
                    currentDurSec = loadedLastSong.Duration > 0 ? loadedLastSong.Duration : 210;
                    int idx = 0;
                    for (int i = 0; i < queueSongs.Count; i++)
                    {
                        if (queueSongs[i].Id == currentSong.Id) { idx = i; break; }
                    }
                    currentQueueIndex = idx;
                }

                ApplyEffectiveDspAndVolume();
            }
            catch { }
        }

        private void ApplyCustomTaskbarAndWindowIcon()
        {
            try
            {
                int sz = 64;
                using (Bitmap bmp = new Bitmap(sz, sz))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    Rectangle box = new Rectangle(3, 3, sz - 6, sz - 6);
                    using (GraphicsPath gp = GetRoundRectPath(box, 16))
                    using (SolidBrush bgB = new SolidBrush(Color.FromArgb(12, 14, 20)))
                    using (Pen goldPen = new Pen(Color.FromArgb(255, 193, 7), 2.5f))
                    {
                        g.FillPath(bgB, gp);
                        g.DrawPath(goldPen, gp);
                    }

                    int cx = sz / 2;
                    int cy = sz / 2;
                    int[] dxArr = new int[] { -14, -7, 0, 7, 14 };
                    int[] hhArr = new int[] { 7, 13, 18, 12, 7 };
                    using (SolidBrush goldBrush = new SolidBrush(Color.FromArgb(255, 193, 7)))
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            Rectangle barR = new Rectangle(cx + dxArr[i] - 2, cy - hhArr[i], 4, hhArr[i] * 2);
                            FillRoundRect(g, goldBrush, barR, 2);
                        }
                    }

                    IntPtr hIcon = bmp.GetHicon();
                    customAppIcon = Icon.FromHandle(hIcon);
                    this.Icon = customAppIcon;
                }
            }
            catch
            {
                try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            }
        }

        private void InitThemes()
        {
            themes.Add(new ThemeItem("Royal Gold", Color.FromArgb(255, 193, 7)));
            themes.Add(new ThemeItem("Crimson", Color.FromArgb(255, 42, 85)));
            themes.Add(new ThemeItem("Cyber Cyan", Color.FromArgb(0, 229, 255)));
            themes.Add(new ThemeItem("Emerald", Color.FromArgb(0, 230, 118)));
            themes.Add(new ThemeItem("Amethyst", Color.FromArgb(179, 136, 255)));
            themes.Add(new ThemeItem("Sunset", Color.FromArgb(255, 112, 67)));
            themes.Add(new ThemeItem("Minimal Silver", Color.FromArgb(226, 232, 240)));
            themes.Add(new ThemeItem("Warm Honey", Color.FromArgb(255, 213, 79)));
            themes.Add(new ThemeItem("Midnight Amber", Color.FromArgb(255, 179, 0)));
        }

        private void InitCatalog()
        {
            catalog.Add(new SongItem("s1", "Payal", "Yo Yo Honey Singh, Paradox", "GLORY", 226, "Party",
                "https://c.saavncdn.com/173/GLORY-Hindi-2024-20250117161048-500x500.jpg",
                "https://aac.saavncdn.com/173/ad5df053bfb2a4755cbb6c74e6183406_320.mp4",
                "https://aac.saavncdn.com/173/ad5df053bfb2a4755cbb6c74e6183406_160.mp4",
                "[00:02.20]Tere bin jeena nahi, ae dil mera kehnda ae\n[00:08.50]Har pal teri yaad da dariya ve behnda ae\n[00:15.00]Payal teri khanke jadon nache tu floor te\n[00:21.50]Sari duniya de mundeyan da dhyan tere val te\n[00:28.00]Chhan chhan bole teri payal di jhankar ve\n[00:35.00]Raat saari nachde na thakde yaar ve\n[00:42.50]Jadon turdi ae kudiye tu moran wangu chaldi\n[00:50.00]Husn tere di agg saare shehar vich baldi\n[00:58.00]Yo Yo Honey Singh beat utte paave bhangra\n[01:06.00]Paradox verse naal karda ae dhamaka\n[01:14.50]Payal teri khanke jadon nache tu floor te\n[01:23.00]Sari duniya de mundeyan da dhyan tere val te\n[01:32.00]Chammak teri akhiyan di taareyan nu maat paave\n[01:41.00]Jehda vi takke tainu dil apna haar jaave\n[01:50.50]Nach le tu khul ke ajj raat saadi apni ae\n[02:00.00]Har ik dhadkan vich teri hi kahani ae\n[02:10.00]Payal teri khanke jadon nache tu floor te\n[02:20.00]Chhan chhan bole teri payal di jhankar ve"));

            catalog.Add(new SongItem("s2", "Raat Di Gedi", "Diljit Dosanjh, Neeru Bajwa", "Raat Di Gedi", 198, "Energize",
                "https://c.saavncdn.com/698/Raat-Di-Gedi-Punjabi-2018-20180323-500x500.jpg",
                "https://aac.saavncdn.com/698/727adb91e70e5a4ed2a268acdbf55172_320.mp4",
                "https://aac.saavncdn.com/698/727adb91e70e5a4ed2a268acdbf55172_160.mp4",
                "[00:02.00]Raat di gedi gal risk di\n[00:07.50]Tang chhedi aa tere ishq di\n[00:13.50]Gaddiyan de rim ghumme, bass kardi shine\n[00:20.00]Diljit Dosanjh naam hi kaafi ae sign\n[00:27.00]Heavyweight beat utte karda growl\n[00:34.00]Pind to leke Toronto tak loud\n[00:41.50]Raat di gedi gal risk di\n[00:49.00]Tang chhedi aa tere ishq di\n[00:57.00]Tere piche ghumdi ae gaddi kaali raat nu\n[01:05.50]Dil chaunda sunna bas teri har baat nu\n[01:14.00]Sardari poori kaim, jatt paunda poora gaa\n[01:23.00]Tere ghar wali gali vich baneya ae raah\n[01:32.00]Raat di gedi gal risk di\n[01:41.00]Tang chhedi aa tere ishq di"));

            catalog.Add(new SongItem("s3", "Jackpot", "Gur Sidhu, Cheema Y", "Jackpot", 176, "Workout",
                "https://c.saavncdn.com/512/Bermuda-Triangle-Punjabi-2026-20260513183834-500x500.jpg",
                "https://aac.saavncdn.com/512/16c4d3b6d6c8afb695d48a52821a6ef2_320.mp4",
                "https://aac.saavncdn.com/512/16c4d3b6d6c8afb695d48a52821a6ef2_160.mp4",
                "[00:02.00]Pehli goli chaldi te hunda jackpot\n[00:07.80]Kudiye tu lutt leya gabru da thought\n[00:14.00]Gur Sidhu beat utte fire karda\n[00:20.50]Poora shehar dekh sanu cheer karda\n[00:27.50]Honsle buland saade kade na jhukde\n[00:35.00]Manzilan توں pehlan kadam na rukde\n[00:43.00]Cheema Y likhda ae sachi har baat ve\n[00:51.00]Mehnat de naal badli ae saadi raat ve\n[01:00.00]Pehli goli chaldi te hunda jackpot\n[01:09.00]Kudiye tu lutt leya gabru da thought\n[01:18.50]Top gear vich gaddi chale bina brake ton\n[01:28.00]Asli ne yaar saare door rehnde fake ton"));

            catalog.Add(new SongItem("s4", "Softly", "Karan Aujla, Ikky", "Making Memories", 218, "Chill",
                "https://c.saavncdn.com/918/AUJLA-SZN-1-Punjabi-2026-20260930131343-500x500.jpg",
                "https://aac.saavncdn.com/918/4b45117b680211704c642063d0e5ef2b_320.mp4",
                "https://aac.saavncdn.com/918/4b45117b680211704c642063d0e5ef2b_160.mp4",
                "[00:02.00]Hold it softly, walk it softly\n[00:08.00]Jadon tu langhdi akhan saariyaan rondi\n[00:14.50]Karan Aujla likhda geet tere layi\n[00:21.00]Ikky beat te kare vibe banayi\n[00:28.00]Tere nakhre da mull koi paave na\n[00:35.50]Tere warga hor koi nazar aave na\n[00:43.50]Hold it softly, talk to me softly\n[00:52.00]Suit paake jadon nikle tu baahar ni\n[01:00.50]Tere utte marda ae tera sardar ni\n[01:09.50]Chann naalon sohna mukhda ae tera\n[01:19.00]Tere bina sunna lagda charchehera\n[01:29.00]Hold it softly, walk it softly\n[01:39.00]Making memories with you all night long"));

            catalog.Add(new SongItem("s5", "G.O.A.T.", "Diljit Dosanjh", "G.O.A.T.", 224, "Energize",
                "https://c.saavncdn.com/597/G-O-A-T-Punjabi-2020-20240708055140-500x500.jpg",
                "https://aac.saavncdn.com/597/ce842951d6cde3c4355046ca5e250809_320.mp4",
                "https://aac.saavncdn.com/597/ce842951d6cde3c4355046ca5e250809_160.mp4",
                "[00:02.00]Diamond jini chammak teri, koka tera lishke\n[00:08.50]Diljit de gaane utte mundeyan de dil khiske\n[00:15.50]G.O.A.T. da khitaab sanu rab ne ditta ae\n[00:23.00]Har maidan fateh asi karke jitta ae\n[00:31.00]Pagg wala munda dekh duniya te chhaya\n[00:39.50]Mehnat de naal apna mukam banaya\n[00:48.50]Kalakaar mainu kehnde, lokan da pyaar ae\n[00:58.00]Dosanjhanwala naam har zubaan te taiyaar ae\n[01:08.00]G.O.A.T. da khitaab sanu rab ne ditta ae\n[01:18.50]Har maidan fateh asi karke jitta ae"));

            catalog.Add(new SongItem("s6", "Faded", "Alan Walker", "Different World", 165, "Relax",
                "https://c.saavncdn.com/670/Faded-Instrumental-2022-20260324143104-500x500.jpg",
                "https://aac.saavncdn.com/670/31d24c80462a61591ceabf81d1c749ae_320.mp4",
                "https://aac.saavncdn.com/670/31d24c80462a61591ceabf81d1c749ae_160.mp4",
                "[00:02.00]You were the shadow to my light\n[00:07.50]Did you feel us?\n[00:13.00]Another start, you fade away\n[00:19.00]Afraid our aim is out of sight\n[00:25.00]Wanna see us alive\n[00:31.50]Where are you now?\n[00:38.00]Was it all in my fantasy?\n[00:45.00]Where are you now?\n[00:52.00]Were you only imaginary?\n[01:00.00]Atlantis, under the sea, under the sea\n[01:09.00]Where are you now? Another dream\n[01:18.00]The monster's running wild inside of me\n[01:27.00]I'm faded, so lost, I'm faded"));

            catalog.Add(new SongItem("s7", "Tauba Tauba", "Karan Aujla", "Bad Newz", 207, "Party",
                "https://c.saavncdn.com/992/Bad-Newz-Hindi-2024-20250730113701-500x500.jpg",
                "https://aac.saavncdn.com/992/5d44da8bc1d78fb72d18b701d758fd1f_320.mp4",
                "https://aac.saavncdn.com/992/5d44da8bc1d78fb72d18b701d758fd1f_160.mp4",
                "[00:02.00]Husan tera tauba tauba, nakhre tere tauba tauba\n[00:08.50]Takde hi saare mundeyan da dil lutti jaave\n[00:15.00]Karan Aujla likhda jo vi geet karda ae hit\n[00:22.00]Jadon nache tu floor utte lagdi ae lit\n[00:29.50]Ik jhalak teri paun layi saare khade ne\n[00:37.50]Tere husan de charche shehar vich bade ne\n[00:46.00]Husan tera tauba tauba, nakhre tere tauba tauba\n[00:55.00]Akhiyan naal kardi ae jaadu har dil te\n[01:04.50]Asin vi fida ho gaye tere kaale til te\n[01:14.00]Husan tera tauba tauba, nakhre tere tauba tauba"));

            catalog.Add(new SongItem("s8", "Winning Speech", "Karan Aujla, MXRCI", "Winning Speech", 227, "Focus",
                "https://c.saavncdn.com/089/Winning-Speech-Punjabi-2024-20260626013220-500x500.jpg",
                "https://aac.saavncdn.com/089/64beffa430e4c948223ec6bfcc3a13f0_320.mp4",
                "https://aac.saavncdn.com/089/64beffa430e4c948223ec6bfcc3a13f0_160.mp4",
                "[00:02.00]Sab kehnde si koshish kar ke dekh la\n[00:08.50]Main keha mehnat kar ke lekh likh la\n[00:15.50]Winning speech ajj stage te bolange\n[00:23.00]Raaz kamyaabi wale saare kholange\n[00:31.00]Thalle ton uth ke aaye aan aasman tak\n[00:39.50]Geet saade gunjde ne har jahaan tak\n[00:48.50]MXRCI di beat utte kalam chaldi\n[00:58.00]Sachi mehnat kade vi nahi ٹaldi\n[01:08.00]Winning speech ajj stage te bolange"));

            catalog.Add(new SongItem("s9", "Kesariya", "Pritam, Arijit Singh", "Brahmastra", 268, "Relax",
                "https://c.saavncdn.com/871/Brahmastra-Original-Motion-Picture-Soundtrack-Hindi-2022-20221006155213-500x500.jpg",
                "https://aac.saavncdn.com/871/c2febd353f3a076a406fa37510f31f9f_320.mp4",
                "https://aac.saavncdn.com/871/c2febd353f3a076a406fa37510f31f9f_160.mp4",
                "[00:02.00]Mujhko itna bataye koi\n[00:08.00]Kaise tujhse dil na lagaye koi\n[00:14.50]Rabba ne tujhko banane mein\n[00:21.00]Kardi hai husn ki khaali tijoriyan\n[00:28.00]Kajal ki siyaahi se likhi\n[00:35.00]Hai tune jaane kitno ki love storiyan\n[00:42.50]Kesariya tera ishq hai piya\n[00:50.00]Rang jaaun jo main haath lagaun\n[00:58.00]Din beete saara teri fikr mein\n[01:06.50]Rain saari teri khair manaun\n[01:15.50]Patjhad ke mausam mein bhi rangi chanaaron jaisi\n[01:25.00]Jhanke sannaaton mein tu veena ke taaron jaisi\n[01:35.00]Kesariya tera ishq hai piya\n[01:45.00]Rang jaaun jo main haath lagaun"));

            catalog.Add(new SongItem("s10", "Amplifier", "Imran Khan", "Unforgettable", 189, "Energize",
                "https://c.saavncdn.com/078/AiSh-The-Covers-Collection-Hindi-2022-20260710184759-500x500.jpg",
                "https://aac.saavncdn.com/078/20282c192f1f8a23845424f74dd05aad_320.mp4",
                "https://aac.saavncdn.com/078/20282c192f1f8a23845424f74dd05aad_160.mp4",
                "[00:02.00]Gaddi sadi behja ni, door kithe lai ja ni\n[00:08.50]Woofer tu meri main tera amplifier\n[00:15.50]Tere bin dil mera lagda na kalliya\n[00:23.00]Bass jadon wajje poora hilda mohalla\n[00:31.00]Ni tu lutt leya chain mera pehli takni ch\n[00:39.50]Naam tera likheya ae dil wali akhni ch\n[00:48.50]Woofer tu meri main tera amplifier\n[00:58.00]Gaddi sadi behja ni, door kithe lai ja ni"));

            foreach (SongItem s in catalog)
            {
                EnsureSyncedLyricsParsed(s);
            }

            likedIds.Add("s1");
            likedIds.Add("s4");
        }

        // =========================================================================
        // BETTERLYRICS AUTO-SYNC ENGINE & MULTI-SOURCE LRC FETCHER
        // =========================================================================

        private void EnsureSyncedLyricsParsed(SongItem song)
        {
            if (song == null) return;
            if (song.SyncedLines == null) song.SyncedLines = new List<LyricLine>();
            if (song.SyncedLines.Count > 0) return;

            string raw = song.Lyrics ?? "";
            string lowTitle = (song.Title ?? "").ToLowerInvariant();

            if (lowTitle.Contains("nakhre kyon") || lowTitle.Contains("nakhre kyun"))
            {
                raw = "[00:01.00]Kyun samjhe na baatein meri tu\n" +
                      "[00:04.20]Kyun ae lad'di\n" +
                      "[00:07.50]Soorat ae sohni, seerat teri bewafa\n" +
                      "[00:12.00]Samjhe na ishaare haan kinni vaari main kahaa\n" +
                      "[00:17.20]Tu aaja chal mere naal\n" +
                      "[00:21.50]Nakhre kyon kardi, dil te vaar kardi\n" +
                      "[00:26.50]Har gal utte kudiye tu inkaar kardi\n" +
                      "[00:32.00]Teri akhiyan da jaadu saade dil utte chaleya\n" +
                      "[00:38.00]Tere bin pal vi na saathon jaave paleya\n" +
                      "[00:44.50]Kyun samjhe na baatein meri tu\n" +
                      "[00:50.00]Kyun ae lad'di";
            }
            else if (lowTitle.Contains("excuses"))
            {
                raw = "[00:02.50]Mere dil naal laare ni tu laun waliye\n" +
                      "[00:07.20]Dil tera jado tutuga tan pata laguga\n" +
                      "[00:12.00]Dil naal laare ni tu laun waliye\n" +
                      "[00:16.80]Dil tera jado tutuga tan pata laguga\n" +
                      "[00:21.50]Tutte dil nu sambhalna kinna aukha hunda ae\n" +
                      "[00:26.20]Kehndi hundi si chan tak raah bana de\n" +
                      "[00:31.00]Taare ne pasand mainu hetha saare laade\n" +
                      "[00:36.50]Ohna taareyan de vichon jado mainu dekhengi\n" +
                      "[00:42.00]Meri yaad jado aaugi tan pata laguga\n" +
                      "[00:47.50]Dil naal laare ni tu laun waliye\n" +
                      "[00:53.00]Dil tera jado tutuga tan pata laguga";
            }

            int targetDur = song.Duration > 0 ? song.Duration : 200;
            List<LyricLine> parsed = ParseLrcOrPlainText(raw, targetDur, song.Title, song.Artist);
            song.SyncedLines = parsed;
            AutoCalibrateLyricsToDuration(song, targetDur);
        }

        // Automatically calibrates and stretches/aligns lyric timestamps to match the actual audio track duration!
        private void AutoCalibrateLyricsToDuration(SongItem song, double realDurationSec)
        {
            if (song == null || song.SyncedLines == null || song.SyncedLines.Count < 2 || realDurationSec <= 15) return;
            List<LyricLine> lines = song.SyncedLines;

            bool isExactLrc = lines[0].IsExactLrcTimestamp;
            double lastEnd = lines[lines.Count - 1].EndSec;
            double firstStart = lines[0].StartSec;

            // If non-exact plain lyrics OR if LRC timestamps only cover less than 65% or more than 108% of realDurationSec,
            // smoothly scale/distribute timestamps across the song so auto-sync stays locked from start to finish!
            if (!isExactLrc || lastEnd < realDurationSec * 0.68 || lastEnd > realDurationSec * 1.06)
            {
                double startAnchor = isExactLrc ? Math.Min(12.0, Math.Max(1.0, firstStart)) : 2.0;
                double usableSpan = Math.Max(20.0, realDurationSec - startAnchor - 4.0);

                if (isExactLrc && (lastEnd - firstStart) > 12.0)
                {
                    double origSpan = lastEnd - firstStart;
                    double scale = usableSpan / origSpan;
                    // Clamp scale factor for exact LRC so if it's close to 1.0 we preserve exact vocal timing
                    if (scale >= 0.88 && scale <= 1.15) scale = 1.0;

                    if (Math.Abs(scale - 1.0) > 0.05)
                    {
                        for (int i = 0; i < lines.Count; i++)
                        {
                            lines[i].StartSec = startAnchor + (lines[i].StartSec - firstStart) * scale;
                            lines[i].EndSec = startAnchor + (lines[i].EndSec - firstStart) * scale;
                        }
                    }
                }
                else
                {
                    // Word/syllable-weighted auto-sync distribution across the entire track duration!
                    double totalWeight = 0;
                    double[] weights = new double[lines.Count];
                    for (int i = 0; i < lines.Count; i++)
                    {
                        int charLen = Math.Max(10, (lines[i].Text ?? "").Length);
                        weights[i] = Math.Pow(charLen, 0.75);
                        totalWeight += weights[i];
                    }

                    double curT = startAnchor;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        double dur = Math.Max(2.2, (weights[i] / Math.Max(1.0, totalWeight)) * usableSpan);
                        lines[i].StartSec = curT;
                        lines[i].EndSec = curT + dur;
                        curT += dur;
                    }
                }
            }

            song.LastCalibratedDuration = realDurationSec;
        }

        private List<LyricLine> ParseLrcOrPlainText(string rawText, int totalDurSec, string fallbackTitle, string fallbackArtist)
        {
            List<LyricLine> lines = new List<LyricLine>();
            if (!string.IsNullOrEmpty(rawText))
            {
                string[] split = rawText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                Regex lrcRx = new Regex(@"\[(\d+):(\d+(?:\.\d+)?)\](.*)");

                foreach (string s in split)
                {
                    Match m = lrcRx.Match(s);
                    if (m.Success)
                    {
                        int mins = 0;
                        double secs = 0;
                        int.TryParse(m.Groups[1].Value, out mins);
                        double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out secs);
                        string txt = m.Groups[3].Value.Trim();
                        if (!string.IsNullOrEmpty(txt) && txt != "♪")
                        {
                            lines.Add(new LyricLine(mins * 60.0 + secs, mins * 60.0 + secs + 4.2, txt, true));
                        }
                    }
                }

                if (lines.Count > 0)
                {
                    for (int i = 0; i < lines.Count - 1; i++)
                    {
                        lines[i].EndSec = Math.Max(lines[i].StartSec + 1.0, lines[i + 1].StartSec);
                    }
                    lines[lines.Count - 1].EndSec = Math.Max(lines[lines.Count - 1].StartSec + 4.5, Math.Min(totalDurSec, lines[lines.Count - 1].StartSec + 7.0));
                    return lines;
                }

                // Plain text lines (filter out metadata headers)
                List<string> plain = new List<string>();
                foreach (string s in split)
                {
                    string t = s.Trim();
                    if (string.IsNullOrEmpty(t)) continue;
                    if (t.Contains("320kbps") || t.StartsWith("Mood:")) continue;
                    plain.Add(t);
                }

                if (plain.Count >= 3)
                {
                    double cur = 2.0;
                    double step = Math.Max(2.8, (Math.Max(60, totalDurSec) - 6.0) / plain.Count);
                    for (int i = 0; i < plain.Count; i++)
                    {
                        lines.Add(new LyricLine(cur, cur + step, plain[i], false));
                        cur += step;
                    }
                    return lines;
                }
            }

            // Full-length multi-stanza rhythmic fallback across the whole song while live fetch runs
            string cleanT = CleanSongDisplayTitle(fallbackTitle ?? "Melody");
            string cleanA = GetPrimaryArtistName(fallbackArtist ?? "Artist");
            string[] baseVerses = new string[]
            {
                cleanT + " — " + cleanA,
                "Har dhadkan vich goonje teri aawaz",
                "Feel the rhythm rising through the night",
                "Dil diyan gallan te suran da safar",
                "Lost in the melody, floating through the sound",
                "Tere naal judiyan ne saariyan yaadan",
                "Har lafz vich chhupi teri dastaan",
                "Bass hits deep as the lights fade low",
                "Raat di khamoshi vich geet tera gaavaan",
                "Let the music carry every moment away",
                "Suran de samundar vich khoya mera dil",
                cleanT + " — BetterLyrics Auto-Sync"
            };
            double curTime = 2.0;
            double verseStep = Math.Max(3.5, (Math.Max(90, totalDurSec) - 6.0) / baseVerses.Length);
            for (int i = 0; i < baseVerses.Length; i++)
            {
                lines.Add(new LyricLine(curTime, curTime + verseStep, baseVerses[i], false));
                curTime += verseStep;
            }
            return lines;
        }

        // Extracts ALL syncedLyrics and plainLyrics candidates from an LRCLIB JSON response array and picks the best match!
        private void ExtractBestLrclibMatch(string json, int targetDur, out string bestSynced, out string bestPlain)
        {
            bestSynced = "";
            bestPlain = "";
            if (string.IsNullOrEmpty(json)) return;

            List<string> syncedCandidates = ExtractAllJsonStringFieldsStatic(json, "syncedLyrics");
            foreach (string s in syncedCandidates)
            {
                if (!string.IsNullOrEmpty(s) && s.Contains("[0") && s.Length > bestSynced.Length)
                {
                    bestSynced = s;
                    if (bestSynced.Length > 200) break;
                }
            }

            List<string> plainCandidates = ExtractAllJsonStringFieldsStatic(json, "plainLyrics");
            foreach (string p in plainCandidates)
            {
                if (!string.IsNullOrEmpty(p) && p.Length > bestPlain.Length)
                {
                    bestPlain = p;
                    if (bestPlain.Length > 200) break;
                }
            }
        }

        private static List<string> ExtractAllJsonStringFieldsStatic(string json, string fieldName)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrEmpty(json)) return results;
            string pat = "\"" + fieldName + "\"";
            int idx = json.IndexOf(pat);
            while (idx >= 0)
            {
                int colon = json.IndexOf(':', idx + pat.Length);
                if (colon > 0)
                {
                    int vStart = colon + 1;
                    while (vStart < json.Length && char.IsWhiteSpace(json[vStart])) vStart++;
                    if (vStart < json.Length && json[vStart] == '"')
                    {
                        StringBuilder sb = new StringBuilder();
                        bool esc = false;
                        for (int k = vStart + 1; k < json.Length; k++)
                        {
                            char c = json[k];
                            if (esc)
                            {
                                if (c == 'n') sb.Append('\n');
                                else if (c == 'r') { }
                                else if (c == '"') sb.Append('"');
                                else if (c == '\\') sb.Append('\\');
                                else if (c == '/') sb.Append('/');
                                else sb.Append(c);
                                esc = false;
                            }
                            else if (c == '\\') esc = true;
                            else if (c == '"') break;
                            else sb.Append(c);
                        }
                        string res = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(res)) results.Add(res);
                    }
                }
                idx = json.IndexOf(pat, idx + pat.Length);
            }
            return results;
        }

        // Multi-source BetterLyrics fetcher + Auto-Sync Calibrator!
        private void FetchBetterLyricsAsync(SongItem song)
        {
            if (song == null) return;
            EnsureSyncedLyricsParsed(song);

            int myLyrVer = ++lyricsFetchVersion;
            isLoadingBetterLyrics = true;
            lyricSyncOffsetSec = 0.0;

            Thread lyrThread = new Thread(() =>
            {
                List<LyricLine> fetchedLines = null;
                string syncType = "Auto-Synced";
                string cleanTitle = CleanSongDisplayTitle(song.Title);
                string baseTitle = GetNormalizedBaseTitle(song.Title);
                string primaryArtist = GetPrimaryArtistName(song.Artist);

                string[] searchUrls = new string[]
                {
                    "https://lrclib.net/api/search?track_name=" + Uri.EscapeDataString(cleanTitle) + "&artist_name=" + Uri.EscapeDataString(primaryArtist),
                    "https://lrclib.net/api/search?q=" + Uri.EscapeDataString(cleanTitle + " " + primaryArtist),
                    "https://lrclib.net/api/search?q=" + Uri.EscapeDataString(baseTitle + " " + primaryArtist),
                    "https://lrclib.net/api/search?track_name=" + Uri.EscapeDataString(cleanTitle)
                };

                foreach (string lrcUrl in searchUrls)
                {
                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            wc.Encoding = Encoding.UTF8;
                            wc.Headers.Add("User-Agent", "LeoMusicStudio/9.0 (BetterLyrics AutoSync)");
                            string json = wc.DownloadString(lrcUrl);

                            string bestSynced = "";
                            string bestPlain = "";
                            ExtractBestLrclibMatch(json, song.Duration, out bestSynced, out bestPlain);

                            if (!string.IsNullOrEmpty(bestSynced))
                            {
                                fetchedLines = ParseLrcOrPlainText(bestSynced, song.Duration, song.Title, song.Artist);
                                if (fetchedLines != null && fetchedLines.Count >= 4)
                                {
                                    syncType = "LRCLIB Time-Synced";
                                    break;
                                }
                            }
                            else if (!string.IsNullOrEmpty(bestPlain) && (fetchedLines == null || fetchedLines.Count < 4))
                            {
                                fetchedLines = ParseLrcOrPlainText(bestPlain, song.Duration, song.Title, song.Artist);
                                syncType = "Auto-Synced Lyrics";
                            }
                        }
                    }
                    catch { }
                }

                // Fallback: JioSaavn Official Lyrics API + Saavn.dev Lyrics Mirror
                string pid = ExtractRawSaavnPid(song.Id);
                if ((fetchedLines == null || fetchedLines.Count < 4) && !string.IsNullOrEmpty(pid))
                {
                    string[] jioLyrUrls = new string[]
                    {
                        "https://www.jiosaavn.com/api.php?__call=lyrics.getLyrics&lyrics_id=" + Uri.EscapeDataString(pid) + "&ctx=android&_format=json&_marker=0",
                        "https://saavn.dev/api/songs/" + Uri.EscapeDataString(pid) + "/lyrics"
                    };
                    foreach (string jUrl in jioLyrUrls)
                    {
                        try
                        {
                            using (WebClient wc = new WebClient())
                            {
                                wc.Encoding = Encoding.UTF8;
                                wc.Headers.Add("User-Agent", "Mozilla/5.0 (Linux; Android 14)");
                                string json = wc.DownloadString(jUrl);
                                List<string> lyrFields = ExtractAllJsonStringFieldsStatic(json, "lyrics");
                                if (lyrFields.Count == 0) lyrFields = ExtractAllJsonStringFieldsStatic(json, "snippet");
                                foreach (string lyrHtml in lyrFields)
                                {
                                    string cleanLyr = lyrHtml.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
                                    cleanLyr = Regex.Replace(cleanLyr, "<[^>]+>", "");
                                    cleanLyr = CleanHtml(cleanLyr);
                                    if (cleanLyr.Length > 30)
                                    {
                                        fetchedLines = ParseLrcOrPlainText(cleanLyr, song.Duration, song.Title, song.Artist);
                                        syncType = "Auto-Synced Studio Lyrics";
                                        break;
                                    }
                                }
                                if (fetchedLines != null && fetchedLines.Count >= 4) break;
                            }
                        }
                        catch { }
                    }
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    if (myLyrVer == lyricsFetchVersion)
                    {
                        isLoadingBetterLyrics = false;
                        lyricsSyncSourceLabel = syncType;
                    }
                    if (fetchedLines != null && fetchedLines.Count >= 4)
                    {
                        song.SyncedLines = fetchedLines;
                        double durToUse = (currentSong != null && currentSong.Id == song.Id && currentDurSec > 20) ? currentDurSec : song.Duration;
                        AutoCalibrateLyricsToDuration(song, durToUse);
                        this.Invalidate();
                    }
                });
            });
            lyrThread.IsBackground = true;
            lyrThread.Start();
        }

        private void UpdateTargetAmbientColorFromSong(SongItem song)
        {
            if (song == null) return;
            string jpgUrl = (song.ThumbUrl ?? "").Replace(".webp", ".jpg");
            lock (dominantColorCache)
            {
                if (dominantColorCache.ContainsKey(jpgUrl))
                {
                    Color c = dominantColorCache[jpgUrl];
                    targetAmbR = c.R;
                    targetAmbG = c.G;
                    targetAmbB = c.B;
                    return;
                }
            }

            string lowT = (song.Title ?? "").ToLowerInvariant();
            if (lowT.Contains("excuses") || song.Mood == "Chill")
            {
                targetAmbR = 104f; targetAmbG = 58f; targetAmbB = 98f;
            }
            else if (lowT.Contains("nakhre") || song.Mood == "Party")
            {
                targetAmbR = 112f; targetAmbG = 66f; targetAmbB = 28f;
            }
            else if (song.Mood == "Relax")
            {
                targetAmbR = 42f; targetAmbG = 72f; targetAmbB = 96f;
            }
            else if (song.Mood == "Workout")
            {
                targetAmbR = 108f; targetAmbG = 38f; targetAmbB = 48f;
            }
            else
            {
                targetAmbR = 96f; targetAmbG = 64f; targetAmbB = 34f;
            }
        }

        private void ComputeDominantColorFromBitmap(string jpgUrl, Bitmap bmp)
        {
            if (bmp == null) return;
            try
            {
                long sumR = 0, sumG = 0, sumB = 0, count = 0;
                int stepX = Math.Max(1, bmp.Width / 16);
                int stepY = Math.Max(1, bmp.Height / 16);
                for (int y = stepY; y < bmp.Height - stepY; y += stepY)
                {
                    for (int x = stepX; x < bmp.Width - stepX; x += stepX)
                    {
                        Color px = bmp.GetPixel(x, y);
                        int bright = (px.R + px.G + px.B) / 3;
                        if (bright > 25 && bright < 230)
                        {
                            sumR += px.R;
                            sumG += px.G;
                            sumB += px.B;
                            count++;
                        }
                    }
                }
                if (count > 0)
                {
                    int r = (int)Math.Min(145, Math.Max(38, (sumR / count) * 0.72));
                    int g = (int)Math.Min(125, Math.Max(30, (sumG / count) * 0.68));
                    int b = (int)Math.Min(145, Math.Max(30, (sumB / count) * 0.72));
                    Color dom = Color.FromArgb(r, g, b);
                    lock (dominantColorCache)
                    {
                        dominantColorCache[jpgUrl] = dom;
                    }
                    if (currentSong != null && (currentSong.ThumbUrl ?? "").Replace(".webp", ".jpg") == jpgUrl)
                    {
                        targetAmbR = dom.R;
                        targetAmbG = dom.G;
                        targetAmbB = dom.B;
                    }
                }
            }
            catch { }
        }

        private void RecordSongPlayForQuickPlay(SongItem playedSong)
        {
            if (playedSong == null) return;
            userPlayedSongsCount++;

            string primArt = GetPrimaryArtistName(playedSong.Artist);
            if (!string.IsNullOrEmpty(primArt) && primArt != "Studio Artist")
            {
                favoritePlayedArtists.Remove(primArt);
                favoritePlayedArtists.Insert(0, primArt);
                if (favoritePlayedArtists.Count > 8) favoritePlayedArtists.RemoveAt(favoritePlayedArtists.Count - 1);
            }

            for (int i = quickPlaySongs.Count - 1; i >= 0; i--)
            {
                if (quickPlaySongs[i].Id == playedSong.Id || IsSameSongTitleOrVariant(quickPlaySongs[i].Title, playedSong.Title))
                {
                    quickPlaySongs.RemoveAt(i);
                }
            }
            quickPlaySongs.Insert(0, playedSong);
            if (quickPlaySongs.Count > 10) quickPlaySongs.RemoveRange(10, quickPlaySongs.Count - 10);

            if (!quickPlayUnlocked && userPlayedSongsCount >= 1)
            {
                quickPlayUnlocked = true;
                RefreshQuickPlayWithNewSongsOnStartup();
            }

            SavePersistentSettings();
        }

        private void RefreshQuickPlayWithNewSongsOnStartup()
        {
            Thread qpThread = new Thread(() =>
            {
                List<SongItem> freshPicks = new List<SongItem>();
                string seedArtist = favoritePlayedArtists.Count > 0 ? favoritePlayedArtists[0] : "Karan Aujla Diljit Dosanjh";
                string secondArtist = favoritePlayedArtists.Count > 1 ? favoritePlayedArtists[1] : "Arijit Singh Badshah";

                string[] queries = new string[]
                {
                    seedArtist + " latest hit songs",
                    secondArtist + " top trending songs"
                };

                foreach (string q in queries)
                {
                    try
                    {
                        string url = "https://www.jiosaavn.com/api.php?__call=search.getResults&_format=json&n=12&p=1&_marker=0&ctx=android&q=" + Uri.EscapeDataString(q);
                        using (WebClient wc = new WebClient())
                        {
                            wc.Encoding = Encoding.UTF8;
                            wc.Headers.Add("User-Agent", "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/120.0 Mobile Safari/537.36");
                            string json = wc.DownloadString(url);
                            List<SongItem> list = ParseOfficialJioSaavnJson(json);
                            foreach (SongItem s in list)
                            {
                                if (!IsJunkAudioVariant(s.Title) && !ContainsSongByTitleOrId(freshPicks, s))
                                {
                                    freshPicks.Add(s);
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (freshPicks.Count > 0)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        List<SongItem> merged = new List<SongItem>();
                        int freshAdded = 0;
                        foreach (SongItem fp in freshPicks)
                        {
                            if (!ContainsSongByTitleOrId(quickPlaySongs, fp) && !ContainsSongByTitleOrId(merged, fp))
                            {
                                merged.Add(fp);
                                freshAdded++;
                                if (freshAdded >= 3) break;
                            }
                        }
                        foreach (SongItem oldS in quickPlaySongs)
                        {
                            if (!ContainsSongByTitleOrId(merged, oldS) && merged.Count < 8)
                            {
                                merged.Add(oldS);
                            }
                        }
                        foreach (SongItem fp in freshPicks)
                        {
                            if (!ContainsSongByTitleOrId(merged, fp) && merged.Count < 8)
                            {
                                merged.Add(fp);
                            }
                        }

                        if (merged.Count > 0)
                        {
                            quickPlaySongs = merged;
                            quickPlayUnlocked = true;
                            PrefetchThumbnails(quickPlaySongs);
                            SavePersistentSettings();
                            this.Invalidate();
                        }
                    });
                }
            });
            qpThread.IsBackground = true;
            qpThread.Start();
        }

        private void InitSearchInput()
        {
            searchBox = new TextBox();
            searchBox.BorderStyle = BorderStyle.None;
            searchBox.BackColor = Color.FromArgb(26, 30, 44);
            searchBox.ForeColor = Color.White;
            searchBox.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            searchBox.Location = new Point(244, 60);
            searchBox.Size = new Size(265, 22);
            searchBox.Text = "";
            searchBox.TabStop = false;
            searchBox.Visible = false;
            searchBox.KeyDown += OnSearchBoxKeyDown;
            searchBox.TextChanged += OnSearchBoxTextChanged;
            this.Controls.Add(searchBox);

            adminPassBox = new TextBox();
            adminPassBox.BorderStyle = BorderStyle.FixedSingle;
            adminPassBox.BackColor = Color.FromArgb(16, 20, 32);
            adminPassBox.ForeColor = Color.FromArgb(255, 193, 7);
            adminPassBox.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            adminPassBox.PasswordChar = '*';
            adminPassBox.Size = new Size(210, 26);
            adminPassBox.TabStop = false;
            adminPassBox.Visible = false;
            adminPassBox.KeyDown += OnAdminPassBoxKeyDown;
            this.Controls.Add(adminPassBox);
        }

        private void OnAdminPassBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                VerifyAdminUpdatePassword();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                HideAdminPasswordBox();
            }
        }

        private void ShowAdminPasswordBox()
        {
            isPasswordPromptActive = true;
            adminPassBox.Text = "";
            int mw = 480, mh = 330;
            int mx = (this.ClientSize.Width - mw) / 2;
            int my = (this.ClientSize.Height - mh) / 2;
            adminPassBox.Location = new Point(mx + 24, my + 224);
            adminPassBox.Visible = true;
            adminPassBox.BringToFront();
            adminPassBox.Focus();
            this.Invalidate();
        }

        private void HideAdminPasswordBox()
        {
            isPasswordPromptActive = false;
            if (adminPassBox != null)
            {
                adminPassBox.Visible = false;
                adminPassBox.Text = "";
            }
            this.ActiveControl = null;
            this.Focus();
            this.Invalidate();
        }

        private void VerifyAdminUpdatePassword()
        {
            string entered = (adminPassBox != null ? adminPassBox.Text : "").Trim();
            if (entered == ADMIN_UPDATE_PASSWORD)
            {
                isAdminUpdateUnlocked = true;
                HideAdminPasswordBox();
                ShowToast("Admin Unlocked! Opening Protected Update Config...");
                OpenProtectedUpdateFilesForAdmin();
            }
            else
            {
                if (adminPassBox != null) adminPassBox.Text = "";
                ShowToast("Access Denied: Invalid Password!");
                this.Invalidate();
            }
        }

        private void FocusSearchInput()
        {
            if (isFullPlayerExpanded) isFullPlayerExpanded = false;
            if (activeTab == 4) activeTab = 1;
            isSearchEditing = true;
            searchBox.Visible = true;
            searchBox.Focus();
            searchBox.SelectionStart = searchBox.Text.Length;
            this.Invalidate();
        }

        private void UnfocusSearchInput()
        {
            if (isSearchEditing || searchBox.Focused || searchBox.Visible)
            {
                isSearchEditing = false;
                searchQueryText = searchBox.Text;
                searchBox.Visible = false;
                this.ActiveControl = null;
                this.Focus();
                this.Invalidate();
            }
        }

        private void TriggerButtonBounce(string key)
        {
            btnClickBounce[key] = 1.0f;
            this.Invalidate();
        }

        private float GetButtonScale(string key)
        {
            float hov = btnHoverAnim.ContainsKey(key) ? btnHoverAnim[key] : 0f;
            float bnc = btnClickBounce.ContainsKey(key) ? btnClickBounce[key] : 0f;
            // Spring bounce: dips slightly then overshoots before settling
            float clickEffect = (float)Math.Sin(bnc * Math.PI) * 0.12f;
            return 1.0f + (hov * 0.06f) - clickEffect;
        }

        private Rectangle ScaleRectCentered(Rectangle r, float scale)
        {
            if (Math.Abs(scale - 1.0f) < 0.003f) return r;
            int nw = Math.Max(4, (int)Math.Round(r.Width * scale));
            int nh = Math.Max(4, (int)Math.Round(r.Height * scale));
            int nx = r.X + (r.Width - nw) / 2;
            int ny = r.Y + (r.Height - nh) / 2;
            return new Rectangle(nx, ny, nw, nh);
        }

        // DUAL-DECK WMP AUDIO ENGINE
        private void InitDualWmpAudioEngine()
        {
            try
            {
                wmpType = Type.GetTypeFromProgID("WMPlayer.OCX.7");
                if (wmpType != null)
                {
                    wmpPlayer = Activator.CreateInstance(wmpType);
                    wmpControls = wmpType.InvokeMember("controls", BindingFlags.GetProperty, null, wmpPlayer, null);
                    wmpSettings = wmpType.InvokeMember("settings", BindingFlags.GetProperty, null, wmpPlayer, null);

                    wmpDeckB = Activator.CreateInstance(wmpType);
                    wmpControlsB = wmpType.InvokeMember("controls", BindingFlags.GetProperty, null, wmpDeckB, null);
                    wmpSettingsB = wmpType.InvokeMember("settings", BindingFlags.GetProperty, null, wmpDeckB, null);

                    ApplyEffectiveDspAndVolume();
                }
            }
            catch { }
        }

        private int ComputeTargetEffectiveVolume()
        {
            if (isMuted) return 0;
            if (!eqEnabled) return Math.Max(0, Math.Min(100, volumePercent));

            float eqMeanDb = (eqBands[0] * 1.3f + eqBands[1] * 1.15f + eqBands[2] * 1.0f + eqBands[3] * 1.05f + eqBands[4] * 1.1f) / 5.6f;
            float bassExtra = (bassBoostPct - 35) * 0.14f;
            float loudExtra = loudnessGainPct * 0.18f;

            float pulseMod = 0f;
            if (isPlaying && (eqBands[0] > 2f || bassBoostPct > 40))
            {
                pulseMod = (float)Math.Sin(wavePhase * 1.6f) * ((eqBands[0] + bassBoostPct * 0.1f) * 0.22f);
            }

            int finalVol = (int)Math.Round(volumePercent + eqMeanDb * 1.6f + bassExtra + loudExtra + pulseMod);
            return Math.Max(2, Math.Min(100, finalVol));
        }

        private void ApplyEffectiveDspAndVolume()
        {
            try
            {
                if (wmpSettings != null)
                {
                    int targetV = ComputeTargetEffectiveVolume();
                    int scaledV = (targetV * deckAFadeInProgress) / 100;
                    wmpSettings.GetType().InvokeMember("mute", BindingFlags.SetProperty, null, wmpSettings, new object[] { isMuted });
                    wmpSettings.GetType().InvokeMember("volume", BindingFlags.SetProperty, null, wmpSettings, new object[] { Math.Max(0, Math.Min(100, scaledV)) });

                    int bal = 0;
                    if (eqEnabled && virtualizerPct > 10 && isPlaying)
                    {
                        bal = (int)(Math.Sin(wavePhase * 0.35f) * (virtualizerPct * 0.12f));
                    }
                    wmpSettings.GetType().InvokeMember("balance", BindingFlags.SetProperty, null, wmpSettings, new object[] { bal });
                }
            }
            catch { }
        }

        private void SetVolume(int newVol)
        {
            volumePercent = Math.Max(0, Math.Min(100, newVol));
            isMuted = (volumePercent == 0);
            ApplyEffectiveDspAndVolume();
            SavePersistentSettings();
            ShowToast("Volume: " + volumePercent + "%");
            this.Invalidate();
        }

        private void ToggleMute()
        {
            isMuted = !isMuted;
            ApplyEffectiveDspAndVolume();
            SavePersistentSettings();
            ShowToast(isMuted ? "Muted" : ("Volume: " + volumePercent + "%"));
            this.Invalidate();
        }

        private void ApplyEqPreset(string presetName)
        {
            activeEqPreset = presetName;
            eqEnabled = true;
            if (presetName == "Flat") eqBands = new float[] { 0f, 0f, 0f, 0f, 0f };
            else if (presetName == "Bass Boost") eqBands = new float[] { 7.5f, 4.5f, 0.0f, 1.5f, 3.0f };
            else if (presetName == "Club") eqBands = new float[] { 5.0f, 3.0f, 3.5f, 4.5f, 3.0f };
            else if (presetName == "Hip Hop") eqBands = new float[] { 7.0f, 4.0f, -1.5f, 2.5f, 4.5f };
            else if (presetName == "Rock") eqBands = new float[] { 5.5f, -1.5f, 2.0f, 5.0f, 6.0f };
            else if (presetName == "Pop") eqBands = new float[] { -1.0f, 3.0f, 5.5f, 4.0f, -0.5f };
            else if (presetName == "Vocal Boost") eqBands = new float[] { -3.0f, 1.5f, 6.5f, 5.0f, 2.0f };
            else if (presetName == "Acoustic") eqBands = new float[] { 4.0f, 2.5f, 2.0f, 4.5f, 5.0f };
            else if (presetName == "Electronic") eqBands = new float[] { 6.5f, 3.5f, -0.5f, 4.0f, 6.0f };
            ApplyEffectiveDspAndVolume();
            SavePersistentSettings();
            ShowToast("Applied EQ Preset: " + presetName);
            this.Invalidate();
        }

        private void SeekRelative(double deltaSec)
        {
            if (wmpControls == null) return;
            double target = Math.Max(0.0, Math.Min(currentDurSec, currentPosSec + deltaSec));
            SeekToAbsolute(target);
        }

        private void SeekToAbsolute(double targetSec)
        {
            if (wmpControls == null) return;
            double target = Math.Max(0.0, Math.Min(currentDurSec, targetSec));
            currentPosSec = target;
            smoothPosSec = target;
            try
            {
                wmpControls.GetType().InvokeMember("currentPosition", BindingFlags.SetProperty, null, wmpControls, new object[] { target });
            }
            catch { }
            this.Invalidate();
        }

        private void HandOffToCrossfadeDeckB()
        {
            if (!crossfadeEnabled || crossfadeSeconds <= 0 || !isPlaying || wmpPlayer == null || wmpDeckB == null)
            {
                deckAFadeInProgress = 100;
                return;
            }
            try
            {
                string curUrl = Convert.ToString(wmpType.InvokeMember("URL", BindingFlags.GetProperty, null, wmpPlayer, null));
                if (!string.IsNullOrEmpty(curUrl) && currentPosSec > 1.0)
                {
                    deckBVolume = ComputeTargetEffectiveVolume();
                    if (wmpSettingsB != null)
                    {
                        wmpSettingsB.GetType().InvokeMember("mute", BindingFlags.SetProperty, null, wmpSettingsB, new object[] { isMuted });
                        wmpSettingsB.GetType().InvokeMember("volume", BindingFlags.SetProperty, null, wmpSettingsB, new object[] { deckBVolume });
                    }
                    wmpType.InvokeMember("URL", BindingFlags.SetProperty, null, wmpDeckB, new object[] { curUrl });
                    if (wmpControlsB != null)
                    {
                        wmpControlsB.GetType().InvokeMember("currentPosition", BindingFlags.SetProperty, null, wmpControlsB, new object[] { currentPosSec });
                        wmpControlsB.GetType().InvokeMember("play", BindingFlags.InvokeMethod, null, wmpControlsB, null);
                    }
                    deckAFadeInProgress = 12;
                    return;
                }
            }
            catch { }
            deckAFadeInProgress = 100;
        }

        private void PlaySong(SongItem song)
        {
            PlaySong(song, false);
        }

        private void PlaySong(SongItem song, bool isQueueStep)
        {
            if (song == null) return;

            if (string.IsNullOrEmpty(song.Mood) || song.Mood == "All")
            {
                song.Mood = InferSongMood(song.Title, song.Artist, song.Album, song.Duration);
            }

            if (currentSong != null && currentSong.Id != song.Id && isPlaying && crossfadeEnabled)
            {
                HandOffToCrossfadeDeckB();
            }
            else
            {
                deckAFadeInProgress = 100;
            }

            currentSong = song;
            currentPosSec = 0;
            smoothPosSec = 0;
            lyricSyncOffsetSec = 0.0;
            currentDurSec = song.Duration > 0 ? song.Duration : 210;
            isPlaying = true;
            isAutoEndCrossfadeTriggered = false;
            lyricsSmoothScrollY = 0f;
            TriggerButtonBounce("play");
            ApplyEffectiveDspAndVolume();
            int myPlayVer = ++playRequestVersion;

            // Parse & fetch BetterLyrics time-synced lines + update ambient backdrop color!
            EnsureSyncedLyricsParsed(song);
            FetchBetterLyricsAsync(song);
            UpdateTargetAmbientColorFromSong(song);

            // Record play for Quick Play unlocking & personalization!
            RecordSongPlayForQuickPlay(song);

            if (!isQueueStep)
            {
                BuildSmartSimilarQueueForSeed(song);
            }
            else
            {
                // Always enforce unique titles in queue even when stepping
                EnforceUniqueQueueTitlesInPlace();
                int qIdx = -1;
                for (int i = 0; i < queueSongs.Count; i++)
                {
                    if (queueSongs[i].Id == song.Id) { qIdx = i; break; }
                }
                if (qIdx == -1)
                {
                    queueSongs.Insert(0, song);
                    currentQueueIndex = 0;
                }
                else
                {
                    currentQueueIndex = qIdx;
                }
                SavePersistentSettings();
            }

            PrefetchSingleThumbnail(song.ThumbUrl);

            string safeId = Regex.Replace(song.Id, "[^a-zA-Z0-9_-]", "_");
            string cachedFile = Path.Combine(cacheFolder, safeId + ".m4a");

            if (File.Exists(cachedFile) && new FileInfo(cachedFile).Length > 10000)
            {
                isBuffering = false;
                StartWmpFileOrUrl(cachedFile);
                this.Invalidate();
                return;
            }

            isBuffering = true;
            StartWmpFileOrUrl(song.AudioUrl);
            this.Invalidate();

            Thread cacheThread = new Thread(() =>
            {
                string[] candidates = new string[] { song.AudioUrl, song.FallbackUrl, song.FallbackUrl96 };
                string tempFile = cachedFile + "." + myPlayVer + ".tmp";

                foreach (string candidateUrl in candidates)
                {
                    if (string.IsNullOrEmpty(candidateUrl)) continue;
                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                            wc.DownloadFile(candidateUrl, tempFile);
                        }
                        if (File.Exists(tempFile) && new FileInfo(tempFile).Length > 10000)
                        {
                            try
                            {
                                if (File.Exists(cachedFile)) File.Delete(cachedFile);
                                File.Move(tempFile, cachedFile);
                            }
                            catch
                            {
                                cachedFile = tempFile;
                            }

                            this.BeginInvoke((MethodInvoker)delegate
                            {
                                isBuffering = false;
                                if (playRequestVersion == myPlayVer && currentSong != null && currentSong.Id == song.Id && isPlaying)
                                {
                                    if (currentPosSec < 0.5)
                                    {
                                        StartWmpFileOrUrl(cachedFile);
                                    }
                                }
                                this.Invalidate();
                            });
                            return;
                        }
                    }
                    catch { }
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    isBuffering = false;
                    this.Invalidate();
                });
            });
            cacheThread.IsBackground = true;
            cacheThread.Start();
        }

        private void StartWmpFileOrUrl(string target)
        {
            try
            {
                if (wmpPlayer != null)
                {
                    wmpType.InvokeMember("URL", BindingFlags.SetProperty, null, wmpPlayer, new object[] { target });
                    if (wmpControls != null)
                    {
                        wmpControls.GetType().InvokeMember("play", BindingFlags.InvokeMethod, null, wmpControls, null);
                        if (skipSilence)
                        {
                            try { wmpControls.GetType().InvokeMember("currentPosition", BindingFlags.SetProperty, null, wmpControls, new object[] { 0.8 }); } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private void TogglePlayPause()
        {
            TriggerButtonBounce("play");
            if (wmpPlayer == null || wmpControls == null) return;
            try
            {
                string currentUrl = Convert.ToString(wmpType.InvokeMember("URL", BindingFlags.GetProperty, null, wmpPlayer, null));
                if (string.IsNullOrEmpty(currentUrl))
                {
                    PlaySong(currentSong, true);
                    return;
                }
                if (isPlaying)
                {
                    wmpControls.GetType().InvokeMember("pause", BindingFlags.InvokeMethod, null, wmpControls, null);
                    if (wmpControlsB != null)
                    {
                        try { wmpControlsB.GetType().InvokeMember("stop", BindingFlags.InvokeMethod, null, wmpControlsB, null); } catch { }
                        deckBVolume = 0;
                    }
                    isPlaying = false;
                }
                else
                {
                    wmpControls.GetType().InvokeMember("play", BindingFlags.InvokeMethod, null, wmpControls, null);
                    isPlaying = true;
                }
            }
            catch { }
            this.Invalidate();
        }

        private void NextTrack()
        {
            TriggerButtonBounce("next");
            if (queueSongs.Count == 0) return;
            EnforceUniqueQueueTitlesInPlace();
            if (isShuffle && queueSongs.Count > 1)
            {
                Random rnd = new Random();
                int nextIdx = rnd.Next(0, queueSongs.Count);
                if (nextIdx == currentQueueIndex) nextIdx = (nextIdx + 1) % queueSongs.Count;
                currentQueueIndex = nextIdx;
            }
            else
            {
                currentQueueIndex = (currentQueueIndex + 1) % queueSongs.Count;
            }
            PlaySong(queueSongs[currentQueueIndex], true);
        }

        private void PrevTrack()
        {
            TriggerButtonBounce("prev");
            if (queueSongs.Count == 0) return;
            EnforceUniqueQueueTitlesInPlace();
            currentQueueIndex = (currentQueueIndex - 1 + queueSongs.Count) % queueSongs.Count;
            PlaySong(queueSongs[currentQueueIndex], true);
        }

        private void AddToQueue(SongItem song)
        {
            if (song == null) return;
            if (string.IsNullOrEmpty(song.Mood) || song.Mood == "All")
            {
                song.Mood = InferSongMood(song.Title, song.Artist, song.Album, song.Duration);
            }
            manualQueuedIds.Add(song.Id);
            if (!ContainsSongByTitleOrId(queueSongs, song))
            {
                int insertPos = Math.Min(queueSongs.Count, currentQueueIndex + 1);
                queueSongs.Insert(insertPos, song);
            }
            rightPanelMode = 0;
            SavePersistentSettings();
            ShowToast("Added Next in Queue: " + song.Title);
            this.Invalidate();
        }

        private void DownloadSongDirectly(SongItem song)
        {
            if (song == null || !string.IsNullOrEmpty(downloadingSongId)) return;
            TriggerButtonBounce("download");

            string safeTitle = CleanSongDisplayTitle(song.Title) + " - " + GetPrimaryArtistName(song.Artist) + " (320kbps).m4a";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeTitle = safeTitle.Replace(c.ToString(), "");
            }
            string destFile = Path.Combine(downloadsFolder, safeTitle);

            string safeId = Regex.Replace(song.Id, "[^a-zA-Z0-9_-]", "_");
            string cachedFile = Path.Combine(cacheFolder, safeId + ".m4a");
            if (File.Exists(cachedFile) && new FileInfo(cachedFile).Length > 10000)
            {
                try
                {
                    File.Copy(cachedFile, destFile, true);
                    AddSongToDownloadedList(song);
                    ShowToast("Saved to Downloads: " + safeTitle);
                    this.Invalidate();
                    return;
                }
                catch { }
            }

            downloadingSongId = song.Id;
            downloadProgress = 10;
            ShowToast("Downloading: " + song.Title + "...");

            Thread dlThread = new Thread(() =>
            {
                string[] candidates = new string[] { song.AudioUrl, song.FallbackUrl, song.FallbackUrl96 };
                bool ok = false;
                foreach (string url in candidates)
                {
                    if (string.IsNullOrEmpty(url)) continue;
                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                            wc.DownloadFile(url, destFile);
                        }
                        if (File.Exists(destFile) && new FileInfo(destFile).Length > 10000)
                        {
                            ok = true;
                            break;
                        }
                    }
                    catch { }
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    downloadingSongId = "";
                    downloadProgress = 0;
                    if (ok)
                    {
                        AddSongToDownloadedList(song);
                        ShowToast("Saved to Downloads: " + safeTitle);
                    }
                    else
                    {
                        ShowToast("Download failed");
                    }
                    this.Invalidate();
                });
            });
            dlThread.IsBackground = true;
            dlThread.Start();
        }

        private void AddSongToDownloadedList(SongItem song)
        {
            bool exists = false;
            foreach (SongItem item in downloadedSongs)
            {
                if (item.Id == song.Id) { exists = true; break; }
            }
            if (!exists) downloadedSongs.Insert(0, song);
        }

        private string GetAudioCacheSizeText()
        {
            try
            {
                if (!Directory.Exists(cacheFolder)) return "0.0 MB";
                long bytes = 0;
                foreach (string f in Directory.GetFiles(cacheFolder))
                {
                    bytes += new FileInfo(f).Length;
                }
                double mb = bytes / (1024.0 * 1024.0);
                return mb.ToString("0.0") + " MB";
            }
            catch { return "0.0 MB"; }
        }

        private void ClearAudioCache()
        {
            try
            {
                if (Directory.Exists(cacheFolder))
                {
                    foreach (string f in Directory.GetFiles(cacheFolder))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
                ShowToast("Audio Cache Cleared");
                this.Invalidate();
            }
            catch { }
        }

        private void OpenDiscordLink()
        {
            TriggerButtonBounce("discord");
            try
            {
                ShowToast("Opening Discord: discord.gg/GYwNyJy7sx");
                Process.Start(new ProcessStartInfo("https://discord.gg/GYwNyJy7sx") { UseShellExecute = true });
            }
            catch { }
        }

        // =========================================================================
        // v10.1.0 IN-APP AUTO-UPDATE ENGINE (umas8549/leo-music-updates)
        // Automatically stamps the new version number into LeoLauncher.cs, compiles
        // LeoMusic.exe across all folders (AppData, Current Folder, Desktop), and
        // restarts cleanly into the updated version!
        // =========================================================================

        private void LockUpdateFileReadOnly(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.ReadOnly | FileAttributes.Hidden);
                }
            }
            catch { }
        }

        private void UnlockUpdateFileWritable(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }
            }
            catch { }
        }

        private void LoadAndLockProtectedUpdateConfig()
        {
            try
            {
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeoMusic");
                string[] urlFiles = new string[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_url.txt"),
                    Path.Combine(appDataDir, "update_url.txt")
                };
                foreach (string f in urlFiles)
                {
                    if (File.Exists(f))
                    {
                        try
                        {
                            string raw = File.ReadAllText(f, Encoding.UTF8).Trim();
                            if (!string.IsNullOrEmpty(raw) && raw.StartsWith("http") && !raw.Contains("YOUR_GITHUB_USERNAME"))
                            {
                                updateManifestUrl = raw;
                            }
                        }
                        catch { }
                        LockUpdateFileReadOnly(f);
                    }
                }

                string[] verFiles = new string[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.json"),
                    Path.Combine(appDataDir, "version.json")
                };
                foreach (string vf in verFiles)
                {
                    LockUpdateFileReadOnly(vf);
                }
            }
            catch { }
        }

        private void OpenProtectedUpdateFilesForAdmin()
        {
            try
            {
                string verPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.json");
                string urlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_url.txt");

                UnlockUpdateFileWritable(verPath);
                UnlockUpdateFileWritable(urlPath);

                if (!File.Exists(urlPath))
                {
                    File.WriteAllText(urlPath, updateManifestUrl + Environment.NewLine, Encoding.UTF8);
                }
                if (!File.Exists(verPath))
                {
                    string defaultJson = "{\r\n  \"version\": \"10.1.0\",\r\n  \"title\": \"Leo Music Studio v10.1.0 Update\",\r\n  \"notes\": \"• Connected Live Auto-Updater to umas8549/leo-music-updates\",\r\n  \"source_url\": \"https://raw.githubusercontent.com/umas8549/leo-music-updates/main/LeoLauncher.cs\",\r\n  \"download_url\": \"https://github.com/umas8549/leo-music-updates/releases/download/v10.1.0/Leo_Music_Windows_v10.1.0.zip\"\r\n}\r\n";
                    File.WriteAllText(verPath, defaultJson, Encoding.UTF8);
                }

                Thread editThread = new Thread(() =>
                {
                    try
                    {
                        Process p = Process.Start("notepad.exe", "\"" + verPath + "\"");
                        if (p != null) p.WaitForExit();
                    }
                    catch { }
                    LoadAndLockProtectedUpdateConfig();
                });
                editThread.IsBackground = true;
                editThread.Start();
            }
            catch { }
        }

        private string NormalizeFourPartVersion(string ver)
        {
            string clean = Regex.Replace(ver ?? "10.1.0", @"[^0-9\.]", "").Trim('.');
            if (string.IsNullOrEmpty(clean)) return "10.1.0.0";
            string[] parts = clean.Split('.');
            if (parts.Length == 1) return parts[0] + ".0.0.0";
            if (parts.Length == 2) return parts[0] + "." + parts[1] + ".0.0";
            if (parts.Length == 3) return parts[0] + "." + parts[1] + "." + parts[2] + ".0";
            return parts[0] + "." + parts[1] + "." + parts[2] + "." + parts[3];
        }

        private bool IsRemoteVersionNewer(string remoteVer, string currentVer)
        {
            if (string.IsNullOrEmpty(remoteVer)) return false;
            try
            {
                Version vr = new Version(NormalizeFourPartVersion(remoteVer));
                Version vc = new Version(NormalizeFourPartVersion(currentVer));
                return vr > vc;
            }
            catch
            {
                return !string.Equals(remoteVer.Trim(), currentVer.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        private void CheckForAppUpdatesAsync(bool isManualUserClick)
        {
            if (isCheckingUpdate) return;
            isCheckingUpdate = true;
            if (isManualUserClick)
            {
                TriggerButtonBounce("update_top");
                ShowToast("Checking GitHub (umas8549/leo-music-updates)...");
            }

            Thread upThread = new Thread(() =>
            {
                string json = "";
                bool fetchedFromRemote = false;

                // Always check user's live GitHub raw manifest first (with cache-busting query param so GitHub CDN never serves stale cache!)
                string targetUrl = updateManifestUrl;
                if (string.IsNullOrEmpty(targetUrl) || targetUrl.Contains("YOUR_GITHUB_USERNAME"))
                {
                    targetUrl = DEFAULT_GITHUB_VERSION_URL;
                }

                try
                {
                    string bustUrl = targetUrl + (targetUrl.Contains("?") ? "&" : "?") + "t=" + Environment.TickCount;
                    using (WebClient wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        wc.Headers.Add("User-Agent", "LeoMusicStudio-Updater/10.1");
                        wc.Headers.Add("Cache-Control", "no-cache, no-store");
                        json = wc.DownloadString(bustUrl);
                        if (!string.IsNullOrEmpty(json) && json.Contains("\"version\""))
                        {
                            fetchedFromRemote = true;
                        }
                    }
                }
                catch { }

                // Fallback to local version.json only if offline
                if (string.IsNullOrEmpty(json))
                {
                    try
                    {
                        string localVerFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.json");
                        if (!File.Exists(localVerFile))
                        {
                            localVerFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeoMusic", "version.json");
                        }
                        if (File.Exists(localVerFile))
                        {
                            json = File.ReadAllText(localVerFile, Encoding.UTF8);
                        }
                    }
                    catch { }
                }

                string parsedVer = "";
                string parsedTitle = "";
                string parsedNotes = "";
                string parsedSourceUrl = "";
                string parsedDownloadUrl = "";

                if (!string.IsNullOrEmpty(json))
                {
                    List<string> vList = ExtractAllJsonStringFieldsStatic(json, "version");
                    if (vList.Count > 0) parsedVer = vList[0];

                    List<string> tList = ExtractAllJsonStringFieldsStatic(json, "title");
                    if (tList.Count > 0) parsedTitle = tList[0];

                    List<string> nList = ExtractAllJsonStringFieldsStatic(json, "notes");
                    if (nList.Count > 0) parsedNotes = nList[0];

                    List<string> sList = ExtractAllJsonStringFieldsStatic(json, "source_url");
                    if (sList.Count > 0) parsedSourceUrl = sList[0];

                    List<string> dList = ExtractAllJsonStringFieldsStatic(json, "download_url");
                    if (dList.Count > 0) parsedDownloadUrl = dList[0];
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    isCheckingUpdate = false;

                    if (!string.IsNullOrEmpty(parsedVer))
                    {
                        latestRemoteVersion = parsedVer;
                        if (!string.IsNullOrEmpty(parsedTitle)) latestUpdateTitle = parsedTitle;
                        if (!string.IsNullOrEmpty(parsedNotes)) latestUpdateNotes = parsedNotes;
                        if (!string.IsNullOrEmpty(parsedSourceUrl) && !parsedSourceUrl.Contains("YOUR_GITHUB_USERNAME"))
                        {
                            latestUpdateSourceUrl = parsedSourceUrl;
                        }
                        if (!string.IsNullOrEmpty(parsedDownloadUrl) && !parsedDownloadUrl.Contains("YOUR_GITHUB_USERNAME"))
                        {
                            latestUpdateDownloadUrl = parsedDownloadUrl;
                        }

                        bool isNewer = IsRemoteVersionNewer(parsedVer, CURRENT_APP_VERSION);
                        if (isNewer && fetchedFromRemote)
                        {
                            isUpdatePopupOpen = true;
                            ShowToast("New Update Available: v" + parsedVer);
                            this.Invalidate();
                            return;
                        }
                    }

                    if (isManualUserClick)
                    {
                        isUpdatePopupOpen = true;
                        bool upToDate = !IsRemoteVersionNewer(latestRemoteVersion, CURRENT_APP_VERSION);
                        if (upToDate)
                        {
                            ShowToast("You are running v" + CURRENT_APP_VERSION + " (Latest: v" + latestRemoteVersion + ")");
                        }
                        this.Invalidate();
                    }
                });
            });
            upThread.IsBackground = true;
            upThread.Start();
        }

        // Stamps the target version (e.g., "10.1.0" / "10.1.0.0") directly into C# source code before compiling
        // so even if the developer forgot to edit AssemblyFileVersion inside LeoLauncher.cs on GitHub, the compiled
        // LeoMusic.exe ALWAYS gets the exact new File Version and CURRENT_APP_VERSION!
        private string StampVersionIntoCsSource(string csCode, string newShortVer)
        {
            if (string.IsNullOrEmpty(csCode) || string.IsNullOrEmpty(newShortVer)) return csCode;
            string cleanShort = Regex.Replace(newShortVer, @"[^0-9\.]", "").Trim('.');
            if (string.IsNullOrEmpty(cleanShort)) cleanShort = "10.1.0";
            string fourPart = NormalizeFourPartVersion(cleanShort);

            csCode = Regex.Replace(csCode, @"private const string CURRENT_APP_VERSION = ""[^""]+"";",
                "private const string CURRENT_APP_VERSION = \"" + cleanShort + "\";");
            csCode = Regex.Replace(csCode, @"\[assembly:\s*AssemblyVersion\(""[^""]+""\)\]",
                "[assembly: AssemblyVersion(\"" + fourPart + "\")]");
            csCode = Regex.Replace(csCode, @"\[assembly:\s*AssemblyFileVersion\(""[^""]+""\)\]",
                "[assembly: AssemblyFileVersion(\"" + fourPart + "\")]");
            return csCode;
        }

        private void PerformOneClickSelfUpdate()
        {
            if (isInstallingUpdate) return;
            HideAdminPasswordBox();

            isInstallingUpdate = true;
            ShowToast("Installing Update v" + latestRemoteVersion + " & Recompiling...");
            this.Invalidate();

            Thread instThread = new Thread(() =>
            {
                try
                {
                    string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeoMusic");
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string srcUrl = latestUpdateSourceUrl;
                    if (string.IsNullOrEmpty(srcUrl) || srcUrl.Contains("YOUR_GITHUB_USERNAME"))
                    {
                        srcUrl = DEFAULT_GITHUB_SOURCE_URL;
                    }

                    string newCsCode = "";

                    // 1. Try downloading the latest LeoLauncher.cs from the user's GitHub Raw URL
                    try
                    {
                        string bustSrcUrl = srcUrl + (srcUrl.Contains("?") ? "&" : "?") + "t=" + Environment.TickCount;
                        using (WebClient wc = new WebClient())
                        {
                            wc.Encoding = Encoding.UTF8;
                            wc.Headers.Add("User-Agent", "LeoMusicStudio-Updater/10.1");
                            wc.Headers.Add("Cache-Control", "no-cache, no-store");
                            string downloaded = wc.DownloadString(bustSrcUrl);
                            if (!string.IsNullOrEmpty(downloaded) && downloaded.Length > 5000 && downloaded.Contains("namespace LeoMusicNative"))
                            {
                                newCsCode = downloaded;
                            }
                        }
                    }
                    catch { }

                    // 2. Fallback: If LeoLauncher.cs wasn't uploaded to GitHub yet, use the installed LeoLauncher.cs and stamp the new version!
                    if (string.IsNullOrEmpty(newCsCode))
                    {
                        string localCs = Path.Combine(appDataDir, "LeoLauncher.cs");
                        if (!File.Exists(localCs)) localCs = Path.Combine(baseDir, "LeoLauncher.cs");
                        if (File.Exists(localCs))
                        {
                            newCsCode = File.ReadAllText(localCs, Encoding.UTF8);
                        }
                    }

                    // 3. Also download the release ZIP to Downloads folder if download_url points to a .zip
                    if (!string.IsNullOrEmpty(latestUpdateDownloadUrl) && latestUpdateDownloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string zipName = Path.GetFileName(new Uri(latestUpdateDownloadUrl).LocalPath);
                            if (string.IsNullOrEmpty(zipName)) zipName = "Leo_Music_Update_v" + latestRemoteVersion + ".zip";
                            string zipDest = Path.Combine(downloadsFolder, zipName);
                            using (WebClient wcZip = new WebClient())
                            {
                                wcZip.Headers.Add("User-Agent", "LeoMusicStudio-Updater/10.1");
                                wcZip.DownloadFile(latestUpdateDownloadUrl, zipDest);
                            }
                        }
                        catch { }
                    }

                    if (!string.IsNullOrEmpty(newCsCode) && newCsCode.Contains("namespace LeoMusicNative"))
                    {
                        // Stamp latestRemoteVersion into CURRENT_APP_VERSION and AssemblyFileVersion so the compiled EXE is guaranteed to be v<latestRemoteVersion>!
                        newCsCode = StampVersionIntoCsSource(newCsCode, latestRemoteVersion);
                        string crlfCode = newCsCode.Replace("\r\n", "\n").Replace("\n", "\r\n");
                        byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
                        byte[] body = Encoding.UTF8.GetBytes(crlfCode);
                        byte[] combined = new byte[bom.Length + body.Length];
                        Buffer.BlockCopy(bom, 0, combined, 0, bom.Length);
                        Buffer.BlockCopy(body, 0, combined, bom.Length, body.Length);

                        string targetCsAppData = Path.Combine(appDataDir, "LeoLauncher.cs");
                        string targetCsBase = Path.Combine(baseDir, "LeoLauncher.cs");
                        UnlockUpdateFileWritable(targetCsAppData);
                        UnlockUpdateFileWritable(targetCsBase);

                        try { File.WriteAllBytes(targetCsAppData, combined); } catch { }
                        try { File.WriteAllBytes(targetCsBase, combined); } catch { }

                        // Update local version.json so it matches the newly installed version
                        string updatedVerJson = "{\r\n  \"version\": \"" + latestRemoteVersion + "\",\r\n  \"title\": \"" + latestUpdateTitle.Replace("\"", "'") + "\",\r\n  \"notes\": \"Updated to v" + latestRemoteVersion + "\",\r\n  \"source_url\": \"" + srcUrl + "\",\r\n  \"download_url\": \"" + latestUpdateDownloadUrl + "\"\r\n}\r\n";
                        string verAppData = Path.Combine(appDataDir, "version.json");
                        string verBase = Path.Combine(baseDir, "version.json");
                        UnlockUpdateFileWritable(verAppData);
                        UnlockUpdateFileWritable(verBase);
                        try { File.WriteAllText(verAppData, updatedVerJson, Encoding.UTF8); } catch { }
                        try { File.WriteAllText(verBase, updatedVerJson, Encoding.UTF8); } catch { }
                        LockUpdateFileReadOnly(verAppData);
                        LockUpdateFileReadOnly(verBase);

                        // Write RunAutoUpdate.bat that updates LeoMusic.exe in AppData, BaseDir, AND Desktop, then launches it!
                        string updaterBat = Path.Combine(appDataDir, "RunAutoUpdate.bat");
                        string cleanBaseDir = baseDir.TrimEnd('\\');
                        StringBuilder bat = new StringBuilder();
                        bat.AppendLine("@echo off");
                        bat.AppendLine("ping 127.0.0.1 -n 2 >nul");
                        bat.AppendLine("taskkill /F /IM \"LeoMusic.exe\" >nul 2>&1");
                        bat.AppendLine("taskkill /F /IM \"Leo Music.exe\" >nul 2>&1");
                        bat.AppendLine("ping 127.0.0.1 -n 2 >nul");
                        bat.AppendLine("set \"CSC=%WINDIR%\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe\"");
                        bat.AppendLine("if not exist \"%CSC%\" set \"CSC=%WINDIR%\\Microsoft.NET\\Framework\\v4.0.30319\\csc.exe\"");
                        bat.AppendLine("del /F /Q \"" + appDataDir + "\\LeoMusic.exe\" >nul 2>&1");
                        bat.AppendLine("\"%CSC%\" /nologo /target:winexe /out:\"" + appDataDir + "\\LeoMusic.exe\" /win32icon:\"" + appDataDir + "\\leo_icon.ico\" \"" + targetCsAppData + "\"");
                        bat.AppendLine("if exist \"" + appDataDir + "\\LeoMusic.exe\" (");
                        bat.AppendLine("    copy /Y \"" + appDataDir + "\\LeoMusic.exe\" \"" + cleanBaseDir + "\\LeoMusic.exe\" >nul 2>&1");
                        bat.AppendLine("    copy /Y \"" + appDataDir + "\\LeoMusic.exe\" \"" + cleanBaseDir + "\\Leo Music.exe\" >nul 2>&1");
                        bat.AppendLine("    if exist \"%USERPROFILE%\\Desktop\" copy /Y \"" + appDataDir + "\\LeoMusic.exe\" \"%USERPROFILE%\\Desktop\\Leo Music.exe\" >nul 2>&1");
                        bat.AppendLine("    if exist \"%USERPROFILE%\\OneDrive\\Desktop\" copy /Y \"" + appDataDir + "\\LeoMusic.exe\" \"%USERPROFILE%\\OneDrive\\Desktop\\Leo Music.exe\" >nul 2>&1");
                        bat.AppendLine("    start \"\" \"" + appDataDir + "\\LeoMusic.exe\"");
                        bat.AppendLine(")");
                        File.WriteAllText(updaterBat, bat.ToString(), Encoding.UTF8);

                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            SavePersistentSettings();
                            ProcessStartInfo psi = new ProcessStartInfo(updaterBat);
                            psi.WindowStyle = ProcessWindowStyle.Hidden;
                            psi.CreateNoWindow = true;
                            Process.Start(psi);
                            Application.Exit();
                        });
                        return;
                    }
                }
                catch { }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    isInstallingUpdate = false;
                    if (!string.IsNullOrEmpty(latestUpdateDownloadUrl) && latestUpdateDownloadUrl.StartsWith("http"))
                    {
                        try { Process.Start(new ProcessStartInfo(latestUpdateDownloadUrl) { UseShellExecute = true }); } catch { }
                    }
                    isUpdatePopupOpen = false;
                    ShowToast("Opened update download link");
                    this.Invalidate();
                });
            });
            instThread.IsBackground = true;
            instThread.Start();
        }

        private void ShowToast(string msg)
        {
            statusMessage = msg;
            statusTick = 55;
            this.Invalidate();
        }

        private void OnUiTimerTick(object sender, EventArgs e)
        {
            if (statusTick > 0) statusTick--;

            // 0. Automatic Startup Update Check (~2.5s after launch) & Update Popup Animation
            if (startupUpdateCheckTicks > 0)
            {
                startupUpdateCheckTicks--;
                if (startupUpdateCheckTicks == 0)
                {
                    CheckForAppUpdatesAsync(false);
                }
            }
            float targetUpAnim = isUpdatePopupOpen ? 1.0f : 0.0f;
            if (Math.Abs(updatePopupAnim - targetUpAnim) > 0.004f)
            {
                updatePopupAnim += (targetUpAnim - updatePopupAnim) * 0.25f;
                if (Math.Abs(updatePopupAnim - targetUpAnim) <= 0.008f) updatePopupAnim = targetUpAnim;
            }

            // 1. Smooth Opening & Closing Animation for Full-Screen BetterLyrics / Studio Panel
            float targetFull = isFullPlayerExpanded ? 1.0f : 0.0f;
            if (Math.Abs(fullPlayerAnim - targetFull) > 0.004f)
            {
                fullPlayerAnim += (targetFull - fullPlayerAnim) * 0.24f;
                if (Math.Abs(fullPlayerAnim - targetFull) <= 0.008f) fullPlayerAnim = targetFull;
            }

            // 2. Smooth Tab Opening Fade/Slide Animation
            if (tabFadeAnim < 1.0f)
            {
                tabFadeAnim = Math.Min(1.0f, tabFadeAnim + 0.14f);
            }

            // 3. Smooth Ambient Album-Art Color Cross-Blending
            ambR += (targetAmbR - ambR) * 0.08f;
            ambG += (targetAmbG - ambG) * 0.08f;
            ambB += (targetAmbB - ambB) * 0.08f;

            // 4. Smooth Button Hover Scale & Click Bounce Decay
            List<string> hovKeys = new List<string>(btnHoverAnim.Keys);
            foreach (string k in hovKeys)
            {
                float targetH = (k == currentHoveredBtnKey) ? 1.0f : 0.0f;
                float curH = btnHoverAnim[k];
                curH += (targetH - curH) * 0.28f;
                if (curH < 0.01f && targetH == 0f) btnHoverAnim.Remove(k);
                else btnHoverAnim[k] = curH;
            }
            if (!string.IsNullOrEmpty(currentHoveredBtnKey) && !btnHoverAnim.ContainsKey(currentHoveredBtnKey))
            {
                btnHoverAnim[currentHoveredBtnKey] = 0.25f;
            }

            List<string> bncKeys = new List<string>(btnClickBounce.Keys);
            foreach (string k in bncKeys)
            {
                float v = btnClickBounce[k] - 0.16f;
                if (v <= 0f) btnClickBounce.Remove(k);
                else btnClickBounce[k] = v;
            }

            // 5. Continuous Rotating & Shape-Morphing Artwork + Spinner Phase
            spinnerAngle = (spinnerAngle + 8.5f) % 360f;
            if (isPlaying)
            {
                wavePhase += 0.14f;
                vinylAngle = (vinylAngle + 1.2f) % 360f;
                morphPhase += 0.048f;
            }
            else
            {
                morphPhase += 0.012f;
            }

            // 6. Dual-Deck Crossfade Ramp
            if (deckBVolume > 0 && wmpSettingsB != null)
            {
                int step = Math.Max(1, 100 / Math.Max(8, crossfadeSeconds * 20));
                deckBVolume = Math.Max(0, deckBVolume - step);
                try
                {
                    wmpSettingsB.GetType().InvokeMember("volume", BindingFlags.SetProperty, null, wmpSettingsB, new object[] { deckBVolume });
                    if (deckBVolume == 0 && wmpControlsB != null)
                    {
                        wmpControlsB.GetType().InvokeMember("stop", BindingFlags.InvokeMethod, null, wmpControlsB, null);
                    }
                }
                catch { deckBVolume = 0; }
            }

            if (deckAFadeInProgress < 100)
            {
                int stepIn = Math.Max(2, 100 / Math.Max(8, crossfadeSeconds * 18));
                deckAFadeInProgress = Math.Min(100, deckAFadeInProgress + stepIn);
            }

            if (isPlaying)
            {
                ApplyEffectiveDspAndVolume();
            }

            // 7. Sleep Timer
            if (sleepTimerSecondsLeft > 0 && isPlaying)
            {
                sleepTickCounter++;
                if (sleepTickCounter >= 35)
                {
                    sleepTickCounter = 0;
                    sleepTimerSecondsLeft--;
                    if (sleepTimerSecondsLeft <= 0)
                    {
                        sleepTimerMinutes = 0;
                        TogglePlayPause();
                        ShowToast("Sleep Timer: Playback Paused");
                    }
                }
            }

            // 8. Playback Position & Auto-Crossfade Trigger
            if (wmpPlayer != null && wmpControls != null && isPlaying)
            {
                try
                {
                    smoothPosSec += 0.028;
                    double pos = Convert.ToDouble(wmpControls.GetType().InvokeMember("currentPosition", BindingFlags.GetProperty, null, wmpControls, null));
                    if (pos >= 0)
                    {
                        currentPosSec = pos;
                        if (Math.Abs(pos - smoothPosSec) > 0.38) smoothPosSec = pos;
                        else smoothPosSec += (pos - smoothPosSec) * 0.28;
                    }

                    object media = wmpType.InvokeMember("currentMedia", BindingFlags.GetProperty, null, wmpPlayer, null);
                    if (media != null)
                    {
                        double dur = Convert.ToDouble(media.GetType().InvokeMember("duration", BindingFlags.GetProperty, null, media, null));
                        if (dur > 10)
                        {
                            if (Math.Abs(dur - currentDurSec) > 1.5 && currentSong != null)
                            {
                                currentDurSec = dur;
                                AutoCalibrateLyricsToDuration(currentSong, dur);
                            }
                            else
                            {
                                currentDurSec = dur;
                            }
                        }
                    }

                    if (crossfadeEnabled && crossfadeSeconds > 0 && currentDurSec > 25 && !isAutoEndCrossfadeTriggered)
                    {
                        double rem = currentDurSec - currentPosSec;
                        if (rem > 0.4 && rem <= crossfadeSeconds)
                        {
                            isAutoEndCrossfadeTriggered = true;
                            if (isRepeatOne) PlaySong(currentSong, true);
                            else NextTrack();
                            return;
                        }
                    }

                    int state = Convert.ToInt32(wmpType.InvokeMember("playState", BindingFlags.GetProperty, null, wmpPlayer, null));
                    if (state == 8 || (currentDurSec > 20 && currentPosSec >= currentDurSec - 0.5))
                    {
                        if (isRepeatOne) PlaySong(currentSong, true);
                        else NextTrack();
                    }
                }
                catch { }
            }

            this.Invalidate();
        }

        private void FilterDisplaySongs()
        {
            displaySongs.Clear();
            string q = (searchQueryText ?? "").Trim().ToLowerInvariant();

            if (activeTab == 2)
            {
                foreach (SongItem s in catalog)
                {
                    if (likedIds.Contains(s.Id)) displaySongs.Add(s);
                }
                foreach (SongItem s in liveSearchResults)
                {
                    if (likedIds.Contains(s.Id) && !ContainsSong(displaySongs, s.Id)) displaySongs.Add(s);
                }
                foreach (SongItem s in quickPlaySongs)
                {
                    if (likedIds.Contains(s.Id) && !ContainsSong(displaySongs, s.Id)) displaySongs.Add(s);
                }
                scrollOffset = 0;
                this.Invalidate();
                return;
            }
            else if (activeTab == 3)
            {
                displaySongs.AddRange(downloadedSongs);
                scrollOffset = 0;
                this.Invalidate();
                return;
            }

            if (!string.IsNullOrEmpty(q) && liveSearchResults.Count > 0)
            {
                displaySongs.AddRange(liveSearchResults);
                scrollOffset = 0;
                this.Invalidate();
                return;
            }

            foreach (SongItem s in catalog)
            {
                bool moodMatch = (selectedMood == "All") || s.Mood.Equals(selectedMood, StringComparison.OrdinalIgnoreCase);
                bool queryMatch = string.IsNullOrEmpty(q) || s.Title.ToLowerInvariant().Contains(q) || s.Artist.ToLowerInvariant().Contains(q);
                if (moodMatch && queryMatch)
                {
                    displaySongs.Add(s);
                }
            }
            scrollOffset = 0;
            this.Invalidate();
        }

        private bool ContainsSong(List<SongItem> list, string id)
        {
            foreach (SongItem s in list)
            {
                if (s.Id == id) return true;
            }
            return false;
        }

        private void OnSearchBoxTextChanged(object sender, EventArgs e)
        {
            searchQueryText = searchBox.Text;
            searchDebounceTimer.Stop();
            if (string.IsNullOrEmpty(searchQueryText.Trim()))
            {
                liveSearchResults.Clear();
                FilterDisplaySongs();
            }
            else
            {
                FilterDisplaySongs();
                searchDebounceTimer.Start();
            }
        }

        private void OnSearchDebounceTick(object sender, EventArgs e)
        {
            searchDebounceTimer.Stop();
            string q = (searchQueryText ?? "").Trim();
            if (q.Length >= 2)
            {
                RunLiveSaavnSearch(q);
            }
        }

        private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                searchDebounceTimer.Stop();
                string q = searchBox.Text.Trim();
                UnfocusSearchInput();
                if (!string.IsNullOrEmpty(q))
                {
                    RunLiveSaavnSearch(q);
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                UnfocusSearchInput();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (isPasswordPromptActive && adminPassBox != null && adminPassBox.Visible && adminPassBox.Focused)
            {
                if (keyData == Keys.Escape)
                {
                    HideAdminPasswordBox();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            if (isSearchEditing && searchBox.Visible && searchBox.Focused)
            {
                if (keyData == Keys.Escape)
                {
                    UnfocusSearchInput();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            switch (keyData)
            {
                case Keys.Escape:
                    if (isFullPlayerExpanded)
                    {
                        isFullPlayerExpanded = false;
                        this.Invalidate();
                        return true;
                    }
                    break;
                case Keys.F:
                    isFullPlayerExpanded = !isFullPlayerExpanded;
                    if (isFullPlayerExpanded) UnfocusSearchInput();
                    this.Invalidate();
                    return true;
                case Keys.E:
                    isFullPlayerExpanded = false;
                    activeTab = 4;
                    tabFadeAnim = 0.2f;
                    this.Invalidate();
                    return true;
                case Keys.Space:
                    TogglePlayPause();
                    return true;
                case Keys.Right:
                    SeekRelative(5.0);
                    return true;
                case Keys.Left:
                    SeekRelative(-5.0);
                    return true;
                case Keys.Up:
                    SetVolume(volumePercent + 5);
                    return true;
                case Keys.Down:
                    SetVolume(volumePercent - 5);
                    return true;
                case Keys.N:
                    NextTrack();
                    return true;
                case Keys.P:
                    PrevTrack();
                    return true;
                case Keys.M:
                    ToggleMute();
                    return true;
                case Keys.L:
                    if (currentSong != null)
                    {
                        if (likedIds.Contains(currentSong.Id))
                        {
                            likedIds.Remove(currentSong.Id);
                            ShowToast("Removed from Liked");
                        }
                        else
                        {
                            likedIds.Add(currentSong.Id);
                            ShowToast("Added to Liked");
                        }
                        SavePersistentSettings();
                        this.Invalidate();
                    }
                    return true;
                case Keys.D:
                    DownloadSongDirectly(currentSong);
                    return true;
                case Keys.S:
                    isShuffle = !isShuffle;
                    SavePersistentSettings();
                    ShowToast(isShuffle ? "Shuffle On" : "Shuffle Off");
                    return true;
                case Keys.R:
                    isRepeatOne = !isRepeatOne;
                    SavePersistentSettings();
                    ShowToast(isRepeatOne ? "Repeat One On" : "Repeat Off");
                    return true;
                case Keys.Q:
                    rightPanelMode = (rightPanelMode == 0) ? 1 : 0;
                    SavePersistentSettings();
                    ShowToast(rightPanelMode == 0 ? "Showing Up Next Queue" : "Showing BetterLyrics");
                    this.Invalidate();
                    return true;
                case Keys.OemQuestion:
                case Keys.Divide:
                case (Keys.Control | Keys.F):
                    FocusSearchInput();
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RunLiveSaavnSearch(string query)
        {
            int myVer = ++searchRequestVersion;
            isSearchingNetwork = true;
            ShowToast("Searching \"" + query + "\"...");

            Thread t = new Thread(() =>
            {
                List<SongItem> found = new List<SongItem>();

                try
                {
                    string directUrl = "https://www.jiosaavn.com/api.php?__call=search.getResults&_format=json&n=25&p=1&_marker=0&ctx=android&q=" + Uri.EscapeDataString(query);
                    using (WebClient wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        wc.Headers.Add("User-Agent", "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/120.0 Mobile Safari/537.36");
                        wc.Headers.Add("Accept", "application/json");
                        string json = wc.DownloadString(directUrl);
                        found = ParseOfficialJioSaavnJson(json);
                    }
                }
                catch { }

                if (found.Count == 0)
                {
                    string[] mirrors = new string[]
                    {
                        "https://saavn.dev/api/search/songs?query=" + Uri.EscapeDataString(query) + "&limit=25",
                        "https://saavn.sumit.co/api/search/songs?query=" + Uri.EscapeDataString(query) + "&limit=25"
                    };

                    foreach (string mirrorUrl in mirrors)
                    {
                        try
                        {
                            using (WebClient wc = new WebClient())
                            {
                                wc.Encoding = Encoding.UTF8;
                                wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                                string json = wc.DownloadString(mirrorUrl);
                                found = ParseMirrorSearchJson(json);
                                if (found.Count > 0) break;
                            }
                        }
                        catch { }
                    }
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    if (myVer != searchRequestVersion) return;
                    isSearchingNetwork = false;
                    if (found.Count > 0)
                    {
                        List<SongItem> dedupedSearch = DeduplicateSearchList(found);
                        liveSearchResults = dedupedSearch;
                        selectedMood = "All";
                        if (activeTab == 4) activeTab = 1;
                        displaySongs.Clear();
                        displaySongs.AddRange(dedupedSearch);
                        scrollOffset = 0;
                        PrefetchThumbnails(dedupedSearch);
                        ShowToast("Found " + dedupedSearch.Count + " songs for \"" + query + "\"");
                        this.Invalidate();
                    }
                    else
                    {
                        ShowToast("No online results for \"" + query + "\"");
                        this.Invalidate();
                    }
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        // =========================================================================
        // STRICT UNIQUE SONG TITLE FILTER & SMART SIMILAR-MOOD/TONE QUEUE ENGINE
        // =========================================================================

        private string CleanSongDisplayTitle(string rawTitle)
        {
            if (string.IsNullOrEmpty(rawTitle)) return "Unknown Track";
            string s = rawTitle;
            int pIdx = s.IndexOf(" (From ");
            if (pIdx < 0) pIdx = s.IndexOf(" (from ");
            if (pIdx > 2) s = s.Substring(0, pIdx);
            return s.Trim();
        }

        // Pure C# + Verbatim Regex Canonical Title Normalizer (Zero escape sequence issues!)
        private string GetNormalizedBaseTitle(string rawTitle)
        {
            if (string.IsNullOrEmpty(rawTitle)) return "";
            string s = rawTitle.ToLowerInvariant();

            // Remove (...) and [...] blocks using verbatim regex
            s = Regex.Replace(s, @"\([^\)]*\)", " ");
            s = Regex.Replace(s, @"\[[^\]]*\]", " ");

            // Strip suffixes after " - ", " | ", " : ", " / "
            string[] splitSeps = new string[] { " - ", " | ", " : ", " / ", " – " };
            foreach (string sep in splitSeps)
            {
                int idx = s.IndexOf(sep);
                if (idx > 2) s = s.Substring(0, idx);
            }

            string[] stripTokens = new string[]
            {
                "slowed and reverb", "slowed reverb", "slowed", "reverb", "lofi flip", "lofi mix", "lo-fi", "lofi",
                "dj remix", "club mix", "remix", "unplugged", "reprised", "reprise", "female version", "male version",
                "version", "instrumental", "karaoke", "ringtone", "mashup", "cover", "8d audio", "8d", "bass boosted",
                "sped up", "full video", "full song", "lyrical", "audio song", "official", "title track", "from "
            };
            foreach (string tok in stripTokens)
            {
                s = s.Replace(tok, " ");
            }

            StringBuilder cleanSb = new StringBuilder();
            bool lastWasSpace = true;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    cleanSb.Append(c);
                    lastWasSpace = false;
                }
                else if (!lastWasSpace)
                {
                    cleanSb.Append(' ');
                    lastWasSpace = true;
                }
            }
            return cleanSb.ToString().Trim();
        }

        private bool IsJunkAudioVariant(string rawTitle)
        {
            if (string.IsNullOrEmpty(rawTitle)) return true;
            string low = rawTitle.ToLowerInvariant();
            if (low.Contains("ringtone") || low.Contains("karaoke") || low.Contains("instrumental") ||
                low.Contains("dialogue") || low.Contains("teaser") || low.Contains("trailer") ||
                low.Contains("8d ") || low.Contains("sped up") || low.Contains("dj ") || low.Contains("mashup"))
            {
                return true;
            }
            return false;
        }

        // Strictly detects if titleA and titleB are the same song or a variant/cover/remix of each other
        private bool IsSameSongTitleOrVariant(string titleA, string titleB)
        {
            string normA = GetNormalizedBaseTitle(titleA);
            string normB = GetNormalizedBaseTitle(titleB);
            if (string.IsNullOrEmpty(normA) || string.IsNullOrEmpty(normB)) return false;
            if (normA == normB) return true;

            if (normA.Length >= 4 && normB.Length >= 4)
            {
                if (normA.Contains(normB) || normB.Contains(normA)) return true;
            }

            // Also compare first 2 words (e.g. "tauba tauba bad newz" vs "tauba tauba punjabi")
            string[] wA = normA.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string[] wB = normB.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (wA.Length >= 2 && wB.Length >= 2 && wA[0] == wB[0] && wA[1] == wB[1])
            {
                return true;
            }
            if (wA.Length == 1 && wB.Length >= 1 && wA[0].Length >= 4 && wA[0] == wB[0])
            {
                return true;
            }
            if (wB.Length == 1 && wA.Length >= 1 && wB[0].Length >= 4 && wB[0] == wA[0])
            {
                return true;
            }

            return false;
        }

        // Enforces that queueSongs NEVER contains two songs with the same normalized title!
        private void EnforceUniqueQueueTitlesInPlace()
        {
            List<SongItem> unique = new List<SongItem>();
            foreach (SongItem s in queueSongs)
            {
                if (s == null) continue;
                if (!ContainsSongByTitleOrId(unique, s))
                {
                    unique.Add(s);
                }
            }
            // If deduplication reduced the queue below 6 items, top up with distinct mood-matched catalog songs
            if (unique.Count < 8)
            {
                foreach (SongItem cs in catalog)
                {
                    if (!ContainsSongByTitleOrId(unique, cs))
                    {
                        unique.Add(cs);
                    }
                }
            }
            queueSongs.Clear();
            queueSongs.AddRange(unique);
            if (currentSong != null)
            {
                for (int i = 0; i < queueSongs.Count; i++)
                {
                    if (queueSongs[i].Id == currentSong.Id) { currentQueueIndex = i; break; }
                }
            }
        }

        private List<SongItem> DeduplicateSearchList(List<SongItem> items)
        {
            List<SongItem> result = new List<SongItem>();
            foreach (SongItem s in items)
            {
                if (s == null) continue;
                bool dup = false;
                foreach (SongItem ex in result)
                {
                    if (IsSameSongTitleOrVariant(ex.Title, s.Title) &&
                        GetPrimaryArtistName(ex.Artist).Equals(GetPrimaryArtistName(s.Artist), StringComparison.OrdinalIgnoreCase))
                    {
                        dup = true;
                        break;
                    }
                }
                if (!dup) result.Add(s);
            }
            if (result.Count < 6 && items.Count > result.Count)
            {
                foreach (SongItem s in items)
                {
                    if (!ContainsSong(result, s.Id)) result.Add(s);
                    if (result.Count >= 12) break;
                }
            }
            return result;
        }

        private string GetPrimaryArtistName(string artistField)
        {
            if (string.IsNullOrEmpty(artistField)) return "Studio Artist";
            string[] parts = artistField.Split(new char[] { ',', '&', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                string first = parts[0].Trim();
                int featIdx = first.ToLowerInvariant().IndexOf(" feat");
                if (featIdx > 0) first = first.Substring(0, featIdx).Trim();
                if (!string.IsNullOrEmpty(first)) return first;
            }
            return artistField.Trim();
        }

        private string InferSongMood(string title, string artist, string album, int durationSec)
        {
            string combined = ((title ?? "") + " " + (artist ?? "") + " " + (album ?? "")).ToLowerInvariant();

            if (combined.Contains("party") || combined.Contains("club") || combined.Contains("dance") ||
                combined.Contains("payal") || combined.Contains("tauba") || combined.Contains("badshah") ||
                combined.Contains("honey singh") || combined.Contains("paradox") || combined.Contains("nakhre") ||
                combined.Contains("ghungroo") || combined.Contains("kala chashma") || combined.Contains("bom ") ||
                combined.Contains("hookah") || combined.Contains("dj") || combined.Contains("bhangra") ||
                combined.Contains("chammak") || combined.Contains("floor") || combined.Contains("saturday"))
            {
                return "Party";
            }

            if (combined.Contains("workout") || combined.Contains("gym") || combined.Contains("jackpot") ||
                combined.Contains("gur sidhu") || combined.Contains("cheema y") || combined.Contains("sidhu moose") ||
                combined.Contains("295") || combined.Contains("beast") || combined.Contains("raftaar") ||
                combined.Contains("divine") || combined.Contains("kr$na") || combined.Contains("power") ||
                combined.Contains("levels") || combined.Contains("legend") || combined.Contains("sarkar"))
            {
                return "Workout";
            }

            if (combined.Contains("gedi") || combined.Contains("g.o.a.t") || combined.Contains("goat") ||
                combined.Contains("amplifier") || combined.Contains("imran khan") || combined.Contains("diljit") ||
                combined.Contains("millionaire") || combined.Contains("elevated") || combined.Contains("born to shine") ||
                combined.Contains("lover") || combined.Contains("hass hass") || combined.Contains("energ"))
            {
                return "Energize";
            }

            if (combined.Contains("kesariya") || combined.Contains("arijit") || combined.Contains("pritam") ||
                combined.Contains("atif aslam") || combined.Contains("shreya") || combined.Contains("jubin") ||
                combined.Contains("vishal mishra") || combined.Contains("faded") || combined.Contains("ishq") ||
                combined.Contains("pyaar") || combined.Contains("mohabbat") || combined.Contains("tum hi ho") ||
                combined.Contains("apna bana le") || combined.Contains("satranga") || combined.Contains("shayad") ||
                combined.Contains("raabta") || combined.Contains("hawayein") || combined.Contains("relax") ||
                combined.Contains("unplugged") || combined.Contains("acoustic"))
            {
                return "Relax";
            }

            if (combined.Contains("winning speech") || combined.Contains("mxrci") || combined.Contains("focus") ||
                combined.Contains("study") || combined.Contains("kar har maidan") || combined.Contains("zinda") ||
                combined.Contains("chak de") || combined.Contains("lakshya"))
            {
                return "Focus";
            }

            if (combined.Contains("softly") || combined.Contains("aujla") || combined.Contains("ap dhillon") ||
                combined.Contains("shubh") || combined.Contains("ikky") || combined.Contains("making memories") ||
                combined.Contains("with you") || combined.Contains("admiring you") || combined.Contains("obsessed") ||
                combined.Contains("excuses") || combined.Contains("gurinder") || combined.Contains("chill") ||
                combined.Contains("vibe") || combined.Contains("moon") || combined.Contains("night") ||
                combined.Contains("talwiinder") || combined.Contains("king"))
            {
                return "Chill";
            }

            if (durationSec > 0 && durationSec <= 185) return "Energize";
            if (durationSec >= 245) return "Relax";
            return "Chill";
        }

        private string InferSongToneCluster(SongItem s)
        {
            if (s == null) return "punjabi_chill";
            string text = ((s.Title ?? "") + " " + (s.Artist ?? "") + " " + (s.Album ?? "")).ToLowerInvariant();

            if (text.Contains("alan walker") || text.Contains("faded") || text.Contains("marshmello") ||
                text.Contains("chainsmokers") || text.Contains("weeknd") || text.Contains("ed sheeran") ||
                text.Contains("justin bieber") || text.Contains("coldplay") || text.Contains("imagine dragons") ||
                text.Contains("dua lipa") || text.Contains("english"))
            {
                return "global_pop_edm";
            }

            if (text.Contains("arijit") || text.Contains("pritam") || text.Contains("atif") ||
                text.Contains("shreya") || text.Contains("jubin") || text.Contains("vishal mishra") ||
                text.Contains("darshan raval") || text.Contains("armaan malik") || text.Contains("sachin-jigar") ||
                text.Contains("sonu nigam") || text.Contains("kk") || text.Contains("mohit chauhan") ||
                text.Contains("amit trivedi") || text.Contains("rahman") || text.Contains("kesariya") ||
                text.Contains("brahmastra") || text.Contains("ishq") || text.Contains("tum hi ho") ||
                text.Contains("apna bana le") || text.Contains("satranga") || text.Contains("kabir singh") ||
                text.Contains("aashiqui"))
            {
                return "bollywood_romantic";
            }

            if (text.Contains("gur sidhu") || text.Contains("cheema y") || text.Contains("sidhu moose") ||
                text.Contains("raftaar") || text.Contains("divine") || text.Contains("kr$na") ||
                text.Contains("seedhe maut") || text.Contains("mc stan") || text.Contains("emiway") ||
                text.Contains("bohemia") || text.Contains("jackpot") || text.Contains("winning speech") ||
                text.Contains("mxrci"))
            {
                return "desi_hiphop";
            }

            if (text.Contains("honey singh") || text.Contains("badshah") || text.Contains("paradox") ||
                text.Contains("payal") || text.Contains("tauba") || text.Contains("glory") ||
                text.Contains("harrdy sandhu") || text.Contains("guru randhawa") || text.Contains("neha kakkar") ||
                text.Contains("fazilpuria") || text.Contains("mika singh") || text.Contains("party") ||
                text.Contains("raat di gedi") || text.Contains("amplifier") || text.Contains("imran khan") ||
                text.Contains("nakhre") || text.Contains("qaran"))
            {
                return "punjabi_party";
            }

            if (text.Contains("aujla") || text.Contains("diljit") || text.Contains("ap dhillon") ||
                text.Contains("shubh") || text.Contains("ikky") || text.Contains("softly") ||
                text.Contains("talwiinder") || text.Contains("prophec") || text.Contains("harnoor") ||
                text.Contains("making memories") || text.Contains("excuses") || text.Contains("gurinder"))
            {
                return (s.Mood == "Party" || s.Mood == "Energize") ? "punjabi_party" : "punjabi_chill";
            }

            if (s.Mood == "Relax") return "bollywood_romantic";
            if (s.Mood == "Party") return "punjabi_party";
            if (s.Mood == "Workout" || s.Mood == "Focus") return "desi_hiphop";
            return "punjabi_chill";
        }

        private int ScoreCandidateSimilarity(SongItem seed, SongItem candidate, string seedCluster, string seedPrimaryArtist)
        {
            int score = 0;
            string candCluster = InferSongToneCluster(candidate);
            if (candCluster == seedCluster) score += 55;
            else if ((seedCluster == "punjabi_party" && (candCluster == "punjabi_chill" || candCluster == "desi_hiphop")) ||
                     (seedCluster == "punjabi_chill" && (candCluster == "punjabi_party" || candCluster == "desi_hiphop")) ||
                     (seedCluster == "desi_hiphop" && (candCluster == "punjabi_party" || candCluster == "punjabi_chill")))
            {
                score += 30;
            }

            if (candidate.Mood == seed.Mood) score += 45;
            else if ((seed.Mood == "Party" && candidate.Mood == "Energize") ||
                     (seed.Mood == "Energize" && (candidate.Mood == "Party" || candidate.Mood == "Workout")) ||
                     (seed.Mood == "Chill" && (candidate.Mood == "Relax" || candidate.Mood == "Focus")) ||
                     (seed.Mood == "Relax" && candidate.Mood == "Chill") ||
                     (seed.Mood == "Workout" && (candidate.Mood == "Energize" || candidate.Mood == "Focus")) ||
                     (seed.Mood == "Focus" && (candidate.Mood == "Chill" || candidate.Mood == "Workout")))
            {
                score += 25;
            }

            string candArtistLow = (candidate.Artist ?? "").ToLowerInvariant();
            if (!string.IsNullOrEmpty(seedPrimaryArtist) && candArtistLow.Contains(seedPrimaryArtist.ToLowerInvariant()))
            {
                score += 28;
            }

            int durDiff = Math.Abs(candidate.Duration - seed.Duration);
            if (durDiff <= 30) score += 12;
            else if (durDiff <= 60) score += 6;

            return score;
        }

        private string ExtractRawSaavnPid(string songId)
        {
            if (string.IsNullOrEmpty(songId)) return "";
            if (songId.StartsWith("jio_") || songId.StartsWith("mir_"))
            {
                string[] parts = songId.Split('_');
                if (parts.Length >= 2 && parts[1].Length >= 4)
                {
                    return parts[1];
                }
            }
            return "";
        }

        private void BuildSmartSimilarQueueForSeed(SongItem seedSong)
        {
            if (seedSong == null) return;
            int myQueueVer = ++smartQueueVersion;

            string seedMood = InferSongMood(seedSong.Title, seedSong.Artist, seedSong.Album, seedSong.Duration);
            seedSong.Mood = seedMood;
            string seedCluster = InferSongToneCluster(seedSong);
            string seedPrimaryArtist = GetPrimaryArtistName(seedSong.Artist);

            List<SongItem> initialQueue = new List<SongItem>();
            initialQueue.Add(seedSong);

            foreach (SongItem existing in queueSongs)
            {
                if (manualQueuedIds.Contains(existing.Id) && !IsSameSongTitleOrVariant(existing.Title, seedSong.Title))
                {
                    if (!ContainsSongByTitleOrId(initialQueue, existing))
                    {
                        initialQueue.Add(existing);
                    }
                }
            }

            List<SongItem> localPool = new List<SongItem>();
            foreach (SongItem s in liveSearchResults)
            {
                if (!IsSameSongTitleOrVariant(s.Title, seedSong.Title) && !IsJunkAudioVariant(s.Title))
                {
                    localPool.Add(s);
                }
            }
            foreach (SongItem s in quickPlaySongs)
            {
                if (!IsSameSongTitleOrVariant(s.Title, seedSong.Title) && !IsJunkAudioVariant(s.Title))
                {
                    localPool.Add(s);
                }
            }
            foreach (SongItem s in catalog)
            {
                if (!IsSameSongTitleOrVariant(s.Title, seedSong.Title))
                {
                    localPool.Add(s);
                }
            }

            localPool.Sort(delegate(SongItem a, SongItem b)
            {
                int sa = ScoreCandidateSimilarity(seedSong, a, seedCluster, seedPrimaryArtist);
                int sb = ScoreCandidateSimilarity(seedSong, b, seedCluster, seedPrimaryArtist);
                return sb.CompareTo(sa);
            });

            foreach (SongItem cand in localPool)
            {
                if (!ContainsSongByTitleOrId(initialQueue, cand))
                {
                    initialQueue.Add(cand);
                }
            }

            queueSongs.Clear();
            queueSongs.AddRange(initialQueue);
            currentQueueIndex = 0;
            queueScrollOffset = 0;
            SavePersistentSettings();
            this.Invalidate();

            Thread simThread = new Thread(() =>
            {
                List<SongItem> fetchedCandidates = new List<SongItem>();
                string rawPid = ExtractRawSaavnPid(seedSong.Id);

                if (!string.IsNullOrEmpty(rawPid))
                {
                    try
                    {
                        string recoUrl = "https://www.jiosaavn.com/api.php?__call=reco.getreco&api_version=4&_format=json&_marker=0&ctx=android&pid=" + Uri.EscapeDataString(rawPid);
                        using (WebClient wc = new WebClient())
                        {
                            wc.Encoding = Encoding.UTF8;
                            wc.Headers.Add("User-Agent", "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/120.0 Mobile Safari/537.36");
                            string json = wc.DownloadString(recoUrl);
                            List<SongItem> recoList = ParseOfficialJioSaavnJson(json);
                            fetchedCandidates.AddRange(recoList);
                        }
                    }
                    catch { }

                    if (fetchedCandidates.Count < 6)
                    {
                        try
                        {
                            string mirSugUrl = "https://saavn.dev/api/songs/" + Uri.EscapeDataString(rawPid) + "/suggestions?limit=20";
                            using (WebClient wc = new WebClient())
                            {
                                wc.Encoding = Encoding.UTF8;
                                wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                                string json = wc.DownloadString(mirSugUrl);
                                List<SongItem> sugList = ParseMirrorSearchJson(json);
                                fetchedCandidates.AddRange(sugList);
                            }
                        }
                        catch { }
                    }
                }

                List<string> vibeQueries = BuildSimilarToneQueries(seedSong, seedCluster, seedMood, seedPrimaryArtist);
                foreach (string vq in vibeQueries)
                {
                    if (myQueueVer != smartQueueVersion) return;
                    try
                    {
                        string qUrl = "https://www.jiosaavn.com/api.php?__call=search.getResults&_format=json&n=20&p=1&_marker=0&ctx=android&q=" + Uri.EscapeDataString(vq);
                        using (WebClient wc = new WebClient())
                        {
                            wc.Encoding = Encoding.UTF8;
                            wc.Headers.Add("User-Agent", "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/120.0 Mobile Safari/537.36");
                            string json = wc.DownloadString(qUrl);
                            List<SongItem> qList = ParseOfficialJioSaavnJson(json);
                            fetchedCandidates.AddRange(qList);
                        }
                    }
                    catch { }
                }

                List<SongItem> curatedSimilar = new List<SongItem>();
                foreach (SongItem cand in fetchedCandidates)
                {
                    if (cand == null) continue;
                    if (IsJunkAudioVariant(cand.Title)) continue;
                    if (IsSameSongTitleOrVariant(cand.Title, seedSong.Title)) continue;
                    if (ContainsSongByTitleOrId(curatedSimilar, cand)) continue;

                    cand.Mood = InferSongMood(cand.Title, cand.Artist, cand.Album, cand.Duration);
                    curatedSimilar.Add(cand);
                }

                curatedSimilar.Sort(delegate(SongItem a, SongItem b)
                {
                    int sa = ScoreCandidateSimilarity(seedSong, a, seedCluster, seedPrimaryArtist);
                    int sb = ScoreCandidateSimilarity(seedSong, b, seedCluster, seedPrimaryArtist);
                    return sb.CompareTo(sa);
                });

                List<SongItem> balancedQueue = new List<SongItem>();
                balancedQueue.Add(seedSong);

                foreach (SongItem existing in initialQueue)
                {
                    if (manualQueuedIds.Contains(existing.Id) && !ContainsSongByTitleOrId(balancedQueue, existing))
                    {
                        balancedQueue.Add(existing);
                    }
                }

                Dictionary<string, int> artistCounts = new Dictionary<string, int>();
                foreach (SongItem cand in curatedSimilar)
                {
                    if (balancedQueue.Count >= 22) break;
                    if (ContainsSongByTitleOrId(balancedQueue, cand)) continue;

                    string primArt = GetPrimaryArtistName(cand.Artist).ToLowerInvariant();
                    int cnt = artistCounts.ContainsKey(primArt) ? artistCounts[primArt] : 0;
                    int limit = (primArt == seedPrimaryArtist.ToLowerInvariant()) ? 3 : 2;
                    if (cnt >= limit) continue;

                    artistCounts[primArt] = cnt + 1;
                    balancedQueue.Add(cand);
                }

                foreach (SongItem loc in localPool)
                {
                    if (balancedQueue.Count >= 22) break;
                    if (!ContainsSongByTitleOrId(balancedQueue, loc))
                    {
                        balancedQueue.Add(loc);
                    }
                }

                if (balancedQueue.Count > 1)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (myQueueVer != smartQueueVersion) return;
                        if (currentSong == null || currentSong.Id != seedSong.Id) return;

                        queueSongs.Clear();
                        queueSongs.AddRange(balancedQueue);
                        EnforceUniqueQueueTitlesInPlace();
                        currentQueueIndex = 0;
                        PrefetchThumbnails(queueSongs);
                        SavePersistentSettings();
                        ShowToast("Smart Queue: " + (queueSongs.Count - 1) + " similar " + seedMood + " vibe songs ready");
                        this.Invalidate();
                    });
                }
            });
            simThread.IsBackground = true;
            simThread.Start();
        }

        private bool ContainsSongByTitleOrId(List<SongItem> list, SongItem candidate)
        {
            if (candidate == null) return true;
            foreach (SongItem existing in list)
            {
                if (existing.Id == candidate.Id) return true;
                if (IsSameSongTitleOrVariant(existing.Title, candidate.Title)) return true;
            }
            return false;
        }

        private List<string> BuildSimilarToneQueries(SongItem seed, string cluster, string mood, string primaryArtist)
        {
            List<string> queries = new List<string>();
            if (!string.IsNullOrEmpty(primaryArtist) && primaryArtist != "Studio Artist")
            {
                queries.Add(primaryArtist + " hit songs");
            }

            if (cluster == "punjabi_party")
            {
                queries.Add("Diljit Dosanjh Karan Aujla Badshah party songs");
            }
            else if (cluster == "punjabi_chill")
            {
                queries.Add("Karan Aujla AP Dhillon Shubh Diljit chill songs");
            }
            else if (cluster == "desi_hiphop")
            {
                queries.Add("Karan Aujla Shubh Gur Sidhu Sidhu Moose Wala");
            }
            else if (cluster == "bollywood_romantic")
            {
                queries.Add("Arijit Singh Pritam Vishal Mishra Atif Aslam romantic");
            }
            else if (cluster == "global_pop_edm")
            {
                queries.Add("Alan Walker Marshmello Chainsmokers Weeknd hits");
            }
            else
            {
                queries.Add(primaryArtist + " " + mood + " songs");
            }

            return queries;
        }

        private static string DecryptJioMediaUrl(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return null;
            try
            {
                string clean = encryptedBase64.Replace("\\/", "/").Replace("\\u002B", "+").Trim();
                byte[] keyBytes = Encoding.UTF8.GetBytes("38346591");
                byte[] cipherBytes = Convert.FromBase64String(clean);
                using (DESCryptoServiceProvider des = new DESCryptoServiceProvider())
                {
                    des.Key = keyBytes;
                    des.Mode = CipherMode.ECB;
                    des.Padding = PaddingMode.PKCS7;
                    using (ICryptoTransform decryptor = des.CreateDecryptor())
                    {
                        byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                        string url = Encoding.UTF8.GetString(plainBytes).Trim();
                        if (url.StartsWith("http"))
                        {
                            return url.Replace("http://", "https://");
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private List<string> ExtractSongObjectsFromJson(string json)
        {
            List<string> objects = new List<string>();
            if (string.IsNullOrEmpty(json)) return objects;

            int arrStart = -1;
            string trimmed = json.TrimStart();
            if (trimmed.StartsWith("["))
            {
                arrStart = json.IndexOf('[');
            }
            else
            {
                int resIdx = json.IndexOf("\"results\"");
                if (resIdx < 0) resIdx = json.IndexOf("\"data\"");
                if (resIdx >= 0) arrStart = json.IndexOf('[', resIdx);
                if (arrStart < 0) arrStart = json.IndexOf('[');
            }
            if (arrStart < 0) return objects;

            int depth = 0;
            int objStart = -1;
            bool inStr = false;
            bool escaped = false;

            for (int i = arrStart + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped) { escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }
                if (c == '"') { inStr = !inStr; continue; }
                if (inStr) continue;

                if (c == '{')
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        objects.Add(json.Substring(objStart, i - objStart + 1));
                        objStart = -1;
                    }
                }
                else if (c == ']' && depth == 0)
                {
                    break;
                }
            }
            return objects;
        }

        private string GetTopLevelJsonField(string objJson, string key)
        {
            string pattern = "\"" + key + "\"";
            int depth = 0;
            bool inStr = false;
            bool escaped = false;

            for (int i = 0; i < objJson.Length; i++)
            {
                char c = objJson[i];
                if (escaped) { escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }
                if (c == '"')
                {
                    if (!inStr && depth == 1 && i + pattern.Length <= objJson.Length && objJson.Substring(i, pattern.Length) == pattern)
                    {
                        int colon = objJson.IndexOf(':', i + pattern.Length);
                        if (colon > 0)
                        {
                            int valStart = colon + 1;
                            while (valStart < objJson.Length && char.IsWhiteSpace(objJson[valStart])) valStart++;
                            if (valStart < objJson.Length && objJson[valStart] == '"')
                            {
                                StringBuilder sb = new StringBuilder();
                                bool esc = false;
                                for (int k = valStart + 1; k < objJson.Length; k++)
                                {
                                    char vc = objJson[k];
                                    if (esc)
                                    {
                                        if (vc == 'n') sb.Append('\n');
                                        else if (vc == '"') sb.Append('"');
                                        else if (vc == '/') sb.Append('/');
                                        else if (vc == '\\') sb.Append('\\');
                                        else sb.Append(vc);
                                        esc = false;
                                    }
                                    else if (vc == '\\') esc = true;
                                    else if (vc == '"') break;
                                    else sb.Append(vc);
                                }
                                return sb.ToString();
                            }
                            else if (valStart < objJson.Length && char.IsDigit(objJson[valStart]))
                            {
                                int endNum = valStart;
                                while (endNum < objJson.Length && char.IsDigit(objJson[endNum])) endNum++;
                                return objJson.Substring(valStart, endNum - valStart);
                            }
                        }
                    }
                    inStr = !inStr;
                    continue;
                }
                if (inStr) continue;
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
            }
            return "";
        }

        private List<SongItem> ParseOfficialJioSaavnJson(string json)
        {
            List<SongItem> list = new List<SongItem>();
            List<string> items = ExtractSongObjectsFromJson(json);

            for (int i = 0; i < items.Count; i++)
            {
                string obj = items[i];
                string id = GetTopLevelJsonField(obj, "id");
                if (string.IsNullOrEmpty(id)) id = "jio_" + i;

                string title = GetTopLevelJsonField(obj, "song");
                if (string.IsNullOrEmpty(title)) title = GetTopLevelJsonField(obj, "title");
                if (string.IsNullOrEmpty(title)) title = GetTopLevelJsonField(obj, "name");
                if (string.IsNullOrEmpty(title)) continue;

                string artist = GetTopLevelJsonField(obj, "singers");
                if (string.IsNullOrEmpty(artist)) artist = GetTopLevelJsonField(obj, "primary_artists");
                if (string.IsNullOrEmpty(artist)) artist = GetTopLevelJsonField(obj, "subtitle");
                if (string.IsNullOrEmpty(artist)) artist = "Studio Artist";

                string album = GetTopLevelJsonField(obj, "album");
                if (string.IsNullOrEmpty(album)) album = "Single";

                int dur = 210;
                int.TryParse(GetTopLevelJsonField(obj, "duration"), out dur);
                if (dur <= 0) dur = 210;

                string img = GetTopLevelJsonField(obj, "image");
                if (!string.IsNullOrEmpty(img))
                {
                    img = img.Replace("150x150", "500x500").Replace("50x50", "500x500").Replace(".webp", ".jpg").Replace("http://", "https://");
                }
                else
                {
                    img = catalog[0].ThumbUrl;
                }

                string encUrl = GetTopLevelJsonField(obj, "encrypted_media_url");
                string decrypted = DecryptJioMediaUrl(encUrl);
                if (string.IsNullOrEmpty(decrypted))
                {
                    Match mUrl = Regex.Match(obj, @"https?:\\?/\\?/aac\.saavncdn\.com\\?/[^""\\]+\.mp4");
                    if (mUrl.Success) decrypted = mUrl.Value.Replace("\\/", "/").Replace("http://", "https://");
                }

                if (string.IsNullOrEmpty(decrypted)) continue;

                string baseStream = decrypted.Replace("_96.mp4", "").Replace("_160.mp4", "").Replace("_320.mp4", "");
                string url320 = baseStream + "_320.mp4";
                string url160 = baseStream + "_160.mp4";

                string cleanTitle = CleanSongDisplayTitle(CleanHtml(title));
                string cleanArtist = CleanHtml(artist);
                string cleanAlbum = CleanHtml(album);
                string inferredMood = InferSongMood(cleanTitle, cleanArtist, cleanAlbum, dur);

                SongItem item = new SongItem("jio_" + id + "_" + i, cleanTitle, cleanArtist, cleanAlbum, dur, inferredMood, img, url320, url160, "");
                EnsureSyncedLyricsParsed(item);
                list.Add(item);
            }
            return list;
        }

        private List<SongItem> ParseMirrorSearchJson(string json)
        {
            List<SongItem> list = new List<SongItem>();
            List<string> items = ExtractSongObjectsFromJson(json);

            for (int i = 0; i < items.Count; i++)
            {
                string obj = items[i];
                string id = GetTopLevelJsonField(obj, "id");
                if (string.IsNullOrEmpty(id)) id = "mir_" + i;

                string title = GetTopLevelJsonField(obj, "name");
                if (string.IsNullOrEmpty(title)) title = GetTopLevelJsonField(obj, "title");
                if (string.IsNullOrEmpty(title)) continue;

                int dur = 210;
                int.TryParse(GetTopLevelJsonField(obj, "duration"), out dur);
                if (dur <= 0) dur = 210;

                string artist = GetTopLevelJsonField(obj, "primaryArtists");
                if (string.IsNullOrEmpty(artist))
                {
                    Match am = Regex.Match(obj, @"""primary""\s*:\s*\[\s*\{[^\}]*""name""\s*:\s*""([^""]+)""");
                    if (am.Success) artist = am.Groups[1].Value;
                }
                if (string.IsNullOrEmpty(artist)) artist = "Studio Artist";

                string url320 = "";
                string url160 = "";
                Match m320 = Regex.Match(obj, @"https?:\\?/\\?/aac\.saavncdn\.com\\?/[^""\\]+_320\.mp4");
                Match m160 = Regex.Match(obj, @"https?:\\?/\\?/aac\.saavncdn\.com\\?/[^""\\]+_160\.mp4");
                Match mAny = Regex.Match(obj, @"https?:\\?/\\?/aac\.saavncdn\.com\\?/[^""\\]+\.mp4");

                if (m320.Success) url320 = m320.Value.Replace("\\/", "/").Replace("http://", "https://");
                if (m160.Success) url160 = m160.Value.Replace("\\/", "/").Replace("http://", "https://");
                if (string.IsNullOrEmpty(url320) && mAny.Success)
                {
                    string raw = mAny.Value.Replace("\\/", "/").Replace("http://", "https://");
                    string b = raw.Replace("_96.mp4", "").Replace("_160.mp4", "").Replace("_320.mp4", "");
                    url320 = b + "_320.mp4";
                    url160 = b + "_160.mp4";
                }
                if (string.IsNullOrEmpty(url320)) continue;

                string img = catalog[0].ThumbUrl;
                Match imgM = Regex.Match(obj, @"https?:\\?/\\?/c\.saavncdn\.com\\?/[^""\\]+500x500\.(?:jpg|webp)");
                if (imgM.Success) img = imgM.Value.Replace("\\/", "/").Replace(".webp", ".jpg").Replace("http://", "https://");

                string cleanTitle = CleanSongDisplayTitle(CleanHtml(title));
                string cleanArtist = CleanHtml(artist);
                string inferredMood = InferSongMood(cleanTitle, cleanArtist, "Single", dur);

                SongItem item = new SongItem("mir_" + id + "_" + i, cleanTitle, cleanArtist, "Single", dur, inferredMood, img, url320, url160, "");
                EnsureSyncedLyricsParsed(item);
                list.Add(item);
            }
            return list;
        }

        private string CleanHtml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&quot;", "\"").Replace("&amp;", "&").Replace("&#039;", "'").Replace("&lt;", "<").Replace("&gt;", ">");
        }

        private void PrefetchThumbnails(List<SongItem> items)
        {
            foreach (SongItem s in items)
            {
                PrefetchSingleThumbnail(s.ThumbUrl);
            }
        }

        private void PrefetchSingleThumbnail(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            string jpgUrl = url.Replace(".webp", ".jpg");
            lock (loadingImages)
            {
                if (imageCache.ContainsKey(jpgUrl) || loadingImages.Contains(jpgUrl)) return;
                loadingImages.Add(jpgUrl);
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "Mozilla/5.0");
                        byte[] data = wc.DownloadData(jpgUrl);
                        using (MemoryStream ms = new MemoryStream(data))
                        {
                            Image img = Image.FromStream(ms);
                            Bitmap bmp = new Bitmap(img);
                            ComputeDominantColorFromBitmap(jpgUrl, bmp);
                            lock (imageCache)
                            {
                                imageCache[jpgUrl] = bmp;
                            }
                            this.BeginInvoke((MethodInvoker)delegate { this.Invalidate(); });
                        }
                    }
                }
                catch { }
            });
        }

        // VECTOR ICON HELPERS
        private void DrawPlayTriangle(Graphics g, Brush brush, float cx, float cy, float radius)
        {
            PointF[] pts = new PointF[]
            {
                new PointF(cx - radius * 0.42f, cy - radius * 0.65f),
                new PointF(cx - radius * 0.42f, cy + radius * 0.65f),
                new PointF(cx + radius * 0.68f, cy)
            };
            g.FillPolygon(brush, pts);
        }

        private void DrawPauseBars(Graphics g, Brush brush, float cx, float cy, float barW, float barH, float gap)
        {
            g.FillRectangle(brush, cx - gap / 2f - barW, cy - barH / 2f, barW, barH);
            g.FillRectangle(brush, cx + gap / 2f, cy - barH / 2f, barW, barH);
        }

        private void DrawSkipPrevIcon(Graphics g, Brush brush, float cx, float cy)
        {
            g.FillRectangle(brush, cx - 7f, cy - 6f, 2.2f, 12f);
            PointF[] pts = new PointF[]
            {
                new PointF(cx - 4f, cy),
                new PointF(cx + 6f, cy - 6f),
                new PointF(cx + 6f, cy + 6f)
            };
            g.FillPolygon(brush, pts);
        }

        private void DrawSkipNextIcon(Graphics g, Brush brush, float cx, float cy)
        {
            PointF[] pts = new PointF[]
            {
                new PointF(cx - 6f, cy - 6f),
                new PointF(cx - 6f, cy + 6f),
                new PointF(cx + 4f, cy)
            };
            g.FillPolygon(brush, pts);
            g.FillRectangle(brush, cx + 4.8f, cy - 6f, 2.2f, 12f);
        }

        private void DrawDownloadArrow(Graphics g, Color color, float cx, float cy)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx, cy - 5.5f, cx, cy + 2.5f);
                g.DrawLine(p, cx - 3.8f, cy - 1.0f, cx, cy + 3.0f);
                g.DrawLine(p, cx + 3.8f, cy - 1.0f, cx, cy + 3.0f);
                g.DrawLine(p, cx - 5f, cy + 5.5f, cx + 5f, cy + 5.5f);
            }
        }

        private void DrawPlusQueueIcon(Graphics g, Color color, float cx, float cy)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 5f, cy, cx + 5f, cy);
                g.DrawLine(p, cx, cy - 5f, cx, cy + 5f);
            }
        }

        private void DrawExpandIcon(Graphics g, Color color, float cx, float cy)
        {
            using (Pen p = new Pen(color, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 5f, cy + 5f, cx + 5f, cy - 5f);
                g.DrawLine(p, cx + 1f, cy - 5f, cx + 5f, cy - 5f);
                g.DrawLine(p, cx + 5f, cy - 1f, cx + 5f, cy - 5f);
                g.DrawLine(p, cx - 5f, cy + 1f, cx - 5f, cy + 5f);
                g.DrawLine(p, cx - 1f, cy + 5f, cx - 5f, cy + 5f);
            }
        }

        private void DrawHeartIcon(Graphics g, Color color, float cx, float cy, bool filled)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddBezier(cx, cy - 2.5f, cx - 4f, cy - 7f, cx - 8f, cy - 1f, cx, cy + 6f);
                path.AddBezier(cx, cy + 6f, cx + 8f, cy - 1f, cx + 4f, cy - 7f, cx, cy - 2.5f);
                path.CloseFigure();
                if (filled)
                {
                    using (SolidBrush b = new SolidBrush(color)) g.FillPath(b, path);
                }
                else
                {
                    using (Pen p = new Pen(color, 1.5f)) g.DrawPath(p, path);
                }
            }
        }

        private void DrawToggleSwitch(Graphics g, Rectangle rect, bool isOn)
        {
            using (SolidBrush b = new SolidBrush(isOn ? accentColor : Color.FromArgb(40, 45, 62)))
            {
                FillRoundRect(g, b, rect, rect.Height / 2);
            }
            int knobSz = rect.Height - 6;
            int kx = isOn ? (rect.Right - knobSz - 3) : (rect.X + 3);
            using (SolidBrush kb = new SolidBrush(isOn ? Color.Black : Color.White))
            {
                g.FillEllipse(kb, kx, rect.Y + 3, knobSz, knobSz);
            }
        }

        private void DrawEqResponsiveVisualizer(Graphics g, int startX, int baseY, int numBars, int spacing, float barW, float maxH)
        {
            using (SolidBrush vb = new SolidBrush(accentColor))
            {
                for (int i = 0; i < numBars; i++)
                {
                    float bandPos = (float)i / Math.Max(1, numBars - 1) * 4f;
                    int b0 = Math.Min(4, (int)bandPos);
                    int b1 = Math.Min(4, b0 + 1);
                    float frac = bandPos - b0;
                    float dbGain = eqEnabled ? (eqBands[b0] * (1f - frac) + eqBands[b1] * frac) : 0f;
                    if (b0 <= 1 && eqEnabled) dbGain += (bassBoostPct * 0.05f);

                    float eqMult = Math.Max(0.28f, Math.Min(1.75f, 1.0f + dbGain * 0.065f));
                    float bh = isPlaying ? (4f + (float)Math.Abs(Math.Sin(wavePhase * 1.25f + i * 0.52f)) * maxH * eqMult) : 4f;
                    bh = Math.Min(maxH * 1.45f, bh);
                    g.FillRectangle(vb, startX + i * spacing, baseY - bh, barW, bh);
                }
            }
        }

        // =========================================================================
        // UPGRADED BETTERLYRICS KARAOKE REAL-TIME AUTO-SYNC RENDERER
        // Uses sub-frame smoothPosSec clock for 35-FPS continuous syllable fill,
        // active-line soft glow, intro spinner, and instant sync controls!
        // =========================================================================

        private void PaintBetterLyricsCanvas(Graphics g, Rectangle bounds, bool isFullScreenStyle, StringFormat sfCenter)
        {
            if (currentSong == null) return;
            EnsureSyncedLyricsParsed(currentSong);
            List<LyricLine> lines = currentSong.SyncedLines;

            Region oldClip = g.Clip;
            g.SetClip(bounds);

            // 1. Header: "Lyrics from BetterLyrics • Auto-Synced"
            int headerY = bounds.Y + (isFullScreenStyle ? 10 : 8);
            string hdrText = isFullScreenStyle
                ? ("Lyrics from BetterLyrics  •  " + lyricsSyncSourceLabel + (Math.Abs(lyricSyncOffsetSec) > 0.05 ? (" (" + lyricSyncOffsetSec.ToString("+0.0;-0.0") + "s)") : ""))
                : "Lyrics from BetterLyrics";

            using (SolidBrush hdrBrush = new SolidBrush(Color.FromArgb(200, 245, 238, 235)))
            {
                g.DrawString(hdrText, fontBetterHeader, hdrBrush,
                    new RectangleF(bounds.X + 12, headerY, bounds.Width - (isFullScreenStyle ? 150 : 24), 22),
                    isFullScreenStyle ? sfCenter : sfCenter);
            }

            // In Full-Screen / Full-Panel mode, provide subtle Sync Fine-Tune Pills (-0.5s | Auto | +0.5s) in top-right of lyrics box
            if (isFullScreenStyle)
            {
                syncMinusBtnRect = new Rectangle(bounds.Right - 142, headerY - 1, 40, 22);
                syncResetBtnRect = new Rectangle(bounds.Right - 98, headerY - 1, 46, 22);
                syncPlusBtnRect = new Rectangle(bounds.Right - 48, headerY - 1, 40, 22);

                using (SolidBrush sb = new SolidBrush(Color.FromArgb(55, 0, 0, 0)))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(215, 255, 255, 255)))
                {
                    FillRoundRect(g, sb, syncMinusBtnRect, 8);
                    FillRoundRect(g, sb, syncResetBtnRect, 8);
                    FillRoundRect(g, sb, syncPlusBtnRect, 8);
                    g.DrawString("-0.5s", fontSmallBold, tb, syncMinusBtnRect, sfCenter);
                    g.DrawString("Sync", fontSmallBold, tb, syncResetBtnRect, sfCenter);
                    g.DrawString("+0.5s", fontSmallBold, tb, syncPlusBtnRect, sfCenter);
                }
            }
            else
            {
                syncMinusBtnRect = Rectangle.Empty;
                syncResetBtnRect = Rectangle.Empty;
                syncPlusBtnRect = Rectangle.Empty;
            }

            if (lines == null || lines.Count == 0)
            {
                g.Clip = oldClip;
                return;
            }

            // Use high-precision sub-frame smoothPosSec + lyricSyncOffsetSec so word highlight glides without stepping!
            double syncClock = Math.Max(0.0, smoothPosSec + lyricSyncOffsetSec);

            int activeIdx = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (syncClock >= lines[i].StartSec && syncClock <= lines[i].EndSec)
                {
                    activeIdx = i;
                    break;
                }
                if (syncClock >= lines[i].StartSec)
                {
                    activeIdx = i;
                }
            }

            // 2. Intro circular countdown spinner before vocals start (matching 1000101257.jpg!)
            bool showIntroSpinner = (syncClock < lines[0].StartSec) || isLoadingBetterLyrics;
            int topContentOffset = bounds.Y + (isFullScreenStyle ? 40 : 34);
            if (showIntroSpinner)
            {
                int spSz = isFullScreenStyle ? 28 : 20;
                int spX = bounds.X + (bounds.Width - spSz) / 2;
                int spY = topContentOffset;
                using (Pen ringBg = new Pen(Color.FromArgb(65, 255, 255, 255), 3.0f))
                using (Pen ringFg = new Pen(Color.FromArgb(240, 255, 255, 255), 3.0f))
                {
                    ringFg.StartCap = LineCap.Round;
                    ringFg.EndCap = LineCap.Round;
                    g.DrawEllipse(ringBg, spX, spY, spSz, spSz);
                    float sweep = 95f;
                    if (syncClock < lines[0].StartSec && lines[0].StartSec > 0.4)
                    {
                        sweep = (float)Math.Max(45.0, Math.Min(340.0, (syncClock / lines[0].StartSec) * 340.0));
                    }
                    g.DrawArc(ringFg, spX, spY, spSz, spSz, spinnerAngle, sweep);
                }
                topContentOffset += spSz + (isFullScreenStyle ? 14 : 8);
            }

            Font fActive = isFullScreenStyle ? fontLyricsFullActive : fontLyricsSideActive;
            Font fIdle = isFullScreenStyle ? fontLyricsFullIdle : fontLyricsSideIdle;
            int padSide = isFullScreenStyle ? 36 : 14;
            int maxLineW = Math.Max(120, bounds.Width - padSide * 2);

            // Calculate exact cumulative Y position of activeIdx so scrolling locks onto the active line regardless of wrapped line heights!
            float activeLineOffsetTop = 0f;
            float currentCumY = 0f;
            List<List<string>> allWrappedRows = new List<List<string>>();
            List<int> blockHeights = new List<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                bool isCur = (i == activeIdx && syncClock >= lines[i].StartSec);
                Font uf = isCur ? fActive : fIdle;
                List<string> sr = WrapLyricTextToRows(g, lines[i].Text, uf, maxLineW);
                allWrappedRows.Add(sr);
                int bh = sr.Count * (isFullScreenStyle ? 33 : 22) + (isFullScreenStyle ? 18 : 11);
                blockHeights.Add(bh);
                if (i == Math.Max(0, activeIdx))
                {
                    activeLineOffsetTop = currentCumY;
                }
                currentCumY += bh;
            }

            // Keep active line positioned comfortably at ~28% from the top of the lyrics viewport
            float targetViewportAnchor = (bounds.Height - (topContentOffset - bounds.Y)) * 0.24f;
            float targetScrollY = Math.Max(0f, activeLineOffsetTop - targetViewportAnchor);
            lyricsSmoothScrollY += (targetScrollY - lyricsSmoothScrollY) * 0.18f;

            activeLyricHitRects.Clear();
            activeLyricHitTimes.Clear();

            float drawY = topContentOffset - lyricsSmoothScrollY;

            for (int i = 0; i < lines.Count; i++)
            {
                LyricLine ll = lines[i];
                bool isCurrent = (i == activeIdx && syncClock >= ll.StartSec);
                Font useFont = isCurrent ? fActive : fIdle;
                List<string> subRows = allWrappedRows[i];
                int blockH = blockHeights[i];

                if (drawY + blockH >= bounds.Y + 26 && drawY <= bounds.Bottom - 6)
                {
                    Rectangle hitR = new Rectangle(bounds.X + padSide / 2, (int)drawY - 2, bounds.Width - padSide, blockH);
                    activeLyricHitRects.Add(hitR);
                    activeLyricHitTimes.Add(ll.StartSec);

                    // Subtle active line pill highlight in Full View
                    if (isCurrent && isFullScreenStyle)
                    {
                        using (SolidBrush actBg = new SolidBrush(Color.FromArgb(24, 255, 255, 255)))
                        {
                            FillRoundRect(g, actBg, new Rectangle(bounds.X + 16, (int)drawY - 6, bounds.Width - 32, blockH - 4), 14);
                        }
                    }

                    int distFromFocus = Math.Abs(i - Math.Max(0, activeIdx));
                    int idleAlpha = Math.Max(40, 138 - distFromFocus * 24);
                    if (drawY < bounds.Y + 55) idleAlpha = Math.Min(idleAlpha, 65);
                    if (drawY + blockH > bounds.Bottom - 35) idleAlpha = Math.Min(idleAlpha, 55);

                    float lineProg = 0f;
                    if (isCurrent)
                    {
                        double span = Math.Max(0.45, ll.EndSec - ll.StartSec);
                        lineProg = (float)Math.Max(0.0, Math.Min(1.0, (syncClock - ll.StartSec) / span));
                    }

                    int totalChars = Math.Max(1, ll.Text.Length);
                    int charsBeforeRow = 0;
                    float rowY = drawY;
                    int rowStepH = isFullScreenStyle ? 33 : 22;

                    for (int r = 0; r < subRows.Count; r++)
                    {
                        string rowText = subRows[r];
                        SizeF rowSz = g.MeasureString(rowText, useFont);
                        float rowX = isFullScreenStyle ? (bounds.X + padSide) : (bounds.X + 14);
                        if (showIntroSpinner && isFullScreenStyle)
                        {
                            rowX = bounds.X + (bounds.Width - rowSz.Width) / 2f;
                        }

                        Color baseCol = isCurrent
                            ? Color.FromArgb(110, 245, 235, 230)
                            : Color.FromArgb(idleAlpha, 235, 222, 215);

                        using (SolidBrush baseBrush = new SolidBrush(baseCol))
                        {
                            g.DrawString(rowText, useFont, baseBrush, rowX, rowY);
                        }

                        // Progressive Left-to-Right Karaoke Character/Syllable Highlight Layer for Active Line!
                        if (isCurrent && lineProg > 0.005f)
                        {
                            float rowStartFrac = (float)charsBeforeRow / totalChars;
                            float rowEndFrac = (float)(charsBeforeRow + rowText.Length) / totalChars;
                            float rowLocalProg = 0f;
                            if (lineProg >= rowEndFrac) rowLocalProg = 1.0f;
                            else if (lineProg > rowStartFrac)
                            {
                                rowLocalProg = (lineProg - rowStartFrac) / Math.Max(0.04f, rowEndFrac - rowStartFrac);
                            }

                            if (rowLocalProg > 0.005f)
                            {
                                float clipW = rowSz.Width * rowLocalProg;
                                Region prevLineClip = g.Clip;
                                RectangleF highlightClip = new RectangleF(rowX, rowY - 3f, clipW, rowStepH + 8f);
                                g.SetClip(highlightClip, CombineMode.Intersect);

                                using (SolidBrush glowB = new SolidBrush(Color.FromArgb(65, 255, 255, 255)))
                                {
                                    g.DrawString(rowText, useFont, glowB, rowX + 1.2f, rowY + 1.2f);
                                }
                                using (SolidBrush activeWhite = new SolidBrush(Color.White))
                                {
                                    g.DrawString(rowText, useFont, activeWhite, rowX, rowY);
                                }

                                g.Clip = prevLineClip;
                            }
                        }

                        charsBeforeRow += rowText.Length + 1;
                        rowY += rowStepH;
                    }
                }

                drawY += blockH;
            }

            g.Clip = oldClip;
        }

        private List<string> WrapLyricTextToRows(Graphics g, string text, Font font, int maxWidth)
        {
            List<string> rows = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                rows.Add("");
                return rows;
            }
            string[] words = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder current = new StringBuilder();
            foreach (string w in words)
            {
                string test = current.Length == 0 ? w : (current.ToString() + " " + w);
                SizeF sz = g.MeasureString(test, font);
                if (sz.Width > maxWidth && current.Length > 0)
                {
                    rows.Add(current.ToString());
                    current = new StringBuilder(w);
                }
                else
                {
                    current = new StringBuilder(test);
                }
            }
            if (current.Length > 0) rows.Add(current.ToString());
            return rows;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            int sidebarW = 195;
            int rightW = 315;
            int playerH = 90;
            int mainH = h - playerH;

            Color curBgMain = oledPureBlack ? Color.Black : bgMain;
            Color curBgSide = oledPureBlack ? Color.FromArgb(6, 7, 10) : bgSidebar;
            Color curBgCard = oledPureBlack ? Color.FromArgb(13, 15, 22) : bgCard;

            using (SolidBrush mainB = new SolidBrush(curBgMain))
            {
                g.FillRectangle(mainB, 0, 0, w, h);
            }

            StringFormat sfCenter = new StringFormat();
            sfCenter.Alignment = StringAlignment.Center;
            sfCenter.LineAlignment = StringAlignment.Center;
            sfCenter.Trimming = StringTrimming.EllipsisCharacter;
            sfCenter.FormatFlags = StringFormatFlags.NoWrap;

            StringFormat sfLeftEllipsis = new StringFormat();
            sfLeftEllipsis.Alignment = StringAlignment.Near;
            sfLeftEllipsis.LineAlignment = StringAlignment.Center;
            sfLeftEllipsis.Trimming = StringTrimming.EllipsisCharacter;
            sfLeftEllipsis.FormatFlags = StringFormatFlags.NoWrap;

            if (fullPlayerAnim < 0.99f)
            {
                // 1. Left Minimal Sidebar
                using (SolidBrush sb = new SolidBrush(curBgSide))
                {
                    g.FillRectangle(sb, 0, 0, sidebarW, mainH);
                }

                Rectangle crestRect = new Rectangle(20, 18, 30, 30);
                using (SolidBrush ab = new SolidBrush(Color.FromArgb(35, accentColor)))
                using (Pen ap = new Pen(accentColor, 1.8f))
                {
                    FillRoundRect(g, ab, crestRect, 9);
                    DrawRoundRect(g, ap, crestRect, 9);
                    using (SolidBrush gb = new SolidBrush(accentColor))
                    {
                        g.FillRectangle(gb, crestRect.X + 7, crestRect.Y + 11, 2.5f, 8f);
                        g.FillRectangle(gb, crestRect.X + 13, crestRect.Y + 6, 2.5f, 18f);
                        g.FillRectangle(gb, crestRect.X + 19, crestRect.Y + 9, 2.5f, 12f);
                    }
                    g.DrawString("Leo", fontBrand, Brushes.White, 58, 17);
                }

                string[] navLabels = new string[] { "Home", "Search", "Liked", "Downloads", "Settings & EQ" };
                for (int i = 0; i < navLabels.Length; i++)
                {
                    string btnKey = "nav_" + i;
                    Rectangle baseNavRect = new Rectangle(14, 68 + i * 43, sidebarW - 28, 37);
                    Rectangle navRect = ScaleRectCentered(baseNavRect, GetButtonScale(btnKey));
                    bool isSel = (activeTab == i);
                    bool isHov = (hoveredNavIndex == i);

                    if (isSel)
                    {
                        using (SolidBrush b = new SolidBrush(accentColor))
                        {
                            FillRoundRect(g, b, navRect, 10);
                        }
                    }
                    else if (isHov)
                    {
                        using (SolidBrush b = new SolidBrush(bgCardHover))
                        {
                            FillRoundRect(g, b, navRect, 10);
                        }
                    }

                    using (Font fNav = new Font("Segoe UI", 10f, isSel ? FontStyle.Bold : FontStyle.Regular))
                    using (SolidBrush tb = new SolidBrush(isSel ? Color.Black : (isHov ? textWhite : textSub)))
                    {
                        g.DrawString(navLabels[i], fNav, tb, navRect.X + 16, navRect.Y + 8);
                    }
                }

                Rectangle discordSideBtn = ScaleRectCentered(new Rectangle(14, mainH - 158, sidebarW - 28, 36), GetButtonScale("discord_side"));
                using (SolidBrush dsb = new SolidBrush(Color.FromArgb(40, 88, 101, 242)))
                using (Pen dsp = new Pen(Color.FromArgb(160, 88, 101, 242), 1.2f))
                {
                    FillRoundRect(g, dsb, discordSideBtn, 10);
                    DrawRoundRect(g, dsp, discordSideBtn, 10);
                }
                using (Font fDisc = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (SolidBrush dtb = new SolidBrush(Color.FromArgb(140, 155, 255)))
                {
                    g.DrawString("Discord Community", fDisc, dtb, discordSideBtn, sfCenter);
                }

                Rectangle fullBtnSide = ScaleRectCentered(new Rectangle(14, mainH - 116, sidebarW - 28, 36), GetButtonScale("full_side"));
                using (SolidBrush fbs = new SolidBrush(Color.FromArgb(26, accentColor.R, accentColor.G, accentColor.B)))
                using (Pen fbp = new Pen(Color.FromArgb(90, accentColor), 1f))
                {
                    FillRoundRect(g, fbs, fullBtnSide, 10);
                    DrawRoundRect(g, fbp, fullBtnSide, 10);
                }
                using (Font fExp = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (SolidBrush eb = new SolidBrush(accentColor))
                {
                    g.DrawString("⤢  Full Studio Panel", fExp, eb, fullBtnSide, sfCenter);
                }

                using (SolidBrush mb = new SolidBrush(textMuted))
                {
                    g.DrawString("THEME", fontSmallBold, mb, 18, mainH - 64);
                }
                for (int i = 0; i < 5; i++)
                {
                    Rectangle dotRect = ScaleRectCentered(new Rectangle(18 + i * 31, mainH - 42, 20, 20), GetButtonScale("theme_" + i));
                    using (SolidBrush tb = new SolidBrush(themes[i].Accent))
                    {
                        g.FillEllipse(tb, dotRect);
                    }
                    if (i == activeThemeIdx)
                    {
                        using (Pen p = new Pen(Color.White, 2f))
                        {
                            g.DrawEllipse(p, dotRect);
                        }
                    }
                }

                // 2. Center Stage
                int centerX = sidebarW;
                int centerW = w - sidebarW - rightW;
                int slideOffsetY = (int)((1.0f - tabFadeAnim) * 14f);

                GraphicsState centerState = g.Save();
                g.TranslateTransform(0, slideOffsetY);
                if (activeTab == 4)
                {
                    PaintSettingsAndEqualizerScreen(g, centerX, centerW, mainH, curBgCard, sfCenter, sfLeftEllipsis);
                }
                else
                {
                    PaintCenterSongStage(g, centerX, centerW, mainH, curBgCard, sfCenter, sfLeftEllipsis);
                }
                g.Restore(centerState);

                // 3. Right Docked Now-Playing + BetterLyrics / Queue Panel
                int rightX = w - rightW;
                PaintRightDockedPanel(g, rightX, rightW, mainH, curBgSide, curBgCard, sfCenter, sfLeftEllipsis);

                // 4. Bottom Main Song Player Bar
                PaintBottomPlayerBar(g, w, h, playerH, sfCenter, sfLeftEllipsis);
            }

            // Smooth Opening & Closing Animation Overlay for Full BetterLyrics / Studio Panel!
            if (fullPlayerAnim > 0.01f && currentSong != null)
            {
                GraphicsState fullState = g.Save();
                float slideY = (1.0f - fullPlayerAnim) * (h * 0.16f);
                g.TranslateTransform(0, slideY);
                PaintFullNowPlayingPanel(g, w, h, fullPlayerAnim, sfCenter, sfLeftEllipsis);
                g.Restore(fullState);
            }

            // Window Top-Right Controls
            using (Pen winPen = new Pen(textSub, 1.5f))
            {
                g.DrawLine(winPen, w - 94, 20, w - 84, 20);
                g.DrawRectangle(winPen, w - 62, 14, 10, 10);
                g.DrawLine(winPen, w - 32, 14, w - 22, 24);
                g.DrawLine(winPen, w - 22, 14, w - 32, 24);
            }

            if (updatePopupAnim > 0.01f)
            {
                PaintUpdatePopupModal(g, w, h, updatePopupAnim, sfCenter, sfLeftEllipsis);
            }

            if (statusTick > 0 && !string.IsNullOrEmpty(statusMessage))
            {
                int midX = w / 2;
                Rectangle toastRect = new Rectangle(midX - 200, h - playerH - 46, 400, 34);
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(235, 22, 26, 38)))
                using (Pen tp = new Pen(accentColor, 1.5f))
                {
                    FillRoundRect(g, tb, toastRect, 17);
                    DrawRoundRect(g, tp, toastRect, 17);
                }
                using (Font fToast = new Font("Segoe UI", 9f, FontStyle.Bold))
                {
                    g.DrawString(statusMessage, fToast, Brushes.White, toastRect, sfCenter);
                }
            }
        }

        private void DrawPadlockIcon(Graphics g, Color col, float cx, float cy, bool unlocked)
        {
            using (Pen p = new Pen(col, 1.6f))
            using (SolidBrush b = new SolidBrush(col))
            {
                RectangleF body = new RectangleF(cx - 5f, cy - 1f, 10f, 8f);
                g.FillRectangle(b, body);
                if (unlocked)
                {
                    g.DrawArc(p, cx - 3.5f, cy - 6.5f, 7f, 6f, 180f, 130f);
                }
                else
                {
                    g.DrawArc(p, cx - 3.5f, cy - 6.5f, 7f, 7f, 180f, 180f);
                }
            }
        }

        private void PaintUpdatePopupModal(Graphics g, int w, int h, float anim, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            int scrimAlpha = (int)(anim * 185);
            using (SolidBrush scrim = new SolidBrush(Color.FromArgb(scrimAlpha, 4, 6, 12)))
            {
                g.FillRectangle(scrim, 0, 0, w, h);
            }

            int mw = (int)(480 * (0.88f + 0.12f * anim));
            int mh = (int)(330 * (0.88f + 0.12f * anim));
            int mx = (w - mw) / 2;
            int my = (h - mh) / 2;
            Rectangle modalRect = new Rectangle(mx, my, mw, mh);

            using (SolidBrush mb = new SolidBrush(Color.FromArgb(248, 18, 22, 34)))
            using (Pen mp = new Pen(accentColor, 2.0f))
            {
                FillRoundRect(g, mb, modalRect, 20);
                DrawRoundRect(g, mp, modalRect, 20);
            }

            Rectangle badgeRect = new Rectangle(mx + 24, my + 20, 175, 24);
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(45, accentColor.R, accentColor.G, accentColor.B)))
            {
                FillRoundRect(g, bb, badgeRect, 12);
            }
            using (SolidBrush bt = new SolidBrush(accentColor))
            {
                g.DrawString("⚡ AUTO-UPDATE CENTER", fontSmallBold, bt, badgeRect, sfCenter);
            }

            using (SolidBrush verB = new SolidBrush(textSub))
            {
                g.DrawString("Installed: v" + CURRENT_APP_VERSION + "  →  Latest: v" + latestRemoteVersion, fontSmallBold, verB, mx + 210, my + 24);
            }

            using (Font fModalTitle = new Font("Segoe UI", 14f, FontStyle.Bold))
            {
                g.DrawString(latestUpdateTitle, fModalTitle, Brushes.White, new RectangleF(mx + 24, my + 52, mw - 48, 28), sfLeftEllipsis);
            }

            Rectangle notesBox = new Rectangle(mx + 24, my + 86, mw - 48, 114);
            using (SolidBrush nb = new SolidBrush(Color.FromArgb(200, 11, 14, 22)))
            using (Pen np = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
            {
                FillRoundRect(g, nb, notesBox, 12);
                DrawRoundRect(g, np, notesBox, 12);
            }

            using (SolidBrush ab = new SolidBrush(accentColor))
            {
                g.DrawString("WHAT'S NEW IN v" + latestRemoteVersion + ":", fontSmallBold, ab, notesBox.X + 14, notesBox.Y + 10);
            }

            string[] noteLines = (latestUpdateNotes ?? "").Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int ny = notesBox.Y + 30;
            using (SolidBrush ntb = new SolidBrush(Color.FromArgb(230, 240, 245, 250)))
            {
                for (int i = 0; i < noteLines.Length; i++)
                {
                    if (ny + 18 > notesBox.Bottom - 6) break;
                    g.DrawString(noteLines[i], fontSubReg, ntb, new RectangleF(notesBox.X + 14, ny, notesBox.Width - 28, 20), sfLeftEllipsis);
                    ny += 21;
                }
            }

            if (isPasswordPromptActive)
            {
                DrawPadlockIcon(g, accentColor, mx + 30, my + 212, false);
                using (SolidBrush pb = new SolidBrush(accentColor))
                {
                    g.DrawString("Enter Admin Password to Unlock Update File:", fontSmallBold, pb, mx + 42, my + 205);
                }
                Rectangle unlockConfirmBtn = new Rectangle(mx + 242, my + 224, 88, 26);
                using (SolidBrush ucb = new SolidBrush(accentColor))
                {
                    FillRoundRect(g, ucb, unlockConfirmBtn, 8);
                }
                g.DrawString("Unlock", fontSmallBold, Brushes.Black, unlockConfirmBtn, sfCenter);
            }
            else
            {
                DrawPadlockIcon(g, isAdminUpdateUnlocked ? accentColor : textMuted, mx + 30, my + 223, isAdminUpdateUnlocked);
                using (SolidBrush hintB = new SolidBrush(textMuted))
                {
                    string lockMsg = isAdminUpdateUnlocked
                        ? "Admin Mode Unlocked: You can edit version.json & update_url.txt."
                        : "Update configuration is password-protected against user tampering.";
                    g.DrawString(lockMsg, fontSmallBold, hintB, mx + 42, my + 216);
                }
            }

            int btnY = my + mh - 54;
            updateInstallBtnRect = new Rectangle(mx + 24, btnY, 214, 38);
            updateEditUrlBtnRect = new Rectangle(mx + 248, btnY, 114, 38);
            updateLaterBtnRect = new Rectangle(mx + 372, btnY, 84, 38);

            using (SolidBrush ib = new SolidBrush(accentColor))
            {
                FillRoundRect(g, ib, updateInstallBtnRect, 12);
            }
            g.DrawString(isInstallingUpdate ? "Updating..." : "⚡ Update & Restart Now", fontSmallBold, Brushes.Black, updateInstallBtnRect, sfCenter);

            using (SolidBrush eb = new SolidBrush(Color.FromArgb(35, 42, 60)))
            {
                FillRoundRect(g, eb, updateEditUrlBtnRect, 12);
            }
            DrawPadlockIcon(g, Brushes.White is SolidBrush ? Color.White : Color.White, updateEditUrlBtnRect.X + 18, updateEditUrlBtnRect.Y + 19, isAdminUpdateUnlocked);
            g.DrawString(isAdminUpdateUnlocked ? "  Edit Config" : "  Admin Config", fontSmallBold, Brushes.White, updateEditUrlBtnRect, sfCenter);

            using (SolidBrush lb = new SolidBrush(Color.FromArgb(28, 33, 48)))
            {
                FillRoundRect(g, lb, updateLaterBtnRect, 12);
            }
            using (SolidBrush lt = new SolidBrush(textSub))
            {
                g.DrawString("Later", fontSmallBold, lt, updateLaterBtnRect, sfCenter);
            }
        }

        private int GetCenterSongListTopY()
        {
            if (activeTab == 0 && string.IsNullOrEmpty(searchQueryText))
            {
                return quickPlayUnlocked ? 236 : 192;
            }
            return 146;
        }

        private void PaintCenterSongStage(Graphics g, int centerX, int centerW, int mainH, Color curBgCard, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            Rectangle searchPill = new Rectangle(centerX + 26, 48, 330, 36);
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(26, 30, 44)))
            using (Pen sp = new Pen(isSearchEditing ? accentColor : Color.FromArgb(45, 50, 70), isSearchEditing ? 1.5f : 1f))
            {
                FillRoundRect(g, sb, searchPill, 18);
                DrawRoundRect(g, sp, searchPill, 18);
            }

            using (Pen mp = new Pen(textSub, 1.6f))
            {
                g.DrawEllipse(mp, searchPill.X + 14, searchPill.Y + 11, 10, 10);
                g.DrawLine(mp, searchPill.X + 22, searchPill.Y + 19, searchPill.X + 26, searchPill.Y + 23);
            }

            if (!isSearchEditing)
            {
                using (Font fPh = new Font("Segoe UI", 9.5f))
                using (SolidBrush pb = new SolidBrush(string.IsNullOrEmpty(searchQueryText) ? textMuted : textWhite))
                {
                    string displayStr = string.IsNullOrEmpty(searchQueryText) ? "Search any song or artist..." : searchQueryText;
                    g.DrawString(displayStr, fPh, pb, searchPill.X + 34, searchPill.Y + 9);
                }
            }

            if (!string.IsNullOrEmpty(searchQueryText))
            {
                using (Pen xp = new Pen(textSub, 1.5f))
                {
                    int cx = searchPill.Right - 18;
                    int cy = searchPill.Y + 18;
                    g.DrawLine(xp, cx - 4, cy - 4, cx + 4, cy + 4);
                    g.DrawLine(xp, cx + 4, cy - 4, cx - 4, cy + 4);
                }
            }

            Rectangle searchGoBtn = ScaleRectCentered(new Rectangle(searchPill.Right + 8, 48, 78, 36), GetButtonScale("search_go"));
            using (SolidBrush sgb = new SolidBrush(isSearchingNetwork ? accentColor : curBgCard))
            {
                FillRoundRect(g, sgb, searchGoBtn, 18);
            }
            using (SolidBrush sgt = new SolidBrush(isSearchingNetwork ? Color.Black : accentColor))
            {
                g.DrawString(isSearchingNetwork ? "..." : "Search", fontSmallBold, sgt, searchGoBtn, sfCenter);
            }

            Rectangle topDiscordBtn = ScaleRectCentered(new Rectangle(searchPill.Right + 94, 48, 84, 36), GetButtonScale("discord_top"));
            using (SolidBrush dbb = new SolidBrush(Color.FromArgb(88, 101, 242)))
            {
                FillRoundRect(g, dbb, topDiscordBtn, 18);
            }
            g.DrawString("Discord", fontSmallBold, Brushes.White, topDiscordBtn, sfCenter);

            Rectangle topUpdateBtn = ScaleRectCentered(new Rectangle(searchPill.Right + 186, 48, 96, 36), GetButtonScale("update_top"));
            using (SolidBrush ubb = new SolidBrush(Color.FromArgb(35, accentColor.R, accentColor.G, accentColor.B)))
            using (Pen ubp = new Pen(accentColor, 1.3f))
            {
                FillRoundRect(g, ubb, topUpdateBtn, 18);
                DrawRoundRect(g, ubp, topUpdateBtn, 18);
            }
            using (SolidBrush ubt = new SolidBrush(accentColor))
            {
                g.DrawString(isCheckingUpdate ? "Checking..." : "↻ v" + CURRENT_APP_VERSION, fontSmallBold, ubt, topUpdateBtn, sfCenter);
            }

            // Mood Filter Chips
            int chipX = centerX + 26;
            int chipY = 96;
            for (int i = 0; i < moods.Length; i++)
            {
                string m = moods[i];
                SizeF sz = g.MeasureString(m, fontSmallBold);
                int cw = (int)sz.Width + 22;
                Rectangle cRect = ScaleRectCentered(new Rectangle(chipX, chipY, cw, 27), GetButtonScale("mood_" + i));
                bool active = (selectedMood == m);

                using (SolidBrush cb = new SolidBrush(active ? accentColor : curBgCard))
                {
                    FillRoundRect(g, cb, cRect, 13);
                }
                using (SolidBrush ctb = new SolidBrush(active ? Color.Black : textSub))
                {
                    g.DrawString(m, fontSmallBold, ctb, cRect, sfCenter);
                }
                chipX += cw + 7;
            }

            // QUICK PLAY SECTION (Shown on Home tab: unlocks after playing songs, persisted & refreshed with new songs on startup!)
            if (activeTab == 0 && string.IsNullOrEmpty(searchQueryText))
            {
                if (!quickPlayUnlocked || quickPlaySongs.Count == 0)
                {
                    Rectangle qpBanner = new Rectangle(centerX + 26, 134, centerW - 52, 44);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(20, accentColor.R, accentColor.G, accentColor.B)))
                    using (Pen p = new Pen(Color.FromArgb(70, accentColor), 1f))
                    {
                        FillRoundRect(g, b, qpBanner, 11);
                        DrawRoundRect(g, p, qpBanner, 11);
                    }
                    using (SolidBrush tb = new SolidBrush(textSub))
                    {
                        g.DrawString("⚡ Quick Play: Play any song below to unlock your personalized Quick Play feed (auto-saved with fresh songs on every launch!)",
                            fontSubReg, tb, new RectangleF(qpBanner.X + 14, qpBanner.Y, qpBanner.Width - 28, qpBanner.Height), sfLeftEllipsis);
                    }
                }
                else
                {
                    using (SolidBrush ab = new SolidBrush(accentColor))
                    {
                        g.DrawString("⚡ QUICK PLAY  •  PERSONALIZED & FRESH PICKS", fontSmallBold, ab, centerX + 28, 132);
                    }

                    Rectangle qpRefreshBtn = ScaleRectCentered(new Rectangle(centerX + centerW - 118, 128, 92, 22), GetButtonScale("qp_refresh"));
                    using (SolidBrush rb = new SolidBrush(curBgCard))
                    {
                        FillRoundRect(g, rb, qpRefreshBtn, 11);
                    }
                    using (SolidBrush rt = new SolidBrush(accentColor))
                    {
                        g.DrawString("↻ New Songs", fontSmallBold, rt, qpRefreshBtn, sfCenter);
                    }

                    int numQp = Math.Min(4, quickPlaySongs.Count);
                    int gap = 10;
                    int cardW = (centerW - 52 - (numQp - 1) * gap) / Math.Max(1, numQp);
                    for (int i = 0; i < numQp; i++)
                    {
                        SongItem qps = quickPlaySongs[i];
                        bool isHovQp = (hoveredQuickPlayIndex == i);
                        bool isCurQp = (currentSong != null && currentSong.Id == qps.Id);
                        Rectangle baseQpRect = new Rectangle(centerX + 26 + i * (cardW + gap), 154, cardW, 68);
                        Rectangle qpRect = ScaleRectCentered(baseQpRect, GetButtonScale("qp_" + i));

                        using (SolidBrush cb = new SolidBrush(isCurQp ? Color.FromArgb(42, accentColor.R, accentColor.G, accentColor.B) : (isHovQp ? bgCardHover : curBgCard)))
                        {
                            FillRoundRect(g, cb, qpRect, 12);
                        }
                        if (isCurQp || isHovQp)
                        {
                            using (Pen p = new Pen(Color.FromArgb(isCurQp ? 160 : 80, accentColor), 1.2f))
                            {
                                DrawRoundRect(g, p, qpRect, 12);
                            }
                        }

                        DrawStaticAlbumArt(g, qps.ThumbUrl, new Rectangle(qpRect.X + 8, qpRect.Y + 8, 52, 52), 10);
                        using (SolidBrush wb = new SolidBrush(isCurQp ? accentColor : textWhite))
                        using (SolidBrush sb = new SolidBrush(textSub))
                        {
                            g.DrawString(qps.Title, fontSmallBold, wb, new RectangleF(qpRect.X + 66, qpRect.Y + 12, qpRect.Width - 72, 20), sfLeftEllipsis);
                            g.DrawString(qps.Artist, fontSubReg, sb, new RectangleF(qpRect.X + 66, qpRect.Y + 33, qpRect.Width - 72, 18), sfLeftEllipsis);
                        }
                    }
                }
            }

            int listTop = GetCenterSongListTopY();
            int rowH = 58;
            Region oldClip = g.Clip;
            g.SetClip(new Rectangle(centerX, listTop, centerW, mainH - listTop - 8));

            for (int i = 0; i < displaySongs.Count; i++)
            {
                int ry = listTop + i * (rowH + 6) - scrollOffset;
                if (ry + rowH < listTop || ry > mainH) continue;

                SongItem s = displaySongs[i];
                bool isCur = (currentSong != null && currentSong.Id == s.Id);
                bool isHov = (hoveredSongIndex == i);

                Rectangle baseRowRect = new Rectangle(centerX + 26, ry, centerW - 52, rowH);
                Rectangle rowRect = isHov ? ScaleRectCentered(baseRowRect, 1.012f) : baseRowRect;

                using (SolidBrush rb = new SolidBrush(isCur ? Color.FromArgb(35, accentColor.R, accentColor.G, accentColor.B) : (isHov ? bgCardHover : curBgCard)))
                {
                    FillRoundRect(g, rb, rowRect, 12);
                }
                if (isCur)
                {
                    using (Pen ap = new Pen(Color.FromArgb(120, accentColor), 1f))
                    {
                        DrawRoundRect(g, ap, rowRect, 12);
                    }
                }

                Rectangle thumbRect = new Rectangle(rowRect.X + 8, rowRect.Y + 8, 42, 42);
                DrawStaticAlbumArt(g, s.ThumbUrl, thumbRect, 10);

                using (SolidBrush wb = new SolidBrush(isCur ? accentColor : textWhite))
                using (SolidBrush subB = new SolidBrush(textSub))
                {
                    int textMaxW = rowRect.Width - 250;
                    g.DrawString(s.Title, fontTitleBold, wb, new RectangleF(rowRect.X + 62, rowRect.Y + 8, textMaxW, 22), sfLeftEllipsis);
                    g.DrawString(s.Artist, fontSubReg, subB, new RectangleF(rowRect.X + 62, rowRect.Y + 29, textMaxW, 20), sfLeftEllipsis);
                }

                using (SolidBrush mb = new SolidBrush(textMuted))
                {
                    g.DrawString(FormatSec(s.Duration), fontSubReg, mb, rowRect.Right - 175, rowRect.Y + 20);
                }

                Rectangle addQBtn = new Rectangle(rowRect.Right - 130, rowRect.Y + 14, 30, 30);
                using (SolidBrush qb = new SolidBrush(Color.FromArgb(30, 35, 50)))
                {
                    FillRoundRect(g, qb, addQBtn, 8);
                }
                DrawPlusQueueIcon(g, textSub, addQBtn.X + 15f, addQBtn.Y + 15f);

                bool liked = likedIds.Contains(s.Id);
                DrawHeartIcon(g, liked ? accentColor : textMuted, rowRect.Right - 78, rowRect.Y + 29, liked);

                Rectangle dlBtn = new Rectangle(rowRect.Right - 52, rowRect.Y + 14, 40, 30);
                bool isDlThis = (downloadingSongId == s.Id);
                using (SolidBrush dlb = new SolidBrush(isDlThis ? accentColor : Color.FromArgb(34, 39, 56)))
                {
                    FillRoundRect(g, dlb, dlBtn, 8);
                }
                if (isDlThis)
                {
                    g.DrawString(downloadProgress + "%", fontSmallBold, Brushes.Black, dlBtn, sfCenter);
                }
                else
                {
                    DrawDownloadArrow(g, accentColor, dlBtn.X + dlBtn.Width / 2f, dlBtn.Y + dlBtn.Height / 2f);
                }
            }
            g.Clip = oldClip;
        }

        private void PaintSettingsAndEqualizerScreen(Graphics g, int centerX, int centerW, int mainH, Color curBgCard, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            int padX = centerX + 26;
            int contentW = centerW - 52;

            using (Font fHead = new Font("Segoe UI", 14.5f, FontStyle.Bold))
            using (SolidBrush wb = new SolidBrush(textWhite))
            {
                g.DrawString("Studio DSP, Equalizer & Playback Settings (Auto-Saved)", fHead, wb, padX, 24);
            }

            Rectangle eqCard = new Rectangle(padX, 64, contentW, 232);
            using (SolidBrush cb = new SolidBrush(curBgCard))
            {
                FillRoundRect(g, cb, eqCard, 14);
            }

            using (Font fSec = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (SolidBrush ab = new SolidBrush(accentColor))
            {
                g.DrawString("5-BAND HARDWARE EQUALIZER & DSP (" + (eqEnabled ? activeEqPreset : "OFF") + ")", fSec, ab, eqCard.X + 18, eqCard.Y + 14);
            }

            Rectangle eqToggleRect = new Rectangle(eqCard.Right - 64, eqCard.Y + 12, 44, 22);
            DrawToggleSwitch(g, eqToggleRect, eqEnabled);

            int px = eqCard.X + 18;
            int py = eqCard.Y + 42;
            using (Font fChip = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            {
                for (int i = 0; i < eqPresetNames.Length; i++)
                {
                    string pn = eqPresetNames[i];
                    int pw = (int)g.MeasureString(pn, fChip).Width + 16;
                    if (px + pw > eqCard.Right - 16) break;
                    Rectangle pr = new Rectangle(px, py, pw, 24);
                    bool act = eqEnabled && (activeEqPreset == pn);
                    using (SolidBrush pb = new SolidBrush(act ? accentColor : Color.FromArgb(30, 35, 50)))
                    {
                        FillRoundRect(g, pb, pr, 12);
                    }
                    using (SolidBrush pt = new SolidBrush(act ? Color.Black : textSub))
                    {
                        g.DrawString(pn, fChip, pt, pr, sfCenter);
                    }
                    px += pw + 6;
                }
            }

            int bandStartX = eqCard.X + 36;
            int bandTopY = eqCard.Y + 82;
            int bandTrackH = 108;
            int bandSpacing = 54;

            using (SolidBrush subB = new SolidBrush(textSub))
            {
                for (int i = 0; i < 5; i++)
                {
                    int bx = bandStartX + i * bandSpacing;
                    using (Pen tp = new Pen(Color.FromArgb(40, 46, 66), 4.5f))
                    {
                        tp.StartCap = LineCap.Round;
                        tp.EndCap = LineCap.Round;
                        g.DrawLine(tp, bx, bandTopY, bx, bandTopY + bandTrackH);
                    }

                    float norm = (eqBands[i] + 10f) / 20f;
                    int thumbY = bandTopY + (int)((1f - norm) * bandTrackH);

                    using (Pen ap = new Pen(eqEnabled ? accentColor : textMuted, 4.5f))
                    {
                        ap.StartCap = LineCap.Round;
                        ap.EndCap = LineCap.Round;
                        g.DrawLine(ap, bx, thumbY, bx, bandTopY + bandTrackH);
                    }
                    g.FillEllipse(Brushes.White, bx - 6, thumbY - 6, 12, 12);
                    g.DrawString(eqBandLabels[i], fontSmallBold, subB, new RectangleF(bx - 24, bandTopY + bandTrackH + 6, 48, 16), sfCenter);
                }
            }

            int dspStartX = eqCard.X + 320;
            int dspW = Math.Max(140, eqCard.Right - dspStartX - 24);
            string[] dspNames = new string[] { "Bass Boost", "3D Virtualizer", "Loudness Gain" };
            int[] dspVals = new int[] { bassBoostPct, virtualizerPct, loudnessGainPct };

            using (SolidBrush wb = new SolidBrush(textWhite))
            using (SolidBrush sb = new SolidBrush(accentColor))
            {
                for (int i = 0; i < 3; i++)
                {
                    int dy = eqCard.Y + 86 + i * 46;
                    g.DrawString(dspNames[i], fontSmallBold, wb, dspStartX, dy);
                    g.DrawString(dspVals[i] + "%", fontSmallBold, sb, dspStartX + dspW - 38, dy);

                    int barY = dy + 24;
                    using (Pen bp = new Pen(Color.FromArgb(40, 46, 66), 4.5f))
                    {
                        bp.StartCap = LineCap.Round;
                        bp.EndCap = LineCap.Round;
                        g.DrawLine(bp, dspStartX, barY, dspStartX + dspW, barY);
                    }
                    int actW = (int)(dspW * (dspVals[i] / 100.0));
                    if (actW > 2)
                    {
                        using (Pen ap = new Pen(eqEnabled ? accentColor : textMuted, 4.5f))
                        {
                            ap.StartCap = LineCap.Round;
                            ap.EndCap = LineCap.Round;
                            g.DrawLine(ap, dspStartX, barY, dspStartX + actW, barY);
                        }
                    }
                    g.FillEllipse(Brushes.White, dspStartX + actW - 5, barY - 5, 10, 10);
                }
            }

            int halfW = (contentW - 14) / 2;
            Rectangle cfCard = new Rectangle(padX, 310, halfW, 188);
            using (SolidBrush cb = new SolidBrush(curBgCard))
            {
                FillRoundRect(g, cb, cfCard, 14);
            }
            using (SolidBrush ab = new SolidBrush(accentColor))
            {
                g.DrawString("DUAL-DECK CROSSFADE & TRANSITIONS", fontSmallBold, ab, cfCard.X + 16, cfCard.Y + 14);
            }

            using (SolidBrush wb = new SolidBrush(textWhite))
            using (SolidBrush subB = new SolidBrush(textSub))
            {
                g.DrawString("Overlap Crossfade (" + (crossfadeEnabled ? (crossfadeSeconds + "s Active") : "Off") + ")", fontSubReg, wb, cfCard.X + 16, cfCard.Y + 44);
                DrawToggleSwitch(g, new Rectangle(cfCard.Right - 58, cfCard.Y + 42, 42, 20), crossfadeEnabled);

                int[] cfOpts = new int[] { 1, 3, 5, 8, 12 };
                for (int i = 0; i < cfOpts.Length; i++)
                {
                    Rectangle cfr = new Rectangle(cfCard.X + 16 + i * 48, cfCard.Y + 70, 42, 24);
                    bool act = crossfadeEnabled && (crossfadeSeconds == cfOpts[i]);
                    using (SolidBrush b = new SolidBrush(act ? accentColor : Color.FromArgb(30, 35, 50)))
                    {
                        FillRoundRect(g, b, cfr, 8);
                    }
                    using (SolidBrush tb = new SolidBrush(act ? Color.Black : textSub))
                    {
                        g.DrawString(cfOpts[i] + "s", fontSubReg, tb, cfr, sfCenter);
                    }
                }

                g.DrawString("Gapless Playback", fontSubReg, wb, cfCard.X + 16, cfCard.Y + 106);
                DrawToggleSwitch(g, new Rectangle(cfCard.Right - 58, cfCard.Y + 104, 42, 20), gaplessPlayback);

                g.DrawString("Skip Silence", fontSubReg, wb, cfCard.X + 16, cfCard.Y + 134);
                DrawToggleSwitch(g, new Rectangle(cfCard.Right - 58, cfCard.Y + 132, 42, 20), skipSilence);

                g.DrawString("Auto-Mix Smart Queue", fontSubReg, wb, cfCard.X + 16, cfCard.Y + 162);
                DrawToggleSwitch(g, new Rectangle(cfCard.Right - 58, cfCard.Y + 160, 42, 20), autoMixEnabled);
            }

            Rectangle uiCard = new Rectangle(padX + halfW + 14, 310, halfW, 188);
            using (SolidBrush cb = new SolidBrush(curBgCard))
            {
                FillRoundRect(g, cb, uiCard, 14);
            }
            using (SolidBrush ab = new SolidBrush(accentColor))
            {
                g.DrawString("ANIMATED ARTWORK, TIMER & CACHE", fontSmallBold, ab, uiCard.X + 16, uiCard.Y + 14);
            }

            using (SolidBrush wb = new SolidBrush(textWhite))
            using (SolidBrush subB = new SolidBrush(textSub))
            {
                for (int i = 0; i < artworkShapeNames.Length; i++)
                {
                    int sw = (uiCard.Width - 44) / 4;
                    Rectangle sr = new Rectangle(uiCard.X + 16 + i * (sw + 4), uiCard.Y + 42, sw, 24);
                    bool act = (artworkShapeMode == i);
                    using (SolidBrush b = new SolidBrush(act ? accentColor : Color.FromArgb(30, 35, 50)))
                    {
                        FillRoundRect(g, b, sr, 8);
                    }
                    using (SolidBrush tb = new SolidBrush(act ? Color.Black : textSub))
                    {
                        g.DrawString(artworkShapeNames[i], fontSmallBold, tb, sr, sfCenter);
                    }
                }

                g.DrawString("Sleep Timer:", fontSmallBold, wb, uiCard.X + 16, uiCard.Y + 78);
                int[] stOpts = new int[] { 0, 15, 30, 60 };
                for (int i = 0; i < stOpts.Length; i++)
                {
                    Rectangle str = new Rectangle(uiCard.X + 102 + i * 44, uiCard.Y + 74, 38, 22);
                    bool act = (sleepTimerMinutes == stOpts[i]);
                    using (SolidBrush b = new SolidBrush(act ? accentColor : Color.FromArgb(30, 35, 50)))
                    {
                        FillRoundRect(g, b, str, 7);
                    }
                    using (SolidBrush tb = new SolidBrush(act ? Color.Black : textSub))
                    {
                        g.DrawString(stOpts[i] == 0 ? "Off" : (stOpts[i] + "m"), fontSmallBold, tb, str, sfCenter);
                    }
                }

                g.DrawString("Wavy Progress Bar", fontSmallBold, wb, uiCard.X + 16, uiCard.Y + 108);
                DrawToggleSwitch(g, new Rectangle(uiCard.Right - 58, uiCard.Y + 106, 42, 20), wavySliderEnabled);

                g.DrawString("OLED Pure Black Mode", fontSmallBold, wb, uiCard.X + 16, uiCard.Y + 136);
                DrawToggleSwitch(g, new Rectangle(uiCard.Right - 58, uiCard.Y + 134, 42, 20), oledPureBlack);

                g.DrawString("Cache: " + GetAudioCacheSizeText(), fontSmallBold, subB, uiCard.X + 16, uiCard.Y + 162);
                Rectangle clrBtn = new Rectangle(uiCard.Right - 86, uiCard.Y + 158, 70, 22);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(36, 41, 58)))
                {
                    FillRoundRect(g, b, clrBtn, 7);
                }
                using (SolidBrush ab = new SolidBrush(accentColor))
                {
                    g.DrawString("Clear", fontSmallBold, ab, clrBtn, sfCenter);
                }
            }

            Rectangle palCard = new Rectangle(padX, 510, contentW, 74);
            using (SolidBrush cb = new SolidBrush(curBgCard))
            {
                FillRoundRect(g, cb, palCard, 14);
            }
            using (SolidBrush ab = new SolidBrush(accentColor))
            {
                g.DrawString("SIGNATURE THEME PALETTES", fontSmallBold, ab, palCard.X + 16, palCard.Y + 10);
            }
            int swX = palCard.X + 16;
            for (int i = 0; i < themes.Count; i++)
            {
                int wChip = (contentW - 32 - 8 * 6) / 9;
                Rectangle tr = new Rectangle(swX + i * (wChip + 6), palCard.Y + 34, wChip, 28);
                bool act = (activeThemeIdx == i);
                using (SolidBrush b = new SolidBrush(act ? Color.FromArgb(45, themes[i].Accent) : Color.FromArgb(28, 32, 46)))
                {
                    FillRoundRect(g, b, tr, 8);
                }
                if (act)
                {
                    using (Pen p = new Pen(themes[i].Accent, 1.5f)) DrawRoundRect(g, p, tr, 8);
                }
                using (SolidBrush db = new SolidBrush(themes[i].Accent))
                {
                    g.FillEllipse(db, tr.X + 6, tr.Y + 8, 12, 12);
                }
                using (SolidBrush tb = new SolidBrush(textWhite))
                {
                    g.DrawString(themes[i].Name.Split(' ')[0], fontSmallBold, tb, tr.X + 21, tr.Y + 6);
                }
            }
        }

        private void PaintRightDockedPanel(Graphics g, int rightX, int rightW, int mainH, Color curBgSide, Color curBgCard, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            using (SolidBrush rpb = new SolidBrush(curBgSide))
            {
                g.FillRectangle(rpb, rightX, 0, rightW, mainH);
            }

            if (currentSong == null) return;

            Rectangle flowerRect = new Rectangle(rightX + (rightW - 168) / 2, 38, 168, 168);
            DrawAnimatedStudioArtwork(g, currentSong.ThumbUrl, flowerRect, artworkShapeMode);

            Rectangle expBtn = ScaleRectCentered(new Rectangle(rightX + 16, 14, 28, 28), GetButtonScale("exp_right"));
            using (SolidBrush eb = new SolidBrush(curBgCard))
            {
                FillRoundRect(g, eb, expBtn, 8);
            }
            DrawExpandIcon(g, accentColor, expBtn.X + 14f, expBtn.Y + 14f);

            using (Font fNpTitle = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (SolidBrush wb = new SolidBrush(textWhite))
            using (SolidBrush sb = new SolidBrush(textSub))
            {
                g.DrawString(currentSong.Title, fNpTitle, wb, new RectangleF(rightX + 14, 214, rightW - 28, 24), sfCenter);
                g.DrawString(currentSong.Artist, fontSubReg, sb, new RectangleF(rightX + 14, 238, rightW - 28, 20), sfCenter);
            }

            int totalVisW = 14 * 8;
            int startVisX = rightX + (rightW - totalVisW) / 2;
            DrawEqResponsiveVisualizer(g, startVisX, 276, 14, 8, 4f, 13f);

            Rectangle tabContainer = new Rectangle(rightX + 16, 286, rightW - 32, 32);
            using (SolidBrush tcb = new SolidBrush(curBgCard))
            {
                FillRoundRect(g, tcb, tabContainer, 10);
            }

            int halfTabW = (tabContainer.Width - 6) / 2;
            Rectangle qTabRect = new Rectangle(tabContainer.X + 3, tabContainer.Y + 3, halfTabW, 26);
            Rectangle lTabRect = new Rectangle(tabContainer.X + 3 + halfTabW, tabContainer.Y + 3, halfTabW, 26);

            using (SolidBrush activeTabBrush = new SolidBrush(accentColor))
            {
                FillRoundRect(g, activeTabBrush, rightPanelMode == 0 ? qTabRect : lTabRect, 8);
            }

            using (SolidBrush qTxtB = new SolidBrush(rightPanelMode == 0 ? Color.Black : textSub))
            using (SolidBrush lTxtB = new SolidBrush(rightPanelMode == 1 ? Color.Black : textSub))
            {
                g.DrawString("Queue (" + queueSongs.Count + ")", fontSmallBold, qTxtB, qTabRect, sfCenter);
                g.DrawString("BetterLyrics", fontSmallBold, lTxtB, lTabRect, sfCenter);
            }

            Rectangle panelBox = new Rectangle(rightX + 16, 324, rightW - 32, mainH - 336);
            if (rightPanelMode == 1)
            {
                // Ambient album-art tinted card for BetterLyrics in Docked Panel!
                Color ambTop = Color.FromArgb(255, (int)Math.Min(255, ambR * 0.7f), (int)Math.Min(255, ambG * 0.7f), (int)Math.Min(255, ambB * 0.7f));
                Color ambBot = Color.FromArgb(255, (int)Math.Min(255, ambR * 0.28f), (int)Math.Min(255, ambG * 0.28f), (int)Math.Min(255, ambB * 0.28f));
                using (LinearGradientBrush lgb = new LinearGradientBrush(panelBox, ambTop, ambBot, LinearGradientMode.Vertical))
                {
                    FillRoundRect(g, lgb, panelBox, 14);
                }
                PaintBetterLyricsCanvas(g, panelBox, false, sfCenter);
            }
            else
            {
                using (SolidBrush lb = new SolidBrush(Color.FromArgb(18, 21, 30)))
                {
                    FillRoundRect(g, lb, panelBox, 14);
                }

                Region prevRightClip = g.Clip;
                g.SetClip(new Rectangle(panelBox.X, panelBox.Y + 6, panelBox.Width, panelBox.Height - 12));

                int qRowH = 46;
                for (int i = 0; i < queueSongs.Count; i++)
                {
                    int qy = panelBox.Y + 8 + i * (qRowH + 4) - queueScrollOffset;
                    if (qy + qRowH < panelBox.Y || qy > panelBox.Bottom) continue;

                    SongItem qs = queueSongs[i];
                    bool isPlayingThis = (currentSong != null && currentSong.Id == qs.Id);
                    bool isHovQ = (hoveredQueueIndex == i);

                    Rectangle qRowRect = new Rectangle(panelBox.X + 8, qy, panelBox.Width - 16, qRowH);
                    if (isPlayingThis || isHovQ)
                    {
                        using (SolidBrush qrb = new SolidBrush(isPlayingThis ? Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B) : bgCardHover))
                        {
                            FillRoundRect(g, qrb, qRowRect, 9);
                        }
                    }

                    DrawStaticAlbumArt(g, qs.ThumbUrl, new Rectangle(qRowRect.X + 6, qRowRect.Y + 6, 34, 34), 8);

                    using (Font fQa = new Font("Segoe UI", 7.8f, FontStyle.Regular))
                    using (SolidBrush qtb = new SolidBrush(isPlayingThis ? accentColor : textWhite))
                    using (SolidBrush qab = new SolidBrush(textSub))
                    {
                        string moodTag = (!string.IsNullOrEmpty(qs.Mood) && qs.Mood != "All") ? ("[" + qs.Mood + "] ") : "";
                        g.DrawString(qs.Title, fontSmallBold, qtb, new RectangleF(qRowRect.X + 46, qRowRect.Y + 5, qRowRect.Width - 54, 19), sfLeftEllipsis);
                        g.DrawString(moodTag + qs.Artist, fQa, qab, new RectangleF(qRowRect.X + 46, qRowRect.Y + 23, qRowRect.Width - 54, 17), sfLeftEllipsis);
                    }
                }
                g.Clip = prevRightClip;
            }
        }

        // =========================================================================
        // UPGRADED FULL-SCREEN BETTERLYRICS & STUDIO PLAYER PANEL
        // Supports 3 Seamless Modes:
        //   Mode 1 (Default): Studio Morphing Artwork + Auto-Synced BetterLyrics Split
        //   Mode 2 (Cinema):  Full-Screen Centered BetterLyrics (1:1 Screenshot Layout)
        //   Mode 0 (Queue):   Studio Morphing Artwork + Smart Similar-Vibe Queue
        // =========================================================================

        private void PaintFullNowPlayingPanel(Graphics g, int w, int h, float animAlpha, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            int alphaByte = (int)Math.Max(0, Math.Min(255, animAlpha * 255));
            Color cTop = Color.FromArgb(alphaByte, (int)Math.Min(255, ambR * 0.95f), (int)Math.Min(255, ambG * 0.90f), (int)Math.Min(255, ambB * 0.95f));
            Color cMid = Color.FromArgb(alphaByte, (int)Math.Min(255, ambR * 1.15f), (int)Math.Min(255, ambG * 1.08f), (int)Math.Min(255, ambB * 1.15f));
            Color cBot = Color.FromArgb(alphaByte, (int)Math.Min(255, ambR * 0.30f), (int)Math.Min(255, ambG * 0.28f), (int)Math.Min(255, ambB * 0.32f));

            using (LinearGradientBrush bgGrad = new LinearGradientBrush(new Rectangle(0, 0, w, h), cTop, cBot, LinearGradientMode.Vertical))
            {
                ColorBlend cb = new ColorBlend(3);
                cb.Colors = new Color[] { cTop, cMid, cBot };
                cb.Positions = new float[] { 0.0f, 0.46f, 1.0f };
                bgGrad.InterpolationColors = cb;
                g.FillRectangle(bgGrad, 0, 0, w, h);
            }

            // Ambient breathing light orbs for studio depth
            int orbOffsetX = (int)(Math.Sin(wavePhase * 0.25f) * 35f);
            int orbOffsetY = (int)(Math.Cos(wavePhase * 0.20f) * 20f);
            using (SolidBrush orbB = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
            {
                g.FillEllipse(orbB, w / 4 - 160 + orbOffsetX, 70 + orbOffsetY, 320, 320);
                g.FillEllipse(orbB, (w * 3) / 4 - 180 - orbOffsetX, 110 - orbOffsetY, 360, 360);
            }

            Color softPillColor = Color.FromArgb(
                Math.Min(255, (int)(ambR * 1.45f + 105)),
                Math.Min(255, (int)(ambG * 1.45f + 95)),
                Math.Min(255, (int)(ambB * 1.45f + 95)));
            Color darkIconColor = Color.FromArgb(44, 28, 22);

            // Top Glass Header Bar
            Rectangle backBtn = ScaleRectCentered(new Rectangle(28, 16, 128, 34), GetButtonScale("full_close"));
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(75, 0, 0, 0)))
            {
                FillRoundRect(g, bb, backBtn, 17);
            }
            g.DrawString("↓  Close Panel", fontSmallBold, Brushes.White, backBtn, sfCenter);

            string modeName = (fullViewSubMode == 1) ? "View: Studio + Lyrics" : ((fullViewSubMode == 2) ? "View: Cinema Lyrics" : "View: Studio + Queue");
            Rectangle modeBtn = ScaleRectCentered(new Rectangle(164, 16, 160, 34), GetButtonScale("full_mode"));
            using (SolidBrush mb = new SolidBrush(Color.FromArgb(75, 0, 0, 0)))
            {
                FillRoundRect(g, mb, modeBtn, 17);
            }
            g.DrawString(modeName, fontSmallBold, Brushes.White, modeBtn, sfCenter);

            Rectangle styleBtn = ScaleRectCentered(new Rectangle(332, 16, 126, 34), GetButtonScale("full_style"));
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(75, 0, 0, 0)))
            {
                FillRoundRect(g, sb, styleBtn, 17);
            }
            g.DrawString("Art: " + artworkShapeNames[artworkShapeMode], fontSmallBold, Brushes.White, styleBtn, sfCenter);

            Rectangle dspPill = new Rectangle(466, 16, 168, 34);
            using (SolidBrush dpb = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            {
                FillRoundRect(g, dpb, dspPill, 17);
            }
            g.DrawString("EQ: " + (eqEnabled ? activeEqPreset : "Off") + " • CF " + (crossfadeEnabled ? (crossfadeSeconds + "s") : "Off"), fontSmallBold, Brushes.White, dspPill, sfCenter);

            Rectangle discBtn = ScaleRectCentered(new Rectangle(642, 16, 108, 34), GetButtonScale("full_discord"));
            using (SolidBrush db = new SolidBrush(Color.FromArgb(190, 88, 101, 242)))
            {
                FillRoundRect(g, db, discBtn, 17);
            }
            g.DrawString("Join Discord", fontSmallBold, Brushes.White, discBtn, sfCenter);

            int bottomDeckTop = h - 246;
            int stageTop = 60;
            int stageH = Math.Max(180, bottomDeckTop - stageTop - 12);

            if (fullViewSubMode == 2)
            {
                // MODE 2: FULL-WIDTH CINEMA BETTERLYRICS STAGE (1:1 layout from user screenshots!)
                int lyrStageW = Math.Min(820, w - 80);
                Rectangle lyrBounds = new Rectangle((w - lyrStageW) / 2, stageTop, lyrStageW, stageH);
                PaintBetterLyricsCanvas(g, lyrBounds, true, sfCenter);
            }
            else
            {
                // MODE 1 (Studio + BetterLyrics Split) OR MODE 0 (Studio + Smart Queue Split)
                int leftW = Math.Min(360, w / 3 + 20);
                Rectangle leftCard = new Rectangle(28, stageTop, leftW, stageH);
                using (SolidBrush lcb = new SolidBrush(Color.FromArgb(52, 0, 0, 0)))
                using (Pen lcp = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                {
                    FillRoundRect(g, lcb, leftCard, 20);
                    DrawRoundRect(g, lcp, leftCard, 20);
                }

                int artSz = Math.Min(196, stageH - 120);
                if (artSz < 110) artSz = 110;
                Rectangle bigArtRect = new Rectangle(leftCard.X + (leftCard.Width - artSz) / 2, leftCard.Y + 18, artSz, artSz);
                DrawAnimatedStudioArtwork(g, currentSong.ThumbUrl, bigArtRect, artworkShapeMode);

                int metaY = bigArtRect.Bottom + 12;
                using (Font fCardTitle = new Font("Georgia", 13.5f, FontStyle.Bold))
                using (SolidBrush subB = new SolidBrush(Color.FromArgb(210, 240, 235, 230)))
                {
                    g.DrawString(currentSong.Title, fCardTitle, Brushes.White, new RectangleF(leftCard.X + 14, metaY, leftCard.Width - 28, 24), sfCenter);
                    g.DrawString(currentSong.Artist + "  •  [" + currentSong.Mood + "]", fontSubReg, subB, new RectangleF(leftCard.X + 14, metaY + 24, leftCard.Width - 28, 20), sfCenter);
                }

                // 18-Bar EQ-Responsive Spectrum Visualizer inside the Studio Card
                int visW = 18 * 9;
                int visStartX = leftCard.X + (leftCard.Width - visW) / 2;
                int visBaseY = Math.Min(leftCard.Bottom - 14, metaY + 76);
                DrawEqResponsiveVisualizer(g, visStartX, visBaseY, 18, 9, 4.5f, 18f);

                // Right Stage: BetterLyrics Auto-Synced Canvas (Mode 1) OR Smart Queue (Mode 0)
                int rightStageX = leftCard.Right + 18;
                int rightStageW = w - rightStageX - 28;
                Rectangle rightStageRect = new Rectangle(rightStageX, stageTop, rightStageW, stageH);

                using (SolidBrush rcb = new SolidBrush(Color.FromArgb(52, 0, 0, 0)))
                using (Pen rcp = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                {
                    FillRoundRect(g, rcb, rightStageRect, 20);
                    DrawRoundRect(g, rcp, rightStageRect, 20);
                }

                if (fullViewSubMode == 1)
                {
                    PaintBetterLyricsCanvas(g, rightStageRect, true, sfCenter);
                }
                else
                {
                    g.DrawString("SMART SIMILAR QUEUE  •  " + queueSongs.Count + " DISTINCT TRACKS (SAME TONE & MOOD)",
                        fontSmallBold, Brushes.White, rightStageRect.X + 20, rightStageRect.Y + 14);

                    Region oldC = g.Clip;
                    g.SetClip(new Rectangle(rightStageRect.X, rightStageRect.Y + 40, rightStageRect.Width, rightStageRect.Height - 48));
                    int qRowH = 46;
                    for (int i = 0; i < queueSongs.Count; i++)
                    {
                        int qy = rightStageRect.Y + 44 + i * (qRowH + 4) - queueScrollOffset;
                        if (qy + qRowH < rightStageRect.Y + 38 || qy > rightStageRect.Bottom) continue;

                        SongItem qs = queueSongs[i];
                        bool isPlay = (currentSong != null && currentSong.Id == qs.Id);
                        Rectangle qr = new Rectangle(rightStageRect.X + 12, qy, rightStageRect.Width - 24, qRowH);
                        if (isPlay)
                        {
                            using (SolidBrush b = new SolidBrush(Color.FromArgb(65, 255, 255, 255)))
                            {
                                FillRoundRect(g, b, qr, 10);
                            }
                        }
                        DrawStaticAlbumArt(g, qs.ThumbUrl, new Rectangle(qr.X + 6, qr.Y + 6, 34, 34), 8);
                        g.DrawString(qs.Title, fontSmallBold, Brushes.White, new RectangleF(qr.X + 46, qr.Y + 5, qr.Width - 110, 18), sfLeftEllipsis);
                        g.DrawString("[" + qs.Mood + "] " + qs.Artist, fontSubReg, Brushes.LightGray, new RectangleF(qr.X + 46, qr.Y + 23, qr.Width - 110, 16), sfLeftEllipsis);
                        g.DrawString(FormatSec(qs.Duration), fontSmallBold, Brushes.LightGray, qr.Right - 46, qr.Y + 14);
                    }
                    g.Clip = oldC;
                }
            }

            // =========================================================================
            // BOTTOM BETTERLYRICS TRANSPORT CONSOLE DECK (Matching user screenshots!)
            // =========================================================================
            int deckW = Math.Min(740, w - 56);
            int deckX = (w - deckW) / 2;
            Rectangle deckConsoleCard = new Rectangle(deckX - 18, bottomDeckTop - 8, deckW + 36, 246);
            using (SolidBrush dcb = new SolidBrush(Color.FromArgb(48, 0, 0, 0)))
            using (Pen dcp = new Pen(Color.FromArgb(35, 255, 255, 255), 1f))
            {
                FillRoundRect(g, dcb, deckConsoleCard, 22);
                DrawRoundRect(g, dcp, deckConsoleCard, 22);
            }

            // Row 1: Album Art Thumbnail + Bold Song Title + Artist + Heart + [⛶] Cinema/Split Pill + [•••] EQ Pill
            Rectangle artThumbRect = new Rectangle(deckX, bottomDeckTop, 56, 56);
            DrawStaticAlbumArt(g, currentSong.ThumbUrl, artThumbRect, 10);

            using (Font fSongTitle = new Font("Georgia", 15f, FontStyle.Bold))
            using (Font fSongArtist = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (SolidBrush artSubB = new SolidBrush(Color.FromArgb(225, 245, 240, 238)))
            {
                g.DrawString(currentSong.Title, fSongTitle, Brushes.White, new RectangleF(deckX + 68, bottomDeckTop + 3, deckW - 220, 28), sfLeftEllipsis);
                g.DrawString(currentSong.Artist, fSongArtist, artSubB, new RectangleF(deckX + 68, bottomDeckTop + 31, deckW - 220, 22), sfLeftEllipsis);
            }

            bool isCurLiked = likedIds.Contains(currentSong.Id);
            DrawHeartIcon(g, isCurLiked ? softPillColor : Color.FromArgb(180, 255, 255, 255), deckX + deckW - 126, bottomDeckTop + 28, isCurLiked);

            Rectangle sqExpandBtn = ScaleRectCentered(new Rectangle(deckX + deckW - 100, bottomDeckTop + 8, 44, 40), GetButtonScale("deck_split"));
            Rectangle sqDotsBtn = ScaleRectCentered(new Rectangle(deckX + deckW - 48, bottomDeckTop + 8, 44, 40), GetButtonScale("deck_eq"));
            using (SolidBrush pillB = new SolidBrush(softPillColor))
            using (SolidBrush iconB = new SolidBrush(darkIconColor))
            {
                FillRoundRect(g, pillB, sqExpandBtn, 14);
                FillRoundRect(g, pillB, sqDotsBtn, 14);
                g.DrawString("⛶", fontTitleBold, iconB, sqExpandBtn, sfCenter);
                g.DrawString("•••", fontTitleBold, iconB, sqDotsBtn, sfCenter);
            }

            // Row 2: Smooth Sub-Frame Wavy Progress Slider + Glowing Thumb + Time Labels
            int sliderY = bottomDeckTop + 74;
            float frac = (float)Math.Max(0.0, Math.Min(1.0, smoothPosSec / Math.Max(1.0, currentDurSec)));
            int activeX = deckX + (int)(deckW * frac);

            using (Pen trackPen = new Pen(Color.FromArgb(95, 255, 255, 255), 3.5f))
            {
                trackPen.StartCap = LineCap.Round;
                trackPen.EndCap = LineCap.Round;
                g.DrawLine(trackPen, activeX, sliderY, deckX + deckW, sliderY);
            }

            if (activeX > deckX + 3)
            {
                List<PointF> wPts = new List<PointF>();
                for (int x = deckX; x <= activeX; x += 3)
                {
                    float amp = (wavySliderEnabled && isPlaying) ? 3.2f : 0f;
                    float y = sliderY + amp * (float)Math.Sin(0.08f * (x - deckX) - wavePhase);
                    wPts.Add(new PointF(x, y));
                }
                if (wPts.Count > 1)
                {
                    using (Pen actPen = new Pen(softPillColor, 4.0f))
                    {
                        actPen.StartCap = LineCap.Round;
                        actPen.EndCap = LineCap.Round;
                        g.DrawLines(actPen, wPts.ToArray());
                    }
                }
            }
            using (SolidBrush thumbB = new SolidBrush(softPillColor))
            {
                g.FillEllipse(thumbB, activeX - 8, sliderY - 8, 16, 16);
            }

            using (SolidBrush timeB = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
            {
                g.DrawString(FormatSec((int)currentPosSec), fontSmallBold, timeB, deckX, sliderY + 11);
                g.DrawString(FormatSec((int)currentDurSec), fontSmallBold, timeB, deckX + deckW - 34, sliderY + 11);
            }

            // Row 3: Large Circular Previous (|<) + Wide Rounded Pill Pause/Play (|| Pause) + Large Circular Next (>|)
            int transY = bottomDeckTop + 108;
            int centerDeckX = w / 2;

            Rectangle bigPrevBtn = ScaleRectCentered(new Rectangle(centerDeckX - 210, transY, 72, 62), GetButtonScale("prev"));
            Rectangle bigPlayPill = ScaleRectCentered(new Rectangle(centerDeckX - 122, transY, 244, 62), GetButtonScale("play"));
            Rectangle bigNextBtn = ScaleRectCentered(new Rectangle(centerDeckX + 138, transY, 72, 62), GetButtonScale("next"));

            using (SolidBrush pillB = new SolidBrush(softPillColor))
            using (SolidBrush darkB = new SolidBrush(darkIconColor))
            using (Font fPauseLabel = new Font("Georgia", 13f, FontStyle.Bold))
            {
                FillRoundRect(g, pillB, bigPrevBtn, 31);
                FillRoundRect(g, pillB, bigPlayPill, 31);
                FillRoundRect(g, pillB, bigNextBtn, 31);

                DrawSkipPrevIcon(g, darkB, bigPrevBtn.X + bigPrevBtn.Width / 2f, bigPrevBtn.Y + bigPrevBtn.Height / 2f);
                DrawSkipNextIcon(g, darkB, bigNextBtn.X + bigNextBtn.Width / 2f, bigNextBtn.Y + bigNextBtn.Height / 2f);

                float pCx = bigPlayPill.X + bigPlayPill.Width / 2f - 28f;
                float pCy = bigPlayPill.Y + bigPlayPill.Height / 2f;
                if (isPlaying)
                {
                    DrawPauseBars(g, darkB, pCx, pCy, 4.5f, 16f, 5f);
                    g.DrawString("Pause", fPauseLabel, darkB, pCx + 16f, pCy - 11f);
                }
                else
                {
                    DrawPlayTriangle(g, darkB, pCx, pCy, 11f);
                    g.DrawString("Play", fPauseLabel, darkB, pCx + 16f, pCy - 11f);
                }
            }

            // Row 4: Bottom 5 Action Pills (Queue, Sleep Timer Moon, Shuffle, BetterLyrics Toggle, Repeat) + Full-Panel Volume Bar + Download Circle
            int botRowY = bottomDeckTop + 186;
            string[] botIcons = new string[] { "☰♪", "☾", "⇄", "≡", "↻" };
            bool[] botActive = new bool[] { fullViewSubMode == 0, sleepTimerMinutes > 0, isShuffle, fullViewSubMode >= 1, isRepeatOne };
            int startBotX = deckX;

            for (int i = 0; i < 5; i++)
            {
                Rectangle bRect = ScaleRectCentered(new Rectangle(startBotX + i * 54, botRowY, 46, 36), GetButtonScale("bot_act_" + i));
                if (botActive[i])
                {
                    using (SolidBrush ab = new SolidBrush(softPillColor))
                    {
                        FillRoundRect(g, ab, bRect, 10);
                    }
                }
                else
                {
                    using (Pen op = new Pen(Color.FromArgb(90, 255, 255, 255), 1.2f))
                    {
                        DrawRoundRect(g, op, bRect, 10);
                    }
                }
                using (SolidBrush tb = new SolidBrush(botActive[i] ? darkIconColor : Color.White))
                {
                    g.DrawString(botIcons[i], fontTitleBold, tb, bRect, sfCenter);
                }
            }

            // Interactive Volume Slider right inside Full Panel!
            int fpVolX = deckX + deckW - 155;
            int fpVolW = 88;
            int fpVolY = botRowY + 18;
            using (Pen vp = new Pen(Color.FromArgb(80, 255, 255, 255), 3.5f))
            {
                vp.StartCap = LineCap.Round;
                vp.EndCap = LineCap.Round;
                g.DrawLine(vp, fpVolX, fpVolY, fpVolX + fpVolW, fpVolY);
            }
            int fpActVolW = isMuted ? 0 : (int)(fpVolW * (volumePercent / 100.0));
            if (fpActVolW > 2)
            {
                using (Pen avp = new Pen(softPillColor, 3.5f))
                {
                    avp.StartCap = LineCap.Round;
                    avp.EndCap = LineCap.Round;
                    g.DrawLine(avp, fpVolX, fpVolY, fpVolX + fpActVolW, fpVolY);
                }
            }
            using (SolidBrush vtb = new SolidBrush(softPillColor))
            {
                g.FillEllipse(vtb, fpVolX + fpActVolW - 5, fpVolY - 5, 10, 10);
            }

            Rectangle botMenuCircle = ScaleRectCentered(new Rectangle(deckX + deckW - 44, botRowY, 42, 36), GetButtonScale("download"));
            using (SolidBrush ab = new SolidBrush(softPillColor))
            {
                FillRoundRect(g, ab, botMenuCircle, 18);
            }
            DrawDownloadArrow(g, darkIconColor, botMenuCircle.X + 21f, botMenuCircle.Y + 18f);
        }

        private void PaintBottomPlayerBar(Graphics g, int w, int h, int playerH, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            int pbY = h - playerH;
            using (SolidBrush pbb = new SolidBrush(bgPlayer))
            using (Pen pbp = new Pen(Color.FromArgb(28, 32, 46), 1f))
            {
                g.FillRectangle(pbb, 0, pbY, w, playerH);
                g.DrawLine(pbp, 0, pbY, w, pbY);
            }

            if (currentSong != null)
            {
                DrawStaticAlbumArt(g, currentSong.ThumbUrl, new Rectangle(18, pbY + 19, 52, 52), 10);
                using (SolidBrush wb = new SolidBrush(textWhite))
                using (SolidBrush sb = new SolidBrush(textSub))
                {
                    g.DrawString(currentSong.Title, fontTitleBold, wb, new RectangleF(80, pbY + 21, 185, 22), sfLeftEllipsis);
                    g.DrawString(currentSong.Artist, fontSubReg, sb, new RectangleF(80, pbY + 43, 185, 20), sfLeftEllipsis);
                }

                bool isLiked = likedIds.Contains(currentSong.Id);
                DrawHeartIcon(g, isLiked ? accentColor : textSub, 278, pbY + 45, isLiked);

                Rectangle expBottomBtn = ScaleRectCentered(new Rectangle(298, pbY + 31, 28, 28), GetButtonScale("exp_bot"));
                using (SolidBrush eb = new SolidBrush(Color.FromArgb(24, 28, 40)))
                {
                    FillRoundRect(g, eb, expBottomBtn, 8);
                }
                DrawExpandIcon(g, isFullPlayerExpanded ? accentColor : textSub, expBottomBtn.X + 14f, expBottomBtn.Y + 14f);
            }

            int midX = w / 2;
            float btnCy = pbY + 30f;

            Rectangle shufBtn = new Rectangle(midX - 114, (int)btnCy - 12, 28, 24);
            using (SolidBrush sb = new SolidBrush(isShuffle ? accentColor : textMuted))
            {
                g.DrawString("SHF", fontSmallBold, sb, shufBtn, sfCenter);
            }

            Rectangle prevCircle = ScaleRectCentered(new Rectangle(midX - 70, (int)btnCy - 16, 32, 32), GetButtonScale("prev"));
            using (SolidBrush pb = new SolidBrush(Color.FromArgb(22, 26, 38)))
            {
                g.FillEllipse(pb, prevCircle);
            }
            DrawSkipPrevIcon(g, Brushes.White, midX - 54f, btnCy);

            Rectangle playCircle = ScaleRectCentered(new Rectangle(midX - 21, (int)btnCy - 21, 42, 42), GetButtonScale("play"));
            using (SolidBrush ab = new SolidBrush(accentColor))
            {
                g.FillEllipse(ab, playCircle);
            }
            if (isPlaying)
            {
                DrawPauseBars(g, Brushes.Black, midX, btnCy, 4.2f, 14f, 4.5f);
            }
            else
            {
                DrawPlayTriangle(g, Brushes.Black, midX, btnCy, 11f);
            }

            Rectangle nextCircle = ScaleRectCentered(new Rectangle(midX + 38, (int)btnCy - 16, 32, 32), GetButtonScale("next"));
            using (SolidBrush nb = new SolidBrush(Color.FromArgb(22, 26, 38)))
            {
                g.FillEllipse(nb, nextCircle);
            }
            DrawSkipNextIcon(g, Brushes.White, midX + 54f, btnCy);

            Rectangle repBtn = new Rectangle(midX + 86, (int)btnCy - 12, 28, 24);
            using (SolidBrush rb = new SolidBrush(isRepeatOne ? accentColor : textMuted))
            {
                g.DrawString("RPT", fontSmallBold, rb, repBtn, sfCenter);
            }

            int barW = 420;
            int barX = midX - barW / 2;
            int barY = pbY + 68;
            float frac = (float)Math.Max(0.0, Math.Min(1.0, currentPosSec / Math.Max(1.0, currentDurSec)));
            int activeX = barX + (int)(barW * frac);

            using (Pen inactivePen = new Pen(Color.FromArgb(42, 47, 65), 3.5f))
            {
                inactivePen.StartCap = LineCap.Round;
                inactivePen.EndCap = LineCap.Round;
                g.DrawLine(inactivePen, activeX, barY, barX + barW, barY);
            }

            if (activeX > barX + 4)
            {
                if (wavySliderEnabled)
                {
                    List<PointF> wavePts = new List<PointF>();
                    for (int x = barX; x <= activeX; x += 3)
                    {
                        float amp = isPlaying ? 3.2f : 1.0f;
                        float y = barY + amp * (float)Math.Sin(0.06f * (x - barX) - wavePhase);
                        wavePts.Add(new PointF(x, y));
                    }
                    if (wavePts.Count > 1)
                    {
                        using (Pen activePen = new Pen(accentColor, 3.5f))
                        {
                            activePen.StartCap = LineCap.Round;
                            activePen.EndCap = LineCap.Round;
                            g.DrawLines(activePen, wavePts.ToArray());
                        }
                    }
                }
                else
                {
                    using (Pen activePen = new Pen(accentColor, 3.5f))
                    {
                        activePen.StartCap = LineCap.Round;
                        activePen.EndCap = LineCap.Round;
                        g.DrawLine(activePen, barX, barY, activeX, barY);
                    }
                }
            }
            g.FillEllipse(Brushes.White, activeX - 5, barY - 5, 10, 10);

            using (SolidBrush tb = new SolidBrush(textMuted))
            {
                g.DrawString(FormatSec((int)currentPosSec), fontSmallBold, tb, barX - 38, barY - 7);
                g.DrawString(FormatSec((int)currentDurSec), fontSmallBold, tb, barX + barW + 8, barY - 7);
            }

            int volBarX = w - 336;
            int volBarW = 72;
            int volBarY = pbY + 45;
            using (Pen vp = new Pen(Color.FromArgb(40, 46, 66), 3.5f))
            {
                vp.StartCap = LineCap.Round;
                vp.EndCap = LineCap.Round;
                g.DrawLine(vp, volBarX, volBarY, volBarX + volBarW, volBarY);
            }
            int actVolW = isMuted ? 0 : (int)(volBarW * (volumePercent / 100.0));
            if (actVolW > 2)
            {
                using (Pen avp = new Pen(accentColor, 3.5f))
                {
                    avp.StartCap = LineCap.Round;
                    avp.EndCap = LineCap.Round;
                    g.DrawLine(avp, volBarX, volBarY, volBarX + actVolW, volBarY);
                }
            }
            g.FillEllipse(Brushes.White, volBarX + actVolW - 4, volBarY - 4, 8, 8);

            Rectangle eqQuickBtn = ScaleRectCentered(new Rectangle(w - 250, pbY + 28, 42, 34), GetButtonScale("eq_bot"));
            using (SolidBrush eqb = new SolidBrush(activeTab == 4 ? accentColor : bgCard))
            {
                FillRoundRect(g, eqb, eqQuickBtn, 14);
            }
            using (SolidBrush eqt = new SolidBrush(activeTab == 4 ? Color.Black : accentColor))
            {
                g.DrawString("EQ", fontSmallBold, eqt, eqQuickBtn, sfCenter);
            }

            Rectangle dlMainBtn = ScaleRectCentered(new Rectangle(w - 200, pbY + 28, 102, 34), GetButtonScale("download"));
            using (SolidBrush dlb = new SolidBrush(accentColor))
            {
                FillRoundRect(g, dlb, dlMainBtn, 17);
            }
            string dlLabel = !string.IsNullOrEmpty(downloadingSongId) ? "Saving..." : "Download";
            g.DrawString(dlLabel, fontSmallBold, Brushes.Black, dlMainBtn, sfCenter);

            Rectangle openDirBtn = ScaleRectCentered(new Rectangle(w - 90, pbY + 28, 70, 34), GetButtonScale("folder_bot"));
            using (SolidBrush odb = new SolidBrush(bgCard))
            {
                FillRoundRect(g, odb, openDirBtn, 17);
            }
            using (SolidBrush odt = new SolidBrush(textSub))
            {
                g.DrawString("Folder", fontSmallBold, odt, openDirBtn, sfCenter);
            }
        }

        private void DrawStaticAlbumArt(Graphics g, string url, Rectangle rect, int radius)
        {
            string jpgUrl = (url ?? "").Replace(".webp", ".jpg");
            Image img = null;
            lock (imageCache)
            {
                if (imageCache.ContainsKey(jpgUrl)) img = imageCache[jpgUrl];
            }

            using (GraphicsPath clipPath = GetRoundRectPath(rect, radius))
            {
                Region prevClip = g.Clip;
                g.SetClip(clipPath, CombineMode.Intersect);
                if (img != null)
                {
                    g.DrawImage(img, rect);
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 34, 48)))
                    {
                        g.FillRectangle(b, rect);
                    }
                }
                g.Clip = prevClip;
            }
        }

        private void DrawAnimatedStudioArtwork(Graphics g, string url, Rectangle rect, int shapeMode)
        {
            string jpgUrl = (url ?? "").Replace(".webp", ".jpg");
            Image img = null;
            lock (imageCache)
            {
                if (imageCache.ContainsKey(jpgUrl)) img = imageCache[jpgUrl];
            }

            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;
            float halfW = rect.Width / 2f;
            double rotRad = (vinylAngle * Math.PI) / 180.0;

            GraphicsPath clipPath = new GraphicsPath();
            int numPts = 180;
            PointF[] pts = new PointF[numPts + 1];

            for (int i = 0; i <= numPts; i++)
            {
                double theta = (i / (double)numPts) * Math.PI * 2.0;
                float r = halfW * 0.90f;

                if (shapeMode == 0)
                {
                    float w8 = (float)(0.5 + 0.5 * Math.Sin(morphPhase));
                    float w4 = (float)(0.5 + 0.5 * Math.Cos(morphPhase * 0.72));
                    float w6 = (float)(0.5 + 0.5 * Math.Sin(morphPhase * 0.45 + 1.2));

                    float baseR = halfW * 0.85f;
                    float mod8 = (float)Math.Cos(8.0 * (theta - rotRad)) * (baseR * 0.11f * w8);
                    float mod4 = (float)Math.Cos(4.0 * (theta + rotRad * 0.6)) * (baseR * 0.08f * w4);
                    float mod6 = (float)Math.Sin(6.0 * (theta - rotRad * 0.8)) * (baseR * 0.06f * w6);
                    r = baseR + mod8 + mod4 + mod6;
                }
                else if (shapeMode == 1)
                {
                    float baseR = halfW * 0.87f;
                    float breathe = 0.115f + 0.025f * (float)Math.Sin(morphPhase * 1.4f);
                    r = baseR + baseR * breathe * (float)Math.Cos(8.0 * (theta - rotRad));
                }
                else if (shapeMode == 2)
                {
                    r = halfW * 0.94f;
                }
                else
                {
                    float baseR = halfW * 0.89f;
                    r = baseR + baseR * 0.065f * (float)Math.Cos(4.0 * (theta - rotRad * 0.7));
                }

                pts[i] = new PointF(cx + r * (float)Math.Cos(theta), cy + r * (float)Math.Sin(theta));
            }
            clipPath.AddPolygon(pts);

            Region prevClip = g.Clip;
            g.SetClip(clipPath, CombineMode.Intersect);

            if (img != null)
            {
                GraphicsState state = g.Save();
                float imgRot = (shapeMode == 2) ? vinylAngle : (vinylAngle * 0.35f);
                g.TranslateTransform(cx, cy);
                g.RotateTransform(imgRot);
                g.TranslateTransform(-cx, -cy);

                int pad = (int)(rect.Width * 0.16f);
                Rectangle rotRect = new Rectangle(rect.X - pad, rect.Y - pad, rect.Width + pad * 2, rect.Height + pad * 2);
                g.DrawImage(img, rotRect);
                g.Restore(state);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 34, 48)))
                {
                    g.FillRectangle(b, rect);
                }
            }

            if (shapeMode == 2)
            {
                using (Pen groovePen = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
                {
                    int g1 = (int)(rect.Width * 0.72f);
                    int g2 = (int)(rect.Width * 0.52f);
                    g.DrawEllipse(groovePen, cx - g1 / 2f, cy - g1 / 2f, g1, g1);
                    g.DrawEllipse(groovePen, cx - g2 / 2f, cy - g2 / 2f, g2, g2);
                }
                int holeSz = rect.Width / 5;
                RectangleF holeR = new RectangleF(cx - holeSz / 2f, cy - holeSz / 2f, holeSz, holeSz);
                using (SolidBrush hb = new SolidBrush(bgMain))
                using (Pen hp = new Pen(accentColor, 2.2f))
                {
                    g.FillEllipse(hb, holeR);
                    g.DrawEllipse(hp, holeR);
                }
            }

            g.Clip = prevClip;

            using (Pen glowPen = new Pen(Color.FromArgb(145, accentColor), 2.0f))
            {
                g.DrawPath(glowPen, clipPath);
            }
            clipPath.Dispose();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            int sidebarW = 195;
            int rightW = 315;
            int playerH = 90;
            int mainH = h - playerH;
            int pbY = h - playerH;
            int centerX = sidebarW;
            int centerW = w - sidebarW - rightW;
            int rightX = w - rightW;

            // 0. If In-App Update Popup Modal is Open, handle modal button clicks first!
            if (isUpdatePopupOpen)
            {
                int mw = 480, mh = 330;
                int mx = (w - mw) / 2;
                int my = (h - mh) / 2;

                if (isPasswordPromptActive)
                {
                    Rectangle unlockConfirmBtn = new Rectangle(mx + 242, my + 224, 88, 26);
                    if (unlockConfirmBtn.Contains(e.Location))
                    {
                        VerifyAdminUpdatePassword();
                        return;
                    }
                }

                if (updateInstallBtnRect.Contains(e.Location))
                {
                    PerformOneClickSelfUpdate();
                    return;
                }
                if (updateEditUrlBtnRect.Contains(e.Location))
                {
                    if (isAdminUpdateUnlocked)
                    {
                        OpenProtectedUpdateFilesForAdmin();
                    }
                    else
                    {
                        ShowAdminPasswordBox();
                    }
                    return;
                }
                if (updateLaterBtnRect.Contains(e.Location))
                {
                    HideAdminPasswordBox();
                    isUpdatePopupOpen = false;
                    this.Invalidate();
                    return;
                }

                Rectangle modalRect = new Rectangle(mx, my, mw, mh);
                if (!modalRect.Contains(e.Location))
                {
                    HideAdminPasswordBox();
                    isUpdatePopupOpen = false;
                    this.Invalidate();
                }
                return;
            }

            if (e.Y < 42 && e.X >= w - 108)
            {
                if (e.X >= w - 42) { SavePersistentSettings(); Application.Exit(); return; }
                if (e.X >= w - 72 && e.X < w - 42)
                {
                    this.WindowState = (this.WindowState == FormWindowState.Maximized) ? FormWindowState.Normal : FormWindowState.Maximized;
                    return;
                }
                if (e.X >= w - 108 && e.X < w - 72) { this.WindowState = FormWindowState.Minimized; return; }
            }

            // 1. If Full-Screen BetterLyrics / Studio View is Open:
            if (isFullPlayerExpanded)
            {
                Rectangle backBtn = new Rectangle(28, 16, 128, 34);
                if (backBtn.Contains(e.Location))
                {
                    TriggerButtonBounce("full_close");
                    isFullPlayerExpanded = false;
                    this.Invalidate();
                    return;
                }

                Rectangle modeBtn = new Rectangle(164, 16, 160, 34);
                if (modeBtn.Contains(e.Location))
                {
                    TriggerButtonBounce("full_mode");
                    // Cycle: 1 (Studio + BetterLyrics) -> 2 (Cinema Full BetterLyrics) -> 0 (Studio + Queue)
                    if (fullViewSubMode == 1) fullViewSubMode = 2;
                    else if (fullViewSubMode == 2) fullViewSubMode = 0;
                    else fullViewSubMode = 1;
                    SavePersistentSettings();
                    string mToast = (fullViewSubMode == 1) ? "Studio + BetterLyrics Split View" : ((fullViewSubMode == 2) ? "Cinema Full-Screen BetterLyrics" : "Studio + Smart Queue View");
                    ShowToast(mToast);
                    return;
                }

                Rectangle styleBtn = new Rectangle(332, 16, 126, 34);
                if (styleBtn.Contains(e.Location))
                {
                    TriggerButtonBounce("full_style");
                    artworkShapeMode = (artworkShapeMode + 1) % artworkShapeNames.Length;
                    SavePersistentSettings();
                    ShowToast("Art Style: " + artworkShapeNames[artworkShapeMode]);
                    return;
                }

                Rectangle dspPill = new Rectangle(466, 16, 168, 34);
                if (dspPill.Contains(e.Location))
                {
                    isFullPlayerExpanded = false;
                    activeTab = 4;
                    tabFadeAnim = 0.2f;
                    this.Invalidate();
                    return;
                }

                Rectangle discBtn = new Rectangle(642, 16, 108, 34);
                if (discBtn.Contains(e.Location))
                {
                    OpenDiscordLink();
                    return;
                }

                // Sync Fine-Tune Buttons (-0.5s | Sync | +0.5s) in BetterLyrics Header
                if (!syncMinusBtnRect.IsEmpty && syncMinusBtnRect.Contains(e.Location))
                {
                    lyricSyncOffsetSec -= 0.5;
                    ShowToast("Lyrics Sync Offset: " + lyricSyncOffsetSec.ToString("+0.0;-0.0;0.0") + "s");
                    return;
                }
                if (!syncResetBtnRect.IsEmpty && syncResetBtnRect.Contains(e.Location))
                {
                    lyricSyncOffsetSec = 0.0;
                    if (currentSong != null) AutoCalibrateLyricsToDuration(currentSong, currentDurSec);
                    ShowToast("Lyrics Auto-Synced to Track Clock");
                    return;
                }
                if (!syncPlusBtnRect.IsEmpty && syncPlusBtnRect.Contains(e.Location))
                {
                    lyricSyncOffsetSec += 0.5;
                    ShowToast("Lyrics Sync Offset: " + lyricSyncOffsetSec.ToString("+0.0;-0.0;0.0") + "s");
                    return;
                }

                int bottomDeckTop = h - 246;
                int deckW = Math.Min(740, w - 56);
                int deckX = (w - deckW) / 2;
                int centerDeckX = w / 2;

                // Heart icon in Full Panel transport deck
                Rectangle likeFullRect = new Rectangle(deckX + deckW - 142, bottomDeckTop + 10, 34, 36);
                if (likeFullRect.Contains(e.Location) && currentSong != null)
                {
                    if (likedIds.Contains(currentSong.Id)) likedIds.Remove(currentSong.Id);
                    else likedIds.Add(currentSong.Id);
                    SavePersistentSettings();
                    ShowToast(likedIds.Contains(currentSong.Id) ? "Added to Liked Songs" : "Removed from Liked");
                    return;
                }

                // [⛶] Cinema Lyrics / Studio Split toggle button
                Rectangle sqExpandBtn = new Rectangle(deckX + deckW - 100, bottomDeckTop + 8, 44, 40);
                if (sqExpandBtn.Contains(e.Location))
                {
                    TriggerButtonBounce("deck_split");
                    fullViewSubMode = (fullViewSubMode == 2) ? 1 : 2;
                    SavePersistentSettings();
                    ShowToast(fullViewSubMode == 2 ? "Cinema Full-Screen BetterLyrics" : "Studio + BetterLyrics Split View");
                    return;
                }

                // [•••] Equalizer & Studio Settings button
                Rectangle sqDotsBtn = new Rectangle(deckX + deckW - 48, bottomDeckTop + 8, 44, 40);
                if (sqDotsBtn.Contains(e.Location))
                {
                    TriggerButtonBounce("deck_eq");
                    isFullPlayerExpanded = false;
                    activeTab = 4;
                    tabFadeAnim = 0.2f;
                    this.Invalidate();
                    return;
                }

                // Row 2: Wavy Slider Seek in Full BetterLyrics View
                int sliderY = bottomDeckTop + 74;
                Rectangle fullSeekRect = new Rectangle(deckX - 6, sliderY - 14, deckW + 12, 28);
                if (fullSeekRect.Contains(e.Location))
                {
                    double frac = Math.Max(0.0, Math.Min(1.0, (double)(e.X - deckX) / deckW));
                    SeekToAbsolute(frac * currentDurSec);
                    return;
                }

                // Row 3: Big Prev, Big Pause/Play Pill, Big Next
                int transY = bottomDeckTop + 108;
                Rectangle bigPrevBtn = new Rectangle(centerDeckX - 210, transY, 72, 62);
                if (bigPrevBtn.Contains(e.Location)) { PrevTrack(); return; }

                Rectangle bigPlayPill = new Rectangle(centerDeckX - 122, transY, 244, 62);
                if (bigPlayPill.Contains(e.Location)) { TogglePlayPause(); return; }

                Rectangle bigNextBtn = new Rectangle(centerDeckX + 138, transY, 72, 62);
                if (bigNextBtn.Contains(e.Location)) { NextTrack(); return; }

                // Row 4: Bottom 5 Action Pills + Full-Panel Volume Bar + Download
                int botRowY = bottomDeckTop + 186;
                for (int i = 0; i < 5; i++)
                {
                    Rectangle bRect = new Rectangle(deckX + i * 54, botRowY, 46, 36);
                    if (bRect.Contains(e.Location))
                    {
                        TriggerButtonBounce("bot_act_" + i);
                        if (i == 0)
                        {
                            fullViewSubMode = (fullViewSubMode == 0) ? 1 : 0;
                            SavePersistentSettings();
                            ShowToast(fullViewSubMode == 0 ? "Showing Studio + Smart Queue" : "Showing Studio + BetterLyrics");
                        }
                        else if (i == 1)
                        {
                            int[] stOpts = new int[] { 0, 15, 30, 60 };
                            int nextSt = 15;
                            for (int k = 0; k < stOpts.Length; k++)
                            {
                                if (sleepTimerMinutes == stOpts[k]) { nextSt = stOpts[(k + 1) % stOpts.Length]; break; }
                            }
                            sleepTimerMinutes = nextSt;
                            sleepTimerSecondsLeft = sleepTimerMinutes * 60;
                            SavePersistentSettings();
                            ShowToast(sleepTimerMinutes == 0 ? "Sleep Timer Off" : ("Sleep Timer: " + sleepTimerMinutes + "m"));
                        }
                        else if (i == 2)
                        {
                            isShuffle = !isShuffle;
                            SavePersistentSettings();
                            ShowToast(isShuffle ? "Shuffle On" : "Shuffle Off");
                        }
                        else if (i == 3)
                        {
                            fullViewSubMode = (fullViewSubMode == 1) ? 2 : 1;
                            SavePersistentSettings();
                            ShowToast(fullViewSubMode == 2 ? "Cinema Full-Screen BetterLyrics" : "Studio + BetterLyrics Split View");
                        }
                        else if (i == 4)
                        {
                            isRepeatOne = !isRepeatOne;
                            SavePersistentSettings();
                            ShowToast(isRepeatOne ? "Repeat One On" : "Repeat Off");
                        }
                        this.Invalidate();
                        return;
                    }
                }

                // Volume Slider inside Full Panel
                int fpVolX = deckX + deckW - 155;
                int fpVolW = 88;
                Rectangle fpVolRect = new Rectangle(fpVolX - 6, botRowY, fpVolW + 12, 36);
                if (fpVolRect.Contains(e.Location))
                {
                    int newV = (int)(((double)(e.X - fpVolX) / fpVolW) * 100.0);
                    SetVolume(newV);
                    return;
                }

                Rectangle botMenuCircle = new Rectangle(deckX + deckW - 44, botRowY, 42, 36);
                if (botMenuCircle.Contains(e.Location))
                {
                    DownloadSongDirectly(currentSong);
                    return;
                }

                // Clicking the rotating Studio Artwork on the left of Full Panel cycles Art Style!
                if (fullViewSubMode != 2)
                {
                    int leftW = Math.Min(360, w / 3 + 20);
                    Rectangle leftCard = new Rectangle(28, 60, leftW, bottomDeckTop - 72);
                    if (leftCard.Contains(e.Location))
                    {
                        artworkShapeMode = (artworkShapeMode + 1) % artworkShapeNames.Length;
                        SavePersistentSettings();
                        ShowToast("Art Style: " + artworkShapeNames[artworkShapeMode]);
                        return;
                    }
                }

                // Clicking any lyric line in Full Panel seeks directly to that line!
                if (fullViewSubMode >= 1)
                {
                    for (int i = 0; i < activeLyricHitRects.Count; i++)
                    {
                        if (activeLyricHitRects[i].Contains(e.Location))
                        {
                            SeekToAbsolute(activeLyricHitTimes[i]);
                            ShowToast("Synced to lyric (" + FormatSec((int)activeLyricHitTimes[i]) + ")");
                            return;
                        }
                    }
                }
                else
                {
                    int leftW = Math.Min(360, w / 3 + 20);
                    int rightStageX = 28 + leftW + 18;
                    int rightStageW = w - rightStageX - 28;
                    Rectangle rightStageRect = new Rectangle(rightStageX, 60, rightStageW, bottomDeckTop - 72);
                    if (rightStageRect.Contains(e.Location))
                    {
                        int qRowH = 46;
                        for (int i = 0; i < queueSongs.Count; i++)
                        {
                            int qy = rightStageRect.Y + 44 + i * (qRowH + 4) - queueScrollOffset;
                            Rectangle qr = new Rectangle(rightStageRect.X + 12, qy, rightStageRect.Width - 24, qRowH);
                            if (qr.Contains(e.Location))
                            {
                                currentQueueIndex = i;
                                PlaySong(queueSongs[i], true);
                                return;
                            }
                        }
                    }
                }

                if (e.Y < 42)
                {
                    isDraggingWindow = true;
                    dragStartPoint = e.Location;
                }
                return;
            }

            // 2. Bottom Player Bar Clicks
            if (e.Y >= pbY)
            {
                UnfocusSearchInput();
                int midX = w / 2;
                float btnCy = pbY + 30f;

                if (e.X >= 16 && e.X <= 265)
                {
                    TriggerButtonBounce("exp_bot");
                    isFullPlayerExpanded = !isFullPlayerExpanded;
                    this.Invalidate();
                    return;
                }
                if (e.X >= 266 && e.X <= 292 && currentSong != null)
                {
                    if (likedIds.Contains(currentSong.Id)) likedIds.Remove(currentSong.Id);
                    else likedIds.Add(currentSong.Id);
                    SavePersistentSettings();
                    this.Invalidate();
                    return;
                }
                if (e.X >= 295 && e.X <= 330)
                {
                    TriggerButtonBounce("exp_bot");
                    isFullPlayerExpanded = !isFullPlayerExpanded;
                    this.Invalidate();
                    return;
                }

                Rectangle shufBtn = new Rectangle(midX - 118, (int)btnCy - 14, 34, 28);
                if (shufBtn.Contains(e.Location))
                {
                    isShuffle = !isShuffle;
                    SavePersistentSettings();
                    ShowToast(isShuffle ? "Shuffle On" : "Shuffle Off");
                    return;
                }

                Rectangle prevCircle = new Rectangle(midX - 72, (int)btnCy - 18, 36, 36);
                if (prevCircle.Contains(e.Location)) { PrevTrack(); return; }

                Rectangle playCircle = new Rectangle(midX - 23, (int)btnCy - 23, 46, 46);
                if (playCircle.Contains(e.Location)) { TogglePlayPause(); return; }

                Rectangle nextCircle = new Rectangle(midX + 36, (int)btnCy - 18, 36, 36);
                if (nextCircle.Contains(e.Location)) { NextTrack(); return; }

                Rectangle repBtn = new Rectangle(midX + 82, (int)btnCy - 14, 34, 28);
                if (repBtn.Contains(e.Location))
                {
                    isRepeatOne = !isRepeatOne;
                    SavePersistentSettings();
                    ShowToast(isRepeatOne ? "Repeat One On" : "Repeat Off");
                    return;
                }

                int barW = 420;
                int barX = midX - barW / 2;
                Rectangle seekRect = new Rectangle(barX, pbY + 54, barW, 28);
                if (seekRect.Contains(e.Location))
                {
                    double frac = Math.Max(0.0, Math.Min(1.0, (double)(e.X - barX) / barW));
                    SeekToAbsolute(frac * currentDurSec);
                    return;
                }

                int volBarX = w - 336;
                int volBarW = 72;
                Rectangle volRect = new Rectangle(volBarX - 4, pbY + 30, volBarW + 8, 30);
                if (volRect.Contains(e.Location))
                {
                    int newV = (int)(((double)(e.X - volBarX) / volBarW) * 100.0);
                    SetVolume(newV);
                    return;
                }

                Rectangle eqQuickBtn = new Rectangle(w - 250, pbY + 28, 42, 34);
                if (eqQuickBtn.Contains(e.Location))
                {
                    TriggerButtonBounce("eq_bot");
                    isFullPlayerExpanded = false;
                    activeTab = (activeTab == 4) ? 0 : 4;
                    tabFadeAnim = 0.2f;
                    this.Invalidate();
                    return;
                }

                Rectangle dlMainBtn = new Rectangle(w - 200, pbY + 28, 102, 34);
                if (dlMainBtn.Contains(e.Location))
                {
                    DownloadSongDirectly(currentSong);
                    return;
                }

                Rectangle openDirBtn = new Rectangle(w - 90, pbY + 28, 70, 34);
                if (openDirBtn.Contains(e.Location))
                {
                    TriggerButtonBounce("folder_bot");
                    try { Process.Start("explorer.exe", downloadsFolder); } catch { }
                    return;
                }
                return;
            }

            // 3. Left Sidebar Navigation Clicks
            if (e.X < sidebarW && e.Y < mainH)
            {
                UnfocusSearchInput();
                for (int i = 0; i < 5; i++)
                {
                    Rectangle navRect = new Rectangle(14, 68 + i * 43, sidebarW - 28, 37);
                    if (navRect.Contains(e.Location))
                    {
                        TriggerButtonBounce("nav_" + i);
                        activeTab = i;
                        tabFadeAnim = 0.2f;
                        if (i == 0)
                        {
                            searchBox.Text = "";
                            searchQueryText = "";
                            liveSearchResults.Clear();
                        }
                        else if (i == 1)
                        {
                            FocusSearchInput();
                        }
                        if (i < 4) FilterDisplaySongs();
                        else this.Invalidate();
                        return;
                    }
                }

                Rectangle discordSideBtn = new Rectangle(14, mainH - 158, sidebarW - 28, 36);
                if (discordSideBtn.Contains(e.Location))
                {
                    TriggerButtonBounce("discord_side");
                    OpenDiscordLink();
                    return;
                }

                Rectangle fullBtnSide = new Rectangle(14, mainH - 116, sidebarW - 28, 36);
                if (fullBtnSide.Contains(e.Location))
                {
                    TriggerButtonBounce("full_side");
                    isFullPlayerExpanded = true;
                    this.Invalidate();
                    return;
                }

                for (int i = 0; i < 5; i++)
                {
                    Rectangle dotRect = new Rectangle(18 + i * 31, mainH - 42, 22, 22);
                    if (dotRect.Contains(e.Location))
                    {
                        TriggerButtonBounce("theme_" + i);
                        activeThemeIdx = i;
                        accentColor = themes[i].Accent;
                        SavePersistentSettings();
                        this.Invalidate();
                        return;
                    }
                }
                return;
            }

            // 4. Right Docked Panel Clicks
            if (e.X >= rightX && e.Y < mainH)
            {
                UnfocusSearchInput();
                Rectangle expBtn = new Rectangle(rightX + 16, 14, 28, 28);
                if (expBtn.Contains(e.Location))
                {
                    TriggerButtonBounce("exp_right");
                    isFullPlayerExpanded = true;
                    this.Invalidate();
                    return;
                }

                Rectangle flowerRect = new Rectangle(rightX + (rightW - 168) / 2, 38, 168, 168);
                if (flowerRect.Contains(e.Location))
                {
                    artworkShapeMode = (artworkShapeMode + 1) % artworkShapeNames.Length;
                    SavePersistentSettings();
                    ShowToast("Player Style: " + artworkShapeNames[artworkShapeMode]);
                    return;
                }

                Rectangle tabContainer = new Rectangle(rightX + 16, 286, rightW - 32, 32);
                if (tabContainer.Contains(e.Location))
                {
                    int halfTabW = (tabContainer.Width - 6) / 2;
                    rightPanelMode = (e.X < tabContainer.X + 3 + halfTabW) ? 0 : 1;
                    SavePersistentSettings();
                    this.Invalidate();
                    return;
                }

                Rectangle panelBox = new Rectangle(rightX + 16, 324, rightW - 32, mainH - 336);
                if (panelBox.Contains(e.Location))
                {
                    if (rightPanelMode == 0)
                    {
                        int qRowH = 46;
                        for (int i = 0; i < queueSongs.Count; i++)
                        {
                            int qy = panelBox.Y + 8 + i * (qRowH + 4) - queueScrollOffset;
                            Rectangle qRowRect = new Rectangle(panelBox.X + 8, qy, panelBox.Width - 16, qRowH);
                            if (qRowRect.Contains(e.Location))
                            {
                                currentQueueIndex = i;
                                PlaySong(queueSongs[i], true);
                                return;
                            }
                        }
                    }
                    else
                    {
                        // Clicking a lyric line in the Right Docked BetterLyrics panel also seeks to that line!
                        for (int i = 0; i < activeLyricHitRects.Count; i++)
                        {
                            if (activeLyricHitRects[i].Contains(e.Location))
                            {
                                SeekToAbsolute(activeLyricHitTimes[i]);
                                ShowToast("Jumped to lyric (" + FormatSec((int)activeLyricHitTimes[i]) + ")");
                                return;
                            }
                        }
                    }
                }
                return;
            }

            // 5. Settings & EQ Tab Clicks
            if (activeTab == 4 && e.X >= centerX && e.X < rightX)
            {
                UnfocusSearchInput();
                HandleSettingsClick(e.Location, centerX, centerW, false);
                return;
            }

            // 6. Center Song Stage Search, Quick Play & List Clicks
            Rectangle searchPill = new Rectangle(centerX + 26, 48, 330, 36);
            Rectangle searchGoBtn = new Rectangle(searchPill.Right + 8, 48, 78, 36);
            Rectangle topDiscordBtn = new Rectangle(searchPill.Right + 94, 48, 84, 36);
            Rectangle topUpdateBtn = new Rectangle(searchPill.Right + 186, 48, 96, 36);

            if (topDiscordBtn.Contains(e.Location))
            {
                UnfocusSearchInput();
                OpenDiscordLink();
                return;
            }

            if (topUpdateBtn.Contains(e.Location))
            {
                UnfocusSearchInput();
                CheckForAppUpdatesAsync(true);
                return;
            }

            if (searchGoBtn.Contains(e.Location))
            {
                TriggerButtonBounce("search_go");
                string q = (searchBox.Visible ? searchBox.Text : searchQueryText).Trim();
                UnfocusSearchInput();
                if (!string.IsNullOrEmpty(q)) RunLiveSaavnSearch(q);
                else FocusSearchInput();
                return;
            }

            if (searchPill.Contains(e.Location))
            {
                if (!string.IsNullOrEmpty(searchQueryText) && e.X >= searchPill.Right - 32)
                {
                    searchBox.Text = "";
                    searchQueryText = "";
                    liveSearchResults.Clear();
                    UnfocusSearchInput();
                    FilterDisplaySongs();
                    return;
                }
                FocusSearchInput();
                return;
            }
            else
            {
                UnfocusSearchInput();
            }

            if (e.Y < 42)
            {
                isDraggingWindow = true;
                dragStartPoint = e.Location;
                return;
            }

            // Mood Filter Chips Clicks
            if (e.Y >= 92 && e.Y <= 126 && e.X >= centerX && e.X < rightX)
            {
                using (Graphics g = this.CreateGraphics())
                {
                    int chipX = centerX + 26;
                    for (int i = 0; i < moods.Length; i++)
                    {
                        int cw = (int)g.MeasureString(moods[i], fontSmallBold).Width + 22;
                        Rectangle cRect = new Rectangle(chipX, 96, cw, 27);
                        if (cRect.Contains(e.Location))
                        {
                            TriggerButtonBounce("mood_" + i);
                            selectedMood = moods[i];
                            liveSearchResults.Clear();
                            searchBox.Text = "";
                            searchQueryText = "";
                            FilterDisplaySongs();
                            return;
                        }
                        chipX += cw + 7;
                    }
                }
            }

            // Quick Play Card & Refresh Clicks (when on Home tab and Quick Play is unlocked)
            if (activeTab == 0 && string.IsNullOrEmpty(searchQueryText) && quickPlayUnlocked && quickPlaySongs.Count > 0)
            {
                Rectangle qpRefreshBtn = new Rectangle(centerX + centerW - 118, 126, 92, 26);
                if (qpRefreshBtn.Contains(e.Location))
                {
                    TriggerButtonBounce("qp_refresh");
                    ShowToast("Refreshing Quick Play with new songs...");
                    RefreshQuickPlayWithNewSongsOnStartup();
                    return;
                }

                int numQp = Math.Min(4, quickPlaySongs.Count);
                int gap = 10;
                int cardW = (centerW - 52 - (numQp - 1) * gap) / Math.Max(1, numQp);
                for (int i = 0; i < numQp; i++)
                {
                    Rectangle qpRect = new Rectangle(centerX + 26 + i * (cardW + gap), 154, cardW, 68);
                    if (qpRect.Contains(e.Location))
                    {
                        TriggerButtonBounce("qp_" + i);
                        PlaySong(quickPlaySongs[i], false);
                        return;
                    }
                }
            }

            int listTop = GetCenterSongListTopY();
            int rowH = 58;
            if (e.X >= centerX && e.X < rightX && e.Y >= listTop && e.Y < mainH)
            {
                for (int i = 0; i < displaySongs.Count; i++)
                {
                    int ry = listTop + i * (rowH + 6) - scrollOffset;
                    Rectangle rowRect = new Rectangle(centerX + 26, ry, centerW - 52, rowH);
                    if (rowRect.Contains(e.Location))
                    {
                        SongItem clicked = displaySongs[i];

                        Rectangle dlBtn = new Rectangle(rowRect.Right - 54, rowRect.Y + 12, 44, 34);
                        if (dlBtn.Contains(e.Location)) { DownloadSongDirectly(clicked); return; }

                        Rectangle likeBtn = new Rectangle(rowRect.Right - 96, rowRect.Y + 12, 36, 34);
                        if (likeBtn.Contains(e.Location))
                        {
                            if (likedIds.Contains(clicked.Id)) likedIds.Remove(clicked.Id);
                            else likedIds.Add(clicked.Id);
                            SavePersistentSettings();
                            this.Invalidate();
                            return;
                        }

                        Rectangle addQBtn = new Rectangle(rowRect.Right - 134, rowRect.Y + 12, 34, 34);
                        if (addQBtn.Contains(e.Location)) { AddToQueue(clicked); return; }

                        PlaySong(clicked, false);
                        return;
                    }
                }
            }
        }

        private void HandleSettingsClick(Point pt, int centerX, int centerW, bool isDrag)
        {
            int padX = centerX + 26;
            int contentW = centerW - 52;
            Rectangle eqCard = new Rectangle(padX, 64, contentW, 232);

            int bandStartX = eqCard.X + 36;
            int bandTopY = eqCard.Y + 82;
            int bandTrackH = 108;
            int bandSpacing = 54;
            for (int i = 0; i < 5; i++)
            {
                int bx = bandStartX + i * bandSpacing;
                Rectangle bRect = new Rectangle(bx - 18, bandTopY - 10, 36, bandTrackH + 20);
                if (bRect.Contains(pt))
                {
                    float frac = 1f - Math.Max(0f, Math.Min(1f, (float)(pt.Y - bandTopY) / bandTrackH));
                    eqBands[i] = -10f + frac * 20f;
                    eqEnabled = true;
                    activeEqPreset = "Custom";
                    ApplyEffectiveDspAndVolume();
                    SavePersistentSettings();
                    ShowToast(eqBandLabels[i] + ": " + eqBands[i].ToString("+0.0;-0.0;0.0") + " dB");
                    return;
                }
            }

            int dspStartX = eqCard.X + 320;
            int dspW = Math.Max(140, eqCard.Right - dspStartX - 24);
            for (int i = 0; i < 3; i++)
            {
                int barY = eqCard.Y + 86 + i * 46 + 24;
                Rectangle dRect = new Rectangle(dspStartX - 8, barY - 14, dspW + 16, 28);
                if (dRect.Contains(pt))
                {
                    int pct = (int)Math.Max(0, Math.Min(100, ((double)(pt.X - dspStartX) / dspW) * 100.0));
                    eqEnabled = true;
                    if (i == 0) { bassBoostPct = pct; ShowToast("Bass Boost: " + pct + "%"); }
                    else if (i == 1) { virtualizerPct = pct; ShowToast("3D Virtualizer: " + pct + "%"); }
                    else { loudnessGainPct = pct; ShowToast("Loudness Gain: " + pct + "%"); }
                    ApplyEffectiveDspAndVolume();
                    SavePersistentSettings();
                    return;
                }
            }

            if (isDrag) return;

            Rectangle eqToggleRect = new Rectangle(eqCard.Right - 68, eqCard.Y + 8, 52, 28);
            if (eqToggleRect.Contains(pt))
            {
                eqEnabled = !eqEnabled;
                ApplyEffectiveDspAndVolume();
                SavePersistentSettings();
                ShowToast(eqEnabled ? "Equalizer Enabled (" + activeEqPreset + ")" : "Equalizer Bypassed");
                return;
            }

            using (Graphics g = this.CreateGraphics())
            using (Font fChip = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            {
                int px = eqCard.X + 18;
                int py = eqCard.Y + 42;
                for (int i = 0; i < eqPresetNames.Length; i++)
                {
                    int pw = (int)g.MeasureString(eqPresetNames[i], fChip).Width + 16;
                    Rectangle pr = new Rectangle(px, py, pw, 26);
                    if (pr.Contains(pt))
                    {
                        ApplyEqPreset(eqPresetNames[i]);
                        return;
                    }
                    px += pw + 6;
                }
            }

            int halfW = (contentW - 14) / 2;
            Rectangle cfCard = new Rectangle(padX, 310, halfW, 188);
            if (new Rectangle(cfCard.X + 12, cfCard.Y + 38, cfCard.Width - 24, 26).Contains(pt))
            {
                crossfadeEnabled = !crossfadeEnabled;
                SavePersistentSettings();
                ShowToast(crossfadeEnabled ? ("Crossfade Enabled (" + crossfadeSeconds + "s)") : "Crossfade Off");
                return;
            }
            int[] cfOpts = new int[] { 1, 3, 5, 8, 12 };
            for (int i = 0; i < cfOpts.Length; i++)
            {
                Rectangle cfr = new Rectangle(cfCard.X + 16 + i * 48, cfCard.Y + 68, 44, 28);
                if (cfr.Contains(pt))
                {
                    crossfadeSeconds = cfOpts[i];
                    crossfadeEnabled = true;
                    SavePersistentSettings();
                    ShowToast("Crossfade Applied: " + crossfadeSeconds + "s Overlap");
                    return;
                }
            }
            if (new Rectangle(cfCard.X + 12, cfCard.Y + 100, cfCard.Width - 24, 26).Contains(pt))
            {
                gaplessPlayback = !gaplessPlayback;
                SavePersistentSettings();
                ShowToast(gaplessPlayback ? "Gapless Playback On" : "Gapless Playback Off");
                return;
            }
            if (new Rectangle(cfCard.X + 12, cfCard.Y + 128, cfCard.Width - 24, 26).Contains(pt))
            {
                skipSilence = !skipSilence;
                SavePersistentSettings();
                ShowToast(skipSilence ? "Skip Silence On" : "Skip Silence Off");
                return;
            }
            if (new Rectangle(cfCard.X + 12, cfCard.Y + 156, cfCard.Width - 24, 26).Contains(pt))
            {
                autoMixEnabled = !autoMixEnabled;
                SavePersistentSettings();
                ShowToast(autoMixEnabled ? "Auto-Mix Queue On" : "Auto-Mix Queue Off");
                return;
            }

            Rectangle uiCard = new Rectangle(padX + halfW + 14, 310, halfW, 188);
            for (int i = 0; i < artworkShapeNames.Length; i++)
            {
                int sw = (uiCard.Width - 44) / 4;
                Rectangle sr = new Rectangle(uiCard.X + 16 + i * (sw + 4), uiCard.Y + 40, sw, 28);
                if (sr.Contains(pt))
                {
                    artworkShapeMode = i;
                    SavePersistentSettings();
                    ShowToast("Animated Style: " + artworkShapeNames[i]);
                    return;
                }
            }
            int[] stOpts = new int[] { 0, 15, 30, 60 };
            for (int i = 0; i < stOpts.Length; i++)
            {
                Rectangle str = new Rectangle(uiCard.X + 102 + i * 44, uiCard.Y + 72, 40, 26);
                if (str.Contains(pt))
                {
                    sleepTimerMinutes = stOpts[i];
                    sleepTimerSecondsLeft = sleepTimerMinutes * 60;
                    SavePersistentSettings();
                    ShowToast(sleepTimerMinutes == 0 ? "Sleep Timer Off" : ("Sleep Timer: " + sleepTimerMinutes + " mins"));
                    return;
                }
            }
            if (new Rectangle(uiCard.X + 12, uiCard.Y + 102, uiCard.Width - 24, 26).Contains(pt))
            {
                wavySliderEnabled = !wavySliderEnabled;
                SavePersistentSettings();
                ShowToast(wavySliderEnabled ? "Wavy Progress Bar On" : "Classic Bar On");
                return;
            }
            if (new Rectangle(uiCard.X + 12, uiCard.Y + 130, uiCard.Width - 24, 26).Contains(pt))
            {
                oledPureBlack = !oledPureBlack;
                SavePersistentSettings();
                ShowToast(oledPureBlack ? "OLED Pure Black Enabled" : "Obsidian Surface Restored");
                return;
            }
            if (new Rectangle(uiCard.Right - 90, uiCard.Y + 156, 76, 26).Contains(pt))
            {
                ClearAudioCache();
                return;
            }

            Rectangle palCard = new Rectangle(padX, 510, contentW, 74);
            int swX = palCard.X + 16;
            for (int i = 0; i < themes.Count; i++)
            {
                int wChip = (contentW - 32 - 8 * 6) / 9;
                Rectangle tr = new Rectangle(swX + i * (wChip + 6), palCard.Y + 32, wChip, 32);
                if (tr.Contains(pt))
                {
                    activeThemeIdx = i;
                    accentColor = themes[i].Accent;
                    SavePersistentSettings();
                    ShowToast("Theme Saved: " + themes[i].Name);
                    return;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            int sidebarW = 195;
            int rightW = 315;
            int playerH = 90;
            int pbY = h - playerH;
            int centerX = sidebarW;
            int centerW = w - sidebarW - rightW;
            int rightX = w - rightW;

            if (isDraggingWindow && e.Button == MouseButtons.Left)
            {
                this.Location = new Point(this.Left + e.X - dragStartPoint.X, this.Top + e.Y - dragStartPoint.Y);
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                if (isFullPlayerExpanded)
                {
                    int bottomDeckTop = h - 250;
                    int deckW = Math.Min(680, w - 80);
                    int deckX = (w - deckW) / 2;
                    int sliderY = bottomDeckTop + 78;
                    if (e.Y >= sliderY - 16 && e.Y <= sliderY + 16 && e.X >= deckX - 8 && e.X <= deckX + deckW + 8)
                    {
                        double frac = Math.Max(0.0, Math.Min(1.0, (double)(e.X - deckX) / deckW));
                        SeekToAbsolute(frac * currentDurSec);
                        return;
                    }
                }
                else if (e.Y >= pbY)
                {
                    int volBarX = w - 336;
                    int volBarW = 72;
                    if (e.X >= volBarX - 8 && e.X <= volBarX + volBarW + 8 && e.Y >= pbY + 26 && e.Y <= pbY + 62)
                    {
                        SetVolume((int)(((double)(e.X - volBarX) / volBarW) * 100.0));
                        return;
                    }
                }
                else if (activeTab == 4 && e.X >= centerX && e.X < rightX)
                {
                    HandleSettingsClick(e.Location, centerX, centerW, true);
                    return;
                }
            }

            // Detect hovered button key for smooth button hover scale animations!
            string newHovKey = "";
            int midX = w / 2;

            if (isFullPlayerExpanded)
            {
                int bottomDeckTop = h - 250;
                int transY = bottomDeckTop + 114;
                if (new Rectangle(28, 18, 128, 34).Contains(e.Location)) newHovKey = "full_close";
                else if (new Rectangle(166, 18, 152, 34).Contains(e.Location)) newHovKey = "full_mode";
                else if (new Rectangle(328, 18, 124, 34).Contains(e.Location)) newHovKey = "full_style";
                else if (new Rectangle(462, 18, 108, 34).Contains(e.Location)) newHovKey = "full_discord";
                else if (new Rectangle(midX - 210, transY, 72, 64).Contains(e.Location)) newHovKey = "prev";
                else if (new Rectangle(midX - 122, transY, 244, 64).Contains(e.Location)) newHovKey = "play";
                else if (new Rectangle(midX + 138, transY, 72, 64).Contains(e.Location)) newHovKey = "next";
                currentHoveredBtnKey = newHovKey;
                return;
            }

            hoveredNavIndex = -1;
            hoveredSongIndex = -1;
            hoveredQueueIndex = -1;
            hoveredQuickPlayIndex = -1;

            int mainH = h - playerH;

            if (e.Y >= pbY)
            {
                float btnCy = pbY + 30f;
                if (new Rectangle(midX - 72, (int)btnCy - 18, 36, 36).Contains(e.Location)) newHovKey = "prev";
                else if (new Rectangle(midX - 23, (int)btnCy - 23, 46, 46).Contains(e.Location)) newHovKey = "play";
                else if (new Rectangle(midX + 36, (int)btnCy - 18, 36, 36).Contains(e.Location)) newHovKey = "next";
                else if (new Rectangle(w - 250, pbY + 28, 42, 34).Contains(e.Location)) newHovKey = "eq_bot";
                else if (new Rectangle(w - 200, pbY + 28, 102, 34).Contains(e.Location)) newHovKey = "download";
                else if (new Rectangle(w - 90, pbY + 28, 70, 34).Contains(e.Location)) newHovKey = "folder_bot";
                else if (new Rectangle(298, pbY + 31, 28, 28).Contains(e.Location)) newHovKey = "exp_bot";
            }
            else if (e.X < sidebarW)
            {
                for (int i = 0; i < 5; i++)
                {
                    Rectangle navRect = new Rectangle(14, 68 + i * 43, sidebarW - 28, 37);
                    if (navRect.Contains(e.Location))
                    {
                        hoveredNavIndex = i;
                        newHovKey = "nav_" + i;
                        break;
                    }
                }
                if (new Rectangle(14, mainH - 158, sidebarW - 28, 36).Contains(e.Location)) newHovKey = "discord_side";
                else if (new Rectangle(14, mainH - 116, sidebarW - 28, 36).Contains(e.Location)) newHovKey = "full_side";
            }
            else if (activeTab < 4 && e.X < rightX)
            {
                if (activeTab == 0 && string.IsNullOrEmpty(searchQueryText) && quickPlayUnlocked && quickPlaySongs.Count > 0)
                {
                    if (new Rectangle(centerX + centerW - 118, 126, 92, 26).Contains(e.Location))
                    {
                        newHovKey = "qp_refresh";
                    }
                    int numQp = Math.Min(4, quickPlaySongs.Count);
                    int gap = 10;
                    int cardW = (centerW - 52 - (numQp - 1) * gap) / Math.Max(1, numQp);
                    for (int i = 0; i < numQp; i++)
                    {
                        Rectangle qpRect = new Rectangle(centerX + 26 + i * (cardW + gap), 154, cardW, 68);
                        if (qpRect.Contains(e.Location))
                        {
                            hoveredQuickPlayIndex = i;
                            newHovKey = "qp_" + i;
                            break;
                        }
                    }
                }

                int listTop = GetCenterSongListTopY();
                if (e.Y >= listTop && e.Y < mainH)
                {
                    int idx = (e.Y - listTop + scrollOffset) / 64;
                    if (idx >= 0 && idx < displaySongs.Count) hoveredSongIndex = idx;
                }
            }
            else if (e.X >= rightX && rightPanelMode == 0 && e.Y >= 330 && e.Y < mainH - 12)
            {
                int qIdx = (e.Y - 332 + queueScrollOffset) / 50;
                if (qIdx >= 0 && qIdx < queueSongs.Count) hoveredQueueIndex = qIdx;
            }

            currentHoveredBtnKey = newHovKey;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            isDraggingWindow = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int rightX = this.ClientSize.Width - 315;
            if ((isFullPlayerExpanded && fullViewSubMode == 0) || (!isFullPlayerExpanded && e.X >= rightX && rightPanelMode == 0))
            {
                int maxQScroll = Math.Max(0, queueSongs.Count * 50 - (this.ClientSize.Height - 450));
                queueScrollOffset = Math.Max(0, Math.Min(maxQScroll, queueScrollOffset - (e.Delta / 2)));
            }
            else if (!isFullPlayerExpanded && activeTab < 4)
            {
                int maxScroll = Math.Max(0, displaySongs.Count * 64 - (this.ClientSize.Height - 280));
                scrollOffset = Math.Max(0, Math.Min(maxScroll, scrollOffset - (e.Delta / 2)));
            }
            this.Invalidate();
        }

        private string FormatSec(int sec)
        {
            if (sec < 0) sec = 0;
            int m = sec / 60;
            int s = sec % 60;
            return m + ":" + (s < 10 ? "0" + s : s.ToString());
        }

        private GraphicsPath GetRoundRectPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void FillRoundRect(Graphics g, Brush b, Rectangle r, int radius)
        {
            using (GraphicsPath p = GetRoundRectPath(r, radius))
            {
                g.FillPath(b, p);
            }
        }

        private void DrawRoundRect(Graphics g, Pen pen, Rectangle r, int radius)
        {
            using (GraphicsPath p = GetRoundRectPath(r, radius))
            {
                g.DrawPath(pen, p);
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            string localAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeoMusic");

            EnsureDesktopLaunchers(localAppDir);

            if (args != null && args.Length > 0 && args[0] == "--install")
            {
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LeoMainForm());
        }

        private static void EnsureDesktopLaunchers(string installDir)
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                string icoPath = Path.Combine(installDir, "leo_icon.ico");
                if (!File.Exists(icoPath))
                {
                    icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "leo_icon.ico");
                }

                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] folders = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    Path.Combine(userProfile, "Desktop"),
                    Path.Combine(userProfile, @"OneDrive\Desktop"),
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs)
                };

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);

                foreach (string dir in folders)
                {
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        try
                        {
                            string lnkPath = Path.Combine(dir, "Leo Music.lnk");
                            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                            Type scType = shortcut.GetType();
                            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
                            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { installDir });
                            if (File.Exists(icoPath))
                            {
                                scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { icoPath + ",0" });
                            }
                            else
                            {
                                scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { exePath + ",0" });
                            }
                            scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Leo Music Studio" });
                            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}
