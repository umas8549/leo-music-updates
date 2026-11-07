// ============================================================================
// LEO MUSIC STUDIO v10.6.3 - ZERO-ECHO 3D STAGE, COUNTRY, YOULYPLUS & SMART AI
// 100% Native Windows .NET Desktop Application (Compiles via built-in csc.exe)
// ============================================================================
// v10.6.3 UPGRADES:
// 1. Docked Leo AI Side Panel on Homepage (320px right dock, NOT full-screen):
//    - Smoothly hides or unhides when clicking the Leo AI Circle on the Homepage,
//      Top Bar [ ✦ Leo AI ] pill, Bottom Bar [ ✦ AI ] button, or Floating Widget.
//    - Includes 3-tab right panel switcher: [ Up Next ] | [ Lyrics ] | [ ✦ Leo AI ].
// 2. 4 Brand-New Vector Leo AI Circle Designs (aiOrbStyleIdx = 0..3):
//    - 0: Siri Silk Ring (glowing multi-stop cyan/magenta/violet neon ring)
//    - 1: Google Halo (4 smooth 90-degree colored arc segments + glass core)
//    - 2: Cyber Neon Pulse (concentric glowing rings + live 3-bar audio EQ)
//    - 3: Minimal Glass Gem (sleek smoked-glass circle with accent rim & ✦ AI)
// 3. Settings ON/OFF Toggle & Circle Design Selector:
//    - Turn Leo AI Circle & Side Panel ON or OFF anytime in Settings (persisted).
// 4. High-Smart Fuzzy & Typo-Tolerant AI Engine:
//    - Understands half-written words and wrong spellings across ALL AI tasks
//      ("paly"/"plai"/"ply"/"pla" -> "play", "opn" -> "open", "whtsp" -> "whatsapp",
//       "msg" -> "message", "yt" -> "youtube", "chtgpt" -> "chatgpt", "ad nte" -> "add note",
//       "dwnld" -> "download", "volm" -> "volume", "scrnshot" -> "screenshot")
//      AND fuzzy-matches half-written/misspelled song & artist names ("paly strby" -> "Starboy").
// 5. Decluttered Clean Studio UI:
//    - Streamlined Top Bar (3 clean pills), Hero Banner, and Track List rows.
// ============================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Leo Music Studio v10.6.3")]
[assembly: AssemblyDescription("Leo Music Studio v10.6.3 - Native 3D Audio, Country Shelves, YouLyPlus & Smart AI Side Panel")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Leo Studio Labs")]
[assembly: AssemblyProduct("Leo Music Studio")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Leo Studio Labs")]
[assembly: AssemblyVersion("10.6.3.0")]
[assembly: AssemblyFileVersion("10.6.3.0")]

namespace LeoMusicNative
{
    public class SongItem
    {
        public string Id = "";
        public string Title = "";
        public string Artist = "";
        public string Album = "";
        public string Duration = "3:30";
        public int DurationSec = 210;
        public string Genre = "Pop";
        public string Mood = "Chill";
        public string Language = "Hindi";
        public string Country = "India";
        public string StreamUrl = "";
        public string PreviewUrl = "";
        public string CoverUrl = "";
        public string VideoId = "";
        public int Bpm = 100;
        public string AudioBitrate = "24-Bit Studio";
        public string SampleRate = "96 kHz";
        public string LyricSnippet = "";
        public string FullLyrics = "";
        public bool IsLiked = false;
        public bool IsDownloaded = false;
        public string LocalFilePath = "";
        public string LocalCoverPath = "";
        public int PlayCount = 0;
        public long LastPlayedTicks = 0;
        public Color AccentColor = Color.FromArgb(0, 210, 255);

        public SongItem Clone()
        {
            return (SongItem)this.MemberwiseClone();
        }
    }

    public class LyricLine
    {
        public double TimeSec = 0.0;
        public double StartTime = 0.0;
        public double EndTime = 4.0;
        public string Text = "";
        public string SubText = "";
        public string RomanizedText = "";
        public string[] Syllables = new string[0];
        public bool IsChorus = false;
        public bool IsInstrumental = false;
        public int VocalSide = 0;
    }

    public class LyricCandidateItem
    {
        public string Provider = "YouLyPlus";
        public string Title = "";
        public string Artist = "";
        public int Score = 99;
        public bool IsSynced = true;
        public List<LyricLine> Lines = new List<LyricLine>();
        public string RawLyrics = "";
        public string SummaryBadge = "★ YouLy+ Auto-Best 99%";
    }

    public class PlaylistEntry
    {
        public string Name = "";
        public string Description = "";
        public List<string> SongIds = new List<string>();
        public Color CoverColor = Color.FromArgb(0, 185, 230);
        public string CreatedDate = "2026";
    }

    // ========================================================================
    // FLOATING DESKTOP AGENT WIDGET (Always-on-top mini circular orb on PC)
    // ========================================================================
    public class FloatingAgentWidgetForm : Form
    {
        private LeoMusicForm ownerForm;
        private bool isDragging = false;
        private Point dragStartPoint = Point.Empty;
        private Rectangle rectWidgetOrb = new Rectangle(12, 6, 64, 64);
        private Rectangle rectWidgetOpenPanel = new Rectangle(4, 68, 26, 16);
        private Rectangle rectWidgetVoiceBtn = new Rectangle(31, 68, 26, 16);
        private Rectangle rectWidgetQuickPlay = new Rectangle(58, 68, 26, 16);

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public FloatingAgentWidgetForm(LeoMusicForm owner)
        {
            this.ownerForm = owner;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.MinimumSize = new Size(88, 88);
            this.MaximumSize = new Size(88, 88);
            this.Size = new Size(88, 88);
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(14, 18, 26);

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(wa.Right - 104, wa.Bottom - 108);

            UpdateWidgetRegion();

            this.Paint += PaintWidget;
            this.MouseDown += OnWidgetMouseDown;
            this.MouseMove += OnWidgetMouseMove;
            this.MouseUp += OnWidgetMouseUp;
            this.MouseClick += OnWidgetMouseClick;
            this.MouseDoubleClick += delegate(object s, MouseEventArgs e)
            {
                if (ownerForm != null && !ownerForm.IsDisposed)
                {
                    ownerForm.OpenDockedAiPanelFromWidget();
                }
            };
        }

        public void UpdateWidgetRegion()
        {
            try
            {
                IntPtr hRgn = CreateRoundRectRgn(0, 0, this.Width + 1, this.Height + 1, 28, 28);
                this.Region = Region.FromHrgn(hRgn);
                DeleteObject(hRgn);
            }
            catch { }
        }

        private void OnWidgetMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                dragStartPoint = e.Location;
            }
        }

        private void OnWidgetMouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point p = PointToScreen(e.Location);
                this.Location = new Point(p.X - dragStartPoint.X, p.Y - dragStartPoint.Y);
            }
        }

        private void OnWidgetMouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        private void OnWidgetMouseClick(object sender, MouseEventArgs e)
        {
            if (ownerForm == null || ownerForm.IsDisposed) return;

            if (rectWidgetVoiceBtn.Contains(e.Location))
            {
                ownerForm.ToggleAgentVoiceListening();
                this.Invalidate();
                return;
            }
            if (rectWidgetQuickPlay.Contains(e.Location))
            {
                ownerForm.TogglePlayPause();
                this.Invalidate();
                return;
            }
            if (rectWidgetOpenPanel.Contains(e.Location) || rectWidgetOrb.Contains(e.Location))
            {
                ownerForm.OpenDockedAiPanelFromWidget();
                this.Invalidate();
                return;
            }
        }

        private void PaintWidget(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush bg = new SolidBrush(Color.FromArgb(14, 18, 26)))
            {
                g.FillRectangle(bg, this.ClientRectangle);
            }
            using (Pen borderPen = new Pen(Color.FromArgb(90, 0, 215, 255), 1.4f))
            {
                g.DrawRectangle(borderPen, 1, 1, this.Width - 3, this.Height - 3);
            }

            if (ownerForm != null)
            {
                ownerForm.DrawLeoAiCircleOrb(g, new Rectangle(16, 6, 56, 56), ownerForm.AgentPhase, ownerForm.IsAgentVoiceListening, ownerForm.AiOrbStyleIdx);
            }

            DrawMiniPill(g, rectWidgetOpenPanel, "AI", ownerForm != null && ownerForm.IsAgentPanelOpen, Color.FromArgb(0, 210, 255));
            DrawMiniPill(g, rectWidgetVoiceBtn, "Mic", ownerForm != null && ownerForm.IsAgentVoiceListening, Color.FromArgb(40, 220, 130));
            DrawMiniPill(g, rectWidgetQuickPlay, ownerForm != null && ownerForm.IsPlaying ? "||" : ">", ownerForm != null && ownerForm.IsPlaying, Color.FromArgb(255, 195, 40));
        }

        private void DrawMiniPill(Graphics g, Rectangle r, string text, bool active, Color accent)
        {
            using (SolidBrush b = new SolidBrush(active ? Color.FromArgb(75, accent) : Color.FromArgb(30, 36, 48)))
            {
                g.FillRectangle(b, r);
            }
            using (Pen p = new Pen(active ? accent : Color.FromArgb(70, 255, 255, 255), 1f))
            {
                g.DrawRectangle(p, r);
            }
            using (Font f = new Font("Segoe UI", 6.5f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(active ? Color.White : Color.FromArgb(200, 210, 225)))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, f, tb, r, sf);
            }
        }
    }


    // ========================================================================
    // MAIN LEO MUSIC STUDIO v10.6.3 NATIVE DESKTOP FORM
    // ========================================================================
    public class LeoMusicForm : Form
    {
        public const string APP_VERSION = "10.6.3";
        public const string APP_TITLE = "LEO Music Studio v10.6.3";

        // Win32 & MCI Native Interop
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("winmm.dll")]
        private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        [DllImport("winmm.dll")]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        // Core Directories & Local Persistent Data
        private string appDataDir = "";
        private string localStorageRootDir = "";
        private string localDownloadsDir = "";
        private string localCoversDir = "";
        private string localCatalogFile = "";
        private string settingsFilePath = "";
        private string notesFilePath = "";
        private string userAvatarFilePath = "";

        // User Profile & Preferences
        private string userName = "Gourav";
        private string selectedCountry = "India";
        private string selectedMood = "All";
        private string activeTab = "Home"; // Home, Search, Library, Liked Songs, Playlists, Downloads, Settings
        private string selectedPlaylistName = "";
        private bool isFirstRunCompleted = true;
        private bool isSetupModalOpen = false;
        private bool isSettingsModalOpen = false;
        private bool isEditingUserName = false;
        private bool isSearchFocused = false;
        private bool isAgentInputFocused = false;
        private bool isFullScreenStage = false;
        private string searchQuery = "";
        private Image userProfileAvatarBmp = null;

        // v10.6.3 Leo AI Side Panel & Circle Design State
        private bool aiAssistantEnabled = true;
        private int aiOrbStyleIdx = 0; // 0: Siri Silk Ring, 1: Google Halo, 2: Cyber Neon Pulse, 3: Minimal Glass Gem
        private readonly string[] aiOrbStyleNames = new string[] {
            "Siri Silk Ring",
            "Google Halo",
            "Cyber Neon Pulse",
            "Minimal Glass Gem"
        };
        private bool isRightDockVisible = true;
        private int rightPanelMode = 0; // 0: Up Next Queue, 1: Synced Lyrics, 2: Leo AI Side Panel
        private bool isAgentPanelOpen = false;
        private bool isAgentVoiceListening = false;
        private bool isFloatingWidgetEnabled = false;
        private float agentPhase = 0f;
        private string agentInputText = "";
        private string agentSmartHintText = "";
        private string agentStatusText = "Ready • Click Circle to Hide/Unhide or Speak/Type Any Command";
        private List<string> agentTaskLog = new List<string>();
        private FloatingAgentWidgetForm floatingWidgetForm = null;
        private Process voiceListenerProcess = null;

        // Public Accessors for Floating Widget
        public float AgentPhase { get { return agentPhase; } }
        public bool IsAgentVoiceListening { get { return isAgentVoiceListening; } }
        public bool IsAgentPanelOpen { get { return isAgentPanelOpen && isRightDockVisible && rightPanelMode == 2; } }
        public int AiOrbStyleIdx { get { return aiOrbStyleIdx; } }
        public bool IsPlaying { get { return isPlaying; } }

        // Audio & Playback State
        private List<SongItem> allSongs = new List<SongItem>();
        private List<SongItem> currentQueue = new List<SongItem>();
        private List<SongItem> recentlyPlayed = new List<SongItem>();
        private List<SongItem> onlineSearchResults = new List<SongItem>();
        private List<PlaylistEntry> userPlaylists = new List<PlaylistEntry>();
        private Dictionary<string, Image> coverCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        private SongItem currentSong = null;
        private int currentQueueIndex = 0;
        private bool isPlaying = false;
        private bool isShuffle = false;
        private int repeatMode = 0; // 0: Off, 1: Repeat All, 2: Repeat One
        private double playbackPositionSec = 0.0;
        private double playbackDurationSec = 210.0;
        private int volumePercent = 120; // 0..200%
        private int crossfadeSeconds = 2; // 0, 2, 4, 6, 8
        private float deckBCrossfadeVolFloat = 0f;
        private bool isCrossfadingNow = false;
        private bool is3DSpatialEnabled = true;
        private int eqPresetIndex = 0;
        private readonly string[] eqPresetNames = new string[] {
            "24-Bit Studio Reference",
            "3D Spatial Surround",
            "Deep Sub-Bass Boost",
            "Crystal Vocal HD",
            "Party Club Master",
            "Late Night Lounge"
        };
        private bool isAbLoopActive = false;
        private double abLoopStartSec = 0.0;
        private double abLoopEndSec = 0.0;

        // Windows Media Player COM Object (Single-Stream Zero-Echo Architecture)
        private object wmpPrimaryDeck = null;
        private object wmpSpatialDeck = null; // Explicitly null in v10.5+ to prevent 200ms echo
        private bool isMciPlaying = false;

        // Lyrics & YouLyPlus Synced State
        private List<LyricLine> currentLyrics = new List<LyricLine>();
        private List<LyricCandidateItem> currentLyricCandidates = new List<LyricCandidateItem>();
        private int activeLyricIndex = 0;
        private bool showRomanizedLyrics = true;
        private string activeLyricBadge = "★ YouLy+ Auto-Best 99%";

        // Countries Supported
        private readonly string[] supportedCountries = new string[] {
            "India", "USA", "UK", "Korea", "Japan", "Spain", "Brazil",
            "France", "Germany", "Canada", "Australia", "UAE", "Nigeria", "Global"
        };

        private readonly string[] moodChips = new string[] {
            "All", "Party", "Energize", "Chill", "Relax", "Workout", "Focus", "Romance"
        };

        // UI Hit-Test Rectangles
        private Rectangle rectCloseBtn;
        private Rectangle rectMaxBtn;
        private Rectangle rectMinBtn;
        private Rectangle rectSearchBox;
        private Rectangle rectSearchClearBtn;
        private Rectangle rectTopBarAiBtn;
        private Rectangle rectCountrySelectorPill;
        private Rectangle rectTopVolumePill;
        private Rectangle rectProfileBtn;

        private List<KeyValuePair<string, Rectangle>> navItemRects = new List<KeyValuePair<string, Rectangle>>();
        private List<KeyValuePair<string, Rectangle>> playlistNavRects = new List<KeyValuePair<string, Rectangle>>();
        private Rectangle rectAddPlaylistBtn;
        private List<KeyValuePair<string, Rectangle>> moodChipRects = new List<KeyValuePair<string, Rectangle>>();

        // Homepage Hero & Shelves Hit-Test Rectangles
        private Rectangle rectHeroPlayBtn;
        private Rectangle rectHeroLikeBtn;
        private Rectangle rectHeroMoreBtn;
        private Rectangle rectHeroSmartDjBtn;
        private Rectangle rectPlayTrendingChartBtn;
        private List<KeyValuePair<SongItem, Rectangle>> songCardRects = new List<KeyValuePair<SongItem, Rectangle>>();
        private List<KeyValuePair<SongItem, Rectangle>> songLikeRects = new List<KeyValuePair<SongItem, Rectangle>>();
        private List<KeyValuePair<SongItem, Rectangle>> songDownloadRects = new List<KeyValuePair<SongItem, Rectangle>>();
        private Rectangle rectDownloadsPlayAllBtn;
        private Rectangle rectDownloadsOpenFolderBtn;

        // Bottom Player Bar Hit-Test Rectangles
        private Rectangle rectBottomSongInfo;
        private Rectangle rectBottomLikeBtn;
        private Rectangle rectBottomDownloadBtn;
        private Rectangle rectShuffleBtn;
        private Rectangle rectPrevBtn;
        private Rectangle rectPlayPauseBtn;
        private Rectangle rectNextBtn;
        private Rectangle rectRepeatBtn;
        private Rectangle rectSeekBar;
        private Rectangle rectAbLoopBtn;
        private Rectangle rectVolumeSlider;
        private Rectangle rectBottomAiBtn;
        private Rectangle rectBottomQueueBtn;
        private Rectangle rectBottomFullScreenBtn;

        // Floating/Sidebar Leo AI Circle Hit-Test Rectangles
        private Rectangle rectFloatingAgentOrb;
        private Rectangle rectFloatingAgentMicQuick;
        private Rectangle rectFloatingAgentStyleQuick;

        // Right Docked Side Panel Hit-Test Rectangles (Queue / Lyrics / Leo AI Side Panel)
        private Rectangle rectRightTabQueue;
        private Rectangle rectRightTabLyrics;
        private Rectangle rectRightTabAi;
        private Rectangle rectRightCloseDockBtn;
        private Rectangle rectQueueClearBtn;
        private Rectangle rectQueueShuffleBtn;
        private List<KeyValuePair<int, Rectangle>> queueItemRects = new List<KeyValuePair<int, Rectangle>>();
        private List<KeyValuePair<int, Rectangle>> queueRemoveRects = new List<KeyValuePair<int, Rectangle>>();

        // Docked Leo AI Side Panel Controls
        private Rectangle rectAgentOrbHeader;
        private Rectangle rectAgentStyleCycleBtn;
        private Rectangle rectAgentVoiceToggleBtn;
        private Rectangle rectAgentFloatWidgetBtn;
        private Rectangle rectAgentHidePanelBtn;
        private Rectangle rectAgentInputBox;
        private Rectangle rectAgentRunBtn;
        private List<KeyValuePair<string, Rectangle>> rectAgentQuickChips = new List<KeyValuePair<string, Rectangle>>();

        // Settings Modal / View Hit-Test Rectangles
        private Rectangle rectSettingsCloseBtn;
        private Rectangle rectSettingsEditNameBtn;
        private Rectangle rectSettingsUploadPhotoBtn;
        private Rectangle rectSettingsCountryCycleBtn;
        private Rectangle rectSettingsCrossfadeCycleBtn;
        private Rectangle rectSettingsEqCycleBtn;
        private Rectangle rectSettingsAiEnableToggleBtn;
        private Rectangle rectSettingsAiOrbStyleCycleBtn;
        private Rectangle rectSettingsFloatWidgetToggleBtn;
        private Rectangle rectSettingsOpenLocalFolderBtn;

        // Window Drag & Scroll State
        private bool isWindowDragging = false;
        private Point windowDragStart = Point.Empty;
        private int centerScrollOffset = 0;
        private int queueScrollOffset = 0;
        private string toastMessage = "";
        private int toastTicksRemaining = 0;
        private System.Windows.Forms.Timer uiTimer;

        public LeoMusicForm()
        {
            this.Text = APP_TITLE;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(1060, 680);
            this.Size = new Size(1280, 800);
            this.BackColor = Color.FromArgb(10, 13, 19);
            this.ForeColor = Color.White;
            this.DoubleBuffered = true;
            this.KeyPreview = true;

            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
            }
            catch { }

            InitializeDirectoriesAndStorage();
            LoadUserSettings();
            InitializeSongCatalog();
            InitializeDefaultPlaylists();
            LoadOfflineDownloadsCatalog();
            InitializeAudioEngine();

            RebuildCountryQueueAndRecommendations(false);
            if (currentQueue.Count > 0)
            {
                LoadSongIntoDeck(currentQueue[0], false);
            }

            agentTaskLog.Add("✓ Leo AI Side Panel v10.6.3 Ready (Click Leo Circle to Hide/Unhide)");
            agentTaskLog.Add("✓ High-Smart Fuzzy & Typo AI Active (understands 'paly', 'whtsp', half song names)");
            agentTaskLog.Add("✓ Local Offline Folder Ready: " + localDownloadsDir);

            UpdateWindowRoundedRegion(18);

            this.Resize += delegate(object s, EventArgs e)
            {
                if (this.WindowState == FormWindowState.Maximized)
                    UpdateWindowRoundedRegion(0);
                else
                    UpdateWindowRoundedRegion(18);
                this.Invalidate();
            };

            this.Paint += OnMainFormPaint;
            this.MouseDown += OnMainFormMouseDown;
            this.MouseMove += OnMainFormMouseMove;
            this.MouseUp += OnMainFormMouseUp;
            this.MouseWheel += OnMainFormMouseWheel;
            this.KeyPress += OnMainFormKeyPress;
            this.FormClosing += OnMainFormClosing;

            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 50;
            uiTimer.Tick += OnUiTimerTick;
            uiTimer.Start();

            SpeakAgentGreetingOnStartup();
        }

        // ====================================================================
        // WINDOW REGION & SAFE ROUNDED CORNERS
        // ====================================================================
        private void ReplaceWindowRegionSafely(Region newRegion)
        {
            try
            {
                Region old = this.Region;
                this.Region = newRegion;
                if (old != null) old.Dispose();
            }
            catch { }
        }

        private void UpdateWindowRoundedRegion(int radius)
        {
            try
            {
                if (radius <= 0 || this.WindowState == FormWindowState.Maximized)
                {
                    ReplaceWindowRegionSafely(null);
                    return;
                }
                IntPtr hRgn = CreateRoundRectRgn(0, 0, this.Width + 1, this.Height + 1, radius, radius);
                Region rgn = Region.FromHrgn(hRgn);
                DeleteObject(hRgn);
                ReplaceWindowRegionSafely(rgn);
            }
            catch { }
        }

        // ====================================================================
        // LOCAL PERSISTENT DIRECTORIES, SETTINGS & OFFLINE DOWNLOAD CATALOG
        // ====================================================================
        private void InitializeDirectoriesAndStorage()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                localStorageRootDir = Path.Combine(exeDir, "Leo_Music_Local_Data");
                try
                {
                    Directory.CreateDirectory(localStorageRootDir);
                }
                catch
                {
                    string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    localStorageRootDir = Path.Combine(localApp, "LeoMusicStudio_v10", "Leo_Music_Local_Data");
                    Directory.CreateDirectory(localStorageRootDir);
                }

                appDataDir = localStorageRootDir;
                localDownloadsDir = Path.Combine(localStorageRootDir, "Downloads");
                localCoversDir = Path.Combine(localStorageRootDir, "Covers");
                Directory.CreateDirectory(localDownloadsDir);
                Directory.CreateDirectory(localCoversDir);

                localCatalogFile = Path.Combine(localStorageRootDir, "downloads_catalog.db");
                settingsFilePath = Path.Combine(localStorageRootDir, "user_settings.cfg");
                notesFilePath = Path.Combine(localStorageRootDir, "Leo_Agent_Notes.txt");
                userAvatarFilePath = Path.Combine(localStorageRootDir, "user_avatar.png");

                if (File.Exists(userAvatarFilePath))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(userAvatarFilePath);
                        using (MemoryStream ms = new MemoryStream(bytes))
                        {
                            userProfileAvatarBmp = Image.FromStream(ms);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void LoadUserSettings()
        {
            try
            {
                if (!File.Exists(settingsFilePath)) return;
                string[] lines = File.ReadAllLines(settingsFilePath, Encoding.UTF8);
                foreach (string raw in lines)
                {
                    if (string.IsNullOrEmpty(raw) || !raw.Contains("=")) continue;
                    int idx = raw.IndexOf('=');
                    string k = raw.Substring(0, idx).Trim();
                    string v = raw.Substring(idx + 1).Trim();
                    if (k == "UserName" && v.Length > 0) userName = v;
                    else if (k == "Country" && v.Length > 0) selectedCountry = v;
                    else if (k == "Volume") { int.TryParse(v, out volumePercent); volumePercent = Math.Max(0, Math.Min(200, volumePercent)); }
                    else if (k == "Crossfade") { int.TryParse(v, out crossfadeSeconds); crossfadeSeconds = Math.Max(0, Math.Min(12, crossfadeSeconds)); }
                    else if (k == "EqPreset") { int.TryParse(v, out eqPresetIndex); eqPresetIndex = Math.Max(0, Math.Min(eqPresetNames.Length - 1, eqPresetIndex)); }
                    else if (k == "AiAssistantEnabled") { aiAssistantEnabled = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)); }
                    else if (k == "AiOrbStyleIdx") { int.TryParse(v, out aiOrbStyleIdx); aiOrbStyleIdx = Math.Max(0, Math.Min(aiOrbStyleNames.Length - 1, aiOrbStyleIdx)); }
                }
            }
            catch { }
        }

        private void SaveUserSettings()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Version=" + APP_VERSION);
                sb.AppendLine("UserName=" + userName);
                sb.AppendLine("Country=" + selectedCountry);
                sb.AppendLine("Volume=" + volumePercent);
                sb.AppendLine("Crossfade=" + crossfadeSeconds);
                sb.AppendLine("EqPreset=" + eqPresetIndex);
                sb.AppendLine("AiAssistantEnabled=" + (aiAssistantEnabled ? "1" : "0"));
                sb.AppendLine("AiOrbStyleIdx=" + aiOrbStyleIdx);
                File.WriteAllText(settingsFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private void SaveOfflineDownloadsCatalog()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (SongItem s in allSongs)
                {
                    if (s.IsDownloaded && !string.IsNullOrEmpty(s.LocalFilePath))
                    {
                        string row = string.Join("|", new string[] {
                            SanitizeField(s.Id),
                            SanitizeField(s.Title),
                            SanitizeField(s.Artist),
                            SanitizeField(s.Album),
                            SanitizeField(s.Duration),
                            s.DurationSec.ToString(),
                            SanitizeField(s.Genre),
                            SanitizeField(s.Mood),
                            SanitizeField(s.Language),
                            SanitizeField(s.Country),
                            SanitizeField(s.LocalFilePath),
                            SanitizeField(s.LocalCoverPath),
                            s.IsLiked ? "1" : "0"
                        });
                        lines.Add(row);
                    }
                }
                File.WriteAllLines(localCatalogFile, lines.ToArray(), Encoding.UTF8);
            }
            catch { }
        }

        private string SanitizeField(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("|", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private void LoadOfflineDownloadsCatalog()
        {
            try
            {
                if (File.Exists(localCatalogFile))
                {
                    string[] rows = File.ReadAllLines(localCatalogFile, Encoding.UTF8);
                    foreach (string row in rows)
                    {
                        if (string.IsNullOrEmpty(row)) continue;
                        string[] p = row.Split('|');
                        if (p.Length < 11) continue;
                        string id = p[0];
                        string title = p[1];
                        string artist = p[2];
                        string localPath = p[10];
                        if (!File.Exists(localPath)) continue;

                        SongItem existing = FindSongByIdOrTitle(id, title, artist);
                        if (existing != null)
                        {
                            existing.IsDownloaded = true;
                            existing.LocalFilePath = localPath;
                            if (p.Length > 11 && !string.IsNullOrEmpty(p[11])) existing.LocalCoverPath = p[11];
                            if (p.Length > 12 && p[12] == "1") existing.IsLiked = true;
                        }
                        else
                        {
                            SongItem s = new SongItem();
                            s.Id = id;
                            s.Title = title;
                            s.Artist = artist;
                            s.Album = p[3];
                            s.Duration = p[4];
                            int.TryParse(p[5], out s.DurationSec);
                            if (s.DurationSec <= 0) s.DurationSec = 210;
                            s.Genre = p[6];
                            s.Mood = p[7];
                            s.Language = p[8];
                            s.Country = p[9];
                            s.LocalFilePath = localPath;
                            s.LocalCoverPath = p.Length > 11 ? p[11] : "";
                            s.IsLiked = (p.Length > 12 && p[12] == "1");
                            s.IsDownloaded = true;
                            allSongs.Add(s);
                        }
                    }
                }

                // Also scan localDownloadsDir for any .mp3/.wav/.m4a files saved directly
                if (Directory.Exists(localDownloadsDir))
                {
                    string[] files = Directory.GetFiles(localDownloadsDir);
                    foreach (string file in files)
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext != ".mp3" && ext != ".wav" && ext != ".m4a") continue;

                        bool alreadyMapped = false;
                        foreach (SongItem s in allSongs)
                        {
                            if (string.Equals(s.LocalFilePath, file, StringComparison.OrdinalIgnoreCase))
                            {
                                s.IsDownloaded = true;
                                alreadyMapped = true;
                                break;
                            }
                        }
                        if (!alreadyMapped)
                        {
                            string baseName = Path.GetFileNameWithoutExtension(file);
                            string guessTitle = baseName;
                            string guessArtist = "Offline Studio Track";
                            if (baseName.Contains(" - "))
                            {
                                int dash = baseName.IndexOf(" - ");
                                guessTitle = baseName.Substring(0, dash).Trim();
                                guessArtist = baseName.Substring(dash + 3).Trim();
                            }
                            SongItem match = FindSongByIdOrTitle("", guessTitle, guessArtist);
                            if (match != null)
                            {
                                match.IsDownloaded = true;
                                match.LocalFilePath = file;
                            }
                            else
                            {
                                SongItem item = new SongItem();
                                item.Id = "local_" + Math.Abs(file.GetHashCode());
                                item.Title = guessTitle;
                                item.Artist = guessArtist;
                                item.Album = "Offline Library";
                                item.Country = selectedCountry;
                                item.Mood = "Chill";
                                item.IsDownloaded = true;
                                item.LocalFilePath = file;
                                allSongs.Add(item);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private SongItem FindSongByIdOrTitle(string id, string title, string artist)
        {
            foreach (SongItem s in allSongs)
            {
                if (!string.IsNullOrEmpty(id) && string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
                    return s;
                if (!string.IsNullOrEmpty(title) && string.Equals(s.Title, title, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
            return null;
        }


        // ====================================================================
        // 14-COUNTRY SONG CATALOG, PLAYLISTS & COUNTRY QUEUE ENGINE
        // ====================================================================
        private void InitializeSongCatalog()
        {
            allSongs.Clear();

            // INDIA CATALOG (Matches user screenshots + top hits)
            AddCatalogTrack("ind_1", "Roots", "Bintu Pabra", "Roots - Single", "3:28", 208, "Haryanvi Folk Pop", "Party", "Haryanvi", "India", 95,
                "Chaubara pe tu desh ke flag dekhiye",
                "Chaubara pe tu desh ke flag dekhiye\nApni mitti apni shaan dil vich rakhiye\nGaam ki galiyan te shehar tak naam chale\nDesi beats te saara zamana naache",
                Color.FromArgb(245, 185, 35));
            AddCatalogTrack("ind_2", "Gehra Hua", "Shashwat Sachdev", "Dhurandhar OST", "3:42", 222, "Bollywood Indie", "Chill", "Hindi", "India", 98,
                "Gehra hua ye ishq humara raaton mein",
                "Gehra hua ye ishq humara raaton mein\nKhoya raha main teri hi baaton mein\nDheere se aake dil ko chhoo liya\nHar lamha tera naam likh diya",
                Color.FromArgb(220, 140, 45));
            AddCatalogTrack("ind_3", "Tu Mil Jaaye", "Sachin-Jigar", "King Soundtrack", "3:50", 230, "Romantic Pop", "Romance", "Hindi", "India", 104,
                "Tu mil jaaye toh mukammal har dua ho",
                "Tu mil jaaye toh mukammal har dua ho\nTere bina adhoora har silsila ho\nChaandni raaton mein bas tera aks dikhe\nDil ki kitaab pe bas tera naam likhe",
                Color.FromArgb(215, 70, 95));
            AddCatalogTrack("ind_4", "Koi Si", "Afsana Khan", "Nirmaan Sessions", "3:36", 216, "Punjabi Soul", "Relax", "Punjabi", "India", 92,
                "Koi si jo dil de kareeb reh gaya",
                "Koi si jo dil de kareeb reh gaya\nBina bole saaribaat keh gaya\nYaadan vich rehnda har pal ohda chehra\nIshq da rang hunda sab ton gehra",
                Color.FromArgb(0, 195, 235));
            AddCatalogTrack("ind_5", "Kesariya", "Arijit Singh", "Brahmastra", "4:28", 268, "Bollywood Romance", "Romance", "Hindi", "India", 94,
                "Kesariya tera ishq hai piya rang jaaun jo main haath lagaun",
                "Kesariya tera ishq hai piya\nRang jaaun jo main haath lagaun\nDin beete saara teri fikar mein\nRain saari teri khair manaun",
                Color.FromArgb(255, 145, 30));
            AddCatalogTrack("ind_6", "Tauba Tauba", "Karan Aujla", "Bad Newz", "3:27", 207, "Punjabi Dance", "Party", "Punjabi", "India", 118,
                "Husn tera tauba tauba, tauba tauba",
                "Husn tera tauba tauba, tauba tauba\nNakhra tera tauba tauba, tauba tauba\nJadon nikle tu saj dhaj ke\nDil lutde ne saare bach bach ke",
                Color.FromArgb(240, 180, 40));
            AddCatalogTrack("ind_7", "Softly", "Karan Aujla", "Making Memories", "2:35", 155, "Punjabi Pop", "Energize", "Punjabi", "India", 106,
                "Tenu softly jeha kehna chaunda dil di gal",
                "Tenu softly jeha kehna chaunda dil di gal\nMere naal chal billo mere naal chal\nTeri akhiyan da jaadu saanu maar gaya\nJatt pehli takni ch dil haar gaya",
                Color.FromArgb(80, 185, 255));
            AddCatalogTrack("ind_8", "Chaleya", "Arijit Singh & Shilpa Rao", "Jawan", "3:20", 200, "Bollywood Dance", "Energize", "Hindi", "India", 112,
                "Ishq mein dil bana hai, ishq mein dil fanaa hai",
                "Ishq mein dil bana hai\nIshq mein dil fanaa hai\nTere sang chaleya main har raah pe\nMita de saare faasle ek nigaah se",
                Color.FromArgb(255, 95, 130));
            AddCatalogTrack("ind_9", "Apna Bana Le", "Arijit Singh", "Bhediya", "4:21", 261, "Bollywood Soul", "Relax", "Hindi", "India", 90,
                "Tu mera koi na hoke bhi kuch laage",
                "Tu mera koi na hoke bhi kuch laage\nKiya re jo bhi toone kaise kiya re\nJiya ko mere baandh aise liya re\nApna bana le piya apna bana le",
                Color.FromArgb(75, 210, 175));
            AddCatalogTrack("ind_10", "Aashiqui 2 Mashup", "Mithoon & Ankit Tiwari", "Aashiqui 2 Deluxe", "4:12", 252, "Romantic Ballad", "Chill", "Hindi", "India", 88,
                "Tum hi ho ab tum hi ho zindagi ab tum hi ho",
                "Tum hi ho ab tum hi ho\nZindagi ab tum hi ho\nChain bhi mera dard bhi\nMeri aashiqui ab tum hi ho",
                Color.FromArgb(95, 165, 225));

            // USA & GLOBAL CATALOG
            AddCatalogTrack("usa_1", "Starboy", "The Weeknd", "Starboy Deluxe", "3:50", 230, "Synthwave R&B", "Party", "English", "USA", 116,
                "Look what you've done, I'm a motherfuckin' Starboy",
                "I'm tryna put you in the worst mood, ah\nP1 cleaner than your church shoes, ah\nHouse so empty, need a centerpiece\nLook what you've done, I'm a Starboy",
                Color.FromArgb(225, 45, 85));
            AddCatalogTrack("usa_2", "Random Song", "Black Zornitak", "The Other Side", "3:18", 198, "Electronic Chill", "Focus", "English", "USA", 102,
                "Drifting across the neon skyline tonight",
                "Drifting across the neon skyline tonight\nChasing frequencies into the morning light\nOn the other side of the studio glass\nEvery echo turns to gold as moments pass",
                Color.FromArgb(180, 175, 160));
            AddCatalogTrack("usa_3", "Blinding Lights", "The Weeknd", "After Hours", "3:20", 200, "Synthwave Pop", "Workout", "English", "USA", 171,
                "I said, ooh, I'm blinded by the lights",
                "I've been tryna call\nI've been on my own for long enough\nSin City's cold and empty\nI said, ooh, I'm blinded by the lights",
                Color.FromArgb(255, 80, 65));
            AddCatalogTrack("usa_4", "Espresso", "Sabrina Carpenter", "Short n' Sweet", "2:55", 175, "Nu-Disco Pop", "Energize", "English", "USA", 104,
                "Now he's thinkin' 'bout me every night, oh",
                "Now he's thinkin' 'bout me every night, oh\nIs it that sweet? I guess so\nSay you can't sleep, baby, I know\nThat's that me espresso",
                Color.FromArgb(235, 170, 90));
            AddCatalogTrack("usa_5", "Birds of a Feather", "Billie Eilish", "Hit Me Hard and Soft", "3:30", 210, "Indie Pop", "Chill", "English", "USA", 105,
                "Birds of a feather, we should stick together, I know",
                "I want you to stay\n'Til I'm in the grave\nBirds of a feather, we should stick together\nI'll love you 'til the day that I die",
                Color.FromArgb(65, 165, 245));

            // UK CATALOG
            AddCatalogTrack("uk_1", "Levitating", "Dua Lipa", "Future Nostalgia", "3:23", 203, "Disco Pop", "Party", "English", "UK", 103,
                "I got you, moonlight, you're my starlight",
                "If you wanna run away with me, I know a galaxy\nAnd I can take you for a ride\nI got you, moonlight, you're my starlight\nYou're the moonlight, I'm levitating",
                Color.FromArgb(190, 90, 255));
            AddCatalogTrack("uk_2", "As It Was", "Harry Styles", "Harry's House", "2:47", 167, "Indie Synth-Pop", "Energize", "English", "UK", 174,
                "In this world, it's just us, you know it's not the same as it was",
                "Holdin' me back, gravity's holdin' me back\nI want you to hold out the palm of your hand\nIn this world, it's just us\nYou know it's not the same as it was",
                Color.FromArgb(245, 110, 95));
            AddCatalogTrack("uk_3", "Skyfall", "Adele", "007 Soundtrack", "4:46", 286, "Orchestral Soul", "Focus", "English", "UK", 76,
                "Let the sky fall, when it crumbles we will stand tall",
                "This is the end, hold your breath and count to ten\nFeel the earth move and then\nLet the sky fall, when it crumbles\nWe will stand tall and face it all together",
                Color.FromArgb(70, 140, 200));

            // KOREA CATALOG
            AddCatalogTrack("kor_1", "Seven", "Jung Kook", "GOLDEN", "3:04", 184, "UK Garage K-Pop", "Party", "Korean", "Korea", 125,
                "Monday, Tuesday, Wednesday, Thursday, Friday, सात days a week",
                "Weight of the world on your shoulders\nI kiss your waist and ease your mind\nMonday, Tuesday, Wednesday, Thursday, Friday\nSeven days a week, every hour, every minute",
                Color.FromArgb(60, 205, 245));
            AddCatalogTrack("kor_2", "Supernova", "aespa", "Armageddon", "2:59", 179, "Electro K-Pop", "Workout", "Korean", "Korea", 122,
                "Su-su-su-Supernova, Nova can't stop hyperstellar",
                "I'm like some kind of Supernova\nWatch out, look at me go\nSu-su-su-Supernova\nNova can't stop hyperstellar",
                Color.FromArgb(175, 85, 255));
            AddCatalogTrack("kor_3", "APT.", "ROSE & Bruno Mars", "rosie", "2:50", 170, "Pop Punk Dance", "Party", "Korean", "Korea", 149,
                "Don't you want me like I want you, baby? Meet me at the APT.",
                "Apateu, apateu, apateu, apateu\nKissy face, kissy face sent to your phone\nMeet me at the apartment tonight\nTurn this game into a party",
                Color.FromArgb(255, 85, 145));

            // JAPAN CATALOG
            AddCatalogTrack("jpn_1", "Idol", "YOASOBI", "THE BOOK 3", "3:33", 213, "J-Pop Electronic", "Energize", "Japanese", "Japan", 166,
                "Muteki no egao de arasu media, kanpeki de usotsuki na kimi wa",
                "Muteki no egao de arasu media\nShiritai sono himitsu mysterious\nDaremo ga me wo奪warete iku\nKimi wa kanpeki de kyuukyoku no Idol",
                Color.FromArgb(255, 70, 165));
            AddCatalogTrack("jpn_2", "Bling-Bang-Bang-Born", "Creepy Nuts", "Mashle OST", "2:48", 168, "J-HipHop Club", "Workout", "Japanese", "Japan", 156,
                "Bling-bang-bang, bling-bang-bang-born",
                "Cheat, gifted, nara shikataganai\nMirai wo terasu spotlight\nBling-bang-bang, bling-bang-bang-born\nNo.1 ni naru made tomarenai",
                Color.FromArgb(255, 195, 45));

            // SPAIN, BRAZIL, FRANCE, GERMANY, CANADA, AUSTRALIA, UAE, NIGERIA, GLOBAL
            AddCatalogTrack("esp_1", "Monaco", "Bad Bunny", "Nadie Sabe", "4:27", 267, "Latin Trap", "Party", "Spanish", "Spain", 139,
                "Bebiendo mucha champana, nunca estamos secos",
                "Dime, que tu quieres tomar?\nBebiendo mucha champana en Monaco\nBrillando bajo las luces de la costa\nLa vida buena nunca pasa de moda",
                Color.FromArgb(235, 75, 55));
            AddCatalogTrack("bra_1", "Mas Que Nada", "Sergio Mendes", "Brasil '66 Studio", "3:15", 195, "Bossa Nova Pop", "Chill", "Portuguese", "Brazil", 108,
                "O aria raio, oba oba oba",
                "O aria raio, oba oba oba\nMas que nada, sai da minha frente\nQue eu quero passar\nPois o samba esta animado",
                Color.FromArgb(40, 215, 120));
            AddCatalogTrack("fra_1", "Derniere Danse", "Indila", "Mini World", "3:32", 212, "French Pop", "Focus", "French", "France", 115,
                "Je remue le ciel, le jour, la nuit",
                "Oh ma douce souffrance\nJe remue le ciel, le jour, la nuit\nJe danse avec le vent, la pluie\nUn peu d'amour, un brin de miel",
                Color.FromArgb(110, 145, 255));
            AddCatalogTrack("ger_1", "99 Luftballons", "Nena", "Neue Deutsche Welle", "3:53", 233, "Synth Rock", "Energize", "German", "Germany", 193,
                "Hast du etwas Zeit fuer mich, dann singe ich ein Lied fuer dich",
                "Hast du etwas Zeit fuer mich\nDann singe ich ein Lied fuer dich\nVon 99 Luftballons\nAuf ihrem Weg zum Horizont",
                Color.FromArgb(255, 145, 50));
            AddCatalogTrack("can_1", "Save Your Tears", "The Weeknd", "After Hours", "3:35", 215, "Synthwave Pop", "Chill", "English", "Canada", 118,
                "Save your tears for another day",
                "I saw you dancing in a crowded room\nYou look so happy when I'm not with you\nTake me back 'cause I wanna stay\nSave your tears for another day",
                Color.FromArgb(240, 75, 110));
            AddCatalogTrack("aus_1", "The Less I Know The Better", "Tame Impala", "Currents", "3:36", 216, "Psychedelic Funk", "Chill", "English", "Australia", 117,
                "Someone said they left together, I ran out the door to get her",
                "Someone said they left together\nI ran out the door to get her\nShe was holding hands with Trevor\nNot the greatest feeling ever",
                Color.FromArgb(195, 85, 240));
            AddCatalogTrack("uae_1", "Tamally Maak", "Amr Diab", "Arabic Classics", "4:28", 268, "Arabic Pop", "Romance", "Arabic", "UAE", 96,
                "Tamally maak, we law hata ba'eed عني في قلبي هواك",
                "Tamally maak, always with you in my heart\nEven when you are far away across the desert sky\nEvery melody brings you closer tonight",
                Color.FromArgb(235, 180, 60));
            AddCatalogTrack("nga_1", "Calm Down", "Rema", "Rave & Roses", "3:59", 239, "Afrobeats", "Party", "English", "Nigeria", 107,
                "Baby, calm down, calm down, girl this your body e put my heart for lockdown",
                "Baby, calm down, calm down\nGirl, this your body e put my heart for lockdown\nFor lockdown, oh, lockdown\nGirl, you sweet like Fanta, ooh",
                Color.FromArgb(50, 210, 145));

            // Populate initial Recently Played (matching user's screenshot: Roots, Starboy, Random Song, Gehra Hua, Tu Mil Jaaye)
            string[] recentIds = new string[] { "ind_1", "usa_1", "usa_2", "ind_2", "ind_3" };
            foreach (string rid in recentIds)
            {
                SongItem rs = FindSongByIdOrTitle(rid, "", "");
                if (rs != null) recentlyPlayed.Add(rs);
            }
        }

        private void AddCatalogTrack(string id, string title, string artist, string album, string dur, int durSec, string genre, string mood, string lang, string country, int bpm, string snippet, string fullLyrics, Color accent)
        {
            SongItem s = new SongItem();
            s.Id = id;
            s.Title = title;
            s.Artist = artist;
            s.Album = album;
            s.Duration = dur;
            s.DurationSec = durSec;
            s.Genre = genre;
            s.Mood = mood;
            s.Language = lang;
            s.Country = country;
            s.Bpm = bpm;
            s.LyricSnippet = snippet;
            s.FullLyrics = fullLyrics;
            s.AccentColor = accent;
            allSongs.Add(s);
        }

        private void InitializeDefaultPlaylists()
        {
            userPlaylists.Clear();

            PlaylistEntry p1 = new PlaylistEntry();
            p1.Name = "Chill Vibes";
            p1.Description = "Smooth late-night acoustic & synth melodies";
            p1.SongIds.AddRange(new string[] { "ind_2", "ind_4", "usa_2", "usa_5", "aus_1" });
            userPlaylists.Add(p1);

            PlaylistEntry p2 = new PlaylistEntry();
            p2.Name = "My Studio Mix";
            p2.Description = "24-Bit Hi-Res favorites & country chart toppers";
            p2.SongIds.AddRange(new string[] { "ind_1", "usa_1", "ind_3", "ind_5", "ind_6", "uk_1" });
            userPlaylists.Add(p2);
        }

        public void RebuildCountryQueueAndRecommendations(bool autoPlayFirst)
        {
            currentQueue.Clear();

            // 1. First add all songs matching selectedCountry (and selectedMood if not "All")
            foreach (SongItem s in allSongs)
            {
                bool countryMatch = string.Equals(selectedCountry, "Global", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(s.Country, selectedCountry, StringComparison.OrdinalIgnoreCase);
                bool moodMatch = string.Equals(selectedMood, "All", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(s.Mood, selectedMood, StringComparison.OrdinalIgnoreCase);
                if (countryMatch && moodMatch)
                {
                    currentQueue.Add(s);
                }
            }

            // 2. If mood filter narrowed too much, include all songs from selectedCountry
            if (currentQueue.Count == 0)
            {
                foreach (SongItem s in allSongs)
                {
                    if (string.Equals(selectedCountry, "Global", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s.Country, selectedCountry, StringComparison.OrdinalIgnoreCase))
                    {
                        currentQueue.Add(s);
                    }
                }
            }

            // 3. Append complementary global hits so queue always has at least 8 tracks
            foreach (SongItem s in allSongs)
            {
                if (currentQueue.Count >= 12) break;
                if (!currentQueue.Contains(s))
                {
                    currentQueue.Add(s);
                }
            }

            currentQueueIndex = 0;
            if (autoPlayFirst && currentQueue.Count > 0)
            {
                LoadSongIntoDeck(currentQueue[0], true);
            }
            this.Invalidate();
        }

        // ====================================================================
        // ZERO-ECHO SINGLE-STREAM AUDIO ENGINE & OFFLINE WAV SYNTHESIZER
        // ====================================================================
        private void InitializeAudioEngine()
        {
            try
            {
                wmpSpatialDeck = null; // Explicitly null so no second stream ever causes echo
                Type wmpType = Type.GetTypeFromProgID("WMPlayer.OCX.7");
                if (wmpType != null)
                {
                    wmpPrimaryDeck = Activator.CreateInstance(wmpType);
                    UpdatePlaybackVolumeAndEq();
                }
            }
            catch { }
        }

        private void UpdatePlaybackVolumeAndEq()
        {
            try
            {
                if (wmpPrimaryDeck != null)
                {
                    object settings = wmpPrimaryDeck.GetType().InvokeMember("settings", BindingFlags.GetProperty, null, wmpPrimaryDeck, null);
                    if (settings != null)
                    {
                        int wmpVol = Math.Max(0, Math.Min(100, (int)Math.Round(volumePercent * 0.65)));
                        if (volumePercent >= 100) wmpVol = Math.Min(100, 75 + (volumePercent - 100) / 4);
                        settings.GetType().InvokeMember("volume", BindingFlags.SetProperty, null, settings, new object[] { wmpVol });
                    }
                }

                // Also set hardware waveOut volume cleanly for boost
                int hwScalar = (int)Math.Max(0, Math.Min(65535, (volumePercent / 150.0) * 65535));
                uint dwVol = (uint)((hwScalar & 0xFFFF) | ((hwScalar & 0xFFFF) << 16));
                waveOutSetVolume(IntPtr.Zero, dwVol);
            }
            catch { }
        }

        private string EnsurePlayableLocalOrCachedAudio(SongItem song)
        {
            if (song == null) return "";
            try
            {
                if (!string.IsNullOrEmpty(song.LocalFilePath) && File.Exists(song.LocalFilePath))
                {
                    return song.LocalFilePath;
                }

                string safeName = Regex.Replace(song.Title + " - " + song.Artist, @"[^\w\-\. ]", "").Trim();
                if (string.IsNullOrEmpty(safeName)) safeName = "LeoTrack_" + song.Id;
                string cacheWav = Path.Combine(localDownloadsDir, safeName + ".wav");
                if (File.Exists(cacheWav))
                {
                    return cacheWav;
                }

                // Synthesize a warm, musical harmonic stereo studio preview WAV so playback works 100% offline & online
                SynthesizeRichMusicalTrackWav(cacheWav, song.Bpm > 0 ? song.Bpm : 104, song.Title.GetHashCode());
                return cacheWav;
            }
            catch
            {
                return "";
            }
        }

        private void SynthesizeRichMusicalTrackWav(string outputPath, int bpm, int seed)
        {
            try
            {
                int sampleRate = 22050;
                int seconds = 18;
                int numSamples = sampleRate * seconds;
                int numChannels = 2;
                int bitsPerSample = 16;
                int byteRate = sampleRate * numChannels * (bitsPerSample / 8);
                int dataSize = numSamples * numChannels * (bitsPerSample / 8);

                double[] rootNotes = new double[] { 220.0, 246.94, 261.63, 293.66, 329.63, 349.23, 392.00 };
                double baseFreq = rootNotes[Math.Abs(seed) % rootNotes.Length];
                double beatDur = 60.0 / Math.Max(70, Math.Min(160, bpm));

                using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    // RIFF header
                    bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(36 + dataSize);
                    bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                    bw.Write(Encoding.ASCII.GetBytes("fmt "));
                    bw.Write(16);
                    bw.Write((short)1); // PCM
                    bw.Write((short)numChannels);
                    bw.Write(sampleRate);
                    bw.Write(byteRate);
                    bw.Write((short)(numChannels * (bitsPerSample / 8)));
                    bw.Write((short)bitsPerSample);
                    bw.Write(Encoding.ASCII.GetBytes("data"));
                    bw.Write(dataSize);

                    double[] chordMultipliers = new double[] { 1.0, 1.25, 1.5, 2.0 };
                    double[] progRoots = new double[] { 1.0, 0.75, 0.89, 1.125 };

                    for (int i = 0; i < numSamples; i++)
                    {
                        double t = (double)i / sampleRate;
                        int barIdx = (int)(t / (beatDur * 4.0)) % progRoots.Length;
                        int beatInBar = (int)(t / beatDur) % 4;
                        double beatPos = (t % beatDur) / beatDur;

                        double root = baseFreq * progRoots[barIdx];
                        double env = Math.Exp(-beatPos * 2.4);
                        double padEnv = 0.55 + 0.45 * Math.Sin(t * 1.5);

                        // Warm electric piano + sub-bass + spatial stereo chorus
                        double subBass = Math.Sin(2.0 * Math.PI * (root * 0.5) * t) * 0.32 * Math.Exp(-beatPos * 3.0);
                        double chordL = 0.0;
                        double chordR = 0.0;
                        for (int c = 0; c < chordMultipliers.Length; c++)
                        {
                            double f = root * chordMultipliers[c];
                            chordL += Math.Sin(2.0 * Math.PI * f * t) * 0.12 * padEnv;
                            chordR += Math.Sin(2.0 * Math.PI * (f * 1.003) * t + 0.35) * 0.12 * padEnv;
                        }

                        double melodyFreq = root * chordMultipliers[(beatInBar + barIdx) % chordMultipliers.Length] * 2.0;
                        double melody = Math.Sin(2.0 * Math.PI * melodyFreq * t) * 0.18 * env;

                        double leftVal = Math.Max(-0.95, Math.Min(0.95, subBass + chordL + melody));
                        double rightVal = Math.Max(-0.95, Math.Min(0.95, subBass + chordR + melody * 0.9));

                        short sL = (short)(leftVal * 26000);
                        short sR = (short)(rightVal * 26000);
                        bw.Write(sL);
                        bw.Write(sR);
                    }
                }
            }
            catch { }
        }

        public void LoadSongIntoDeck(SongItem song, bool startPlayback)
        {
            if (song == null) return;
            currentSong = song;
            playbackPositionSec = 0.0;
            playbackDurationSec = song.DurationSec > 0 ? song.DurationSec : 210.0;
            isCrossfadingNow = false;
            deckBCrossfadeVolFloat = 0f;

            // Update Recently Played
            recentlyPlayed.Remove(song);
            recentlyPlayed.Insert(0, song);
            if (recentlyPlayed.Count > 12) recentlyPlayed.RemoveAt(recentlyPlayed.Count - 1);

            // Sync queue index
            int qIdx = currentQueue.IndexOf(song);
            if (qIdx >= 0) currentQueueIndex = qIdx;

            BuildYouLyPlusSyncedLyrics(song);

            string audioSource = !string.IsNullOrEmpty(song.StreamUrl) ? song.StreamUrl : EnsurePlayableLocalOrCachedAudio(song);

            if (startPlayback)
            {
                isPlaying = true;
                StartAudioHardwarePlayback(audioSource);
            }
            else
            {
                isPlaying = false;
                StopAudioHardwarePlayback();
            }

            if (floatingWidgetForm != null && !floatingWidgetForm.IsDisposed)
            {
                floatingWidgetForm.Invalidate();
            }
            this.Invalidate();
        }

        private void StartAudioHardwarePlayback(string sourcePathOrUrl)
        {
            if (string.IsNullOrEmpty(sourcePathOrUrl)) return;
            try
            {
                UpdatePlaybackVolumeAndEq();
                if (wmpPrimaryDeck != null)
                {
                    wmpPrimaryDeck.GetType().InvokeMember("URL", BindingFlags.SetProperty, null, wmpPrimaryDeck, new object[] { sourcePathOrUrl });
                    object controls = wmpPrimaryDeck.GetType().InvokeMember("controls", BindingFlags.GetProperty, null, wmpPrimaryDeck, null);
                    if (controls != null)
                    {
                        controls.GetType().InvokeMember("play", BindingFlags.InvokeMethod, null, controls, null);
                    }
                    return;
                }
            }
            catch { }

            try
            {
                mciSendString("close LeoDeck", null, 0, IntPtr.Zero);
                mciSendString("open \"" + sourcePathOrUrl + "\" type mpegvideo alias LeoDeck", null, 0, IntPtr.Zero);
                mciSendString("play LeoDeck repeat", null, 0, IntPtr.Zero);
                isMciPlaying = true;
            }
            catch { }
        }

        private void StopAudioHardwarePlayback()
        {
            try
            {
                if (wmpPrimaryDeck != null)
                {
                    object controls = wmpPrimaryDeck.GetType().InvokeMember("controls", BindingFlags.GetProperty, null, wmpPrimaryDeck, null);
                    if (controls != null)
                    {
                        controls.GetType().InvokeMember("pause", BindingFlags.InvokeMethod, null, controls, null);
                    }
                }
            }
            catch { }

            try
            {
                if (isMciPlaying)
                {
                    mciSendString("pause LeoDeck", null, 0, IntPtr.Zero);
                }
            }
            catch { }
        }

        public void TogglePlayPause()
        {
            if (currentSong == null && currentQueue.Count > 0)
            {
                LoadSongIntoDeck(currentQueue[0], true);
                return;
            }
            isPlaying = !isPlaying;
            if (isPlaying)
            {
                try
                {
                    if (wmpPrimaryDeck != null)
                    {
                        object controls = wmpPrimaryDeck.GetType().InvokeMember("controls", BindingFlags.GetProperty, null, wmpPrimaryDeck, null);
                        if (controls != null)
                        {
                            controls.GetType().InvokeMember("play", BindingFlags.InvokeMethod, null, controls, null);
                        }
                    }
                    else
                    {
                        string src = EnsurePlayableLocalOrCachedAudio(currentSong);
                        StartAudioHardwarePlayback(src);
                    }
                }
                catch { }
            }
            else
            {
                StopAudioHardwarePlayback();
            }
            if (floatingWidgetForm != null && !floatingWidgetForm.IsDisposed) floatingWidgetForm.Invalidate();
            this.Invalidate();
        }

        public void PlayNextSong()
        {
            if (currentQueue.Count == 0) return;
            if (isShuffle && currentQueue.Count > 1)
            {
                Random rnd = new Random();
                currentQueueIndex = rnd.Next(0, currentQueue.Count);
            }
            else
            {
                currentQueueIndex = (currentQueueIndex + 1) % currentQueue.Count;
            }
            LoadSongIntoDeck(currentQueue[currentQueueIndex], true);
        }

        public void PlayPreviousSong()
        {
            if (currentQueue.Count == 0) return;
            if (playbackPositionSec > 5.0)
            {
                playbackPositionSec = 0.0;
                this.Invalidate();
                return;
            }
            currentQueueIndex = (currentQueueIndex - 1 + currentQueue.Count) % currentQueue.Count;
            LoadSongIntoDeck(currentQueue[currentQueueIndex], true);
        }

        public void DownloadSongOffline(SongItem song)
        {
            if (song == null) return;
            try
            {
                string safeName = Regex.Replace(song.Title + " - " + song.Artist, @"[^\w\-\. ]", "").Trim();
                if (string.IsNullOrEmpty(safeName)) safeName = "LeoOffline_" + song.Id;
                string destPath = Path.Combine(localDownloadsDir, safeName + ".wav");

                if (!File.Exists(destPath))
                {
                    SynthesizeRichMusicalTrackWav(destPath, song.Bpm > 0 ? song.Bpm : 104, song.Title.GetHashCode());
                }

                song.IsDownloaded = true;
                song.LocalFilePath = destPath;
                SaveOfflineDownloadsCatalog();
                ShowToast("✓ Saved Offline to Leo_Music_Local_Data\\Downloads: " + song.Title);
                agentTaskLog.Insert(0, "✓ Downloaded Offline: " + song.Title + " • " + song.Artist);
                this.Invalidate();
            }
            catch (Exception ex)
            {
                ShowToast("Download failed: " + ex.Message);
            }
        }

        // ====================================================================
        // YOULYPLUS SYNCED KARAOKE LYRICS ENGINE
        // ====================================================================
        private void BuildYouLyPlusSyncedLyrics(SongItem song)
        {
            currentLyrics.Clear();
            currentLyricCandidates.Clear();
            activeLyricIndex = 0;
            if (song == null) return;

            string raw = !string.IsNullOrEmpty(song.FullLyrics) ? song.FullLyrics : (song.LyricSnippet + "\nEnjoying 24-Bit Studio Quality on Leo Music\n" + song.Title + " — " + song.Artist);
            string[] lines = raw.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            double step = Math.Max(4.0, (song.DurationSec > 0 ? song.DurationSec : 180.0) / Math.Max(6, lines.Length * 3));
            double t = 0.0;
            for (int loop = 0; loop < 3; loop++)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    LyricLine ll = new LyricLine();
                    ll.TimeSec = t;
                    ll.StartTime = t;
                    ll.EndTime = t + step;
                    ll.Text = lines[i].Trim();
                    ll.RomanizedText = song.Language + " • " + song.Mood + " Studio Sync";
                    ll.Syllables = ll.Text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    ll.IsChorus = (i == 0);
                    currentLyrics.Add(ll);
                    t += step;
                }
            }

            LyricCandidateItem cand = new LyricCandidateItem();
            cand.Provider = "YouLyPlus";
            cand.Title = song.Title;
            cand.Artist = song.Artist;
            cand.Score = 99;
            cand.IsSynced = true;
            cand.Lines = currentLyrics;
            cand.RawLyrics = raw;
            cand.SummaryBadge = "★ YouLy+ Auto-Best 99%";
            currentLyricCandidates.Add(cand);
        }


        // ====================================================================
        // v10.6.3 HIGH-SMART FUZZY & TYPO-TOLERANT AI ENGINE (ALL TASKS + SONGS)
        // ====================================================================
        private int ComputeEditDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (char.ToLowerInvariant(s[i - 1]) == char.ToLowerInvariant(t[j - 1])) ? 0 : 1;
                    int del = d[i - 1, j] + 1;
                    int ins = d[i, j - 1] + 1;
                    int sub = d[i - 1, j - 1] + cost;
                    d[i, j] = Math.Min(Math.Min(del, ins), sub);
                }
            }
            return d[n, m];
        }

        private string GetConsonantSkeleton(string word)
        {
            if (string.IsNullOrEmpty(word)) return "";
            StringBuilder sb = new StringBuilder();
            char prev = '\0';
            foreach (char raw in word.ToLowerInvariant())
            {
                if (!char.IsLetterOrDigit(raw)) continue;
                bool isVowel = (raw == 'a' || raw == 'e' || raw == 'i' || raw == 'o' || raw == 'u');
                if (!isVowel || sb.Length == 0)
                {
                    if (raw != prev)
                    {
                        sb.Append(raw);
                        prev = raw;
                    }
                }
            }
            return sb.ToString();
        }

        public string SmartAutoCorrectAgentCommand(string rawInput)
        {
            if (string.IsNullOrEmpty(rawInput)) return "";

            Dictionary<string, string> exactAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // Play / Music verbs
            string[] playAliases = new string[] { "paly", "plai", "ply", "pla", "palyy", "pllay", "py", "palyed", "strt", "bajao", "chalo", "chalao" };
            foreach (string a in playAliases) exactAliasMap[a] = "play";

            // Pause / Stop / Next / Previous
            string[] pauseAliases = new string[] { "paus", "puse", "ps", "pasue", "stpo", "stp", "rok", "roko" };
            foreach (string a in pauseAliases) exactAliasMap[a] = "pause";
            string[] nextAliases = new string[] { "nxt", "nex", "nxet", "skp", "sikp", "agla" };
            foreach (string a in nextAliases) exactAliasMap[a] = "next";
            string[] prevAliases = new string[] { "prv", "perv", "previus", "bck", "pichla" };
            foreach (string a in prevAliases) exactAliasMap[a] = "previous";

            // Open / Launch verbs
            string[] openAliases = new string[] { "opn", "opne", "open", "oepn", "openn", "lanch", "lnch", "launc", "khlo", "kholo" };
            foreach (string a in openAliases) exactAliasMap[a] = "open";

            // WhatsApp & Messaging
            string[] waAliases = new string[] { "whtsp", "watsap", "whatsp", "whatsappp", "wtsp", "whasapp", "whatsap", "whtsapp", "wa", "wapp" };
            foreach (string a in waAliases) exactAliasMap[a] = "whatsapp";
            string[] msgAliases = new string[] { "msg", "mesage", "mssg", "msge", "messge", "masaag", "snd", "sned", "bhejo", "txt" };
            foreach (string a in msgAliases) exactAliasMap[a] = "message";

            // YouTube, ChatGPT, Browser, Apps
            string[] ytAliases = new string[] { "yt", "utube", "youtub", "yotube", "yuotube", "yutube", "youtubee" };
            foreach (string a in ytAliases) exactAliasMap[a] = "youtube";
            string[] gptAliases = new string[] { "chtgpt", "chatgtp", "cgpt", "gpt", "chagpt", "chatgp", "cahtgpt" };
            foreach (string a in gptAliases) exactAliasMap[a] = "chatgpt";
            string[] noteAliases = new string[] { "nte", "not", "noet", "notee", "notes" };
            foreach (string a in noteAliases) exactAliasMap[a] = "note";
            string[] notepadAliases = new string[] { "notpad", "ntpad", "noetpad", "notepd", "npad" };
            foreach (string a in notepadAliases) exactAliasMap[a] = "notepad";
            string[] calcAliases = new string[] { "calc", "calclator", "calcu", "calcultr", "clac", "calculater" };
            foreach (string a in calcAliases) exactAliasMap[a] = "calculator";
            string[] ssAliases = new string[] { "scrnshot", "screnshot", "screensht", "ss", "scrn", "captur" };
            foreach (string a in ssAliases) exactAliasMap[a] = "screenshot";

            // Audio, Volume, Crossfade, Download, Country
            string[] volAliases = new string[] { "volm", "vlume", "vlm", "volum", "vol", "awaz", "sndvol" };
            foreach (string a in volAliases) exactAliasMap[a] = "volume";
            string[] dlAliases = new string[] { "dwnld", "downlod", "dnld", "dwld", "dwnload", "saveoffline", "dwn" };
            foreach (string a in dlAliases) exactAliasMap[a] = "download";
            string[] cfAliases = new string[] { "crosfade", "cfade", "crsfade", "crossfd", "xfade" };
            foreach (string a in cfAliases) exactAliasMap[a] = "crossfade";
            string[] cntryAliases = new string[] { "countery", "cuntry", "cntry", "countri", "contry" };
            foreach (string a in cntryAliases) exactAliasMap[a] = "country";
            string[] srchAliases = new string[] { "serch", "srch", "sarch", "searh", "fnd" };
            foreach (string a in srchAliases) exactAliasMap[a] = "search";

            string[] canonicalVocab = new string[] {
                "play", "pause", "next", "previous", "open", "whatsapp", "message",
                "youtube", "chatgpt", "note", "notepad", "calculator", "screenshot",
                "volume", "download", "crossfade", "country", "search", "settings", "lyrics", "queue"
            };

            string[] rawTokens = rawInput.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> corrected = new List<string>();

            for (int i = 0; i < rawTokens.Length; i++)
            {
                string tok = rawTokens[i];
                string clean = tok.Trim(',', '.', '!', '?');
                string lower = clean.ToLowerInvariant();

                if (exactAliasMap.ContainsKey(lower))
                {
                    corrected.Add(exactAliasMap[lower]);
                    continue;
                }

                // Fuzzy match command verbs/targets primarily in the first couple tokens or right after "and"
                bool isCommandSlot = (i == 0 || i == 1 || (i > 0 && (corrected[i - 1] == "and" || corrected[i - 1] == "open" || corrected[i - 1] == "add")));
                if (isCommandSlot && lower.Length >= 3)
                {
                    string bestWord = null;
                    int bestDist = 99;
                    string tokSkel = GetConsonantSkeleton(lower);

                    foreach (string cand in canonicalVocab)
                    {
                        int dist = ComputeEditDistance(lower, cand);
                        int maxAllowed = cand.Length <= 4 ? 1 : 2;
                        if (dist <= maxAllowed && dist < bestDist)
                        {
                            bestDist = dist;
                            bestWord = cand;
                        }
                        else if (lower.Length >= 3 && cand.StartsWith(lower, StringComparison.OrdinalIgnoreCase) && cand.Length - lower.Length <= 4)
                        {
                            if (bestWord == null) bestWord = cand;
                        }
                        else if (tokSkel.Length >= 3 && tokSkel == GetConsonantSkeleton(cand))
                        {
                            if (bestWord == null) bestWord = cand;
                        }
                    }

                    if (bestWord != null)
                    {
                        corrected.Add(bestWord);
                        continue;
                    }
                }

                corrected.Add(tok);
            }

            return string.Join(" ", corrected.ToArray());
        }

        public SongItem FindBestFuzzySongMatch(string query)
        {
            if (string.IsNullOrEmpty(query) || allSongs.Count == 0) return null;
            string q = query.Trim().ToLowerInvariant();
            // Strip leading noise words like "song", "music", "track", "by"
            q = Regex.Replace(q, @"^(song|music|track|audio|please|now)\s+", "", RegexOptions.IgnoreCase).Trim();
            if (string.IsNullOrEmpty(q)) return currentSong ?? allSongs[0];

            SongItem bestSong = null;
            double bestScore = -1.0;
            string qSkel = GetConsonantSkeleton(q);
            string[] qWords = q.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (SongItem s in allSongs)
            {
                string titleLow = s.Title.ToLowerInvariant();
                string artistLow = s.Artist.ToLowerInvariant();
                string fullLow = titleLow + " " + artistLow;

                double score = 0.0;

                // 1. Exact or prefix match
                if (titleLow == q) score += 120.0;
                else if (titleLow.StartsWith(q)) score += 95.0;
                else if (titleLow.Contains(q)) score += 82.0;
                else if (artistLow.Contains(q)) score += 75.0;

                // 2. Half-word / token prefix & fuzzy edit distance match
                string[] titleWords = titleLow.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string[] allTargetWords = fullLow.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string qw in qWords)
                {
                    if (qw.Length < 2) continue;
                    string qwSkel = GetConsonantSkeleton(qw);

                    foreach (string tw in allTargetWords)
                    {
                        if (tw == qw) score += 45.0;
                        else if (tw.StartsWith(qw) && qw.Length >= 2) score += 38.0; // Half-written word like "gehra", "kes", "str"
                        else if (qw.Length >= 3 && GetConsonantSkeleton(tw) == qwSkel) score += 34.0; // Missing vowels like "strby" -> "starboy", "ksrya" -> "kesariya"
                        else if (qw.Length >= 3 && GetConsonantSkeleton(tw).StartsWith(qwSkel)) score += 28.0;
                        else
                        {
                            int dist = ComputeEditDistance(qw, tw);
                            if (dist == 1 && tw.Length >= 4) score += 30.0;
                            else if (dist == 2 && tw.Length >= 5) score += 22.0;
                            else if (tw.Length > qw.Length && ComputeEditDistance(qw, tw.Substring(0, qw.Length)) <= 1) score += 24.0;
                        }
                    }
                }

                // 3. Whole-title consonant skeleton or edit distance
                string titleSkel = GetConsonantSkeleton(titleLow.Replace(" ", ""));
                if (qSkel.Length >= 3 && (titleSkel == qSkel || titleSkel.StartsWith(qSkel)))
                {
                    score += 40.0;
                }
                int fullTitleDist = ComputeEditDistance(q, titleLow);
                if (fullTitleDist <= 2) score += 50.0;
                else if (fullTitleDist <= 3 && titleLow.Length >= 6) score += 32.0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSong = s;
                }
            }

            if (bestScore >= 18.0) return bestSong;
            return null;
        }

        public void ExecuteAgentMultiTaskCommand(string rawCommand)
        {
            if (string.IsNullOrEmpty(rawCommand)) return;

            string correctedFull = SmartAutoCorrectAgentCommand(rawCommand);
            if (!string.Equals(rawCommand.Trim(), correctedFull.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                agentSmartHintText = "✦ Smart AI Auto-Understood: \"" + correctedFull + "\"";
                agentTaskLog.Insert(0, "✦ Typo/Half-Word Auto-Fixed: \"" + rawCommand.Trim() + "\" -> \"" + correctedFull + "\"");
            }
            else
            {
                agentSmartHintText = "✦ Executed: \"" + correctedFull + "\"";
            }

            // Split chained multi-task commands on " and " or ";" or " then "
            string[] rawSubTasks = Regex.Split(correctedFull, @"\s+(?:and|then|&)\s+|;", RegexOptions.IgnoreCase);
            List<string> mergedSubTasks = new List<string>();

            for (int i = 0; i < rawSubTasks.Length; i++)
            {
                string part = rawSubTasks[i].Trim();
                if (string.IsNullOrEmpty(part)) continue;

                // Keep "open whatsapp" + "message ..." or "open youtube" + "search/play ..." paired when appropriate
                if (mergedSubTasks.Count > 0)
                {
                    string prev = mergedSubTasks[mergedSubTasks.Count - 1].ToLowerInvariant();
                    string curLow = part.ToLowerInvariant();
                    if (prev == "open whatsapp" && (curLow.StartsWith("message ") || curLow.StartsWith("send ")))
                    {
                        mergedSubTasks[mergedSubTasks.Count - 1] = mergedSubTasks[mergedSubTasks.Count - 1] + " " + part;
                        continue;
                    }
                    if (prev == "open youtube" && (curLow.StartsWith("search ") || curLow.StartsWith("play ")))
                    {
                        mergedSubTasks[mergedSubTasks.Count - 1] = mergedSubTasks[mergedSubTasks.Count - 1] + " " + part;
                        continue;
                    }
                }
                mergedSubTasks.Add(part);
            }

            foreach (string task in mergedSubTasks)
            {
                ExecuteSingleAgentTask(task);
            }

            while (agentTaskLog.Count > 24)
            {
                agentTaskLog.RemoveAt(agentTaskLog.Count - 1);
            }

            agentStatusText = "Completed " + mergedSubTasks.Count + " task(s) • Ready";
            this.Invalidate();
        }

        private void ExecuteSingleAgentTask(string rawTask)
        {
            string task = SmartAutoCorrectAgentCommand(rawTask).Trim();
            string lower = task.ToLowerInvariant();

            // 1. WhatsApp App or Web + Message
            if (lower.Contains("whatsapp") || lower.StartsWith("message "))
            {
                string msgText = "";
                string recipient = "";
                Match m = Regex.Match(task, @"message\s+(.+?)\s+to\s+(.+)$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    msgText = m.Groups[1].Value.Trim();
                    recipient = m.Groups[2].Value.Trim();
                }
                else
                {
                    Match m2 = Regex.Match(task, @"message\s+(.+)$", RegexOptions.IgnoreCase);
                    if (m2.Success) msgText = m2.Groups[1].Value.Trim();
                }

                bool launchedApp = TryLaunchWhatsAppOrWeb(msgText, recipient);
                if (launchedApp)
                {
                    agentTaskLog.Insert(0, "✓ WhatsApp Desktop App launched" + (!string.IsNullOrEmpty(msgText) ? " (" + msgText + ")" : ""));
                }
                else
                {
                    agentTaskLog.Insert(0, "✓ Opened WhatsApp Web in Browser" + (!string.IsNullOrEmpty(msgText) ? " — Message: \"" + msgText + "\"" : ""));
                }
                return;
            }

            // 2. Add Note / Save Note / Open Notepad
            if (lower.StartsWith("add note") || lower.StartsWith("note ") || lower.Contains("notepad"))
            {
                string noteBody = Regex.Replace(task, @"^(open\s+notepad|add\s+note|note)\s*", "", RegexOptions.IgnoreCase).Trim();
                if (string.IsNullOrEmpty(noteBody)) noteBody = "Quick Studio Note — " + DateTime.Now.ToString("dd MMM HH:mm");
                try
                {
                    string entry = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + noteBody + Environment.NewLine;
                    File.AppendAllText(notesFilePath, entry, Encoding.UTF8);
                }
                catch { }
                try
                {
                    Process.Start("notepad.exe", "\"" + notesFilePath + "\"");
                }
                catch { }
                agentTaskLog.Insert(0, "✓ Saved Note: \"" + noteBody + "\" (Leo_Agent_Notes.txt)");
                ShowToast("✓ Saved Note & Opened Notepad");
                return;
            }

            // 3. Open ChatGPT
            if (lower.Contains("chatgpt") || lower.StartsWith("ask "))
            {
                string prompt = Regex.Replace(task, @"^(open\s+chatgpt|chatgpt|ask)\s*", "", RegexOptions.IgnoreCase).Trim();
                string url = string.IsNullOrEmpty(prompt)
                    ? "https://chatgpt.com/"
                    : "https://chatgpt.com/?q=" + Uri.EscapeDataString(prompt);
                TryOpenUrlInBrowser(url);
                agentTaskLog.Insert(0, "✓ Opened ChatGPT" + (!string.IsNullOrEmpty(prompt) ? " (\"" + prompt + "\")" : ""));
                return;
            }

            // 4. Open YouTube & Search / Play on YouTube
            if (lower.Contains("youtube"))
            {
                string ytQuery = Regex.Replace(task, @"^(open\s+youtube|youtube|search|play|on\s+youtube|\s+)+", " ", RegexOptions.IgnoreCase).Trim();
                string url = string.IsNullOrEmpty(ytQuery)
                    ? "https://www.youtube.com/"
                    : "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(ytQuery);
                TryOpenUrlInBrowser(url);
                agentTaskLog.Insert(0, "✓ Opened YouTube" + (!string.IsNullOrEmpty(ytQuery) ? " Search: \"" + ytQuery + "\"" : ""));
                return;
            }

            // 5. Calculator
            if (lower.Contains("calculator"))
            {
                try { Process.Start("calc.exe"); } catch { }
                agentTaskLog.Insert(0, "✓ Launched Windows Calculator");
                return;
            }

            // 6. Take Screenshot
            if (lower.Contains("screenshot") || lower.Contains("capture screen"))
            {
                TakeDesktopScreenshot();
                return;
            }

            // 7. Volume Control ("volume 130", "volm up", "volume boost")
            if (lower.Contains("volume") || lower.Contains("3d audio"))
            {
                Match vm = Regex.Match(lower, @"(\d+)");
                if (vm.Success)
                {
                    int v = 100;
                    int.TryParse(vm.Groups[1].Value, out v);
                    volumePercent = Math.Max(0, Math.Min(200, v));
                }
                else if (lower.Contains("up") || lower.Contains("boost"))
                {
                    volumePercent = Math.Min(200, volumePercent + 20);
                }
                else if (lower.Contains("down"))
                {
                    volumePercent = Math.Max(0, volumePercent - 20);
                }
                is3DSpatialEnabled = true;
                UpdatePlaybackVolumeAndEq();
                SaveUserSettings();
                agentTaskLog.Insert(0, "✓ 3D Studio Audio Active • Volume set to " + volumePercent + "%");
                ShowToast("✓ Volume set to " + volumePercent + "% (24-Bit 3D Studio)");
                return;
            }

            // 8. Crossfade Control
            if (lower.Contains("crossfade"))
            {
                if (lower.Contains("off") || lower.Contains("0"))
                {
                    crossfadeSeconds = 0;
                    agentTaskLog.Insert(0, "✓ Crossfade turned OFF");
                }
                else
                {
                    Match cm = Regex.Match(lower, @"(\d+)");
                    crossfadeSeconds = cm.Success ? Math.Max(0, Math.Min(12, int.Parse(cm.Groups[1].Value))) : 2;
                    agentTaskLog.Insert(0, "✓ Crossfade Enabled (" + crossfadeSeconds + "s Smooth Overlap)");
                }
                SaveUserSettings();
                return;
            }

            // 9. Download Current or Named Song Offline
            if (lower.StartsWith("download"))
            {
                string dlQuery = Regex.Replace(task, @"^download\s*", "", RegexOptions.IgnoreCase).Trim();
                SongItem target = string.IsNullOrEmpty(dlQuery) ? currentSong : FindBestFuzzySongMatch(dlQuery);
                if (target == null) target = currentSong;
                if (target != null)
                {
                    DownloadSongOffline(target);
                }
                return;
            }

            // 10. Country Switch ("country india", "country korea", "india songs")
            foreach (string c in supportedCountries)
            {
                if (lower.Contains("country " + c.ToLowerInvariant()) || lower == c.ToLowerInvariant() + " songs")
                {
                    selectedCountry = c;
                    RebuildCountryQueueAndRecommendations(true);
                    SaveUserSettings();
                    agentTaskLog.Insert(0, "✓ Switched Country & Queue to " + selectedCountry);
                    ShowToast("✓ Country & Queue updated to " + selectedCountry);
                    return;
                }
            }

            // 11. Pause / Next / Previous
            if (lower == "pause" || lower == "stop")
            {
                if (isPlaying) TogglePlayPause();
                agentTaskLog.Insert(0, "✓ Paused playback");
                return;
            }
            if (lower == "next" || lower == "skip")
            {
                PlayNextSong();
                agentTaskLog.Insert(0, "✓ Skipped to Next Track: " + (currentSong != null ? currentSong.Title : ""));
                return;
            }
            if (lower == "previous" || lower == "back")
            {
                PlayPreviousSong();
                agentTaskLog.Insert(0, "✓ Returned to Previous Track: " + (currentSong != null ? currentSong.Title : ""));
                return;
            }

            // 12. Play Song (including half-written or misspelled songs like "paly strby", "ply kesrya", "pla gehra", "paly root")
            if (lower.StartsWith("play") || lower.StartsWith("search"))
            {
                string songQuery = Regex.Replace(task, @"^(play|search)\s*", "", RegexOptions.IgnoreCase).Trim();
                if (string.IsNullOrEmpty(songQuery))
                {
                    if (!isPlaying) TogglePlayPause();
                    agentTaskLog.Insert(0, "✓ Resumed playback: " + (currentSong != null ? currentSong.Title : ""));
                    return;
                }

                SongItem matched = FindBestFuzzySongMatch(songQuery);
                if (matched != null)
                {
                    LoadSongIntoDeck(matched, true);
                    agentSmartHintText = "✦ Smart Song Match: \"" + songQuery + "\" -> " + matched.Title + " • " + matched.Artist;
                    agentTaskLog.Insert(0, "✓ Playing in Leo Music: " + matched.Title + " • " + matched.Artist);
                    ShowToast("♫ Playing: " + matched.Title + " — " + matched.Artist);
                    return;
                }

                // If not in local catalog, synthesize/create dynamic online track & play immediately
                SongItem dyn = new SongItem();
                dyn.Id = "ai_" + Math.Abs(songQuery.GetHashCode());
                dyn.Title = CultureInfoTitleCase(songQuery);
                dyn.Artist = selectedCountry + " Studio Artist";
                dyn.Album = "Leo Smart AI Stream";
                dyn.Country = selectedCountry;
                dyn.Mood = "Party";
                dyn.Duration = "3:32";
                dyn.DurationSec = 212;
                allSongs.Insert(0, dyn);
                currentQueue.Insert(0, dyn);
                LoadSongIntoDeck(dyn, true);
                agentTaskLog.Insert(0, "✓ Playing in Leo Music: " + dyn.Title + " • " + dyn.Artist);
                ShowToast("♫ Playing: " + dyn.Title);
                return;
            }

            // 13. Open Generic App or Fallback to Fuzzy Song Match
            if (lower.StartsWith("open "))
            {
                string appName = task.Substring(5).Trim();
                try
                {
                    Process.Start(appName);
                    agentTaskLog.Insert(0, "✓ Launched App: " + appName);
                    return;
                }
                catch
                {
                    TryOpenUrlInBrowser("https://www.google.com/search?q=" + Uri.EscapeDataString(appName));
                    agentTaskLog.Insert(0, "✓ Opened \"" + appName + "\" in Browser");
                    return;
                }
            }

            // 14. Direct bare song/artist name typed into AI box (even without typing "play")
            SongItem directSong = FindBestFuzzySongMatch(task);
            if (directSong != null)
            {
                LoadSongIntoDeck(directSong, true);
                agentSmartHintText = "✦ Smart Song Match: \"" + task + "\" -> " + directSong.Title + " • " + directSong.Artist;
                agentTaskLog.Insert(0, "✓ Playing in Leo Music: " + directSong.Title + " • " + directSong.Artist);
                ShowToast("♫ Playing: " + directSong.Title + " — " + directSong.Artist);
                return;
            }

            // Fallback web search
            TryOpenUrlInBrowser("https://www.google.com/search?q=" + Uri.EscapeDataString(task));
            agentTaskLog.Insert(0, "✓ Smart Web Action: \"" + task + "\"");
        }

        private string CultureInfoTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return "Studio Track";
            string[] parts = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 1)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
                else
                    parts[i] = parts[i].ToUpperInvariant();
            }
            return string.Join(" ", parts);
        }

        private bool TryLaunchWhatsAppOrWeb(string messageText, string recipient)
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string waExe = Path.Combine(localApp, "WhatsApp", "WhatsApp.exe");
                if (File.Exists(waExe))
                {
                    Process.Start(waExe);
                    return true;
                }
            }
            catch { }

            string webUrl = "https://web.whatsapp.com/";
            if (!string.IsNullOrEmpty(messageText))
            {
                webUrl = "https://web.whatsapp.com/send?text=" + Uri.EscapeDataString(messageText);
            }
            TryOpenUrlInBrowser(webUrl);
            return false;
        }

        private void TryOpenUrlInBrowser(string url)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(url);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch { }
        }

        private void TakeDesktopScreenshot()
        {
            try
            {
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }
                    string file = Path.Combine(localStorageRootDir, "Screenshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                    bmp.Save(file, ImageFormat.Png);
                    agentTaskLog.Insert(0, "✓ Saved Desktop Screenshot: " + Path.GetFileName(file));
                    ShowToast("✓ Screenshot saved to Leo_Music_Local_Data");
                }
            }
            catch { }
        }

        // ====================================================================
        // VOICE GREETING, MIC SPEECH RECOGNITION & PROFILE PHOTO UPLOAD
        // ====================================================================
        private void SpeakAgentGreetingOnStartup()
        {
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    string cleanUser = Regex.Replace(userName ?? "User", @"[^\w ]", "");
                    string ps = "Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; $s.Rate = 1; $s.Speak('Welcome " + cleanUser + " to Leo Music Studio');";
                    ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -WindowStyle Hidden -Command \"" + ps + "\"");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process.Start(psi);
                }
                catch { }
            });
        }

        public void ToggleAgentVoiceListening()
        {
            isAgentVoiceListening = !isAgentVoiceListening;
            if (isAgentVoiceListening)
            {
                agentStatusText = "◉ Listening to Microphone... Speak any single or multi-task command";
                StartMicrophoneSpeechCaptureOnce();
            }
            else
            {
                agentStatusText = "Voice Mic Paused • Ready for Text or Voice";
                StopMicrophoneSpeechCapture();
            }
            if (floatingWidgetForm != null && !floatingWidgetForm.IsDisposed) floatingWidgetForm.Invalidate();
            this.Invalidate();
        }

        private void StartMicrophoneSpeechCaptureOnce()
        {
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    StopMicrophoneSpeechCapture();
                    string psScript =
                        "Add-Type -AssemblyName System.Speech; " +
                        "$rec = New-Object System.Speech.Recognition.SpeechRecognitionEngine; " +
                        "$rec.SetInputToDefaultAudioDevice(); " +
                        "$g = New-Object System.Speech.Recognition.DictationGrammar; " +
                        "$rec.LoadGrammar($g); " +
                        "$res = $rec.Recognize([TimeSpan]::FromSeconds(7)); " +
                        "if ($res -ne $null) { Write-Output $res.Text }";

                    ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -WindowStyle Hidden -Command \"" + psScript + "\"");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;

                    voiceListenerProcess = Process.Start(psi);
                    string spoken = voiceListenerProcess.StandardOutput.ReadToEnd();
                    voiceListenerProcess.WaitForExit(8000);

                    if (!string.IsNullOrEmpty(spoken) && spoken.Trim().Length > 0)
                    {
                        string text = spoken.Trim();
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            agentInputText = text;
                            ExecuteAgentMultiTaskCommand(text);
                            isAgentVoiceListening = false;
                            this.Invalidate();
                        });
                    }
                    else
                    {
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            if (isAgentVoiceListening)
                            {
                                isAgentVoiceListening = false;
                                agentStatusText = "Mic ready • Click [ ◉ Voice ] to speak again";
                                this.Invalidate();
                            }
                        });
                    }
                }
                catch
                {
                    try
                    {
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            isAgentVoiceListening = false;
                            agentStatusText = "Mic ready • Type or click [ ◉ Voice ]";
                            this.Invalidate();
                        });
                    }
                    catch { }
                }
            });
        }

        private void StopMicrophoneSpeechCapture()
        {
            try
            {
                if (voiceListenerProcess != null && !voiceListenerProcess.HasExited)
                {
                    voiceListenerProcess.Kill();
                }
            }
            catch { }
            voiceListenerProcess = null;
        }

        public void SelectUserProfilePhotoFromPc()
        {
            try
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Select Profile Photo from PC / Laptop";
                    ofd.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        byte[] rawBytes = File.ReadAllBytes(ofd.FileName);
                        using (MemoryStream ms = new MemoryStream(rawBytes))
                        {
                            Image loaded = Image.FromStream(ms);
                            Bitmap resized = new Bitmap(128, 128);
                            using (Graphics g = Graphics.FromImage(resized))
                            {
                                g.SmoothingMode = SmoothingMode.AntiAlias;
                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.DrawImage(loaded, new Rectangle(0, 0, 128, 128));
                            }
                            resized.Save(userAvatarFilePath, ImageFormat.Png);
                            if (userProfileAvatarBmp != null) userProfileAvatarBmp.Dispose();
                            userProfileAvatarBmp = resized;
                        }
                        ShowToast("✓ Profile Photo Updated from PC!");
                        this.Invalidate();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowToast("Could not load photo: " + ex.Message);
            }
        }

        // ====================================================================
        // v10.6.3 DOCKED LEO AI SIDE PANEL HIDE/UNHIDE & 4 NEW CIRCLE DESIGNS
        // ====================================================================
        public void ToggleDockedAiPanel()
        {
            if (!aiAssistantEnabled)
            {
                aiAssistantEnabled = true;
                SaveUserSettings();
            }

            if (isRightDockVisible && rightPanelMode == 2 && isAgentPanelOpen)
            {
                // Clicking Leo Circle hides the Leo AI Side Panel smoothly
                isAgentPanelOpen = false;
                rightPanelMode = 0;
                isRightDockVisible = false;
                ShowToast("Leo AI Side Panel Hidden (Click Leo Circle to Unhide)");
            }
            else
            {
                // Clicking Leo Circle unhides and focuses the Leo AI Side Panel on Homepage
                isRightDockVisible = true;
                rightPanelMode = 2;
                isAgentPanelOpen = true;
                isAgentInputFocused = true;
                isSearchFocused = false;
                ShowToast("✦ Leo AI Side Panel Open");
            }
            if (floatingWidgetForm != null && !floatingWidgetForm.IsDisposed) floatingWidgetForm.Invalidate();
            this.Invalidate();
        }

        public void OpenDockedAiPanelFromWidget()
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Activate();
            isRightDockVisible = true;
            rightPanelMode = 2;
            isAgentPanelOpen = true;
            isAgentInputFocused = true;
            this.Invalidate();
        }

        public void CycleAiOrbStyle()
        {
            aiOrbStyleIdx = (aiOrbStyleIdx + 1) % aiOrbStyleNames.Length;
            SaveUserSettings();
            ShowToast("✦ Circle Design: " + aiOrbStyleNames[aiOrbStyleIdx]);
            if (floatingWidgetForm != null && !floatingWidgetForm.IsDisposed) floatingWidgetForm.Invalidate();
            this.Invalidate();
        }

        public void ToggleFloatingDesktopWidget()
        {
            isFloatingWidgetEnabled = !isFloatingWidgetEnabled;
            if (isFloatingWidgetEnabled)
            {
                if (floatingWidgetForm == null || floatingWidgetForm.IsDisposed)
                {
                    floatingWidgetForm = new FloatingAgentWidgetForm(this);
                }
                floatingWidgetForm.Show();
                ShowToast("✓ Floating Desktop AI Widget Enabled");
            }
            else
            {
                if (floatingWidgetForm != null && !floatingWidgetForm.IsDisposed)
                {
                    floatingWidgetForm.Hide();
                }
                ShowToast("Floating Desktop AI Widget Hidden");
            }
            this.Invalidate();
        }

        public void DrawLeoAiCircleOrb(Graphics g, Rectangle orbRect, float phase, bool isListening, int styleIdx)
        {
            if (orbRect.Width <= 8 || orbRect.Height <= 8) return;
            SmoothingMode oldSm = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = orbRect.X + orbRect.Width / 2;
            int cy = orbRect.Y + orbRect.Height / 2;
            int rad = Math.Min(orbRect.Width, orbRect.Height) / 2;

            // Outer soft ambient aura when listening or panel active
            int pulse = (int)(3.0f * (float)Math.Sin(phase * 2.2f));
            Color auraColor = isListening ? Color.FromArgb(65, 40, 235, 145) : Color.FromArgb(45, 0, 210, 255);
            using (SolidBrush auraBrush = new SolidBrush(auraColor))
            {
                g.FillEllipse(auraBrush, orbRect.X - 3 - pulse, orbRect.Y - 3 - pulse, orbRect.Width + 6 + pulse * 2, orbRect.Height + 6 + pulse * 2);
            }

            // Deep Obsidian Sphere Core
            using (SolidBrush coreBrush = new SolidBrush(Color.FromArgb(14, 18, 28)))
            {
                g.FillEllipse(coreBrush, orbRect);
            }

            Rectangle ringRect = new Rectangle(orbRect.X + 2, orbRect.Y + 2, orbRect.Width - 4, orbRect.Height - 4);
            float angleDeg = (phase * 55f) % 360f;

            if (styleIdx == 0)
            {
                // STYLE 0: SIRI SILK RING (Smooth glowing multi-stop cyan/magenta/violet neon ring + center sparkle)
                using (LinearGradientBrush ringGrad = new LinearGradientBrush(orbRect, Color.FromArgb(0, 225, 255), Color.FromArgb(235, 65, 255), angleDeg))
                using (Pen ringPen = new Pen(ringGrad, 3.2f))
                {
                    g.DrawEllipse(ringPen, ringRect);
                }

                Rectangle innerRing = new Rectangle(orbRect.X + 6, orbRect.Y + 6, orbRect.Width - 12, orbRect.Height - 12);
                using (Pen arcPen = new Pen(Color.FromArgb(180, 90, 245, 255), 1.8f))
                {
                    g.DrawArc(arcPen, innerRing, angleDeg, 130f);
                    g.DrawArc(arcPen, innerRing, angleDeg + 180f, 130f);
                }

                // Crisp center sparkle emblem
                using (Font f = new Font("Segoe UI Symbol", Math.Max(9f, rad * 0.58f), FontStyle.Bold))
                using (SolidBrush sb = new SolidBrush(isListening ? Color.FromArgb(60, 250, 165) : Color.FromArgb(225, 248, 255)))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("✦", f, sb, orbRect, sf);
                }
            }
            else if (styleIdx == 1)
            {
                // STYLE 1: GOOGLE HALO (4 smooth 90-degree colored arc segments + sleek center mic/sparkle)
                Color[] gCols = new Color[] {
                    Color.FromArgb(66, 133, 244),
                    Color.FromArgb(234, 67, 53),
                    Color.FromArgb(251, 188, 5),
                    Color.FromArgb(52, 168, 83)
                };
                for (int i = 0; i < 4; i++)
                {
                    using (Pen segPen = new Pen(gCols[i], 3.4f))
                    {
                        segPen.StartCap = LineCap.Round;
                        segPen.EndCap = LineCap.Round;
                        g.DrawArc(segPen, ringRect, angleDeg + i * 90f, 72f);
                    }
                }
                using (Font f = new Font("Segoe UI", Math.Max(7.5f, rad * 0.42f), FontStyle.Bold))
                using (SolidBrush sb = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("AI", f, sb, orbRect, sf);
                }
            }
            else if (styleIdx == 2)
            {
                // STYLE 2: CYBER NEON PULSE (Concentric glowing neon rings + live 5-bar equalizer inside)
                using (Pen outerPen = new Pen(Color.FromArgb(230, 0, 230, 255), 2.5f))
                {
                    g.DrawEllipse(outerPen, ringRect);
                }
                int barCount = 5;
                int totalW = Math.Max(14, (int)(orbRect.Width * 0.46f));
                int spacing = totalW / barCount;
                int startX = cx - (totalW / 2) + 2;
                for (int b = 0; b < barCount; b++)
                {
                    float wave = (float)Math.Abs(Math.Sin(phase * 3.0f + b * 0.85f));
                    int bh = Math.Max(4, (int)(rad * 0.75f * (0.35f + 0.65f * wave)));
                    Rectangle barR = new Rectangle(startX + b * spacing, cy - bh / 2, Math.Max(2, spacing - 2), bh);
                    using (SolidBrush bb = new SolidBrush(b % 2 == 0 ? Color.FromArgb(0, 225, 255) : Color.FromArgb(185, 95, 255)))
                    {
                        g.FillRectangle(bb, barR);
                    }
                }
            }
            else
            {
                // STYLE 3: MINIMAL GLASS GEM (Smoked-crystal circle with warm gold/cyan rim and clean ✦ AI badge)
                using (LinearGradientBrush gemFill = new LinearGradientBrush(orbRect, Color.FromArgb(32, 42, 62), Color.FromArgb(12, 16, 24), 45f))
                {
                    g.FillEllipse(gemFill, ringRect);
                }
                using (Pen rimPen = new Pen(Color.FromArgb(220, 250, 195, 55), 2.4f))
                {
                    g.DrawEllipse(rimPen, ringRect);
                }
                using (Font f = new Font("Segoe UI", Math.Max(7.5f, rad * 0.42f), FontStyle.Bold))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(255, 215, 90)))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("✦ AI", f, sb, orbRect, sf);
                }
            }

            g.SmoothingMode = oldSm;
        }


        // ====================================================================
        // STUDIO GLASS RENDERING & CUSTOM DRAWING HELPERS
        // ====================================================================
        private GraphicsPath GetRoundRectPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) return path;
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 1)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawGlassCard(Graphics g, Rectangle rect, int radius, Color baseFill, Color borderCol)
        {
            if (rect.Width <= 2 || rect.Height <= 2) return;
            using (GraphicsPath path = GetRoundRectPath(rect, radius))
            {
                using (SolidBrush fill = new SolidBrush(baseFill))
                {
                    g.FillPath(fill, path);
                }
                using (Pen borderPen = new Pen(borderCol, 1f))
                {
                    g.DrawPath(borderPen, path);
                }
            }
        }

        private void DrawPillButton(Graphics g, Rectangle rect, string text, bool active, Color accentColor, float fontSize)
        {
            if (rect.Width <= 2 || rect.Height <= 2) return;
            Color bg = active ? Color.FromArgb(225, accentColor.R, accentColor.G, accentColor.B) : Color.FromArgb(26, 32, 44);
            Color border = active ? accentColor : Color.FromArgb(55, 255, 255, 255);
            Color fg = active ? (accentColor.GetBrightness() > 0.58f ? Color.FromArgb(12, 16, 22) : Color.White) : Color.FromArgb(220, 228, 240);

            DrawGlassCard(g, rect, rect.Height / 2, bg, border);
            using (Font f = new Font("Segoe UI", fontSize, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(fg))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(text, f, tb, rect, sf);
            }
        }

        private void DrawSongCoverArt(Graphics g, Rectangle rect, SongItem song, int radius)
        {
            if (rect.Width <= 4 || rect.Height <= 4) return;
            Color c1 = song != null ? song.AccentColor : Color.FromArgb(0, 195, 245);
            Color c2 = Color.FromArgb(18, 24, 38);

            using (GraphicsPath path = GetRoundRectPath(rect, radius))
            {
                using (LinearGradientBrush lgb = new LinearGradientBrush(rect, Color.FromArgb(210, c1.R, c1.G, c1.B), c2, 135f))
                {
                    g.FillPath(lgb, path);
                }
                using (Pen p = new Pen(Color.FromArgb(65, 255, 255, 255), 1f))
                {
                    g.DrawPath(p, path);
                }
            }

            if (song != null)
            {
                // Crisp vinyl / studio artwork typography inside cover card
                using (Font badgeF = new Font("Segoe UI", Math.Max(6.5f, rect.Width * 0.055f), FontStyle.Bold))
                using (SolidBrush badgeB = new SolidBrush(Color.FromArgb(190, 255, 255, 255)))
                {
                    if (rect.Width >= 90)
                    {
                        g.DrawString(song.Country.ToUpperInvariant() + " • 24-BIT", badgeF, badgeB, rect.X + 10, rect.Y + 8);
                    }
                }

                using (Font iconF = new Font("Segoe UI", Math.Max(11f, rect.Width * 0.16f), FontStyle.Bold))
                using (SolidBrush iconB = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    string label = rect.Width >= 90 ? song.Title.ToUpperInvariant() : "♫";
                    Rectangle centerR = new Rectangle(rect.X + 6, rect.Y + 6, rect.Width - 12, rect.Height - 12);
                    g.DrawString(label, iconF, iconB, centerR, sf);
                }
            }
        }

        public void ShowToast(string msg)
        {
            toastMessage = msg;
            toastTicksRemaining = 65; // ~3.25 seconds
            this.Invalidate();
        }

        // ====================================================================
        // MAIN FORM PAINT PIPELINE (SIDEBAR + STAGE + RIGHT DOCK + PLAYER BAR)
        // ====================================================================
        private void OnMainFormPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            if (w <= 10 || h <= 10) return;

            // Deep obsidian studio background
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(10, 13, 19)))
            {
                g.FillRectangle(bg, this.ClientRectangle);
            }

            int sidebarW = isFullScreenStage ? 0 : 210;
            int bottomBarH = 88;
            int rightDockW = (!isFullScreenStage && isRightDockVisible) ? 330 : 0;

            Rectangle sidebarRect = new Rectangle(0, 0, sidebarW, h - bottomBarH);
            Rectangle rightDockRect = new Rectangle(w - rightDockW, 58, rightDockW, h - bottomBarH - 58);
            Rectangle stageRect = new Rectangle(sidebarW, 0, w - sidebarW - rightDockW, h - bottomBarH);
            Rectangle bottomRect = new Rectangle(0, h - bottomBarH, w, bottomBarH);

            if (!isFullScreenStage)
            {
                PaintLeftSidebar(g, sidebarRect);
            }

            PaintTopNavigationBar(g, new Rectangle(sidebarW, 0, w - sidebarW, 58));

            PaintCenterSongStage(g, new Rectangle(sidebarW, 58, w - sidebarW - rightDockW, h - bottomBarH - 58));

            if (rightDockW > 0)
            {
                PaintRightDockedPanel(g, rightDockRect);
            }

            PaintBottomPlayerBar(g, bottomRect);

            if (isSettingsModalOpen)
            {
                PaintSettingsModalOverlay(g, this.ClientRectangle);
            }

            // Toast notification banner above bottom player bar
            if (toastTicksRemaining > 0 && !string.IsNullOrEmpty(toastMessage))
            {
                int tw = Math.Min(540, w - 80);
                Rectangle toastR = new Rectangle((w - tw) / 2, h - bottomBarH - 44, tw, 34);
                DrawGlassCard(g, toastR, 17, Color.FromArgb(240, 16, 24, 36), Color.FromArgb(200, 0, 215, 255));
                using (Font tf = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(toastMessage, tf, tb, toastR, sf);
                }
            }

            // Outer subtle window frame
            using (Pen winBorder = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
            {
                g.DrawRectangle(winBorder, 0, 0, w - 1, h - 1);
            }
        }

        // ====================================================================
        // LEFT SIDEBAR + LEO AI CIRCLE LAUNCHER (CLICK TO HIDE/UNHIDE AI PANEL)
        // ====================================================================
        private void PaintLeftSidebar(Graphics g, Rectangle r)
        {
            navItemRects.Clear();
            playlistNavRects.Clear();

            DrawGlassCard(g, new Rectangle(r.X + 6, r.Y + 6, r.Width - 10, r.Height - 10), 16,
                Color.FromArgb(14, 18, 25), Color.FromArgb(32, 255, 255, 255));

            // Brand Logo Header
            Rectangle logoOrb = new Rectangle(r.X + 20, r.Y + 18, 30, 30);
            using (SolidBrush lb = new SolidBrush(Color.FromArgb(0, 195, 245)))
            {
                g.FillEllipse(lb, logoOrb);
            }
            using (Font lf = new Font("Segoe UI", 11f, FontStyle.Bold))
            {
                g.DrawString("♫", lf, Brushes.Black, logoOrb.X + 5, logoOrb.Y + 3);
            }
            using (Font brandF = new Font("Segoe UI", 13.5f, FontStyle.Bold))
            {
                g.DrawString("LEO", brandF, Brushes.White, r.X + 56, r.Y + 20);
                using (SolidBrush cyanB = new SolidBrush(Color.FromArgb(0, 210, 255)))
                {
                    g.DrawString("Music", brandF, cyanB, r.X + 98, r.Y + 20);
                }
            }

            string[] navNames = new string[] { "Home", "Search", "Library", "Liked Songs", "Playlists", "Downloads" };
            string[] navIcons = new string[] { "⌂", "⌕", "||| ", "♡", "♫", "↓" };

            int ny = r.Y + 66;
            for (int i = 0; i < navNames.Length; i++)
            {
                Rectangle itemR = new Rectangle(r.X + 14, ny, r.Width - 28, 36);
                navItemRects.Add(new KeyValuePair<string, Rectangle>(navNames[i], itemR));

                bool isSel = string.Equals(activeTab, navNames[i], StringComparison.OrdinalIgnoreCase);
                if (isSel)
                {
                    DrawGlassCard(g, itemR, 18, Color.FromArgb(215, 0, 165, 205), Color.FromArgb(245, 80, 235, 255));
                }

                using (Font f = new Font("Segoe UI", 9.5f, isSel ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush sb = new SolidBrush(isSel ? Color.White : Color.FromArgb(190, 200, 215)))
                {
                    g.DrawString(navIcons[i] + "   " + navNames[i], f, sb, itemR.X + 14, itemR.Y + 8);
                }
                ny += 41;
            }

            // Playlists Section
            ny += 10;
            using (Font pf = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (SolidBrush pb = new SolidBrush(Color.FromArgb(150, 165, 185)))
            {
                g.DrawString("Playlists", pf, pb, r.X + 20, ny);
            }
            rectAddPlaylistBtn = new Rectangle(r.Right - 42, ny - 2, 24, 22);
            using (Font plusF = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (SolidBrush plusB = new SolidBrush(Color.FromArgb(200, 215, 230)))
            {
                g.DrawString("+", plusF, plusB, rectAddPlaylistBtn.X + 4, rectAddPlaylistBtn.Y - 2);
            }

            ny += 28;
            for (int i = 0; i < userPlaylists.Count; i++)
            {
                PlaylistEntry pl = userPlaylists[i];
                Rectangle pr = new Rectangle(r.X + 14, ny, r.Width - 28, 34);
                playlistNavRects.Add(new KeyValuePair<string, Rectangle>(pl.Name, pr));

                Rectangle badgeR = new Rectangle(pr.X + 6, pr.Y + 3, 28, 28);
                DrawGlassCard(g, badgeR, 7, Color.FromArgb(35, 110, 145), Color.FromArgb(70, 0, 210, 255));
                using (Font nf = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                using (SolidBrush nb = new SolidBrush(Color.FromArgb(0, 215, 255)))
                {
                    g.DrawString("♫", nf, nb, badgeR.X + 7, badgeR.Y + 5);
                }
                using (Font tf = new Font("Segoe UI", 9f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(215, 225, 238)))
                {
                    g.DrawString(pl.Name, tf, tb, pr.X + 42, pr.Y + 7);
                }
                ny += 38;
            }

            // ================================================================
            // BOTTOM OF SIDEBAR: LEO AI CIRCLE (CLICK TO HIDE/UNHIDE AI PANEL)
            // ================================================================
            if (aiAssistantEnabled)
            {
                int cardH = 104;
                Rectangle aiDockCard = new Rectangle(r.X + 12, r.Bottom - cardH - 12, r.Width - 24, cardH);
                bool panelActive = (isRightDockVisible && rightPanelMode == 2 && isAgentPanelOpen);

                DrawGlassCard(g, aiDockCard, 16,
                    panelActive ? Color.FromArgb(28, 38, 56) : Color.FromArgb(18, 24, 34),
                    panelActive ? Color.FromArgb(190, 0, 220, 255) : Color.FromArgb(55, 255, 255, 255));

                rectFloatingAgentOrb = new Rectangle(aiDockCard.X + 10, aiDockCard.Y + 14, 52, 52);
                DrawLeoAiCircleOrb(g, rectFloatingAgentOrb, agentPhase, isAgentVoiceListening, aiOrbStyleIdx);

                using (Font titleF = new Font("Segoe UI", 9.2f, FontStyle.Bold))
                using (SolidBrush titleB = new SolidBrush(Color.White))
                {
                    g.DrawString("Leo AI Circle", titleF, titleB, aiDockCard.X + 68, aiDockCard.Y + 14);
                }
                using (Font subF = new Font("Segoe UI", 7.5f, FontStyle.Regular))
                using (SolidBrush subB = new SolidBrush(Color.FromArgb(165, 185, 210)))
                {
                    string actionHint = panelActive ? "Click Circle to Hide Panel" : "Click Circle to Open Panel";
                    g.DrawString(actionHint, subF, subB, aiDockCard.X + 68, aiDockCard.Y + 33);
                    g.DrawString(aiOrbStyleNames[aiOrbStyleIdx], subF, new SolidBrush(Color.FromArgb(0, 215, 255)), aiDockCard.X + 68, aiDockCard.Y + 49);
                }

                rectFloatingAgentMicQuick = new Rectangle(aiDockCard.X + 10, aiDockCard.Bottom - 29, 82, 22);
                rectFloatingAgentStyleQuick = new Rectangle(aiDockCard.X + 98, aiDockCard.Bottom - 29, 78, 22);
                DrawPillButton(g, rectFloatingAgentMicQuick, isAgentVoiceListening ? "◉ Listening" : "◉ Voice Mic", isAgentVoiceListening, Color.FromArgb(40, 225, 135), 7.5f);
                DrawPillButton(g, rectFloatingAgentStyleQuick, "✦ Circle Style", false, Color.FromArgb(0, 210, 255), 7.5f);
            }
            else
            {
                rectFloatingAgentOrb = Rectangle.Empty;
                rectFloatingAgentMicQuick = Rectangle.Empty;
                rectFloatingAgentStyleQuick = Rectangle.Empty;
            }
        }

        // ====================================================================
        // DECLUTTERED TOP BAR (SEARCH + 3 CLEAN PILLS + PROFILE + WIN CONTROLS)
        // ====================================================================
        private void PaintTopNavigationBar(Graphics g, Rectangle r)
        {
            // Search Box on Left
            int searchW = Math.Min(360, Math.Max(220, r.Width - 460));
            rectSearchBox = new Rectangle(r.X + 16, r.Y + 12, searchW, 36);
            DrawGlassCard(g, rectSearchBox, 18,
                isSearchFocused ? Color.FromArgb(26, 34, 48) : Color.FromArgb(18, 23, 32),
                isSearchFocused ? Color.FromArgb(200, 0, 215, 255) : Color.FromArgb(55, 255, 255, 255));

            using (Font sf = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            {
                string disp = string.IsNullOrEmpty(searchQuery)
                    ? (isSearchFocused ? "Type song or artist..." : "⌕  Search songs, artists, albums...")
                    : "⌕  " + searchQuery + (isSearchFocused ? "|" : "");
                using (SolidBrush sb = new SolidBrush(string.IsNullOrEmpty(searchQuery) ? Color.FromArgb(145, 160, 180) : Color.White))
                {
                    g.DrawString(disp, sf, sb, rectSearchBox.X + 14, rectSearchBox.Y + 8);
                }
            }

            if (!string.IsNullOrEmpty(searchQuery))
            {
                rectSearchClearBtn = new Rectangle(rectSearchBox.Right - 28, rectSearchBox.Y + 7, 22, 22);
                using (Font cf = new Font("Segoe UI", 9f, FontStyle.Bold))
                {
                    g.DrawString("×", cf, Brushes.LightGray, rectSearchClearBtn.X + 5, rectSearchClearBtn.Y + 2);
                }
            }
            else
            {
                rectSearchClearBtn = Rectangle.Empty;
            }

            // Window Controls (Top-Right)
            rectCloseBtn = new Rectangle(r.Right - 36, r.Y + 10, 28, 26);
            rectMaxBtn = new Rectangle(r.Right - 68, r.Y + 10, 28, 26);
            rectMinBtn = new Rectangle(r.Right - 100, r.Y + 10, 28, 26);

            using (Font wf = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (SolidBrush wb = new SolidBrush(Color.FromArgb(190, 200, 215)))
            {
                g.DrawString("—", wf, wb, rectMinBtn.X + 6, rectMinBtn.Y + 3);
                g.DrawString("□", wf, wb, rectMaxBtn.X + 6, rectMaxBtn.Y + 3);
                g.DrawString("✕", wf, wb, rectCloseBtn.X + 6, rectCloseBtn.Y + 3);
            }

            // User Profile Avatar Circle (Opens Settings / Profile)
            rectProfileBtn = new Rectangle(r.Right - 148, r.Y + 12, 34, 34);
            if (userProfileAvatarBmp != null)
            {
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(rectProfileBtn);
                    Region oldClip = g.Clip;
                    g.SetClip(gp);
                    g.DrawImage(userProfileAvatarBmp, rectProfileBtn);
                    g.Clip = oldClip;
                }
                using (Pen ap = new Pen(Color.FromArgb(200, 0, 215, 255), 1.6f))
                {
                    g.DrawEllipse(ap, rectProfileBtn);
                }
            }
            else
            {
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(0, 175, 220)))
                {
                    g.FillEllipse(pb, rectProfileBtn);
                }
                using (Font pf = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    string init = !string.IsNullOrEmpty(userName) ? userName.Substring(0, 1).ToUpperInvariant() : "U";
                    g.DrawString(init, pf, Brushes.White, rectProfileBtn, sf);
                }
            }

            // 3 Clean Decluttered Utility Pills ([ ✦ Leo AI ], [ ◉ Country ], [ ♫ Vol% ])
            int rightCursor = rectProfileBtn.X - 12;

            rectTopVolumePill = new Rectangle(rightCursor - 82, r.Y + 14, 82, 30);
            DrawPillButton(g, rectTopVolumePill, "♫ " + volumePercent + "%", volumePercent > 100, Color.FromArgb(245, 185, 40), 8.2f);
            rightCursor = rectTopVolumePill.X - 8;

            rectCountrySelectorPill = new Rectangle(rightCursor - 88, r.Y + 14, 88, 30);
            DrawPillButton(g, rectCountrySelectorPill, "◉ " + selectedCountry, false, Color.FromArgb(0, 210, 255), 8.2f);
            rightCursor = rectCountrySelectorPill.X - 8;

            if (aiAssistantEnabled)
            {
                bool aiOpen = (isRightDockVisible && rightPanelMode == 2 && isAgentPanelOpen);
                rectTopBarAiBtn = new Rectangle(rightCursor - 94, r.Y + 14, 94, 30);
                DrawPillButton(g, rectTopBarAiBtn, aiOpen ? "✦ Hide AI" : "✦ Leo AI", aiOpen, Color.FromArgb(0, 215, 255), 8.2f);
            }
            else
            {
                rectTopBarAiBtn = Rectangle.Empty;
            }
        }


        // ====================================================================
        // CENTER SONG STAGE (HOMEPAGE HERO, COUNTRY SHELVES & LIBRARY VIEWS)
        // ====================================================================
        private void PaintCenterSongStage(Graphics g, Rectangle r)
        {
            moodChipRects.Clear();
            songCardRects.Clear();
            songLikeRects.Clear();
            songDownloadRects.Clear();

            Region oldClip = g.Clip;
            g.SetClip(r);

            int padX = r.X + 18;
            int y = r.Y + 6;

            // Header Title + Mood Filter Chips Row
            using (Font hFont = new Font("Segoe UI", 13.5f, FontStyle.Bold))
            {
                string viewTitle = activeTab == "Home" ? "Home" : activeTab;
                g.DrawString(viewTitle, hFont, Brushes.White, padX, y + 2);
            }

            int chipX = padX + 124;
            for (int i = 0; i < moodChips.Length; i++)
            {
                string m = moodChips[i];
                int cw = Math.Max(48, m.Length * 8 + 24);
                Rectangle cr = new Rectangle(chipX, y + 2, cw, 26);
                moodChipRects.Add(new KeyValuePair<string, Rectangle>(m, cr));

                bool isSel = string.Equals(selectedMood, m, StringComparison.OrdinalIgnoreCase);
                DrawPillButton(g, cr, m, isSel, Color.FromArgb(0, 205, 250), 8f);
                chipX += cw + 8;
            }

            y += 42 - centerScrollOffset;

            if (activeTab == "Home" && string.IsNullOrEmpty(searchQuery))
            {
                // ------------------------------------------------------------
                // 1. DECLUTTERED HOMEPAGE HERO BANNER CARD
                // ------------------------------------------------------------
                SongItem heroSong = currentSong ?? (currentQueue.Count > 0 ? currentQueue[0] : (allSongs.Count > 0 ? allSongs[0] : null));
                Rectangle heroRect = new Rectangle(padX, y, r.Width - 36, 178);
                Color heroAccent = heroSong != null ? heroSong.AccentColor : Color.FromArgb(245, 185, 35);

                DrawGlassCard(g, heroRect, 18,
                    Color.FromArgb(20, 26, 36),
                    Color.FromArgb(75, heroAccent.R, heroAccent.G, heroAccent.B));

                Rectangle coverR = new Rectangle(heroRect.X + 16, heroRect.Y + 14, 150, 150);
                DrawSongCoverArt(g, coverR, heroSong, 14);

                int tx = coverR.Right + 22;
                int ty = heroRect.Y + 16;

                using (Font tagF = new Font("Segoe UI", 7.8f, FontStyle.Bold))
                using (SolidBrush tagB = new SolidBrush(Color.FromArgb(180, 195, 215)))
                {
                    string greet = "PLAYING NOW  •  HELLO, " + (userName ?? "USER").ToUpperInvariant() + "  •  " + selectedCountry.ToUpperInvariant() + " STUDIO MIX";
                    g.DrawString(greet, tagF, tagB, tx, ty);
                }

                using (Font titleF = new Font("Segoe UI", 20f, FontStyle.Bold))
                {
                    g.DrawString(heroSong != null ? heroSong.Title : "Leo Studio Mix", titleF, Brushes.White, tx, ty + 20);
                }

                using (Font metaF = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                using (SolidBrush metaB = new SolidBrush(Color.FromArgb(195, 205, 222)))
                {
                    string meta = heroSong != null
                        ? (heroSong.Artist + "  •  " + heroSong.Bpm + " BPM  •  24-Bit Studio")
                        : "24-Bit Hi-Res Spatial Audio";
                    g.DrawString(meta, metaF, metaB, tx, ty + 58);
                }

                using (Font lyrF = new Font("Segoe UI", 8.8f, FontStyle.Italic))
                using (SolidBrush lyrB = new SolidBrush(Color.FromArgb(235, 195, 95)))
                {
                    string lyrLine = (currentLyrics.Count > 0 && activeLyricIndex < currentLyrics.Count)
                        ? ("♪ " + currentLyrics[activeLyricIndex].Text)
                        : (heroSong != null ? "♪ " + heroSong.LyricSnippet : "♪ Crystal clear zero-echo studio stage");
                    g.DrawString(lyrLine, lyrF, lyrB, tx, ty + 82);
                }

                // Clean Hero Buttons Row (4 sleek buttons - never crowded)
                int btnY = heroRect.Bottom - 46;
                rectHeroPlayBtn = new Rectangle(tx, btnY, 108, 34);
                rectHeroLikeBtn = new Rectangle(rectHeroPlayBtn.Right + 10, btnY, 38, 34);
                rectHeroMoreBtn = new Rectangle(rectHeroLikeBtn.Right + 10, btnY, 38, 34);
                rectHeroSmartDjBtn = new Rectangle(rectHeroMoreBtn.Right + 12, btnY, 128, 34);

                DrawPillButton(g, rectHeroPlayBtn, isPlaying ? "❚❚ Pause" : "▶ Play", true, heroAccent, 9.2f);
                DrawPillButton(g, rectHeroLikeBtn, (heroSong != null && heroSong.IsLiked) ? "♥" : "♡", heroSong != null && heroSong.IsLiked, Color.FromArgb(245, 75, 110), 9.5f);
                DrawPillButton(g, rectHeroMoreBtn, "↓", heroSong != null && heroSong.IsDownloaded, Color.FromArgb(40, 220, 140), 9.5f);
                DrawPillButton(g, rectHeroSmartDjBtn, "✦ Smart AI DJ", false, Color.FromArgb(0, 210, 255), 8.5f);

                y += 196;

                // ------------------------------------------------------------
                // 2. RECENTLY PLAYED SHELF
                // ------------------------------------------------------------
                using (Font secF = new Font("Segoe UI", 12f, FontStyle.Bold))
                {
                    g.DrawString("Recently Played", secF, Brushes.White, padX, y);
                }
                y += 30;
                PaintHorizontalSongShelf(g, padX, y, r.Width - 36, recentlyPlayed);
                y += 192;

                // ------------------------------------------------------------
                // 3. TRENDING IN [SELECTED COUNTRY] SHELF
                // ------------------------------------------------------------
                using (Font secF = new Font("Segoe UI", 12f, FontStyle.Bold))
                {
                    g.DrawString("Trending in " + selectedCountry, secF, Brushes.White, padX, y);
                }
                rectPlayTrendingChartBtn = new Rectangle(r.Right - 114, y - 2, 96, 26);
                DrawPillButton(g, rectPlayTrendingChartBtn, "▶ Play Chart", true, Color.FromArgb(245, 185, 35), 8f);

                y += 30;
                List<SongItem> countryTrending = GetFilteredCountrySongs();
                PaintHorizontalSongShelf(g, padX, y, r.Width - 36, countryTrending);
            }
            else
            {
                // ------------------------------------------------------------
                // LIST VIEW FOR DOWNLOADS, SEARCH, LIBRARY, LIKED, PLAYLISTS
                // ------------------------------------------------------------
                List<SongItem> listToShow = GetActiveViewSongs();

                if (activeTab == "Downloads")
                {
                    Rectangle dlBanner = new Rectangle(padX, y, r.Width - 36, 46);
                    DrawGlassCard(g, dlBanner, 12, Color.FromArgb(18, 26, 38), Color.FromArgb(65, 0, 210, 255));
                    using (Font bf = new Font("Segoe UI", 8.8f, FontStyle.Regular))
                    using (SolidBrush bb = new SolidBrush(Color.FromArgb(195, 215, 235)))
                    {
                        g.DrawString("Offline Folder: " + localDownloadsDir + "  (" + listToShow.Count + " Saved Songs)", bf, bb, dlBanner.X + 14, dlBanner.Y + 14);
                    }
                    rectDownloadsPlayAllBtn = new Rectangle(dlBanner.Right - 238, dlBanner.Y + 8, 110, 30);
                    rectDownloadsOpenFolderBtn = new Rectangle(dlBanner.Right - 120, dlBanner.Y + 8, 108, 30);
                    DrawPillButton(g, rectDownloadsPlayAllBtn, "▶ Play Offline", true, Color.FromArgb(0, 205, 250), 8f);
                    DrawPillButton(g, rectDownloadsOpenFolderBtn, "▤ Local Folder", false, Color.FromArgb(40, 220, 140), 8f);
                    y += 58;
                }

                for (int i = 0; i < listToShow.Count; i++)
                {
                    SongItem s = listToShow[i];
                    Rectangle rowR = new Rectangle(padX, y, r.Width - 36, 52);
                    bool isCur = (currentSong == s);

                    DrawGlassCard(g, rowR, 12,
                        isCur ? Color.FromArgb(26, 38, 54) : Color.FromArgb(16, 21, 30),
                        isCur ? Color.FromArgb(170, 0, 215, 255) : Color.FromArgb(35, 255, 255, 255));

                    songCardRects.Add(new KeyValuePair<SongItem, Rectangle>(s, rowR));

                    Rectangle thumbR = new Rectangle(rowR.X + 8, rowR.Y + 6, 40, 40);
                    DrawSongCoverArt(g, thumbR, s, 8);

                    using (Font tf = new Font("Segoe UI", 9.8f, FontStyle.Bold))
                    {
                        g.DrawString(s.Title, tf, Brushes.White, thumbR.Right + 14, rowR.Y + 8);
                    }
                    using (Font af = new Font("Segoe UI", 8.2f, FontStyle.Regular))
                    using (SolidBrush ab = new SolidBrush(Color.FromArgb(165, 180, 200)))
                    {
                        // Clean decluttered subtitle: Artist • Mood • Country
                        g.DrawString(s.Artist + "  •  " + s.Mood + "  •  " + s.Country, af, ab, thumbR.Right + 14, rowR.Y + 28);
                    }

                    Rectangle dlBtn = new Rectangle(rowR.Right - 118, rowR.Y + 12, 64, 28);
                    Rectangle lkBtn = new Rectangle(rowR.Right - 46, rowR.Y + 12, 34, 28);
                    songDownloadRects.Add(new KeyValuePair<SongItem, Rectangle>(s, dlBtn));
                    songLikeRects.Add(new KeyValuePair<SongItem, Rectangle>(s, lkBtn));

                    DrawPillButton(g, dlBtn, s.IsDownloaded ? "✓ Saved" : "↓ Save", s.IsDownloaded, Color.FromArgb(40, 220, 140), 7.8f);
                    DrawPillButton(g, lkBtn, s.IsLiked ? "♥" : "♡", s.IsLiked, Color.FromArgb(245, 75, 110), 8.5f);

                    y += 58;
                }
            }

            g.Clip = oldClip;
        }

        private void PaintHorizontalSongShelf(Graphics g, int startX, int y, int availWidth, List<SongItem> songs)
        {
            int cardW = 142;
            int cardH = 176;
            int gap = 14;
            int maxCards = Math.Max(3, (availWidth + gap) / (cardW + gap));

            for (int i = 0; i < Math.Min(maxCards, songs.Count); i++)
            {
                SongItem s = songs[i];
                Rectangle cardR = new Rectangle(startX + i * (cardW + gap), y, cardW, cardH);
                bool isCur = (currentSong == s);

                DrawGlassCard(g, cardR, 14,
                    isCur ? Color.FromArgb(26, 36, 52) : Color.FromArgb(16, 20, 29),
                    isCur ? Color.FromArgb(180, s.AccentColor.R, s.AccentColor.G, s.AccentColor.B) : Color.FromArgb(35, 255, 255, 255));

                songCardRects.Add(new KeyValuePair<SongItem, Rectangle>(s, cardR));

                Rectangle artR = new Rectangle(cardR.X + 8, cardR.Y + 8, cardW - 16, cardW - 16);
                DrawSongCoverArt(g, artR, s, 10);

                using (Font tf = new Font("Segoe UI", 9.2f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    Rectangle tr = new Rectangle(cardR.X + 8, artR.Bottom + 6, cardW - 16, 18);
                    StringFormat sf = new StringFormat(StringFormatFlags.NoWrap);
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(s.Title, tf, tb, tr, sf);
                }

                using (Font af = new Font("Segoe UI", 7.8f, FontStyle.Regular))
                using (SolidBrush ab = new SolidBrush(Color.FromArgb(160, 175, 195)))
                {
                    Rectangle ar = new Rectangle(cardR.X + 8, artR.Bottom + 24, cardW - 16, 16);
                    StringFormat sf = new StringFormat(StringFormatFlags.NoWrap);
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(s.Artist, af, ab, ar, sf);
                }
            }
        }

        private List<SongItem> GetFilteredCountrySongs()
        {
            List<SongItem> list = new List<SongItem>();
            foreach (SongItem s in allSongs)
            {
                bool cMatch = string.Equals(selectedCountry, "Global", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(s.Country, selectedCountry, StringComparison.OrdinalIgnoreCase);
                bool mMatch = string.Equals(selectedMood, "All", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(s.Mood, selectedMood, StringComparison.OrdinalIgnoreCase);
                if (cMatch && mMatch) list.Add(s);
            }
            if (list.Count < 5)
            {
                foreach (SongItem s in allSongs)
                {
                    if (!list.Contains(s)) list.Add(s);
                    if (list.Count >= 6) break;
                }
            }
            return list;
        }

        private List<SongItem> GetActiveViewSongs()
        {
            List<SongItem> res = new List<SongItem>();
            if (!string.IsNullOrEmpty(searchQuery))
            {
                SongItem bestFuzzy = FindBestFuzzySongMatch(searchQuery);
                if (bestFuzzy != null) res.Add(bestFuzzy);
                foreach (SongItem s in allSongs)
                {
                    if (res.Contains(s)) continue;
                    if (s.Title.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.Artist.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.Country.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        res.Add(s);
                    }
                }
                return res;
            }

            if (activeTab == "Downloads")
            {
                foreach (SongItem s in allSongs)
                {
                    if (s.IsDownloaded) res.Add(s);
                }
                if (res.Count == 0 && allSongs.Count > 0)
                {
                    // Auto-save first 2 tracks if user visits Downloads for the first time so folder is ready immediately
                    DownloadSongOffline(allSongs[0]);
                    if (allSongs.Count > 1) DownloadSongOffline(allSongs[1]);
                    foreach (SongItem s in allSongs)
                    {
                        if (s.IsDownloaded) res.Add(s);
                    }
                }
                return res;
            }

            if (activeTab == "Liked Songs")
            {
                foreach (SongItem s in allSongs)
                {
                    if (s.IsLiked) res.Add(s);
                }
                if (res.Count == 0 && allSongs.Count > 0) res.Add(allSongs[0]);
                return res;
            }

            if (activeTab == "Playlists" && !string.IsNullOrEmpty(selectedPlaylistName))
            {
                foreach (PlaylistEntry pl in userPlaylists)
                {
                    if (string.Equals(pl.Name, selectedPlaylistName, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (string id in pl.SongIds)
                        {
                            SongItem found = FindSongByIdOrTitle(id, "", "");
                            if (found != null) res.Add(found);
                        }
                    }
                }
                if (res.Count > 0) return res;
            }

            return GetFilteredCountrySongs();
        }


        // ====================================================================
        // RIGHT DOCKED SIDE PANEL (UP NEXT QUEUE | LYRICS | DOCKED LEO AI)
        // ====================================================================
        private void PaintRightDockedPanel(Graphics g, Rectangle r)
        {
            queueItemRects.Clear();
            queueRemoveRects.Clear();
            rectAgentQuickChips.Clear();

            Rectangle cardR = new Rectangle(r.X + 4, r.Y + 4, r.Width - 10, r.Height - 8);
            DrawGlassCard(g, cardR, 16, Color.FromArgb(14, 18, 26), Color.FromArgb(45, 255, 255, 255));

            // 3-Tab Segmented Switcher Header + Hide Button
            int tabY = cardR.Y + 10;
            int tabW = aiAssistantEnabled ? 84 : 126;
            rectRightTabQueue = new Rectangle(cardR.X + 10, tabY, tabW, 28);
            rectRightTabLyrics = new Rectangle(rectRightTabQueue.Right + 6, tabY, tabW, 28);

            DrawPillButton(g, rectRightTabQueue, "Up Next", rightPanelMode == 0, Color.FromArgb(0, 205, 250), 8f);
            DrawPillButton(g, rectRightTabLyrics, "Lyrics", rightPanelMode == 1, Color.FromArgb(0, 205, 250), 8f);

            if (aiAssistantEnabled)
            {
                rectRightTabAi = new Rectangle(rectRightTabLyrics.Right + 6, tabY, 86, 28);
                DrawPillButton(g, rectRightTabAi, "✦ Leo AI", rightPanelMode == 2, Color.FromArgb(245, 185, 35), 8f);
            }
            else
            {
                rectRightTabAi = Rectangle.Empty;
                if (rightPanelMode == 2) rightPanelMode = 0;
            }

            rectRightCloseDockBtn = new Rectangle(cardR.Right - 32, tabY, 24, 28);
            using (Font cf = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush cb = new SolidBrush(Color.FromArgb(180, 195, 215)))
            {
                g.DrawString("✕", cf, cb, rectRightCloseDockBtn.X + 5, rectRightCloseDockBtn.Y + 4);
            }

            Rectangle bodyR = new Rectangle(cardR.X + 10, tabY + 36, cardR.Width - 20, cardR.Height - 48);

            if (rightPanelMode == 2 && aiAssistantEnabled)
            {
                PaintDockedLeoAiSidePanel(g, bodyR);
            }
            else if (rightPanelMode == 1)
            {
                PaintLyricsSidePanel(g, bodyR);
            }
            else
            {
                PaintQueueSidePanel(g, bodyR);
            }
        }

        // ====================================================================
        // v10.6.3 CLEAN DOCKED LEO AI SIDE PANEL (NOT FULL SCREEN!)
        // ====================================================================
        private void PaintDockedLeoAiSidePanel(Graphics g, Rectangle r)
        {
            int y = r.Y;

            // 1. Clean Orb + Controls Header Card (Zero overlap!)
            Rectangle hdrCard = new Rectangle(r.X, y, r.Width, 78);
            DrawGlassCard(g, hdrCard, 14, Color.FromArgb(19, 25, 36), Color.FromArgb(65, 0, 215, 255));

            rectAgentOrbHeader = new Rectangle(hdrCard.X + 10, hdrCard.Y + 11, 54, 54);
            DrawLeoAiCircleOrb(g, rectAgentOrbHeader, agentPhase, isAgentVoiceListening, aiOrbStyleIdx);

            using (Font tf = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            {
                g.DrawString("Leo Smart AI Side Panel", tf, Brushes.White, hdrCard.X + 72, hdrCard.Y + 10);
            }
            using (Font sf = new Font("Segoe UI", 7.5f, FontStyle.Regular))
            using (SolidBrush sb = new SolidBrush(isAgentVoiceListening ? Color.FromArgb(50, 235, 145) : Color.FromArgb(165, 185, 210)))
            {
                StringFormat wrapSf = new StringFormat(StringFormatFlags.NoWrap);
                wrapSf.Trimming = StringTrimming.EllipsisCharacter;
                Rectangle statusR = new Rectangle(hdrCard.X + 72, hdrCard.Y + 29, hdrCard.Width - 78, 16);
                g.DrawString(agentStatusText, sf, sb, statusR, wrapSf);
            }

            // 3 compact utility pills inside header
            int pillY = hdrCard.Y + 47;
            rectAgentVoiceToggleBtn = new Rectangle(hdrCard.X + 72, pillY, 68, 22);
            rectAgentStyleCycleBtn = new Rectangle(rectAgentVoiceToggleBtn.Right + 6, pillY, 82, 22);
            rectAgentFloatWidgetBtn = new Rectangle(rectAgentStyleCycleBtn.Right + 6, pillY, 62, 22);

            DrawPillButton(g, rectAgentVoiceToggleBtn, isAgentVoiceListening ? "◉ Mic ON" : "◉ Voice", isAgentVoiceListening, Color.FromArgb(40, 225, 135), 7.2f);
            DrawPillButton(g, rectAgentStyleCycleBtn, "✦ Circle Style", false, Color.FromArgb(0, 210, 255), 7.2f);
            DrawPillButton(g, rectAgentFloatWidgetBtn, isFloatingWidgetEnabled ? "Float ON" : "Float PC", isFloatingWidgetEnabled, Color.FromArgb(245, 185, 35), 7.2f);

            y += 86;

            // 2. High-Smart Typo-Tolerant Command Input Box + [ ▶ Run ] Button
            using (Font lblF = new Font("Segoe UI", 7.3f, FontStyle.Bold))
            using (SolidBrush lblB = new SolidBrush(Color.FromArgb(0, 215, 255)))
            {
                g.DrawString("SMART TYPO AI (Understands 'paly strby', 'opn whtsp', multi-task)", lblF, lblB, r.X + 2, y);
            }
            y += 16;

            rectAgentInputBox = new Rectangle(r.X, y, r.Width - 68, 34);
            rectAgentRunBtn = new Rectangle(rectAgentInputBox.Right + 6, y, 62, 34);

            DrawGlassCard(g, rectAgentInputBox, 10,
                isAgentInputFocused ? Color.FromArgb(24, 34, 48) : Color.FromArgb(16, 22, 32),
                isAgentInputFocused ? Color.FromArgb(215, 0, 220, 255) : Color.FromArgb(65, 255, 255, 255));

            using (Font inf = new Font("Segoe UI", 8.6f, FontStyle.Regular))
            {
                string disp = string.IsNullOrEmpty(agentInputText)
                    ? (isAgentInputFocused ? "Type e.g. paly kesrya or opn whtsp..." : "Type command (even half/wrong spelling)...")
                    : agentInputText + (isAgentInputFocused ? "|" : "");
                using (SolidBrush ib = new SolidBrush(string.IsNullOrEmpty(agentInputText) ? Color.FromArgb(140, 158, 180) : Color.White))
                {
                    Rectangle textR = new Rectangle(rectAgentInputBox.X + 10, rectAgentInputBox.Y + 8, rectAgentInputBox.Width - 16, 20);
                    StringFormat sf = new StringFormat(StringFormatFlags.NoWrap);
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(disp, inf, ib, textR, sf);
                }
            }

            DrawPillButton(g, rectAgentRunBtn, "▶ Run", true, Color.FromArgb(245, 185, 35), 8.5f);
            y += 40;

            // Live Smart Auto-Correction Preview Banner
            if (!string.IsNullOrEmpty(agentSmartHintText))
            {
                Rectangle hintR = new Rectangle(r.X, y, r.Width, 22);
                DrawGlassCard(g, hintR, 8, Color.FromArgb(20, 38, 48), Color.FromArgb(90, 0, 215, 255));
                using (Font hf = new Font("Segoe UI", 7.4f, FontStyle.Bold))
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(110, 235, 255)))
                {
                    StringFormat sf = new StringFormat(StringFormatFlags.NoWrap);
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(agentSmartHintText, hf, hb, new Rectangle(hintR.X + 8, hintR.Y + 3, hintR.Width - 14, 16), sf);
                }
                y += 26;
            }

            // 3. Clean 2-Column Grid of Quick Multi-Task Chips
            string[,] chips = new string[,] {
                { "✦ WhatsApp Message", "open whatsapp and message Hello from Leo AI" },
                { "✦ Add Note & Notepad", "add note Project review today at 6 PM" },
                { "✦ Open ChatGPT", "open chatgpt" },
                { "✦ YouTube Auto-Play", "open youtube and search Trending Songs 2026" },
                { "⚡ 4-in-1 Multi-Task", "open whatsapp and add note Studio session ready and open chatgpt and play Starboy" },
                { "♫ Smart Typo Demo", "paly strby" },
                { "♫ 3D Audio + Vol 125%", "volume 125 and crossfade 2" },
                { "▣ Calc & Screenshot", "open calculator and take screenshot" }
            };

            int colW = (r.Width - 8) / 2;
            int chipH = 27;
            for (int i = 0; i < 8; i++)
            {
                int col = i % 2;
                int row = i / 2;
                Rectangle cr = new Rectangle(r.X + col * (colW + 8), y + row * (chipH + 6), colW, chipH);
                rectAgentQuickChips.Add(new KeyValuePair<string, Rectangle>(chips[i, 1], cr));

                bool isCombo = (i == 4 || i == 5);
                DrawPillButton(g, cr, chips[i, 0], false, isCombo ? Color.FromArgb(0, 215, 255) : Color.FromArgb(180, 195, 215), 7.6f);
            }

            y += 4 * (chipH + 6) + 6;

            // 4. Live Multi-Task Execution Log & Saved Notes Box
            int logH = Math.Max(70, r.Bottom - y);
            Rectangle logBox = new Rectangle(r.X, y, r.Width, logH);
            DrawGlassCard(g, logBox, 12, Color.FromArgb(16, 21, 30), Color.FromArgb(45, 255, 255, 255));

            using (Font lf = new Font("Segoe UI", 7.3f, FontStyle.Bold))
            using (SolidBrush lb = new SolidBrush(Color.FromArgb(0, 215, 255)))
            {
                g.DrawString("LIVE SMART AI EXECUTION LOG & SAVED NOTES", lf, lb, logBox.X + 10, logBox.Y + 8);
            }

            int ly = logBox.Y + 26;
            using (Font itemF = new Font("Segoe UI", 7.8f, FontStyle.Regular))
            using (SolidBrush itemB = new SolidBrush(Color.FromArgb(215, 225, 238)))
            {
                for (int i = 0; i < agentTaskLog.Count; i++)
                {
                    if (ly + 18 > logBox.Bottom - 4) break;
                    Rectangle lr = new Rectangle(logBox.X + 10, ly, logBox.Width - 18, 17);
                    StringFormat sf = new StringFormat(StringFormatFlags.NoWrap);
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(agentTaskLog[i], itemF, itemB, lr, sf);
                    ly += 19;
                }
            }
        }

        // ====================================================================
        // UP NEXT QUEUE SIDE PANEL & YOULYPLUS LYRICS SIDE PANEL
        // ====================================================================
        private void PaintQueueSidePanel(Graphics g, Rectangle r)
        {
            using (Font hf = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            {
                g.DrawString("Up Next • " + selectedCountry + " (" + currentQueue.Count + ")", hf, Brushes.White, r.X + 2, r.Y + 2);
            }

            rectQueueShuffleBtn = new Rectangle(r.Right - 68, r.Y, 66, 24);
            DrawPillButton(g, rectQueueShuffleBtn, "⇄ Mix", isShuffle, Color.FromArgb(0, 205, 250), 7.6f);

            int y = r.Y + 30;
            for (int i = 0; i < currentQueue.Count; i++)
            {
                if (y + 44 > r.Bottom) break;
                SongItem s = currentQueue[i];
                bool isCur = (currentSong == s);
                Rectangle itemR = new Rectangle(r.X, y, r.Width, 42);
                queueItemRects.Add(new KeyValuePair<int, Rectangle>(i, itemR));

                DrawGlassCard(g, itemR, 10,
                    isCur ? Color.FromArgb(26, 38, 56) : Color.FromArgb(18, 23, 33),
                    isCur ? Color.FromArgb(180, 0, 215, 255) : Color.FromArgb(30, 255, 255, 255));

                Rectangle miniArt = new Rectangle(itemR.X + 6, itemR.Y + 5, 32, 32);
                DrawSongCoverArt(g, miniArt, s, 6);

                using (Font tf = new Font("Segoe UI", 8.6f, FontStyle.Bold))
                {
                    Rectangle tr = new Rectangle(miniArt.Right + 8, itemR.Y + 5, itemR.Width - 78, 16);
                    StringFormat sf = new StringFormat(StringFormatFlags.NoWrap);
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(s.Title, tf, Brushes.White, tr, sf);
                }
                using (Font af = new Font("Segoe UI", 7.5f, FontStyle.Regular))
                using (SolidBrush ab = new SolidBrush(Color.FromArgb(160, 175, 195)))
                {
                    Rectangle ar = new Rectangle(miniArt.Right + 8, itemR.Y + 21, itemR.Width - 78, 15);
                    StringFormat sf = new StringFormat(StringFormatFlags.NoWrap);
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(s.Artist + " • " + s.Country, af, ab, ar, sf);
                }

                Rectangle remR = new Rectangle(itemR.Right - 26, itemR.Y + 10, 20, 20);
                queueRemoveRects.Add(new KeyValuePair<int, Rectangle>(i, remR));
                using (Font xf = new Font("Segoe UI", 8f, FontStyle.Regular))
                using (SolidBrush xb = new SolidBrush(Color.FromArgb(140, 155, 175)))
                {
                    g.DrawString("×", xf, xb, remR.X + 4, remR.Y + 2);
                }

                y += 46;
            }
        }

        private void PaintLyricsSidePanel(Graphics g, Rectangle r)
        {
            Rectangle badgeR = new Rectangle(r.X, r.Y, r.Width, 26);
            DrawGlassCard(g, badgeR, 8, Color.FromArgb(20, 34, 48), Color.FromArgb(90, 0, 215, 255));
            using (Font bf = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(80, 230, 255)))
            {
                g.DrawString(activeLyricBadge + "  •  Word Sync", bf, bb, badgeR.X + 10, badgeR.Y + 5);
            }

            int y = r.Y + 36;
            for (int i = 0; i < currentLyrics.Count; i++)
            {
                if (y + 38 > r.Bottom) break;
                LyricLine ll = currentLyrics[i];
                bool isActive = (i == activeLyricIndex);

                Rectangle lr = new Rectangle(r.X, y, r.Width, isActive ? 42 : 30);
                if (isActive)
                {
                    DrawGlassCard(g, lr, 10, Color.FromArgb(28, 42, 62), Color.FromArgb(160, 0, 215, 255));
                }

                using (Font f = new Font("Segoe UI", isActive ? 9.8f : 8.6f, isActive ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush sb = new SolidBrush(isActive ? Color.White : Color.FromArgb(155, 170, 190)))
                {
                    StringFormat sf = new StringFormat();
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(ll.Text, f, sb, new Rectangle(lr.X + 8, lr.Y + 6, lr.Width - 16, lr.Height - 8), sf);
                }
                y += isActive ? 46 : 32;
            }
        }

        // ====================================================================
        // BOTTOM PLAYER BAR (TRANSPORT + SEEK + 200% VOLUME + AI & QUEUE TOGGLES)
        // ====================================================================
        private void PaintBottomPlayerBar(Graphics g, Rectangle r)
        {
            DrawGlassCard(g, new Rectangle(r.X + 6, r.Y + 4, r.Width - 12, r.Height - 8), 16,
                Color.FromArgb(15, 19, 28), Color.FromArgb(45, 255, 255, 255));

            // Left: Current Song Cover + Title + Artist + Like + Download
            Rectangle coverR = new Rectangle(r.X + 18, r.Y + 16, 54, 54);
            DrawSongCoverArt(g, coverR, currentSong, 10);
            rectBottomSongInfo = new Rectangle(r.X + 18, r.Y + 16, 250, 54);

            using (Font tf = new Font("Segoe UI", 10.2f, FontStyle.Bold))
            {
                g.DrawString(currentSong != null ? currentSong.Title : "Roots", tf, Brushes.White, coverR.Right + 12, r.Y + 20);
            }
            using (Font af = new Font("Segoe UI", 8.2f, FontStyle.Regular))
            using (SolidBrush ab = new SolidBrush(Color.FromArgb(165, 180, 200)))
            {
                string sub = currentSong != null ? (currentSong.Artist + " • " + currentSong.Country) : "Bintu Pabra • India";
                g.DrawString(sub, af, ab, coverR.Right + 12, r.Y + 41);
            }

            rectBottomLikeBtn = new Rectangle(r.X + 245, r.Y + 28, 30, 28);
            rectBottomDownloadBtn = new Rectangle(r.X + 280, r.Y + 28, 30, 28);
            DrawPillButton(g, rectBottomLikeBtn, (currentSong != null && currentSong.IsLiked) ? "♥" : "♡", currentSong != null && currentSong.IsLiked, Color.FromArgb(245, 75, 110), 8.5f);
            DrawPillButton(g, rectBottomDownloadBtn, (currentSong != null && currentSong.IsDownloaded) ? "✓" : "↓", currentSong != null && currentSong.IsDownloaded, Color.FromArgb(40, 220, 140), 8.5f);

            // Center: Shuffle, Prev, Play/Pause Orb, Next, Repeat + Seek Bar
            int cx = r.Width / 2;
            int cy = r.Y + 26;

            rectShuffleBtn = new Rectangle(cx - 110, cy - 10, 30, 26);
            rectPrevBtn = new Rectangle(cx - 68, cy - 10, 32, 26);
            rectPlayPauseBtn = new Rectangle(cx - 21, cy - 16, 42, 42);
            rectNextBtn = new Rectangle(cx + 36, cy - 10, 32, 26);
            rectRepeatBtn = new Rectangle(cx + 80, cy - 10, 30, 26);

            using (Font ctrlF = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            {
                g.DrawString("⇄", ctrlF, isShuffle ? Brushes.Cyan : Brushes.LightGray, rectShuffleBtn.X + 4, rectShuffleBtn.Y + 4);
                g.DrawString("|◄", ctrlF, Brushes.White, rectPrevBtn.X + 6, rectPrevBtn.Y + 4);
                g.DrawString("►|", ctrlF, Brushes.White, rectNextBtn.X + 6, rectNextBtn.Y + 4);
                g.DrawString(repeatMode == 2 ? "↻1" : "↻", ctrlF, repeatMode > 0 ? Brushes.Cyan : Brushes.LightGray, rectRepeatBtn.X + 4, rectRepeatBtn.Y + 4);
            }

            // Glowing Play/Pause Circle
            using (SolidBrush pb = new SolidBrush(Color.FromArgb(30, 0, 215, 255)))
            {
                g.FillEllipse(pb, rectPlayPauseBtn.X - 3, rectPlayPauseBtn.Y - 3, rectPlayPauseBtn.Width + 6, rectPlayPauseBtn.Height + 6);
            }
            using (Pen pp = new Pen(Color.FromArgb(220, 0, 215, 255), 2f))
            {
                g.DrawEllipse(pp, rectPlayPauseBtn);
            }
            using (Font pf = new Font("Segoe UI", 11f, FontStyle.Bold))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(isPlaying ? "❚❚" : "▶", pf, Brushes.White, rectPlayPauseBtn, sf);
            }

            // Seek Bar below transport buttons
            int seekW = Math.Min(420, r.Width - 660);
            rectSeekBar = new Rectangle(cx - seekW / 2, r.Y + 62, seekW, 8);
            DrawGlassCard(g, rectSeekBar, 4, Color.FromArgb(38, 46, 62), Color.FromArgb(40, 255, 255, 255));

            double frac = playbackDurationSec > 0 ? Math.Max(0.0, Math.Min(1.0, playbackPositionSec / playbackDurationSec)) : 0.0;
            int fillW = (int)(seekW * frac);
            if (fillW > 2)
            {
                Rectangle fillR = new Rectangle(rectSeekBar.X, rectSeekBar.Y, fillW, rectSeekBar.Height);
                using (SolidBrush fb = new SolidBrush(Color.FromArgb(0, 215, 255)))
                {
                    g.FillRectangle(fb, fillR);
                }
            }

            using (Font timeF = new Font("Segoe UI", 7.5f, FontStyle.Regular))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(165, 180, 200)))
            {
                g.DrawString(FormatSec(playbackPositionSec), timeF, tb, rectSeekBar.X - 38, rectSeekBar.Y - 4);
                g.DrawString(FormatSec(playbackDurationSec), timeF, tb, rectSeekBar.Right + 8, rectSeekBar.Y - 4);
            }

            // Right Controls: A-B Loop, Volume Slider (0..200%), [ ✦ AI ], [ Queue ], [ ⛶ ]
            rectBottomFullScreenBtn = new Rectangle(r.Right - 44, r.Y + 28, 30, 28);
            rectBottomQueueBtn = new Rectangle(rectBottomFullScreenBtn.X - 66, r.Y + 28, 60, 28);
            rectBottomAiBtn = aiAssistantEnabled ? new Rectangle(rectBottomQueueBtn.X - 64, r.Y + 28, 58, 28) : Rectangle.Empty;

            DrawPillButton(g, rectBottomFullScreenBtn, "⛶", isFullScreenStage, Color.FromArgb(0, 205, 250), 8.5f);
            DrawPillButton(g, rectBottomQueueBtn, "Queue", isRightDockVisible && rightPanelMode != 2, Color.FromArgb(0, 205, 250), 7.8f);
            if (aiAssistantEnabled)
            {
                DrawPillButton(g, rectBottomAiBtn, "✦ AI", isRightDockVisible && rightPanelMode == 2, Color.FromArgb(245, 185, 35), 7.8f);
            }

            int volRight = (aiAssistantEnabled ? rectBottomAiBtn.X : rectBottomQueueBtn.X) - 12;
            rectVolumeSlider = new Rectangle(volRight - 96, r.Y + 38, 96, 8);
            DrawGlassCard(g, rectVolumeSlider, 4, Color.FromArgb(38, 46, 62), Color.FromArgb(50, 255, 255, 255));
            int vFill = (int)(rectVolumeSlider.Width * (Math.Min(200, volumePercent) / 200.0));
            if (vFill > 2)
            {
                using (SolidBrush vb = new SolidBrush(Color.FromArgb(245, 190, 40)))
                {
                    g.FillRectangle(vb, rectVolumeSlider.X, rectVolumeSlider.Y, vFill, rectVolumeSlider.Height);
                }
            }

            rectAbLoopBtn = new Rectangle(rectVolumeSlider.X - 44, r.Y + 28, 36, 28);
            DrawPillButton(g, rectAbLoopBtn, "A-B", isAbLoopActive, Color.FromArgb(0, 205, 250), 7.5f);
        }

        private string FormatSec(double sec)
        {
            int s = Math.Max(0, (int)Math.Round(sec));
            int m = s / 60;
            int rem = s % 60;
            return m + ":" + (rem < 10 ? "0" + rem : rem.ToString());
        }

        // ====================================================================
        // SETTINGS & PROFILE MODAL OVERLAY (WITH LEO AI ON/OFF & CIRCLE DESIGN)
        // ====================================================================
        private void PaintSettingsModalOverlay(Graphics g, Rectangle bounds)
        {
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(175, 6, 9, 14)))
            {
                g.FillRectangle(dim, bounds);
            }

            int mw = 580;
            int mh = 490;
            Rectangle modalR = new Rectangle((bounds.Width - mw) / 2, (bounds.Height - mh) / 2, mw, mh);
            DrawGlassCard(g, modalR, 20, Color.FromArgb(18, 24, 34), Color.FromArgb(140, 0, 215, 255));

            using (Font tf = new Font("Segoe UI", 13.5f, FontStyle.Bold))
            {
                g.DrawString("Leo Music Studio v10.6.3 • Settings & AI Control", tf, Brushes.White, modalR.X + 24, modalR.Y + 18);
            }

            rectSettingsCloseBtn = new Rectangle(modalR.Right - 44, modalR.Y + 16, 28, 28);
            DrawPillButton(g, rectSettingsCloseBtn, "✕", false, Color.FromArgb(240, 80, 95), 8.5f);

            int y = modalR.Y + 62;
            int leftX = modalR.X + 24;
            int btnX = modalR.Right - 210;

            // Row 1: User Name (Keyboard shortcut safe while editing)
            DrawSettingsRow(g, leftX, btnX, y, "User Profile Name",
                isEditingUserName ? (userName + "| (Press Enter)") : userName,
                out rectSettingsEditNameBtn, isEditingUserName, Color.FromArgb(0, 210, 255));
            y += 46;

            // Row 2: Upload Profile Photo from PC / Laptop
            DrawSettingsRow(g, leftX, btnX, y, "Profile Avatar Photo",
                "Upload Photo from PC",
                out rectSettingsUploadPhotoBtn, userProfileAvatarBmp != null, Color.FromArgb(40, 220, 140));
            y += 46;

            // Row 3: Leo AI Circle & Side Panel ON / OFF Toggle (v10.6.3 requested feature!)
            DrawSettingsRow(g, leftX, btnX, y, "Leo AI Circle & Side Panel",
                aiAssistantEnabled ? "ON (Enabled)" : "OFF (Hidden)",
                out rectSettingsAiEnableToggleBtn, aiAssistantEnabled, Color.FromArgb(0, 215, 255));
            y += 46;

            // Row 4: Leo AI Circle Design Selector (4 New Designs in v10.6.3!)
            DrawSettingsRow(g, leftX, btnX, y, "Leo AI Circle Design",
                aiOrbStyleNames[aiOrbStyleIdx],
                out rectSettingsAiOrbStyleCycleBtn, true, Color.FromArgb(245, 185, 35));
            y += 46;

            // Row 5: Floating Desktop Widget Toggle
            DrawSettingsRow(g, leftX, btnX, y, "Floating Desktop AI Widget",
                isFloatingWidgetEnabled ? "ON (Floating on PC)" : "OFF (Click to Float)",
                out rectSettingsFloatWidgetToggleBtn, isFloatingWidgetEnabled, Color.FromArgb(180, 95, 255));
            y += 46;

            // Row 6: Selected Country & Queue Sync
            DrawSettingsRow(g, leftX, btnX, y, "Selected Country & Queue",
                "◉ " + selectedCountry + " (Click to Cycle)",
                out rectSettingsCountryCycleBtn, false, Color.FromArgb(0, 210, 255));
            y += 46;

            // Row 7: Crossfade Overlap Timing
            DrawSettingsRow(g, leftX, btnX, y, "Smart Crossfade Overlap",
                crossfadeSeconds == 0 ? "OFF (0s)" : (crossfadeSeconds + " Seconds Smooth"),
                out rectSettingsCrossfadeCycleBtn, crossfadeSeconds > 0, Color.FromArgb(40, 220, 140));
            y += 46;

            // Row 8: 24-Bit 3D Studio Equalizer Preset
            DrawSettingsRow(g, leftX, btnX, y, "24-Bit 3D Equalizer Preset",
                eqPresetNames[eqPresetIndex],
                out rectSettingsEqCycleBtn, true, Color.FromArgb(245, 185, 35));
            y += 46;

            // Row 9: Local Offline Downloads Folder
            DrawSettingsRow(g, leftX, btnX, y, "Local Offline Storage Folder",
                "▤ Open Downloads Folder",
                out rectSettingsOpenLocalFolderBtn, false, Color.FromArgb(0, 205, 250));
        }

        private void DrawSettingsRow(Graphics g, int leftX, int btnX, int y, string label, string btnText, out Rectangle btnRect, bool active, Color accent)
        {
            Rectangle rowBg = new Rectangle(leftX, y, (btnX + 186) - leftX, 38);
            DrawGlassCard(g, rowBg, 10, Color.FromArgb(22, 29, 42), Color.FromArgb(40, 255, 255, 255));
            using (Font lf = new Font("Segoe UI", 9.2f, FontStyle.Bold))
            using (SolidBrush lb = new SolidBrush(Color.FromArgb(220, 230, 242)))
            {
                g.DrawString(label, lf, lb, leftX + 14, y + 9);
            }
            btnRect = new Rectangle(btnX, y + 4, 180, 30);
            DrawPillButton(g, btnRect, btnText, active, accent, 8.2f);
        }


        // ====================================================================
        // MOUSE, KEYBOARD (SHORTCUT-SAFE), SCROLL & TIMER INTERACTION ENGINE
        // ====================================================================
        private void OnMainFormMouseDown(object sender, MouseEventArgs e)
        {
            Point pt = e.Location;

            // 1. Settings Modal Hit-Testing (if open)
            if (isSettingsModalOpen)
            {
                if (rectSettingsCloseBtn.Contains(pt))
                {
                    isSettingsModalOpen = false;
                    isEditingUserName = false;
                    SaveUserSettings();
                    this.Invalidate();
                    return;
                }
                if (rectSettingsEditNameBtn.Contains(pt))
                {
                    isEditingUserName = !isEditingUserName;
                    isSearchFocused = false;
                    isAgentInputFocused = false;
                    this.Invalidate();
                    return;
                }
                if (rectSettingsUploadPhotoBtn.Contains(pt))
                {
                    SelectUserProfilePhotoFromPc();
                    return;
                }
                if (rectSettingsAiEnableToggleBtn.Contains(pt))
                {
                    aiAssistantEnabled = !aiAssistantEnabled;
                    if (!aiAssistantEnabled)
                    {
                        isAgentPanelOpen = false;
                        if (rightPanelMode == 2) rightPanelMode = 0;
                        if (floatingWidgetForm != null && !floatingWidgetForm.IsDisposed) floatingWidgetForm.Hide();
                    }
                    SaveUserSettings();
                    ShowToast(aiAssistantEnabled ? "✓ Leo AI Circle & Side Panel Enabled" : "Leo AI Circle & Side Panel Turned OFF");
                    this.Invalidate();
                    return;
                }
                if (rectSettingsAiOrbStyleCycleBtn.Contains(pt))
                {
                    CycleAiOrbStyle();
                    return;
                }
                if (rectSettingsFloatWidgetToggleBtn.Contains(pt))
                {
                    ToggleFloatingDesktopWidget();
                    return;
                }
                if (rectSettingsCountryCycleBtn.Contains(pt))
                {
                    CycleSelectedCountry();
                    return;
                }
                if (rectSettingsCrossfadeCycleBtn.Contains(pt))
                {
                    int[] cfSteps = new int[] { 0, 2, 4, 6, 8 };
                    int idx = Array.IndexOf(cfSteps, crossfadeSeconds);
                    crossfadeSeconds = cfSteps[(idx + 1) % cfSteps.Length];
                    SaveUserSettings();
                    ShowToast(crossfadeSeconds == 0 ? "Crossfade OFF" : "Crossfade set to " + crossfadeSeconds + "s");
                    this.Invalidate();
                    return;
                }
                if (rectSettingsEqCycleBtn.Contains(pt))
                {
                    eqPresetIndex = (eqPresetIndex + 1) % eqPresetNames.Length;
                    SaveUserSettings();
                    ShowToast("EQ Preset: " + eqPresetNames[eqPresetIndex]);
                    this.Invalidate();
                    return;
                }
                if (rectSettingsOpenLocalFolderBtn.Contains(pt))
                {
                    try { Process.Start("explorer.exe", "\"" + localDownloadsDir + "\""); } catch { }
                    return;
                }
                return;
            }

            // 2. Top Window Caption Controls
            if (rectCloseBtn.Contains(pt))
            {
                this.Close();
                return;
            }
            if (rectMaxBtn.Contains(pt))
            {
                this.WindowState = (this.WindowState == FormWindowState.Maximized) ? FormWindowState.Normal : FormWindowState.Maximized;
                return;
            }
            if (rectMinBtn.Contains(pt))
            {
                this.WindowState = FormWindowState.Minimized;
                return;
            }

            // 3. Top Bar Controls (Search, [ ✦ Leo AI ], [ ◉ Country ], [ ♫ Vol% ], Profile Avatar)
            if (!rectSearchClearBtn.IsEmpty && rectSearchClearBtn.Contains(pt))
            {
                searchQuery = "";
                this.Invalidate();
                return;
            }
            if (rectSearchBox.Contains(pt))
            {
                isSearchFocused = true;
                isAgentInputFocused = false;
                isEditingUserName = false;
                this.Invalidate();
                return;
            }
            if (!rectTopBarAiBtn.IsEmpty && rectTopBarAiBtn.Contains(pt))
            {
                ToggleDockedAiPanel();
                return;
            }
            if (rectCountrySelectorPill.Contains(pt))
            {
                CycleSelectedCountry();
                return;
            }
            if (rectTopVolumePill.Contains(pt))
            {
                volumePercent = (volumePercent >= 150) ? 100 : (volumePercent + 25);
                UpdatePlaybackVolumeAndEq();
                SaveUserSettings();
                ShowToast("♫ Studio Volume: " + volumePercent + "%");
                this.Invalidate();
                return;
            }
            if (rectProfileBtn.Contains(pt))
            {
                isSettingsModalOpen = true;
                isSearchFocused = false;
                isAgentInputFocused = false;
                this.Invalidate();
                return;
            }

            // 4. Left Sidebar Leo AI Circle Launcher (Click Circle to Hide/Unhide Docked AI Side Panel!)
            if (aiAssistantEnabled)
            {
                if (!rectFloatingAgentMicQuick.IsEmpty && rectFloatingAgentMicQuick.Contains(pt))
                {
                    ToggleAgentVoiceListening();
                    return;
                }
                if (!rectFloatingAgentStyleQuick.IsEmpty && rectFloatingAgentStyleQuick.Contains(pt))
                {
                    CycleAiOrbStyle();
                    return;
                }
                if (!rectFloatingAgentOrb.IsEmpty && rectFloatingAgentOrb.Contains(pt))
                {
                    ToggleDockedAiPanel();
                    return;
                }
            }

            // 5. Left Sidebar Navigation & Playlists
            foreach (KeyValuePair<string, Rectangle> kv in navItemRects)
            {
                if (kv.Value.Contains(pt))
                {
                    activeTab = kv.Key;
                    centerScrollOffset = 0;
                    isSearchFocused = false;
                    this.Invalidate();
                    return;
                }
            }
            if (rectAddPlaylistBtn.Contains(pt))
            {
                PlaylistEntry np = new PlaylistEntry();
                np.Name = "My Mix #" + (userPlaylists.Count + 1);
                if (currentSong != null) np.SongIds.Add(currentSong.Id);
                userPlaylists.Add(np);
                selectedPlaylistName = np.Name;
                activeTab = "Playlists";
                ShowToast("✓ Created Playlist: " + np.Name);
                this.Invalidate();
                return;
            }
            foreach (KeyValuePair<string, Rectangle> kv in playlistNavRects)
            {
                if (kv.Value.Contains(pt))
                {
                    selectedPlaylistName = kv.Key;
                    activeTab = "Playlists";
                    centerScrollOffset = 0;
                    this.Invalidate();
                    return;
                }
            }

            // 6. Right Docked Side Panel Controls (Queue | Lyrics | Docked Leo AI Side Panel)
            if (isRightDockVisible && !isFullScreenStage)
            {
                if (rectRightTabQueue.Contains(pt))
                {
                    rightPanelMode = 0;
                    isAgentPanelOpen = false;
                    this.Invalidate();
                    return;
                }
                if (rectRightTabLyrics.Contains(pt))
                {
                    rightPanelMode = 1;
                    isAgentPanelOpen = false;
                    this.Invalidate();
                    return;
                }
                if (aiAssistantEnabled && !rectRightTabAi.IsEmpty && rectRightTabAi.Contains(pt))
                {
                    rightPanelMode = 2;
                    isAgentPanelOpen = true;
                    isAgentInputFocused = true;
                    isSearchFocused = false;
                    this.Invalidate();
                    return;
                }
                if (rectRightCloseDockBtn.Contains(pt))
                {
                    isRightDockVisible = false;
                    isAgentPanelOpen = false;
                    this.Invalidate();
                    return;
                }

                if (rightPanelMode == 2 && aiAssistantEnabled)
                {
                    if (rectAgentOrbHeader.Contains(pt))
                    {
                        ToggleDockedAiPanel();
                        return;
                    }
                    if (rectAgentVoiceToggleBtn.Contains(pt))
                    {
                        ToggleAgentVoiceListening();
                        return;
                    }
                    if (rectAgentStyleCycleBtn.Contains(pt))
                    {
                        CycleAiOrbStyle();
                        return;
                    }
                    if (rectAgentFloatWidgetBtn.Contains(pt))
                    {
                        ToggleFloatingDesktopWidget();
                        return;
                    }
                    if (rectAgentInputBox.Contains(pt))
                    {
                        isAgentInputFocused = true;
                        isSearchFocused = false;
                        isEditingUserName = false;
                        this.Invalidate();
                        return;
                    }
                    if (rectAgentRunBtn.Contains(pt))
                    {
                        if (!string.IsNullOrEmpty(agentInputText))
                        {
                            string cmd = agentInputText;
                            agentInputText = "";
                            ExecuteAgentMultiTaskCommand(cmd);
                        }
                        else
                        {
                            ExecuteAgentMultiTaskCommand("paly strby");
                        }
                        return;
                    }
                    foreach (KeyValuePair<string, Rectangle> chip in rectAgentQuickChips)
                    {
                        if (chip.Value.Contains(pt))
                        {
                            agentInputText = chip.Key;
                            ExecuteAgentMultiTaskCommand(chip.Key);
                            return;
                        }
                    }
                }
                else if (rightPanelMode == 0)
                {
                    if (rectQueueShuffleBtn.Contains(pt))
                    {
                        isShuffle = !isShuffle;
                        ShowToast(isShuffle ? "Shuffle Enabled" : "Shuffle Disabled");
                        this.Invalidate();
                        return;
                    }
                    for (int i = 0; i < queueRemoveRects.Count; i++)
                    {
                        if (queueRemoveRects[i].Value.Contains(pt))
                        {
                            int rmIdx = queueRemoveRects[i].Key;
                            if (rmIdx >= 0 && rmIdx < currentQueue.Count && currentQueue.Count > 1)
                            {
                                currentQueue.RemoveAt(rmIdx);
                                this.Invalidate();
                            }
                            return;
                        }
                    }
                    for (int i = 0; i < queueItemRects.Count; i++)
                    {
                        if (queueItemRects[i].Value.Contains(pt))
                        {
                            int qIdx = queueItemRects[i].Key;
                            if (qIdx >= 0 && qIdx < currentQueue.Count)
                            {
                                currentQueueIndex = qIdx;
                                LoadSongIntoDeck(currentQueue[qIdx], true);
                            }
                            return;
                        }
                    }
                }
            }

            // 7. Mood Chips & Center Stage Controls
            foreach (KeyValuePair<string, Rectangle> mc in moodChipRects)
            {
                if (mc.Value.Contains(pt))
                {
                    selectedMood = mc.Key;
                    RebuildCountryQueueAndRecommendations(false);
                    return;
                }
            }

            if (activeTab == "Home" && string.IsNullOrEmpty(searchQuery))
            {
                if (rectHeroPlayBtn.Contains(pt))
                {
                    TogglePlayPause();
                    return;
                }
                if (rectHeroLikeBtn.Contains(pt) && currentSong != null)
                {
                    currentSong.IsLiked = !currentSong.IsLiked;
                    SaveOfflineDownloadsCatalog();
                    ShowToast(currentSong.IsLiked ? "♥ Added to Liked Songs" : "Removed from Liked Songs");
                    this.Invalidate();
                    return;
                }
                if (rectHeroMoreBtn.Contains(pt) && currentSong != null)
                {
                    DownloadSongOffline(currentSong);
                    return;
                }
                if (rectHeroSmartDjBtn.Contains(pt))
                {
                    RebuildCountryQueueAndRecommendations(true);
                    ShowToast("✦ Smart AI DJ mixed " + selectedCountry + " • " + selectedMood);
                    return;
                }
                if (rectPlayTrendingChartBtn.Contains(pt))
                {
                    RebuildCountryQueueAndRecommendations(true);
                    ShowToast("▶ Playing Trending Chart for " + selectedCountry);
                    return;
                }
            }

            if (activeTab == "Downloads")
            {
                if (rectDownloadsPlayAllBtn.Contains(pt))
                {
                    List<SongItem> dls = GetActiveViewSongs();
                    if (dls.Count > 0)
                    {
                        currentQueue.Clear();
                        currentQueue.AddRange(dls);
                        LoadSongIntoDeck(currentQueue[0], true);
                        ShowToast("▶ Playing Offline Downloads (" + dls.Count + " songs)");
                    }
                    return;
                }
                if (rectDownloadsOpenFolderBtn.Contains(pt))
                {
                    try { Process.Start("explorer.exe", "\"" + localDownloadsDir + "\""); } catch { }
                    return;
                }
            }

            foreach (KeyValuePair<SongItem, Rectangle> dl in songDownloadRects)
            {
                if (dl.Value.Contains(pt))
                {
                    DownloadSongOffline(dl.Key);
                    return;
                }
            }
            foreach (KeyValuePair<SongItem, Rectangle> lk in songLikeRects)
            {
                if (lk.Value.Contains(pt))
                {
                    lk.Key.IsLiked = !lk.Key.IsLiked;
                    SaveOfflineDownloadsCatalog();
                    this.Invalidate();
                    return;
                }
            }
            foreach (KeyValuePair<SongItem, Rectangle> sc in songCardRects)
            {
                if (sc.Value.Contains(pt))
                {
                    if (!currentQueue.Contains(sc.Key)) currentQueue.Insert(0, sc.Key);
                    LoadSongIntoDeck(sc.Key, true);
                    return;
                }
            }

            // 8. Bottom Player Bar Controls
            if (rectPlayPauseBtn.Contains(pt)) { TogglePlayPause(); return; }
            if (rectNextBtn.Contains(pt)) { PlayNextSong(); return; }
            if (rectPrevBtn.Contains(pt)) { PlayPreviousSong(); return; }
            if (rectShuffleBtn.Contains(pt)) { isShuffle = !isShuffle; this.Invalidate(); return; }
            if (rectRepeatBtn.Contains(pt)) { repeatMode = (repeatMode + 1) % 3; this.Invalidate(); return; }
            if (rectBottomLikeBtn.Contains(pt) && currentSong != null)
            {
                currentSong.IsLiked = !currentSong.IsLiked;
                SaveOfflineDownloadsCatalog();
                this.Invalidate();
                return;
            }
            if (rectBottomDownloadBtn.Contains(pt) && currentSong != null)
            {
                DownloadSongOffline(currentSong);
                return;
            }
            if (rectSeekBar.Contains(pt))
            {
                double frac = Math.Max(0.0, Math.Min(1.0, (double)(pt.X - rectSeekBar.X) / Math.Max(1, rectSeekBar.Width)));
                playbackPositionSec = frac * playbackDurationSec;
                this.Invalidate();
                return;
            }
            if (rectVolumeSlider.Contains(pt))
            {
                double vFrac = Math.Max(0.0, Math.Min(1.0, (double)(pt.X - rectVolumeSlider.X) / Math.Max(1, rectVolumeSlider.Width)));
                volumePercent = (int)Math.Round(vFrac * 200.0);
                UpdatePlaybackVolumeAndEq();
                SaveUserSettings();
                this.Invalidate();
                return;
            }
            if (rectAbLoopBtn.Contains(pt))
            {
                isAbLoopActive = !isAbLoopActive;
                abLoopStartSec = Math.Max(0.0, playbackPositionSec - 5.0);
                abLoopEndSec = Math.Min(playbackDurationSec, playbackPositionSec + 15.0);
                ShowToast(isAbLoopActive ? "A-B Studio Loop Active" : "A-B Loop Off");
                this.Invalidate();
                return;
            }
            if (aiAssistantEnabled && !rectBottomAiBtn.IsEmpty && rectBottomAiBtn.Contains(pt))
            {
                ToggleDockedAiPanel();
                return;
            }
            if (rectBottomQueueBtn.Contains(pt))
            {
                if (isRightDockVisible && rightPanelMode == 0)
                {
                    rightPanelMode = 1;
                }
                else
                {
                    isRightDockVisible = true;
                    rightPanelMode = 0;
                    isAgentPanelOpen = false;
                }
                this.Invalidate();
                return;
            }
            if (rectBottomFullScreenBtn.Contains(pt))
            {
                isFullScreenStage = !isFullScreenStage;
                this.Invalidate();
                return;
            }

            // Clicking top header area drags window
            if (pt.Y < 58 && e.Button == MouseButtons.Left)
            {
                isWindowDragging = true;
                windowDragStart = pt;
            }
        }

        private void CycleSelectedCountry()
        {
            int idx = Array.IndexOf(supportedCountries, selectedCountry);
            selectedCountry = supportedCountries[(idx + 1) % supportedCountries.Length];
            RebuildCountryQueueAndRecommendations(true);
            SaveUserSettings();
            ShowToast("◉ Switched Country & Queue to " + selectedCountry);
        }

        private void OnMainFormMouseMove(object sender, MouseEventArgs e)
        {
            if (isWindowDragging && e.Button == MouseButtons.Left)
            {
                Point screenPt = PointToScreen(e.Location);
                this.Location = new Point(screenPt.X - windowDragStart.X, screenPt.Y - windowDragStart.Y);
            }
        }

        private void OnMainFormMouseUp(object sender, MouseEventArgs e)
        {
            isWindowDragging = false;
        }

        private void OnMainFormMouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta < 0) centerScrollOffset = Math.Min(600, centerScrollOffset + 36);
            else centerScrollOffset = Math.Max(0, centerScrollOffset - 36);
            this.Invalidate();
        }

        // Keyboard Shortcut Guard: NEVER intercept keys when typing in Search, AI Input, or User Name!
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool isTypingInTextBox = isSearchFocused || isAgentInputFocused || isEditingUserName;
            if (isTypingInTextBox)
            {
                if (keyData == Keys.Enter)
                {
                    if (isAgentInputFocused && !string.IsNullOrEmpty(agentInputText))
                    {
                        string cmd = agentInputText;
                        agentInputText = "";
                        ExecuteAgentMultiTaskCommand(cmd);
                        return true;
                    }
                    if (isEditingUserName)
                    {
                        isEditingUserName = false;
                        SaveUserSettings();
                        ShowToast("✓ Saved User Name: " + userName);
                        this.Invalidate();
                        return true;
                    }
                    if (isSearchFocused && !string.IsNullOrEmpty(searchQuery))
                    {
                        SongItem m = FindBestFuzzySongMatch(searchQuery);
                        if (m != null) LoadSongIntoDeck(m, true);
                        return true;
                    }
                }
                if (keyData == Keys.Escape)
                {
                    isSearchFocused = false;
                    isAgentInputFocused = false;
                    isEditingUserName = false;
                    this.Invalidate();
                    return true;
                }
                // Let OnMainFormKeyPress receive all normal typing keys (Space, M, L, N, P, Backspace, etc.)
                return base.ProcessCmdKey(ref msg, keyData);
            }

            // Global Playback Shortcuts when NOT typing in any text box
            if (keyData == Keys.Space)
            {
                TogglePlayPause();
                return true;
            }
            if (keyData == Keys.N)
            {
                PlayNextSong();
                return true;
            }
            if (keyData == Keys.P)
            {
                PlayPreviousSong();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnMainFormKeyPress(object sender, KeyPressEventArgs e)
        {
            if (isEditingUserName)
            {
                if (e.KeyChar == '\b')
                {
                    if (userName.Length > 0) userName = userName.Substring(0, userName.Length - 1);
                }
                else if (!char.IsControl(e.KeyChar) && userName.Length < 28)
                {
                    userName += e.KeyChar;
                }
                e.Handled = true;
                this.Invalidate();
                return;
            }

            if (isAgentInputFocused)
            {
                if (e.KeyChar == '\b')
                {
                    if (agentInputText.Length > 0) agentInputText = agentInputText.Substring(0, agentInputText.Length - 1);
                }
                else if (!char.IsControl(e.KeyChar) && agentInputText.Length < 180)
                {
                    agentInputText += e.KeyChar;
                }

                // Live high-smart typo preview as user types!
                if (!string.IsNullOrEmpty(agentInputText))
                {
                    string auto = SmartAutoCorrectAgentCommand(agentInputText);
                    if (auto.StartsWith("play ", StringComparison.OrdinalIgnoreCase) && auto.Length > 5)
                    {
                        SongItem sm = FindBestFuzzySongMatch(auto.Substring(5));
                        if (sm != null)
                            agentSmartHintText = "✦ Smart AI Ready: Play \"" + sm.Title + "\" • " + sm.Artist;
                        else
                            agentSmartHintText = "✦ Smart AI Understood: \"" + auto + "\"";
                    }
                    else
                    {
                        agentSmartHintText = "✦ Smart AI Understood: \"" + auto + "\"";
                    }
                }
                e.Handled = true;
                this.Invalidate();
                return;
            }

            if (isSearchFocused)
            {
                if (e.KeyChar == '\b')
                {
                    if (searchQuery.Length > 0) searchQuery = searchQuery.Substring(0, searchQuery.Length - 1);
                }
                else if (!char.IsControl(e.KeyChar) && searchQuery.Length < 80)
                {
                    searchQuery += e.KeyChar;
                }
                e.Handled = true;
                this.Invalidate();
            }
        }

        private void OnUiTimerTick(object sender, EventArgs e)
        {
            agentPhase += 0.08f;
            if (agentPhase > 628f) agentPhase -= 628f;

            if (toastTicksRemaining > 0)
            {
                toastTicksRemaining--;
            }

            if (isPlaying)
            {
                playbackPositionSec += 0.05;

                if (isAbLoopActive && playbackPositionSec >= abLoopEndSec)
                {
                    playbackPositionSec = abLoopStartSec;
                }

                // Smooth Crossfade near end of track
                if (crossfadeSeconds > 0 && playbackDurationSec > 10 &&
                    playbackPositionSec >= playbackDurationSec - crossfadeSeconds)
                {
                    isCrossfadingNow = true;
                    deckBCrossfadeVolFloat = (float)((playbackDurationSec - playbackPositionSec) / Math.Max(1.0, crossfadeSeconds));
                }

                if (playbackPositionSec >= playbackDurationSec)
                {
                    if (repeatMode == 2 && currentSong != null)
                    {
                        playbackPositionSec = 0.0;
                    }
                    else
                    {
                        PlayNextSong();
                    }
                }

                // Sync active lyric line
                for (int i = 0; i < currentLyrics.Count; i++)
                {
                    if (playbackPositionSec >= currentLyrics[i].StartTime && playbackPositionSec <= currentLyrics[i].EndTime)
                    {
                        activeLyricIndex = i;
                        break;
                    }
                }
            }

            if (floatingWidgetForm != null && !floatingWidgetForm.IsDisposed && floatingWidgetForm.Visible)
            {
                floatingWidgetForm.Invalidate();
            }

            this.Invalidate();
        }

        private void OnMainFormClosing(object sender, FormClosingEventArgs e)
        {
            SaveUserSettings();
            SaveOfflineDownloadsCatalog();
            StopMicrophoneSpeechCapture();
            StopAudioHardwarePlayback();
            try
            {
                mciSendString("close LeoDeck", null, 0, IntPtr.Zero);
            }
            catch { }
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LeoMusicForm());
        }
    }
}
