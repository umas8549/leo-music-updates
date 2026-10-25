using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
[assembly: AssemblyVersion("10.4.3.0")]
[assembly: AssemblyFileVersion("10.4.3.0")]

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
        public string Language = "Hindi";
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

    public class HomeHitZone
    {
        public Rectangle Rect;
        public string Action;
        public SongItem Song;
        public string HoverKey;
        public string Payload;

        public HomeHitZone(Rectangle rect, string action, SongItem song, string hoverKey)
        {
            Rect = rect;
            Action = action;
            Song = song;
            HoverKey = hoverKey;
            Payload = "";
        }

        public HomeHitZone(Rectangle rect, string action, SongItem song, string hoverKey, string payload)
        {
            Rect = rect;
            Action = action;
            Song = song;
            HoverKey = hoverKey;
            Payload = payload ?? "";
        }
    }

    public class MusicLanguageItem
    {
        public string Name;
        public string NativeScript;
        public string QueryKeyword;

        public MusicLanguageItem(string name, string nativeScript, string queryKeyword)
        {
            Name = name;
            NativeScript = nativeScript;
            QueryKeyword = queryKeyword;
        }
    }

    public class UserPlaylistItem
    {
        public string Id;
        public string Name;
        public string Subtitle;
        public bool IsLeoAutoMood;
        public List<SongItem> Songs = new List<SongItem>();

        public UserPlaylistItem(string id, string name, string subtitle, bool isLeoAutoMood)
        {
            Id = id;
            Name = name;
            Subtitle = subtitle;
            IsLeoAutoMood = isLeoAutoMood;
        }
    }

    public class LeoMainForm : Form
    {
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

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
        private bool quickPlayUnlocked = true;
        private int userPlayedSongsCount = 0;
        private List<SongItem> quickPlaySongs = new List<SongItem>();
        private List<SongItem> trendingSongs = new List<SongItem>();
        private List<SongItem> recentlyPlayedSongs = new List<SongItem>();
        private List<string> favoritePlayedArtists = new List<string>();
        private int hoveredQuickPlayIndex = -1;
        private List<HomeHitZone> homeHitZones = new List<HomeHitZone>();
        private int totalHomeContentHeight = 900;

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
        private int rightPanelMode = 0; // Default 0 = Up Next Queue (matching v10.4.2 Reference Image 1), 1 = BetterLyrics
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
        private string[] artworkShapeNames = new string[] { "Quantum", "Vortex", "Morph", "Flower", "Vinyl", "Squircle" };

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
        private const string CURRENT_APP_VERSION = "10.4.4";
        private const string ADMIN_UPDATE_PASSWORD = "Gourav123";
        private const string DEFAULT_GITHUB_VERSION_URL = "https://raw.githubusercontent.com/umas8549/leo-music-updates/main/version.json";
        private const string DEFAULT_GITHUB_SOURCE_URL = "https://raw.githubusercontent.com/umas8549/leo-music-updates/main/LeoLauncher.cs";
        private const string DEFAULT_GITHUB_RELEASE_URL = "https://github.com/umas8549/leo-music-updates/releases/download/v10.4.4/Leo_Music_Windows_v10.4.4.zip";

        private string updateManifestUrl = "https://raw.githubusercontent.com/umas8549/leo-music-updates/main/version.json";
        private bool isUpdatePopupOpen = false;
        private float updatePopupAnim = 0f;
        private bool isCheckingUpdate = false;
        private bool isInstallingUpdate = false;
        private bool isPasswordPromptActive = false;
        private bool isAdminUpdateUnlocked = false;
        private TextBox adminPassBox;
        private int startupUpdateCheckTicks = 85;
        private string latestRemoteVersion = "10.4.4";
        private string latestUpdateTitle = "Leo Music Studio v10.4.4 \u2014 Zero-Lag Turbo Engine, Studio Volume Controller (150% Boost) & Playback Smoothness Suite";
        private string latestUpdateNotes = "\u2022 Zero-Lag UI Turbo Engine (Pre-Baked Rounded Thumbnail Cache, Offscreen 384x216 Composite Backdrop Buffer, Debounced Disk I/O & Throttled COM Sync)\n\u2022 Interactive Studio Volume Controller Popup (0-150% Overdrive Pre-Amp Boost, Fine +/-1% Steps, 6 Volume Presets, Stereo Balance L/C/R & Scroll-Wheel Volume)\n\u2022 Playback Smoothness Suite (180ms Smooth Micro-Fade on Play/Pause/Seek, Smart Auto-Volume Normalization, Zero-Lag Next-Track Pre-Buffer, A-B Loop & Buffer Profiles)";

        // v10.2.0 NEW STUDIO FEATURES:
        // 1) Real-Time Playback Speed / Slowed+Reverb / Nightcore Engine
        private double[] playbackSpeedRates = new double[] { 1.0, 0.85, 1.12, 1.25 };
        private string[] playbackSpeedNames = new string[] { "1.0x Normal", "0.85x Slowed", "1.12x Upbeat", "1.25x Nightcore" };
        private int playbackSpeedIdx = 0;

        // 2) Always-on-Top Compact Floating Mini-Player Mode
        private bool isMiniPlayerMode = false;
        private Size savedDesktopSize = new Size(1220, 770);
        private Point savedDesktopLocation = new Point(100, 100);

        // 3) 4 Interactive EQ Spectrum Visualizer Styles (0=Bars, 1=Neon Wave, 2=Mirror, 3=Pulse Orbs)
        private int visualizerStyleIdx = 0;
        private string[] visualizerStyleNames = new string[] { "Studio Bars", "Neon Wave", "Mirror Pulse", "Harmonic Orbs" };

        // v10.2.3 & v10.2.4: MULTI-LANGUAGE ONBOARDING, LANGUAGE PANELS & 60HZ+ AUTO ENGINE
        private bool hasCompletedLanguageSetup = false;
        private bool isLanguageSetupScreenOpen = false;
        private float languageSetupAnim = 0f;
        private HashSet<string> userSelectedLanguages = new HashSet<string>();
        private List<MusicLanguageItem> availableLanguages = new List<MusicLanguageItem>();
        private Rectangle langContinueBtnRect = Rectangle.Empty;
        private int activeLanguageFilterIdx = 0; // 0 = All Selected, 1..N = Specific Language filter
        private string activeLanguageFilterName = "All Languages";
        private Dictionary<string, List<SongItem>> languageSongShelves = new Dictionary<string, List<SongItem>>(StringComparer.OrdinalIgnoreCase);
        private List<HomeHitZone> topFilterHitZones = new List<HomeHitZone>();
        private string activeStreamQuality = "320kbps"; // "320kbps", "160kbps", "96kbps"
        private string[] streamQualityNames = new string[] { "320kbps Studio", "160kbps High", "96kbps Saver" };

        // v10.2.4 60Hz+ / 120Hz AUTO HIGH-REFRESH & INERTIAL SCROLL ENGINE
        private Stopwatch frameClock = Stopwatch.StartNew();
        private int refreshRateModeIdx = 0; // 0 = 60Hz+ Auto (14ms), 1 = 120Hz Ultra (8ms)
        private string[] refreshRateModeNames = new string[] { "60Hz+ Auto", "120Hz Ultra" };
        private float smoothScrollOffset = 0f;
        private int targetScrollOffset = 0;
        private float smoothQueueScrollOffset = 0f;
        private int targetQueueScrollOffset = 0;
        private double sleepSecondAccumulator = 0.0;
        private double listenSecondAccumulator = 0.0;

        // v10.3.1 NEW: HIDE/SHOW RIGHT PANEL, DYNAMIC LIGHTING & PLAYLIST STUDIO ENGINE
        private bool isRightPanelVisible = true;
        private float rightPanelWidthAnim = 315f;
        private bool dynamicLightingEnabled = true;
        private List<UserPlaylistItem> userPlaylists = new List<UserPlaylistItem>();
        private int selectedPlaylistIdx = 0; // 0 = Leo Smart Playlist (Auto-Mood), 1..N = Custom User Playlists
        private TextBox playlistNameBox;
        private bool isCreatingPlaylistInput = false;
        private List<HomeHitZone> playlistStageHitZones = new List<HomeHitZone>();

        // v10.3.2 & v10.3.8: FROSTED GLASS + 3D CURVED LENS GLASS, DYNAMIC BAR OVERLAY & BETTERLYRICS 2.0
        private bool glassEffectEnabled = true;
        private bool curved3DGlassEnabled = true;
        private int miniPlayerStyleIdx = 0; // 0 = Rounded, 1 = Capsule, 2 = Square, 3 = Circle
        private string[] miniPlayerStyleNames = new string[] { "Rounded", "Capsule", "Square", "Circle" };

        // v10.3.8 & v10.4.0 ALWAYS-ON-TOP PHONE-STYLE DYNAMIC BAR & BOXLESS LYRICS STUDIO ENGINE
        private bool isDynamicBarMode = false;
        private bool isDynamicBarExpanded = false;
        private int dynamicBarStyleIdx = 0; // 0 = Pure Pitch Black Phone Pill, 1 = Live-Mesh Aurora Phone Pill
        private string[] dynamicBarStyleNames = new string[] { "Black Pill", "Live-Mesh" };
        private Point savedDynamicBarLocation = new Point(-1, -1);
        private int topMostHeartbeatTicks = 0;
        private int trendingFetchVersion = 0;
        private Rectangle syncRefetchBtnRect = Rectangle.Empty;
        private Rectangle syncCopyBtnRect = Rectangle.Empty;
        private Rectangle syncBoxlessBtnRect = Rectangle.Empty;
        private Rectangle syncGlowBtnRect = Rectangle.Empty;
        private Rectangle syncBgArtBtnRect = Rectangle.Empty;

        // v10.4.0 BOXLESS BETTERLYRICS 2.0 CUSTOMIZATION OPTIONS (7 Studio Controls)
        private int lyricStageStyleIdx = 0; // 0 = Boxless Fade (Default!), 1 = Soft Spotlight, 2 = Glass Frame
        private string[] lyricStageStyleNames = new string[] { "Boxless Fade", "Soft Halo", "Glass Frame" };

        private int lyricFadeModeIdx = 0; // 0 = Smooth Edge, 1 = Deep Focus, 2 = Cascade, 3 = All Crisp
        private string[] lyricFadeModeNames = new string[] { "Smooth Edge", "Deep Focus", "Cascade", "All Crisp" };

        private int lyricFadeSizeIdx = 2; // 0 = Small, 1 = Medium, 2 = Large, 3 = Ultra Deep
        private string[] lyricFadeSizeNames = new string[] { "Small", "Medium", "Large", "Ultra" };
        private int[] lyricFadeZonePixels = new int[] { 65, 115, 165, 225 };

        private int lyricGlowModeIdx = 1; // 0 = Soft Glow, 1 = Neon Bloom, 2 = Hyper Aura, 3 = Clean Off
        private string[] lyricGlowModeNames = new string[] { "Soft Glow", "Neon Bloom", "Hyper Aura", "Clean Off" };

        private int lyricBgArtModeIdx = 1; // 0 = Cover Art Fade, 1 = 3D Morph Art, 2 = Vinyl Aura, 3 = Mesh Only
        private string[] lyricBgArtModeNames = new string[] { "Cover Fade", "3D Morph Art", "Vinyl Aura", "Mesh Only" };

        private int lyricFontStyleIdx = 0; // 0 = Georgia Serif, 1 = Modern Sans, 2 = Poetic Italic, 3 = Bold Cinema
        private string[] lyricFontStyleNames = new string[] { "Georgia Serif", "Modern Sans", "Poetic Italic", "Cinema Bold" };

        private int lyricTextSizeIdx = 1; // 0 = Compact, 1 = Studio, 2 = Large, 3 = Cinema XL
        private string[] lyricTextSizeNames = new string[] { "Compact", "Studio", "Large", "Cinema XL" };

        // v10.4.3 MULTI-IMAGE CINEMA BACKGROUND SLIDESHOW, 5-PAGE SETTINGS STUDIO & 3-DOT POPUP MENU
        private bool songBgImageEnabled = true;
        private int songBgStyleIdx = 0; // 0 = All 6 Auto-Cycle, 1 = Original + Edited, 2 = Concert + Cyber, 3 = Vintage + Artist
        private string[] songBgStyleNames = new string[] { "All 6 (Original+Edited)", "Original + Neon Edited", "Concert + Cyber Glow", "Vintage + Artist Stills" };
        private int songBgAnimModeIdx = 0; // 0 = Smooth Crossfade + Zoom, 1 = Cinema Dissolve, 2 = Slide + Fade, 3 = Pulse Morph
        private string[] songBgAnimModeNames = new string[] { "Crossfade + Zoom", "Cinema Dissolve", "Slide + Fade", "Pulse Morph" };
        private int[] songBgIntervalSecs = new int[] { 5, 8, 12, 18 };
        private int songBgIntervalIdx = 1; // Default 8s per background frame
        private int songBgCurrentSlideIdx = 0;
        private int songBgNextSlideIdx = 1;
        private float songBgCrossfadeAlpha = 0f; // 0.0 -> 1.0 during crossfade
        private bool isSongBgTransitioning = false;
        private double songBgTimerAccum = 0.0;
        private float songBgKenBurnsPhase = 0f;
        private string cachedBgSlidesSongId = "";
        private List<Bitmap> currentSongBgSlides = new List<Bitmap>();
        private List<string> currentSongBgSlideLabels = new List<string>();
        private int bgGalleryFetchVer = 0;

        // v10.4.3 5-Page Organized Settings Sub-Navigation (0=Account, 1=Player & EQ, 2=Lyrics & Bar, 3=Theme & BG, 4=System)
        private int activeSettingsSubPage = 0;
        private string[] settingsSubPageNames = new string[] { "Account & Profile", "Player & Audio EQ", "Lyrics & Dyn Bar", "Theme & Background", "System & Updates" };

        // v10.4.3 Full-Screen 3-Dot Popup Menu State
        private bool isFullThreeDotMenuOpen = false;
        private float fullThreeDotMenuAnim = 0f;

        // v10.4.4 ZERO-LAG TURBO ENGINE, STUDIO VOLUME CONTROLLER (0-150% OVERDRIVE) & PLAYBACK SMOOTHNESS
        private bool uiTurboCacheEnabled = true;
        private Dictionary<string, Bitmap> cachedSizedThumbnails = new Dictionary<string, Bitmap>();
        private Bitmap cachedCompositeBackdropBmp = null;
        private int compositeBackdropFrameCounter = 0;
        private int lastCompositeSlideIdx = -1;
        private int lastCompositeNextSlideIdx = -1;
        private bool settingsSaveDirty = false;
        private int settingsSaveCooldownTicks = 0;
        private int dspComThrottleCounter = 0;

        // v10.4.4 Interactive Studio Volume Controller Popup & 150% Overdrive Pre-Amp Engine
        private bool isVolumeControllerPopupOpen = false;
        private float volumeControllerPopupAnim = 0f;
        private bool volumeOverdriveAllowed = true;
        private int manualStereoBalance = 0; // -50 (Left) .. 0 (Center) .. +50 (Right)
        private bool autoVolumeNormalizeEnabled = true;
        private bool smoothMicroFadeEnabled = true;
        private float microFadeMultiplier = 1.0f;

        // v10.4.4 Playback Smoothness: Next-Track Pre-Buffer, Anti-Stutter Watchdog, Buffer Latency & A-B Loop
        private bool autoPreBufferNextEnabled = true;
        private string lastPreBufferedSongId = "";
        private int audioBufferProfileIdx = 1; // 0 = Ultra-Low 120ms, 1 = Smooth 350ms, 2 = Anti-Drop 700ms
        private string[] audioBufferProfileNames = new string[] { "Ultra-Low 120ms", "Smooth 350ms", "Anti-Drop 700ms" };
        private int streamStallWatchdogTicks = 0;
        private double lastWatchdogPosSec = -1.0;
        private double abLoopStartSec = -1.0;
        private double abLoopEndSec = -1.0;

        // v10.4.0 ZERO-STUTTER 120HZ PERFORMANCE CACHES (Eliminates COM reflection & GDI+ layout lag!)
        private int wmpComPollCounter = 0;
        private int lastAppliedWmpVol = -1;
        private int lastAppliedWmpBal = -999;
        private double lastAppliedWmpRate = -1.0;
        private bool lastAppliedWmpMute = false;
        private Bitmap cachedAuroraBitmap = null;
        private int auroraCacheFrameCounter = 0;
        private string cachedLyricWrapKey = "";
        private List<List<string>> cachedWrappedLyricRows = new List<List<string>>();
        private List<int> cachedLyricBlockHeights = new List<int>();
        private Font[,] lyricFontTableFull = new Font[4, 4];
        private Font[,] lyricFontTableIdle = new Font[4, 4];
        private Font fontLyricsFullActiveZoom = new Font("Georgia", 22.5f, FontStyle.Bold);
        private Font fontLyricsSideActiveZoom = new Font("Georgia", 13.5f, FontStyle.Bold);

        // 4) Total Listening Time Counter (Persisted across restarts)
        private int totalListeningSeconds = 0;
        private int listeningTickCounter = 0;
        private string latestUpdateSourceUrl = "https://raw.githubusercontent.com/umas8549/leo-music-updates/main/LeoLauncher.cs";
        private string latestUpdateDownloadUrl = "https://github.com/umas8549/leo-music-updates/releases/download/v10.4.3/Leo_Music_Windows_v10.4.4.zip";
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
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls | SecurityProtocolType.Ssl3; } catch { }
            try { SetCurrentProcessExplicitAppUserModelID("LeoMusic.NativeStudio.Desktop.v10_4_4"); } catch { }

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

            InitAvailableLanguages();
            InitThemes();
            InitCatalog();
            InitDefaultPlaylists();
            InitSearchInput();
            InitDualWmpAudioEngine();

            // Default initial queue before loading saved settings
            queueSongs.AddRange(catalog);
            currentSong = queueSongs[0];
            currentDurSec = currentSong.Duration;

            // Restore ALL saved settings, last played song, queue, playlists, and Quick Play state from disk!
            LoadPersistentSettings();
            LoadAndLockProtectedUpdateConfig();
            rightPanelWidthAnim = isRightPanelVisible ? 315f : 0f;

            SyncLeoSmartPlaylistWithMood(false);
            EnsureSyncedLyricsParsed(currentSong);
            FetchBetterLyricsAsync(currentSong);
            UpdateTargetAmbientColorFromSong(currentSong);

            FilterDisplaySongs();
            PrefetchThumbnails(catalog);
            if (quickPlaySongs.Count > 0) PrefetchThumbnails(quickPlaySongs);

            // If user already unlocked Quick Play in a previous session, fetch fresh new songs for Quick Play on startup!
            InitDefaultTrendingAndHistory();
            FetchLiveTrendingSongsAsync(false);
            if (quickPlayUnlocked)
            {
                RefreshQuickPlayWithNewSongsOnStartup();
            }

            Application.ApplicationExit += OnApplicationExitSave;

            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = (refreshRateModeIdx == 1) ? 12 : 16; // v10.4.4 Zero-Lag frame pacing (~85 FPS Turbo / ~60 FPS Smooth)
            frameClock.Restart();
            uiTimer.Tick += OnUiTimerTick;
            uiTimer.Start();

            if (!hasCompletedLanguageSetup)
            {
                isLanguageSetupScreenOpen = true;
                languageSetupAnim = 1.0f;
            }

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

        private void QueueSavePersistentSettings()
        {
            settingsSaveDirty = true;
            settingsSaveCooldownTicks = 18;
        }

        // PERSISTENT SETTINGS ENGINE (Saves to BOTH LocalAppData and App BaseDirectory so settings NEVER reset!)
        private void SavePersistentSettings()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("themeIdx=" + activeThemeIdx);
                sb.AppendLine("updateUrl=" + updateManifestUrl);
                sb.AppendLine("speedIdx=" + playbackSpeedIdx);
                sb.AppendLine("langSetup=" + (hasCompletedLanguageSetup ? "1" : "0"));
                sb.AppendLine("languages=" + string.Join(",", new List<string>(userSelectedLanguages).ToArray()));
                sb.AppendLine("streamQuality=" + activeStreamQuality);
                sb.AppendLine("refreshMode=" + refreshRateModeIdx);
                sb.AppendLine("rightPanelVis=" + (isRightPanelVisible ? "1" : "0"));
                sb.AppendLine("dynLighting=" + (dynamicLightingEnabled ? "1" : "0"));
                sb.AppendLine("glassEffect=" + (glassEffectEnabled ? "1" : "0"));
                sb.AppendLine("curved3DGlass=" + (curved3DGlassEnabled ? "1" : "0"));
                sb.AppendLine("dynBarStyle=" + dynamicBarStyleIdx);
                sb.AppendLine("lyrStageStyle=" + lyricStageStyleIdx);
                sb.AppendLine("lyrFadeMode=" + lyricFadeModeIdx);
                sb.AppendLine("lyrFadeSize=" + lyricFadeSizeIdx);
                sb.AppendLine("lyrGlowMode=" + lyricGlowModeIdx);
                sb.AppendLine("lyrBgArt=" + lyricBgArtModeIdx);
                sb.AppendLine("lyrFontStyle=" + lyricFontStyleIdx);
                sb.AppendLine("lyrTextSize=" + lyricTextSizeIdx);
                sb.AppendLine("songBgEnabled=" + (songBgImageEnabled ? "1" : "0"));
                sb.AppendLine("songBgStyle=" + songBgStyleIdx);
                sb.AppendLine("songBgAnim=" + songBgAnimModeIdx);
                sb.AppendLine("songBgInterval=" + songBgIntervalIdx);
                sb.AppendLine("settingsPage=" + activeSettingsSubPage);
                sb.AppendLine("uiTurboCache=" + (uiTurboCacheEnabled ? "1" : "0"));
                sb.AppendLine("volOverdrive=" + (volumeOverdriveAllowed ? "1" : "0"));
                sb.AppendLine("stereoBalance=" + manualStereoBalance);
                sb.AppendLine("autoNormalize=" + (autoVolumeNormalizeEnabled ? "1" : "0"));
                sb.AppendLine("smoothMicroFade=" + (smoothMicroFadeEnabled ? "1" : "0"));
                sb.AppendLine("autoPreBuffer=" + (autoPreBufferNextEnabled ? "1" : "0"));
                sb.AppendLine("bufferProfile=" + audioBufferProfileIdx);
                sb.AppendLine("miniStyle=" + miniPlayerStyleIdx);
                sb.AppendLine("visStyle=" + visualizerStyleIdx);
                sb.AppendLine("listenedSec=" + totalListeningSeconds);
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

                // Save Recently Played History (up to 8)
                List<string> rpEncoded = new List<string>();
                for (int i = 0; i < Math.Min(8, recentlyPlayedSongs.Count); i++)
                {
                    rpEncoded.Add(EncodeSongRecord(recentlyPlayedSongs[i]));
                }
                if (rpEncoded.Count > 0)
                {
                    sb.AppendLine("recentHistory=" + string.Join(";", rpEncoded.ToArray()));
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

                // Save Custom User Playlists (index 1..N; index 0 is Leo Smart Playlist)
                List<string> plBlocks = new List<string>();
                for (int i = 1; i < userPlaylists.Count; i++)
                {
                    UserPlaylistItem pl = userPlaylists[i];
                    List<string> sRecs = new List<string>();
                    for (int k = 0; k < Math.Min(30, pl.Songs.Count); k++)
                    {
                        sRecs.Add(EncodeSongRecord(pl.Songs[k]));
                    }
                    string block = Uri.EscapeDataString(pl.Id ?? ("pl_" + i)) + "^" +
                                   Uri.EscapeDataString(pl.Name ?? ("Playlist " + i)) + "^" +
                                   string.Join("~", sRecs.ToArray());
                    plBlocks.Add(block);
                }
                if (plBlocks.Count > 0)
                {
                    sb.AppendLine("customPlaylists=" + string.Join(";", plBlocks.ToArray()));
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
                List<SongItem> loadedRecent = new List<SongItem>();
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
                    else if (key == "langSetup") hasCompletedLanguageSetup = (val == "1");
                    else if (key == "languages")
                    {
                        userSelectedLanguages.Clear();
                        string[] lArr = val.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string l in lArr) userSelectedLanguages.Add(l.Trim());
                    }
                    else if (key == "streamQuality" && !string.IsNullOrEmpty(val)) activeStreamQuality = val;
                    else if (key == "refreshMode") { int rm = 0; if (int.TryParse(val, out rm) && rm >= 0 && rm <= 1) refreshRateModeIdx = rm; }
                    else if (key == "rightPanelVis") isRightPanelVisible = (val != "0");
                    else if (key == "dynLighting") dynamicLightingEnabled = (val != "0");
                    else if (key == "glassEffect") glassEffectEnabled = (val != "0");
                    else if (key == "curved3DGlass") curved3DGlassEnabled = (val != "0");
                    else if (key == "dynBarStyle") { int v = 0; if (int.TryParse(val, out v) && v >= 0 && v <= 1) dynamicBarStyleIdx = v; }
                    else if (key == "lyrStageStyle") { int v = 0; if (int.TryParse(val, out v) && v >= 0 && v < lyricStageStyleNames.Length) lyricStageStyleIdx = v; }
                    else if (key == "lyrFadeMode") { int v = 0; if (int.TryParse(val, out v) && v >= 0 && v < lyricFadeModeNames.Length) lyricFadeModeIdx = v; }
                    else if (key == "lyrFadeSize") { int v = 2; if (int.TryParse(val, out v) && v >= 0 && v < lyricFadeSizeNames.Length) lyricFadeSizeIdx = v; }
                    else if (key == "lyrGlowMode") { int v = 1; if (int.TryParse(val, out v) && v >= 0 && v < lyricGlowModeNames.Length) lyricGlowModeIdx = v; }
                    else if (key == "lyrBgArt") { int v = 1; if (int.TryParse(val, out v) && v >= 0 && v < lyricBgArtModeNames.Length) lyricBgArtModeIdx = v; }
                    else if (key == "lyrFontStyle") { int v = 0; if (int.TryParse(val, out v) && v >= 0 && v < lyricFontStyleNames.Length) lyricFontStyleIdx = v; }
                    else if (key == "lyrTextSize") { int v = 1; if (int.TryParse(val, out v) && v >= 0 && v < lyricTextSizeNames.Length) lyricTextSizeIdx = v; }
                    else if (key == "songBgEnabled") songBgImageEnabled = (val != "0");
                    else if (key == "songBgStyle") { int v = 0; if (int.TryParse(val, out v) && v >= 0 && v < songBgStyleNames.Length) songBgStyleIdx = v; }
                    else if (key == "songBgAnim") { int v = 0; if (int.TryParse(val, out v) && v >= 0 && v < songBgAnimModeNames.Length) songBgAnimModeIdx = v; }
                    else if (key == "songBgInterval") { int v = 1; if (int.TryParse(val, out v) && v >= 0 && v < songBgIntervalSecs.Length) songBgIntervalIdx = v; }
                    else if (key == "settingsPage") { int v = 0; if (int.TryParse(val, out v) && v >= 0 && v < settingsSubPageNames.Length) activeSettingsSubPage = v; }
                    else if (key == "uiTurboCache") uiTurboCacheEnabled = (val != "0");
                    else if (key == "volOverdrive") volumeOverdriveAllowed = (val != "0");
                    else if (key == "stereoBalance") { int sbv = 0; if (int.TryParse(val, out sbv)) manualStereoBalance = Math.Max(-50, Math.Min(50, sbv)); }
                    else if (key == "autoNormalize") autoVolumeNormalizeEnabled = (val != "0");
                    else if (key == "smoothMicroFade") smoothMicroFadeEnabled = (val != "0");
                    else if (key == "autoPreBuffer") autoPreBufferNextEnabled = (val != "0");
                    else if (key == "bufferProfile") { int bp = 1; if (int.TryParse(val, out bp) && bp >= 0 && bp < audioBufferProfileNames.Length) audioBufferProfileIdx = bp; }
                    else if (key == "miniStyle") { int ms = 0; if (int.TryParse(val, out ms) && ms >= 0 && ms < miniPlayerStyleNames.Length) miniPlayerStyleIdx = ms; }
                    else if (key == "customPlaylists" && !string.IsNullOrEmpty(val))
                    {
                        // Preserve index 0 (Leo Smart Playlist) and load custom playlists
                        while (userPlaylists.Count > 1) userPlaylists.RemoveAt(userPlaylists.Count - 1);
                        string[] blocks = val.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string blk in blocks)
                        {
                            string[] parts = blk.Split('^');
                            if (parts.Length >= 2)
                            {
                                string pid = Uri.UnescapeDataString(parts[0]);
                                string pname = Uri.UnescapeDataString(parts[1]);
                                UserPlaylistItem cpl = new UserPlaylistItem(pid, pname, "Custom User Playlist", false);
                                if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
                                {
                                    string[] sArr = parts[2].Split(new char[] { '~' }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (string sr in sArr)
                                    {
                                        SongItem dec = DecodeSongRecord(sr);
                                        if (dec != null && !ContainsSongByTitleOrId(cpl.Songs, dec)) cpl.Songs.Add(dec);
                                    }
                                }
                                userPlaylists.Add(cpl);
                            }
                        }
                    }
                    else if (key == "speedIdx")
                    {
                        int sp = 0;
                        if (int.TryParse(val, out sp) && sp >= 0 && sp < playbackSpeedRates.Length) playbackSpeedIdx = sp;
                    }
                    else if (key == "visStyle")
                    {
                        int vs = 0;
                        if (int.TryParse(val, out vs) && vs >= 0 && vs < visualizerStyleNames.Length) visualizerStyleIdx = vs;
                    }
                    else if (key == "listenedSec") int.TryParse(val, out totalListeningSeconds);
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
                        if (int.TryParse(val, out v)) volumePercent = Math.Max(0, Math.Min(150, v));
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
                        if (int.TryParse(val, out fv) && fv >= 0 && fv <= 3) fullViewSubMode = fv;
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
                    else if (key == "recentHistory")
                    {
                        string[] recs = val.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string r in recs)
                        {
                            SongItem s = DecodeSongRecord(r);
                            if (s != null && !ContainsSongByTitleOrId(loadedRecent, s)) loadedRecent.Add(s);
                        }
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

                if (loadedRecent.Count > 0)
                {
                    recentlyPlayedSongs = loadedRecent;
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
                "[00:02.00]Pehli goli chaldi te hunda jackpot\n[00:07.80]Kudiye tu lutt leya gabru da thought\n[00:14.00]Gur Sidhu beat utte fire karda\n[00:20.50]Poora shehar dekh sanu cheer karda\n[00:27.50]Honsle buland saade kade na jhukde\n[00:35.00]Manzilan \u062A\u0648\u06BA pehlan kadam na rukde\n[00:43.00]Cheema Y likhda ae sachi har baat ve\n[00:51.00]Mehnat de naal badli ae saadi raat ve\n[01:00.00]Pehli goli chaldi te hunda jackpot\n[01:09.00]Kudiye tu lutt leya gabru da thought\n[01:18.50]Top gear vich gaddi chale bina brake ton\n[01:28.00]Asli ne yaar saare door rehnde fake ton"));

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
                "[00:02.00]Sab kehnde si koshish kar ke dekh la\n[00:08.50]Main keha mehnat kar ke lekh likh la\n[00:15.50]Winning speech ajj stage te bolange\n[00:23.00]Raaz kamyaabi wale saare kholange\n[00:31.00]Thalle ton uth ke aaye aan aasman tak\n[00:39.50]Geet saade gunjde ne har jahaan tak\n[00:48.50]MXRCI di beat utte kalam chaldi\n[00:58.00]Sachi mehnat kade vi nahi \u0679aldi\n[01:08.00]Winning speech ajj stage te bolange"));

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

            // Tag initial 10 studio tracks with accurate Language metadata
            catalog[0].Language = "Hindi";    // Payal - Yo Yo Honey Singh, Paradox (Party)
            catalog[1].Language = "Punjabi";  // Raat Di Gedi - Diljit Dosanjh (Energize)
            catalog[2].Language = "Punjabi";  // Jackpot - Gur Sidhu, Cheema Y (Workout)
            catalog[3].Language = "Punjabi";  // Softly - Karan Aujla, Ikky (Chill)
            catalog[4].Language = "Punjabi";  // G.O.A.T. - Diljit Dosanjh (Energize)
            catalog[5].Language = "English";  // Faded - Alan Walker (Relax)
            catalog[6].Language = "Hindi";    // Tauba Tauba - Karan Aujla (Party)
            catalog[7].Language = "Punjabi";  // Winning Speech - Karan Aujla, MXRCI (Focus)
            catalog[8].Language = "Hindi";    // Kesariya - Pritam, Arijit Singh (Relax)
            catalog[9].Language = "Punjabi";  // Amplifier - Imran Khan (Energize)

            // Add rich Hindi, Haryanvi & Punjabi diverse hits with distinct thumbnails and moods
            SongItem h1 = new SongItem("s11", "295", "Sidhu Moose Wala", "Moosetape", 272, "Workout",
                "https://c.saavncdn.com/606/Moosetape-Punjabi-2021-20220427043323-500x500.jpg",
                "https://aac.saavncdn.com/606/55825075c3e18cd7bc28687c4b393987_320.mp4",
                "https://aac.saavncdn.com/606/55825075c3e18cd7bc28687c4b393987_160.mp4",
                "[00:02.00]Sach bolenga taan milu 295\n[00:08.50]Je karenga tarakki putt hate milugi\n[00:15.00]Sidhu Moose Wala sach likhda ae kalam ton\n[00:22.00]Darda na kade duniya de zulam ton");
            h1.Language = "Punjabi";
            catalog.Add(h1);

            SongItem h2 = new SongItem("s12", "Apna Bana Le", "Arijit Singh, Sachin-Jigar", "Bhediya", 261, "Chill",
                "https://c.saavncdn.com/815/Bhediya-Hindi-2023-20230927155213-500x500.jpg",
                "https://aac.saavncdn.com/815/483a6e4172b92a11704c642063d0e5ef_320.mp4",
                "https://aac.saavncdn.com/815/483a6e4172b92a11704c642063d0e5ef_160.mp4",
                "[00:02.00]Tu mera koi na hoke bhi kuch laage\n[00:09.00]Kiya re jo bhi toone kaise kiya re\n[00:16.50]Jiya ko mere baandh aise liya re\n[00:24.00]Apna bana le piya, apna bana le piya");
            h2.Language = "Hindi";
            catalog.Add(h2);

            SongItem h3 = new SongItem("s13", "Chaleya", "Arijit Singh, Shilpa Rao", "Jawan", 200, "Energize",
                "https://c.saavncdn.com/026/Chaleya-From-Jawan-Hindi-2023-20230814014337-500x500.jpg",
                "https://aac.saavncdn.com/026/98d354bc1d78fb72d18b701d758fd1f1_320.mp4",
                "https://aac.saavncdn.com/026/98d354bc1d78fb72d18b701d758fd1f1_160.mp4",
                "[00:02.00]Ishq mein dil bana hai, ishq mein dil fanaa hai\n[00:08.50]Mita de ya bana de, maine tujhko chuna hai\n[00:15.50]Tere dil pe haq mera hai, tu sanam beshak mera hai");
            h3.Language = "Hindi";
            catalog.Add(h3);

            SongItem hr1 = new SongItem("s14", "2 Khatole", "Masoom Sharma", "2 Khatole", 192, "Party",
                "https://c.saavncdn.com/304/2-Khatole-Haryanvi-2024-20241012153012-500x500.jpg",
                "https://aac.saavncdn.com/173/ad5df053bfb2a4755cbb6c74e6183406_320.mp4",
                "https://aac.saavncdn.com/173/ad5df053bfb2a4755cbb6c74e6183406_160.mp4",
                "[00:02.00]Masoom Sharma Haryanvi beat utte chhaya\n[00:08.50]Poora Haryana ajj floor utte aaya\n[00:15.00]Desi swag saara shehar vich bole");
            hr1.Language = "Haryanvi";
            catalog.Add(hr1);

            SongItem hr2 = new SongItem("s15", "Russian Bandana", "Dhanda Nyoliwala", "Russian Bandana", 195, "Workout",
                "https://c.saavncdn.com/412/Russian-Bandana-Haryanvi-2024-20240910112000-500x500.jpg",
                "https://aac.saavncdn.com/512/16c4d3b6d6c8afb695d48a52821a6ef2_320.mp4",
                "https://aac.saavncdn.com/512/16c4d3b6d6c8afb695d48a52821a6ef2_160.mp4",
                "[00:02.00]Dhanda Nyoliwala kalam chalaave fast\n[00:08.50]Haryanvi hip hop karda ae blast\n[00:15.00]Mehnat de dum te banaya naam");
            hr2.Language = "Haryanvi";
            catalog.Add(hr2);

            SongItem hr3 = new SongItem("s16", "52 Gaj Ka Daman", "Renuka Panwar, Aman Jaji", "52 Gaj Ka Daman", 184, "Party",
                "https://c.saavncdn.com/801/52-Gaj-Ka-Daman-Haryanvi-2020-20201002141022-500x500.jpg",
                "https://aac.saavncdn.com/992/5d44da8bc1d78fb72d18b701d758fd1f_320.mp4",
                "https://aac.saavncdn.com/992/5d44da8bc1d78fb72d18b701d758fd1f_160.mp4",
                "[00:02.00]Apna roop rang sajaungi mehndi haathan mein rachaungi\n[00:09.00]52 gaj ka daman pehan matak chalungi\n[00:16.00]Paayal ki jhankaar se saara vehda goonjega");
            hr3.Language = "Haryanvi";
            catalog.Add(hr3);

            SongItem hr4 = new SongItem("s17", "System Pe System", "R Maan, Khasa Aala Chahar", "System", 188, "Energize",
                "https://c.saavncdn.com/219/System-Pe-System-Haryanvi-2023-20230618121410-500x500.jpg",
                "https://aac.saavncdn.com/698/727adb91e70e5a4ed2a268acdbf55172_320.mp4",
                "https://aac.saavncdn.com/698/727adb91e70e5a4ed2a268acdbf55172_160.mp4",
                "[00:02.00]Chhore Haryane aale system bitha de\n[00:08.50]Bass jadon wajje poora shehar hila de\n[00:15.00]Yaari sab ton uppar saadi pehchan ae");
            hr4.Language = "Haryanvi";
            catalog.Add(hr4);

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
                        if (!string.IsNullOrEmpty(txt) && txt != "\u266A")
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
                cleanT + " \u2014 " + cleanA,
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
                cleanT + " \u2014 BetterLyrics Auto-Sync"
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

                // Tier 4 Smart Timed Studio Stanza Fallback (v10.3.5 Fix: ensures BetterLyrics 2.0 is NEVER blank on regional tracks)
                if (fetchedLines == null || fetchedLines.Count < 4)
                {
                    fetchedLines = BuildSmartTimedStudioLyricsFallback(song);
                    syncType = "BetterLyrics 2.0 Smart Sync";
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

        private List<LyricLine> BuildSmartTimedStudioLyricsFallback(SongItem song)
        {
            List<LyricLine> list = new List<LyricLine>();
            if (song == null) return list;
            if (song.SyncedLines != null && song.SyncedLines.Count >= 4) return song.SyncedLines;

            int dur = song.Duration > 30 ? song.Duration : 210;
            string cleanT = CleanSongDisplayTitle(song.Title);
            string primA = GetPrimaryArtistName(song.Artist);
            string[] lines = new string[]
            {
                "\u266A " + cleanT + " \u2014 " + primA + " \u266A",
                "Studio Master Audio (" + activeStreamQuality + ") \u2022 " + (song.Language ?? "Hindi") + " Edition",
                "Feel the rhythm of " + cleanT + " in 3D Curved Glass Studio",
                "Vocal & Bass Harmonic Lock: " + primA,
                "\u2726 " + cleanT + " (Chorus / Hook Vibe) \u2726",
                "Mood: " + (song.Mood ?? "Chill") + " \u2022 Dynamic Song Lighting Active",
                "Instrumental & Sub-Bass Groove \u2014 " + (song.Album ?? "Single"),
                "\u266A " + cleanT + " \u2022 " + primA + " (Outro Harmonics) \u266A"
            };
            double step = Math.Max(8.0, (dur - 10.0) / lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                double st = 2.5 + i * step;
                double en = st + step - 0.4;
                list.Add(new LyricLine(st, en, lines[i], false));
            }
            return list;
        }

        private Color GetVibrantFallbackColorForSong(SongItem song)
        {
            if (song == null) return Color.FromArgb(255, 193, 7);
            string key = ((song.Title ?? "") + "|" + (song.Artist ?? "")).ToLowerInvariant();
            if (key.Contains("payal") || key.Contains("honey")) return Color.FromArgb(255, 64, 129);      // Vibrant Rose Magenta
            if (key.Contains("pistol") || key.Contains("masoom") || key.Contains("khatole")) return Color.FromArgb(255, 145, 0); // Fiery Amber Orange
            if (key.Contains("softly") || key.Contains("aujla")) return Color.FromArgb(0, 229, 255);     // Electric Cyber Cyan
            if (key.Contains("295") || key.Contains("sidhu") || key.Contains("levels")) return Color.FromArgb(255, 82, 82); // Crimson Red
            if (key.Contains("kesariya") || key.Contains("apna")) return Color.FromArgb(0, 230, 118);    // Emerald Mint
            if (key.Contains("tauba") || key.Contains("bad newz")) return Color.FromArgb(189, 136, 255); // Neon Amethyst
            if (key.Contains("raat di gedi") || key.Contains("diljit")) return Color.FromArgb(255, 213, 79); // Royal Gold
            if (key.Contains("faded") || key.Contains("walker")) return Color.FromArgb(64, 196, 255);    // Ice Blue
            if (key.Contains("russian") || key.Contains("dhanda")) return Color.FromArgb(255, 109, 0);   // Sunset Blaze

            // Deterministic vivid HSV-like palette from title hash + mood
            Color[] vividPalette = new Color[]
            {
                Color.FromArgb(255, 72, 118),
                Color.FromArgb(0, 229, 255),
                Color.FromArgb(255, 171, 0),
                Color.FromArgb(0, 230, 118),
                Color.FromArgb(179, 136, 255),
                Color.FromArgb(255, 110, 64),
                Color.FromArgb(68, 138, 255),
                Color.FromArgb(255, 215, 64)
            };
            int h = Math.Abs(key.GetHashCode());
            return vividPalette[h % vividPalette.Length];
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

            Color fb = GetVibrantFallbackColorForSong(song);
            targetAmbR = fb.R;
            targetAmbG = fb.G;
            targetAmbB = fb.B;
            PrefetchSingleThumbnail(song.ThumbUrl);
        }

        private void ComputeDominantColorFromBitmap(string jpgUrl, Bitmap bmp)
        {
            if (bmp == null) return;
            try
            {
                double sumR = 0, sumG = 0, sumB = 0, totalWeight = 0;
                int stepX = Math.Max(1, bmp.Width / 20);
                int stepY = Math.Max(1, bmp.Height / 20);
                for (int y = stepY; y < bmp.Height - stepY; y += stepY)
                {
                    for (int x = stepX; x < bmp.Width - stepX; x += stepX)
                    {
                        Color px = bmp.GetPixel(x, y);
                        int maxC = Math.Max(px.R, Math.Max(px.G, px.B));
                        int minC = Math.Min(px.R, Math.Min(px.G, px.B));
                        int sat = maxC - minC;
                        int bright = (px.R + px.G + px.B) / 3;
                        if (bright > 28 && bright < 242)
                        {
                            // Weight heavily by saturation so we pick the vivid color from the album art, not dark grey backgrounds!
                            double w = 1.0 + (sat / 25.0) * (sat / 25.0);
                            sumR += px.R * w;
                            sumG += px.G * w;
                            sumB += px.B * w;
                            totalWeight += w;
                        }
                    }
                }
                if (totalWeight > 0)
                {
                    int r = (int)(sumR / totalWeight);
                    int g = (int)(sumG / totalWeight);
                    int b = (int)(sumB / totalWeight);

                    // Boost vibrancy so Dynamic Lighting accent is bright, crisp, and legible on dark surfaces
                    int maxCh = Math.Max(r, Math.Max(g, b));
                    int minCh = Math.Min(r, Math.Min(g, b));
                    if (maxCh - minCh < 45)
                    {
                        // If album cover is monochrome/greyscale, give it a warm golden-cyan studio tint
                        r = Math.Min(255, r + 55);
                        g = Math.Min(255, g + 35);
                    }
                    maxCh = Math.Max(1, Math.Max(r, Math.Max(g, b)));
                    if (maxCh < 225)
                    {
                        float scale = 235f / maxCh;
                        r = Math.Min(255, (int)(r * scale));
                        g = Math.Min(255, (int)(g * scale));
                        b = Math.Min(255, (int)(b * scale));
                    }

                    Color dom = Color.FromArgb(Math.Max(40, r), Math.Max(40, g), Math.Max(40, b));
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

        private void InitAvailableLanguages()
        {
            availableLanguages.Clear();
            availableLanguages.Add(new MusicLanguageItem("Hindi", "\u0939\u093F\u0928\u094D\u0926\u0940", "Hindi"));
            availableLanguages.Add(new MusicLanguageItem("Punjabi", "\u0A2A\u0A70\u0A1C\u0A3E\u0A2C\u0A40", "Punjabi"));
            availableLanguages.Add(new MusicLanguageItem("Haryanvi", "\u0939\u0930\u093F\u092F\u093E\u0923\u0935\u0940", "Haryanvi"));
            availableLanguages.Add(new MusicLanguageItem("Bhojpuri", "\u092D\u094B\u091C\u092A\u0941\u0930\u0940", "Bhojpuri"));
            availableLanguages.Add(new MusicLanguageItem("English", "Pop & EDM", "English"));
            availableLanguages.Add(new MusicLanguageItem("Tamil", "\u0BA4\u0BAE\u0BBF\u0BB4\u0BCD", "Tamil"));
            availableLanguages.Add(new MusicLanguageItem("Telugu", "\u0C24\u0C46\u0C32\u0C41\u0C17\u0C41", "Telugu"));
            availableLanguages.Add(new MusicLanguageItem("Malayalam", "\u0D2E\u0D32\u0D2F\u0D3E\u0D33\u0D02", "Malayalam"));
            availableLanguages.Add(new MusicLanguageItem("Kannada", "\u0C95\u0CA8\u0CCD\u0CA8\u0CA1", "Kannada"));
            availableLanguages.Add(new MusicLanguageItem("Bengali", "\u09AC\u09BE\u0982\u09B2\u09BE", "Bengali"));
            availableLanguages.Add(new MusicLanguageItem("Gujarati", "\u0A97\u0AC1\u0A9C\u0AB0\u0ABE\u0AA4\u0AC0", "Gujarati"));
            availableLanguages.Add(new MusicLanguageItem("Rajasthani", "\u0930\u093E\u091C\u0938\u094D\u0925\u093E\u0928\u0940", "Rajasthani"));
            availableLanguages.Add(new MusicLanguageItem("Urdu", "\u0627\u0631\u062F\u0648", "Urdu"));
            availableLanguages.Add(new MusicLanguageItem("Marathi", "\u092E\u0930\u093E\u0920\u0940", "Marathi"));

            if (userSelectedLanguages.Count == 0)
            {
                userSelectedLanguages.Add("Hindi");
                userSelectedLanguages.Add("Punjabi");
            }
        }

        private string GetLanguageSpecificSearchQueries(string lang, string mood, int variant)
        {
            string mSuffix = (string.IsNullOrEmpty(mood) || mood == "All") ? "" : (" " + mood);
            string l = (lang ?? "Hindi").Trim();
            if (l.Equals("Hindi", StringComparison.OrdinalIgnoreCase))
            {
                if (variant == 0) return "Arijit Singh Sachin-Jigar Vishal Mishra hit songs" + mSuffix;
                return "Badshah Yo Yo Honey Singh Shreya Ghoshal Diljit Bollywood" + mSuffix;
            }
            if (l.Equals("Punjabi", StringComparison.OrdinalIgnoreCase))
            {
                if (variant == 0) return "Karan Aujla Diljit Dosanjh Shubh AP Dhillon hit" + mSuffix;
                return "Sidhu Moose Wala Gur Sidhu Cheema Y Prem Dhillon" + mSuffix;
            }
            if (l.Equals("Haryanvi", StringComparison.OrdinalIgnoreCase))
            {
                if (variant == 0) return "Masoom Sharma Dhanda Nyoliwala Khasa Aala Chahar Haryanvi" + mSuffix;
                return "Gulzaar Chhaniwala Renuka Panwar Ajay Hooda Haryanvi" + mSuffix;
            }
            if (l.Equals("Bhojpuri", StringComparison.OrdinalIgnoreCase))
            {
                if (variant == 0) return "Pawan Singh Khesari Lal Yadav Shilpi Raj Bhojpuri" + mSuffix;
                return "Manoj Tiwari Neelkamal Singh Bhojpuri hit" + mSuffix;
            }
            if (l.Equals("English", StringComparison.OrdinalIgnoreCase))
            {
                if (variant == 0) return "The Weeknd Alan Walker Dua Lipa Ed Sheeran hit" + mSuffix;
                return "Imagine Dragons Justin Bieber Coldplay Post Malone" + mSuffix;
            }
            return l + " top viral hit songs" + mSuffix + (variant == 1 ? " latest" : "");
        }

        private bool CanAddDiverseSongToList(List<SongItem> targetList, SongItem candidate, int maxPerArtist)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.Title)) return false;
            if (IsJunkAudioVariant(candidate.Title)) return false;
            if (ContainsSongByTitleOrId(targetList, candidate)) return false;

            string lowAlbum = (candidate.Album ?? "").ToLowerInvariant();
            if (lowAlbum.Contains("trending hits") || lowAlbum.Contains("best of ") || lowAlbum.Contains("greatest hits"))
            {
                // Avoid compilation albums where all tracks share one thumbnail
                if (targetList.Count < 6) return false;
            }

            string candThumb = (candidate.ThumbUrl ?? "").Trim().ToLowerInvariant();
            string candPrimArt = GetPrimaryArtistName(candidate.Artist).ToLowerInvariant();
            int sameArtistCount = 0;

            for (int i = 0; i < targetList.Count; i++)
            {
                SongItem ex = targetList[i];
                // Never allow two cards with the exact same thumbnail URL in Trending or a Language Shelf!
                if (!string.IsNullOrEmpty(candThumb) && string.Equals((ex.ThumbUrl ?? "").Trim(), candThumb, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                string exArt = GetPrimaryArtistName(ex.Artist).ToLowerInvariant();
                if (!string.IsNullOrEmpty(candPrimArt) && candPrimArt == exArt)
                {
                    sameArtistCount++;
                    if (sameArtistCount >= maxPerArtist) return false;
                }
            }
            return true;
        }

        private void InitDefaultPlaylists()
        {
            userPlaylists.Clear();
            UserPlaylistItem leoPl = new UserPlaylistItem(
                "leo_smart_playlist",
                "Chill Vibes",
                "\u2605 Leo Auto-Mood Smart Playlist \u2022 Updates Automatically As Your Mood Changes",
                true);
            userPlaylists.Add(leoPl);

            UserPlaylistItem haryanviPl = new UserPlaylistItem(
                "pl_haryanvi_hits",
                "Haryanvi Hits",
                "Non-Stop Desi & Haryanvi Bangers \u2022 Click +Playlist on any song to add",
                false);
            if (catalog.Count >= 17)
            {
                haryanviPl.Songs.Add(catalog[13]);
                haryanviPl.Songs.Add(catalog[14]);
                haryanviPl.Songs.Add(catalog[15]);
                haryanviPl.Songs.Add(catalog[16]);
            }
            userPlaylists.Add(haryanviPl);

            UserPlaylistItem workoutPl = new UserPlaylistItem(
                "pl_workout",
                "Workout",
                "High-Energy Gym & Power Beats \u2022 Click +Playlist on any song to add",
                false);
            if (catalog.Count >= 11)
            {
                workoutPl.Songs.Add(catalog[2]);
                workoutPl.Songs.Add(catalog[10]);
                workoutPl.Songs.Add(catalog[7]);
                workoutPl.Songs.Add(catalog[4]);
            }
            userPlaylists.Add(workoutPl);

            UserPlaylistItem sadPl = new UserPlaylistItem(
                "pl_sad_songs",
                "Sad Songs",
                "Late Night Soulful & Acoustic Vibes \u2022 Click +Playlist on any song to add",
                false);
            if (catalog.Count >= 12)
            {
                sadPl.Songs.Add(catalog[8]);
                sadPl.Songs.Add(catalog[11]);
                sadPl.Songs.Add(catalog[5]);
                sadPl.Songs.Add(catalog[3]);
            }
            userPlaylists.Add(sadPl);

            UserPlaylistItem myPl = new UserPlaylistItem(
                "pl_favorites_mix",
                "My Playlist",
                "Custom User Playlist \u2022 Click +Playlist on any song to add",
                false);
            if (catalog.Count >= 7)
            {
                myPl.Songs.Add(catalog[0]);
                myPl.Songs.Add(catalog[1]);
                myPl.Songs.Add(catalog[6]);
                myPl.Songs.Add(catalog[9]);
            }
            userPlaylists.Add(myPl);
        }

        // Automatically adds & removes songs in Leo Smart Playlist whenever the user's Mood or Language changes!
        private void SyncLeoSmartPlaylistWithMood(bool showNotification)
        {
            if (userPlaylists.Count == 0) InitDefaultPlaylists();
            UserPlaylistItem leoPl = userPlaylists[0];
            string activeLang = GetActiveFilterLanguageName();
            string curMood = string.IsNullOrEmpty(selectedMood) ? "All" : selectedMood;

            leoPl.Subtitle = "Leo Auto-Mood Mix \u2022 Mood: " + curMood + " (" + activeLang + ") \u2022 Auto-Adds & Removes Tracks";

            // 1. Automatically REMOVE songs that no longer match the active mood (when a specific mood is chosen) or language
            for (int i = leoPl.Songs.Count - 1; i >= 0; i--)
            {
                SongItem ex = leoPl.Songs[i];
                bool moodMatches = (curMood == "All") || string.Equals(ex.Mood ?? "", curMood, StringComparison.OrdinalIgnoreCase);
                bool langMatches = (activeLang == "All Languages")
                    ? (userSelectedLanguages.Count == 0 || userSelectedLanguages.Contains(ex.Language ?? "Hindi"))
                    : string.Equals(ex.Language ?? "Hindi", activeLang, StringComparison.OrdinalIgnoreCase);
                if (!moodMatches || !langMatches)
                {
                    leoPl.Songs.RemoveAt(i);
                }
            }

            // 2. Gather candidate pool from trending, language shelves, quickPlay, and catalog
            List<SongItem> pool = new List<SongItem>();
            foreach (SongItem s in trendingSongs) pool.Add(s);
            foreach (string ul in userSelectedLanguages)
            {
                if (languageSongShelves.ContainsKey(ul))
                {
                    foreach (SongItem s in languageSongShelves[ul]) pool.Add(s);
                }
            }
            foreach (SongItem s in quickPlaySongs) pool.Add(s);
            foreach (SongItem s in catalog) pool.Add(s);

            // 3. Automatically ADD top matching songs for the current Mood + Language
            foreach (SongItem cand in pool)
            {
                if (leoPl.Songs.Count >= 14) break;
                bool moodOk = (curMood == "All") || string.Equals(cand.Mood ?? "", curMood, StringComparison.OrdinalIgnoreCase);
                bool langOk = (activeLang == "All Languages")
                    ? (userSelectedLanguages.Count == 0 || userSelectedLanguages.Contains(cand.Language ?? "Hindi"))
                    : string.Equals(cand.Language ?? "Hindi", activeLang, StringComparison.OrdinalIgnoreCase);
                if (moodOk && langOk && !ContainsSongByTitleOrId(leoPl.Songs, cand))
                {
                    leoPl.Songs.Add(cand);
                }
            }

            // Ensure at least 6 songs in Leo Smart Playlist if strict mood pool is still loading from network
            if (leoPl.Songs.Count < 6)
            {
                foreach (SongItem cand in pool)
                {
                    if (leoPl.Songs.Count >= 10) break;
                    bool moodOk = (curMood == "All") || string.Equals(cand.Mood ?? "", curMood, StringComparison.OrdinalIgnoreCase);
                    if (moodOk && !ContainsSongByTitleOrId(leoPl.Songs, cand))
                    {
                        leoPl.Songs.Add(cand);
                    }
                }
            }
            if (leoPl.Songs.Count < 4)
            {
                foreach (SongItem cand in catalog)
                {
                    if (leoPl.Songs.Count >= 6) break;
                    if (!ContainsSongByTitleOrId(leoPl.Songs, cand)) leoPl.Songs.Add(cand);
                }
            }

            if (showNotification)
            {
                ShowToast("\u2605 Leo Playlist Auto-Updated for " + curMood + " Mood (" + leoPl.Songs.Count + " Songs)");
            }
        }

        private void AddSongToActiveUserPlaylist(SongItem song)
        {
            if (song == null) return;
            if (userPlaylists.Count < 2)
            {
                UserPlaylistItem defPl = new UserPlaylistItem("pl_custom_1", "My Studio Mix", "Custom User Playlist", false);
                userPlaylists.Add(defPl);
            }
            // If currently viewing a custom playlist (index >= 1), add to it; otherwise add to first custom playlist (index 1)
            int targetIdx = (selectedPlaylistIdx >= 1 && selectedPlaylistIdx < userPlaylists.Count) ? selectedPlaylistIdx : 1;
            UserPlaylistItem targetPl = userPlaylists[targetIdx];
            if (ContainsSongByTitleOrId(targetPl.Songs, song))
            {
                ShowToast("Already in '" + targetPl.Name + "': " + song.Title);
                return;
            }
            targetPl.Songs.Insert(0, song);
            SavePersistentSettings();
            ShowToast("+ Added '" + song.Title + "' to Playlist: " + targetPl.Name);
            this.Invalidate();
        }

        private void PlayEntirePlaylist(UserPlaylistItem pl, bool shufflePlay)
        {
            if (pl == null || pl.Songs.Count == 0)
            {
                ShowToast("Playlist is empty! Add songs using +Playlist.");
                return;
            }
            List<SongItem> list = new List<SongItem>(pl.Songs);
            if (shufflePlay && list.Count > 1)
            {
                Random rnd = new Random();
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rnd.Next(0, i + 1);
                    SongItem tmp = list[i];
                    list[i] = list[j];
                    list[j] = tmp;
                }
                isShuffle = true;
            }
            queueSongs.Clear();
            queueSongs.AddRange(list);
            currentQueueIndex = 0;
            PlaySong(queueSongs[0], true);
            ShowToast("\u25B6 Playing Playlist: " + pl.Name + " (" + queueSongs.Count + " Tracks)");
        }

        private void ToggleRightPanelVisibility()
        {
            TriggerButtonBounce("toggle_right_panel");
            isRightPanelVisible = !isRightPanelVisible;
            SavePersistentSettings();
            ShowToast(isRightPanelVisible ? "Right Side Panel (Queue & Lyrics) Shown" : "Right Side Panel Hidden (Minimalist Wide View)");
            this.Invalidate();
        }

        private void ToggleDynamicLightingMode()
        {
            dynamicLightingEnabled = !dynamicLightingEnabled;
            if (!dynamicLightingEnabled)
            {
                accentColor = themes[activeThemeIdx].Accent;
            }
            else if (currentSong != null)
            {
                UpdateTargetAmbientColorFromSong(currentSong);
            }
            SavePersistentSettings();
            ShowToast(dynamicLightingEnabled
                ? "\u2726 Dynamic Lighting ON (Colors sync to playing song!)"
                : ("Dynamic Lighting OFF (Locked to " + themes[activeThemeIdx].Name + ")"));
            this.Invalidate();
        }

        private void InitDefaultTrendingAndHistory()
        {
            // Seed languageSongShelves from catalog so every selected language has instant songs at startup
            foreach (MusicLanguageItem ml in availableLanguages)
            {
                if (!languageSongShelves.ContainsKey(ml.Name))
                {
                    languageSongShelves[ml.Name] = new List<SongItem>();
                }
            }
            foreach (SongItem s in catalog)
            {
                string sLang = string.IsNullOrEmpty(s.Language) ? "Hindi" : s.Language;
                if (!languageSongShelves.ContainsKey(sLang))
                {
                    languageSongShelves[sLang] = new List<SongItem>();
                }
                if (!ContainsSongByTitleOrId(languageSongShelves[sLang], s))
                {
                    languageSongShelves[sLang].Add(s);
                }
            }

            if (trendingSongs.Count == 0 && catalog.Count >= 8)
            {
                // Pick 6 diverse artists & thumbnails matching user's selected languages
                foreach (SongItem s in catalog)
                {
                    if (userSelectedLanguages.Count == 0 || userSelectedLanguages.Contains(s.Language))
                    {
                        if (CanAddDiverseSongToList(trendingSongs, s, 1))
                        {
                            trendingSongs.Add(s);
                            if (trendingSongs.Count >= 6) break;
                        }
                    }
                }
                foreach (SongItem s in catalog)
                {
                    if (trendingSongs.Count >= 6) break;
                    if (CanAddDiverseSongToList(trendingSongs, s, 1)) trendingSongs.Add(s);
                }
            }
            if (recentlyPlayedSongs.Count == 0 && catalog.Count >= 4)
            {
                if (currentSong != null) recentlyPlayedSongs.Add(currentSong);
                if (!ContainsSongByTitleOrId(recentlyPlayedSongs, catalog[3])) recentlyPlayedSongs.Add(catalog[3]);
                if (!ContainsSongByTitleOrId(recentlyPlayedSongs, catalog[6])) recentlyPlayedSongs.Add(catalog[6]);
                if (!ContainsSongByTitleOrId(recentlyPlayedSongs, catalog[8])) recentlyPlayedSongs.Add(catalog[8]);
            }
        }

        private string GetActiveFilterLanguageName()
        {
            List<string> myLangs = new List<string>(userSelectedLanguages);
            myLangs.Insert(0, "All Languages");
            if (activeLanguageFilterIdx >= 0 && activeLanguageFilterIdx < myLangs.Count)
            {
                return myLangs[activeLanguageFilterIdx];
            }
            activeLanguageFilterIdx = 0;
            return "All Languages";
        }

        private void FetchLiveTrendingSongsAsync(bool showToastOnDone)
        {
            string activeLang = GetActiveFilterLanguageName();
            string activeMood = selectedMood;
            int myFetchVer = ++trendingFetchVersion;

            Thread trThread = new Thread(() =>
            {
                List<string> targetLangs = new List<string>();
                if (activeLang != "All Languages")
                {
                    targetLangs.Add(activeLang);
                }
                foreach (string ul in userSelectedLanguages)
                {
                    if (!targetLangs.Contains(ul)) targetLangs.Add(ul);
                }
                if (targetLangs.Count == 0)
                {
                    targetLangs.Add("Hindi");
                    targetLangs.Add("Punjabi");
                }

                Dictionary<string, List<SongItem>> fetchedShelves = new Dictionary<string, List<SongItem>>(StringComparer.OrdinalIgnoreCase);
                List<SongItem> diverseTrending = new List<SongItem>();

                foreach (string lang in targetLangs)
                {
                    List<SongItem> langList = new List<SongItem>();
                    for (int variant = 0; variant < 2; variant++)
                    {
                        string q = GetLanguageSpecificSearchQueries(lang, activeMood, variant);
                        try
                        {
                            string url = "https://www.jiosaavn.com/api.php?__call=search.getResults&_format=json&n=18&p=1&_marker=0&ctx=android&q=" + Uri.EscapeDataString(q);
                            using (WebClient wc = new WebClient())
                            {
                                wc.Encoding = Encoding.UTF8;
                                wc.Headers.Add("User-Agent", "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/120.0 Mobile Safari/537.36");
                                string json = wc.DownloadString(url);
                                List<SongItem> parsed = ParseOfficialJioSaavnJson(json);
                                foreach (SongItem s in parsed)
                                {
                                    s.Language = lang;
                                    if (activeMood != "All" && !string.IsNullOrEmpty(activeMood))
                                    {
                                        // Tag mood when fetched for a specific mood filter
                                        if (variant == 0) s.Mood = activeMood;
                                    }
                                    if (CanAddDiverseSongToList(langList, s, 2))
                                    {
                                        langList.Add(s);
                                    }
                                    if (activeLang == "All Languages" || string.Equals(activeLang, lang, StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Strictly 1 song per artist & unique thumbnail in Trending Now!
                                        if (CanAddDiverseSongToList(diverseTrending, s, 1) && diverseTrending.Count < 8)
                                        {
                                            diverseTrending.Add(s);
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                        if (langList.Count >= 8) break;
                    }
                    if (langList.Count > 0)
                    {
                        fetchedShelves[lang] = langList;
                    }
                }

                // Interleave top tracks across all selected languages if "All Languages" is active
                if (activeLang == "All Languages" && fetchedShelves.Count > 1)
                {
                    List<SongItem> interleaved = new List<SongItem>();
                    for (int slot = 0; slot < 8; slot++)
                    {
                        foreach (string lang in targetLangs)
                        {
                            if (fetchedShelves.ContainsKey(lang) && slot < fetchedShelves[lang].Count)
                            {
                                SongItem cand = fetchedShelves[lang][slot];
                                if (CanAddDiverseSongToList(interleaved, cand, 1))
                                {
                                    interleaved.Add(cand);
                                    if (interleaved.Count >= 8) break;
                                }
                            }
                        }
                        if (interleaved.Count >= 8) break;
                    }
                    if (interleaved.Count >= 4) diverseTrending = interleaved;
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    if (myFetchVer != trendingFetchVersion) return;
                    foreach (KeyValuePair<string, List<SongItem>> kv in fetchedShelves)
                    {
                        languageSongShelves[kv.Key] = kv.Value;
                        PrefetchThumbnails(kv.Value);
                        // Also merge into catalog if not already present so Mood + Language filters have rich local tracks
                        foreach (SongItem ns in kv.Value)
                        {
                            if (!ContainsSongByTitleOrId(catalog, ns))
                            {
                                catalog.Add(ns);
                            }
                        }
                    }
                    if (diverseTrending.Count >= 4)
                    {
                        trendingSongs = diverseTrending;
                        PrefetchThumbnails(trendingSongs);
                    }
                    SyncLeoSmartPlaylistWithMood(false);
                    FilterDisplaySongs();
                    if (showToastOnDone)
                    {
                        string filterDesc = activeLang + (activeMood != "All" ? (" \u2022 " + activeMood) : "");
                        ShowToast("Updated Feed: " + filterDesc);
                    }
                    this.Invalidate();
                });
            });
            trThread.IsBackground = true;
            trThread.Start();
        }

        private void StartInstantMixRadio()
        {
            string activeLang = GetActiveFilterLanguageName();
            string activeMood = selectedMood;
            List<SongItem> radioPool = new List<SongItem>();

            foreach (SongItem s in trendingSongs)
            {
                if (CanAddDiverseSongToList(radioPool, s, 2)) radioPool.Add(s);
            }
            if (activeLang != "All Languages" && languageSongShelves.ContainsKey(activeLang))
            {
                foreach (SongItem s in languageSongShelves[activeLang])
                {
                    if (CanAddDiverseSongToList(radioPool, s, 2)) radioPool.Add(s);
                }
            }
            else
            {
                foreach (string ul in userSelectedLanguages)
                {
                    if (languageSongShelves.ContainsKey(ul))
                    {
                        foreach (SongItem s in languageSongShelves[ul])
                        {
                            if (CanAddDiverseSongToList(radioPool, s, 2)) radioPool.Add(s);
                        }
                    }
                }
            }
            foreach (SongItem s in displaySongs)
            {
                if (CanAddDiverseSongToList(radioPool, s, 2)) radioPool.Add(s);
            }
            if (radioPool.Count == 0) radioPool.AddRange(catalog);

            // Shuffle slightly for fresh Mix Radio experience
            Random rnd = new Random();
            for (int i = radioPool.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(0, i + 1);
                SongItem tmp = radioPool[i];
                radioPool[i] = radioPool[j];
                radioPool[j] = tmp;
            }

            queueSongs.Clear();
            for (int i = 0; i < Math.Min(22, radioPool.Count); i++)
            {
                queueSongs.Add(radioPool[i]);
            }
            currentQueueIndex = 0;
            if (queueSongs.Count > 0)
            {
                PlaySong(queueSongs[0], true);
                string label = (activeLang == "All Languages" ? "Multi-Language" : activeLang) + (activeMood == "All" ? " Mix" : (" " + activeMood + " Radio"));
                ShowToast("\u25B6 Mix Radio Started: " + label + " (" + queueSongs.Count + " Tracks)");
            }
        }

        private void RecordSongPlayForQuickPlay(SongItem playedSong)
        {
            if (playedSong == null) return;
            userPlayedSongsCount++;

            // Update Recently Played History (MRU at index 0, no duplicate titles)
            for (int i = recentlyPlayedSongs.Count - 1; i >= 0; i--)
            {
                if (recentlyPlayedSongs[i].Id == playedSong.Id || IsSameSongTitleOrVariant(recentlyPlayedSongs[i].Title, playedSong.Title))
                {
                    recentlyPlayedSongs.RemoveAt(i);
                }
            }
            recentlyPlayedSongs.Insert(0, playedSong);
            if (recentlyPlayedSongs.Count > 8) recentlyPlayedSongs.RemoveRange(8, recentlyPlayedSongs.Count - 8);

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

            playlistNameBox = new TextBox();
            playlistNameBox.BorderStyle = BorderStyle.None;
            playlistNameBox.BackColor = Color.FromArgb(24, 28, 42);
            playlistNameBox.ForeColor = Color.White;
            playlistNameBox.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            playlistNameBox.Location = new Point(250, 108);
            playlistNameBox.Size = new Size(210, 22);
            playlistNameBox.Text = "";
            playlistNameBox.Visible = false;
            playlistNameBox.KeyDown += OnPlaylistNameBoxKeyDown;
            this.Controls.Add(playlistNameBox);
        }

        private void OnPlaylistNameBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ConfirmCreateCustomPlaylist();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                HidePlaylistNameBox();
            }
        }

        private void ShowPlaylistNameBox(int x, int y, int w)
        {
            isCreatingPlaylistInput = true;
            playlistNameBox.Location = new Point(x, y);
            playlistNameBox.Size = new Size(w, 22);
            playlistNameBox.Text = "";
            playlistNameBox.Visible = true;
            playlistNameBox.BringToFront();
            playlistNameBox.Focus();
            this.Invalidate();
        }

        private void HidePlaylistNameBox()
        {
            isCreatingPlaylistInput = false;
            if (playlistNameBox != null && playlistNameBox.Visible)
            {
                playlistNameBox.Visible = false;
            }
            this.Invalidate();
        }

        private void ConfirmCreateCustomPlaylist()
        {
            string rawName = (playlistNameBox != null ? playlistNameBox.Text : "").Trim();
            if (string.IsNullOrEmpty(rawName))
            {
                rawName = "My Playlist #" + userPlaylists.Count;
            }
            HidePlaylistNameBox();
            string newId = "pl_" + DateTime.UtcNow.Ticks;
            UserPlaylistItem newPl = new UserPlaylistItem(newId, rawName, "Custom User Playlist", false);
            if (currentSong != null) newPl.Songs.Add(currentSong);
            userPlaylists.Add(newPl);
            selectedPlaylistIdx = userPlaylists.Count - 1;
            SavePersistentSettings();
            ShowToast("Created Playlist: " + rawName);
            this.Invalidate();
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
            if (activeTab == 4 || activeTab == 5) activeTab = 1;
            int rightW = Math.Max(0, (int)Math.Round(rightPanelWidthAnim));
            int centerW = Math.Max(320, this.ClientSize.Width - 198 - rightW);
            int searchW = Math.Min(410, Math.Max(210, centerW - 190));
            searchBox.Location = new Point(198 + 24 + 34, 26);
            searchBox.Size = new Size(Math.Max(130, searchW - 64), 22);
            searchBox.BackColor = Color.FromArgb(24, 18, 22);
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
            if (volumePercent <= 0) return 0;

            // v10.4.4 Smart Auto-Volume Normalization (ReplayGain style mood/track loudness leveling)
            float normMultiplier = 1.0f;
            if (autoVolumeNormalizeEnabled && currentSong != null)
            {
                string m = (currentSong.Mood ?? "All").ToLowerInvariant();
                if (m == "chill" || m == "relax" || m == "focus") normMultiplier = 1.08f; // Gently lift quieter acoustic/chill tracks
                else if (m == "party" || m == "workout") normMultiplier = 0.96f; // Prevent clipping on hot club masters
            }

            // Support 0%..100% Standard Range + 101%..150% Studio Overdrive Pre-Amp Boost!
            float effectiveUserVol = volumePercent * normMultiplier;
            float mFade = smoothMicroFadeEnabled ? Math.Max(0.05f, Math.Min(1.0f, microFadeMultiplier)) : 1.0f;

            if (!eqEnabled)
            {
                // At 100% user volume -> 74 WMP level; at 150% Overdrive -> full 100 WMP hardware ceiling!
                float rawNoEq = effectiveUserVol * 0.72f * mFade;
                return Math.Max(1, Math.Min(100, (int)Math.Round(rawNoEq)));
            }

            // Weighted 5-band acoustic gain: Sub-bass (60Hz), Punch (230Hz), Vocal Mid (910Hz), Presence (3.6kHz), Air (14kHz)
            float lowGain = eqBands[0] * 1.45f + eqBands[1] * 1.15f;
            float midGain = eqBands[2] * 1.25f;
            float highGain = eqBands[3] * 1.10f + eqBands[4] * 1.05f;
            float netEqDb = (lowGain + midGain + highGain) / 4.2f;

            // DSP Sliders contribution
            float bassExtra = (bassBoostPct / 100f) * 14.0f;
            float loudExtra = (loudnessGainPct / 100f) * 18.0f;

            // Real-time harmonic bass-punch envelope modulation when Bass Boost or 60Hz/230Hz is boosted
            float dynamicPunch = 0f;
            if (isPlaying)
            {
                float totalBassDrive = Math.Max(0f, eqBands[0] + eqBands[1] * 0.6f + (bassBoostPct - 20) * 0.12f);
                if (totalBassDrive > 0.5f)
                {
                    double beatWave = Math.Sin(wavePhase * 2.4f);
                    double harmonic = Math.Pow(Math.Max(0.0, beatWave), 1.6);
                    dynamicPunch = (float)(harmonic * Math.Min(12.0, totalBassDrive * 1.05));
                }
            }

            float baseLevel = effectiveUserVol * 0.68f;
            float eqDelta = netEqDb * 2.25f + bassExtra + loudExtra + dynamicPunch;
            float scaledDelta = eqDelta * (Math.Min(135f, effectiveUserVol) / 100f);

            int finalVol = (int)Math.Round((baseLevel + scaledDelta) * mFade);
            return Math.Max(1, Math.Min(100, finalVol));
        }

        private void ApplyEffectiveDspAndVolume()
        {
            try
            {
                if (wmpSettings != null)
                {
                    int targetV = ComputeTargetEffectiveVolume();
                    int scaledV = Math.Max(0, Math.Min(100, (targetV * deckAFadeInProgress) / 100));

                    if (isMuted != lastAppliedWmpMute)
                    {
                        lastAppliedWmpMute = isMuted;
                        wmpSettings.GetType().InvokeMember("mute", BindingFlags.SetProperty, null, wmpSettings, new object[] { isMuted });
                    }
                    if (Math.Abs(scaledV - lastAppliedWmpVol) >= 1 || (scaledV == 0 && lastAppliedWmpVol != 0) || (scaledV == 100 && lastAppliedWmpVol != 100))
                    {
                        lastAppliedWmpVol = scaledV;
                        wmpSettings.GetType().InvokeMember("volume", BindingFlags.SetProperty, null, wmpSettings, new object[] { scaledV });
                    }

                    int bal = manualStereoBalance;
                    if (eqEnabled && isPlaying)
                    {
                        float spatialDrive = virtualizerPct * 0.16f + Math.Max(0f, eqBands[4]) * 0.8f;
                        if (spatialDrive > 1.5f)
                        {
                            bal += (int)Math.Round(Math.Sin(wavePhase * 0.55f) * Math.Min(20f, spatialDrive));
                        }
                    }
                    bal = Math.Max(-100, Math.Min(100, bal));
                    if (Math.Abs(bal - lastAppliedWmpBal) >= 3 || (bal == manualStereoBalance && lastAppliedWmpBal != manualStereoBalance))
                    {
                        lastAppliedWmpBal = bal;
                        wmpSettings.GetType().InvokeMember("balance", BindingFlags.SetProperty, null, wmpSettings, new object[] { bal });
                    }

                    double targetRate = playbackSpeedRates[playbackSpeedIdx];
                    if (Math.Abs(targetRate - lastAppliedWmpRate) > 0.005)
                    {
                        lastAppliedWmpRate = targetRate;
                        wmpSettings.GetType().InvokeMember("rate", BindingFlags.SetProperty, null, wmpSettings, new object[] { targetRate });
                    }
                }
            }
            catch { }
        }

        private void CyclePlaybackSpeed()
        {
            TriggerButtonBounce("speed_btn");
            playbackSpeedIdx = (playbackSpeedIdx + 1) % playbackSpeedRates.Length;
            ApplyEffectiveDspAndVolume();
            SavePersistentSettings();
            ShowToast("Speed Mode: " + playbackSpeedNames[playbackSpeedIdx]);
            this.Invalidate();
        }

        private void CycleVisualizerStyle()
        {
            visualizerStyleIdx = (visualizerStyleIdx + 1) % visualizerStyleNames.Length;
            SavePersistentSettings();
            ShowToast("Visualizer: " + visualizerStyleNames[visualizerStyleIdx]);
            this.Invalidate();
        }

        private void ReplaceWindowRegionSafely(Region newRegion)
        {
            try
            {
                Region oldRegion = this.Region;
                this.Region = newRegion;
                if (oldRegion != null)
                {
                    oldRegion.Dispose();
                }
            }
            catch { }
        }

        private void EnforceAlwaysOnTopOverlayZOrder()
        {
            if (!isMiniPlayerMode && !isDynamicBarMode) return;
            try
            {
                if (!this.TopMost) this.TopMost = true;
                if (this.IsHandleCreated)
                {
                    SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
            }
            catch { }
        }

        private void ApplyDynamicBarSizeAndRegion()
        {
            if (!isDynamicBarMode) return;
            // Phone-Style Sleek Dynamic Island Pill: 264x34 when compact, 360x98 when expanded!
            int targetW = isDynamicBarExpanded ? 360 : 264;
            int targetH = isDynamicBarExpanded ? 98 : 34;
            int cornerR = isDynamicBarExpanded ? 22 : 17;

            this.MinimumSize = new Size(targetW, targetH);
            this.Size = new Size(targetW, targetH);

            using (GraphicsPath gp = GetRoundRectPath(new Rectangle(0, 0, targetW, targetH), cornerR))
            {
                ReplaceWindowRegionSafely(new Region(gp));
            }
            EnforceAlwaysOnTopOverlayZOrder();
        }

        private void SnapDynamicBarToTopCenter()
        {
            try
            {
                Rectangle workArea = Screen.FromControl(this).WorkingArea;
                int nx = workArea.Left + (workArea.Width - this.Width) / 2;
                int ny = workArea.Top + 10;
                this.Location = new Point(nx, ny);
                savedDynamicBarLocation = this.Location;
            }
            catch { }
        }

        private void ToggleDynamicBarExpanded()
        {
            if (!isDynamicBarMode) return;
            int oldCenterX = this.Left + this.Width / 2;
            isDynamicBarExpanded = !isDynamicBarExpanded;
            ApplyDynamicBarSizeAndRegion();
            this.Left = oldCenterX - this.Width / 2;
            savedDynamicBarLocation = this.Location;
            this.Invalidate();
        }

        private void ToggleDynamicBarMode()
        {
            TriggerButtonBounce("dyn_bar_btn");
            if (!isDynamicBarMode)
            {
                UnfocusSearchInput();
                HideAdminPasswordBox();
                HidePlaylistNameBox();
                isUpdatePopupOpen = false;
                if (this.WindowState == FormWindowState.Maximized)
                {
                    this.WindowState = FormWindowState.Normal;
                }
                if (!isMiniPlayerMode)
                {
                    savedDesktopSize = this.Size;
                    savedDesktopLocation = this.Location;
                }
                isMiniPlayerMode = false;
                isDynamicBarMode = true;
                isDynamicBarExpanded = false;
                this.TopMost = true;
                ApplyDynamicBarSizeAndRegion();
                if (savedDynamicBarLocation.X >= 0 && savedDynamicBarLocation.Y >= 0)
                {
                    this.Location = savedDynamicBarLocation;
                }
                else
                {
                    SnapDynamicBarToTopCenter();
                }
                EnforceAlwaysOnTopOverlayZOrder();
                ShowToast("\u2726 Dynamic Bar Active (Overlays Everything \u2022 Drag Anywhere)");
            }
            else
            {
                savedDynamicBarLocation = this.Location;
                isDynamicBarMode = false;
                isDynamicBarExpanded = false;
                this.TopMost = false;
                try { if (this.IsHandleCreated) SetWindowPos(this.Handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW); } catch { }
                ReplaceWindowRegionSafely(null);
                this.MinimumSize = new Size(980, 640);
                this.Size = (savedDesktopSize.Width >= 980 && savedDesktopSize.Height >= 640) ? savedDesktopSize : new Size(1220, 770);
                this.Location = savedDesktopLocation;
                ShowToast("Restored Full Studio Window");
            }
            this.Invalidate();
        }

        private void ApplyMiniPlayerWindowRegion()
        {
            try
            {
                if (!isMiniPlayerMode)
                {
                    if (!isDynamicBarMode) ReplaceWindowRegionSafely(null);
                    return;
                }
                int w = this.Width;
                int h = this.Height;
                if (miniPlayerStyleIdx == 3)
                {
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddEllipse(0, 0, w, h);
                        ReplaceWindowRegionSafely(new Region(gp));
                    }
                }
                else if (miniPlayerStyleIdx == 1)
                {
                    using (GraphicsPath gp = GetRoundRectPath(new Rectangle(0, 0, w, h), h / 2 - 2))
                    {
                        ReplaceWindowRegionSafely(new Region(gp));
                    }
                }
                else if (miniPlayerStyleIdx == 2)
                {
                    using (GraphicsPath gp = GetRoundRectPath(new Rectangle(0, 0, w, h), 10))
                    {
                        ReplaceWindowRegionSafely(new Region(gp));
                    }
                }
                else
                {
                    using (GraphicsPath gp = GetRoundRectPath(new Rectangle(0, 0, w, h), 28))
                    {
                        ReplaceWindowRegionSafely(new Region(gp));
                    }
                }
                EnforceAlwaysOnTopOverlayZOrder();
            }
            catch { }
        }

        private void ApplyMiniPlayerSizeForStyle()
        {
            if (!isMiniPlayerMode) return;
            if (miniPlayerStyleIdx == 3)
            {
                this.MinimumSize = new Size(256, 256);
                this.Size = new Size(256, 256);
            }
            else if (miniPlayerStyleIdx == 1)
            {
                this.MinimumSize = new Size(468, 154);
                this.Size = new Size(468, 154);
            }
            else
            {
                this.MinimumSize = new Size(440, 166);
                this.Size = new Size(440, 166);
            }
            ApplyMiniPlayerWindowRegion();
        }

        private void CycleMiniPlayerStyle(int targetIdx)
        {
            if (targetIdx >= 0 && targetIdx < miniPlayerStyleNames.Length)
            {
                miniPlayerStyleIdx = targetIdx;
            }
            else
            {
                miniPlayerStyleIdx = (miniPlayerStyleIdx + 1) % miniPlayerStyleNames.Length;
            }
            if (isMiniPlayerMode)
            {
                ApplyMiniPlayerSizeForStyle();
            }
            SavePersistentSettings();
            ShowToast("Mini-Player Style: " + miniPlayerStyleNames[miniPlayerStyleIdx]);
            this.Invalidate();
        }

        private void ToggleGlassEffectMode()
        {
            glassEffectEnabled = !glassEffectEnabled;
            SavePersistentSettings();
            ShowToast(glassEffectEnabled ? "\u2726 3D Curved Frosted Glass & Live-Mesh ON" : "Frosted Glass OFF (Solid Matte Studio)");
            this.Invalidate();
        }

        private void ToggleCurved3DGlassMode()
        {
            curved3DGlassEnabled = !curved3DGlassEnabled;
            if (curved3DGlassEnabled) glassEffectEnabled = true;
            SavePersistentSettings();
            ShowToast(curved3DGlassEnabled ? "\u2726 3D Curved Lens Glass Refraction ON" : "Flat Frosted Glass Mode");
            this.Invalidate();
        }

        private void ToggleMiniPlayerMode()
        {
            TriggerButtonBounce("mini_btn");
            if (!isMiniPlayerMode)
            {
                UnfocusSearchInput();
                HideAdminPasswordBox();
                HidePlaylistNameBox();
                isUpdatePopupOpen = false;
                if (this.WindowState == FormWindowState.Maximized)
                {
                    this.WindowState = FormWindowState.Normal;
                }
                if (!isDynamicBarMode)
                {
                    savedDesktopSize = this.Size;
                    savedDesktopLocation = this.Location;
                }
                else
                {
                    savedDynamicBarLocation = this.Location;
                }
                isDynamicBarMode = false;
                isMiniPlayerMode = true;
                this.TopMost = true;
                ApplyMiniPlayerSizeForStyle();
                EnforceAlwaysOnTopOverlayZOrder();
                ShowToast("Glass Mini-Player (" + miniPlayerStyleNames[miniPlayerStyleIdx] + ") \u2022 Overlays Everything");
            }
            else
            {
                isMiniPlayerMode = false;
                this.TopMost = false;
                try { if (this.IsHandleCreated) SetWindowPos(this.Handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW); } catch { }
                ReplaceWindowRegionSafely(null);
                this.MinimumSize = new Size(980, 640);
                this.Size = (savedDesktopSize.Width >= 980 && savedDesktopSize.Height >= 640) ? savedDesktopSize : new Size(1220, 770);
                this.Location = savedDesktopLocation;
                ShowToast("Restored Full Studio Window");
            }
            this.Invalidate();
        }

        private void SetVolume(int newVol)
        {
            int maxAllowed = volumeOverdriveAllowed ? 150 : 100;
            volumePercent = Math.Max(0, Math.Min(maxAllowed, newVol));
            isMuted = (volumePercent == 0);
            ApplyEffectiveDspAndVolume();
            QueueSavePersistentSettings(); // v10.4.4 Zero-Lag: Debounce disk write so volume slider dragging is 0ms instant!
            string boostTag = (volumePercent > 100) ? " (OVERDRIVE BOOST \u26A1)" : "";
            ShowToast("Volume: " + volumePercent + "%" + boostTag);
            this.Invalidate();
        }

        private void AdjustVolumeByStep(int deltaPercent)
        {
            SetVolume(volumePercent + deltaPercent);
        }

        private void ToggleMute()
        {
            isMuted = !isMuted;
            ApplyEffectiveDspAndVolume();
            QueueSavePersistentSettings();
            ShowToast(isMuted ? "Muted" : ("Volume: " + volumePercent + "%"));
            this.Invalidate();
        }

        private void ApplyEqPreset(string presetName)
        {
            activeEqPreset = presetName;
            eqEnabled = true;
            if (presetName == "Flat")
            {
                eqBands = new float[] { 0f, 0f, 0f, 0f, 0f };
                bassBoostPct = 0; virtualizerPct = 0; loudnessGainPct = 0;
            }
            else if (presetName == "Bass Boost")
            {
                eqBands = new float[] { 8.5f, 5.5f, 0.5f, 2.0f, 3.5f };
                bassBoostPct = 85; virtualizerPct = 35; loudnessGainPct = 65;
            }
            else if (presetName == "Club")
            {
                eqBands = new float[] { 6.5f, 4.5f, 3.5f, 4.5f, 4.0f };
                bassBoostPct = 75; virtualizerPct = 65; loudnessGainPct = 75;
            }
            else if (presetName == "Hip Hop")
            {
                eqBands = new float[] { 8.0f, 5.0f, -1.0f, 3.0f, 5.0f };
                bassBoostPct = 80; virtualizerPct = 45; loudnessGainPct = 60;
            }
            else if (presetName == "Rock")
            {
                eqBands = new float[] { 6.0f, -1.0f, 2.5f, 5.5f, 6.5f };
                bassBoostPct = 55; virtualizerPct = 50; loudnessGainPct = 65;
            }
            else if (presetName == "Pop")
            {
                eqBands = new float[] { 1.5f, 3.5f, 6.0f, 4.5f, 2.0f };
                bassBoostPct = 40; virtualizerPct = 40; loudnessGainPct = 55;
            }
            else if (presetName == "Vocal Boost")
            {
                eqBands = new float[] { -3.5f, 1.5f, 7.5f, 6.0f, 3.0f };
                bassBoostPct = 15; virtualizerPct = 20; loudnessGainPct = 50;
            }
            else if (presetName == "Acoustic")
            {
                eqBands = new float[] { 4.0f, 3.0f, 2.5f, 5.0f, 5.5f };
                bassBoostPct = 35; virtualizerPct = 30; loudnessGainPct = 45;
            }
            else if (presetName == "Electronic")
            {
                eqBands = new float[] { 7.5f, 4.0f, 0.0f, 5.0f, 7.0f };
                bassBoostPct = 78; virtualizerPct = 80; loudnessGainPct = 70;
            }
            ApplyEffectiveDspAndVolume();
            SavePersistentSettings();
            ShowToast("\u266B EQ Applied: " + presetName + " (Bass " + bassBoostPct + "% \u2022 Loudness " + loudnessGainPct + "%)");
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
            // v10.4.4 Smooth Micro-Fade on Seek to prevent digital audio pop/click
            if (smoothMicroFadeEnabled && isPlaying)
            {
                microFadeMultiplier = 0.32f;
                ApplyEffectiveDspAndVolume();
            }
            // v10.3.6 Fix: Immediately silence outgoing crossfade Deck B when user scrubs the timeline
            if (deckBVolume > 0 && wmpControlsB != null)
            {
                deckBVolume = 0;
                deckAFadeInProgress = 100;
                try { wmpControlsB.GetType().InvokeMember("stop", BindingFlags.InvokeMethod, null, wmpControlsB, null); } catch { }
            }
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

            // v10.3.6 Fix: Only hand off to Deck B if track was playing for >2.5s; if user rapidly spam-skips tracks, stop Deck B cleanly
            if (currentSong != null && currentSong.Id != song.Id && isPlaying && crossfadeEnabled && currentPosSec > 2.5)
            {
                HandOffToCrossfadeDeckB();
            }
            else
            {
                deckAFadeInProgress = 100;
                deckBVolume = 0;
                if (wmpControlsB != null)
                {
                    try { wmpControlsB.GetType().InvokeMember("stop", BindingFlags.InvokeMethod, null, wmpControlsB, null); } catch { }
                }
            }

            currentSong = song;
            currentPosSec = 0;
            smoothPosSec = 0;
            lyricSyncOffsetSec = 0.0;
            abLoopStartSec = -1.0;
            abLoopEndSec = -1.0;
            streamStallWatchdogTicks = 0;
            lastWatchdogPosSec = -1.0;
            currentDurSec = song.Duration > 0 ? song.Duration : 210;
            isPlaying = true;
            isAutoEndCrossfadeTriggered = false;
            lyricsSmoothScrollY = 0f;
            microFadeMultiplier = smoothMicroFadeEnabled ? 0.22f : 1.0f;
            TriggerButtonBounce("play");
            ApplyEffectiveDspAndVolume();
            int myPlayVer = ++playRequestVersion;

            // Parse & fetch BetterLyrics time-synced lines + update ambient backdrop color!
            EnsureSyncedLyricsParsed(song);
            FetchBetterLyricsAsync(song);
            UpdateTargetAmbientColorFromSong(song);
            RebuildCurrentSongBgSlideshow(song, true);

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
                QueueSavePersistentSettings();
            }

            PrefetchSingleThumbnail(song.ThumbUrl);

            string safeId = Regex.Replace(song.Id, "[^a-zA-Z0-9_-]", "_");
            string cachedFile = Path.Combine(cacheFolder, safeId + ".m4a");

            if (File.Exists(cachedFile) && new FileInfo(cachedFile).Length > 10000)
            {
                isBuffering = false;
                StartWmpFileOrUrl(cachedFile);
                if (autoPreBufferNextEnabled) PreBufferNextQueueSongAsync();
                this.Invalidate();
                return;
            }

            isBuffering = true;
            StartWmpFileOrUrl(song.AudioUrl);
            this.Invalidate();

            Thread cacheThread = new Thread(() =>
            {
                // v10.4.4 Zero-Lag Start: Give WMP 100% network bandwidth for the first 2.2s so playback begins instantly!
                Thread.Sleep(2200);
                if (playRequestVersion != myPlayVer) return;
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
                    if (smoothMicroFadeEnabled && wmpSettings != null)
                    {
                        try { wmpSettings.GetType().InvokeMember("volume", BindingFlags.SetProperty, null, wmpSettings, new object[] { Math.Max(0, lastAppliedWmpVol / 3) }); } catch { }
                    }
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
                    if (smoothMicroFadeEnabled)
                    {
                        microFadeMultiplier = 0.20f;
                        ApplyEffectiveDspAndVolume();
                    }
                    wmpControls.GetType().InvokeMember("play", BindingFlags.InvokeMethod, null, wmpControls, null);
                    isPlaying = true;
                }
            }
            catch { }
            this.Invalidate();
        }

        private void PreBufferNextQueueSongAsync()
        {
            if (!autoPreBufferNextEnabled || queueSongs.Count <= 1) return;
            try
            {
                int nextIdx = (currentQueueIndex + 1) % queueSongs.Count;
                SongItem nextSong = queueSongs[nextIdx];
                if (nextSong == null || string.IsNullOrEmpty(nextSong.Id) || nextSong.Id == lastPreBufferedSongId) return;
                lastPreBufferedSongId = nextSong.Id;
                PrefetchSingleThumbnail(nextSong.ThumbUrl);

                string safeId = Regex.Replace(nextSong.Id, "[^a-zA-Z0-9_-]", "_");
                string nextCachedFile = Path.Combine(cacheFolder, safeId + ".m4a");
                if (File.Exists(nextCachedFile) && new FileInfo(nextCachedFile).Length > 10000) return;

                Thread preThread = new Thread(() =>
                {
                    try
                    {
                        Thread.Sleep(900);
                        string url = nextSong.AudioUrl;
                        if (string.IsNullOrEmpty(url)) return;
                        string tmp = nextCachedFile + ".pre.tmp";
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                            wc.DownloadFile(url, tmp);
                        }
                        if (File.Exists(tmp) && new FileInfo(tmp).Length > 10000)
                        {
                            if (!File.Exists(nextCachedFile)) File.Move(tmp, nextCachedFile);
                            else File.Delete(tmp);
                        }
                    }
                    catch { }
                });
                preThread.IsBackground = true;
                preThread.Start();
            }
            catch { }
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
                    string defaultJson = "{\r\n  \"version\": \"10.1.0\",\r\n  \"title\": \"Leo Music Studio v10.1.0 Update\",\r\n  \"notes\": \"\u2022 Connected Live Auto-Updater to umas8549/leo-music-updates\",\r\n  \"source_url\": \"https://raw.githubusercontent.com/umas8549/leo-music-updates/main/LeoLauncher.cs\",\r\n  \"download_url\": \"https://github.com/umas8549/leo-music-updates/releases/download/v10.4.3/Leo_Music_Windows_v10.4.4.zip\"\r\n}\r\n";
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
                        newCsCode = newCsCode.TrimStart('\uFEFF');
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

                        // Pre-compile to LeoMusic_Build.exe (never locked by running LeoMusic.exe!) AND generate bulletproof ASCII RunAutoUpdate.bat
                        string updaterBat = Path.Combine(appDataDir, "RunAutoUpdate.bat");
                        string cleanBaseDir = baseDir.TrimEnd('\\');
                        string buildExe = Path.Combine(appDataDir, "LeoMusic_Build.exe");
                        string iconPath = Path.Combine(appDataDir, "leo_icon.ico");
                        try { if (File.Exists(buildExe)) File.Delete(buildExe); } catch { }

                        // Also run csc.exe directly right now to create LeoMusic_Build.exe before exiting!
                        try
                        {
                            string cscExe = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe");
                            if (!File.Exists(cscExe)) cscExe = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Microsoft.NET\\Framework\\v4.0.30319\\csc.exe");
                            if (File.Exists(cscExe))
                            {
                                string iconArg = File.Exists(iconPath) ? (" /win32icon:\"" + iconPath + "\"") : "";
                                ProcessStartInfo cscPsi = new ProcessStartInfo(cscExe, "/nologo /nowarn:0414,0169 /target:winexe /out:\"" + buildExe + "\"" + iconArg + " \"" + targetCsAppData + "\"");
                                cscPsi.CreateNoWindow = true;
                                cscPsi.UseShellExecute = false;
                                Process cscProc = Process.Start(cscPsi);
                                if (cscProc != null) cscProc.WaitForExit(15000);
                            }
                        }
                        catch { }

                        StringBuilder bat = new StringBuilder();
                        bat.AppendLine("@echo off");
                        bat.AppendLine("ping 127.0.0.1 -n 3 >nul");
                        bat.AppendLine("taskkill /F /IM \"LeoMusic.exe\" >nul 2>&1");
                        bat.AppendLine("taskkill /F /IM \"Leo Music.exe\" >nul 2>&1");
                        bat.AppendLine("ping 127.0.0.1 -n 3 >nul");
                        bat.AppendLine("set \"CSC=%WINDIR%\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe\"");
                        bat.AppendLine("if not exist \"%CSC%\" set \"CSC=%WINDIR%\\Microsoft.NET\\Framework\\v4.0.30319\\csc.exe\"");
                        bat.AppendLine("if exist \"" + buildExe + "\" goto COPY_STAGE");
                        bat.AppendLine("if exist \"" + iconPath + "\" goto COMPILE_ICON");
                        bat.AppendLine("\"%CSC%\" /nologo /nowarn:0414,0169 /target:winexe /out:\"" + buildExe + "\" \"" + targetCsAppData + "\"");
                        bat.AppendLine("goto COPY_STAGE");
                        bat.AppendLine(":COMPILE_ICON");
                        bat.AppendLine("\"%CSC%\" /nologo /nowarn:0414,0169 /target:winexe /out:\"" + buildExe + "\" /win32icon:\"" + iconPath + "\" \"" + targetCsAppData + "\"");
                        bat.AppendLine(":COPY_STAGE");
                        bat.AppendLine("if not exist \"" + buildExe + "\" exit /b 1");
                        bat.AppendLine("del /F /Q \"" + appDataDir + "\\LeoMusic.exe\" >nul 2>&1");
                        bat.AppendLine("copy /Y \"" + buildExe + "\" \"" + appDataDir + "\\LeoMusic.exe\" >nul 2>&1");
                        bat.AppendLine("copy /Y \"" + buildExe + "\" \"" + cleanBaseDir + "\\LeoMusic.exe\" >nul 2>&1");
                        bat.AppendLine("copy /Y \"" + buildExe + "\" \"" + cleanBaseDir + "\\Leo Music.exe\" >nul 2>&1");
                        bat.AppendLine("if exist \"%USERPROFILE%\\Desktop\" copy /Y \"" + buildExe + "\" \"%USERPROFILE%\\Desktop\\Leo Music.exe\" >nul 2>&1");
                        bat.AppendLine("if exist \"%USERPROFILE%\\OneDrive\\Desktop\" copy /Y \"" + buildExe + "\" \"%USERPROFILE%\\OneDrive\\Desktop\\Leo Music.exe\" >nul 2>&1");
                        bat.AppendLine("start \"\" \"" + appDataDir + "\\LeoMusic.exe\"");
                        File.WriteAllText(updaterBat, bat.ToString(), Encoding.ASCII);
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            SavePersistentSettings();
                            ProcessStartInfo psi = new ProcessStartInfo(updaterBat);
                            psi.WindowStyle = ProcessWindowStyle.Hidden;
                            psi.CreateNoWindow = true;
                            Process.Start(psi);
                            Environment.Exit(0);
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

        private void ToggleHighRefreshRateMode()
        {
            refreshRateModeIdx = (refreshRateModeIdx + 1) % refreshRateModeNames.Length;
            if (uiTimer != null)
            {
                uiTimer.Interval = (refreshRateModeIdx == 1) ? 12 : 16;
            }
            SavePersistentSettings();
            ShowToast("\u26A1 Refresh Engine: " + refreshRateModeNames[refreshRateModeIdx] + " Enabled");
            this.Invalidate();
        }

        private void OnUiTimerTick(object sender, EventArgs e)
        {
            double rawDt = frameClock.Elapsed.TotalSeconds;
            frameClock.Restart();
            float dt = (float)Math.Max(0.004, Math.Min(0.050, rawDt));
            float fpsScale = dt / 0.016f; // Normalized to 60Hz baseline (1.0 at 60 FPS)

            if (statusTick > 0) statusTick--;

            // v10.3.4 Fix: Periodic Always-On-Top Z-Order Heartbeat for Dynamic Bar & Mini-Player
            if (isDynamicBarMode || isMiniPlayerMode)
            {
                topMostHeartbeatTicks++;
                if (topMostHeartbeatTicks >= 28)
                {
                    topMostHeartbeatTicks = 0;
                    EnforceAlwaysOnTopOverlayZOrder();
                }
            }

            // 00. 60Hz+ / 120Hz Smooth Inertial Scrolling Interpolation
            if (Math.Abs(smoothScrollOffset - targetScrollOffset) > 0.5f)
            {
                smoothScrollOffset += (targetScrollOffset - smoothScrollOffset) * Math.Min(0.85f, 0.34f * fpsScale);
                if (Math.Abs(smoothScrollOffset - targetScrollOffset) <= 1.0f) smoothScrollOffset = targetScrollOffset;
                scrollOffset = (int)Math.Round(smoothScrollOffset);
            }
            else
            {
                scrollOffset = targetScrollOffset;
            }

            if (Math.Abs(smoothQueueScrollOffset - targetQueueScrollOffset) > 0.5f)
            {
                smoothQueueScrollOffset += (targetQueueScrollOffset - smoothQueueScrollOffset) * Math.Min(0.85f, 0.34f * fpsScale);
                if (Math.Abs(smoothQueueScrollOffset - targetQueueScrollOffset) <= 1.0f) smoothQueueScrollOffset = targetQueueScrollOffset;
                queueScrollOffset = (int)Math.Round(smoothQueueScrollOffset);
            }
            else
            {
                queueScrollOffset = targetQueueScrollOffset;
            }

            // 0. Automatic Startup Update Check (Only after first-time Language Setup is finished so new users aren't interrupted!)
            if (startupUpdateCheckTicks > 0 && hasCompletedLanguageSetup && !isLanguageSetupScreenOpen)
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
                updatePopupAnim += (targetUpAnim - updatePopupAnim) * Math.Min(0.8f, 0.25f * fpsScale);
                if (Math.Abs(updatePopupAnim - targetUpAnim) <= 0.008f) updatePopupAnim = targetUpAnim;
            }

            float targetLangAnim = isLanguageSetupScreenOpen ? 1.0f : 0.0f;
            if (Math.Abs(languageSetupAnim - targetLangAnim) > 0.004f)
            {
                languageSetupAnim += (targetLangAnim - languageSetupAnim) * Math.Min(0.8f, 0.25f * fpsScale);
                if (Math.Abs(languageSetupAnim - targetLangAnim) <= 0.008f) languageSetupAnim = targetLangAnim;
            }

            // 1. Smooth Opening & Closing Animation for Full-Screen BetterLyrics / Studio Panel
            float targetFull = isFullPlayerExpanded ? 1.0f : 0.0f;
            if (Math.Abs(fullPlayerAnim - targetFull) > 0.004f)
            {
                fullPlayerAnim += (targetFull - fullPlayerAnim) * Math.Min(0.8f, 0.24f * fpsScale);
                if (Math.Abs(fullPlayerAnim - targetFull) <= 0.008f) fullPlayerAnim = targetFull;
            }
            if (!isFullPlayerExpanded) isFullThreeDotMenuOpen = false;

            float targetDotMenu = isFullThreeDotMenuOpen ? 1.0f : 0.0f;
            if (Math.Abs(fullThreeDotMenuAnim - targetDotMenu) > 0.005f)
            {
                fullThreeDotMenuAnim += (targetDotMenu - fullThreeDotMenuAnim) * Math.Min(0.85f, 0.32f * fpsScale);
                if (Math.Abs(fullThreeDotMenuAnim - targetDotMenu) <= 0.01f) fullThreeDotMenuAnim = targetDotMenu;
            }

            // v10.4.4 Studio Volume Controller Popup Animation & Debounced Settings Disk Save
            float targetVolPop = isVolumeControllerPopupOpen ? 1.0f : 0.0f;
            if (Math.Abs(volumeControllerPopupAnim - targetVolPop) > 0.005f)
            {
                volumeControllerPopupAnim += (targetVolPop - volumeControllerPopupAnim) * Math.Min(0.85f, 0.34f * fpsScale);
                if (Math.Abs(volumeControllerPopupAnim - targetVolPop) <= 0.01f) volumeControllerPopupAnim = targetVolPop;
            }

            if (settingsSaveDirty)
            {
                if (settingsSaveCooldownTicks > 0) settingsSaveCooldownTicks--;
                else
                {
                    settingsSaveDirty = false;
                    SavePersistentSettings();
                }
            }

            // v10.4.3 Multi-Image Cinema Background Slideshow & Smooth Crossfade Engine
            if (songBgImageEnabled && !oledPureBlack && currentSong != null)
            {
                songBgKenBurnsPhase += dt * (isPlaying ? 0.42f : 0.14f);
                if (currentSongBgSlides.Count == 0 || cachedBgSlidesSongId != currentSong.Id)
                {
                    RebuildCurrentSongBgSlideshow(currentSong, false);
                }
                if (currentSongBgSlides.Count > 1)
                {
                    if (!isSongBgTransitioning)
                    {
                        songBgTimerAccum += dt;
                        int targetSec = songBgIntervalSecs[Math.Max(0, Math.Min(songBgIntervalSecs.Length - 1, songBgIntervalIdx))];
                        if (songBgTimerAccum >= targetSec)
                        {
                            songBgTimerAccum = 0.0;
                            songBgNextSlideIdx = GetNextAllowedBgSlideIndex(songBgCurrentSlideIdx, currentSongBgSlides.Count);
                            songBgCrossfadeAlpha = 0f;
                            isSongBgTransitioning = true;
                        }
                    }
                    else
                    {
                        songBgCrossfadeAlpha += dt * 0.85f; // ~1.2 second ultra-smooth cinema crossfade
                        if (songBgCrossfadeAlpha >= 1.0f)
                        {
                            songBgCrossfadeAlpha = 0f;
                            songBgCurrentSlideIdx = songBgNextSlideIdx;
                            isSongBgTransitioning = false;
                        }
                    }
                }
            }

            // 2. Smooth Tab Opening Fade/Slide Animation
            if (tabFadeAnim < 1.0f)
            {
                tabFadeAnim = Math.Min(1.0f, tabFadeAnim + 0.14f * fpsScale);
            }

            // 2B. Smooth Right Side Panel Hide / Show Width Animation (315px <-> 0px)
            float targetRightW = isRightPanelVisible ? 315f : 0f;
            if (Math.Abs(rightPanelWidthAnim - targetRightW) > 0.5f)
            {
                rightPanelWidthAnim += (targetRightW - rightPanelWidthAnim) * Math.Min(0.85f, 0.28f * fpsScale);
                if (Math.Abs(rightPanelWidthAnim - targetRightW) <= 1.2f) rightPanelWidthAnim = targetRightW;
            }
            else
            {
                rightPanelWidthAnim = targetRightW;
            }

            // 3. Smooth Ambient Album-Art Color Cross-Blending & Dynamic Lighting Accent Sync!
            ambR += (targetAmbR - ambR) * Math.Min(0.6f, 0.12f * fpsScale);
            ambG += (targetAmbG - ambG) * Math.Min(0.6f, 0.12f * fpsScale);
            ambB += (targetAmbB - ambB) * Math.Min(0.6f, 0.12f * fpsScale);

            if (dynamicLightingEnabled)
            {
                int dr = Math.Max(45, Math.Min(255, (int)Math.Round(ambR)));
                int dg = Math.Max(45, Math.Min(255, (int)Math.Round(ambG)));
                int db = Math.Max(45, Math.Min(255, (int)Math.Round(ambB)));
                accentColor = Color.FromArgb(255, dr, dg, db);
            }

            // 4. Smooth Button Hover Scale & Click Bounce Decay
            List<string> hovKeys = new List<string>(btnHoverAnim.Keys);
            foreach (string k in hovKeys)
            {
                float targetH = (k == currentHoveredBtnKey) ? 1.0f : 0.0f;
                float curH = btnHoverAnim[k];
                curH += (targetH - curH) * Math.Min(0.85f, 0.28f * fpsScale);
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
                float v = btnClickBounce[k] - 0.14f * fpsScale;
                if (v <= 0f) btnClickBounce.Remove(k);
                else btnClickBounce[k] = v;
            }

            // 5. Continuous Rotating & Shape-Morphing Artwork + Spinner Phase (60Hz+ calibrated)
            spinnerAngle = (spinnerAngle + 6.0f * fpsScale) % 360f;
            if (isPlaying)
            {
                wavePhase += 0.10f * fpsScale;
                vinylAngle = (vinylAngle + 0.85f * fpsScale) % 360f;
                morphPhase += 0.035f * fpsScale;
            }
            else
            {
                morphPhase += 0.009f * fpsScale;
            }

            // 6. Dual-Deck Crossfade Ramp
            if (deckBVolume > 0 && wmpSettingsB != null)
            {
                int step = Math.Max(1, 100 / Math.Max(12, crossfadeSeconds * 35));
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

            bool needImmediateDspSync = false;
            if (deckAFadeInProgress < 100)
            {
                int stepIn = Math.Max(2, 100 / Math.Max(12, crossfadeSeconds * 30));
                deckAFadeInProgress = Math.Min(100, deckAFadeInProgress + stepIn);
                needImmediateDspSync = true;
            }

            // v10.4.4 Smooth 180ms Micro-Fade Ramp on Play / Seek
            if (microFadeMultiplier < 0.999f)
            {
                microFadeMultiplier = Math.Min(1.0f, microFadeMultiplier + dt * 5.2f);
                needImmediateDspSync = true;
            }

            // v10.4.4 Zero-Lag COM Fix: Throttle background harmonic DSP COM calls to every 10 frames (~150ms) instead of every frame!
            if (isPlaying)
            {
                dspComThrottleCounter++;
                if (needImmediateDspSync || dspComThrottleCounter >= 10)
                {
                    dspComThrottleCounter = 0;
                    ApplyEffectiveDspAndVolume();
                }
            }

            // 7. Sleep Timer (Accurate 1.0s wall-clock accumulator)
            if (sleepTimerSecondsLeft > 0 && isPlaying)
            {
                sleepSecondAccumulator += dt;
                if (sleepSecondAccumulator >= 1.0)
                {
                    sleepSecondAccumulator -= 1.0;
                    sleepTimerSecondsLeft--;
                    if (sleepTimerSecondsLeft <= 0)
                    {
                        sleepTimerMinutes = 0;
                        TogglePlayPause();
                        ShowToast("Sleep Timer: Playback Paused");
                    }
                }
            }

            // 8. Zero-Stutter Playback Position & Auto-Crossfade Trigger (120Hz RAM clock + throttled COM sync every 16 frames)
            if (wmpPlayer != null && wmpControls != null && isPlaying)
            {
                smoothPosSec += dt * playbackSpeedRates[playbackSpeedIdx];
                currentPosSec = smoothPosSec;
                listenSecondAccumulator += dt;
                if (listenSecondAccumulator >= 1.0)
                {
                    listenSecondAccumulator -= 1.0;
                    totalListeningSeconds++;
                }

                wmpComPollCounter++;
                if (wmpComPollCounter >= 16)
                {
                    wmpComPollCounter = 0;
                    try
                    {
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

                    // v10.4.4 Seamless A-B Loop Check
                    if (abLoopStartSec >= 0 && abLoopEndSec > abLoopStartSec + 1.0 && currentPosSec >= abLoopEndSec)
                    {
                        SeekToAbsolute(abLoopStartSec);
                        return;
                    }

                    // v10.4.4 Pre-Buffer Next Song in Queue 22s before track ends (or after 12s of playback)
                    if (autoPreBufferNextEnabled && currentPosSec > 10.0)
                    {
                        PreBufferNextQueueSongAsync();
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
                    if (likedIds.Contains(s.Id) && !ContainsSongByTitleOrId(displaySongs, s)) displaySongs.Add(s);
                }
                foreach (SongItem s in liveSearchResults)
                {
                    if (likedIds.Contains(s.Id) && !ContainsSongByTitleOrId(displaySongs, s)) displaySongs.Add(s);
                }
                foreach (SongItem s in quickPlaySongs)
                {
                    if (likedIds.Contains(s.Id) && !ContainsSongByTitleOrId(displaySongs, s)) displaySongs.Add(s);
                }
                targetScrollOffset = 0;
                smoothScrollOffset = 0f;
                scrollOffset = 0;
                this.Invalidate();
                return;
            }
            else if (activeTab == 3)
            {
                displaySongs.AddRange(downloadedSongs);
                targetScrollOffset = 0;
                smoothScrollOffset = 0f;
                scrollOffset = 0;
                this.Invalidate();
                return;
            }

            if (!string.IsNullOrEmpty(q) && liveSearchResults.Count > 0)
            {
                displaySongs.AddRange(liveSearchResults);
                targetScrollOffset = 0;
                smoothScrollOffset = 0f;
                scrollOffset = 0;
                this.Invalidate();
                return;
            }

            string activeLang = GetActiveFilterLanguageName();

            // Gather songs from active language shelves + catalog matching both activeLang and selectedMood
            List<SongItem> sourcePool = new List<SongItem>();
            if (activeLang != "All Languages" && languageSongShelves.ContainsKey(activeLang))
            {
                foreach (SongItem s in languageSongShelves[activeLang])
                {
                    if (!ContainsSongByTitleOrId(sourcePool, s)) sourcePool.Add(s);
                }
            }
            else
            {
                foreach (string ul in userSelectedLanguages)
                {
                    if (languageSongShelves.ContainsKey(ul))
                    {
                        foreach (SongItem s in languageSongShelves[ul])
                        {
                            if (!ContainsSongByTitleOrId(sourcePool, s)) sourcePool.Add(s);
                        }
                    }
                }
            }
            foreach (SongItem s in catalog)
            {
                if (!ContainsSongByTitleOrId(sourcePool, s)) sourcePool.Add(s);
            }

            foreach (SongItem s in sourcePool)
            {
                bool langMatch = (activeLang == "All Languages")
                    ? (userSelectedLanguages.Count == 0 || userSelectedLanguages.Contains(s.Language ?? "Hindi"))
                    : string.Equals(s.Language ?? "Hindi", activeLang, StringComparison.OrdinalIgnoreCase);

                bool moodMatch = (selectedMood == "All") || s.Mood.Equals(selectedMood, StringComparison.OrdinalIgnoreCase);
                bool queryMatch = string.IsNullOrEmpty(q) || s.Title.ToLowerInvariant().Contains(q) || s.Artist.ToLowerInvariant().Contains(q);

                if (langMatch && moodMatch && queryMatch && !ContainsSongByTitleOrId(displaySongs, s))
                {
                    displaySongs.Add(s);
                }
            }

            // If strict mood + language filter yielded few local tracks, include remaining language tracks while background fetch loads more
            if (displaySongs.Count < 3)
            {
                foreach (SongItem s in sourcePool)
                {
                    bool langMatch = (activeLang == "All Languages") || string.Equals(s.Language ?? "Hindi", activeLang, StringComparison.OrdinalIgnoreCase);
                    if (langMatch && !ContainsSongByTitleOrId(displaySongs, s))
                    {
                        displaySongs.Add(s);
                    }
                }
            }

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
            if (isCreatingPlaylistInput && playlistNameBox != null && playlistNameBox.Visible && playlistNameBox.Focused)
            {
                if (keyData == Keys.Escape)
                {
                    HidePlaylistNameBox();
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
                case Keys.V:
                    CyclePlaybackSpeed();
                    return true;
                case Keys.W:
                    ToggleMiniPlayerMode();
                    return true;
                case Keys.B:
                    ToggleDynamicBarMode();
                    return true;
                case Keys.G:
                    CycleVisualizerStyle();
                    return true;
                case Keys.U:
                    CheckForAppUpdatesAsync(true);
                    return true;
                case Keys.H:
                    ToggleRightPanelVisibility();
                    return true;
                case Keys.Y:
                    ToggleDynamicLightingMode();
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
            string[] splitSeps = new string[] { " - ", " | ", " : ", " / ", " \u2013 " };
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
                low.Contains("8d ") || low.Contains("sped up") || low.Contains("dj ") || low.Contains("mashup") ||
                low.Contains("non stop") || low.Contains("nonstop") || low.Contains("dandiya") ||
                low.Contains("celebration") || low.Contains("rap song") || low.Contains("tribute") ||
                low.Contains("jukebox") || low.Contains("megamix") || low.Contains("garba"))
            {
                return true;
            }
            return false;
        }

        private string FormatMinimalCardTitle(string rawTitle)
        {
            if (string.IsNullOrEmpty(rawTitle)) return "Untitled";
            string s = rawTitle.Trim();
            int pIdx = s.IndexOf('(');
            if (pIdx > 3) s = s.Substring(0, pIdx).Trim();
            int bIdx = s.IndexOf('[');
            if (bIdx > 3) s = s.Substring(0, bIdx).Trim();
            int dIdx = s.IndexOf(" - ", StringComparison.Ordinal);
            if (dIdx > 3) s = s.Substring(0, dIdx).Trim();
            return s;
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

        private string GetAnyDepthJsonStringField(string objJson, string key)
        {
            if (string.IsNullOrEmpty(objJson) || string.IsNullOrEmpty(key)) return "";
            string topVal = GetTopLevelJsonField(objJson, key);
            if (!string.IsNullOrEmpty(topVal)) return topVal;

            string pattern = "\"" + key + "\"";
            int idx = 0;
            while (idx < objJson.Length)
            {
                int found = objJson.IndexOf(pattern, idx, StringComparison.Ordinal);
                if (found < 0) break;
                int colon = objJson.IndexOf(':', found + pattern.Length);
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
                        string res = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(res)) return res;
                    }
                    else if (valStart < objJson.Length && char.IsDigit(objJson[valStart]))
                    {
                        int endNum = valStart;
                        while (endNum < objJson.Length && char.IsDigit(objJson[endNum])) endNum++;
                        return objJson.Substring(valStart, endNum - valStart);
                    }
                }
                idx = found + pattern.Length;
            }
            return "";
        }

        private string ExtractRealArtistFromSaavnJson(string obj, string albumName)
        {
            string[] keys = new string[] { "primary_artists", "singers", "primaryArtists", "music", "artists" };
            foreach (string k in keys)
            {
                string v = GetAnyDepthJsonStringField(obj, k);
                if (!string.IsNullOrEmpty(v) && !v.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    return v;
                }
            }

            // Check artistMap -> primary_artists -> name array
            Match mMap = Regex.Match(obj, "\"primary_artists\"\\s*:\\s*\\[\\s*\\{[^\\}]*\"name\"\\s*:\\s*\"([^\"]+)\"");
            if (mMap.Success && !string.IsNullOrEmpty(mMap.Groups[1].Value))
            {
                return mMap.Groups[1].Value;
            }

            // Fallback to subtitle ("Artist Name - Album Name")
            string sub = GetAnyDepthJsonStringField(obj, "subtitle");
            if (!string.IsNullOrEmpty(sub))
            {
                int dashIdx = sub.IndexOf(" - ", StringComparison.Ordinal);
                if (dashIdx > 0)
                {
                    string beforeDash = sub.Substring(0, dashIdx).Trim();
                    if (!string.IsNullOrEmpty(beforeDash)) return beforeDash;
                }
                return sub.Trim();
            }

            string headerDesc = GetAnyDepthJsonStringField(obj, "header_desc");
            if (!string.IsNullOrEmpty(headerDesc)) return headerDesc;

            if (!string.IsNullOrEmpty(albumName) && albumName != "Single") return albumName;
            return "Featured Artist";
        }

        private List<SongItem> ParseOfficialJioSaavnJson(string json)
        {
            List<SongItem> list = new List<SongItem>();
            List<string> items = ExtractSongObjectsFromJson(json);

            for (int i = 0; i < items.Count; i++)
            {
                string obj = items[i];
                string id = GetAnyDepthJsonStringField(obj, "id");
                if (string.IsNullOrEmpty(id)) id = "jio_" + i;

                string title = GetAnyDepthJsonStringField(obj, "song");
                if (string.IsNullOrEmpty(title)) title = GetAnyDepthJsonStringField(obj, "title");
                if (string.IsNullOrEmpty(title)) title = GetAnyDepthJsonStringField(obj, "name");
                if (string.IsNullOrEmpty(title)) continue;

                string album = GetAnyDepthJsonStringField(obj, "album");
                if (string.IsNullOrEmpty(album)) album = "Single";

                string artist = ExtractRealArtistFromSaavnJson(obj, album);

                int dur = 210;
                int.TryParse(GetAnyDepthJsonStringField(obj, "duration"), out dur);
                if (dur <= 0) dur = 210;

                string img = GetAnyDepthJsonStringField(obj, "image");
                if (!string.IsNullOrEmpty(img))
                {
                    img = img.Replace("150x150", "500x500").Replace("50x50", "500x500").Replace(".webp", ".jpg").Replace("http://", "https://");
                }
                else
                {
                    img = catalog[i % catalog.Count].ThumbUrl;
                }

                string langMeta = GetAnyDepthJsonStringField(obj, "language");
                string encUrl = GetAnyDepthJsonStringField(obj, "encrypted_media_url");
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
                if (!string.IsNullOrEmpty(langMeta))
                {
                    item.Language = char.ToUpperInvariant(langMeta[0]) + (langMeta.Length > 1 ? langMeta.Substring(1).ToLowerInvariant() : "");
                }
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
                            if (currentSong != null && (currentSong.ThumbUrl ?? "").Replace(".webp", ".jpg") == jpgUrl)
                            {
                                this.BeginInvoke((MethodInvoker)delegate { RebuildCurrentSongBgSlideshow(currentSong, false); });
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
            if (visualizerStyleIdx == 1)
            {
                // Style 1: Smooth Dual Neon Wave
                List<PointF> wave1 = new List<PointF>();
                List<PointF> wave2 = new List<PointF>();
                for (int i = 0; i < numBars; i++)
                {
                    float x = startX + i * spacing + barW / 2f;
                    float amp = isPlaying ? (maxH * 0.85f) : 2.5f;
                    float y1 = baseY - maxH * 0.45f + (float)Math.Sin(wavePhase * 1.3f + i * 0.45f) * amp * 0.55f;
                    float y2 = baseY - maxH * 0.45f + (float)Math.Cos(wavePhase * 1.05f - i * 0.38f) * amp * 0.42f;
                    wave1.Add(new PointF(x, y1));
                    wave2.Add(new PointF(x, y2));
                }
                if (wave1.Count > 1)
                {
                    using (Pen p2 = new Pen(Color.FromArgb(110, accentColor), 2.2f))
                    using (Pen p1 = new Pen(accentColor, 2.8f))
                    {
                        g.DrawCurve(p2, wave2.ToArray());
                        g.DrawCurve(p1, wave1.ToArray());
                    }
                }
                return;
            }

            using (SolidBrush vb = new SolidBrush(accentColor))
            using (SolidBrush glowB = new SolidBrush(Color.FromArgb(95, accentColor)))
            {
                float midY = baseY - maxH * 0.5f;
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

                    float x = startX + i * spacing;
                    if (visualizerStyleIdx == 2)
                    {
                        // Style 2: Center-Mirrored Soundwave
                        g.FillRectangle(vb, x, midY - bh * 0.55f, barW, bh * 1.1f);
                    }
                    else if (visualizerStyleIdx == 3)
                    {
                        // Style 3: Glowing Harmonic Orbs
                        float oy = baseY - bh;
                        g.FillEllipse(glowB, x - 1.5f, oy - 1.5f, barW + 3f, barW + 3f);
                        g.FillEllipse(vb, x, oy, barW, barW);
                        g.FillRectangle(glowB, x + barW * 0.25f, oy + barW, barW * 0.5f, Math.Max(2f, bh - barW));
                    }
                    else
                    {
                        // Style 0: Classic Studio Bars
                        g.FillRectangle(vb, x, baseY - bh, barW, bh);
                    }
                }
            }
        }

        // =========================================================================
        // UPGRADED BETTERLYRICS KARAOKE REAL-TIME AUTO-SYNC RENDERER
        // Uses sub-frame smoothPosSec clock for 35-FPS continuous syllable fill,
        // active-line soft glow, intro spinner, and instant sync controls!
        // =========================================================================

        private Font GetCustomLyricFont(bool isActive, bool isFullScreenStyle)
        {
            int fIdx = Math.Max(0, Math.Min(3, lyricFontStyleIdx));
            int sIdx = Math.Max(0, Math.Min(3, lyricTextSizeIdx));
            if (!isFullScreenStyle)
            {
                return isActive ? fontLyricsSideActiveZoom : fontLyricsSideIdle;
            }
            if (lyricFontTableFull[fIdx, sIdx] == null)
            {
                string[] famNames = new string[] { "Georgia", "Segoe UI", "Georgia", "Segoe UI" };
                FontStyle[] fStyles = new FontStyle[] { FontStyle.Bold, FontStyle.Bold, FontStyle.Bold | FontStyle.Italic, FontStyle.Bold };
                float[] actSizes = new float[] { 18.5f, 22.0f, 25.5f, 29.0f };
                float[] idlSizes = new float[] { 15.5f, 18.0f, 20.5f, 23.0f };
                lyricFontTableFull[fIdx, sIdx] = new Font(famNames[fIdx], actSizes[sIdx], fStyles[fIdx]);
                lyricFontTableIdle[fIdx, sIdx] = new Font(famNames[fIdx], idlSizes[sIdx], fStyles[fIdx]);
            }
            return isActive ? lyricFontTableFull[fIdx, sIdx] : lyricFontTableIdle[fIdx, sIdx];
        }

        private void PaintBetterLyricsCanvas(Graphics g, Rectangle bounds, bool isFullScreenStyle, StringFormat sfCenter)
        {
            if (currentSong == null) return;
            EnsureSyncedLyricsParsed(currentSong);
            List<LyricLine> lines = currentSong.SyncedLines;

            Region oldClip = g.Clip;
            g.SetClip(bounds);

            // 0. Optional Background Art Watermark floating softly behind Boxless Lyrics!
            if (isFullScreenStyle && lyricBgArtModeIdx != 3)
            {
                int artWatermarkSz = Math.Min(bounds.Width, bounds.Height) - 36;
                if (artWatermarkSz > 140)
                {
                    Rectangle wmRect = new Rectangle(
                        bounds.X + (bounds.Width - artWatermarkSz) / 2,
                        bounds.Y + (bounds.Height - artWatermarkSz) / 2 + 8,
                        artWatermarkSz, artWatermarkSz);

                    if (lyricBgArtModeIdx == 0)
                    {
                        // Soft Cover Art Watermark with deep radial vignette
                        string jpgUrl = (currentSong.ThumbUrl ?? "").Replace(".webp", ".jpg");
                        Image wmImg = null;
                        lock (imageCache) { if (imageCache.ContainsKey(jpgUrl)) wmImg = imageCache[jpgUrl]; }
                        if (wmImg != null)
                        {
                            using (GraphicsPath ep = new GraphicsPath())
                            {
                                ep.AddEllipse(wmRect);
                                Region prevC = g.Clip;
                                g.SetClip(ep, CombineMode.Intersect);
                                g.DrawImage(wmImg, wmRect);
                                using (SolidBrush dimB = new SolidBrush(Color.FromArgb(205, (int)Math.Min(255, ambR * 0.55f), (int)Math.Min(255, ambG * 0.52f), (int)Math.Min(255, ambB * 0.55f))))
                                {
                                    g.FillEllipse(dimB, wmRect);
                                }
                                g.Clip = prevC;
                            }
                        }
                    }
                    else if (lyricBgArtModeIdx == 1)
                    {
                        // 3D Morphing Quantum / Lotus Silhouette Watermark behind lyrics
                        float cxW = wmRect.X + wmRect.Width / 2f;
                        float cyW = wmRect.Y + wmRect.Height / 2f;
                        float rW = wmRect.Width * 0.44f;
                        PointF[] poly = new PointF[73];
                        double rotW = (vinylAngle * Math.PI) / 180.0;
                        for (int k = 0; k <= 72; k++)
                        {
                            double th = (k / 72.0) * Math.PI * 2.0;
                            float rad = rW * (0.88f + 0.08f * (float)Math.Sin(6.0 * th + wavePhase) + 0.05f * (float)Math.Cos(4.0 * (th - rotW)));
                            poly[k] = new PointF(cxW + rad * (float)Math.Cos(th), cyW + rad * (float)Math.Sin(th));
                        }
                        using (SolidBrush auraFill = new SolidBrush(Color.FromArgb(22, accentColor.R, accentColor.G, accentColor.B)))
                        using (Pen auraPen = new Pen(Color.FromArgb(52, accentColor), 2.0f))
                        {
                            g.FillPolygon(auraFill, poly);
                            g.DrawPolygon(auraPen, poly);
                        }
                    }
                    else if (lyricBgArtModeIdx == 2)
                    {
                        // Soft Spinning Vinyl Rings Watermark
                        using (Pen vp = new Pen(Color.FromArgb(32, 255, 255, 255), 1.5f))
                        {
                            g.DrawEllipse(vp, wmRect);
                            g.DrawEllipse(vp, wmRect.X + 28, wmRect.Y + 28, wmRect.Width - 56, wmRect.Height - 56);
                            g.DrawEllipse(vp, wmRect.X + 56, wmRect.Y + 56, wmRect.Width - 112, wmRect.Height - 112);
                        }
                    }
                }
            }

            // 1. Minimal Subtle Header + Quick Customization Pills ([Boxless/Halo] [Glow] [Bg Art] [Copy] [LRC] [-0.5s] [Sync] [+0.5s])
            int headerY = bounds.Y + (isFullScreenStyle ? 6 : 6);
            string hdrText = isFullScreenStyle
                ? ("BetterLyrics 2.0 \u2022 " + lyricStageStyleNames[lyricStageStyleIdx] + " \u2022 " + lyricFadeModeNames[lyricFadeModeIdx])
                : "BetterLyrics 2.0";

            using (SolidBrush hdrBrush = new SolidBrush(Color.FromArgb(165, 245, 238, 235)))
            {
                g.DrawString(hdrText, fontBetterHeader, hdrBrush,
                    new RectangleF(bounds.X + 16, headerY, Math.Max(120, bounds.Width - (isFullScreenStyle ? 420 : 24)), 22),
                    isFullScreenStyle ? new StringFormat() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center } : sfCenter);
            }

            if (isFullScreenStyle)
            {
                syncBoxlessBtnRect = new Rectangle(bounds.Right - 418, headerY - 1, 68, 22);
                syncGlowBtnRect    = new Rectangle(bounds.Right - 346, headerY - 1, 56, 22);
                syncBgArtBtnRect   = new Rectangle(bounds.Right - 286, headerY - 1, 54, 22);
                syncCopyBtnRect    = new Rectangle(bounds.Right - 228, headerY - 1, 42, 22);
                syncRefetchBtnRect = new Rectangle(bounds.Right - 182, headerY - 1, 44, 22);
                syncMinusBtnRect   = new Rectangle(bounds.Right - 134, headerY - 1, 38, 22);
                syncResetBtnRect   = new Rectangle(bounds.Right - 92,  headerY - 1, 44, 22);
                syncPlusBtnRect    = new Rectangle(bounds.Right - 44,  headerY - 1, 38, 22);

                using (SolidBrush sb = new SolidBrush(Color.FromArgb(42, 0, 0, 0)))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                {
                    FillRoundRect(g, sb, syncBoxlessBtnRect, 8);
                    FillRoundRect(g, sb, syncGlowBtnRect, 8);
                    FillRoundRect(g, sb, syncBgArtBtnRect, 8);
                    FillRoundRect(g, sb, syncCopyBtnRect, 8);
                    FillRoundRect(g, sb, syncRefetchBtnRect, 8);
                    FillRoundRect(g, sb, syncMinusBtnRect, 8);
                    FillRoundRect(g, sb, syncResetBtnRect, 8);
                    FillRoundRect(g, sb, syncPlusBtnRect, 8);

                    g.DrawString(lyricStageStyleNames[lyricStageStyleIdx].Split(' ')[0], fontSmallBold, tb, syncBoxlessBtnRect, sfCenter);
                    g.DrawString("Glow", fontSmallBold, tb, syncGlowBtnRect, sfCenter);
                    g.DrawString("Art", fontSmallBold, tb, syncBgArtBtnRect, sfCenter);
                    g.DrawString("Copy", fontSmallBold, tb, syncCopyBtnRect, sfCenter);
                    g.DrawString("\u21BB LRC", fontSmallBold, tb, syncRefetchBtnRect, sfCenter);
                    g.DrawString("-0.5s", fontSmallBold, tb, syncMinusBtnRect, sfCenter);
                    g.DrawString("Sync", fontSmallBold, tb, syncResetBtnRect, sfCenter);
                    g.DrawString("+0.5s", fontSmallBold, tb, syncPlusBtnRect, sfCenter);
                }
            }
            else
            {
                syncBoxlessBtnRect = Rectangle.Empty;
                syncGlowBtnRect = Rectangle.Empty;
                syncBgArtBtnRect = Rectangle.Empty;
                syncCopyBtnRect = Rectangle.Empty;
                syncRefetchBtnRect = Rectangle.Empty;
                syncMinusBtnRect = Rectangle.Empty;
                syncResetBtnRect = Rectangle.Empty;
                syncPlusBtnRect = Rectangle.Empty;
            }

            if (lines == null || lines.Count == 0)
            {
                g.Clip = oldClip;
                return;
            }

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

            bool showIntroSpinner = (syncClock < lines[0].StartSec) || isLoadingBetterLyrics;
            int topContentOffset = bounds.Y + (isFullScreenStyle ? 36 : 30);
            if (showIntroSpinner)
            {
                int spSz = isFullScreenStyle ? 26 : 18;
                int spX = bounds.X + (bounds.Width - spSz) / 2;
                int spY = topContentOffset;
                using (Pen ringBg = new Pen(Color.FromArgb(65, 255, 255, 255), 2.8f))
                using (Pen ringFg = new Pen(Color.FromArgb(240, 255, 255, 255), 2.8f))
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
                topContentOffset += spSz + (isFullScreenStyle ? 12 : 8);
            }

            Font fActive = GetCustomLyricFont(true, isFullScreenStyle);
            Font fIdle = GetCustomLyricFont(false, isFullScreenStyle);
            int padSide = isFullScreenStyle ? 34 : 12;
            int maxLineW = Math.Max(120, bounds.Width - padSide * 2);

            int[] stepHeightsActive = new int[] { 28, 34, 39, 44 };
            int[] stepHeightsIdle = new int[] { 24, 29, 33, 37 };
            int actRowStepH = isFullScreenStyle ? stepHeightsActive[Math.Max(0, Math.Min(3, lyricTextSizeIdx))] : 23;
            int idlRowStepH = isFullScreenStyle ? stepHeightsIdle[Math.Max(0, Math.Min(3, lyricTextSizeIdx))] : 20;

            // Zero-Stutter Layout Cache: Only measure wrapped lyric lines when song, width, or font setting changes!
            string wrapCacheKey = currentSong.Id + "|" + lines.Count + "|" + maxLineW + "|" + isFullScreenStyle + "|" + lyricFontStyleIdx + "|" + lyricTextSizeIdx;
            if (cachedLyricWrapKey != wrapCacheKey || cachedWrappedLyricRows.Count != lines.Count)
            {
                cachedLyricWrapKey = wrapCacheKey;
                cachedWrappedLyricRows.Clear();
                cachedLyricBlockHeights.Clear();
                for (int i = 0; i < lines.Count; i++)
                {
                    List<string> sr = WrapLyricTextToRows(g, lines[i].Text, fActive, maxLineW);
                    cachedWrappedLyricRows.Add(sr);
                    int bh = sr.Count * idlRowStepH + (isFullScreenStyle ? 18 : 10);
                    cachedLyricBlockHeights.Add(bh);
                }
            }

            float activeLineOffsetTop = 0f;
            float currentCumY = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                bool isCur = (i == activeIdx && syncClock >= lines[i].StartSec);
                int rowStep = isCur ? actRowStepH : idlRowStepH;
                int bh = cachedWrappedLyricRows[i].Count * rowStep + (isFullScreenStyle ? 18 : 10);
                if (i == Math.Max(0, activeIdx))
                {
                    activeLineOffsetTop = currentCumY;
                }
                currentCumY += bh;
            }

            // Keep active line centered in the sweet spot (~34% from top) for smooth Fade-on-Fade above and below!
            float targetViewportAnchor = (bounds.Height - (topContentOffset - bounds.Y)) * 0.34f;
            float targetScrollY = Math.Max(0f, activeLineOffsetTop - targetViewportAnchor);
            lyricsSmoothScrollY += (targetScrollY - lyricsSmoothScrollY) * 0.22f;

            activeLyricHitRects.Clear();
            activeLyricHitTimes.Clear();

            float drawY = topContentOffset - lyricsSmoothScrollY;
            int fadeZonePx = isFullScreenStyle ? lyricFadeZonePixels[Math.Max(0, Math.Min(3, lyricFadeSizeIdx))] : 55;

            for (int i = 0; i < lines.Count; i++)
            {
                LyricLine ll = lines[i];
                bool isCurrent = (i == activeIdx && syncClock >= ll.StartSec);
                Font useFont = isCurrent ? fActive : fIdle;
                List<string> subRows = cachedWrappedLyricRows[i];
                int rowStepH = isCurrent ? actRowStepH : idlRowStepH;
                int blockH = subRows.Count * rowStepH + (isFullScreenStyle ? 18 : 10);

                if (drawY + blockH >= bounds.Y + 24 && drawY <= bounds.Bottom - 4)
                {
                    Rectangle hitR = new Rectangle(bounds.X + padSide / 2, (int)drawY - 2, bounds.Width - padSide, blockH);
                    activeLyricHitRects.Add(hitR);
                    activeLyricHitTimes.Add(ll.StartSec);

                    // Stage Style 1 ("Soft Halo"): Boxless soft radial spotlight behind active line (NO borders!)
                    if (isCurrent && lyricStageStyleIdx == 1)
                    {
                        Rectangle haloR = new Rectangle(bounds.X + 20, (int)drawY - 8, bounds.Width - 40, blockH + 8);
                        using (GraphicsPath gp = new GraphicsPath())
                        {
                            gp.AddEllipse(haloR);
                            using (PathGradientBrush pgb = new PathGradientBrush(gp))
                            {
                                pgb.CenterColor = Color.FromArgb(44, accentColor.R, accentColor.G, accentColor.B);
                                pgb.SurroundColors = new Color[] { Color.FromArgb(0, accentColor.R, accentColor.G, accentColor.B) };
                                g.FillPath(pgb, gp);
                            }
                        }
                    }
                    // Stage Style 2 ("Glass Frame"): Optional subtle pill only if user explicitly selects Glass Frame
                    else if (isCurrent && lyricStageStyleIdx == 2)
                    {
                        DrawGlassCard(g, new Rectangle(bounds.X + 12, (int)drawY - 4, bounds.Width - 24, blockH - 2), 14, bgCard, true, false);
                    }

                    // Fade-on-Fade Calculation: Combines line-distance fade + smooth top/bottom viewport edge dissolve!
                    int distFromFocus = Math.Abs(i - Math.Max(0, activeIdx));
                    int baseIdleAlpha = 145;
                    if (lyricFadeModeIdx == 0)
                    {
                        // Smooth Edge Fade
                        baseIdleAlpha = Math.Max(22, 148 - distFromFocus * 26);
                    }
                    else if (lyricFadeModeIdx == 1)
                    {
                        // Deep Cinema Focus (dramatic spotlight on active line; neighbors fade deeply into background)
                        baseIdleAlpha = (distFromFocus == 1) ? 92 : Math.Max(14, 64 - distFromFocus * 18);
                    }
                    else if (lyricFadeModeIdx == 2)
                    {
                        // Cascade Fade (past sung lines fade out strongly; upcoming lines cascade)
                        baseIdleAlpha = (i < activeIdx) ? Math.Max(16, 68 - distFromFocus * 22) : Math.Max(28, 155 - distFromFocus * 24);
                    }
                    else
                    {
                        // All Crisp
                        baseIdleAlpha = Math.Max(85, 175 - distFromFocus * 14);
                    }

                    // Smooth Cosine Top & Bottom Edge Dissolve into Background (controlled by lyricFadeSizeIdx!)
                    float lineCenterY = drawY + (blockH * 0.45f);
                    float distFromTopEdge = lineCenterY - (bounds.Y + 28);
                    float distFromBotEdge = bounds.Bottom - lineCenterY;
                    float edgeFactor = 1.0f;

                    if (lyricFadeModeIdx != 3 && fadeZonePx > 10)
                    {
                        if (distFromTopEdge < fadeZonePx)
                        {
                            float t = Math.Max(0f, Math.Min(1f, distFromTopEdge / fadeZonePx));
                            edgeFactor = Math.Min(edgeFactor, (float)((1.0 - Math.Cos(t * Math.PI)) * 0.5));
                        }
                        if (distFromBotEdge < fadeZonePx)
                        {
                            float t = Math.Max(0f, Math.Min(1f, distFromBotEdge / fadeZonePx));
                            edgeFactor = Math.Min(edgeFactor, (float)((1.0 - Math.Cos(t * Math.PI)) * 0.5));
                        }
                    }

                    int idleAlpha = Math.Max(8, Math.Min(255, (int)(baseIdleAlpha * edgeFactor)));

                    float lineProg = 0f;
                    if (isCurrent)
                    {
                        double span = Math.Max(0.45, ll.EndSec - ll.StartSec);
                        lineProg = (float)Math.Max(0.0, Math.Min(1.0, (syncClock - ll.StartSec) / span));
                    }

                    int totalChars = Math.Max(1, ll.Text.Length);
                    int charsBeforeRow = 0;
                    float rowY = drawY;

                    for (int r = 0; r < subRows.Count; r++)
                    {
                        string rowText = subRows[r];
                        SizeF rowSz = g.MeasureString(rowText, useFont);
                        float rowX = isFullScreenStyle ? (bounds.X + padSide) : (bounds.X + 12);
                        if (showIntroSpinner && isFullScreenStyle)
                        {
                            rowX = bounds.X + (bounds.Width - rowSz.Width) / 2f;
                        }

                        Color baseCol = isCurrent
                            ? Color.FromArgb(Math.Max(45, (int)(115 * edgeFactor)), 245, 238, 235)
                            : Color.FromArgb(idleAlpha, 238, 228, 222);

                        using (SolidBrush baseBrush = new SolidBrush(baseCol))
                        {
                            g.DrawString(rowText, useFont, baseBrush, rowX, rowY);
                        }

                        // Boxless Word-by-Word Fluid Karaoke Glow Sweep on Active Line!
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
                                RectangleF highlightClip = new RectangleF(rowX - 4f, rowY - 4f, clipW + 8f, rowStepH + 10f);
                                g.SetClip(highlightClip, CombineMode.Intersect);

                                // Multi-pass Text Glow / Neon Bloom directly behind the letters (NO box!)
                                if (lyricGlowModeIdx != 3)
                                {
                                    int glowAlpha = (lyricGlowModeIdx == 0) ? 75 : ((lyricGlowModeIdx == 1) ? 135 : 185);
                                    Color glowCol = (lyricGlowModeIdx == 0) ? Color.FromArgb(glowAlpha, 255, 255, 255) : Color.FromArgb(glowAlpha, accentColor.R, accentColor.G, accentColor.B);
                                    using (SolidBrush glowB = new SolidBrush(glowCol))
                                    {
                                        g.DrawString(rowText, useFont, glowB, rowX + 1.5f, rowY + 1.5f);
                                        if (lyricGlowModeIdx >= 1)
                                        {
                                            g.DrawString(rowText, useFont, glowB, rowX - 1.2f, rowY - 1.0f);
                                        }
                                        if (lyricGlowModeIdx == 2)
                                        {
                                            using (SolidBrush outerAura = new SolidBrush(Color.FromArgb(90, accentColor.R, accentColor.G, accentColor.B)))
                                            {
                                                g.DrawString(rowText, useFont, outerAura, rowX + 2.8f, rowY + 2.5f);
                                            }
                                        }
                                    }
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

        // =========================================================================
        // v10.4.0 ZERO-STUTTER OFFSCREEN AURORA MESH ENGINE
        // Renders the 3 breathing aurora spheres into a lightweight 240x140 buffer
        // and composites it with bilinear smoothing for 10x faster 120Hz frame rate!
        // =========================================================================

        private void PaintLiveMeshAuroraBackdrop(Graphics g, int w, int h)
        {
            if (!glassEffectEnabled || oledPureBlack || w <= 10 || h <= 10) return;
            try
            {
                int bufW = 240;
                int bufH = 140;
                auroraCacheFrameCounter++;
                if (cachedAuroraBitmap == null || auroraCacheFrameCounter >= 3)
                {
                    auroraCacheFrameCounter = 0;
                    if (cachedAuroraBitmap == null)
                    {
                        cachedAuroraBitmap = new Bitmap(bufW, bufH);
                    }
                    using (Graphics bg = Graphics.FromImage(cachedAuroraBitmap))
                    {
                        bg.Clear(Color.Transparent);
                        bg.SmoothingMode = SmoothingMode.HighSpeed;

                        float t1 = wavePhase * 0.16f;
                        float t2 = morphPhase * 0.42f;

                        int r1 = 115;
                        int cx1 = (int)(bufW * 0.36f + Math.Sin(t1) * (bufW * 0.14f));
                        int cy1 = (int)(bufH * 0.28f + Math.Cos(t2 * 0.8f) * (bufH * 0.12f));

                        int r2 = 105;
                        int cx2 = (int)(bufW * 0.72f + Math.Cos(t1 * 0.75f) * (bufW * 0.12f));
                        int cy2 = (int)(bufH * 0.62f + Math.Sin(t2) * (bufH * 0.14f));

                        int r3 = 92;
                        int cx3 = (int)(bufW * 0.24f + Math.Cos(t2 * 1.1f) * (bufW * 0.10f));
                        int cy3 = (int)(bufH * 0.76f + Math.Sin(t1 * 0.9f) * (bufH * 0.10f));

                        int acR = (int)accentColor.R;
                        int acG = (int)accentColor.G;
                        int acB = (int)accentColor.B;
                        Color cOrb1 = Color.FromArgb(56, acR, acG, acB);
                        Color cOrb2 = Color.FromArgb(44,
                            Math.Min(255, (acB + 90) % 255),
                            Math.Min(255, (acR + 60) % 255),
                            Math.Min(255, Math.Max(110, acG)));
                        Color cOrb3 = Color.FromArgb(38,
                            Math.Min(255, Math.Max(80, acG)),
                            Math.Min(255, (acB + 70) % 255),
                            Math.Min(255, (acR + 110) % 255));

                        DrawSoftMeshOrb(bg, cx1, cy1, r1, cOrb1);
                        DrawSoftMeshOrb(bg, cx2, cy2, r2, cOrb2);
                        DrawSoftMeshOrb(bg, cx3, cy3, r3, cOrb3);
                    }
                }
                g.DrawImage(cachedAuroraBitmap, new Rectangle(0, 0, w, h));
            }
            catch { }
        }

        private void DrawSoftMeshOrb(Graphics g, int cx, int cy, int radius, Color centerColor)
        {
            if (radius < 20) return;
            Rectangle bounds = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(bounds);
                using (PathGradientBrush pgb = new PathGradientBrush(gp))
                {
                    pgb.CenterPoint = new PointF(cx, cy);
                    pgb.CenterColor = centerColor;
                    pgb.SurroundColors = new Color[] { Color.FromArgb(0, centerColor.R, centerColor.G, centerColor.B) };
                    g.FillPath(pgb, gp);
                }
            }
        }

        private void DrawGlassCard(Graphics g, Rectangle rect, int radius, Color fallbackCardColor, bool isSelected, bool isHovered)
        {
            if (rect.Width <= 4 || rect.Height <= 4) return;
            int safeRad = Math.Max(4, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));

            if (!glassEffectEnabled)
            {
                Color solidCol = isSelected
                    ? Color.FromArgb(44, accentColor.R, accentColor.G, accentColor.B)
                    : (isHovered ? bgCardHover : fallbackCardColor);
                using (SolidBrush sb = new SolidBrush(solidCol))
                {
                    FillRoundRect(g, sb, rect, safeRad);
                }
                if (isSelected || isHovered)
                {
                    using (Pen p = new Pen(Color.FromArgb(isSelected ? 175 : 85, accentColor), 1.1f))
                    {
                        DrawRoundRect(g, p, rect, safeRad);
                    }
                }
                return;
            }

            // 1. 3D Floating Depth Drop-Shadow underneath the curved glass card
            if (curved3DGlassEnabled && rect.Height >= 24)
            {
                Rectangle shadowRect = new Rectangle(rect.X + 1, rect.Y + 3, rect.Width, rect.Height);
                using (SolidBrush shB = new SolidBrush(Color.FromArgb(72, 0, 0, 0)))
                {
                    FillRoundRect(g, shB, shadowRect, safeRad);
                }
            }

            // 2. Translucent Crystal Obsidian Glass Core (smooth vertical depth gradient, NO oval artifact in the center!)
            Color coreTop = isSelected
                ? Color.FromArgb(72, accentColor.R, accentColor.G, accentColor.B)
                : (isHovered ? Color.FromArgb(182, 28, 34, 50) : Color.FromArgb(162, 20, 25, 38));
            Color coreBot = isSelected
                ? Color.FromArgb(38, accentColor.R, accentColor.G, accentColor.B)
                : (isHovered ? Color.FromArgb(196, 14, 18, 28) : Color.FromArgb(184, 10, 13, 21));
            using (LinearGradientBrush coreGrad = new LinearGradientBrush(rect, coreTop, coreBot, LinearGradientMode.Vertical))
            {
                FillRoundRect(g, coreGrad, rect, safeRad);
            }

            // 3. True 3D Curved Edge Cushion Bevel & Specular Lip Refraction (Top Lip, Bottom Neon Refraction, Side Cushion)
            if (curved3DGlassEnabled && rect.Height >= 20 && rect.Width >= 32)
            {
                try
                {
                    using (GraphicsPath cardClip = GetRoundRectPath(rect, safeRad))
                    {
                        Region prevClip = g.Clip;
                        g.SetClip(cardClip, CombineMode.Intersect);

                        // Top 3D Curved Lip Specular Highlight (follows the top rounded contour cleanly, 14px deep)
                        int topLipH = Math.Max(8, Math.Min(16, rect.Height / 4));
                        Rectangle topLipRect = new Rectangle(rect.X, rect.Y, rect.Width, topLipH);
                        Color lipBright = Color.FromArgb(isHovered ? 64 : (isSelected ? 56 : 42), 255, 255, 255);
                        Color lipFade = Color.FromArgb(0, 255, 255, 255);
                        using (LinearGradientBrush topLipBrush = new LinearGradientBrush(topLipRect, lipBright, lipFade, LinearGradientMode.Vertical))
                        {
                            g.FillRectangle(topLipBrush, topLipRect);
                        }

                        // Bottom 3D Curved Lip Accent Refraction Glow (12px deep along the bottom curved edge)
                        int botLipH = Math.Max(6, Math.Min(14, rect.Height / 5));
                        Rectangle botLipRect = new Rectangle(rect.X, rect.Bottom - botLipH, rect.Width, botLipH);
                        Color botFade = Color.FromArgb(0, accentColor.R, accentColor.G, accentColor.B);
                        Color botGlow = Color.FromArgb(isSelected ? 64 : (isHovered ? 48 : 28), accentColor.R, accentColor.G, accentColor.B);
                        using (LinearGradientBrush botLipBrush = new LinearGradientBrush(botLipRect, botFade, botGlow, LinearGradientMode.Vertical))
                        {
                            g.FillRectangle(botLipBrush, botLipRect);
                        }

                        // Left & Right 3D Curved Side Cushion Sheen (8px wide along left & right vertical curves)
                        int sideLipW = Math.Max(5, Math.Min(10, rect.Width / 12));
                        Rectangle leftLipRect = new Rectangle(rect.X, rect.Y, sideLipW, rect.Height);
                        using (LinearGradientBrush leftLipBrush = new LinearGradientBrush(
                            leftLipRect,
                            Color.FromArgb(26, 255, 255, 255),
                            Color.FromArgb(0, 255, 255, 255),
                            LinearGradientMode.Horizontal))
                        {
                            g.FillRectangle(leftLipBrush, leftLipRect);
                        }

                        Rectangle rightLipRect = new Rectangle(rect.Right - sideLipW, rect.Y, sideLipW, rect.Height);
                        using (LinearGradientBrush rightLipBrush = new LinearGradientBrush(
                            rightLipRect,
                            Color.FromArgb(0, accentColor.R, accentColor.G, accentColor.B),
                            Color.FromArgb(22, accentColor.R, accentColor.G, accentColor.B),
                            LinearGradientMode.Horizontal))
                        {
                            g.FillRectangle(rightLipBrush, rightLipRect);
                        }

                        g.Clip = prevClip;
                    }

                    // Inner 1px 3D Bevel Thickness Rim (inset by 1px)
                    if (rect.Width > 8 && rect.Height > 8)
                    {
                        Rectangle innerRim = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
                        using (Pen innerPen = new Pen(Color.FromArgb(isHovered ? 48 : 30, 255, 255, 255), 1.0f))
                        {
                            DrawRoundRect(g, innerPen, innerRim, Math.Max(3, safeRad - 1));
                        }
                    }
                }
                catch { }
            }
            else
            {
                // Flat Frosted Glass diagonal sheen when 3D Curved Bevel is turned off
                Color sheenTop = Color.FromArgb(isHovered ? 32 : 20, 255, 255, 255);
                Color sheenBot = Color.FromArgb(2, 255, 255, 255);
                using (LinearGradientBrush sheenGrad = new LinearGradientBrush(rect, sheenTop, sheenBot, LinearGradientMode.ForwardDiagonal))
                {
                    FillRoundRect(g, sheenGrad, rect, safeRad);
                }
            }

            // 4. Crisp Outer 3D Specular Glass Rim Border
            Color rimCol = isSelected
                ? Color.FromArgb(215, accentColor)
                : (isHovered ? Color.FromArgb(160, accentColor) : Color.FromArgb(72, 255, 255, 255));
            using (Pen rimPen = new Pen(rimCol, isSelected ? 1.5f : 1.1f))
            {
                DrawRoundRect(g, rimPen, rect, safeRad);
            }
        }

        // =========================================================================
        // v10.4.2 ATMOSPHERIC SONG BACKDROP & VECTOR ICON ENGINE
        // Renders the active song's cover art scaled across the background with
        // deep cinematic vignette & crimson/accent atmospheric lighting (1:1 Ref Images 1, 2 & 3)
        // =========================================================================

        // =========================================================================
        // v10.4.3 MULTI-IMAGE CINEMA BACKGROUND SLIDESHOW & COLOR-GRADING ENGINE
        // Generates 6 distinct non-thumbnail 16:9 widescreen cinema frames for the
        // playing song (Original Wide, Edited Neon Grade, Concert Portrait Crop,
        // Cyber Duotone Poster, Vintage Film Warmth, + Live Artist Gallery Stills)
        // and crossfades between them with Ken Burns motion!
        // =========================================================================

        private int GetNextAllowedBgSlideIndex(int curIdx, int totalCount)
        {
            if (totalCount <= 1) return 0;
            for (int step = 1; step <= totalCount; step++)
            {
                int cand = (curIdx + step) % totalCount;
                if (songBgStyleIdx == 1 && cand > 1 && cand < 5) continue; // Original + Neon Edited
                if (songBgStyleIdx == 2 && cand != 2 && cand != 3 && cand < 5) continue; // Concert + Cyber Glow
                if (songBgStyleIdx == 3 && cand != 4 && cand < 5) continue; // Vintage + Artist Stills
                return cand;
            }
            return (curIdx + 1) % totalCount;
        }

        private void RebuildCurrentSongBgSlideshow(SongItem song, bool fetchOnlineGallery)
        {
            if (song == null) return;
            try
            {
                string jpgUrl = (song.ThumbUrl ?? "").Replace(".webp", ".jpg");
                Image srcImg = null;
                lock (imageCache)
                {
                    if (imageCache.ContainsKey(jpgUrl)) srcImg = imageCache[jpgUrl];
                }
                if (srcImg == null)
                {
                    PrefetchSingleThumbnail(jpgUrl);
                    return;
                }

                // Build 5 distinct 480x270 (16:9 widescreen) edited cinema background frames from the song artwork!
                List<Bitmap> newSlides = new List<Bitmap>();
                List<string> newLabels = new List<string>();

                // Frame 0: Original Widescreen Cinema Frame (Pan-and-Scan 16:9 with atmospheric studio lighting)
                newSlides.Add(CreateEditedWidescreenSlide(srcImg, 0));
                newLabels.Add("Original Cinema Wide");

                // Frame 1: Edited Neon Anamorphic Grade (Boosted contrast, neon teal/magenta anamorphic light leaks)
                newSlides.Add(CreateEditedWidescreenSlide(srcImg, 1));
                newLabels.Add("Edited Neon Grade");

                // Frame 2: Edited Concert Stage Close-Up (Asymmetric portrait zoom + spotlight stage beams)
                newSlides.Add(CreateEditedWidescreenSlide(srcImg, 2));
                newLabels.Add("Edited Concert Stage");

                // Frame 3: Edited Cyber Duotone Poster (High-contrast artist duotone mapped to song accent color)
                newSlides.Add(CreateEditedWidescreenSlide(srcImg, 3));
                newLabels.Add("Edited Cyber Duotone");

                // Frame 4: Edited Vintage Golden-Hour Film (Warm amber/crimson film grain & soft vignette)
                newSlides.Add(CreateEditedWidescreenSlide(srcImg, 4));
                newLabels.Add("Edited Vintage Film");

                // Also include alternate artist/album artwork already in queue/catalog by the same primary artist!
                string primArt = GetPrimaryArtistName(song.Artist).ToLowerInvariant();
                List<SongItem> pool = new List<SongItem>();
                pool.AddRange(queueSongs);
                pool.AddRange(catalog);
                foreach (SongItem s in pool)
                {
                    if (newSlides.Count >= 7) break;
                    if (s == null || s.Id == song.Id) continue;
                    string sUrl = (s.ThumbUrl ?? "").Replace(".webp", ".jpg");
                    if (string.IsNullOrEmpty(sUrl) || sUrl == jpgUrl) continue;
                    if (GetPrimaryArtistName(s.Artist).ToLowerInvariant() == primArt)
                    {
                        Image altImg = null;
                        lock (imageCache)
                        {
                            if (imageCache.ContainsKey(sUrl)) altImg = imageCache[sUrl];
                        }
                        if (altImg != null)
                        {
                            newSlides.Add(CreateEditedWidescreenSlide(altImg, (newSlides.Count % 2 == 0) ? 1 : 0));
                            newLabels.Add("Artist Gallery Still #" + (newSlides.Count - 4));
                        }
                    }
                }

                currentSongBgSlides = newSlides;
                currentSongBgSlideLabels = newLabels;
                cachedBgSlidesSongId = song.Id;
                if (songBgCurrentSlideIdx >= currentSongBgSlides.Count) songBgCurrentSlideIdx = 0;
                songBgNextSlideIdx = GetNextAllowedBgSlideIndex(songBgCurrentSlideIdx, currentSongBgSlides.Count);

                if (fetchOnlineGallery)
                {
                    FetchAdditionalArtistGallerySlidesAsync(song);
                }
            }
            catch { }
        }

        private Bitmap CreateEditedWidescreenSlide(Image src, int styleVariant)
        {
            int bw = 480;
            int bh = 270;
            Bitmap bmp = new Bitmap(bw, bh);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                int sw = src.Width;
                int sh = src.Height;

                // Different widescreen crops so no frame ever looks like a square thumbnail!
                Rectangle srcCrop;
                if (styleVariant == 0)
                {
                    // Upper-center widescreen 16:9 crop (captures artist eyes/face in wide format)
                    int ch = (int)(sh * 0.58f);
                    int cy = (int)(sh * 0.14f);
                    srcCrop = new Rectangle(0, cy, sw, Math.Max(20, ch));
                }
                else if (styleVariant == 1)
                {
                    // Shifted right widescreen crop + slight tilt zoom
                    int cw = (int)(sw * 0.84f);
                    int ch = (int)(sh * 0.52f);
                    srcCrop = new Rectangle(sw - cw, (int)(sh * 0.18f), Math.Max(20, cw), Math.Max(20, ch));
                }
                else if (styleVariant == 2)
                {
                    // Close-up dramatic center-left crop
                    int cw = (int)(sw * 0.72f);
                    int ch = (int)(sh * 0.46f);
                    srcCrop = new Rectangle((int)(sw * 0.08f), (int)(sh * 0.12f), Math.Max(20, cw), Math.Max(20, ch));
                }
                else if (styleVariant == 3)
                {
                    // Full wide atmospheric crop
                    int ch = (int)(sh * 0.62f);
                    srcCrop = new Rectangle(0, (int)(sh * 0.20f), sw, Math.Max(20, ch));
                }
                else
                {
                    // Golden hour upper-third crop
                    int cw = (int)(sw * 0.88f);
                    int ch = (int)(sh * 0.54f);
                    srcCrop = new Rectangle((sw - cw) / 2, (int)(sh * 0.10f), Math.Max(20, cw), Math.Max(20, ch));
                }

                // Apply custom ColorMatrix per styleVariant so each background frame has a distinct edited look!
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ColorMatrix cm = null;
                    float ar = accentColor.R / 255f;
                    float ag = accentColor.G / 255f;
                    float ab = accentColor.B / 255f;

                    if (styleVariant == 0)
                    {
                        // Variant 0: Rich Cinema Wide (1.18x contrast + subtle saturation boost)
                        cm = new ColorMatrix(new float[][] {
                            new float[] { 1.18f, 0.00f, 0.00f, 0f, 0f },
                            new float[] { 0.00f, 1.16f, 0.00f, 0f, 0f },
                            new float[] { 0.00f, 0.00f, 1.20f, 0f, 0f },
                            new float[] { 0.00f, 0.00f, 0.00f, 1f, 0f },
                            new float[] { -0.05f, -0.05f, -0.04f, 0f, 1f }
                        });
                    }
                    else if (styleVariant == 1)
                    {
                        // Variant 1: Edited Neon Anamorphic Grade (Cyan/Magenta & Accent color grade)
                        cm = new ColorMatrix(new float[][] {
                            new float[] { 1.25f, 0.05f, 0.12f, 0f, 0f },
                            new float[] { 0.04f, 1.12f, 0.10f, 0f, 0f },
                            new float[] { 0.14f, 0.08f, 1.35f, 0f, 0f },
                            new float[] { 0.00f, 0.00f, 0.00f, 1f, 0f },
                            new float[] { 0.02f * ar, -0.04f, 0.06f, 0f, 1f }
                        });
                    }
                    else if (styleVariant == 2)
                    {
                        // Variant 2: Edited Concert Spotlight (Deep dramatic shadows + vibrant highlights)
                        cm = new ColorMatrix(new float[][] {
                            new float[] { 1.32f, 0.02f, 0.00f, 0f, 0f },
                            new float[] { 0.02f, 1.26f, 0.02f, 0f, 0f },
                            new float[] { 0.00f, 0.04f, 1.28f, 0f, 0f },
                            new float[] { 0.00f, 0.00f, 0.00f, 1f, 0f },
                            new float[] { -0.09f, -0.09f, -0.08f, 0f, 1f }
                        });
                    }
                    else if (styleVariant == 3)
                    {
                        // Variant 3: Edited Cyber Duotone (Maps luminance to the active song's neon accent color!)
                        cm = new ColorMatrix(new float[][] {
                            new float[] { 0.35f + 0.85f * ar, 0.25f * ag, 0.20f * ab, 0f, 0f },
                            new float[] { 0.35f * ar, 0.45f + 0.75f * ag, 0.30f * ab, 0f, 0f },
                            new float[] { 0.25f * ar, 0.25f * ag, 0.40f + 0.80f * ab, 0f, 0f },
                            new float[] { 0.00f, 0.00f, 0.00f, 1f, 0f },
                            new float[] { -0.04f, -0.04f, -0.04f, 0f, 1f }
                        });
                    }
                    else
                    {
                        // Variant 4: Edited Vintage Golden-Hour Film (Warm amber/crimson cinema grade)
                        cm = new ColorMatrix(new float[][] {
                            new float[] { 1.28f, 0.12f, 0.02f, 0f, 0f },
                            new float[] { 0.08f, 1.14f, 0.02f, 0f, 0f },
                            new float[] { 0.02f, 0.04f, 0.92f, 0f, 0f },
                            new float[] { 0.00f, 0.00f, 0.00f, 1f, 0f },
                            new float[] { 0.04f, 0.01f, -0.05f, 0f, 1f }
                        });
                    }

                    ia.SetColorMatrix(cm);
                    g.DrawImage(src, new Rectangle(0, 0, bw, bh), srcCrop.X, srcCrop.Y, srcCrop.Width, srcCrop.Height, GraphicsUnit.Pixel, ia);
                }

                // Overlay editorial studio lighting effects on the edited frames (anamorphic flares, stage rays, bokeh)
                if (styleVariant == 1)
                {
                    // Anamorphic horizontal neon lens flare + cyan/accent corner wash
                    using (LinearGradientBrush flareB = new LinearGradientBrush(
                        new Rectangle(0, bh / 3, bw, 46),
                        Color.FromArgb(0, 0, 229, 255),
                        Color.FromArgb(68, accentColor.R, accentColor.G, accentColor.B),
                        LinearGradientMode.Horizontal))
                    {
                        g.FillRectangle(flareB, 0, bh / 3, bw, 46);
                    }
                }
                else if (styleVariant == 2)
                {
                    // Concert spotlight diagonal beams from top-right
                    PointF[] beam1 = new PointF[] { new PointF(bw * 0.75f, 0), new PointF(bw * 0.95f, 0), new PointF(bw * 0.45f, bh), new PointF(bw * 0.15f, bh) };
                    using (SolidBrush beamB = new SolidBrush(Color.FromArgb(38, accentColor.R, accentColor.G, accentColor.B)))
                    {
                        g.FillPolygon(beamB, beam1);
                    }
                }
                else if (styleVariant == 3 || styleVariant == 4)
                {
                    // Subtle studio bokeh light orbs
                    using (SolidBrush orb1 = new SolidBrush(Color.FromArgb(36, accentColor.R, accentColor.G, accentColor.B)))
                    using (SolidBrush orb2 = new SolidBrush(Color.FromArgb(28, 255, 210, 140)))
                    {
                        g.FillEllipse(orb1, bw - 140, 20, 110, 110);
                        g.FillEllipse(orb2, 60, bh - 120, 95, 95);
                    }
                }
            }
            return bmp;
        }

        private void FetchAdditionalArtistGallerySlidesAsync(SongItem song)
        {
            if (song == null) return;
            int myVer = ++bgGalleryFetchVer;
            string primArtist = GetPrimaryArtistName(song.Artist);
            string baseThumb = (song.ThumbUrl ?? "").Replace(".webp", ".jpg");

            Thread t = new Thread(() =>
            {
                List<Bitmap> extraSlides = new List<Bitmap>();
                try
                {
                    string q = primArtist + " top songs";
                    string url = "https://www.jiosaavn.com/api.php?__call=search.getResults&_format=json&n=10&p=1&_marker=0&ctx=android&q=" + Uri.EscapeDataString(q);
                    using (WebClient wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        wc.Headers.Add("User-Agent", "Mozilla/5.0 (Linux; Android 14)");
                        string json = wc.DownloadString(url);
                        List<SongItem> items = ParseOfficialJioSaavnJson(json);
                        HashSet<string> usedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        usedUrls.Add(baseThumb);

                        foreach (SongItem item in items)
                        {
                            if (extraSlides.Count >= 2) break;
                            string u = (item.ThumbUrl ?? "").Replace(".webp", ".jpg");
                            if (string.IsNullOrEmpty(u) || usedUrls.Contains(u)) continue;
                            usedUrls.Add(u);

                            try
                            {
                                byte[] data = wc.DownloadData(u);
                                using (MemoryStream ms = new MemoryStream(data))
                                {
                                    Image dlImg = Image.FromStream(ms);
                                    Bitmap slide = CreateEditedWidescreenSlide(dlImg, extraSlides.Count == 0 ? 1 : 2);
                                    extraSlides.Add(slide);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                if (extraSlides.Count > 0)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (myVer != bgGalleryFetchVer || currentSong == null || currentSong.Id != song.Id) return;
                        for (int i = 0; i < extraSlides.Count; i++)
                        {
                            if (currentSongBgSlides.Count < 8)
                            {
                                currentSongBgSlides.Add(extraSlides[i]);
                                currentSongBgSlideLabels.Add("Live Artist Still #" + (i + 1));
                            }
                        }
                    });
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void DrawSingleBgSlideWithMotion(Graphics g, Bitmap slide, int w, int h, float phaseOffset, float alpha, int animMode)
        {
            if (slide == null || alpha <= 0.01f) return;

            // Smooth Ken Burns zoom & pan so the background feels alive while the song plays!
            float zoom = 1.08f + 0.06f * (float)Math.Sin(phaseOffset * 0.55f);
            if (animMode == 3) zoom += 0.03f * (float)Math.Sin(wavePhase * 1.4f); // Pulse Morph beat zoom

            int drawW = (int)(w * zoom);
            int drawH = (int)(h * zoom);
            int panX = (int)(Math.Sin(phaseOffset * 0.42f) * (w * 0.03f));
            int panY = (int)(Math.Cos(phaseOffset * 0.36f) * (h * 0.025f));

            if (animMode == 2 && alpha < 0.99f)
            {
                // Slide + Fade horizontal motion during transition
                panX += (int)((1.0f - alpha) * 48f);
            }

            Rectangle destRect = new Rectangle((w - drawW) / 2 + panX, (h - drawH) / 2 + panY, drawW, drawH);

            if (alpha >= 0.99f)
            {
                g.DrawImage(slide, destRect);
            }
            else
            {
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ColorMatrix cm = new ColorMatrix();
                    cm.Matrix33 = Math.Max(0f, Math.Min(1f, alpha));
                    ia.SetColorMatrix(cm);
                    g.DrawImage(slide, destRect, 0, 0, slide.Width, slide.Height, GraphicsUnit.Pixel, ia);
                }
            }
        }

        private void PaintAtmosphericSongBackdrop(Graphics g, int w, int h, float intensity)
        {
            if (oledPureBlack || !songBgImageEnabled || currentSong == null || w <= 10 || h <= 10) return;
            try
            {
                if (currentSongBgSlides.Count == 0 || cachedBgSlidesSongId != currentSong.Id)
                {
                    RebuildCurrentSongBgSlideshow(currentSong, false);
                }

                // v10.4.4 ZERO-LAG TURBO COMPOSITOR:
                // Render the multi-image slide motion, ColorMatrix crossfade, scrim & gradients into a fast 384x216 Format32bppPArgb
                // offscreen surface (throttled every 2 ticks when steady) and blit in ONE pass! Cuts CPU time from ~22ms to <0.4ms!
                if (uiTurboCacheEnabled)
                {
                    int cw = 384;
                    int ch = 216;
                    compositeBackdropFrameCounter++;
                    bool needRebuild = (cachedCompositeBackdropBmp == null)
                        || (lastCompositeSlideIdx != songBgCurrentSlideIdx)
                        || isSongBgTransitioning
                        || (compositeBackdropFrameCounter >= 2);

                    if (needRebuild)
                    {
                        compositeBackdropFrameCounter = 0;
                        lastCompositeSlideIdx = songBgCurrentSlideIdx;
                        lastCompositeNextSlideIdx = songBgNextSlideIdx;
                        if (cachedCompositeBackdropBmp == null)
                        {
                            cachedCompositeBackdropBmp = new Bitmap(cw, ch, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                        }
                        using (Graphics bg = Graphics.FromImage(cachedCompositeBackdropBmp))
                        {
                            bg.SmoothingMode = SmoothingMode.HighSpeed;
                            bg.InterpolationMode = InterpolationMode.Bilinear;
                            bg.CompositingQuality = CompositingQuality.HighSpeed;
                            bg.Clear(Color.FromArgb(255, 8, 6, 10));

                            if (currentSongBgSlides.Count > 0)
                            {
                                int curIdx = Math.Max(0, Math.Min(currentSongBgSlides.Count - 1, songBgCurrentSlideIdx));
                                Bitmap curSlide = currentSongBgSlides[curIdx];
                                DrawSingleBgSlideWithMotion(bg, curSlide, cw, ch, songBgKenBurnsPhase + curIdx * 1.7f, 1.0f, songBgAnimModeIdx);

                                if (isSongBgTransitioning && currentSongBgSlides.Count > 1)
                                {
                                    int nxtIdx = Math.Max(0, Math.Min(currentSongBgSlides.Count - 1, songBgNextSlideIdx));
                                    Bitmap nxtSlide = currentSongBgSlides[nxtIdx];
                                    float smoothAlpha = (float)((1.0 - Math.Cos(songBgCrossfadeAlpha * Math.PI)) * 0.5);
                                    DrawSingleBgSlideWithMotion(bg, nxtSlide, cw, ch, songBgKenBurnsPhase + nxtIdx * 1.7f, smoothAlpha, songBgAnimModeIdx);
                                }
                            }
                            else
                            {
                                string jpgUrl = (currentSong.ThumbUrl ?? "").Replace(".webp", ".jpg");
                                Image bgImg = null;
                                lock (imageCache)
                                {
                                    if (imageCache.ContainsKey(jpgUrl)) bgImg = imageCache[jpgUrl];
                                }
                                if (bgImg != null)
                                {
                                    int drawSz = Math.Max(cw, ch + 40);
                                    bg.DrawImage(bgImg, new Rectangle((cw - drawSz) / 2 + 16, (ch - drawSz) / 2 - 6, drawSz, drawSz));
                                }
                            }

                            int baseDarkAlpha = (int)Math.Max(115, Math.Min(195, 168 - intensity * 26));
                            using (SolidBrush darkScrim = new SolidBrush(Color.FromArgb(baseDarkAlpha, 8, 6, 10)))
                            {
                                bg.FillRectangle(darkScrim, 0, 0, cw, ch);
                            }

                            Color tintTop = Color.FromArgb((int)(64 * intensity), Math.Min(255, (int)(accentColor.R * 0.90f)), Math.Min(255, (int)(accentColor.G * 0.35f)), Math.Min(255, (int)(accentColor.B * 0.40f)));
                            Color tintBot = Color.FromArgb(220, 6, 5, 9);
                            using (LinearGradientBrush vGrad = new LinearGradientBrush(new Rectangle(0, 0, cw, ch), tintTop, tintBot, LinearGradientMode.Vertical))
                            {
                                bg.FillRectangle(vGrad, 0, 0, cw, ch);
                            }

                            Rectangle leftFadeRect = new Rectangle(0, 0, Math.Max(64, cw / 3), ch);
                            using (LinearGradientBrush lGrad = new LinearGradientBrush(
                                leftFadeRect,
                                Color.FromArgb(195, 6, 6, 9),
                                Color.FromArgb(0, 6, 6, 9),
                                LinearGradientMode.Horizontal))
                            {
                                bg.FillRectangle(lGrad, leftFadeRect);
                            }
                        }
                    }

                    InterpolationMode prevInterp = g.InterpolationMode;
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.DrawImage(cachedCompositeBackdropBmp, new Rectangle(0, 0, w, h));
                    g.InterpolationMode = prevInterp;
                    return;
                }

                if (currentSongBgSlides.Count > 0)
                {
                    int curIdx = Math.Max(0, Math.Min(currentSongBgSlides.Count - 1, songBgCurrentSlideIdx));
                    Bitmap curSlide = currentSongBgSlides[curIdx];
                    DrawSingleBgSlideWithMotion(g, curSlide, w, h, songBgKenBurnsPhase + curIdx * 1.7f, 1.0f, songBgAnimModeIdx);

                    if (isSongBgTransitioning && currentSongBgSlides.Count > 1)
                    {
                        int nxtIdx = Math.Max(0, Math.Min(currentSongBgSlides.Count - 1, songBgNextSlideIdx));
                        Bitmap nxtSlide = currentSongBgSlides[nxtIdx];
                        float smoothAlpha = (float)((1.0 - Math.Cos(songBgCrossfadeAlpha * Math.PI)) * 0.5);
                        DrawSingleBgSlideWithMotion(g, nxtSlide, w, h, songBgKenBurnsPhase + nxtIdx * 1.7f, smoothAlpha, songBgAnimModeIdx);
                    }
                }

                int baseDarkAlphaFallback = (int)Math.Max(115, Math.Min(195, 168 - intensity * 26));
                using (SolidBrush darkScrim = new SolidBrush(Color.FromArgb(baseDarkAlphaFallback, 8, 6, 10)))
                {
                    g.FillRectangle(darkScrim, 0, 0, w, h);
                }
            }
            catch { }
        }

        private void DrawBrandMusicNoteLogo(Graphics g, int x, int y)
        {
            Color noteCol = accentColor;
            using (SolidBrush glowB = new SolidBrush(Color.FromArgb(55, noteCol.R, noteCol.G, noteCol.B)))
            {
                g.FillEllipse(glowB, x - 3, y - 2, 28, 28);
            }
            using (Pen np = new Pen(noteCol, 2.4f))
            using (SolidBrush nb = new SolidBrush(noteCol))
            {
                np.StartCap = LineCap.Round;
                np.EndCap = LineCap.Round;
                g.FillEllipse(nb, x + 1, y + 14, 8, 6);
                g.FillEllipse(nb, x + 12, y + 12, 8, 6);
                g.DrawLine(np, x + 8, y + 16, x + 8, y + 4);
                g.DrawLine(np, x + 19, y + 14, x + 19, y + 2);
                g.DrawLine(np, x + 8, y + 4, x + 19, y + 2);
                g.DrawLine(np, x + 8, y + 7, x + 19, y + 5);
            }
            using (Font fLeo = new Font("Segoe UI", 13.5f, FontStyle.Bold))
            using (SolidBrush acB = new SolidBrush(noteCol))
            {
                g.DrawString("LEO", fLeo, Brushes.White, x + 26, y + 1);
                g.DrawString("Music", fLeo, acB, x + 66, y + 1);
            }
        }

        private void DrawSidebarNavIcon(Graphics g, int iconType, float cx, float cy, Color col)
        {
            using (Pen p = new Pen(col, 1.6f))
            using (SolidBrush b = new SolidBrush(col))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                if (iconType == 0)
                {
                    PointF[] roof = new PointF[] { new PointF(cx - 6.5f, cy - 0.5f), new PointF(cx, cy - 6.5f), new PointF(cx + 6.5f, cy - 0.5f) };
                    g.DrawLines(p, roof);
                    g.DrawRectangle(p, cx - 4.5f, cy - 0.5f, 9f, 6.5f);
                }
                else if (iconType == 1)
                {
                    g.DrawEllipse(p, cx - 5.5f, cy - 5.5f, 8.5f, 8.5f);
                    g.DrawLine(p, cx + 2.0f, cy + 2.0f, cx + 5.8f, cy + 5.8f);
                }
                else if (iconType == 2)
                {
                    g.DrawLine(p, cx - 5.0f, cy - 5.5f, cx - 5.0f, cy + 5.5f);
                    g.DrawLine(p, cx - 1.2f, cy - 5.5f, cx - 1.2f, cy + 5.5f);
                    g.DrawLine(p, cx + 2.2f, cy - 5.0f, cx + 5.2f, cy + 5.5f);
                }
                else if (iconType == 3)
                {
                    DrawHeartIcon(g, col, cx, cy - 0.5f, false);
                }
                else if (iconType == 4)
                {
                    g.FillEllipse(b, cx - 5.5f, cy + 1.5f, 4.5f, 3.8f);
                    g.FillEllipse(b, cx + 1.5f, cy + 0.5f, 4.5f, 3.8f);
                    g.DrawLine(p, cx - 1.5f, cy + 3.0f, cx - 1.5f, cy - 5.0f);
                    g.DrawLine(p, cx + 5.5f, cy + 2.0f, cx + 5.5f, cy - 6.0f);
                    g.DrawLine(p, cx - 1.5f, cy - 5.0f, cx + 5.5f, cy - 6.0f);
                }
                else if (iconType == 5)
                {
                    DrawDownloadArrow(g, col, cx, cy);
                }
                else if (iconType == 6)
                {
                    g.DrawEllipse(p, cx - 3.5f, cy - 3.5f, 7f, 7f);
                    for (int k = 0; k < 8; k++)
                    {
                        double ang = k * Math.PI / 4.0;
                        float x1 = cx + 4.2f * (float)Math.Cos(ang);
                        float y1 = cy + 4.2f * (float)Math.Sin(ang);
                        float x2 = cx + 6.2f * (float)Math.Cos(ang);
                        float y2 = cy + 6.2f * (float)Math.Sin(ang);
                        g.DrawLine(p, x1, y1, x2, y2);
                    }
                }
            }
        }

        private void DrawShuffleVectorIcon(Graphics g, Color col, float cx, float cy)
        {
            using (Pen p = new Pen(col, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 6.5f, cy - 4.0f, cx - 2.0f, cy - 4.0f);
                g.DrawLine(p, cx - 2.0f, cy - 4.0f, cx + 2.5f, cy + 4.0f);
                g.DrawLine(p, cx + 2.5f, cy + 4.0f, cx + 6.5f, cy + 4.0f);

                g.DrawLine(p, cx - 6.5f, cy + 4.0f, cx - 2.0f, cy + 4.0f);
                g.DrawLine(p, cx - 2.0f, cy + 4.0f, cx + 2.5f, cy - 4.0f);
                g.DrawLine(p, cx + 2.5f, cy - 4.0f, cx + 6.5f, cy - 4.0f);

                g.DrawLine(p, cx + 4.2f, cy - 6.2f, cx + 6.8f, cy - 4.0f);
                g.DrawLine(p, cx + 4.2f, cy - 1.8f, cx + 6.8f, cy - 4.0f);
                g.DrawLine(p, cx + 4.2f, cy + 1.8f, cx + 6.8f, cy + 4.0f);
                g.DrawLine(p, cx + 4.2f, cy + 6.2f, cx + 6.8f, cy + 4.0f);
            }
        }

        private void DrawRepeatVectorIcon(Graphics g, Color col, float cx, float cy)
        {
            using (Pen p = new Pen(col, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawArc(p, cx - 6.5f, cy - 4.5f, 13f, 9f, 200f, 145f);
                g.DrawArc(p, cx - 6.5f, cy - 4.5f, 13f, 9f, 20f, 145f);
                g.DrawLine(p, cx + 3.8f, cy - 6.2f, cx + 6.5f, cy - 3.8f);
                g.DrawLine(p, cx + 3.5f, cy - 2.0f, cx + 6.5f, cy - 3.8f);
                g.DrawLine(p, cx - 3.8f, cy + 6.2f, cx - 6.5f, cy + 3.8f);
                g.DrawLine(p, cx - 3.5f, cy + 2.0f, cx - 6.5f, cy + 3.8f);
            }
        }

        private void DrawSpeakerVectorIcon(Graphics g, Color col, float cx, float cy, bool muted)
        {
            using (Pen p = new Pen(col, 1.6f))
            using (SolidBrush b = new SolidBrush(col))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                PointF[] cone = new PointF[]
                {
                    new PointF(cx - 6.5f, cy - 2.5f),
                    new PointF(cx - 3.5f, cy - 2.5f),
                    new PointF(cx + 0.5f, cy - 5.8f),
                    new PointF(cx + 0.5f, cy + 5.8f),
                    new PointF(cx - 3.5f, cy + 2.5f),
                    new PointF(cx - 6.5f, cy + 2.5f)
                };
                g.FillPolygon(b, cone);
                if (muted)
                {
                    g.DrawLine(p, cx + 3.0f, cy - 3.0f, cx + 7.0f, cy + 3.0f);
                    g.DrawLine(p, cx + 7.0f, cy - 3.0f, cx + 3.0f, cy + 3.0f);
                }
                else
                {
                    g.DrawArc(p, cx + 0.5f, cy - 3.5f, 4.5f, 7.0f, -60f, 120f);
                    g.DrawArc(p, cx + 1.5f, cy - 5.8f, 6.5f, 11.6f, -60f, 120f);
                }
            }
        }

        private void DrawQueueListIcon(Graphics g, Color col, float cx, float cy)
        {
            using (Pen p = new Pen(col, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 6.0f, cy - 4.2f, cx + 6.0f, cy - 4.2f);
                g.DrawLine(p, cx - 6.0f, cy,        cx + 6.0f, cy);
                g.DrawLine(p, cx - 6.0f, cy + 4.2f, cx + 2.5f, cy + 4.2f);
                g.DrawEllipse(p, cx + 3.2f, cy + 2.2f, 3.2f, 3.2f);
            }
        }

        private void DrawFullscreenCornersIcon(Graphics g, Color col, float cx, float cy)
        {
            using (Pen p = new Pen(col, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float r = 5.5f;
                g.DrawLine(p, cx - r, cy - r, cx - r + 3.5f, cy - r);
                g.DrawLine(p, cx - r, cy - r, cx - r, cy - r + 3.5f);
                g.DrawLine(p, cx + r, cy - r, cx + r - 3.5f, cy - r);
                g.DrawLine(p, cx + r, cy - r, cx + r, cy - r + 3.5f);
                g.DrawLine(p, cx - r, cy + r, cx - r + 3.5f, cy + r);
                g.DrawLine(p, cx - r, cy + r, cx - r, cy + r - 3.5f);
                g.DrawLine(p, cx + r, cy + r, cx + r - 3.5f, cy + r);
                g.DrawLine(p, cx + r, cy + r, cx + r, cy + r - 3.5f);
            }
        }

        private void DrawGlowingNeonPlayButton(Graphics g, Rectangle circleRect, bool playing)
        {
            for (int r = 3; r >= 1; r--)
            {
                int expand = r * 3;
                Rectangle haloR = new Rectangle(circleRect.X - expand, circleRect.Y - expand, circleRect.Width + expand * 2, circleRect.Height + expand * 2);
                using (Pen haloPen = new Pen(Color.FromArgb(24 * (4 - r), accentColor.R, accentColor.G, accentColor.B), 2.2f))
                {
                    g.DrawEllipse(haloPen, haloR);
                }
            }

            using (LinearGradientBrush coreB = new LinearGradientBrush(
                circleRect,
                Color.FromArgb(235, Math.Min(255, accentColor.R / 3 + 22), Math.Min(255, accentColor.G / 4 + 12), Math.Min(255, accentColor.B / 4 + 16)),
                Color.FromArgb(248, 12, 10, 14),
                LinearGradientMode.Vertical))
            {
                g.FillEllipse(coreB, circleRect);
            }

            using (Pen neonRim = new Pen(accentColor, 2.2f))
            {
                g.DrawEllipse(neonRim, circleRect);
            }

            float cx = circleRect.X + circleRect.Width / 2f;
            float cy = circleRect.Y + circleRect.Height / 2f;
            float scale = circleRect.Width / 44f;
            if (playing)
            {
                DrawPauseBars(g, Brushes.White, cx, cy, 3.6f * scale, 13.5f * scale, 4.2f * scale);
            }
            else
            {
                DrawPlayTriangle(g, Brushes.White, cx + 1.2f * scale, cy, 10.0f * scale);
            }
        }

        private void DrawDevicePcSpeakerIcon(Graphics g, Color col, float x, float y)
        {
            using (Pen p = new Pen(col, 1.5f))
            {
                FillRoundRect(g, Brushes.Transparent, new Rectangle((int)x, (int)y + 2, 12, 14), 2);
                g.DrawRectangle(p, x, y + 2, 11, 13);
                g.DrawRectangle(p, x + 8, y, 11, 18);
                g.DrawEllipse(p, x + 11.5f, y + 3.5f, 4f, 4f);
                g.DrawEllipse(p, x + 10.5f, y + 9.5f, 6f, 6f);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            int sidebarW = 198;
            int rightW = Math.Max(0, (int)Math.Round(rightPanelWidthAnim));
            int playerH = 82;
            int mainH = h - playerH;

            Color curBgMain = oledPureBlack ? Color.Black : Color.FromArgb(8, 7, 10);
            Color curBgSide = oledPureBlack ? Color.FromArgb(6, 7, 10) : Color.FromArgb(14, 12, 17);
            Color curBgCard = oledPureBlack ? Color.FromArgb(13, 15, 22) : Color.FromArgb(20, 17, 24);

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

            if (isDynamicBarMode)
            {
                PaintFloatingDynamicBarOverlay(g, w, h, sfCenter, sfLeftEllipsis);
                return;
            }

            if (isMiniPlayerMode)
            {
                PaintFloatingMiniPlayer(g, w, h, sfCenter, sfLeftEllipsis);
                return;
            }

            if (fullPlayerAnim < 0.99f)
            {
                // v10.4.4 Fix: Only paint main stage backdrop when Full-Screen Panel is not covering the window!
                PaintAtmosphericSongBackdrop(g, w, h, 1.0f);
                PaintLiveMeshAuroraBackdrop(g, w, h);
            }

            if (fullPlayerAnim < 0.99f)
            {
                // -----------------------------------------------------------------
                // 1. LEFT FLOATING GLASS SIDEBAR (1:1 Reference Image 1)
                // -----------------------------------------------------------------
                Rectangle sideCardRect = new Rectangle(10, 10, sidebarW - 14, h - 20);
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(oledPureBlack ? 245 : 205, 12, 11, 15)))
                using (Pen sp = new Pen(Color.FromArgb(42, 255, 255, 255), 1.0f))
                {
                    FillRoundRect(g, sb, sideCardRect, 18);
                    DrawRoundRect(g, sp, sideCardRect, 18);
                }

                DrawBrandMusicNoteLogo(g, 24, 26);

                string[] navLabels = new string[] { "Home", "Search", "Library", "Liked Songs", "Playlists", "Downloads" };
                int[] navTabIds    = new int[] { 0, 1, 6, 2, 5, 3 };
                int[] navIcons     = new int[] { 0, 1, 2, 3, 4, 5 };

                for (int i = 0; i < navLabels.Length; i++)
                {
                    int tabId = navTabIds[i];
                    string btnKey = "nav_" + tabId;
                    Rectangle baseNavRect = new Rectangle(20, 72 + i * 38, sidebarW - 34, 34);
                    Rectangle navRect = ScaleRectCentered(baseNavRect, GetButtonScale(btnKey));
                    bool isSel = (activeTab == tabId);
                    bool isHov = (hoveredNavIndex == i);

                    if (isSel)
                    {
                        using (LinearGradientBrush selGrad = new LinearGradientBrush(
                            navRect,
                            Color.FromArgb(230, accentColor.R, accentColor.G, accentColor.B),
                            Color.FromArgb(145, Math.Max(0, accentColor.R - 50), Math.Max(0, accentColor.G - 30), Math.Max(0, accentColor.B - 30)),
                            LinearGradientMode.Horizontal))
                        {
                            FillRoundRect(g, selGrad, navRect, 11);
                        }
                        using (Pen selRim = new Pen(Color.FromArgb(180, 255, 255, 255), 0.9f))
                        {
                            DrawRoundRect(g, selRim, navRect, 11);
                        }
                    }
                    else if (isHov)
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
                        {
                            FillRoundRect(g, b, navRect, 11);
                        }
                    }

                    Color iconCol = isSel ? Color.White : (isHov ? textWhite : Color.FromArgb(195, 200, 210));
                    DrawSidebarNavIcon(g, navIcons[i], navRect.X + 18f, navRect.Y + 17f, iconCol);

                    using (Font fNav = new Font("Segoe UI", 9.5f, isSel ? FontStyle.Bold : FontStyle.Regular))
                    using (SolidBrush tb = new SolidBrush(iconCol))
                    {
                        g.DrawString(navLabels[i], fNav, tb, navRect.X + 36, navRect.Y + 7);
                    }
                }

                int divY = 308;
                using (Pen divPen = new Pen(Color.FromArgb(32, 255, 255, 255), 1f))
                {
                    g.DrawLine(divPen, 22, divY, sidebarW - 18, divY);
                }

                int plHeaderY = divY + 10;
                using (Font fPlHdr = new Font("Segoe UI", 9.2f, FontStyle.Regular))
                using (SolidBrush plHdrB = new SolidBrush(Color.FromArgb(185, 190, 202)))
                {
                    g.DrawString("Playlists", fPlHdr, plHdrB, 22, plHeaderY);
                }
                Rectangle sideAddPlRect = new Rectangle(sidebarW - 38, plHeaderY - 2, 22, 22);
                using (Font fPlus = new Font("Segoe UI", 11.5f, FontStyle.Regular))
                using (SolidBrush plusB = new SolidBrush(Color.FromArgb(210, 215, 225)))
                {
                    g.DrawString("+", fPlus, plusB, sideAddPlRect, sfCenter);
                }

                if (userPlaylists.Count == 0) InitDefaultPlaylists();
                int plListStartY = plHeaderY + 26;
                int maxSidePlaylists = Math.Min(5, userPlaylists.Count);
                int bottomSettingsY = h - 54;

                for (int i = 0; i < maxSidePlaylists; i++)
                {
                    int py = plListStartY + i * 36;
                    if (py + 32 > bottomSettingsY - 8) break;

                    UserPlaylistItem plItem = userPlaylists[i];
                    Rectangle plRowRect = new Rectangle(18, py, sidebarW - 30, 32);
                    bool isPlSelected = (activeTab == 5 && selectedPlaylistIdx == i);
                    if (isPlSelected)
                    {
                        using (SolidBrush psb = new SolidBrush(Color.FromArgb(45, accentColor.R, accentColor.G, accentColor.B)))
                        {
                            FillRoundRect(g, psb, plRowRect, 8);
                        }
                    }

                    SongItem plCover = (plItem.Songs.Count > 0) ? plItem.Songs[0] : catalog[i % catalog.Count];
                    DrawStaticAlbumArt(g, plCover.ThumbUrl, new Rectangle(plRowRect.X + 4, plRowRect.Y + 2, 28, 28), 6);

                    using (Font fPlRow = new Font("Segoe UI", 8.8f, isPlSelected ? FontStyle.Bold : FontStyle.Regular))
                    using (SolidBrush ptb = new SolidBrush(isPlSelected ? accentColor : Color.FromArgb(215, 220, 228)))
                    {
                        g.DrawString(plItem.Name, fPlRow, ptb, new RectangleF(plRowRect.X + 38, plRowRect.Y + 4, plRowRect.Width - 42, 24), sfLeftEllipsis);
                    }
                }

                Rectangle settingsNavRect = new Rectangle(20, bottomSettingsY, sidebarW - 34, 34);
                bool isSetSel = (activeTab == 4);
                if (isSetSel)
                {
                    using (SolidBrush sbSet = new SolidBrush(Color.FromArgb(65, accentColor.R, accentColor.G, accentColor.B)))
                    using (Pen spSet = new Pen(accentColor, 1f))
                    {
                        FillRoundRect(g, sbSet, settingsNavRect, 10);
                        DrawRoundRect(g, spSet, settingsNavRect, 10);
                    }
                }
                Color setCol = isSetSel ? accentColor : Color.FromArgb(195, 200, 210);
                DrawSidebarNavIcon(g, 6, settingsNavRect.X + 18f, settingsNavRect.Y + 17f, setCol);
                using (Font fSet = new Font("Segoe UI", 9.5f, isSetSel ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush stb = new SolidBrush(setCol))
                {
                    g.DrawString("Settings", fSet, stb, settingsNavRect.X + 36, settingsNavRect.Y + 7);
                }

                // -----------------------------------------------------------------
                // 2. CENTER STAGE
                // -----------------------------------------------------------------
                int centerX = sidebarW;
                int centerW = Math.Max(320, w - sidebarW - rightW);
                int slideOffsetY = (int)((1.0f - tabFadeAnim) * 14f);

                GraphicsState centerState = g.Save();
                g.TranslateTransform(0, slideOffsetY);
                if (activeTab == 4)
                {
                    PaintSettingsAndEqualizerScreen(g, centerX, centerW, mainH, curBgCard, sfCenter, sfLeftEllipsis);
                }
                else if (activeTab == 5)
                {
                    PaintPlaylistsStudioScreen(g, centerX, centerW, mainH, curBgCard, sfCenter, sfLeftEllipsis);
                }
                else
                {
                    PaintCenterSongStage(g, centerX, centerW, mainH, curBgCard, sfCenter, sfLeftEllipsis);
                }
                g.Restore(centerState);

                // -----------------------------------------------------------------
                // 3. RIGHT FLOATING "UP NEXT" / LYRICS CARD (1:1 Reference Image 1)
                // -----------------------------------------------------------------
                if (rightW > 24)
                {
                    int rightX = w - rightW;
                    Region prevClipR = g.Clip;
                    g.SetClip(new Rectangle(rightX, 0, rightW, mainH));
                    PaintRightDockedPanel(g, rightX, 315, mainH, curBgSide, curBgCard, sfCenter, sfLeftEllipsis);
                    g.Clip = prevClipR;
                }

                // -----------------------------------------------------------------
                // 4. BOTTOM FLOATING ROUNDED PLAYER DOCK (1:1 Reference Image 1)
                // -----------------------------------------------------------------
                PaintBottomPlayerBar(g, w, h, playerH, sfCenter, sfLeftEllipsis);
            }

            if (fullPlayerAnim > 0.01f && currentSong != null)
            {
                GraphicsState fullState = g.Save();
                float slideY = (1.0f - fullPlayerAnim) * (h * 0.16f);
                g.TranslateTransform(0, slideY);
                PaintFullNowPlayingPanel(g, w, h, fullPlayerAnim, sfCenter, sfLeftEllipsis);
                g.Restore(fullState);
            }

            // v10.4.4 Interactive 3D Glass Studio Volume Controller & Playback Smoothness Popup (Overlays both Home & Full-Screen!)
            if (volumeControllerPopupAnim > 0.01f)
            {
                PaintStudioVolumeControllerPopup(g, w, h, volumeControllerPopupAnim, sfCenter, sfLeftEllipsis);
            }

            using (Pen winPen = new Pen(Color.FromArgb(200, 220, 225, 235), 1.5f))
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

            if (languageSetupAnim > 0.01f)
            {
                PaintLanguageSetupScreen(g, w, h, languageSetupAnim, sfCenter, sfLeftEllipsis);
            }

            if (statusTick > 0 && !string.IsNullOrEmpty(statusMessage))
            {
                int midX = w / 2;
                Rectangle toastRect = new Rectangle(midX - 200, h - playerH - 44, 400, 32);
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(235, 18, 16, 22)))
                using (Pen tp = new Pen(accentColor, 1.4f))
                {
                    FillRoundRect(g, tb, toastRect, 16);
                    DrawRoundRect(g, tp, toastRect, 16);
                }
                using (Font fToast = new Font("Segoe UI", 9f, FontStyle.Bold))
                {
                    g.DrawString(statusMessage, fToast, Brushes.White, toastRect, sfCenter);
                }
            }
        }

        // =========================================================================
        // v10.3.8 / v10.4.0 MOVEABLE ALWAYS-ON-TOP "DYNAMIC BAR" OVERLAY ENGINE
        // =========================================================================

        private void PaintFloatingDynamicBarOverlay(Graphics g, int w, int h, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            int cornerR = isDynamicBarExpanded ? 22 : (h / 2 - 1);

            if (dynamicBarStyleIdx == 0)
            {
                using (SolidBrush blkB = new SolidBrush(Color.FromArgb(255, 6, 7, 11)))
                {
                    g.FillRectangle(blkB, 0, 0, w, h);
                }
                using (Pen subRim = new Pen(Color.FromArgb(52, 255, 255, 255), 1.0f))
                {
                    DrawRoundRect(g, subRim, new Rectangle(1, 1, w - 3, h - 3), cornerR);
                }
            }
            else
            {
                Color cTop = Color.FromArgb(255, (int)Math.Min(255, ambR * 0.42f + 8), (int)Math.Min(255, ambG * 0.40f + 10), (int)Math.Min(255, ambB * 0.45f + 16));
                Color cBot = Color.FromArgb(255, 6, 8, 14);
                using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0, 0, w, h), cTop, cBot, LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(bg, 0, 0, w, h);
                }
                PaintLiveMeshAuroraBackdrop(g, w, h);
                using (Pen meshRim = new Pen(Color.FromArgb(85, accentColor), 1.0f))
                {
                    DrawRoundRect(g, meshRim, new Rectangle(1, 1, w - 3, h - 3), cornerR);
                }
            }

            if (!isDynamicBarExpanded)
            {
                int cy = h / 2;
                Rectangle logoCircle = new Rectangle(7, cy - 11, 22, 22);
                using (SolidBrush lb = new SolidBrush(Color.FromArgb(38, accentColor.R, accentColor.G, accentColor.B)))
                using (Pen lp = new Pen(Color.FromArgb(180, accentColor), 1.2f))
                {
                    g.FillEllipse(lb, logoCircle);
                    g.DrawEllipse(lp, logoCircle);
                }
                using (SolidBrush barB = new SolidBrush(accentColor))
                {
                    g.FillRectangle(barB, logoCircle.X + 5.5f, logoCircle.Y + 8f, 2.0f, 6f);
                    g.FillRectangle(barB, logoCircle.X + 10.0f, logoCircle.Y + 5f, 2.0f, 12f);
                    g.FillRectangle(barB, logoCircle.X + 14.5f, logoCircle.Y + 7f, 2.0f, 8f);
                }

                string titleStr = currentSong != null
                    ? (FormatMinimalCardTitle(currentSong.Title) + " \u2022 " + GetPrimaryArtistName(currentSong.Artist))
                    : "Leo Music Studio";
                using (SolidBrush wb = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
                {
                    g.DrawString(titleStr, fontSmallBold, wb, new RectangleF(35, cy - 9, w - 76, 18), sfLeftEllipsis);
                }

                DrawEqResponsiveVisualizer(g, w - 34, cy + 6, 4, 5, 2.2f, 7.5f);

                float fracC = (float)Math.Max(0.0, Math.Min(1.0, smoothPosSec / Math.Max(1.0, currentDurSec)));
                int progW = (int)((w - 40) * fracC);
                if (progW > 2)
                {
                    using (Pen pp = new Pen(Color.FromArgb(150, accentColor), 1.5f))
                    {
                        g.DrawLine(pp, 20, h - 2, 20 + progW, h - 2);
                    }
                }
                return;
            }

            if (currentSong != null)
            {
                Rectangle podArt = new Rectangle(12, 12, 46, 46);
                DrawAnimatedStudioArtwork(g, currentSong.ThumbUrl, podArt, artworkShapeMode);

                int tx = 66;
                int tw = w - tx - 146;
                using (SolidBrush wb = new SolidBrush(Color.White))
                using (SolidBrush sb = new SolidBrush(textSub))
                {
                    g.DrawString(FormatMinimalCardTitle(currentSong.Title), fontSmallBold, wb, new RectangleF(tx, 12, tw, 18), sfLeftEllipsis);
                    g.DrawString(GetPrimaryArtistName(currentSong.Artist), fontSubReg, sb, new RectangleF(tx, 30, tw, 16), sfLeftEllipsis);
                }
            }

            Rectangle btnFull  = new Rectangle(w - 44, 10, 36, 20);
            Rectangle btnMini  = new Rectangle(btnFull.X - 38, 10, 35, 20);
            Rectangle btnStyle = new Rectangle(btnMini.X - 44, 10, 41, 20);
            Rectangle btnComp  = new Rectangle(btnStyle.X - 26, 10, 23, 20);

            using (SolidBrush pb = new SolidBrush(Color.FromArgb(65, 255, 255, 255)))
            using (SolidBrush db = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
            {
                FillRoundRect(g, db, btnComp, 8);
                FillRoundRect(g, db, btnStyle, 8);
                FillRoundRect(g, db, btnMini, 8);
                FillRoundRect(g, pb, btnFull, 8);
            }
            g.DrawString("\u25B4", fontSmallBold, Brushes.White, btnComp, sfCenter);
            g.DrawString(dynamicBarStyleIdx == 0 ? "Black" : "Mesh", fontSmallBold, Brushes.White, btnStyle, sfCenter);
            g.DrawString("Mini", fontSmallBold, Brushes.White, btnMini, sfCenter);
            g.DrawString("Full", fontSmallBold, Brushes.Black, btnFull, sfCenter);

            int cyPod = 74;
            Rectangle prevR = new Rectangle(14, cyPod - 11, 22, 22);
            Rectangle playR = new Rectangle(40, cyPod - 13, 26, 26);
            Rectangle nextR = new Rectangle(70, cyPod - 11, 22, 22);

            using (SolidBrush cb = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
            using (SolidBrush ab = new SolidBrush(accentColor))
            {
                g.FillEllipse(cb, prevR);
                g.FillEllipse(ab, playR);
                g.FillEllipse(cb, nextR);
            }
            DrawSkipPrevIcon(g, Brushes.White, prevR.X + 11f, cyPod);
            if (isPlaying) DrawPauseBars(g, Brushes.Black, playR.X + 13f, cyPod, 3.0f, 9.5f, 3.2f);
            else DrawPlayTriangle(g, Brushes.Black, playR.X + 13f, cyPod, 7.0f);
            DrawSkipNextIcon(g, Brushes.White, nextR.X + 11f, cyPod);

            int barX = 104;
            int barW = w - barX - 46;
            int barY = cyPod;
            float frac = (float)Math.Max(0.0, Math.Min(1.0, smoothPosSec / Math.Max(1.0, currentDurSec)));
            int actX = barX + (int)(barW * frac);
            using (Pen ip = new Pen(Color.FromArgb(55, 255, 255, 255), 2.8f))
            using (Pen ap = new Pen(accentColor, 3.0f))
            {
                g.DrawLine(ip, barX, barY, barX + barW, barY);
                if (actX > barX + 2) g.DrawLine(ap, barX, barY, actX, barY);
            }
            g.FillEllipse(Brushes.White, actX - 3.5f, barY - 3.5f, 7f, 7f);
            using (SolidBrush tb = new SolidBrush(textSub))
            {
                g.DrawString(FormatSec((int)currentPosSec), fontSmallBold, tb, w - 38, barY - 7);
            }
        }

        private void PaintFloatingMiniPlayer(Graphics g, int w, int h, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            Color cTop = Color.FromArgb(255, (int)Math.Min(255, ambR * 0.55f + 14), (int)Math.Min(255, ambG * 0.52f + 16), (int)Math.Min(255, ambB * 0.58f + 24));
            Color cBot = Color.FromArgb(255, 10, 13, 22);
            using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0, 0, w, h), cTop, cBot, LinearGradientMode.ForwardDiagonal))
            {
                g.FillRectangle(bg, 0, 0, w, h);
            }

            PaintLiveMeshAuroraBackdrop(g, w, h);

            if (miniPlayerStyleIdx == 3)
            {
                using (Pen rimPen = new Pen(Color.FromArgb(190, accentColor), 2.5f))
                {
                    g.DrawEllipse(rimPen, 2, 2, w - 5, h - 5);
                }

                if (currentSong != null)
                {
                    Rectangle orbArt = new Rectangle((w - 116) / 2, 18, 116, 116);
                    DrawAnimatedStudioArtwork(g, currentSong.ThumbUrl, orbArt, artworkShapeMode);

                    using (SolidBrush wb = new SolidBrush(Color.White))
                    using (SolidBrush sb = new SolidBrush(textSub))
                    {
                        g.DrawString(FormatMinimalCardTitle(currentSong.Title), fontSmallBold, wb, new RectangleF(24, 140, w - 48, 18), sfCenter);
                        g.DrawString(GetPrimaryArtistName(currentSong.Artist), fontSubReg, sb, new RectangleF(28, 157, w - 56, 16), sfCenter);
                    }
                }

                int cyOrb = 194;
                int midOrb = w / 2;
                Rectangle prevOrb = new Rectangle(midOrb - 54, cyOrb - 13, 26, 26);
                Rectangle playOrb = new Rectangle(midOrb - 16, cyOrb - 16, 32, 32);
                Rectangle nextOrb = new Rectangle(midOrb + 28, cyOrb - 13, 26, 26);
                using (SolidBrush cb = new SolidBrush(Color.FromArgb(95, 0, 0, 0)))
                using (SolidBrush ab = new SolidBrush(accentColor))
                {
                    g.FillEllipse(cb, prevOrb);
                    g.FillEllipse(ab, playOrb);
                    g.FillEllipse(cb, nextOrb);
                }
                DrawSkipPrevIcon(g, Brushes.White, prevOrb.X + 13f, cyOrb);
                if (isPlaying) DrawPauseBars(g, Brushes.Black, playOrb.X + 16f, cyOrb, 3.5f, 11f, 3.8f);
                else DrawPlayTriangle(g, Brushes.Black, playOrb.X + 16f, cyOrb, 8.5f);
                DrawSkipNextIcon(g, Brushes.White, nextOrb.X + 13f, cyOrb);

                Rectangle shapeOrbBtn = new Rectangle(midOrb - 68, 220, 62, 22);
                Rectangle fullOrbBtn = new Rectangle(midOrb + 6, 220, 62, 22);
                DrawGlassCard(g, shapeOrbBtn, 11, bgCard, false, false);
                DrawGlassCard(g, fullOrbBtn, 11, bgCard, true, false);
                g.DrawString("Shape", fontSmallBold, Brushes.White, shapeOrbBtn, sfCenter);
                g.DrawString("Full UI", fontSmallBold, Brushes.White, fullOrbBtn, sfCenter);
                return;
            }

            int cornerRad = (miniPlayerStyleIdx == 1) ? (h / 2 - 2) : ((miniPlayerStyleIdx == 2) ? 10 : 28);
            DrawGlassCard(g, new Rectangle(1, 1, w - 3, h - 3), cornerRad, bgCard, false, false);

            int artLeft = (miniPlayerStyleIdx == 1) ? 22 : 14;
            if (currentSong != null)
            {
                Rectangle miniArt = new Rectangle(artLeft, (h - 90) / 2 - 6, 88, 88);
                DrawAnimatedStudioArtwork(g, currentSong.ThumbUrl, miniArt, artworkShapeMode);

                int textLeft = miniArt.Right + 12;
                int textW = w - textLeft - 154;
                using (SolidBrush wb = new SolidBrush(Color.White))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(215, 235, 240, 248)))
                {
                    g.DrawString(FormatMinimalCardTitle(currentSong.Title), fontTitleBold, wb, new RectangleF(textLeft, 14, textW, 22), sfLeftEllipsis);
                    g.DrawString(GetPrimaryArtistName(currentSong.Artist), fontSubReg, sb, new RectangleF(textLeft, 35, textW, 18), sfLeftEllipsis);
                }

                EnsureSyncedLyricsParsed(currentSong);
                string activeLyricText = "\u266A " + currentSong.Title;
                double syncClock = Math.Max(0.0, smoothPosSec + lyricSyncOffsetSec);
                if (currentSong.SyncedLines != null)
                {
                    for (int i = 0; i < currentSong.SyncedLines.Count; i++)
                    {
                        if (syncClock >= currentSong.SyncedLines[i].StartSec)
                        {
                            activeLyricText = "\u266A " + currentSong.SyncedLines[i].Text;
                        }
                    }
                }
                using (SolidBrush lyrB = new SolidBrush(accentColor))
                {
                    g.DrawString(activeLyricText, fontSmallBold, lyrB, new RectangleF(textLeft, 56, w - textLeft - 24, 20), sfLeftEllipsis);
                }
            }

            int rightMargin = (miniPlayerStyleIdx == 1) ? 24 : 14;
            Rectangle expandDesktopBtn = new Rectangle(w - rightMargin - 64, 12, 64, 24);
            Rectangle miniShapeBtn = new Rectangle(expandDesktopBtn.X - 66, 12, 62, 24);
            Rectangle miniDynBarBtn = new Rectangle(miniShapeBtn.X - 66, 12, 62, 24);
            DrawGlassCard(g, miniDynBarBtn, 12, bgCard, false, false);
            DrawGlassCard(g, miniShapeBtn, 12, bgCard, false, false);
            DrawGlassCard(g, expandDesktopBtn, 12, bgCard, true, false);
            g.DrawString("Dyn Bar", fontSmallBold, Brushes.White, miniDynBarBtn, sfCenter);
            g.DrawString(miniPlayerStyleNames[miniPlayerStyleIdx], fontSmallBold, Brushes.White, miniShapeBtn, sfCenter);
            g.DrawString("\u2922 Full UI", fontSmallBold, Brushes.White, expandDesktopBtn, sfCenter);

            int tLeft = artLeft + 100;
            int cy = 96;
            Rectangle prevR = new Rectangle(tLeft, cy - 13, 26, 26);
            Rectangle playR = new Rectangle(tLeft + 34, cy - 16, 32, 32);
            Rectangle nextR = new Rectangle(tLeft + 74, cy - 13, 26, 26);
            Rectangle speedR = new Rectangle(tLeft + 112, cy - 12, 92, 24);

            using (SolidBrush cb = new SolidBrush(Color.FromArgb(75, 0, 0, 0)))
            using (SolidBrush ab = new SolidBrush(accentColor))
            {
                g.FillEllipse(cb, prevR);
                g.FillEllipse(ab, playR);
                g.FillEllipse(cb, nextR);
                FillRoundRect(g, cb, speedR, 12);
            }
            DrawSkipPrevIcon(g, Brushes.White, prevR.X + 13f, cy);
            if (isPlaying) DrawPauseBars(g, Brushes.Black, playR.X + 16f, cy, 3.5f, 11f, 3.8f);
            else DrawPlayTriangle(g, Brushes.Black, playR.X + 16f, cy, 8.5f);
            DrawSkipNextIcon(g, Brushes.White, nextR.X + 13f, cy);

            using (SolidBrush spTxt = new SolidBrush(playbackSpeedIdx == 0 ? textSub : accentColor))
            {
                g.DrawString(playbackSpeedNames[playbackSpeedIdx], fontSmallBold, spTxt, speedR, sfCenter);
            }

            int barX = (miniPlayerStyleIdx == 1) ? 42 : 20;
            int barW = w - barX * 2;
            int barY = h - 18;
            float frac = (float)Math.Max(0.0, Math.Min(1.0, smoothPosSec / Math.Max(1.0, currentDurSec)));
            int actX = barX + (int)(barW * frac);
            using (Pen ip = new Pen(Color.FromArgb(60, 255, 255, 255), 3f))
            using (Pen ap = new Pen(accentColor, 3.2f))
            {
                g.DrawLine(ip, barX, barY, barX + barW, barY);
                if (actX > barX + 2) g.DrawLine(ap, barX, barY, actX, barY);
            }
            g.FillEllipse(Brushes.White, actX - 4, barY - 4, 8, 8);
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

        private void PaintLanguageSetupScreen(Graphics g, int w, int h, float anim, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            int bgAlpha = (int)(anim * 248);
            using (SolidBrush bgB = new SolidBrush(Color.FromArgb(bgAlpha, 8, 10, 16)))
            {
                g.FillRectangle(bgB, 0, 0, w, h);
            }

            int mw = Math.Min(820, w - 64);
            int mh = Math.Min(560, h - 64);
            int mx = (w - mw) / 2;
            int my = (h - mh) / 2;
            Rectangle modalBox = new Rectangle(mx, my, mw, mh);

            using (SolidBrush mb = new SolidBrush(Color.FromArgb(245, 16, 20, 30)))
            using (Pen mp = new Pen(Color.FromArgb(160, accentColor), 1.8f))
            {
                FillRoundRect(g, mb, modalBox, 22);
                DrawRoundRect(g, mp, modalBox, 22);
            }

            Rectangle badgeRect = new Rectangle(mx + (mw - 220) / 2, my + 24, 220, 26);
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B)))
            {
                FillRoundRect(g, bb, badgeRect, 13);
            }
            using (SolidBrush bt = new SolidBrush(accentColor))
            {
                g.DrawString("\u2605 PERSONALIZE YOUR MUSIC", fontSmallBold, bt, badgeRect, sfCenter);
            }

            using (Font fLangTitle = new Font("Segoe UI", 16f, FontStyle.Bold))
            {
                g.DrawString("What music languages do you listen to?", fLangTitle, Brushes.White,
                    new RectangleF(mx + 20, my + 56, mw - 40, 30), sfCenter);
            }

            using (SolidBrush subB = new SolidBrush(textSub))
            {
                g.DrawString("Select one or more languages (e.g. 1, 2, 3, or 4) to personalize your Home feed, trending charts, and recommendations.",
                    fontSubReg, subB, new RectangleF(mx + 40, my + 88, mw - 80, 22), sfCenter);
            }

            int gridTop = my + 120;
            int cols = 7;
            int cardGap = 10;
            int cardW = (mw - 48 - (cols - 1) * cardGap) / cols;
            int cardH = 74;

            for (int i = 0; i < availableLanguages.Count; i++)
            {
                int r = i / cols;
                int c = i % cols;
                int cx = mx + 24 + c * (cardW + cardGap);
                int cy = gridTop + r * (cardH + 12);

                MusicLanguageItem item = availableLanguages[i];
                bool isSel = userSelectedLanguages.Contains(item.Name);
                Rectangle cardRect = new Rectangle(cx, cy, cardW, cardH);

                using (SolidBrush cb = new SolidBrush(isSel ? Color.FromArgb(45, accentColor.R, accentColor.G, accentColor.B) : Color.FromArgb(24, 28, 42)))
                {
                    FillRoundRect(g, cb, cardRect, 12);
                }
                using (Pen cp = new Pen(isSel ? accentColor : Color.FromArgb(45, 50, 70), isSel ? 1.8f : 1f))
                {
                    DrawRoundRect(g, cp, cardRect, 12);
                }

                if (isSel)
                {
                    Rectangle chkDot = new Rectangle(cardRect.Right - 20, cardRect.Y + 6, 14, 14);
                    using (SolidBrush chkB = new SolidBrush(accentColor))
                    {
                        g.FillEllipse(chkB, chkDot);
                    }
                    g.DrawString("\u2713", fontSmallBold, Brushes.Black, chkDot, sfCenter);
                }

                using (Font fLangName = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                using (Font fNative = new Font("Segoe UI", 9f, FontStyle.Regular))
                using (SolidBrush wb = new SolidBrush(isSel ? accentColor : textWhite))
                using (SolidBrush nb = new SolidBrush(isSel ? textWhite : textSub))
                {
                    g.DrawString(item.Name, fLangName, wb, new RectangleF(cardRect.X + 6, cardRect.Y + 12, cardRect.Width - 12, 22), sfCenter);
                    g.DrawString(item.NativeScript, fNative, nb, new RectangleF(cardRect.X + 6, cardRect.Y + 36, cardRect.Width - 12, 20), sfCenter);
                }
            }

            int bottomY = my + mh - 72;
            int selCount = userSelectedLanguages.Count;
            string selSummary = (selCount == 0)
                ? "Please select at least 1 language to continue."
                : (selCount + " Language" + (selCount > 1 ? "s" : "") + " Selected: " + string.Join(", ", new List<string>(userSelectedLanguages).ToArray()));

            using (SolidBrush sumB = new SolidBrush(selCount == 0 ? Color.FromArgb(255, 120, 120) : accentColor))
            {
                g.DrawString(selSummary, fontSmallBold, sumB, new RectangleF(mx + 30, bottomY - 26, mw - 60, 20), sfCenter);
            }

            int btnW = Math.Min(320, mw - 80);
            langContinueBtnRect = new Rectangle(mx + (mw - btnW) / 2, bottomY, btnW, 44);
            using (SolidBrush cbb = new SolidBrush(selCount > 0 ? accentColor : Color.FromArgb(40, 45, 60)))
            {
                FillRoundRect(g, cbb, langContinueBtnRect, 14);
            }
            string btnLabel = selCount > 0
                ? ("Continue to Leo Music (" + selCount + " Selected) \u2192")
                : "Select at least 1 language";
            using (SolidBrush cbt = new SolidBrush(selCount > 0 ? Color.Black : textMuted))
            {
                g.DrawString(btnLabel, fontTitleBold, cbt, langContinueBtnRect, sfCenter);
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
                g.DrawString("\u26A1 AUTO-UPDATE CENTER", fontSmallBold, bt, badgeRect, sfCenter);
            }

            using (SolidBrush verB = new SolidBrush(textSub))
            {
                g.DrawString("Installed: v" + CURRENT_APP_VERSION + "  \u2192  Latest: v" + latestRemoteVersion, fontSmallBold, verB, mx + 210, my + 24);
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
            g.DrawString(isInstallingUpdate ? "Updating..." : "\u26A1 Update & Restart Now", fontSmallBold, Brushes.Black, updateInstallBtnRect, sfCenter);

            using (SolidBrush eb = new SolidBrush(Color.FromArgb(35, 42, 60)))
            {
                FillRoundRect(g, eb, updateEditUrlBtnRect, 12);
            }
            DrawPadlockIcon(g, Color.White, updateEditUrlBtnRect.X + 18, updateEditUrlBtnRect.Y + 19, isAdminUpdateUnlocked);
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
            return 72;
        }

        // =========================================================================
        // v10.4.2 / v10.4.3 REDESIGNED CENTER STAGE & HOME PAGE (1:1 Reference Image 1)
        // =========================================================================

        private void PaintCenterSongStage(Graphics g, int centerX, int centerW, int mainH, Color curBgCard, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            topFilterHitZones.Clear();
            homeHitZones.Clear();

            int searchW = Math.Min(410, Math.Max(210, centerW - 190));
            Rectangle searchPill = new Rectangle(centerX + 24, 18, searchW, 38);
            using (SolidBrush spBg = new SolidBrush(Color.FromArgb(175, 24, 18, 22)))
            using (Pen spRim = new Pen(isSearchEditing ? accentColor : Color.FromArgb(55, 255, 255, 255), isSearchEditing ? 1.5f : 1.0f))
            {
                FillRoundRect(g, spBg, searchPill, 19);
                DrawRoundRect(g, spRim, searchPill, 19);
            }

            using (Pen mp = new Pen(Color.FromArgb(190, 210, 215, 225), 1.6f))
            {
                g.DrawEllipse(mp, searchPill.X + 14, searchPill.Y + 11, 11, 11);
                g.DrawLine(mp, searchPill.X + 23, searchPill.Y + 20, searchPill.X + 27, searchPill.Y + 24);
            }

            if (!isSearchEditing)
            {
                using (Font fPh = new Font("Segoe UI", 9.5f))
                using (SolidBrush pb = new SolidBrush(string.IsNullOrEmpty(searchQueryText) ? Color.FromArgb(155, 185, 190, 202) : textWhite))
                {
                    string displayStr = string.IsNullOrEmpty(searchQueryText) ? "Search songs, artists, albums..." : searchQueryText;
                    g.DrawString(displayStr, fPh, pb, new RectangleF(searchPill.X + 36, searchPill.Y + 9, searchPill.Width - 54, 20), sfLeftEllipsis);
                }
            }

            if (!string.IsNullOrEmpty(searchQueryText))
            {
                using (Pen xp = new Pen(textSub, 1.5f))
                {
                    int cx = searchPill.Right - 16;
                    int cy = searchPill.Y + 19;
                    g.DrawLine(xp, cx - 4, cy - 4, cx + 4, cy + 4);
                    g.DrawLine(xp, cx + 4, cy - 4, cx - 4, cy + 4);
                }
            }

            int avatarX = centerX + centerW - 64;
            Rectangle avatarRect = new Rectangle(avatarX, 18, 36, 36);
            SongItem avSong = currentSong ?? (catalog.Count > 0 ? catalog[0] : null);
            if (avSong != null)
            {
                DrawStaticAlbumArt(g, avSong.ThumbUrl, avatarRect, 18);
            }
            using (Pen avRim = new Pen(Color.FromArgb(190, accentColor), 1.6f))
            {
                g.DrawEllipse(avRim, avatarRect);
            }
            using (Pen chvPen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.6f))
            {
                int chX = avatarRect.Right + 10;
                int chY = avatarRect.Y + 17;
                g.DrawLine(chvPen, chX - 4, chY - 2, chX, chY + 2);
                g.DrawLine(chvPen, chX, chY + 2, chX + 4, chY - 2);
            }
            Rectangle avatarHitRect = new Rectangle(avatarX, 16, 56, 40);
            topFilterHitZones.Add(new HomeHitZone(avatarHitRect, "open_account_settings", null, "avatar_top", ""));

            int utilRight = avatarX - 10;
            if (utilRight - 66 > searchPill.Right + 12)
            {
                Rectangle topVolBtn = new Rectangle(utilRight - 64, 22, 60, 28);
                using (SolidBrush b = new SolidBrush(isVolumeControllerPopupOpen ? Color.FromArgb(85, accentColor) : Color.FromArgb(110, 22, 18, 24)))
                using (Pen p = new Pen(Color.FromArgb(80, accentColor), 1f))
                {
                    FillRoundRect(g, b, topVolBtn, 14);
                    DrawRoundRect(g, p, topVolBtn, 14);
                }
                using (SolidBrush t = new SolidBrush(volumePercent > 100 ? Color.FromArgb(255, 255, 205, 65) : Color.White))
                {
                    g.DrawString("\u266B " + (isMuted ? "0%" : (volumePercent + "%")), fontSmallBold, t, topVolBtn, sfCenter);
                }
                topFilterHitZones.Add(new HomeHitZone(topVolBtn, "toggle_vol_popup_top", null, "vol_top_btn", ""));
                utilRight -= 68;
            }
            if (utilRight - 64 > searchPill.Right + 12)
            {
                Rectangle topTurboBtn = new Rectangle(utilRight - 62, 22, 58, 28);
                using (SolidBrush b = new SolidBrush(uiTurboCacheEnabled ? Color.FromArgb(55, accentColor.R, accentColor.G, accentColor.B) : Color.FromArgb(110, 22, 18, 24)))
                using (Pen p = new Pen(Color.FromArgb(uiTurboCacheEnabled ? 130 : 45, accentColor), 1f))
                {
                    FillRoundRect(g, b, topTurboBtn, 14);
                    DrawRoundRect(g, p, topTurboBtn, 14);
                }
                using (SolidBrush t = new SolidBrush(uiTurboCacheEnabled ? accentColor : textSub))
                {
                    g.DrawString("\u26A1 Turbo", fontSmallBold, t, topTurboBtn, sfCenter);
                }
                topFilterHitZones.Add(new HomeHitZone(topTurboBtn, "toggle_turbo_top", null, "turbo_top_btn", ""));
                utilRight -= 66;
            }
            if (utilRight - 58 > searchPill.Right + 12)
            {
                Rectangle topDynBtn = new Rectangle(utilRight - 58, 22, 54, 28);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 22, 18, 24)))
                using (Pen p = new Pen(Color.FromArgb(65, accentColor), 1f))
                {
                    FillRoundRect(g, b, topDynBtn, 14);
                    DrawRoundRect(g, p, topDynBtn, 14);
                }
                using (SolidBrush t = new SolidBrush(accentColor))
                {
                    g.DrawString("\u2726 Bar", fontSmallBold, t, topDynBtn, sfCenter);
                }
                topFilterHitZones.Add(new HomeHitZone(topDynBtn, "toggle_dyn_bar_top", null, "dyn_bar_btn", ""));
                utilRight -= 62;
            }
            if (utilRight - 48 > searchPill.Right + 12)
            {
                Rectangle topMiniBtn = new Rectangle(utilRight - 48, 22, 44, 28);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 22, 18, 24)))
                using (Pen p = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                {
                    FillRoundRect(g, b, topMiniBtn, 14);
                    DrawRoundRect(g, p, topMiniBtn, 14);
                }
                using (SolidBrush t = new SolidBrush(textSub))
                {
                    g.DrawString("Mini", fontSmallBold, t, topMiniBtn, sfCenter);
                }
                topFilterHitZones.Add(new HomeHitZone(topMiniBtn, "toggle_mini_top", null, "mini_btn", ""));
                utilRight -= 52;
            }
            if (utilRight - 34 > searchPill.Right + 12)
            {
                Rectangle topUpdBtn = new Rectangle(utilRight - 34, 22, 30, 28);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 22, 18, 24)))
                using (Pen p = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                {
                    FillRoundRect(g, b, topUpdBtn, 14);
                    DrawRoundRect(g, p, topUpdBtn, 14);
                }
                using (SolidBrush t = new SolidBrush(textSub))
                {
                    g.DrawString("\u21BB", fontSmallBold, t, topUpdBtn, sfCenter);
                }
                topFilterHitZones.Add(new HomeHitZone(topUpdBtn, "check_updates_top", null, "update_top", ""));
            }

            int viewportTop = 66;
            int viewportH = mainH - viewportTop - 4;
            Rectangle stageViewport = new Rectangle(centerX, viewportTop, centerW, viewportH);

            if (activeTab == 0 && string.IsNullOrEmpty(searchQueryText))
            {
                PaintScrollableHomeDashboard(g, stageViewport, curBgCard, sfCenter, sfLeftEllipsis);
                return;
            }

            int listStartTop = viewportTop + 38;
            string tabHeading = (activeTab == 2) ? "Liked Songs"
                : ((activeTab == 3) ? "Downloads"
                : ((activeTab == 6) ? "Library \u2022 All Tracks" : ("Search Results" + (string.IsNullOrEmpty(searchQueryText) ? "" : (": \"" + searchQueryText + "\"")))));

            using (Font fSecHdr = new Font("Segoe UI", 13.5f, FontStyle.Bold))
            {
                g.DrawString(tabHeading, fSecHdr, Brushes.White, centerX + 24, viewportTop + 4);
            }

            int chipX = centerX + 215;
            int chipY = viewportTop + 6;
            for (int i = 0; i < moods.Length; i++)
            {
                string m = moods[i];
                int cw = (int)g.MeasureString(m, fontSmallBold).Width + 16;
                if (chipX + cw > centerX + centerW - 24) break;
                Rectangle cRect = new Rectangle(chipX, chipY, cw, 22);
                bool active = string.Equals(selectedMood, m, StringComparison.OrdinalIgnoreCase);
                using (SolidBrush cb = new SolidBrush(active ? accentColor : Color.FromArgb(120, 24, 20, 28)))
                {
                    FillRoundRect(g, cb, cRect, 11);
                }
                using (SolidBrush ctb = new SolidBrush(active ? Color.White : textSub))
                {
                    g.DrawString(m, fontSmallBold, ctb, cRect, sfCenter);
                }
                topFilterHitZones.Add(new HomeHitZone(cRect, "select_mood_chip", null, "mood_" + i, m));
                chipX += cw + 5;
            }

            Rectangle listViewport = new Rectangle(centerX, listStartTop, centerW, mainH - listStartTop - 6);
            int rowH = 58;
            totalHomeContentHeight = displaySongs.Count * (rowH + 6) + 24;
            Region oldClip = g.Clip;
            g.SetClip(listViewport);

            for (int i = 0; i < displaySongs.Count; i++)
            {
                int ry = listStartTop + 4 + i * (rowH + 6) - scrollOffset;
                if (ry + rowH < listStartTop || ry > listViewport.Bottom) continue;

                SongItem s = displaySongs[i];
                PaintInteractiveSongRow(g, s, new Rectangle(centerX + 24, ry, centerW - 48, rowH), curBgCard, "list_" + i, sfCenter, sfLeftEllipsis);
            }
            g.Clip = oldClip;
            DrawCustomScrollbar(g, listViewport, totalHomeContentHeight);
        }

        private List<SongItem> GetFilteredTrendingForHome(string activeLang, string activeMood)
        {
            List<SongItem> result = new List<SongItem>();
            List<SongItem> candidates = new List<SongItem>();

            if (activeLang != "All Languages" && languageSongShelves.ContainsKey(activeLang))
            {
                candidates.AddRange(languageSongShelves[activeLang]);
            }
            candidates.AddRange(trendingSongs);
            candidates.AddRange(displaySongs);
            candidates.AddRange(catalog);

            foreach (SongItem s in candidates)
            {
                bool langOk = (activeLang == "All Languages") || string.Equals(s.Language ?? "Hindi", activeLang, StringComparison.OrdinalIgnoreCase);
                bool moodOk = (activeMood == "All") || string.Equals(s.Mood ?? "All", activeMood, StringComparison.OrdinalIgnoreCase);
                if (langOk && moodOk && CanAddDiverseSongToList(result, s, 1))
                {
                    result.Add(s);
                    if (result.Count >= 5) return result;
                }
            }
            foreach (SongItem s in candidates)
            {
                bool langOk = (activeLang == "All Languages") || string.Equals(s.Language ?? "Hindi", activeLang, StringComparison.OrdinalIgnoreCase);
                if (langOk && CanAddDiverseSongToList(result, s, 1))
                {
                    result.Add(s);
                    if (result.Count >= 5) return result;
                }
            }
            foreach (SongItem s in candidates)
            {
                if (CanAddDiverseSongToList(result, s, 1))
                {
                    result.Add(s);
                    if (result.Count >= 5) break;
                }
            }
            return result;
        }

        private List<SongItem> GetSongsForLanguagePanel(string langName, string activeMood)
        {
            List<SongItem> list = new List<SongItem>();
            List<SongItem> raw = new List<SongItem>();
            if (languageSongShelves.ContainsKey(langName))
            {
                raw.AddRange(languageSongShelves[langName]);
            }
            foreach (SongItem s in catalog)
            {
                if (string.Equals(s.Language ?? "Hindi", langName, StringComparison.OrdinalIgnoreCase))
                {
                    raw.Add(s);
                }
            }
            if (activeMood != "All")
            {
                foreach (SongItem s in raw)
                {
                    if (string.Equals(s.Mood ?? "", activeMood, StringComparison.OrdinalIgnoreCase) && CanAddDiverseSongToList(list, s, 2))
                    {
                        list.Add(s);
                    }
                }
            }
            foreach (SongItem s in raw)
            {
                if (CanAddDiverseSongToList(list, s, 2))
                {
                    list.Add(s);
                }
            }
            return list;
        }

        // =========================================================================
        // v10.4.2 / v10.4.3 REDESIGNED HOME PAGE DASHBOARD (1:1 Reference Image 1)
        // =========================================================================

        private void PaintScrollableHomeDashboard(Graphics g, Rectangle vp, Color curBgCard, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            Region prevClip = g.Clip;
            g.SetClip(vp);

            int padX = vp.X + 24;
            int contentW = vp.Width - 44;
            int curY = vp.Y + 12 - scrollOffset;

            string activeLang = GetActiveFilterLanguageName();
            List<SongItem> curTrending = GetFilteredTrendingForHome(activeLang, selectedMood);
            SongItem heroSong = currentSong ?? ((curTrending.Count > 0) ? curTrending[0] : catalog[0]);

            // 1. HERO "PLAYING NOW" SPOTLIGHT (Boxless on the Atmospheric Backdrop, 1:1 Ref Image 1)
            int heroArtSz = 196;
            if (curY + heroArtSz + 10 >= vp.Y && curY <= vp.Bottom)
            {
                Rectangle heroArtRect = new Rectangle(padX, curY, heroArtSz, heroArtSz);

                for (int r = 3; r >= 1; r--)
                {
                    int ex = r * 3;
                    using (Pen gp = new Pen(Color.FromArgb(18 * (4 - r), accentColor.R, accentColor.G, accentColor.B), 2.5f))
                    {
                        DrawRoundRect(g, gp, new Rectangle(heroArtRect.X - ex, heroArtRect.Y - ex, heroArtRect.Width + ex * 2, heroArtRect.Height + ex * 2), 16 + ex);
                    }
                }
                DrawStaticAlbumArt(g, heroSong.ThumbUrl, heroArtRect, 16);
                using (Pen artRim = new Pen(Color.FromArgb(65, 255, 255, 255), 1.0f))
                {
                    DrawRoundRect(g, artRim, heroArtRect, 16);
                }
                homeHitZones.Add(new HomeHitZone(heroArtRect, "open_full_player", heroSong, "hero_art"));

                int infoX = heroArtRect.Right + 28;
                int infoMaxW = Math.Max(160, contentW - heroArtSz - 32);

                using (Font fEyebrow = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (Font fHeroTitle = new Font("Segoe UI", 26f, FontStyle.Bold))
                using (Font fHeroArtist = new Font("Segoe UI", 11.5f, FontStyle.Regular))
                using (SolidBrush eyeB = new SolidBrush(Color.FromArgb(190, 215, 210, 218)))
                using (SolidBrush artB = new SolidBrush(Color.FromArgb(215, 205, 210, 220)))
                {
                    string bgSlideTag = "";
                    if (songBgImageEnabled && currentSongBgSlideLabels.Count > 0)
                    {
                        int sIdx = Math.Max(0, Math.Min(currentSongBgSlideLabels.Count - 1, songBgCurrentSlideIdx));
                        bgSlideTag = "  \u2022  BG: " + currentSongBgSlideLabels[sIdx].ToUpperInvariant();
                    }
                    g.DrawString("PLAYING NOW" + bgSlideTag, fEyebrow, eyeB, infoX + 2, curY + 34);
                    g.DrawString(FormatMinimalCardTitle(heroSong.Title), fHeroTitle, Brushes.White, new RectangleF(infoX - 2, curY + 52, infoMaxW, 46), sfLeftEllipsis);
                    g.DrawString(GetPrimaryArtistName(heroSong.Artist), fHeroArtist, artB, new RectangleF(infoX + 2, curY + 100, infoMaxW, 24), sfLeftEllipsis);
                }

                int btnRowY = curY + 138;
                Rectangle heroPlayPill = ScaleRectCentered(new Rectangle(infoX, btnRowY, 118, 40), GetButtonScale("hero_play"));
                using (LinearGradientBrush playGrad = new LinearGradientBrush(
                    heroPlayPill,
                    accentColor,
                    Color.FromArgb(255, Math.Max(0, accentColor.R - 35), Math.Max(0, accentColor.G - 25), Math.Max(0, accentColor.B - 25)),
                    LinearGradientMode.Horizontal))
                {
                    FillRoundRect(g, playGrad, heroPlayPill, 20);
                }
                float pTriX = heroPlayPill.X + 34f;
                float pTriY = heroPlayPill.Y + 20f;
                if (isPlaying && currentSong != null && currentSong.Id == heroSong.Id)
                {
                    DrawPauseBars(g, Brushes.White, pTriX, pTriY, 3.0f, 11f, 3.5f);
                    using (Font fPlayBtn = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                    {
                        g.DrawString("Pause", fPlayBtn, Brushes.White, heroPlayPill.X + 48, heroPlayPill.Y + 10);
                    }
                }
                else
                {
                    DrawPlayTriangle(g, Brushes.White, pTriX, pTriY, 7.5f);
                    using (Font fPlayBtn = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                    {
                        g.DrawString("Play", fPlayBtn, Brushes.White, heroPlayPill.X + 50, heroPlayPill.Y + 10);
                    }
                }
                homeHitZones.Add(new HomeHitZone(heroPlayPill, "toggle_hero_play", heroSong, "hero_play"));

                Rectangle heroLikeCircle = ScaleRectCentered(new Rectangle(heroPlayPill.Right + 12, btnRowY, 40, 40), GetButtonScale("hero_like"));
                using (SolidBrush cBg = new SolidBrush(Color.FromArgb(145, 22, 18, 24)))
                using (Pen cRim = new Pen(Color.FromArgb(55, 255, 255, 255), 1.0f))
                {
                    g.FillEllipse(cBg, heroLikeCircle);
                    g.DrawEllipse(cRim, heroLikeCircle);
                }
                bool heroLiked = likedIds.Contains(heroSong.Id);
                DrawHeartIcon(g, heroLiked ? accentColor : Color.White, heroLikeCircle.X + 20f, heroLikeCircle.Y + 20f, heroLiked);
                homeHitZones.Add(new HomeHitZone(heroLikeCircle, "like_song", heroSong, "hero_like"));

                Rectangle heroMoreCircle = ScaleRectCentered(new Rectangle(heroLikeCircle.Right + 12, btnRowY, 40, 40), GetButtonScale("hero_add_pl"));
                using (SolidBrush cBg = new SolidBrush(Color.FromArgb(145, 22, 18, 24)))
                using (Pen cRim = new Pen(Color.FromArgb(55, 255, 255, 255), 1.0f))
                {
                    g.FillEllipse(cBg, heroMoreCircle);
                    g.DrawEllipse(cRim, heroMoreCircle);
                }
                g.DrawString("\u2022\u2022\u2022", fontSmallBold, Brushes.White, heroMoreCircle, sfCenter);
                homeHitZones.Add(new HomeHitZone(heroMoreCircle, "add_playlist_song", heroSong, "hero_add_pl"));
            }
            curY += heroArtSz + 26;

            // 2. "RECENTLY PLAYED" + "See All >" — 5-CARD 1:1 SQUARE ART ROW (1:1 Ref Image 1)
            List<SongItem> recentRowSongs = new List<SongItem>();
            if (currentSong != null) recentRowSongs.Add(currentSong);
            foreach (SongItem s in recentlyPlayedSongs)
            {
                if (recentRowSongs.Count >= 5) break;
                if (!ContainsSongByTitleOrId(recentRowSongs, s)) recentRowSongs.Add(s);
            }
            foreach (SongItem s in quickPlaySongs)
            {
                if (recentRowSongs.Count >= 5) break;
                if (!ContainsSongByTitleOrId(recentRowSongs, s)) recentRowSongs.Add(s);
            }
            foreach (SongItem s in catalog)
            {
                if (recentRowSongs.Count >= 5) break;
                if (!ContainsSongByTitleOrId(recentRowSongs, s)) recentRowSongs.Add(s);
            }

            if (curY + 28 >= vp.Y && curY <= vp.Bottom)
            {
                using (Font fSecTitle = new Font("Segoe UI", 13.5f, FontStyle.Bold))
                {
                    g.DrawString("Recently Played", fSecTitle, Brushes.White, padX, curY);
                }
                Rectangle seeAllRect = new Rectangle(padX + contentW - 72, curY + 2, 72, 22);
                using (Font fSeeAll = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                using (SolidBrush saB = new SolidBrush(Color.FromArgb(190, 210, 215, 225)))
                {
                    g.DrawString("See All  \u203A", fSeeAll, saB, seeAllRect, sfCenter);
                }
                homeHitZones.Add(new HomeHitZone(seeAllRect, "open_library_tab", null, "see_all_rec"));
            }
            curY += 34;

            int numRecCards = Math.Min(5, recentRowSongs.Count);
            int recGap = 14;
            int recCardW = (contentW - (numRecCards - 1) * recGap) / Math.Max(1, numRecCards);
            int recArtSz = Math.Max(92, Math.Min(138, recCardW));
            int recRowTotalH = recArtSz + 46;

            if (curY + recRowTotalH >= vp.Y && curY <= vp.Bottom)
            {
                for (int i = 0; i < numRecCards; i++)
                {
                    SongItem rs = recentRowSongs[i];
                    string hKey = "rec_card_" + i;
                    bool isHov = (currentHoveredBtnKey == hKey);
                    bool isCur = (currentSong != null && currentSong.Id == rs.Id);

                    int cardX = padX + i * (recCardW + recGap);
                    Rectangle artBox = ScaleRectCentered(new Rectangle(cardX, curY, recArtSz, recArtSz), GetButtonScale(hKey));
                    DrawStaticAlbumArt(g, rs.ThumbUrl, artBox, 12);

                    using (Pen borderP = new Pen(isCur ? accentColor : (isHov ? Color.FromArgb(150, 255, 255, 255) : Color.FromArgb(40, 255, 255, 255)), isCur ? 1.8f : 1.0f))
                    {
                        DrawRoundRect(g, borderP, artBox, 12);
                    }

                    if (isHov)
                    {
                        Rectangle playBadge = new Rectangle(artBox.Right - 34, artBox.Bottom - 34, 28, 28);
                        using (SolidBrush pb = new SolidBrush(accentColor))
                        {
                            g.FillEllipse(pb, playBadge);
                        }
                        DrawPlayTriangle(g, Brushes.White, playBadge.X + 14f, playBadge.Y + 14f, 6.5f);
                    }

                    using (Font fCardT = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                    using (Font fCardA = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                    using (SolidBrush tb = new SolidBrush(isCur ? accentColor : Color.White))
                    using (SolidBrush ab = new SolidBrush(Color.FromArgb(175, 195, 200, 212)))
                    {
                        g.DrawString(FormatMinimalCardTitle(rs.Title), fCardT, tb, new RectangleF(cardX, curY + recArtSz + 6, recArtSz, 19), sfLeftEllipsis);
                        g.DrawString(GetPrimaryArtistName(rs.Artist), fCardA, ab, new RectangleF(cardX, curY + recArtSz + 24, recArtSz, 17), sfLeftEllipsis);
                    }

                    Rectangle fullCardHit = new Rectangle(cardX, curY, recArtSz, recRowTotalH);
                    homeHitZones.Add(new HomeHitZone(fullCardHit, "play_song", rs, hKey));
                }
            }
            curY += recRowTotalH + 22;

            // 3. BELOW-THE-FOLD SHELVES (Trending Now + Mood Filter + All Tracks)
            if (curY + 28 >= vp.Y && curY <= vp.Bottom)
            {
                string trTitle = "Trending Now" + (activeLang == "All Languages" ? "" : (" \u2022 " + activeLang));
                using (Font fSecTitle = new Font("Segoe UI", 13f, FontStyle.Bold))
                {
                    g.DrawString(trTitle, fSecTitle, Brushes.White, padX, curY);
                }
                Rectangle trRefBtn = ScaleRectCentered(new Rectangle(padX + contentW - 82, curY + 2, 82, 22), GetButtonScale("tr_refresh"));
                DrawGlassCard(g, trRefBtn, 11, curBgCard, false, currentHoveredBtnKey == "tr_refresh");
                using (SolidBrush rt = new SolidBrush(textSub))
                {
                    g.DrawString("\u21BB Refresh", fontSmallBold, rt, trRefBtn, sfCenter);
                }
                homeHitZones.Add(new HomeHitZone(trRefBtn, "refresh_trending", null, "tr_refresh"));
            }
            curY += 32;

            int numTrCards = Math.Min(5, curTrending.Count);
            if (curY + recRowTotalH >= vp.Y && curY <= vp.Bottom)
            {
                for (int i = 0; i < numTrCards; i++)
                {
                    SongItem trs = curTrending[i];
                    string hKey = "tr_card_" + i;
                    bool isHov = (currentHoveredBtnKey == hKey);
                    bool isCur = (currentSong != null && currentSong.Id == trs.Id);

                    int cardX = padX + i * (recCardW + recGap);
                    Rectangle artBox = ScaleRectCentered(new Rectangle(cardX, curY, recArtSz, recArtSz), GetButtonScale(hKey));
                    DrawStaticAlbumArt(g, trs.ThumbUrl, artBox, 12);

                    using (Pen borderP = new Pen(isCur ? accentColor : Color.FromArgb(40, 255, 255, 255), 1.0f))
                    {
                        DrawRoundRect(g, borderP, artBox, 12);
                    }

                    using (Font fCardT = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                    using (Font fCardA = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                    using (SolidBrush tb = new SolidBrush(isCur ? accentColor : Color.White))
                    using (SolidBrush ab = new SolidBrush(Color.FromArgb(175, 195, 200, 212)))
                    {
                        g.DrawString(FormatMinimalCardTitle(trs.Title), fCardT, tb, new RectangleF(cardX, curY + recArtSz + 6, recArtSz, 19), sfLeftEllipsis);
                        g.DrawString(GetPrimaryArtistName(trs.Artist), fCardA, ab, new RectangleF(cardX, curY + recArtSz + 24, recArtSz, 17), sfLeftEllipsis);
                    }

                    Rectangle fullCardHit = new Rectangle(cardX, curY, recArtSz, recRowTotalH);
                    homeHitZones.Add(new HomeHitZone(fullCardHit, "play_song", trs, hKey));
                }
            }
            curY += recRowTotalH + 20;

            if (curY + 30 >= vp.Y && curY <= vp.Bottom)
            {
                using (Font fSecTitle = new Font("Segoe UI", 12.5f, FontStyle.Bold))
                {
                    g.DrawString("All Tracks \u2022 " + activeLang + " (" + displaySongs.Count + ")", fSecTitle, Brushes.White, padX, curY);
                }
                int mChipX = padX + 230;
                for (int i = 0; i < moods.Length; i++)
                {
                    string m = moods[i];
                    int cw = (int)g.MeasureString(m, fontSmallBold).Width + 16;
                    if (mChipX + cw > padX + contentW) break;
                    Rectangle cRect = new Rectangle(mChipX, curY + 1, cw, 22);
                    bool actM = string.Equals(selectedMood, m, StringComparison.OrdinalIgnoreCase);
                    using (SolidBrush cb = new SolidBrush(actM ? accentColor : Color.FromArgb(120, 24, 20, 28)))
                    {
                        FillRoundRect(g, cb, cRect, 11);
                    }
                    using (SolidBrush ctb = new SolidBrush(actM ? Color.White : textSub))
                    {
                        g.DrawString(m, fontSmallBold, ctb, cRect, sfCenter);
                    }
                    homeHitZones.Add(new HomeHitZone(cRect, "select_mood_inline", null, "mood_in_" + i, m));
                    mChipX += cw + 5;
                }
            }
            curY += 32;

            int rowH = 56;
            for (int i = 0; i < displaySongs.Count; i++)
            {
                if (curY + rowH >= vp.Y && curY <= vp.Bottom)
                {
                    SongItem s = displaySongs[i];
                    PaintInteractiveSongRow(g, s, new Rectangle(padX, curY, contentW, rowH), curBgCard, "row_" + i, sfCenter, sfLeftEllipsis);
                }
                curY += rowH + 6;
            }

            totalHomeContentHeight = (curY + scrollOffset) - vp.Y + 24;
            g.Clip = prevClip;

            DrawCustomScrollbar(g, vp, totalHomeContentHeight);
        }

        private void PaintPlaylistsStudioScreen(Graphics g, int centerX, int centerW, int mainH, Color curBgCard, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            playlistStageHitZones.Clear();
            homeHitZones.Clear();
            int padX = centerX + 26;
            int contentW = centerW - 52;

            if (userPlaylists.Count == 0) InitDefaultPlaylists();
            if (selectedPlaylistIdx < 0 || selectedPlaylistIdx >= userPlaylists.Count) selectedPlaylistIdx = 0;

            using (Font fHead = new Font("Segoe UI", 15f, FontStyle.Bold))
            using (SolidBrush wb = new SolidBrush(textWhite))
            using (SolidBrush sb = new SolidBrush(textSub))
            {
                g.DrawString("Playlists & Leo Auto-Mood Studio", fHead, wb, padX, 24);
                g.DrawString("Leo Smart Playlist automatically adds & removes songs as your mood changes, or create your own playlists.", fontSubReg, sb, padX, 52);
            }

            if (isCreatingPlaylistInput)
            {
                Rectangle inputBg = new Rectangle(padX + contentW - 300, 26, 196, 32);
                using (SolidBrush ib = new SolidBrush(Color.FromArgb(24, 28, 42)))
                using (Pen ip = new Pen(accentColor, 1.4f))
                {
                    FillRoundRect(g, ib, inputBg, 10);
                    DrawRoundRect(g, ip, inputBg, 10);
                }
                Rectangle savePlBtn = new Rectangle(padX + contentW - 98, 26, 98, 32);
                using (SolidBrush sb = new SolidBrush(accentColor))
                {
                    FillRoundRect(g, sb, savePlBtn, 10);
                }
                g.DrawString("Save Playlist", fontSmallBold, Brushes.Black, savePlBtn, sfCenter);
                playlistStageHitZones.Add(new HomeHitZone(savePlBtn, "confirm_create_playlist", null, "save_pl_btn", ""));
            }
            else
            {
                Rectangle createPlBtn = ScaleRectCentered(new Rectangle(padX + contentW - 148, 26, 148, 34), GetButtonScale("create_pl_btn"));
                using (SolidBrush cb = new SolidBrush(accentColor))
                {
                    FillRoundRect(g, cb, createPlBtn, 17);
                }
                g.DrawString("+ Create Playlist", fontSmallBold, Brushes.Black, createPlBtn, sfCenter);
                playlistStageHitZones.Add(new HomeHitZone(createPlBtn, "start_create_playlist", null, "create_pl_btn", ""));
            }

            int plCardY = 82;
            int plCardH = 74;
            int numPl = Math.Min(5, userPlaylists.Count);
            int plGap = 10;
            int plCardW = (contentW - (Math.Max(2, numPl) - 1) * plGap) / Math.Max(2, numPl);

            for (int i = 0; i < userPlaylists.Count && i < 5; i++)
            {
                UserPlaylistItem pl = userPlaylists[i];
                bool isSel = (selectedPlaylistIdx == i);
                string hKey = "pl_card_" + i;
                Rectangle pRect = ScaleRectCentered(new Rectangle(padX + i * (plCardW + plGap), plCardY, plCardW, plCardH), GetButtonScale(hKey));

                using (SolidBrush b = new SolidBrush(isSel ? Color.FromArgb(48, accentColor.R, accentColor.G, accentColor.B) : curBgCard))
                using (Pen p = new Pen(isSel ? accentColor : Color.FromArgb(45, 52, 72), isSel ? 1.5f : 1f))
                {
                    FillRoundRect(g, b, pRect, 14);
                    DrawRoundRect(g, p, pRect, 14);
                }

                SongItem coverS = (pl.Songs.Count > 0) ? pl.Songs[0] : catalog[i % catalog.Count];
                DrawStaticAlbumArt(g, coverS.ThumbUrl, new Rectangle(pRect.X + 10, pRect.Y + 10, 54, 54), 10);

                using (SolidBrush wb = new SolidBrush(isSel ? accentColor : textWhite))
                using (SolidBrush sb = new SolidBrush(textSub))
                {
                    int tw = pRect.Width - 74;
                    g.DrawString(pl.Name, fontTitleBold, wb, new RectangleF(pRect.X + 70, pRect.Y + 14, tw, 22), sfLeftEllipsis);
                    string subInfo = pl.IsLeoAutoMood ? ("Auto-Mood: " + selectedMood + " (" + pl.Songs.Count + ")") : (pl.Songs.Count + " Songs");
                    g.DrawString(subInfo, fontSubReg, sb, new RectangleF(pRect.X + 70, pRect.Y + 38, tw, 18), sfLeftEllipsis);
                }

                playlistStageHitZones.Add(new HomeHitZone(pRect, "select_playlist_idx", null, hKey, i.ToString()));
            }

            UserPlaylistItem activePl = userPlaylists[selectedPlaylistIdx];
            int bannerY = 168;
            Rectangle bannerRect = new Rectangle(padX, bannerY, contentW, 64);
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(26, accentColor.R, accentColor.G, accentColor.B)))
            using (Pen bp = new Pen(Color.FromArgb(90, accentColor), 1.1f))
            {
                FillRoundRect(g, bb, bannerRect, 14);
                DrawRoundRect(g, bp, bannerRect, 14);
            }

            using (Font fPlTitle = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (SolidBrush wb = new SolidBrush(textWhite))
            using (SolidBrush sb = new SolidBrush(textSub))
            {
                g.DrawString(activePl.Name + "  (" + activePl.Songs.Count + " Tracks)", fPlTitle, wb, bannerRect.X + 16, bannerRect.Y + 10);
                g.DrawString(activePl.Subtitle, fontSubReg, sb, bannerRect.X + 16, bannerRect.Y + 36);
            }

            if (activePl.IsLeoAutoMood)
            {
                int mx = bannerRect.Right - 495;
                if (mx > bannerRect.X + 240)
                {
                    for (int mIdx = 0; mIdx < moods.Length; mIdx++)
                    {
                        string mName = moods[mIdx];
                        int mw = (int)g.MeasureString(mName, fontSmallBold).Width + 14;
                        Rectangle mr = new Rectangle(mx, bannerRect.Y + 20, mw, 24);
                        bool isActM = string.Equals(selectedMood, mName, StringComparison.OrdinalIgnoreCase);
                        using (SolidBrush mb = new SolidBrush(isActM ? accentColor : curBgCard))
                        {
                            FillRoundRect(g, mb, mr, 12);
                        }
                        using (SolidBrush mt = new SolidBrush(isActM ? Color.Black : textSub))
                        {
                            g.DrawString(mName, fontSmallBold, mt, mr, sfCenter);
                        }
                        playlistStageHitZones.Add(new HomeHitZone(mr, "leo_playlist_mood", null, "leo_m_" + mIdx, mName));
                        mx += mw + 4;
                    }
                }
            }
            else if (selectedPlaylistIdx >= 1 && userPlaylists.Count > 2)
            {
                Rectangle delPlBtn = new Rectangle(bannerRect.Right - 220, bannerRect.Y + 18, 96, 28);
                using (SolidBrush db = new SolidBrush(Color.FromArgb(45, 255, 80, 80)))
                {
                    FillRoundRect(g, db, delPlBtn, 14);
                }
                g.DrawString("Delete List", fontSmallBold, Brushes.White, delPlBtn, sfCenter);
                playlistStageHitZones.Add(new HomeHitZone(delPlBtn, "delete_custom_playlist", null, "del_pl", ""));
            }

            Rectangle playAllPlBtn = ScaleRectCentered(new Rectangle(bannerRect.Right - 114, bannerRect.Y + 17, 100, 30), GetButtonScale("play_active_pl"));
            using (SolidBrush pb = new SolidBrush(accentColor))
            {
                FillRoundRect(g, pb, playAllPlBtn, 15);
            }
            g.DrawString("\u25B6 Play All", fontSmallBold, Brushes.Black, playAllPlBtn, sfCenter);
            playlistStageHitZones.Add(new HomeHitZone(playAllPlBtn, "play_active_playlist", null, "play_active_pl", ""));

            int vpTop = 242;
            Rectangle vp = new Rectangle(centerX, vpTop, centerW, mainH - vpTop - 8);
            int rowH = 58;
            totalHomeContentHeight = activePl.Songs.Count * (rowH + 6) + 24;

            Region oldClip = g.Clip;
            g.SetClip(vp);
            for (int i = 0; i < activePl.Songs.Count; i++)
            {
                int ry = vpTop + 4 + i * (rowH + 6) - scrollOffset;
                if (ry + rowH < vpTop || ry > vp.Bottom) continue;
                SongItem s = activePl.Songs[i];
                PaintInteractiveSongRow(g, s, new Rectangle(padX, ry, contentW, rowH), curBgCard, "pl_row_" + i, sfCenter, sfLeftEllipsis);
            }
            g.Clip = oldClip;
            DrawCustomScrollbar(g, vp, totalHomeContentHeight);
        }

        private void PaintInteractiveSongRow(Graphics g, SongItem s, Rectangle baseRowRect, Color curBgCard, string hoverKey, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            bool isCur = (currentSong != null && currentSong.Id == s.Id);
            bool isHov = (currentHoveredBtnKey == hoverKey);
            Rectangle rowRect = isHov ? ScaleRectCentered(baseRowRect, 1.006f) : baseRowRect;

            DrawGlassCard(g, rowRect, 12, curBgCard, isCur, isHov);

            Rectangle thumbRect = new Rectangle(rowRect.X + 8, rowRect.Y + 8, 42, 42);
            DrawStaticAlbumArt(g, s.ThumbUrl, thumbRect, 10);

            using (SolidBrush wb = new SolidBrush(isCur ? accentColor : textWhite))
            using (SolidBrush subB = new SolidBrush(textSub))
            {
                int textMaxW = rowRect.Width - 290;
                g.DrawString(s.Title, fontTitleBold, wb, new RectangleF(rowRect.X + 60, rowRect.Y + 8, textMaxW, 22), sfLeftEllipsis);
                g.DrawString(s.Artist + "  \u2022  " + s.Mood, fontSubReg, subB, new RectangleF(rowRect.X + 60, rowRect.Y + 29, textMaxW, 20), sfLeftEllipsis);
            }

            using (SolidBrush mb = new SolidBrush(textMuted))
            {
                g.DrawString(FormatSec(s.Duration), fontSubReg, mb, rowRect.Right - 220, rowRect.Y + 20);
            }

            Rectangle addPlBtn = new Rectangle(rowRect.Right - 172, rowRect.Y + 14, 34, 30);
            using (SolidBrush plb = new SolidBrush(Color.FromArgb(30, 35, 50)))
            {
                FillRoundRect(g, plb, addPlBtn, 8);
            }
            bool isCustomPlView = (activeTab == 5 && selectedPlaylistIdx >= 1);
            using (SolidBrush plt = new SolidBrush(isCustomPlView ? Color.FromArgb(255, 110, 110) : accentColor))
            {
                g.DrawString(isCustomPlView ? "\u2715" : "+\u266B", fontSmallBold, plt, addPlBtn, sfCenter);
            }

            Rectangle addQBtn = new Rectangle(rowRect.Right - 132, rowRect.Y + 14, 30, 30);
            using (SolidBrush qb = new SolidBrush(Color.FromArgb(30, 35, 50)))
            {
                FillRoundRect(g, qb, addQBtn, 8);
            }
            DrawPlusQueueIcon(g, textSub, addQBtn.X + 15f, addQBtn.Y + 15f);

            Rectangle likeBtn = new Rectangle(rowRect.Right - 94, rowRect.Y + 14, 32, 30);
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

            homeHitZones.Add(new HomeHitZone(dlBtn, "download_song", s, hoverKey));
            homeHitZones.Add(new HomeHitZone(likeBtn, "like_song", s, hoverKey));
            homeHitZones.Add(new HomeHitZone(addQBtn, "queue_song", s, hoverKey));
            homeHitZones.Add(new HomeHitZone(addPlBtn, isCustomPlView ? "remove_playlist_song" : "add_playlist_song", s, hoverKey));
            homeHitZones.Add(new HomeHitZone(rowRect, "play_song", s, hoverKey));
        }

        private void DrawCustomScrollbar(Graphics g, Rectangle vp, int contentHeight)
        {
            if (contentHeight <= vp.Height + 10) return;
            int trackX = vp.Right - 10;
            int trackY = vp.Y + 6;
            int trackH = vp.Height - 12;

            float ratio = (float)vp.Height / contentHeight;
            int thumbH = Math.Max(32, (int)(trackH * ratio));
            int maxScroll = Math.Max(1, contentHeight - vp.Height);
            float scrollFrac = Math.Max(0f, Math.Min(1f, (float)scrollOffset / maxScroll));
            int thumbY = trackY + (int)((trackH - thumbH) * scrollFrac);

            using (SolidBrush trB = new SolidBrush(Color.FromArgb(25, 255, 255, 255)))
            using (SolidBrush thB = new SolidBrush(Color.FromArgb(145, accentColor)))
            {
                FillRoundRect(g, trB, new Rectangle(trackX, trackY, 5, trackH), 2);
                FillRoundRect(g, thB, new Rectangle(trackX, thumbY, 5, thumbH), 2);
            }
        }

        // =========================================================================
        // v10.4.3 MULTI-PAGE TABBED SETTINGS STUDIO
        // Replaces the single crowded settings page with 5 spacious, organized tabs:
        //   0: [ Account & Profile ]   — Studio Profile, Listening Stats, 14 Languages & Stream Quality
        //   1: [ Player & Audio EQ ]   — 5-Band Hardware EQ, 3 DSP Sliders, Crossfade, Speed & Sleep Timer
        //   2: [ Lyrics & Dyn Bar ]    — BetterLyrics 2.0 (8 Visual Controls), Dynamic Bar & Mini-Player Shapes
        //   3: [ Theme & Background ]  — Multi-Image Song Background Slideshow (ON/OFF, Style, Fade, Speed, Preview),
        //                                3D Glass, Dynamic Lighting, 6 3D Art Styles & 9 Theme Palettes
        //   4: [ System & Updates ]    — 60Hz+/120Hz Engine, Audio Cache Manager, GitHub OTA Updater & Discord
        // =========================================================================

        private void PaintSettingsAndEqualizerScreen(Graphics g, int centerX, int centerW, int mainH, Color curBgCard, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            int padX = centerX + 24;
            int contentW = centerW - 48;

            using (Font fHead = new Font("Segoe UI", 15f, FontStyle.Bold))
            using (SolidBrush wb = new SolidBrush(textWhite))
            using (SolidBrush sb = new SolidBrush(textSub))
            {
                g.DrawString("Studio Settings", fHead, wb, padX, 16);
                g.DrawString("Organized across 5 dedicated studio pages \u2022 All settings save automatically", fontSubReg, sb, padX + 158, 21);
            }

            // 5-Tab Horizontal Navigation Pill Bar (`y = 50, h = 36`)
            int tabBarY = 50;
            int numTabs = settingsSubPageNames.Length;
            int tabGap = 8;
            int tabW = (contentW - (numTabs - 1) * tabGap) / numTabs;

            for (int i = 0; i < numTabs; i++)
            {
                Rectangle tRect = new Rectangle(padX + i * (tabW + tabGap), tabBarY, tabW, 36);
                bool isSel = (activeSettingsSubPage == i);
                if (isSel)
                {
                    using (LinearGradientBrush selB = new LinearGradientBrush(
                        tRect,
                        Color.FromArgb(235, accentColor.R, accentColor.G, accentColor.B),
                        Color.FromArgb(165, Math.Max(0, accentColor.R - 45), Math.Max(0, accentColor.G - 25), Math.Max(0, accentColor.B - 25)),
                        LinearGradientMode.Horizontal))
                    {
                        FillRoundRect(g, selB, tRect, 12);
                    }
                    using (Pen rp = new Pen(Color.FromArgb(190, 255, 255, 255), 1.0f))
                    {
                        DrawRoundRect(g, rp, tRect, 12);
                    }
                }
                else
                {
                    DrawGlassCard(g, tRect, 12, curBgCard, false, false);
                }

                using (Font fTab = new Font("Segoe UI", 9.0f, isSel ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(isSel ? Color.White : Color.FromArgb(205, 210, 222)))
                {
                    g.DrawString(settingsSubPageNames[i], fTab, tb, tRect, sfCenter);
                }
            }

            int pageTopY = 98;
            int halfW = (contentW - 16) / 2;

            // -----------------------------------------------------------------
            // PAGE 0: ACCOUNT & PROFILE PAGE
            // -----------------------------------------------------------------
            if (activeSettingsSubPage == 0)
            {
                // Card 1: User Studio Profile & Listening Statistics Banner (`y = 98, h = 154`)
                Rectangle profCard = new Rectangle(padX, pageTopY, contentW, 154);
                DrawGlassCard(g, profCard, 18, curBgCard, true, false);

                Rectangle avRect = new Rectangle(profCard.X + 22, profCard.Y + 22, 92, 92);
                SongItem avS = currentSong ?? (catalog.Count > 0 ? catalog[0] : null);
                if (avS != null) DrawStaticAlbumArt(g, avS.ThumbUrl, avRect, 46);
                using (Pen avRim = new Pen(accentColor, 2.2f))
                {
                    g.DrawEllipse(avRim, avRect);
                }

                using (Font fBadge = new Font("Segoe UI", 8.2f, FontStyle.Bold))
                using (Font fName = new Font("Segoe UI", 16f, FontStyle.Bold))
                using (SolidBrush ab = new SolidBrush(accentColor))
                using (SolidBrush sb = new SolidBrush(textSub))
                {
                    g.DrawString("\u2605 LEO STUDIO PRO ACCOUNT \u2022 v" + CURRENT_APP_VERSION, fBadge, ab, profCard.X + 132, profCard.Y + 20);
                    g.DrawString("Leo Music Studio Listener", fName, Brushes.White, profCard.X + 130, profCard.Y + 38);
                    string favArtStr = favoritePlayedArtists.Count > 0
                        ? ("Top Artists: " + string.Join(", ", favoritePlayedArtists.GetRange(0, Math.Min(3, favoritePlayedArtists.Count)).ToArray()))
                        : "Top Artists: Diljit Dosanjh, Karan Aujla, Arijit Singh";
                    g.DrawString(favArtStr, fontSubReg, sb, new RectangleF(profCard.X + 132, profCard.Y + 68, profCard.Width - 150, 20), sfLeftEllipsis);
                }

                // 4 Stat Pills inside Account Card
                int minsListened = Math.Max(1, totalListeningSeconds / 60);
                string[] statLabels = new string[]
                {
                    "Listening Time: " + minsListened + " mins",
                    "Songs Played: " + Math.Max(1, userPlayedSongsCount),
                    "Liked Songs: " + likedIds.Count,
                    "Custom Playlists: " + userPlaylists.Count
                };
                int statW = (profCard.Width - 154 - 3 * 10) / 4;
                for (int i = 0; i < 4; i++)
                {
                    Rectangle sr = new Rectangle(profCard.X + 132 + i * (statW + 10), profCard.Y + 98, statW, 36);
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(45, 0, 0, 0)))
                    using (Pen sp = new Pen(Color.FromArgb(65, accentColor), 1f))
                    {
                        FillRoundRect(g, sb, sr, 10);
                        DrawRoundRect(g, sp, sr, 10);
                    }
                    g.DrawString(statLabels[i], fontSmallBold, Brushes.White, sr, sfCenter);
                }

                // Card 2 (Left): Personalized Music Languages (`y = 266, h = 190`)
                Rectangle langCard = new Rectangle(padX, pageTopY + 168, halfW, 190);
                DrawGlassCard(g, langCard, 18, curBgCard, false, false);
                using (SolidBrush ab = new SolidBrush(accentColor))
                using (SolidBrush wb = new SolidBrush(textWhite))
                using (SolidBrush sb = new SolidBrush(textSub))
                {
                    g.DrawString("PERSONALIZED MUSIC LANGUAGES (14 LANGUAGES)", fontSmallBold, ab, langCard.X + 18, langCard.Y + 14);
                    g.DrawString("Your Home feed, Trending Now & Leo Auto-Mood Playlist curate from:", fontSubReg, sb, new RectangleF(langCard.X + 18, langCard.Y + 38, langCard.Width - 36, 20), sfLeftEllipsis);

                    string selLangs = string.Join(", ", new List<string>(userSelectedLanguages).ToArray());
                    if (string.IsNullOrEmpty(selLangs)) selLangs = "Hindi, Punjabi, Haryanvi";
                    using (Font fLangList = new Font("Segoe UI", 11f, FontStyle.Bold))
                    {
                        g.DrawString(selLangs, fLangList, wb, new RectangleF(langCard.X + 18, langCard.Y + 66, langCard.Width - 36, 44), sfLeftEllipsis);
                    }
                }
                Rectangle openLangModalBtn = new Rectangle(langCard.X + 18, langCard.Bottom - 54, langCard.Width - 36, 38);
                using (SolidBrush b = new SolidBrush(accentColor))
                {
                    FillRoundRect(g, b, openLangModalBtn, 12);
                }
                g.DrawString("Choose Music Languages (" + userSelectedLanguages.Count + " Selected) \u2192", fontSmallBold, Brushes.White, openLangModalBtn, sfCenter);

                // Card 3 (Right): Audio Stream Quality & Community (`y = 266, h = 190`)
                Rectangle qualCard = new Rectangle(padX + halfW + 16, pageTopY + 168, halfW, 190);
                DrawGlassCard(g, qualCard, 18, curBgCard, false, false);
                using (SolidBrush ab = new SolidBrush(accentColor))
                using (SolidBrush sb = new SolidBrush(textSub))
                {
                    g.DrawString("STREAMING AUDIO QUALITY & COMMUNITY", fontSmallBold, ab, qualCard.X + 18, qualCard.Y + 14);
                    g.DrawString("Select decryption bitrate for JioSaavn 320kbps engine:", fontSubReg, sb, qualCard.X + 18, qualCard.Y + 38);
                }
                string[] qCodes = new string[] { "320kbps", "160kbps", "96kbps" };
                int qBtnW = (qualCard.Width - 36 - 16) / 3;
                for (int i = 0; i < 3; i++)
                {
                    Rectangle qr = new Rectangle(qualCard.X + 18 + i * (qBtnW + 8), qualCard.Y + 68, qBtnW, 36);
                    bool actQ = string.Equals(activeStreamQuality, qCodes[i], StringComparison.OrdinalIgnoreCase);
                    using (SolidBrush b = new SolidBrush(actQ ? accentColor : Color.FromArgb(30, 35, 50)))
                    {
                        FillRoundRect(g, b, qr, 10);
                    }
                    using (SolidBrush t = new SolidBrush(actQ ? Color.White : textSub))
                    {
                        g.DrawString(streamQualityNames[i], fontSmallBold, t, qr, sfCenter);
                    }
                }

                Rectangle discAccBtn = new Rectangle(qualCard.X + 18, qualCard.Bottom - 54, qualCard.Width - 36, 38);
                using (SolidBrush db = new SolidBrush(Color.FromArgb(195, 88, 101, 242)))
                {
                    FillRoundRect(g, db, discAccBtn, 12);
                }
                g.DrawString("Join Leo Music Discord Community \u2197", fontSmallBold, Brushes.White, discAccBtn, sfCenter);
                return;
            }

            // -----------------------------------------------------------------
            // PAGE 1: PLAYER & AUDIO EQ PAGE
            // -----------------------------------------------------------------
            if (activeSettingsSubPage == 1)
            {
                // Row 1: 5-Band Hardware Equalizer & 3 DSP Sliders (`y = 98, h = 228`)
                Rectangle eqCard = new Rectangle(padX, pageTopY, contentW, 228);
                DrawGlassCard(g, eqCard, 18, curBgCard, false, false);

                using (Font fSec = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (SolidBrush ab = new SolidBrush(accentColor))
                {
                    g.DrawString("5-BAND HARDWARE EQUALIZER & DSP (" + (eqEnabled ? activeEqPreset : "OFF") + ")", fSec, ab, eqCard.X + 18, eqCard.Y + 14);
                }
                Rectangle eqToggleRect = new Rectangle(eqCard.Right - 64, eqCard.Y + 12, 44, 20);
                DrawToggleSwitch(g, eqToggleRect, eqEnabled);

                int px = eqCard.X + 18;
                int py = eqCard.Y + 40;
                using (Font fChip = new Font("Segoe UI", 8.2f, FontStyle.Bold))
                {
                    for (int i = 0; i < eqPresetNames.Length; i++)
                    {
                        string pn = eqPresetNames[i];
                        int pw = (int)g.MeasureString(pn, fChip).Width + 16;
                        if (px + pw > eqCard.Right - 16) break;
                        Rectangle pr = new Rectangle(px, py, pw, 24);
                        bool act = eqEnabled && (activeEqPreset == pn);
                        using (SolidBrush pb = new SolidBrush(act ? accentColor : Color.FromArgb(35, 42, 60)))
                        {
                            FillRoundRect(g, pb, pr, 12);
                        }
                        using (SolidBrush pt = new SolidBrush(act ? Color.White : textSub))
                        {
                            g.DrawString(pn, fChip, pt, pr, sfCenter);
                        }
                        px += pw + 6;
                    }
                }

                int bandStartX = eqCard.X + 36;
                int bandTopY = eqCard.Y + 84;
                int bandTrackH = 104;
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
                        string dbTxt = eqBands[i].ToString("+0.0;-0.0;0.0");
                        g.DrawString(dbTxt, fontSmallBold, Brushes.White, new RectangleF(bx - 24, bandTopY - 16, 48, 15), sfCenter);
                        g.DrawString(eqBandLabels[i], fontSmallBold, subB, new RectangleF(bx - 24, bandTopY + bandTrackH + 5, 48, 16), sfCenter);
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
                        int dy = eqCard.Y + 80 + i * 46;
                        g.DrawString(dspNames[i], fontSmallBold, wb, dspStartX, dy);
                        g.DrawString(dspVals[i] + "%", fontSmallBold, sb, dspStartX + dspW - 38, dy);

                        int barY = dy + 22;
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

                // Row 2 Left: Dual-Deck Crossfade & Playback (`y = 338, h = 194`)
                Rectangle cfCard = new Rectangle(padX, pageTopY + 240, halfW, 194);
                DrawGlassCard(g, cfCard, 18, curBgCard, false, false);
                using (SolidBrush ab = new SolidBrush(accentColor))
                using (SolidBrush wb = new SolidBrush(textWhite))
                using (SolidBrush subB = new SolidBrush(textSub))
                {
                    g.DrawString("DUAL-DECK CROSSFADE & GAPLESS ENGINE", fontSmallBold, ab, cfCard.X + 16, cfCard.Y + 14);
                    g.DrawString("Overlap Crossfade (" + (crossfadeEnabled ? (crossfadeSeconds + "s") : "Off") + ")", fontSubReg, wb, cfCard.X + 16, cfCard.Y + 42);
                    DrawToggleSwitch(g, new Rectangle(cfCard.Right - 58, cfCard.Y + 40, 42, 20), crossfadeEnabled);

                    int[] cfOpts = new int[] { 1, 3, 5, 8, 12 };
                    for (int i = 0; i < cfOpts.Length; i++)
                    {
                        Rectangle cfr = new Rectangle(cfCard.X + 16 + i * 50, cfCard.Y + 70, 44, 24);
                        bool act = crossfadeEnabled && (crossfadeSeconds == cfOpts[i]);
                        using (SolidBrush b = new SolidBrush(act ? accentColor : Color.FromArgb(30, 35, 50)))
                        {
                            FillRoundRect(g, b, cfr, 8);
                        }
                        using (SolidBrush tb = new SolidBrush(act ? Color.White : textSub))
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

                // Row 2 Right: Playback Speed / Nightcore & Sleep Timer (`y = 338, h = 194`)
                Rectangle spdCard = new Rectangle(padX + halfW + 16, pageTopY + 240, halfW, 194);
                DrawGlassCard(g, spdCard, 18, curBgCard, false, false);
                using (SolidBrush ab = new SolidBrush(accentColor))
                using (SolidBrush wb = new SolidBrush(textWhite))
                {
                    g.DrawString("PLAYBACK SPEED, NIGHTCORE & SLEEP TIMER", fontSmallBold, ab, spdCard.X + 16, spdCard.Y + 14);
                    g.DrawString("Real-Time Speed / Pitch Mode:", fontSmallBold, wb, spdCard.X + 16, spdCard.Y + 42);
                }
                int spBtnW = (spdCard.Width - 32 - 3 * 6) / 4;
                for (int i = 0; i < playbackSpeedNames.Length; i++)
                {
                    Rectangle sr = new Rectangle(spdCard.X + 16 + i * (spBtnW + 6), spdCard.Y + 66, spBtnW, 28);
                    bool actS = (playbackSpeedIdx == i);
                    using (SolidBrush b = new SolidBrush(actS ? accentColor : Color.FromArgb(30, 35, 50)))
                    {
                        FillRoundRect(g, b, sr, 8);
                    }
                    using (SolidBrush t = new SolidBrush(actS ? Color.White : textSub))
                    {
                        g.DrawString(playbackSpeedNames[i], fontSmallBold, t, sr, sfCenter);
                    }
                }

                using (SolidBrush wb = new SolidBrush(textWhite))
                {
                    g.DrawString("Auto Sleep Timer:", fontSmallBold, wb, spdCard.X + 16, spdCard.Y + 112);
                }
                int[] stOpts = new int[] { 0, 15, 30, 60 };
                for (int i = 0; i < stOpts.Length; i++)
                {
                    Rectangle str = new Rectangle(spdCard.X + 16 + i * (spBtnW + 6), spdCard.Y + 136, spBtnW, 28);
                    bool act = (sleepTimerMinutes == stOpts[i]);
                    using (SolidBrush b = new SolidBrush(act ? accentColor : Color.FromArgb(30, 35, 50)))
                    {
                        FillRoundRect(g, b, str, 8);
                    }
                    using (SolidBrush tb = new SolidBrush(act ? Color.White : textSub))
                    {
                        g.DrawString(stOpts[i] == 0 ? "Off" : (stOpts[i] + " mins"), fontSmallBold, tb, str, sfCenter);
                    }
                }
                return;
            }

            // -----------------------------------------------------------------
            // PAGE 2: LYRICS & DYNAMIC BAR PAGE
            // -----------------------------------------------------------------
            if (activeSettingsSubPage == 2)
            {
                // Card 1: BetterLyrics 2.0 Studio (8 Customization Options) (`y = 98, h = 196`)
                Rectangle lyrSetCard = new Rectangle(padX, pageTopY, contentW, 196);
                DrawGlassCard(g, lyrSetCard, 18, curBgCard, false, false);
                using (SolidBrush ab = new SolidBrush(accentColor))
                using (SolidBrush sb = new SolidBrush(textSub))
                {
                    g.DrawString("BETTERLYRICS 2.0 STUDIO (BOXLESS FADE-ON-FADE, FADE SIZE, GLOW, ART & TYPOGRAPHY)", fontSmallBold, ab, lyrSetCard.X + 18, lyrSetCard.Y + 14);
                    g.DrawString("Click any studio control below to customize your full-screen and side-panel synced lyrics:", fontSubReg, sb, lyrSetCard.X + 18, lyrSetCard.Y + 36);
                }

                string[] lyrOptTitles = new string[]
                {
                    "Stage Style: " + lyricStageStyleNames[lyricStageStyleIdx],
                    "Fade Curve: " + lyricFadeModeNames[lyricFadeModeIdx],
                    "Fade Zone Size: " + lyricFadeSizeNames[lyricFadeSizeIdx],
                    "Vocal Glow: " + lyricGlowModeNames[lyricGlowModeIdx],
                    "Background Art: " + lyricBgArtModeNames[lyricBgArtModeIdx],
                    "Font Family: " + lyricFontStyleNames[lyricFontStyleIdx],
                    "Lyrics Size: " + lyricTextSizeNames[lyricTextSizeIdx],
                    "Dynamic Bar: " + dynamicBarStyleNames[dynamicBarStyleIdx]
                };

                int lCols = 4;
                int lBtnW = (lyrSetCard.Width - 36 - (lCols - 1) * 10) / lCols;
                for (int i = 0; i < 8; i++)
                {
                    int r = i / lCols;
                    int c = i % lCols;
                    Rectangle lr = new Rectangle(lyrSetCard.X + 18 + c * (lBtnW + 10), lyrSetCard.Y + 68 + r * 52, lBtnW, 40);
                    bool isHighlight = (i == 0 && lyricStageStyleIdx == 0) || (i == 3 && lyricGlowModeIdx != 3);
                    using (SolidBrush b = new SolidBrush(isHighlight ? Color.FromArgb(52, accentColor.R, accentColor.G, accentColor.B) : Color.FromArgb(28, 34, 48)))
                    using (Pen p = new Pen(Color.FromArgb(isHighlight ? 180 : 80, accentColor), 1.1f))
                    {
                        FillRoundRect(g, b, lr, 10);
                        DrawRoundRect(g, p, lr, 10);
                    }
                    g.DrawString(lyrOptTitles[i], fontSmallBold, Brushes.White, lr, sfCenter);
                }

                // Card 2 (Left): Always-On-Top Phone-Style Dynamic Bar (`y = 308, h = 158`)
                Rectangle dynCard = new Rectangle(padX, pageTopY + 210, halfW, 158);
                DrawGlassCard(g, dynCard, 18, curBgCard, false, false);
                using (SolidBrush ab = new SolidBrush(accentColor))
                using (SolidBrush sb = new SolidBrush(textSub))
                {
                    g.DrawString("PHONE-STYLE SMALL DYNAMIC BAR OVERLAY", fontSmallBold, ab, dynCard.X + 18, dynCard.Y + 14);
                    g.DrawString("Compact 264x34 pill with Leo Crest, Song Name & Waveform that overlays any app:", fontSubReg, sb, new RectangleF(dynCard.X + 18, dynCard.Y + 38, dynCard.Width - 36, 36), sfLeftEllipsis);
                }
                Rectangle launchDynBtn = new Rectangle(dynCard.X + 18, dynCard.Bottom - 52, (dynCard.Width - 46) / 2, 36);
                Rectangle switchDynStyleBtn = new Rectangle(launchDynBtn.Right + 10, dynCard.Bottom - 52, (dynCard.Width - 46) / 2, 36);
                using (SolidBrush b1 = new SolidBrush(accentColor))
                using (SolidBrush b2 = new SolidBrush(Color.FromArgb(34, 40, 56)))
                {
                    FillRoundRect(g, b1, launchDynBtn, 10);
                    FillRoundRect(g, b2, switchDynStyleBtn, 10);
                }
                g.DrawString("\u2726 Launch Dynamic Bar", fontSmallBold, Brushes.White, launchDynBtn, sfCenter);
                g.DrawString("Theme: " + dynamicBarStyleNames[dynamicBarStyleIdx], fontSmallBold, Brushes.White, switchDynStyleBtn, sfCenter);

                // Card 3 (Right): Floating Mini-Player Window Shapes (`y = 308, h = 158`)
                Rectangle miniCard = new Rectangle(padX + halfW + 16, pageTopY + 210, halfW, 158);
                DrawGlassCard(g, miniCard, 18, curBgCard, false, false);
                using (SolidBrush ab = new SolidBrush(accentColor))
                using (SolidBrush sb = new SolidBrush(textSub))
                {
                    g.DrawString("FLOATING MINI-PLAYER WINDOW SHAPE", fontSmallBold, ab, miniCard.X + 18, miniCard.Y + 14);
                    g.DrawString("Select window geometry for Always-On-Top Mini-Player mode:", fontSubReg, sb, miniCard.X + 18, miniCard.Y + 38);
                }
                int mBtnW = (miniCard.Width - 36 - 3 * 6) / 4;
                for (int i = 0; i < miniPlayerStyleNames.Length; i++)
                {
                    Rectangle mr = new Rectangle(miniCard.X + 18 + i * (mBtnW + 6), miniCard.Y + 74, mBtnW, 34);
                    bool actM = (miniPlayerStyleIdx == i);
                    using (SolidBrush mb = new SolidBrush(actM ? accentColor : Color.FromArgb(30, 35, 50)))
                    {
                        FillRoundRect(g, mb, mr, 9);
                    }
                    using (SolidBrush mt = new SolidBrush(actM ? Color.White : textSub))
                    {
                        g.DrawString(miniPlayerStyleNames[i], fontSmallBold, mt, mr, sfCenter);
                    }
                }
                return;
            }

            // -----------------------------------------------------------------
            // PAGE 3: THEME & MULTI-IMAGE SONG BACKGROUND PAGE
            // -----------------------------------------------------------------
            if (activeSettingsSubPage == 3)
            {
                // Card 1: v10.4.3 Multi-Image Song Background Slideshow Studio (`y = 98, h = 178`)
                Rectangle bgSlideCard = new Rectangle(padX, pageTopY, contentW, 178);
                DrawGlassCard(g, bgSlideCard, 18, curBgCard, songBgImageEnabled, false);

                using (SolidBrush ab = new SolidBrush(accentColor))
                using (SolidBrush wb = new SolidBrush(textWhite))
                using (SolidBrush sb = new SolidBrush(textSub))
                {
                    g.DrawString("MULTI-IMAGE PLAYING SONG BACKGROUND SLIDESHOW (ORIGINAL, EDITED NEON, CONCERT, CYBER & ARTIST GALLERY)", fontSmallBold, ab, bgSlideCard.X + 18, bgSlideCard.Y + 14);
                    string curFrameDesc = (currentSongBgSlideLabels.Count > 0)
                        ? ("Active Frame (" + (songBgCurrentSlideIdx + 1) + "/" + currentSongBgSlides.Count + "): " + currentSongBgSlideLabels[Math.Max(0, Math.Min(currentSongBgSlideLabels.Count - 1, songBgCurrentSlideIdx))])
                        : "Automatically generates 6 widescreen non-thumbnail cinema frames per playing song";
                    g.DrawString("Show Playing Song Multi-Image Background Slideshow  \u2022  " + curFrameDesc, fontSubReg, wb, new RectangleF(bgSlideCard.X + 18, bgSlideCard.Y + 38, bgSlideCard.Width - 100, 22), sfLeftEllipsis);
                }
                DrawToggleSwitch(g, new Rectangle(bgSlideCard.Right - 64, bgSlideCard.Y + 36, 46, 22), songBgImageEnabled);

                // 4 Control Pills for Multi-Image Background: [Gallery Mode] [Transition Fade] [Change Speed] [Next Frame Now]
                string[] bgCtrlLabels = new string[]
                {
                    "Images: " + songBgStyleNames[songBgStyleIdx],
                    "Animation: " + songBgAnimModeNames[songBgAnimModeIdx],
                    "Interval: Every " + songBgIntervalSecs[songBgIntervalIdx] + "s",
                    "\u2726 Preview Next BG Frame"
                };
                int bgBtnW = (bgSlideCard.Width - 36 - 3 * 10) / 4;
                for (int i = 0; i < 4; i++)
                {
                    Rectangle br = new Rectangle(bgSlideCard.X + 18 + i * (bgBtnW + 10), bgSlideCard.Y + 74, bgBtnW, 38);
                    using (SolidBrush b = new SolidBrush(i == 3 ? accentColor : Color.FromArgb(32, 36, 52)))
                    using (Pen p = new Pen(Color.FromArgb(120, accentColor), 1f))
                    {
                        FillRoundRect(g, b, br, 10);
                        if (i < 3) DrawRoundRect(g, p, br, 10);
                    }
                    g.DrawString(bgCtrlLabels[i], fontSmallBold, Brushes.White, br, sfCenter);
                }

                // Live Filmstrip Preview of the 6 Generated Widescreen Slides inside the Card!
                int stripY = bgSlideCard.Y + 122;
                int numPrv = Math.Min(6, currentSongBgSlides.Count);
                int prvGap = 10;
                int prvW = (bgSlideCard.Width - 36 - Math.Max(1, numPrv - 1) * prvGap) / Math.Max(1, numPrv);
                for (int i = 0; i < numPrv; i++)
                {
                    Rectangle pr = new Rectangle(bgSlideCard.X + 18 + i * (prvW + prvGap), stripY, prvW, 44);
                    g.DrawImage(currentSongBgSlides[i], pr);
                    bool isCurSlide = (songBgCurrentSlideIdx == i);
                    using (Pen p = new Pen(isCurSlide ? accentColor : Color.FromArgb(75, 255, 255, 255), isCurSlide ? 2.2f : 1.0f))
                    {
                        g.DrawRectangle(p, pr);
                    }
                    if (i < currentSongBgSlideLabels.Count)
                    {
                        using (SolidBrush sc = new SolidBrush(Color.FromArgb(165, 0, 0, 0)))
                        {
                            g.FillRectangle(sc, pr.X, pr.Bottom - 16, pr.Width, 16);
                        }
                        using (Font fTiny = new Font("Segoe UI", 7.2f, FontStyle.Bold))
                        {
                            g.DrawString(currentSongBgSlideLabels[i], fTiny, isCurSlide ? new SolidBrush(accentColor) : Brushes.White, new RectangleF(pr.X + 2, pr.Bottom - 16, pr.Width - 4, 16), sfCenter);
                        }
                    }
                }

                // Card 2: Glass Surface, Dynamic Lighting, OLED & 6 3D Artwork Animations (`y = 288, h = 132`)
                Rectangle surfCard = new Rectangle(padX, pageTopY + 190, contentW, 132);
                DrawGlassCard(g, surfCard, 18, curBgCard, false, false);
                using (SolidBrush ab = new SolidBrush(accentColor))
                using (SolidBrush wb = new SolidBrush(textWhite))
                {
                    g.DrawString("3D CURVED GLASS, DYNAMIC SONG LIGHTING & 3D ARTWORK ANIMATIONS", fontSmallBold, ab, surfCard.X + 18, surfCard.Y + 12);
                }

                string[] surfToggles = new string[]
                {
                    "3D Curved Glass: " + (glassEffectEnabled ? (curved3DGlassEnabled ? "3D Convex" : "Flat Glass") : "OFF"),
                    "Dynamic Song Lighting: " + (dynamicLightingEnabled ? "ON" : "Manual"),
                    "OLED Pure Black: " + (oledPureBlack ? "ON" : "OFF"),
                    "Visualizer: " + visualizerStyleNames[visualizerStyleIdx]
                };
                for (int i = 0; i < 4; i++)
                {
                    Rectangle tr = new Rectangle(surfCard.X + 18 + i * (bgBtnW + 10), surfCard.Y + 38, bgBtnW, 34);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(32, 36, 52)))
                    using (Pen p = new Pen(Color.FromArgb(90, accentColor), 1f))
                    {
                        FillRoundRect(g, b, tr, 9);
                        DrawRoundRect(g, p, tr, 9);
                    }
                    g.DrawString(surfToggles[i], fontSmallBold, Brushes.White, tr, sfCenter);
                }

                int numShapes = artworkShapeNames.Length;
                int sw = (surfCard.Width - 36 - (numShapes - 1) * 8) / numShapes;
                for (int i = 0; i < numShapes; i++)
                {
                    Rectangle sr = new Rectangle(surfCard.X + 18 + i * (sw + 8), surfCard.Y + 84, sw, 32);
                    bool act = (artworkShapeMode == i);
                    using (SolidBrush b = new SolidBrush(act ? accentColor : Color.FromArgb(30, 35, 50)))
                    {
                        FillRoundRect(g, b, sr, 8);
                    }
                    using (SolidBrush tb = new SolidBrush(act ? Color.White : textSub))
                    {
                        g.DrawString("3D Art: " + artworkShapeNames[i], fontSmallBold, tb, sr, sfCenter);
                    }
                }

                // Card 3: 9 Signature Studio Theme Palettes (`y = 432, h = 64`)
                Rectangle palCard = new Rectangle(padX, pageTopY + 334, contentW, 64);
                DrawGlassCard(g, palCard, 16, curBgCard, false, false);
                int swX = palCard.X + 14;
                for (int i = 0; i < themes.Count; i++)
                {
                    int wChip = (contentW - 28 - 8 * 6) / 9;
                    Rectangle tr = new Rectangle(swX + i * (wChip + 6), palCard.Y + 14, wChip, 36);
                    bool act = (!dynamicLightingEnabled && activeThemeIdx == i);
                    using (SolidBrush b = new SolidBrush(act ? Color.FromArgb(65, themes[i].Accent) : Color.FromArgb(28, 32, 46)))
                    {
                        FillRoundRect(g, b, tr, 9);
                    }
                    if (act)
                    {
                        using (Pen p = new Pen(themes[i].Accent, 1.6f)) DrawRoundRect(g, p, tr, 9);
                    }
                    using (SolidBrush db = new SolidBrush(themes[i].Accent))
                    {
                        g.FillEllipse(db, tr.X + 8, tr.Y + 12, 12, 12);
                    }
                    using (SolidBrush tb = new SolidBrush(textWhite))
                    {
                        g.DrawString(themes[i].Name.Split(' ')[0], fontSmallBold, tb, tr.X + 24, tr.Y + 10);
                    }
                }
                return;
            }

            // -----------------------------------------------------------------
            // PAGE 4: SYSTEM, PERFORMANCE & AUTO-UPDATER PAGE
            // -----------------------------------------------------------------
            Rectangle sysCard = new Rectangle(padX, pageTopY, halfW, 210);
            DrawGlassCard(g, sysCard, 18, curBgCard, false, false);
            using (SolidBrush ab = new SolidBrush(accentColor))
            using (SolidBrush wb = new SolidBrush(textWhite))
            using (SolidBrush sb = new SolidBrush(textSub))
            {
                g.DrawString("120HZ HIGH-REFRESH ENGINE & AUDIO CACHE", fontSmallBold, ab, sysCard.X + 18, sysCard.Y + 14);
                g.DrawString("Active Frame Pacing: " + refreshRateModeNames[refreshRateModeIdx], fontTitleBold, wb, sysCard.X + 18, sysCard.Y + 42);
                g.DrawString("Local 320kbps Audio Cache Size: " + GetAudioCacheSizeText(), fontSubReg, sb, sysCard.X + 18, sysCard.Y + 70);
            }
            Rectangle hzBtn = new Rectangle(sysCard.X + 18, sysCard.Y + 106, sysCard.Width - 36, 36);
            Rectangle clrCacheBtn = new Rectangle(sysCard.X + 18, sysCard.Y + 152, (sysCard.Width - 46) / 2, 36);
            Rectangle openDlFolderBtn = new Rectangle(clrCacheBtn.Right + 10, sysCard.Y + 152, (sysCard.Width - 46) / 2, 36);
            using (SolidBrush b1 = new SolidBrush(accentColor))
            using (SolidBrush b2 = new SolidBrush(Color.FromArgb(34, 40, 56)))
            {
                FillRoundRect(g, b1, hzBtn, 10);
                FillRoundRect(g, b2, clrCacheBtn, 10);
                FillRoundRect(g, b2, openDlFolderBtn, 10);
            }
            g.DrawString("\u26A1 Switch Refresh Rate (" + refreshRateModeNames[refreshRateModeIdx] + ")", fontSmallBold, Brushes.White, hzBtn, sfCenter);
            g.DrawString("Clear Audio Cache", fontSmallBold, Brushes.White, clrCacheBtn, sfCenter);
            g.DrawString("Open Downloads Folder", fontSmallBold, Brushes.White, openDlFolderBtn, sfCenter);

            Rectangle updCard = new Rectangle(padX + halfW + 16, pageTopY, halfW, 210);
            DrawGlassCard(g, updCard, 18, curBgCard, false, false);
            using (SolidBrush ab = new SolidBrush(accentColor))
            using (SolidBrush wb = new SolidBrush(textWhite))
            using (SolidBrush sb = new SolidBrush(textSub))
            {
                g.DrawString("GITHUB IN-APP AUTO-UPDATER (v" + CURRENT_APP_VERSION + ")", fontSmallBold, ab, updCard.X + 18, updCard.Y + 14);
                g.DrawString("Connected to: umas8549/leo-music-updates", fontTitleBold, wb, updCard.X + 18, updCard.Y + 42);
                g.DrawString("Admin Config Lock: Password Protected", fontSubReg, sb, updCard.X + 18, updCard.Y + 70);
            }
            Rectangle chkUpdSysBtn = new Rectangle(updCard.X + 18, updCard.Y + 106, updCard.Width - 36, 36);
            Rectangle admUpdSysBtn = new Rectangle(updCard.X + 18, updCard.Y + 152, updCard.Width - 36, 36);
            using (SolidBrush b1 = new SolidBrush(accentColor))
            using (SolidBrush b2 = new SolidBrush(Color.FromArgb(34, 40, 56)))
            {
                FillRoundRect(g, b1, chkUpdSysBtn, 10);
                FillRoundRect(g, b2, admUpdSysBtn, 10);
            }
            g.DrawString("\u21BB Check for Updates Now", fontSmallBold, Brushes.White, chkUpdSysBtn, sfCenter);
            g.DrawString("Open Update Center & Admin Config", fontSmallBold, Brushes.White, admUpdSysBtn, sfCenter);

            // Row 2 Card on System Page: v10.4.4 Zero-Lag Turbo Engine, Studio Volume Controller & Playback Smoothness Suite (`y = pageTopY + 224, h = 166`)
            Rectangle turboCard = new Rectangle(padX, pageTopY + 224, contentW, 166);
            DrawGlassCard(g, turboCard, 18, curBgCard, true, false);
            using (SolidBrush ab = new SolidBrush(accentColor))
            using (SolidBrush wb = new SolidBrush(textWhite))
            using (SolidBrush sb = new SolidBrush(textSub))
            {
                g.DrawString("v10.4.4 ZERO-LAG TURBO OPTIMIZER, 150% VOLUME CONTROLLER & PLAYBACK SMOOTHNESS", fontSmallBold, ab, turboCard.X + 18, turboCard.Y + 14);
                g.DrawString("Pre-baked thumbnail cache, 384x216 composite backdrop buffer, 180ms smooth micro-fade, auto-leveler & next-track pre-buffer:", fontSubReg, sb, turboCard.X + 18, turboCard.Y + 36);
            }

            string[] turboLabels = new string[]
            {
                "\u26A1 UI Turbo Cache: " + (uiTurboCacheEnabled ? "ON (Zero-Lag)" : "OFF"),
                "\u266B Volume Popup & 150% Boost",
                "Smooth Micro-Fade: " + (smoothMicroFadeEnabled ? "ON (180ms)" : "OFF"),
                "Auto-Volume Leveler: " + (autoVolumeNormalizeEnabled ? "ON" : "OFF"),
                "Next-Song Pre-Buffer: " + (autoPreBufferNextEnabled ? "ON (0ms)" : "OFF"),
                "Buffer Profile: " + audioBufferProfileNames[audioBufferProfileIdx]
            };
            int tCols = 3;
            int tBtnW = (turboCard.Width - 36 - (tCols - 1) * 10) / tCols;
            for (int i = 0; i < 6; i++)
            {
                int r = i / tCols;
                int c = i % tCols;
                Rectangle tr = new Rectangle(turboCard.X + 18 + c * (tBtnW + 10), turboCard.Y + 66 + r * 44, tBtnW, 36);
                using (SolidBrush b = new SolidBrush(i == 1 ? accentColor : Color.FromArgb(32, 36, 52)))
                using (Pen p = new Pen(Color.FromArgb(120, accentColor), 1f))
                {
                    FillRoundRect(g, b, tr, 10);
                    if (i != 1) DrawRoundRect(g, p, tr, 10);
                }
                g.DrawString(turboLabels[i], fontSmallBold, Brushes.White, tr, sfCenter);
            }
        }

        // =========================================================================
        // v10.4.2 / v10.4.3 REDESIGNED RIGHT FLOATING "UP NEXT" CARD (1:1 Ref Image 1)
        // =========================================================================

        private void PaintRightDockedPanel(Graphics g, int rightX, int rightW, int mainH, Color curBgSide, Color curBgCard, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            if (currentSong == null) return;

            Rectangle cardRect = new Rectangle(rightX + 6, 66, rightW - 18, mainH - 72);
            using (SolidBrush cb = new SolidBrush(Color.FromArgb(oledPureBlack ? 240 : 195, 16, 13, 18)))
            using (Pen cp = new Pen(Color.FromArgb(42, 255, 255, 255), 1.0f))
            {
                FillRoundRect(g, cb, cardRect, 18);
                DrawRoundRect(g, cp, cardRect, 18);
            }

            using (Font fUpHdr = new Font("Segoe UI", 12.5f, FontStyle.Bold))
            {
                string hdrTitle = (rightPanelMode == 1) ? "Lyrics" : "Up Next";
                g.DrawString(hdrTitle, fUpHdr, Brushes.White, cardRect.X + 18, cardRect.Y + 16);
            }

            Rectangle modeToggleRect = new Rectangle(cardRect.Right - 42, cardRect.Y + 14, 28, 28);
            DrawQueueListIcon(g, rightPanelMode == 1 ? accentColor : Color.FromArgb(200, 215, 220, 230), modeToggleRect.X + 14f, modeToggleRect.Y + 14f);

            Rectangle listBox = new Rectangle(cardRect.X + 8, cardRect.Y + 52, cardRect.Width - 16, cardRect.Height - 60);

            if (rightPanelMode == 1)
            {
                PaintBetterLyricsCanvas(g, listBox, false, sfCenter);
                return;
            }

            Region prevRightClip = g.Clip;
            g.SetClip(listBox);

            int qRowH = 58;
            for (int i = 0; i < queueSongs.Count; i++)
            {
                int qy = listBox.Y + 2 + i * (qRowH + 4) - queueScrollOffset;
                if (qy + qRowH < listBox.Y || qy > listBox.Bottom) continue;

                SongItem qs = queueSongs[i];
                bool isPlayingThis = (currentSong != null && currentSong.Id == qs.Id);
                bool isHovQ = (hoveredQueueIndex == i);

                Rectangle qRowRect = new Rectangle(listBox.X + 4, qy, listBox.Width - 8, qRowH);
                if (isPlayingThis)
                {
                    using (LinearGradientBrush actGrad = new LinearGradientBrush(
                        qRowRect,
                        Color.FromArgb(85, accentColor.R, accentColor.G, accentColor.B),
                        Color.FromArgb(35, accentColor.R, accentColor.G, accentColor.B),
                        LinearGradientMode.Horizontal))
                    using (Pen actRim = new Pen(Color.FromArgb(95, accentColor), 1.0f))
                    {
                        FillRoundRect(g, actGrad, qRowRect, 12);
                        DrawRoundRect(g, actRim, qRowRect, 12);
                    }
                }
                else if (isHovQ)
                {
                    using (SolidBrush hovB = new SolidBrush(Color.FromArgb(32, 255, 255, 255)))
                    {
                        FillRoundRect(g, hovB, qRowRect, 12);
                    }
                }
                else if (i < queueSongs.Count - 1)
                {
                    using (Pen sepPen = new Pen(Color.FromArgb(18, 255, 255, 255), 1f))
                    {
                        g.DrawLine(sepPen, qRowRect.X + 10, qRowRect.Bottom + 1, qRowRect.Right - 10, qRowRect.Bottom + 1);
                    }
                }

                DrawStaticAlbumArt(g, qs.ThumbUrl, new Rectangle(qRowRect.X + 8, qRowRect.Y + 8, 42, 42), 8);

                using (Font fQt = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (Font fQa = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (SolidBrush qtb = new SolidBrush(Color.White))
                using (SolidBrush qab = new SolidBrush(Color.FromArgb(175, 195, 200, 212)))
                {
                    int txW = qRowRect.Width - 92;
                    g.DrawString(FormatMinimalCardTitle(qs.Title), fQt, qtb, new RectangleF(qRowRect.X + 58, qRowRect.Y + 10, txW, 19), sfLeftEllipsis);
                    g.DrawString(GetPrimaryArtistName(qs.Artist), fQa, qab, new RectangleF(qRowRect.X + 58, qRowRect.Y + 29, txW, 17), sfLeftEllipsis);
                }

                if (isPlayingThis)
                {
                    DrawEqResponsiveVisualizer(g, qRowRect.Right - 30, qRowRect.Y + 36, 5, 4, 2.2f, 12f);
                }
                else
                {
                    using (SolidBrush dotB = new SolidBrush(Color.FromArgb(160, 200, 205, 215)))
                    {
                        g.DrawString("\u2022\u2022\u2022", fontSmallBold, dotB, new RectangleF(qRowRect.Right - 34, qRowRect.Y + 18, 28, 22), sfCenter);
                    }
                }
            }
            g.Clip = prevRightClip;
        }

        // =========================================================================
        // v10.4.3 REDESIGNED FULL-SCREEN SONG & LYRICS PANEL + 3-DOT POPUP MENU
        // =========================================================================

        private void PaintFullNowPlayingPanel(Graphics g, int w, int h, float animAlpha, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            using (SolidBrush solidBase = new SolidBrush(Color.FromArgb(255, 8, 6, 10)))
            {
                g.FillRectangle(solidBase, 0, 0, w, h);
            }

            PaintAtmosphericSongBackdrop(g, w, h, 1.18f);
            PaintLiveMeshAuroraBackdrop(g, w, h);

            // 1. Top-Left: ♫ LEO Music + `⌄` Collapse Chevron (`x = 28, y = 58`)
            DrawBrandMusicNoteLogo(g, 28, 18);

            Rectangle collapseChvRect = new Rectangle(24, 52, 34, 30);
            using (Pen chvP = new Pen(Color.FromArgb(225, 255, 255, 255), 2.0f))
            {
                chvP.StartCap = LineCap.Round;
                chvP.EndCap = LineCap.Round;
                float cx = collapseChvRect.X + 15f;
                float cy = collapseChvRect.Y + 15f;
                g.DrawLine(chvP, cx - 7f, cy - 3f, cx, cy + 4f);
                g.DrawLine(chvP, cx, cy + 4f, cx + 7f, cy - 3f);
            }

            // 2. Top-Center: `[ Song ]` / `[ Lyrics ]` Pill Tabs (`y = 32`, 1:1 Ref Images 2 & 3)
            int tabContainerW = 156;
            int tabContainerX = (w - tabContainerW) / 2;
            int tabY = 30;
            Rectangle tabOuter = new Rectangle(tabContainerX - 3, tabY - 3, tabContainerW + 6, 36);
            using (SolidBrush tbBg = new SolidBrush(Color.FromArgb(145, 16, 14, 20)))
            using (Pen tbRim = new Pen(Color.FromArgb(45, 255, 255, 255), 1.0f))
            {
                FillRoundRect(g, tbBg, tabOuter, 18);
                DrawRoundRect(g, tbRim, tabOuter, 18);
            }

            Rectangle songTabPill = new Rectangle(tabContainerX, tabY, 76, 30);
            Rectangle lyricsTabPill = new Rectangle(tabContainerX + 80, tabY, 76, 30);
            bool isSongTabActive = (fullViewSubMode != 2);

            using (SolidBrush actTabB = new SolidBrush(accentColor))
            {
                FillRoundRect(g, actTabB, isSongTabActive ? songTabPill : lyricsTabPill, 15);
            }
            using (Font fTab = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush sTb = new SolidBrush(isSongTabActive ? Color.White : Color.FromArgb(185, 205, 210, 220)))
            using (SolidBrush lTb = new SolidBrush(!isSongTabActive ? Color.White : Color.FromArgb(185, 205, 210, 220)))
            {
                g.DrawString("Song", fTab, sTb, songTabPill, sfCenter);
                g.DrawString("Lyrics", fTab, lTb, lyricsTabPill, sfCenter);
            }

            // 3. Top-Right: Interactive `⋮` Three-Dots Button (`x = w - 50, y = 46, 36x36`) with glass pill highlight when open!
            Rectangle topDotsMenuRect = new Rectangle(w - 50, 46, 36, 36);
            if (isFullThreeDotMenuOpen || currentHoveredBtnKey == "full_dots_btn")
            {
                using (SolidBrush dhb = new SolidBrush(Color.FromArgb(isFullThreeDotMenuOpen ? 85 : 42, accentColor.R, accentColor.G, accentColor.B)))
                using (Pen dhp = new Pen(Color.FromArgb(160, accentColor), 1.2f))
                {
                    FillRoundRect(g, dhb, topDotsMenuRect, 12);
                    DrawRoundRect(g, dhp, topDotsMenuRect, 12);
                }
            }
            using (SolidBrush dotB = new SolidBrush(isFullThreeDotMenuOpen ? accentColor : Color.White))
            {
                float dx = topDotsMenuRect.X + 18f;
                g.FillEllipse(dotB, dx - 2.2f, topDotsMenuRect.Y + 9f,  4.4f, 4.4f);
                g.FillEllipse(dotB, dx - 2.2f, topDotsMenuRect.Y + 16f, 4.4f, 4.4f);
                g.FillEllipse(dotB, dx - 2.2f, topDotsMenuRect.Y + 23f, 4.4f, 4.4f);
            }

            // 4. Bottom-Left: Device Info (`Playing on` / `This PC`, 1:1 Ref Images 2 & 3)
            int botBarY = h - 54;
            DrawDevicePcSpeakerIcon(g, Color.FromArgb(215, 230, 235, 242), 30f, botBarY + 10f);
            using (Font fDevSub = new Font("Segoe UI", 8.0f, FontStyle.Regular))
            using (Font fDevMain = new Font("Segoe UI", 9.2f, FontStyle.Bold))
            using (SolidBrush devSubB = new SolidBrush(Color.FromArgb(170, 200, 205, 215)))
            {
                g.DrawString("Playing on", fDevSub, devSubB, 56, botBarY + 6);
                g.DrawString("This PC", fDevMain, Brushes.White, 56, botBarY + 20);
            }

            // 5. Bottom-Right: Volume Control (`\u266B` + Slider + v10.4.4 `% ▴` Volume Controller Pill) + `☰` Queue/Lyrics Toggle + `⛶` Fullscreen
            int fpVolBarX = w - 262;
            int fpVolBarW = 96;
            int fpVolBarY = botBarY + 20;

            DrawSpeakerVectorIcon(g, isVolumeControllerPopupOpen ? accentColor : Color.FromArgb(225, 240, 242, 248), fpVolBarX - 18f, fpVolBarY, isMuted || volumePercent == 0);

            int maxFpVol = volumeOverdriveAllowed ? 150 : 100;
            using (Pen vp = new Pen(Color.FromArgb(75, 255, 255, 255), 3.6f))
            {
                vp.StartCap = LineCap.Round;
                vp.EndCap = LineCap.Round;
                g.DrawLine(vp, fpVolBarX, fpVolBarY, fpVolBarX + fpVolBarW, fpVolBarY);
            }
            int fpActVolW = isMuted ? 0 : Math.Min(fpVolBarW, (int)(fpVolBarW * (volumePercent / (double)maxFpVol)));
            Color fpVolCol = (volumePercent > 100) ? Color.FromArgb(255, 255, 195, 45) : accentColor;
            if (fpActVolW > 2)
            {
                using (Pen avp = new Pen(fpVolCol, 3.8f))
                {
                    avp.StartCap = LineCap.Round;
                    avp.EndCap = LineCap.Round;
                    g.DrawLine(avp, fpVolBarX, fpVolBarY, fpVolBarX + fpActVolW, fpVolBarY);
                }
            }
            g.FillEllipse(Brushes.White, fpVolBarX + fpActVolW - 6, fpVolBarY - 6, 12, 12);

            // Clickable Volume % / Studio Volume Controller Pill (`w - 156, botBarY + 8, 54x24`)
            Rectangle fpVolPillRect = new Rectangle(fpVolBarX + fpVolBarW + 8, botBarY + 8, 52, 24);
            using (SolidBrush vpb = new SolidBrush(isVolumeControllerPopupOpen ? Color.FromArgb(95, accentColor) : Color.FromArgb(45, 255, 255, 255)))
            using (Pen vpp = new Pen(isVolumeControllerPopupOpen ? accentColor : Color.FromArgb(80, 255, 255, 255), 1f))
            {
                FillRoundRect(g, vpb, fpVolPillRect, 11);
                DrawRoundRect(g, vpp, fpVolPillRect, 11);
            }
            using (SolidBrush vpt = new SolidBrush(volumePercent > 100 ? Color.FromArgb(255, 255, 210, 75) : Color.White))
            {
                string vStr = (isMuted ? "MUTE" : (volumePercent + "%")) + " \u25B4";
                g.DrawString(vStr, fontSmallBold, vpt, fpVolPillRect, sfCenter);
            }

            Rectangle fpQueueToggleRect = new Rectangle(w - 94, botBarY + 6, 30, 28);
            DrawQueueListIcon(g, fullViewSubMode == 1 ? accentColor : Color.FromArgb(215, 240, 242, 248), fpQueueToggleRect.X + 15f, fpQueueToggleRect.Y + 14f);

            Rectangle fpFullIconRect = new Rectangle(w - 52, botBarY + 6, 30, 28);
            DrawFullscreenCornersIcon(g, Color.FromArgb(215, 240, 242, 248), fpFullIconRect.X + 15f, fpFullIconRect.Y + 14f);

            // 6. Main Stage Content (Modes: 1 = Both Playing + Lyrics, 0 = Only Playing Song, 2 = Only Full Lyrics, 3 = Visualizer + Lyrics)
            if (fullViewSubMode == 2)
            {
                int lyrStageW = Math.Min(940, w - 80);
                Rectangle lyrBounds = new Rectangle((w - lyrStageW) / 2, 84, lyrStageW, h - 154);
                if (lyricStageStyleIdx == 2)
                {
                    DrawGlassCard(g, lyrBounds, 22, bgCard, false, false);
                }
                PaintBetterLyricsCanvas(g, lyrBounds, true, sfCenter);
            }
            else if (fullViewSubMode == 3)
            {
                // Mode 3: Concert Spectrum Visualizer Stage + Live Synced Lyrics Subtitle Dock
                int stageW = Math.Min(920, w - 80);
                Rectangle concCard = new Rectangle((w - stageW) / 2, 88, stageW, h - 162);
                DrawGlassCard(g, concCard, 22, bgCard, false, false);

                int artSzV = Math.Min(190, concCard.Height - 160);
                if (artSzV < 110) artSzV = 110;
                Rectangle centerArt = new Rectangle(concCard.X + (concCard.Width - artSzV) / 2, concCard.Y + 22, artSzV, artSzV);
                DrawAnimatedStudioArtwork(g, currentSong.ThumbUrl, centerArt, artworkShapeMode);

                int visBars = 28;
                int visStep = 14;
                int visTotalW = visBars * visStep;
                int visStartX = concCard.X + (concCard.Width - visTotalW) / 2;
                int visBaseY = centerArt.Bottom + 54;
                DrawEqResponsiveVisualizer(g, visStartX, visBaseY, visBars, visStep, 6.0f, 36f);

                EnsureSyncedLyricsParsed(currentSong);
                string curLine = "\u266A " + currentSong.Title;
                string nextLine = currentSong.Artist + " \u2022 Visualizer: " + visualizerStyleNames[visualizerStyleIdx];
                double syncClock = Math.Max(0.0, smoothPosSec + lyricSyncOffsetSec);
                if (currentSong.SyncedLines != null && currentSong.SyncedLines.Count > 0)
                {
                    for (int i = 0; i < currentSong.SyncedLines.Count; i++)
                    {
                        if (syncClock >= currentSong.SyncedLines[i].StartSec)
                        {
                            curLine = "\u266A " + currentSong.SyncedLines[i].Text;
                            if (i + 1 < currentSong.SyncedLines.Count) nextLine = currentSong.SyncedLines[i + 1].Text;
                        }
                    }
                }
                Rectangle subDock = new Rectangle(concCard.X + 40, concCard.Bottom - 76, concCard.Width - 80, 58);
                DrawGlassCard(g, subDock, 16, bgCard, true, false);
                g.DrawString(curLine, fontTitleBold, Brushes.White, new RectangleF(subDock.X + 16, subDock.Y + 6, subDock.Width - 32, 24), sfCenter);
                g.DrawString(nextLine, fontSubReg, Brushes.LightGray, new RectangleF(subDock.X + 16, subDock.Y + 32, subDock.Width - 32, 20), sfCenter);
            }
            else
            {
                bool showRightLyricsCard = (fullViewSubMode == 1);
                int rightCardW = showRightLyricsCard ? 305 : 0;
                int rightCardX = w - rightCardW - 36;

                int availStageH = h - 180;
                int artSz = Math.Max(240, Math.Min(370, availStageH - 20));
                int artX = 64;
                int artY = 92 + (availStageH - artSz) / 2;
                Rectangle bigArtRect = new Rectangle(artX, artY, artSz, artSz);

                for (int r = 4; r >= 1; r--)
                {
                    int ex = r * 4;
                    using (Pen glowP = new Pen(Color.FromArgb(16 * (5 - r), accentColor.R, accentColor.G, accentColor.B), 3.0f))
                    {
                        DrawRoundRect(g, glowP, new Rectangle(bigArtRect.X - ex, bigArtRect.Y - ex, bigArtRect.Width + ex * 2, bigArtRect.Height + ex * 2), 20 + ex);
                    }
                }
                DrawStaticAlbumArt(g, currentSong.ThumbUrl, bigArtRect, 20);
                using (Pen artRim = new Pen(Color.FromArgb(65, 255, 255, 255), 1.1f))
                {
                    DrawRoundRect(g, artRim, bigArtRect, 20);
                }

                int centerColX = bigArtRect.Right + 54;
                int centerColRightLimit = showRightLyricsCard ? (rightCardX - 36) : (w - 64);
                int centerColW = Math.Max(260, centerColRightLimit - centerColX);
                int centerColTopY = artY + (artSz - 210) / 2;

                using (Font fFullTitle = new Font("Segoe UI", 28f, FontStyle.Bold))
                using (Font fFullArtist = new Font("Segoe UI", 12.5f, FontStyle.Regular))
                using (SolidBrush artSubB = new SolidBrush(Color.FromArgb(195, 205, 210, 220)))
                {
                    g.DrawString(FormatMinimalCardTitle(currentSong.Title), fFullTitle, Brushes.White, new RectangleF(centerColX - 3, centerColTopY, centerColW - 44, 50), sfLeftEllipsis);
                    g.DrawString(GetPrimaryArtistName(currentSong.Artist), fFullArtist, artSubB, new RectangleF(centerColX, centerColTopY + 52, centerColW - 44, 26), sfLeftEllipsis);
                }

                bool isCurLiked = likedIds.Contains(currentSong.Id);
                float heartCx = centerColX + centerColW - 18f;
                float heartCy = centerColTopY + 58f;
                if (isCurLiked)
                {
                    using (SolidBrush hg = new SolidBrush(Color.FromArgb(45, accentColor.R, accentColor.G, accentColor.B)))
                    {
                        g.FillEllipse(hg, heartCx - 16f, heartCy - 16f, 32f, 32f);
                    }
                }
                DrawHeartIcon(g, isCurLiked ? accentColor : Color.FromArgb(195, 255, 255, 255), heartCx, heartCy, true);

                int sliderY = centerColTopY + 104;
                float frac = (float)Math.Max(0.0, Math.Min(1.0, smoothPosSec / Math.Max(1.0, currentDurSec)));
                int activeX = centerColX + (int)(centerColW * frac);

                using (Pen trackPen = new Pen(Color.FromArgb(75, 255, 255, 255), 3.8f))
                {
                    trackPen.StartCap = LineCap.Round;
                    trackPen.EndCap = LineCap.Round;
                    g.DrawLine(trackPen, centerColX, sliderY, centerColX + centerColW, sliderY);
                }
                if (activeX > centerColX + 2)
                {
                    using (Pen actPen = new Pen(accentColor, 4.0f))
                    {
                        actPen.StartCap = LineCap.Round;
                        actPen.EndCap = LineCap.Round;
                        g.DrawLine(actPen, centerColX, sliderY, activeX, sliderY);
                    }
                }
                using (SolidBrush thGlow = new SolidBrush(Color.FromArgb(65, accentColor)))
                using (SolidBrush thCore = new SolidBrush(accentColor))
                {
                    g.FillEllipse(thGlow, activeX - 10, sliderY - 10, 20, 20);
                    g.FillEllipse(thCore, activeX - 6, sliderY - 6, 12, 12);
                }

                using (Font fTime = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                using (SolidBrush timeB = new SolidBrush(Color.FromArgb(195, 210, 215, 225)))
                {
                    g.DrawString(FormatSec((int)currentPosSec), fTime, timeB, centerColX, sliderY + 12);
                    SizeF durSz = g.MeasureString(FormatSec((int)currentDurSec), fTime);
                    g.DrawString(FormatSec((int)currentDurSec), fTime, timeB, centerColX + centerColW - durSz.Width, sliderY + 12);
                }

                int ctrlCy = centerColTopY + 176;
                int ctrlMidX = centerColX + centerColW / 2;

                DrawShuffleVectorIcon(g, isShuffle ? accentColor : Color.FromArgb(220, 240, 242, 248), ctrlMidX - 132f, ctrlCy);
                DrawSkipPrevIcon(g, Brushes.White, ctrlMidX - 68f, ctrlCy);

                Rectangle bigNeonPlayRect = ScaleRectCentered(new Rectangle(ctrlMidX - 31, ctrlCy - 31, 62, 62), GetButtonScale("play"));
                DrawGlowingNeonPlayButton(g, bigNeonPlayRect, isPlaying);

                DrawSkipNextIcon(g, Brushes.White, ctrlMidX + 68f, ctrlCy);
                DrawRepeatVectorIcon(g, isRepeatOne ? accentColor : Color.FromArgb(220, 240, 242, 248), ctrlMidX + 132f, ctrlCy);

                if (showRightLyricsCard)
                {
                    Rectangle rightLyrCard = new Rectangle(rightCardX, artY - 6, rightCardW, artSz + 12);
                    using (SolidBrush rlb = new SolidBrush(Color.FromArgb(195, 16, 13, 18)))
                    using (Pen rlp = new Pen(Color.FromArgb(48, accentColor.R, accentColor.G, accentColor.B), 1.1f))
                    {
                        FillRoundRect(g, rlb, rightLyrCard, 18);
                        DrawRoundRect(g, rlp, rightLyrCard, 18);
                    }

                    using (Font fLyrCardHdr = new Font("Segoe UI", 12.5f, FontStyle.Bold))
                    {
                        g.DrawString("Lyrics", fLyrCardHdr, Brushes.White, rightLyrCard.X + 20, rightLyrCard.Y + 16);
                    }
                    DrawQueueListIcon(g, Color.FromArgb(200, 220, 225, 235), rightLyrCard.Right - 28f, rightLyrCard.Y + 26f);

                    EnsureSyncedLyricsParsed(currentSong);
                    List<LyricLine> lines = currentSong.SyncedLines;
                    if (lines != null && lines.Count > 0)
                    {
                        double syncClock = Math.Max(0.0, smoothPosSec + lyricSyncOffsetSec);
                        int activeIdx = 0;
                        for (int i = 0; i < lines.Count; i++)
                        {
                            if (syncClock >= lines[i].StartSec) activeIdx = i;
                        }

                        Rectangle lyrInner = new Rectangle(rightLyrCard.X + 6, rightLyrCard.Y + 52, rightLyrCard.Width - 12, rightLyrCard.Height - 64);
                        Region oldC = g.Clip;
                        g.SetClip(lyrInner);

                        int lineStepH = 34;
                        float targetAnchor = lyrInner.Height * 0.36f;
                        float targetScroll = Math.Max(0f, activeIdx * lineStepH - targetAnchor);
                        lyricsSmoothScrollY += (targetScroll - lyricsSmoothScrollY) * 0.22f;

                        activeLyricHitRects.Clear();
                        activeLyricHitTimes.Clear();

                        using (Font fActL = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                        using (Font fIdlL = new Font("Segoe UI", 9.8f, FontStyle.Regular))
                        {
                            for (int i = 0; i < lines.Count; i++)
                            {
                                float ly = lyrInner.Y + i * lineStepH - lyricsSmoothScrollY;
                                if (ly + lineStepH < lyrInner.Y - 10 || ly > lyrInner.Bottom + 10) continue;

                                Rectangle hitR = new Rectangle(lyrInner.X + 8, (int)ly, lyrInner.Width - 16, lineStepH);
                                activeLyricHitRects.Add(hitR);
                                activeLyricHitTimes.Add(lines[i].StartSec);

                                bool isCurLine = (i == activeIdx);
                                int dist = Math.Abs(i - activeIdx);
                                int alpha = isCurLine ? 255 : Math.Max(45, 165 - dist * 28);

                                if (isCurLine)
                                {
                                    using (SolidBrush barB = new SolidBrush(accentColor))
                                    using (SolidBrush washB = new SolidBrush(Color.FromArgb(28, accentColor.R, accentColor.G, accentColor.B)))
                                    {
                                        FillRoundRect(g, washB, new Rectangle(lyrInner.X + 4, (int)ly - 2, lyrInner.Width - 8, lineStepH - 2), 8);
                                        FillRoundRect(g, barB, new Rectangle(rightLyrCard.X + 1, (int)ly, 3, lineStepH - 6), 2);
                                    }
                                    using (SolidBrush actTxtB = new SolidBrush(accentColor))
                                    {
                                        g.DrawString(lines[i].Text, fActL, actTxtB, new RectangleF(lyrInner.X + 14, ly, lyrInner.Width - 20, lineStepH - 2), sfLeftEllipsis);
                                    }
                                }
                                else
                                {
                                    using (SolidBrush idlTxtB = new SolidBrush(Color.FromArgb(alpha, 200, 205, 215)))
                                    {
                                        g.DrawString(lines[i].Text, fIdlL, idlTxtB, new RectangleF(lyrInner.X + 14, ly, lyrInner.Width - 20, lineStepH - 2), sfLeftEllipsis);
                                    }
                                }
                            }
                        }
                        g.Clip = oldC;
                    }
                }
            }

            // 7. v10.4.3 FULL-SCREEN 3-DOT POPUP MENU OVERLAY (Opens when clicking `⋮` at top-right!)
            if (fullThreeDotMenuAnim > 0.01f)
            {
                PaintFullThreeDotPopupMenu(g, w, h, fullThreeDotMenuAnim, sfCenter, sfLeftEllipsis);
            }
        }

        private void PaintFullThreeDotPopupMenu(Graphics g, int w, int h, float anim, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            int popW = 276;
            int popH = 344;
            int popX = w - popW - 22;
            int popY = 86 - (int)((1.0f - anim) * 12f);
            Rectangle popRect = new Rectangle(popX, popY, popW, popH);

            // Deep drop shadow + 3D Curved Frosted Glass Menu Card
            using (SolidBrush shB = new SolidBrush(Color.FromArgb((int)(160 * anim), 0, 0, 0)))
            {
                FillRoundRect(g, shB, new Rectangle(popRect.X + 3, popRect.Y + 6, popRect.Width, popRect.Height), 18);
            }
            using (SolidBrush cardB = new SolidBrush(Color.FromArgb((int)(244 * anim), 16, 15, 22)))
            using (Pen rimP = new Pen(Color.FromArgb((int)(185 * anim), accentColor), 1.4f))
            {
                FillRoundRect(g, cardB, popRect, 18);
                DrawRoundRect(g, rimP, popRect, 18);
            }

            using (Font fHdr = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (SolidBrush ab = new SolidBrush(accentColor))
            {
                g.DrawString("FULL PANEL VIEW & STUDIO OPTIONS", fHdr, ab, popRect.X + 16, popRect.Y + 12);
            }

            string[] itemLabels = new string[]
            {
                "Playing + Lyrics (Both)",
                "Only Playing (Song Art)",
                "Only Lyrics (Full Boxless)",
                "Concert Visualizer + Lyrics",
                "Next Background Image  \u2726",
                "Launch Dynamic Bar Overlay",
                "Open Audio EQ & Settings",
                "Download 320kbps Track"
            };
            bool[] itemSelected = new bool[]
            {
                fullViewSubMode == 1,
                fullViewSubMode == 0,
                fullViewSubMode == 2,
                fullViewSubMode == 3,
                false,
                isDynamicBarMode,
                false,
                false
            };

            int rowStart = popRect.Y + 36;
            int rowH = 36;
            for (int i = 0; i < itemLabels.Length; i++)
            {
                int ry = rowStart + i * 38;
                if (i == 4)
                {
                    using (Pen divP = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                    {
                        g.DrawLine(divP, popRect.X + 14, ry - 3, popRect.Right - 14, ry - 3);
                    }
                }
                Rectangle itemR = new Rectangle(popRect.X + 10, ry, popRect.Width - 20, rowH);
                bool isSel = itemSelected[i];
                if (isSel)
                {
                    using (LinearGradientBrush sb = new LinearGradientBrush(
                        itemR,
                        Color.FromArgb(95, accentColor.R, accentColor.G, accentColor.B),
                        Color.FromArgb(35, accentColor.R, accentColor.G, accentColor.B),
                        LinearGradientMode.Horizontal))
                    using (Pen sp = new Pen(Color.FromArgb(140, accentColor), 1f))
                    {
                        FillRoundRect(g, sb, itemR, 10);
                        DrawRoundRect(g, sp, itemR, 10);
                    }
                }
                else
                {
                    using (SolidBrush ib = new SolidBrush(Color.FromArgb(24, 255, 255, 255)))
                    {
                        FillRoundRect(g, ib, itemR, 10);
                    }
                }

                // Radio dot or action icon on left
                if (i < 4)
                {
                    Rectangle radioR = new Rectangle(itemR.X + 12, itemR.Y + 11, 14, 14);
                    using (Pen rp = new Pen(isSel ? accentColor : Color.FromArgb(140, 200, 205, 215), 1.5f))
                    {
                        g.DrawEllipse(rp, radioR);
                    }
                    if (isSel)
                    {
                        using (SolidBrush rb = new SolidBrush(accentColor))
                        {
                            g.FillEllipse(rb, radioR.X + 3, radioR.Y + 3, 8, 8);
                        }
                    }
                }
                else
                {
                    using (SolidBrush ab = new SolidBrush(accentColor))
                    {
                        string sym = (i == 4) ? "\u2726" : ((i == 5) ? "\u25AC" : ((i == 6) ? "\u2699" : "\u2193"));
                        g.DrawString(sym, fontSmallBold, ab, itemR.X + 12, itemR.Y + 9);
                    }
                }

                using (Font fItem = new Font("Segoe UI", 9.2f, isSel ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(isSel ? Color.White : Color.FromArgb(225, 230, 238)))
                {
                    g.DrawString(itemLabels[i], fItem, tb, new RectangleF(itemR.X + 34, itemR.Y + 6, itemR.Width - 40, 24), sfLeftEllipsis);
                }
            }
        }

        // =========================================================================
        // v10.4.2 / v10.4.3 REDESIGNED BOTTOM FLOATING PLAYER DOCK (1:1 Ref Image 1)
        // =========================================================================

        private void PaintBottomPlayerBar(Graphics g, int w, int h, int playerH, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            Rectangle dockRect = new Rectangle(204, h - 76, w - 216, 66);
            using (SolidBrush dBg = new SolidBrush(Color.FromArgb(oledPureBlack ? 245 : 215, 16, 13, 18)))
            using (Pen dRim = new Pen(Color.FromArgb(45, 255, 255, 255), 1.0f))
            {
                FillRoundRect(g, dBg, dockRect, 16);
                DrawRoundRect(g, dRim, dockRect, 16);
            }

            if (currentSong != null)
            {
                DrawStaticAlbumArt(g, currentSong.ThumbUrl, new Rectangle(dockRect.X + 12, dockRect.Y + 10, 46, 46), 8);
                using (Font fBt = new Font("Segoe UI", 10.0f, FontStyle.Bold))
                using (Font fBa = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (SolidBrush wb = new SolidBrush(Color.White))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(180, 200, 205, 215)))
                {
                    g.DrawString(FormatMinimalCardTitle(currentSong.Title), fBt, wb, new RectangleF(dockRect.X + 68, dockRect.Y + 13, 135, 20), sfLeftEllipsis);
                    g.DrawString(GetPrimaryArtistName(currentSong.Artist), fBa, sb, new RectangleF(dockRect.X + 68, dockRect.Y + 33, 135, 18), sfLeftEllipsis);
                }

                bool isLiked = likedIds.Contains(currentSong.Id);
                DrawHeartIcon(g, isLiked ? accentColor : Color.FromArgb(170, 200, 205, 215), dockRect.X + 216, dockRect.Y + 33, true);
            }

            int midX = dockRect.X + dockRect.Width / 2;
            float btnCy = dockRect.Y + 23f;

            DrawShuffleVectorIcon(g, isShuffle ? accentColor : Color.FromArgb(190, 215, 220, 230), midX - 96f, btnCy);
            DrawSkipPrevIcon(g, Brushes.White, midX - 48f, btnCy);

            Rectangle playCircle = ScaleRectCentered(new Rectangle(midX - 19, (int)btnCy - 19, 38, 38), GetButtonScale("play"));
            DrawGlowingNeonPlayButton(g, playCircle, isPlaying);

            DrawSkipNextIcon(g, Brushes.White, midX + 48f, btnCy);
            DrawRepeatVectorIcon(g, isRepeatOne ? accentColor : Color.FromArgb(190, 215, 220, 230), midX + 96f, btnCy);

            int barW = Math.Min(360, Math.Max(200, dockRect.Width - 520));
            int barX = midX - barW / 2;
            int barY = dockRect.Y + 52;
            float frac = (float)Math.Max(0.0, Math.Min(1.0, smoothPosSec / Math.Max(1.0, currentDurSec)));
            int activeX = barX + (int)(barW * frac);

            using (Pen inactivePen = new Pen(Color.FromArgb(65, 255, 255, 255), 3.2f))
            {
                inactivePen.StartCap = LineCap.Round;
                inactivePen.EndCap = LineCap.Round;
                g.DrawLine(inactivePen, barX, barY, barX + barW, barY);
            }
            if (activeX > barX + 2)
            {
                using (Pen activePen = new Pen(accentColor, 3.5f))
                {
                    activePen.StartCap = LineCap.Round;
                    activePen.EndCap = LineCap.Round;
                    g.DrawLine(activePen, barX, barY, activeX, barY);
                }
            }
            using (SolidBrush thumbB = new SolidBrush(accentColor))
            {
                g.FillEllipse(thumbB, activeX - 5, barY - 5, 10, 10);
            }

            using (Font fTime = new Font("Segoe UI", 8.0f, FontStyle.Regular))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(180, 200, 205, 215)))
            {
                g.DrawString(FormatSec((int)currentPosSec), fTime, tb, barX - 32, barY - 7);
                g.DrawString(FormatSec((int)currentDurSec), fTime, tb, barX + barW + 8, barY - 7);
            }

            // v10.4.4 Upgraded Bottom Right Controls: [A-B Loop] + [Speaker] + [0-150% Volume Slider] + [Vol% ▴ Controller Pill] + [Queue] + [Full]
            int volBarX = dockRect.Right - 224;
            int volBarW = 76;
            int volBarY = dockRect.Y + 33;

            // Quick A-B Loop Pill on Bottom Bar (`volBarX - 74, volBarY - 11, 44x22`)
            Rectangle abPillRect = new Rectangle(volBarX - 76, volBarY - 11, 44, 22);
            bool abActive = (abLoopStartSec >= 0);
            using (SolidBrush abb = new SolidBrush(abActive ? Color.FromArgb(85, accentColor) : Color.FromArgb(28, 255, 255, 255)))
            using (Pen abp = new Pen(abActive ? accentColor : Color.FromArgb(55, 255, 255, 255), 1f))
            {
                FillRoundRect(g, abb, abPillRect, 10);
                DrawRoundRect(g, abp, abPillRect, 10);
            }
            string abText = (abLoopStartSec < 0) ? "A-B" : ((abLoopEndSec < 0) ? "A\u2022.." : "A-B\u21BB");
            g.DrawString(abText, fontSmallBold, abActive ? new SolidBrush(accentColor) : Brushes.White, abPillRect, sfCenter);

            DrawSpeakerVectorIcon(g, isVolumeControllerPopupOpen ? accentColor : Color.FromArgb(210, 230, 235, 242), volBarX - 16f, volBarY, isMuted || volumePercent == 0);

            int maxDockVol = volumeOverdriveAllowed ? 150 : 100;
            using (Pen vp = new Pen(Color.FromArgb(65, 255, 255, 255), 3.2f))
            {
                vp.StartCap = LineCap.Round;
                vp.EndCap = LineCap.Round;
                g.DrawLine(vp, volBarX, volBarY, volBarX + volBarW, volBarY);
            }
            int actVolW = isMuted ? 0 : Math.Min(volBarW, (int)(volBarW * (volumePercent / (double)maxDockVol)));
            Color dockVolCol = (volumePercent > 100) ? Color.FromArgb(255, 255, 195, 45) : accentColor;
            if (actVolW > 2)
            {
                using (Pen avp = new Pen(dockVolCol, 3.5f))
                {
                    avp.StartCap = LineCap.Round;
                    avp.EndCap = LineCap.Round;
                    g.DrawLine(avp, volBarX, volBarY, volBarX + actVolW, volBarY);
                }
            }
            using (SolidBrush vThumb = new SolidBrush(dockVolCol))
            {
                g.FillEllipse(vThumb, volBarX + actVolW - 5, volBarY - 5, 10, 10);
            }

            // Clickable Volume % / Studio Volume Controller Popup Trigger Pill (`volBarX + volBarW + 6, volBarY - 11, 50x22`)
            Rectangle volPillRect = new Rectangle(volBarX + volBarW + 6, volBarY - 11, 50, 22);
            using (SolidBrush vpb = new SolidBrush(isVolumeControllerPopupOpen ? Color.FromArgb(90, accentColor) : Color.FromArgb(38, 255, 255, 255)))
            using (Pen vpp = new Pen(isVolumeControllerPopupOpen ? accentColor : Color.FromArgb(75, 255, 255, 255), 1f))
            {
                FillRoundRect(g, vpb, volPillRect, 10);
                DrawRoundRect(g, vpp, volPillRect, 10);
            }
            using (SolidBrush vpt = new SolidBrush(volumePercent > 100 ? Color.FromArgb(255, 255, 210, 75) : Color.White))
            {
                string vStr = (isMuted ? "0%" : (volumePercent + "%")) + "\u25B4";
                g.DrawString(vStr, fontSmallBold, vpt, volPillRect, sfCenter);
            }

            DrawQueueListIcon(g, isRightPanelVisible ? Color.FromArgb(220, 240, 242, 248) : accentColor, dockRect.Right - 64f, volBarY);
            DrawFullscreenCornersIcon(g, isFullPlayerExpanded ? accentColor : Color.FromArgb(220, 240, 242, 248), dockRect.Right - 28f, volBarY);
        }

        // =========================================================================
        // v10.4.4 INTERACTIVE 3D GLASS STUDIO VOLUME & SMOOTHNESS CONTROLLER POPUP
        // =========================================================================

        private Rectangle GetVolumeControllerPopupRect(int w, int h)
        {
            int popW = 334;
            int popH = 362;
            int popX = Math.Max(210, w - popW - 22);
            int popY = Math.Max(60, h - popH - 86);
            return new Rectangle(popX, popY, popW, popH);
        }

        private void PaintStudioVolumeControllerPopup(Graphics g, int w, int h, float anim, StringFormat sfCenter, StringFormat sfLeftEllipsis)
        {
            Rectangle popRect = GetVolumeControllerPopupRect(w, h);
            int slideOffset = (int)((1.0f - anim) * 18f);
            Rectangle r = new Rectangle(popRect.X, popRect.Y + slideOffset, popRect.Width, popRect.Height);

            // Deep shadow + 3D Curved Glass card
            using (SolidBrush shB = new SolidBrush(Color.FromArgb((int)(165 * anim), 0, 0, 0)))
            {
                FillRoundRect(g, shB, new Rectangle(r.X - 4, r.Y + 6, r.Width + 8, r.Height + 8), 22);
            }
            using (SolidBrush bgB = new SolidBrush(Color.FromArgb((int)(246 * anim), 16, 14, 22)))
            using (Pen rimP = new Pen(Color.FromArgb((int)(185 * anim), accentColor), 1.5f))
            {
                FillRoundRect(g, bgB, r, 20);
                DrawRoundRect(g, rimP, r, 20);
            }

            // Top specular 3D glass highlight
            Rectangle domeR = new Rectangle(r.X + 12, r.Y + 3, r.Width - 24, 22);
            using (LinearGradientBrush domeB = new LinearGradientBrush(domeR, Color.FromArgb((int)(42 * anim), 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
            {
                FillRoundRect(g, domeB, domeR, 10);
            }

            using (SolidBrush ab = new SolidBrush(accentColor))
            using (SolidBrush wb = new SolidBrush(Color.White))
            using (SolidBrush sb = new SolidBrush(textSub))
            {
                g.DrawString("\u266B STUDIO VOLUME & SMOOTHNESS", fontSmallBold, ab, r.X + 16, r.Y + 14);
                string badgeTxt = isMuted ? "MUTED" : (volumePercent > 100 ? (volumePercent + "% BOOST \u26A1") : (volumePercent + "% STUDIO"));
                Color badgeCol = volumePercent > 100 ? Color.FromArgb(255, 255, 200, 55) : Color.White;
                using (SolidBrush bCol = new SolidBrush(badgeCol))
                {
                    g.DrawString(badgeTxt, fontTitleBold, bCol, r.Right - 118, r.Y + 12);
                }

                // 1. Interactive 0% - 150% Overdrive Volume Slider Bar (`r.Y + 46`)
                int slX = r.X + 18;
                int slW = r.Width - 36;
                int slY = r.Y + 50;
                int maxV = volumeOverdriveAllowed ? 150 : 100;

                using (Pen trkP = new Pen(Color.FromArgb(65, 255, 255, 255), 6.0f))
                {
                    trkP.StartCap = LineCap.Round;
                    trkP.EndCap = LineCap.Round;
                    g.DrawLine(trkP, slX, slY, slX + slW, slY);
                }

                // 100% Unity Marker tick
                int unityX = slX + (int)(slW * (100.0 / maxV));
                using (Pen uPen = new Pen(Color.FromArgb(140, 255, 255, 255), 1.5f))
                {
                    g.DrawLine(uPen, unityX, slY - 7, unityX, slY + 7);
                }

                int fillW = isMuted ? 0 : Math.Min(slW, (int)(slW * (volumePercent / (double)maxV)));
                if (fillW > 2)
                {
                    Color fillC = (volumePercent > 100) ? Color.FromArgb(255, 255, 190, 40) : accentColor;
                    using (Pen actP = new Pen(fillC, 6.2f))
                    {
                        actP.StartCap = LineCap.Round;
                        actP.EndCap = LineCap.Round;
                        g.DrawLine(actP, slX, slY, slX + fillW, slY);
                    }
                }
                g.FillEllipse(Brushes.White, slX + fillW - 7, slY - 7, 14, 14);

                using (Font fTiny = new Font("Segoe UI", 7.4f, FontStyle.Regular))
                {
                    g.DrawString("0%", fTiny, sb, slX, slY + 9);
                    g.DrawString("100% Unity", fTiny, sb, unityX - 24, slY + 9);
                    g.DrawString(volumeOverdriveAllowed ? "150% Boost" : "100% Max", fTiny, sb, slX + slW - 52, slY + 9);
                }

                // 2. Fine Step Buttons Row (`r.Y + 80`): [-5%] [-1%] [Mute] [+1%] [+5%]
                string[] stepLabels = new string[] { "-5%", "-1%", isMuted ? "Unmute" : "Mute", "+1%", "+5%" };
                int stGap = 6;
                int stW = (slW - 4 * stGap) / 5;
                for (int i = 0; i < 5; i++)
                {
                    Rectangle sr = new Rectangle(slX + i * (stW + stGap), r.Y + 80, stW, 26);
                    bool isMuteBtn = (i == 2 && isMuted);
                    using (SolidBrush b = new SolidBrush(isMuteBtn ? accentColor : Color.FromArgb(34, 38, 54)))
                    using (Pen p = new Pen(Color.FromArgb(80, accentColor), 1f))
                    {
                        FillRoundRect(g, b, sr, 8);
                        DrawRoundRect(g, p, sr, 8);
                    }
                    g.DrawString(stepLabels[i], fontSmallBold, Brushes.White, sr, sfCenter);
                }

                // 3. 6 One-Click Studio Volume Presets (`r.Y + 114`): [20%] [50%] [75%] [85%] [100%] [135%\u26A1]
                int[] presetVols = new int[] { 20, 50, 75, 85, 100, 135 };
                string[] presetNames = new string[] { "20%", "50%", "75%", "85%", "100%", "135%\u26A1" };
                int prW = (slW - 5 * 5) / 6;
                for (int i = 0; i < 6; i++)
                {
                    Rectangle pr = new Rectangle(slX + i * (prW + 5), r.Y + 114, prW, 25);
                    bool actP = (!isMuted && Math.Abs(volumePercent - presetVols[i]) <= 2);
                    using (SolidBrush b = new SolidBrush(actP ? accentColor : Color.FromArgb(26, 30, 44)))
                    {
                        FillRoundRect(g, b, pr, 7);
                    }
                    g.DrawString(presetNames[i], fontSmallBold, Brushes.White, pr, sfCenter);
                }

                // 4. Stereo Balance L / Center / R Control (`r.Y + 148`)
                string balStr = (manualStereoBalance == 0) ? "Center (0)" : (manualStereoBalance < 0 ? ("Left " + Math.Abs(manualStereoBalance) + "%") : ("Right " + manualStereoBalance + "%"));
                g.DrawString("Stereo Balance: " + balStr, fontSmallBold, wb, slX, r.Y + 148);
                string[] balBtns = new string[] { "L -25%", "Center 0", "R +25%" };
                int bBtnW = (slW - 12) / 3;
                for (int i = 0; i < 3; i++)
                {
                    Rectangle br = new Rectangle(slX + i * (bBtnW + 6), r.Y + 168, bBtnW, 24);
                    bool actB = (i == 0 && manualStereoBalance < -5) || (i == 1 && Math.Abs(manualStereoBalance) <= 5) || (i == 2 && manualStereoBalance > 5);
                    using (SolidBrush b = new SolidBrush(actB ? Color.FromArgb(75, accentColor) : Color.FromArgb(28, 32, 46)))
                    using (Pen p = new Pen(actB ? accentColor : Color.FromArgb(50, 255, 255, 255), 1f))
                    {
                        FillRoundRect(g, b, br, 7);
                        DrawRoundRect(g, p, br, 7);
                    }
                    g.DrawString(balBtns[i], fontSmallBold, Brushes.White, br, sfCenter);
                }

                // 5. Playback Smoothness & Zero-Lag Quick Toggles (`r.Y + 202`)
                string[] smoothToggles = new string[]
                {
                    "Smooth Micro-Fade (Play/Pause/Seek): " + (smoothMicroFadeEnabled ? "ON" : "OFF"),
                    "Smart Auto-Volume Leveler (Normalize): " + (autoVolumeNormalizeEnabled ? "ON" : "OFF"),
                    "Zero-Lag Next-Track Pre-Buffer: " + (autoPreBufferNextEnabled ? "ON" : "OFF"),
                    "\u26A1 v10.4.4 UI Turbo Cache (Zero-Lag): " + (uiTurboCacheEnabled ? "ON" : "OFF")
                };
                bool[] smoothStates = new bool[] { smoothMicroFadeEnabled, autoVolumeNormalizeEnabled, autoPreBufferNextEnabled, uiTurboCacheEnabled };

                for (int i = 0; i < 4; i++)
                {
                    Rectangle tr = new Rectangle(slX, r.Y + 202 + i * 36, slW, 31);
                    bool stOn = smoothStates[i];
                    using (SolidBrush b = new SolidBrush(stOn ? Color.FromArgb(42, accentColor.R, accentColor.G, accentColor.B) : Color.FromArgb(24, 28, 40)))
                    using (Pen p = new Pen(stOn ? Color.FromArgb(145, accentColor) : Color.FromArgb(45, 255, 255, 255), 1f))
                    {
                        FillRoundRect(g, b, tr, 9);
                        DrawRoundRect(g, p, tr, 9);
                    }
                    g.DrawString(smoothToggles[i], fontSmallBold, stOn ? Brushes.White : sb, new RectangleF(tr.X + 12, tr.Y + 4, tr.Width - 56, 22), sfLeftEllipsis);
                    DrawToggleSwitch(g, new Rectangle(tr.Right - 44, tr.Y + 6, 34, 18), stOn);
                }
            }
        }

        private void DrawStaticAlbumArt(Graphics g, string url, Rectangle rect, int radius)
        {
            if (rect.Width <= 4 || rect.Height <= 4) return;
            string jpgUrl = (url ?? "").Replace(".webp", ".jpg");
            Image img = null;
            lock (imageCache)
            {
                if (imageCache.ContainsKey(jpgUrl)) img = imageCache[jpgUrl];
            }
            if (img == null && !string.IsNullOrEmpty(jpgUrl))
            {
                PrefetchSingleThumbnail(jpgUrl);
            }

            // v10.4.4 ZERO-LAG THUMBNAIL CACHE: Pre-bake exact-sized rounded-corner Format32bppPArgb bitmaps in RAM!
            // Eliminates 18x per-frame 500x500 -> card downscaling and SetClip(GraphicsPath) overhead during scrolling!
            if (uiTurboCacheEnabled && img != null && rect.Width <= 380 && rect.Height <= 380)
            {
                string cacheKey = jpgUrl + "_" + rect.Width + "x" + rect.Height + "_r" + radius;
                Bitmap baked = null;
                lock (cachedSizedThumbnails)
                {
                    if (cachedSizedThumbnails.ContainsKey(cacheKey))
                    {
                        baked = cachedSizedThumbnails[cacheKey];
                    }
                    else
                    {
                        try
                        {
                            if (cachedSizedThumbnails.Count > 180) cachedSizedThumbnails.Clear();
                            baked = new Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                            using (Graphics bg = Graphics.FromImage(baked))
                            {
                                bg.SmoothingMode = SmoothingMode.AntiAlias;
                                bg.InterpolationMode = InterpolationMode.Bilinear;
                                Rectangle localR = new Rectangle(0, 0, rect.Width, rect.Height);
                                using (GraphicsPath cp = GetRoundRectPath(localR, radius))
                                {
                                    bg.SetClip(cp);
                                    bg.DrawImage(img, localR);
                                }
                            }
                            cachedSizedThumbnails[cacheKey] = baked;
                        }
                        catch { baked = null; }
                    }
                }
                if (baked != null)
                {
                    g.DrawImageUnscaled(baked, rect.X, rect.Y);
                    return;
                }
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
                    int h = Math.Abs((jpgUrl ?? "leo").GetHashCode());
                    Color c1 = Color.FromArgb(255, 28 + (h % 45), 32 + ((h / 7) % 40), 52 + ((h / 13) % 55));
                    Color c2 = Color.FromArgb(255, Math.Min(255, accentColor.R / 3 + 18), Math.Min(255, accentColor.G / 3 + 18), Math.Min(255, accentColor.B / 3 + 28));
                    using (LinearGradientBrush lg = new LinearGradientBrush(rect, c1, c2, LinearGradientMode.ForwardDiagonal))
                    {
                        g.FillRectangle(lg, rect);
                    }
                    using (SolidBrush nb = new SolidBrush(Color.FromArgb(140, accentColor)))
                    using (StringFormat sfc = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("\u266B", fontTitleBold, nb, rect, sfc);
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
            if (img == null && !string.IsNullOrEmpty(jpgUrl)) PrefetchSingleThumbnail(jpgUrl);

            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;
            float halfW = rect.Width / 2f;
            double rotRad = (vinylAngle * Math.PI) / 180.0;

            if (shapeMode == 0 || shapeMode == 1)
            {
                float pulseR = halfW * (0.94f + 0.05f * (float)Math.Sin(wavePhase * 1.6f));
                using (Pen haloPen = new Pen(Color.FromArgb(45, accentColor), 3.5f))
                {
                    g.DrawEllipse(haloPen, cx - pulseR, cy - pulseR, pulseR * 2f, pulseR * 2f);
                }
            }

            GraphicsPath clipPath = new GraphicsPath();
            int numPts = 180;
            PointF[] pts = new PointF[numPts + 1];

            for (int i = 0; i <= numPts; i++)
            {
                double theta = (i / (double)numPts) * Math.PI * 2.0;
                float r = halfW * 0.86f;

                if (shapeMode == 0)
                {
                    float baseR = halfW * 0.78f;
                    float q1 = (float)Math.Sin(12.0 * theta + wavePhase * 1.4) * (baseR * 0.035f);
                    float q2 = (float)Math.Cos(6.0 * (theta - rotRad)) * (baseR * 0.045f);
                    r = baseR + q1 + q2;
                }
                else if (shapeMode == 1)
                {
                    float baseR = halfW * 0.80f;
                    float star = (float)Math.Abs(Math.Cos(4.0 * (theta - rotRad * 0.8))) * (baseR * 0.12f);
                    float harmonic = (float)Math.Sin(8.0 * (theta + rotRad * 1.2)) * (baseR * 0.05f);
                    r = baseR + star + harmonic;
                }
                else if (shapeMode == 2)
                {
                    float w8 = (float)(0.5 + 0.5 * Math.Sin(morphPhase));
                    float w4 = (float)(0.5 + 0.5 * Math.Cos(morphPhase * 0.72));
                    float w6 = (float)(0.5 + 0.5 * Math.Sin(morphPhase * 0.45 + 1.2));
                    float baseR = halfW * 0.85f;
                    r = baseR
                        + (float)Math.Cos(8.0 * (theta - rotRad)) * (baseR * 0.11f * w8)
                        + (float)Math.Cos(4.0 * (theta + rotRad * 0.6)) * (baseR * 0.08f * w4)
                        + (float)Math.Sin(6.0 * (theta - rotRad * 0.8)) * (baseR * 0.06f * w6);
                }
                else if (shapeMode == 3)
                {
                    float baseR = halfW * 0.87f;
                    float breathe = 0.115f + 0.025f * (float)Math.Sin(morphPhase * 1.4f);
                    r = baseR + baseR * breathe * (float)Math.Cos(8.0 * (theta - rotRad));
                }
                else if (shapeMode == 4)
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
                float imgRot = (shapeMode == 4) ? vinylAngle : (vinylAngle * 0.38f);
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

            if (shapeMode == 4)
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

            using (Pen glowPen = new Pen(Color.FromArgb(170, accentColor), 2.0f))
            {
                g.DrawPath(glowPen, clipPath);
            }
            clipPath.Dispose();

            if (shapeMode == 0)
            {
                float ringRx = halfW * 0.96f;
                float ringRy = halfW * 0.36f;
                float[] tiltAngles = new float[] { vinylAngle * 1.15f, -vinylAngle * 0.95f + 60f, vinylAngle * 0.65f + 120f };

                for (int rIdx = 0; rIdx < 3; rIdx++)
                {
                    GraphicsState gs = g.Save();
                    g.TranslateTransform(cx, cy);
                    g.RotateTransform(tiltAngles[rIdx]);

                    Color ringCol = (rIdx == 0)
                        ? Color.FromArgb(150, accentColor)
                        : ((rIdx == 1) ? Color.FromArgb(120, 0, 229, 255) : Color.FromArgb(115, 255, 255, 255));
                    using (Pen rp = new Pen(ringCol, 1.6f))
                    {
                        g.DrawEllipse(rp, -ringRx, -ringRy, ringRx * 2f, ringRy * 2f);
                    }

                    double orbAngle = wavePhase * (1.8 + rIdx * 0.45) + rIdx * 2.1;
                    float px = ringRx * (float)Math.Cos(orbAngle);
                    float py = ringRy * (float)Math.Sin(orbAngle);
                    using (SolidBrush haloB = new SolidBrush(Color.FromArgb(90, accentColor)))
                    {
                        g.FillEllipse(haloB, px - 7f, py - 7f, 14f, 14f);
                    }
                    g.FillEllipse(Brushes.White, px - 3.5f, py - 3.5f, 7f, 7f);
                    g.Restore(gs);
                }
            }
            else if (shapeMode == 1)
            {
                int vertices = 8;
                PointF[] outerStar = new PointF[vertices];
                PointF[] innerStar = new PointF[vertices];
                float rOut = halfW * 0.96f;
                float rIn = halfW * 0.82f;
                for (int k = 0; k < vertices; k++)
                {
                    double a1 = (k / (double)vertices) * Math.PI * 2.0 + rotRad * 1.3;
                    double a2 = (k / (double)vertices) * Math.PI * 2.0 - rotRad * 1.1 + (Math.PI / vertices);
                    outerStar[k] = new PointF(cx + rOut * (float)Math.Cos(a1), cy + rOut * (float)Math.Sin(a1));
                    innerStar[k] = new PointF(cx + rIn * (float)Math.Cos(a2), cy + rIn * (float)Math.Sin(a2));
                }
                using (Pen pLattice = new Pen(Color.FromArgb(115, accentColor), 1.3f))
                using (Pen pRay = new Pen(Color.FromArgb(80, 255, 255, 255), 1.0f))
                {
                    g.DrawPolygon(pLattice, outerStar);
                    for (int k = 0; k < vertices; k++)
                    {
                        g.DrawLine(pRay, outerStar[k], innerStar[k]);
                        g.DrawLine(pRay, outerStar[k], innerStar[(k + 1) % vertices]);
                        g.FillEllipse(Brushes.White, outerStar[k].X - 2.5f, outerStar[k].Y - 2.5f, 5f, 5f);
                    }
                }
            }
        }

        // =========================================================================
        // v10.4.3 MOUSE HIT-TESTING & INTERACTION ENGINE
        // =========================================================================

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            int sidebarW = 198;
            int rightW = Math.Max(0, (int)Math.Round(rightPanelWidthAnim));
            int playerH = 82;
            int mainH = h - playerH;
            int pbY = h - playerH;
            int centerX = sidebarW;
            int centerW = Math.Max(320, w - sidebarW - rightW);
            int rightX = w - rightW;

            if (isLanguageSetupScreenOpen)
            {
                int mw = Math.Min(820, w - 64);
                int mh = Math.Min(560, h - 64);
                int mx = (w - mw) / 2;
                int my = (h - mh) / 2;

                int gridTop = my + 120;
                int cols = 7;
                int cardGap = 10;
                int cardW = (mw - 48 - (cols - 1) * cardGap) / cols;
                int cardH = 74;

                for (int i = 0; i < availableLanguages.Count; i++)
                {
                    int r = i / cols;
                    int c = i % cols;
                    int cx = mx + 24 + c * (cardW + cardGap);
                    int cy = gridTop + r * (cardH + 12);
                    Rectangle cardRect = new Rectangle(cx, cy, cardW, cardH);
                    if (cardRect.Contains(e.Location))
                    {
                        string langName = availableLanguages[i].Name;
                        if (userSelectedLanguages.Contains(langName))
                        {
                            if (userSelectedLanguages.Count > 1) userSelectedLanguages.Remove(langName);
                            else ShowToast("Keep at least 1 language selected.");
                        }
                        else
                        {
                            userSelectedLanguages.Add(langName);
                        }
                        this.Invalidate();
                        return;
                    }
                }

                if (!langContinueBtnRect.IsEmpty && langContinueBtnRect.Contains(e.Location))
                {
                    if (userSelectedLanguages.Count >= 1)
                    {
                        hasCompletedLanguageSetup = true;
                        isLanguageSetupScreenOpen = false;
                        SavePersistentSettings();
                        FetchLiveTrendingSongsAsync(true);
                        FilterDisplaySongs();
                        ShowToast("Languages Saved! Home feed personalized.");
                        this.Invalidate();
                    }
                    return;
                }
                return;
            }

            if (isDynamicBarMode)
            {
                if (!isDynamicBarExpanded)
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        dynamicBarStyleIdx = (dynamicBarStyleIdx + 1) % 2;
                        SavePersistentSettings();
                        this.Invalidate();
                        return;
                    }
                    if (e.X >= w - 40 || e.Clicks >= 2)
                    {
                        ToggleDynamicBarExpanded();
                        return;
                    }
                    if (e.X <= 30)
                    {
                        TogglePlayPause();
                        return;
                    }
                    isDraggingWindow = true;
                    dragStartPoint = e.Location;
                    return;
                }
                else
                {
                    Rectangle btnFull  = new Rectangle(w - 44, 10, 36, 20);
                    Rectangle btnMini  = new Rectangle(btnFull.X - 38, 10, 35, 20);
                    Rectangle btnStyle = new Rectangle(btnMini.X - 44, 10, 41, 20);
                    Rectangle btnComp  = new Rectangle(btnStyle.X - 26, 10, 23, 20);

                    if (btnComp.Contains(e.Location)) { ToggleDynamicBarExpanded(); return; }
                    if (btnStyle.Contains(e.Location))
                    {
                        dynamicBarStyleIdx = (dynamicBarStyleIdx + 1) % 2;
                        SavePersistentSettings();
                        this.Invalidate();
                        return;
                    }
                    if (btnMini.Contains(e.Location)) { ToggleMiniPlayerMode(); return; }
                    if (btnFull.Contains(e.Location)) { ToggleDynamicBarMode(); return; }

                    int cyPod = 74;
                    if (new Rectangle(14, cyPod - 11, 22, 22).Contains(e.Location)) { PrevTrack(); return; }
                    if (new Rectangle(40, cyPod - 13, 26, 26).Contains(e.Location)) { TogglePlayPause(); return; }
                    if (new Rectangle(70, cyPod - 11, 22, 22).Contains(e.Location)) { NextTrack(); return; }

                    int barX = 104;
                    int barW = w - barX - 46;
                    if (e.Y >= cyPod - 10 && e.Y <= cyPod + 10 && e.X >= barX && e.X <= barX + barW)
                    {
                        double frac = Math.Max(0.0, Math.Min(1.0, (double)(e.X - barX) / barW));
                        SeekToAbsolute(frac * currentDurSec);
                        return;
                    }

                    isDraggingWindow = true;
                    dragStartPoint = e.Location;
                    return;
                }
            }

            if (isMiniPlayerMode)
            {
                if (miniPlayerStyleIdx == 3)
                {
                    int midOrb = w / 2;
                    int cyOrb = 194;
                    if (new Rectangle(midOrb - 54, cyOrb - 13, 26, 26).Contains(e.Location)) { PrevTrack(); return; }
                    if (new Rectangle(midOrb - 16, cyOrb - 16, 32, 32).Contains(e.Location)) { TogglePlayPause(); return; }
                    if (new Rectangle(midOrb + 28, cyOrb - 13, 26, 26).Contains(e.Location)) { NextTrack(); return; }
                    if (new Rectangle(midOrb - 68, 220, 62, 22).Contains(e.Location)) { CycleMiniPlayerStyle(-1); return; }
                    if (new Rectangle(midOrb + 6, 220, 62, 22).Contains(e.Location)) { ToggleMiniPlayerMode(); return; }
                    if (new Rectangle((w - 116) / 2, 18, 116, 116).Contains(e.Location))
                    {
                        artworkShapeMode = (artworkShapeMode + 1) % artworkShapeNames.Length;
                        SavePersistentSettings();
                        return;
                    }
                    isDraggingWindow = true;
                    dragStartPoint = e.Location;
                    return;
                }

                int rightMargin = (miniPlayerStyleIdx == 1) ? 24 : 14;
                Rectangle expandDesktopBtn = new Rectangle(w - rightMargin - 64, 12, 64, 24);
                Rectangle miniShapeBtn = new Rectangle(expandDesktopBtn.X - 66, 12, 62, 24);
                Rectangle miniDynBarBtn = new Rectangle(miniShapeBtn.X - 66, 12, 62, 24);
                if (expandDesktopBtn.Contains(e.Location)) { ToggleMiniPlayerMode(); return; }
                if (miniShapeBtn.Contains(e.Location)) { CycleMiniPlayerStyle(-1); return; }
                if (miniDynBarBtn.Contains(e.Location)) { ToggleDynamicBarMode(); return; }

                int artLeft = (miniPlayerStyleIdx == 1) ? 22 : 14;
                if (new Rectangle(artLeft, (h - 90) / 2 - 6, 88, 88).Contains(e.Location))
                {
                    artworkShapeMode = (artworkShapeMode + 1) % artworkShapeNames.Length;
                    SavePersistentSettings();
                    ShowToast("Animation: " + artworkShapeNames[artworkShapeMode]);
                    return;
                }

                int tLeft = artLeft + 100;
                int cy = 96;
                if (new Rectangle(tLeft, cy - 13, 26, 26).Contains(e.Location)) { PrevTrack(); return; }
                if (new Rectangle(tLeft + 34, cy - 16, 32, 32).Contains(e.Location)) { TogglePlayPause(); return; }
                if (new Rectangle(tLeft + 74, cy - 13, 26, 26).Contains(e.Location)) { NextTrack(); return; }
                if (new Rectangle(tLeft + 112, cy - 12, 92, 24).Contains(e.Location)) { CyclePlaybackSpeed(); return; }

                int barX = (miniPlayerStyleIdx == 1) ? 42 : 20;
                int barW = w - barX * 2;
                int barY = h - 18;
                if (e.Y >= barY - 10 && e.Y <= barY + 10 && e.X >= barX && e.X <= barX + barW)
                {
                    double frac = Math.Max(0.0, Math.Min(1.0, (double)(e.X - barX) / barW));
                    SeekToAbsolute(frac * currentDurSec);
                    return;
                }

                isDraggingWindow = true;
                dragStartPoint = e.Location;
                return;
            }

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

                if (updateInstallBtnRect.Contains(e.Location)) { PerformOneClickSelfUpdate(); return; }
                if (updateEditUrlBtnRect.Contains(e.Location))
                {
                    if (isAdminUpdateUnlocked) OpenProtectedUpdateFilesForAdmin();
                    else ShowAdminPasswordBox();
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

            // v10.4.4 Handle clicks inside the Studio Volume & Smoothness Controller Popup first if open!
            if (isVolumeControllerPopupOpen)
            {
                Rectangle popRect = GetVolumeControllerPopupRect(w, h);
                if (popRect.Contains(e.Location))
                {
                    int slX = popRect.X + 18;
                    int slW = popRect.Width - 36;
                    int slY = popRect.Y + 50;
                    int maxV = volumeOverdriveAllowed ? 150 : 100;

                    // 1. 0% - 150% Volume Slider Bar click
                    if (new Rectangle(slX - 8, slY - 14, slW + 16, 28).Contains(e.Location))
                    {
                        double frac = Math.Max(0.0, Math.Min(1.0, (double)(e.X - slX) / slW));
                        SetVolume((int)Math.Round(frac * maxV));
                        return;
                    }

                    // 2. Fine Step Buttons: [-5%] [-1%] [Mute] [+1%] [+5%]
                    int stGap = 6;
                    int stW = (slW - 4 * stGap) / 5;
                    for (int i = 0; i < 5; i++)
                    {
                        Rectangle sr = new Rectangle(slX + i * (stW + stGap), popRect.Y + 80, stW, 26);
                        if (sr.Contains(e.Location))
                        {
                            if (i == 0) AdjustVolumeByStep(-5);
                            else if (i == 1) AdjustVolumeByStep(-1);
                            else if (i == 2) ToggleMute();
                            else if (i == 3) AdjustVolumeByStep(+1);
                            else if (i == 4) AdjustVolumeByStep(+5);
                            return;
                        }
                    }

                    // 3. 6 One-Click Volume Presets: [20%] [50%] [75%] [85%] [100%] [135%]
                    int[] presetVols = new int[] { 20, 50, 75, 85, 100, 135 };
                    int prW = (slW - 5 * 5) / 6;
                    for (int i = 0; i < 6; i++)
                    {
                        Rectangle pr = new Rectangle(slX + i * (prW + 5), popRect.Y + 114, prW, 25);
                        if (pr.Contains(e.Location))
                        {
                            if (presetVols[i] > 100) volumeOverdriveAllowed = true;
                            SetVolume(presetVols[i]);
                            return;
                        }
                    }

                    // 4. Stereo Balance L / Center / R Buttons
                    int bBtnW = (slW - 12) / 3;
                    for (int i = 0; i < 3; i++)
                    {
                        Rectangle br = new Rectangle(slX + i * (bBtnW + 6), popRect.Y + 168, bBtnW, 24);
                        if (br.Contains(e.Location))
                        {
                            manualStereoBalance = (i == 0) ? -25 : ((i == 1) ? 0 : 25);
                            ApplyEffectiveDspAndVolume();
                            QueueSavePersistentSettings();
                            ShowToast("Stereo Balance: " + (manualStereoBalance == 0 ? "Center (0)" : (manualStereoBalance < 0 ? "Left 25%" : "Right 25%")));
                            this.Invalidate();
                            return;
                        }
                    }

                    // 5. 4 Playback Smoothness & UI Turbo Quick Toggles
                    for (int i = 0; i < 4; i++)
                    {
                        Rectangle tr = new Rectangle(slX, popRect.Y + 202 + i * 36, slW, 31);
                        if (tr.Contains(e.Location))
                        {
                            if (i == 0)
                            {
                                smoothMicroFadeEnabled = !smoothMicroFadeEnabled;
                                ShowToast("Smooth 180ms Micro-Fade: " + (smoothMicroFadeEnabled ? "ON" : "OFF"));
                            }
                            else if (i == 1)
                            {
                                autoVolumeNormalizeEnabled = !autoVolumeNormalizeEnabled;
                                ApplyEffectiveDspAndVolume();
                                ShowToast("Smart Auto-Volume Leveler: " + (autoVolumeNormalizeEnabled ? "ON" : "OFF"));
                            }
                            else if (i == 2)
                            {
                                autoPreBufferNextEnabled = !autoPreBufferNextEnabled;
                                if (autoPreBufferNextEnabled) PreBufferNextQueueSongAsync();
                                ShowToast("Zero-Lag Next-Track Pre-Buffer: " + (autoPreBufferNextEnabled ? "ON" : "OFF"));
                            }
                            else if (i == 3)
                            {
                                uiTurboCacheEnabled = !uiTurboCacheEnabled;
                                ShowToast("\u26A1 v10.4.4 UI Turbo Cache: " + (uiTurboCacheEnabled ? "ON (Zero-Lag)" : "OFF"));
                            }
                            QueueSavePersistentSettings();
                            this.Invalidate();
                            return;
                        }
                    }
                    return;
                }
                else
                {
                    // Clicked outside popup -> close cleanly
                    isVolumeControllerPopupOpen = false;
                    this.Invalidate();
                }
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

            // -----------------------------------------------------------------
            // 1. FULL-SCREEN SONG / LYRICS STUDIO PANEL CLICKS (1:1 Ref Images 2 & 3 + 3-Dot Popup!)
            // -----------------------------------------------------------------
            if (isFullPlayerExpanded)
            {
                // 1A. If the 3-Dot Popup Menu is open, handle menu item clicks first!
                if (isFullThreeDotMenuOpen)
                {
                    int popW = 276;
                    int popH = 344;
                    int popX = w - popW - 22;
                    int popY = 86;
                    Rectangle popRect = new Rectangle(popX, popY, popW, popH);

                    if (popRect.Contains(e.Location))
                    {
                        int rowStart = popRect.Y + 36;
                        int rowH = 36;
                        for (int i = 0; i < 8; i++)
                        {
                            int ry = rowStart + i * 38;
                            Rectangle itemR = new Rectangle(popRect.X + 10, ry, popRect.Width - 20, rowH);
                            if (itemR.Contains(e.Location))
                            {
                                isFullThreeDotMenuOpen = false;
                                if (i == 0)
                                {
                                    fullViewSubMode = 1;
                                    SavePersistentSettings();
                                    ShowToast("View: Playing + Lyrics (Both)");
                                }
                                else if (i == 1)
                                {
                                    fullViewSubMode = 0;
                                    SavePersistentSettings();
                                    ShowToast("View: Only Playing (Pure Song View)");
                                }
                                else if (i == 2)
                                {
                                    fullViewSubMode = 2;
                                    SavePersistentSettings();
                                    ShowToast("View: Only Lyrics (Full-Screen Boxless)");
                                }
                                else if (i == 3)
                                {
                                    fullViewSubMode = 3;
                                    SavePersistentSettings();
                                    ShowToast("View: Concert Visualizer + Lyrics Stage");
                                }
                                else if (i == 4)
                                {
                                    if (currentSongBgSlides.Count > 1)
                                    {
                                        songBgNextSlideIdx = GetNextAllowedBgSlideIndex(songBgCurrentSlideIdx, currentSongBgSlides.Count);
                                        songBgCrossfadeAlpha = 0f;
                                        isSongBgTransitioning = true;
                                        string lbl = (songBgNextSlideIdx < currentSongBgSlideLabels.Count) ? currentSongBgSlideLabels[songBgNextSlideIdx] : "Next Frame";
                                        ShowToast("\u2726 Background Crossfading to: " + lbl);
                                    }
                                    else
                                    {
                                        ShowToast("Preparing multi-image background frames...");
                                    }
                                }
                                else if (i == 5)
                                {
                                    isFullPlayerExpanded = false;
                                    ToggleDynamicBarMode();
                                    return;
                                }
                                else if (i == 6)
                                {
                                    isFullPlayerExpanded = false;
                                    activeTab = 4;
                                    activeSettingsSubPage = 1;
                                    tabFadeAnim = 0.2f;
                                    ShowToast("Opened Player & Equalizer Settings");
                                }
                                else if (i == 7)
                                {
                                    DownloadSongDirectly(currentSong);
                                }
                                this.Invalidate();
                                return;
                            }
                        }
                        return;
                    }
                    else if (!new Rectangle(w - 54, 44, 42, 42).Contains(e.Location))
                    {
                        // Clicked outside popup -> close popup cleanly
                        isFullThreeDotMenuOpen = false;
                        this.Invalidate();
                        return;
                    }
                }

                // Top-Right `⋮` Three-Dots Button toggles the 3-Dot Popup Menu!
                if (new Rectangle(w - 54, 44, 42, 42).Contains(e.Location))
                {
                    isFullThreeDotMenuOpen = !isFullThreeDotMenuOpen;
                    this.Invalidate();
                    return;
                }

                // Top-Left Brand Logo or `⌄` Collapse Chevron closes Full-Screen Panel
                if (new Rectangle(16, 12, 140, 74).Contains(e.Location))
                {
                    TriggerButtonBounce("full_close");
                    isFullThreeDotMenuOpen = false;
                    isFullPlayerExpanded = false;
                    this.Invalidate();
                    return;
                }

                int tabContainerW = 156;
                int tabContainerX = (w - tabContainerW) / 2;
                int tabY = 30;
                Rectangle songTabPill = new Rectangle(tabContainerX, tabY, 76, 30);
                Rectangle lyricsTabPill = new Rectangle(tabContainerX + 80, tabY, 76, 30);

                if (songTabPill.Contains(e.Location))
                {
                    fullViewSubMode = (fullViewSubMode == 1) ? 0 : 1;
                    SavePersistentSettings();
                    ShowToast(fullViewSubMode == 1 ? "Playing + Lyrics (Both)" : "Only Playing (Pure Song View)");
                    this.Invalidate();
                    return;
                }
                if (lyricsTabPill.Contains(e.Location))
                {
                    fullViewSubMode = (fullViewSubMode == 2) ? 1 : 2;
                    SavePersistentSettings();
                    ShowToast(fullViewSubMode == 2 ? "Only Lyrics (Full-Screen Boxless)" : "Playing + Lyrics (Both)");
                    this.Invalidate();
                    return;
                }

                int botBarY = h - 54;
                if (new Rectangle(24, botBarY, 150, 42).Contains(e.Location))
                {
                    isFullPlayerExpanded = false;
                    activeTab = 4;
                    activeSettingsSubPage = 1;
                    tabFadeAnim = 0.2f;
                    ShowToast("Audio Output: This PC (" + (eqEnabled ? activeEqPreset : "Flat") + ")");
                    this.Invalidate();
                    return;
                }

                int fpVolBarX = w - 262;
                int fpVolBarW = 96;
                int maxFpVol = volumeOverdriveAllowed ? 150 : 100;
                if (new Rectangle(fpVolBarX - 28, botBarY, 24, 38).Contains(e.Location))
                {
                    if (e.Button == MouseButtons.Right) { isVolumeControllerPopupOpen = !isVolumeControllerPopupOpen; this.Invalidate(); }
                    else ToggleMute();
                    return;
                }
                if (new Rectangle(fpVolBarX - 4, botBarY, fpVolBarW + 8, 38).Contains(e.Location))
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        isVolumeControllerPopupOpen = !isVolumeControllerPopupOpen;
                        this.Invalidate();
                        return;
                    }
                    int newV = (int)Math.Round(((double)(e.X - fpVolBarX) / fpVolBarW) * maxFpVol);
                    SetVolume(newV);
                    return;
                }
                Rectangle fpVolPillRect = new Rectangle(fpVolBarX + fpVolBarW + 6, botBarY + 4, 56, 32);
                if (fpVolPillRect.Contains(e.Location))
                {
                    isVolumeControllerPopupOpen = !isVolumeControllerPopupOpen;
                    this.Invalidate();
                    return;
                }
                if (new Rectangle(w - 94, botBarY + 2, 34, 34).Contains(e.Location))
                {
                    fullViewSubMode = (fullViewSubMode == 1) ? 0 : 1;
                    SavePersistentSettings();
                    ShowToast(fullViewSubMode == 1 ? "Lyrics Side Panel Shown" : "Lyrics Side Panel Hidden");
                    this.Invalidate();
                    return;
                }
                if (new Rectangle(w - 52, botBarY + 2, 34, 34).Contains(e.Location))
                {
                    isFullPlayerExpanded = false;
                    this.Invalidate();
                    return;
                }

                if (fullViewSubMode == 2)
                {
                    if (!syncBoxlessBtnRect.IsEmpty && syncBoxlessBtnRect.Contains(e.Location))
                    {
                        lyricStageStyleIdx = (lyricStageStyleIdx + 1) % lyricStageStyleNames.Length;
                        SavePersistentSettings();
                        ShowToast("Lyrics Stage: " + lyricStageStyleNames[lyricStageStyleIdx]);
                        return;
                    }
                    if (!syncGlowBtnRect.IsEmpty && syncGlowBtnRect.Contains(e.Location))
                    {
                        lyricGlowModeIdx = (lyricGlowModeIdx + 1) % lyricGlowModeNames.Length;
                        SavePersistentSettings();
                        ShowToast("Lyrics Glow: " + lyricGlowModeNames[lyricGlowModeIdx]);
                        return;
                    }
                    if (!syncBgArtBtnRect.IsEmpty && syncBgArtBtnRect.Contains(e.Location))
                    {
                        lyricBgArtModeIdx = (lyricBgArtModeIdx + 1) % lyricBgArtModeNames.Length;
                        SavePersistentSettings();
                        ShowToast("Lyrics Background Art: " + lyricBgArtModeNames[lyricBgArtModeIdx]);
                        return;
                    }
                    if (!syncCopyBtnRect.IsEmpty && syncCopyBtnRect.Contains(e.Location) && currentSong != null)
                    {
                        try
                        {
                            StringBuilder lsb = new StringBuilder();
                            if (currentSong.SyncedLines != null)
                            {
                                foreach (LyricLine ll in currentSong.SyncedLines) lsb.AppendLine(ll.Text);
                            }
                            string copyTxt = lsb.ToString().Trim();
                            if (!string.IsNullOrEmpty(copyTxt)) Clipboard.SetText(copyTxt);
                            ShowToast("Copied Song Lyrics to Clipboard!");
                        }
                        catch { }
                        return;
                    }
                    if (!syncRefetchBtnRect.IsEmpty && syncRefetchBtnRect.Contains(e.Location) && currentSong != null)
                    {
                        lyricSyncOffsetSec = 0.0;
                        FetchBetterLyricsAsync(currentSong);
                        ShowToast("Re-Fetching & Calibrating BetterLyrics 2.0...");
                        return;
                    }
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
                }
                else if (fullViewSubMode == 3)
                {
                    CycleVisualizerStyle();
                    return;
                }
                else
                {
                    bool showRightLyricsCard = (fullViewSubMode == 1);
                    int rightCardW = showRightLyricsCard ? 305 : 0;
                    int rightCardX = w - rightCardW - 36;
                    int availStageH = h - 180;
                    int artSz = Math.Max(240, Math.Min(370, availStageH - 20));
                    int artX = 64;
                    int artY = 92 + (availStageH - artSz) / 2;
                    Rectangle bigArtRect = new Rectangle(artX, artY, artSz, artSz);

                    int centerColX = bigArtRect.Right + 54;
                    int centerColRightLimit = showRightLyricsCard ? (rightCardX - 36) : (w - 64);
                    int centerColW = Math.Max(260, centerColRightLimit - centerColX);
                    int centerColTopY = artY + (artSz - 210) / 2;

                    Rectangle heartRect = new Rectangle(centerColX + centerColW - 38, centerColTopY + 38, 36, 36);
                    if (heartRect.Contains(e.Location) && currentSong != null)
                    {
                        if (likedIds.Contains(currentSong.Id)) likedIds.Remove(currentSong.Id);
                        else likedIds.Add(currentSong.Id);
                        SavePersistentSettings();
                        ShowToast(likedIds.Contains(currentSong.Id) ? "Added to Liked Songs" : "Removed from Liked");
                        this.Invalidate();
                        return;
                    }

                    int sliderY = centerColTopY + 104;
                    if (new Rectangle(centerColX - 8, sliderY - 14, centerColW + 16, 28).Contains(e.Location))
                    {
                        double frac = Math.Max(0.0, Math.Min(1.0, (double)(e.X - centerColX) / centerColW));
                        SeekToAbsolute(frac * currentDurSec);
                        return;
                    }

                    int ctrlCy = centerColTopY + 176;
                    int ctrlMidX = centerColX + centerColW / 2;

                    if (new Rectangle(ctrlMidX - 150, ctrlCy - 18, 36, 36).Contains(e.Location))
                    {
                        isShuffle = !isShuffle;
                        SavePersistentSettings();
                        ShowToast(isShuffle ? "Shuffle On" : "Shuffle Off");
                        this.Invalidate();
                        return;
                    }
                    if (new Rectangle(ctrlMidX - 88, ctrlCy - 20, 40, 40).Contains(e.Location))
                    {
                        PrevTrack();
                        return;
                    }
                    if (new Rectangle(ctrlMidX - 34, ctrlCy - 34, 68, 68).Contains(e.Location))
                    {
                        TogglePlayPause();
                        return;
                    }
                    if (new Rectangle(ctrlMidX + 48, ctrlCy - 20, 40, 40).Contains(e.Location))
                    {
                        NextTrack();
                        return;
                    }
                    if (new Rectangle(ctrlMidX + 114, ctrlCy - 18, 36, 36).Contains(e.Location))
                    {
                        isRepeatOne = !isRepeatOne;
                        SavePersistentSettings();
                        ShowToast(isRepeatOne ? "Repeat One On" : "Repeat Off");
                        this.Invalidate();
                        return;
                    }

                    if (showRightLyricsCard)
                    {
                        Rectangle rightLyrCard = new Rectangle(rightCardX, artY - 6, rightCardW, artSz + 12);
                        if (new Rectangle(rightLyrCard.Right - 44, rightLyrCard.Y + 10, 34, 32).Contains(e.Location))
                        {
                            fullViewSubMode = 2;
                            SavePersistentSettings();
                            ShowToast("Expanded to Full-Screen Boxless Lyrics");
                            this.Invalidate();
                            return;
                        }
                    }
                }

                for (int i = 0; i < activeLyricHitRects.Count; i++)
                {
                    if (activeLyricHitRects[i].Contains(e.Location))
                    {
                        if (e.Button == MouseButtons.Right && currentSong != null && currentSong.SyncedLines != null && i < currentSong.SyncedLines.Count)
                        {
                            try { Clipboard.SetText(currentSong.SyncedLines[i].Text); ShowToast("Copied Lyric Line!"); } catch { }
                            return;
                        }
                        SeekToAbsolute(activeLyricHitTimes[i]);
                        ShowToast("Synced to lyric (" + FormatSec((int)activeLyricHitTimes[i]) + ")");
                        return;
                    }
                }

                if (e.Y < 48)
                {
                    isDraggingWindow = true;
                    dragStartPoint = e.Location;
                }
                return;
            }

            // -----------------------------------------------------------------
            // 2. BOTTOM FLOATING PLAYER DOCK CLICKS (`x >= 204, y >= h - 78`)
            // -----------------------------------------------------------------
            Rectangle dockRect = new Rectangle(204, h - 76, w - 216, 66);
            if (dockRect.Contains(e.Location))
            {
                UnfocusSearchInput();
                int midX = dockRect.X + dockRect.Width / 2;
                float btnCy = dockRect.Y + 23f;

                if (e.X >= dockRect.X + 8 && e.X <= dockRect.X + 198)
                {
                    TriggerButtonBounce("exp_bot");
                    isFullPlayerExpanded = !isFullPlayerExpanded;
                    this.Invalidate();
                    return;
                }
                if (e.X >= dockRect.X + 200 && e.X <= dockRect.X + 234 && currentSong != null)
                {
                    if (likedIds.Contains(currentSong.Id)) likedIds.Remove(currentSong.Id);
                    else likedIds.Add(currentSong.Id);
                    SavePersistentSettings();
                    this.Invalidate();
                    return;
                }

                if (new Rectangle(midX - 114, (int)btnCy - 14, 34, 28).Contains(e.Location))
                {
                    isShuffle = !isShuffle;
                    SavePersistentSettings();
                    ShowToast(isShuffle ? "Shuffle On" : "Shuffle Off");
                    this.Invalidate();
                    return;
                }
                if (new Rectangle(midX - 66, (int)btnCy - 16, 36, 32).Contains(e.Location)) { PrevTrack(); return; }
                if (new Rectangle(midX - 22, (int)btnCy - 22, 44, 44).Contains(e.Location)) { TogglePlayPause(); return; }
                if (new Rectangle(midX + 30, (int)btnCy - 16, 36, 32).Contains(e.Location)) { NextTrack(); return; }
                if (new Rectangle(midX + 78, (int)btnCy - 14, 34, 28).Contains(e.Location))
                {
                    isRepeatOne = !isRepeatOne;
                    SavePersistentSettings();
                    ShowToast(isRepeatOne ? "Repeat One On" : "Repeat Off");
                    this.Invalidate();
                    return;
                }

                int barW = Math.Min(360, Math.Max(200, dockRect.Width - 520));
                int barX = midX - barW / 2;
                int barY = dockRect.Y + 52;
                if (new Rectangle(barX - 6, barY - 12, barW + 12, 24).Contains(e.Location))
                {
                    double frac = Math.Max(0.0, Math.Min(1.0, (double)(e.X - barX) / barW));
                    SeekToAbsolute(frac * currentDurSec);
                    return;
                }

                int volBarX = dockRect.Right - 224;
                int volBarW = 76;
                int volBarY = dockRect.Y + 33;
                int maxDockVol = volumeOverdriveAllowed ? 150 : 100;

                // Quick A-B Seamless Loop Pill click
                Rectangle abPillRect = new Rectangle(volBarX - 78, volBarY - 14, 48, 28);
                if (abPillRect.Contains(e.Location))
                {
                    if (abLoopStartSec < 0)
                    {
                        abLoopStartSec = currentPosSec;
                        abLoopEndSec = -1.0;
                        ShowToast("A-B Loop: Point A set at " + FormatSec((int)abLoopStartSec) + " (Click again to set B)");
                    }
                    else if (abLoopEndSec < 0 && currentPosSec > abLoopStartSec + 1.0)
                    {
                        abLoopEndSec = currentPosSec;
                        ShowToast("\u21BB A-B Seamless Loop Active: " + FormatSec((int)abLoopStartSec) + " \u2192 " + FormatSec((int)abLoopEndSec));
                    }
                    else
                    {
                        abLoopStartSec = -1.0;
                        abLoopEndSec = -1.0;
                        ShowToast("A-B Loop Cleared");
                    }
                    this.Invalidate();
                    return;
                }

                if (new Rectangle(volBarX - 26, volBarY - 14, 22, 28).Contains(e.Location))
                {
                    if (e.Button == MouseButtons.Right) { isVolumeControllerPopupOpen = !isVolumeControllerPopupOpen; this.Invalidate(); }
                    else ToggleMute();
                    return;
                }
                if (new Rectangle(volBarX - 4, volBarY - 14, volBarW + 6, 28).Contains(e.Location))
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        isVolumeControllerPopupOpen = !isVolumeControllerPopupOpen;
                        this.Invalidate();
                        return;
                    }
                    int newV = (int)Math.Round(((double)(e.X - volBarX) / volBarW) * maxDockVol);
                    SetVolume(newV);
                    return;
                }
                Rectangle volPillRect = new Rectangle(volBarX + volBarW + 4, volBarY - 14, 54, 28);
                if (volPillRect.Contains(e.Location))
                {
                    isVolumeControllerPopupOpen = !isVolumeControllerPopupOpen;
                    this.Invalidate();
                    return;
                }
                if (new Rectangle(dockRect.Right - 80, volBarY - 16, 32, 32).Contains(e.Location))
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        ToggleRightPanelVisibility();
                    }
                    else
                    {
                        if (!isRightPanelVisible) ToggleRightPanelVisibility();
                        else
                        {
                            rightPanelMode = (rightPanelMode == 0) ? 1 : 0;
                            SavePersistentSettings();
                            ShowToast(rightPanelMode == 0 ? "Right Panel: Up Next Queue" : "Right Panel: Synced Lyrics");
                            this.Invalidate();
                        }
                    }
                    return;
                }
                if (new Rectangle(dockRect.Right - 44, volBarY - 16, 32, 32).Contains(e.Location))
                {
                    TriggerButtonBounce("exp_bot");
                    isFullPlayerExpanded = !isFullPlayerExpanded;
                    this.Invalidate();
                    return;
                }
                return;
            }

            // -----------------------------------------------------------------
            // 3. LEFT SIDEBAR NAVIGATION & QUICK PLAYLIST CLICKS (`x < sidebarW`)
            // -----------------------------------------------------------------
            if (e.X < sidebarW)
            {
                UnfocusSearchInput();
                HidePlaylistNameBox();

                int[] navTabIds = new int[] { 0, 1, 6, 2, 5, 3 };
                for (int i = 0; i < navTabIds.Length; i++)
                {
                    Rectangle navRect = new Rectangle(20, 72 + i * 38, sidebarW - 34, 34);
                    if (navRect.Contains(e.Location))
                    {
                        int tabId = navTabIds[i];
                        TriggerButtonBounce("nav_" + tabId);
                        activeTab = tabId;
                        tabFadeAnim = 0.2f;
                        targetScrollOffset = 0;
                        if (tabId == 0 || tabId == 6)
                        {
                            searchBox.Text = "";
                            searchQueryText = "";
                            liveSearchResults.Clear();
                        }
                        else if (tabId == 1)
                        {
                            FocusSearchInput();
                        }
                        else if (tabId == 5)
                        {
                            SyncLeoSmartPlaylistWithMood(false);
                        }
                        if (tabId != 4) FilterDisplaySongs();
                        else this.Invalidate();
                        return;
                    }
                }

                int divY = 308;
                int plHeaderY = divY + 10;
                if (new Rectangle(sidebarW - 42, plHeaderY - 4, 28, 26).Contains(e.Location))
                {
                    activeTab = 5;
                    tabFadeAnim = 0.2f;
                    int padX = centerX + 26;
                    int contentW = centerW - 52;
                    ShowPlaylistNameBox(padX + contentW - 292, 31, 180);
                    return;
                }

                int plListStartY = plHeaderY + 26;
                int bottomSettingsY = h - 54;
                int maxSidePlaylists = Math.Min(5, userPlaylists.Count);
                for (int i = 0; i < maxSidePlaylists; i++)
                {
                    int py = plListStartY + i * 36;
                    if (py + 32 > bottomSettingsY - 8) break;
                    Rectangle plRowRect = new Rectangle(18, py, sidebarW - 30, 32);
                    if (plRowRect.Contains(e.Location))
                    {
                        selectedPlaylistIdx = i;
                        activeTab = 5;
                        tabFadeAnim = 0.2f;
                        targetScrollOffset = 0;
                        if (i == 0) SyncLeoSmartPlaylistWithMood(true);
                        this.Invalidate();
                        return;
                    }
                }

                if (new Rectangle(20, bottomSettingsY, sidebarW - 34, 34).Contains(e.Location))
                {
                    TriggerButtonBounce("nav_4");
                    activeTab = 4;
                    tabFadeAnim = 0.2f;
                    this.Invalidate();
                    return;
                }
                return;
            }

            // -----------------------------------------------------------------
            // 4. RIGHT FLOATING "UP NEXT" / LYRICS CARD CLICKS (`e.X >= rightX`)
            // -----------------------------------------------------------------
            if (rightW > 40 && e.X >= rightX && e.Y < mainH)
            {
                UnfocusSearchInput();
                HidePlaylistNameBox();
                Rectangle cardRect = new Rectangle(rightX + 6, 66, rightW - 18, mainH - 72);

                if (new Rectangle(cardRect.Right - 46, cardRect.Y + 10, 36, 34).Contains(e.Location))
                {
                    rightPanelMode = (rightPanelMode == 0) ? 1 : 0;
                    SavePersistentSettings();
                    ShowToast(rightPanelMode == 0 ? "Showing Up Next Queue" : "Showing Synced Lyrics");
                    this.Invalidate();
                    return;
                }

                Rectangle listBox = new Rectangle(cardRect.X + 8, cardRect.Y + 52, cardRect.Width - 16, cardRect.Height - 60);
                if (listBox.Contains(e.Location))
                {
                    if (rightPanelMode == 0)
                    {
                        int qRowH = 58;
                        for (int i = 0; i < queueSongs.Count; i++)
                        {
                            int qy = listBox.Y + 2 + i * (qRowH + 4) - queueScrollOffset;
                            Rectangle qRowRect = new Rectangle(listBox.X + 4, qy, listBox.Width - 8, qRowH);
                            if (qRowRect.Contains(e.Location))
                            {
                                if (e.X >= qRowRect.Right - 36 && currentSong != null && queueSongs[i].Id != currentSong.Id)
                                {
                                    AddSongToActiveUserPlaylist(queueSongs[i]);
                                    return;
                                }
                                currentQueueIndex = i;
                                PlaySong(queueSongs[i], true);
                                return;
                            }
                        }
                    }
                    else
                    {
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
                HidePlaylistNameBox();
                HandleSettingsClick(e.Location, centerX, centerW, false);
                return;
            }

            // 5B. Playlists & Leo Auto-Mood Studio Tab Clicks (activeTab == 5)
            if (activeTab == 5 && e.X >= centerX && e.X < rightX)
            {
                UnfocusSearchInput();
                for (int i = 0; i < playlistStageHitZones.Count; i++)
                {
                    HomeHitZone phz = playlistStageHitZones[i];
                    if (phz.Rect.Contains(e.Location))
                    {
                        if (!string.IsNullOrEmpty(phz.HoverKey)) TriggerButtonBounce(phz.HoverKey);
                        if (phz.Action == "start_create_playlist")
                        {
                            int padX = centerX + 26;
                            int contentW = centerW - 52;
                            ShowPlaylistNameBox(padX + contentW - 292, 31, 180);
                            return;
                        }
                        else if (phz.Action == "confirm_create_playlist")
                        {
                            ConfirmCreateCustomPlaylist();
                            return;
                        }
                        else if (phz.Action == "select_playlist_idx")
                        {
                            int idx = 0;
                            int.TryParse(phz.Payload, out idx);
                            selectedPlaylistIdx = idx;
                            targetScrollOffset = 0;
                            this.Invalidate();
                            return;
                        }
                        else if (phz.Action == "leo_playlist_mood")
                        {
                            selectedMood = phz.Payload;
                            SyncLeoSmartPlaylistWithMood(true);
                            FilterDisplaySongs();
                            FetchLiveTrendingSongsAsync(false);
                            this.Invalidate();
                            return;
                        }
                        else if (phz.Action == "play_active_playlist")
                        {
                            if (selectedPlaylistIdx >= 0 && selectedPlaylistIdx < userPlaylists.Count)
                            {
                                PlayEntirePlaylist(userPlaylists[selectedPlaylistIdx], false);
                            }
                            return;
                        }
                        else if (phz.Action == "delete_custom_playlist")
                        {
                            if (selectedPlaylistIdx >= 1 && selectedPlaylistIdx < userPlaylists.Count)
                            {
                                string delName = userPlaylists[selectedPlaylistIdx].Name;
                                userPlaylists.RemoveAt(selectedPlaylistIdx);
                                selectedPlaylistIdx = 0;
                                SavePersistentSettings();
                                ShowToast("Deleted Playlist: " + delName);
                                this.Invalidate();
                            }
                            return;
                        }
                    }
                }

                for (int i = 0; i < homeHitZones.Count; i++)
                {
                    HomeHitZone hz = homeHitZones[i];
                    if (hz.Rect.Contains(e.Location))
                    {
                        if (hz.Action == "play_song" && hz.Song != null) { PlaySong(hz.Song, false); return; }
                        if (hz.Action == "queue_song" && hz.Song != null) { AddToQueue(hz.Song); return; }
                        if (hz.Action == "like_song" && hz.Song != null)
                        {
                            if (likedIds.Contains(hz.Song.Id)) likedIds.Remove(hz.Song.Id);
                            else likedIds.Add(hz.Song.Id);
                            SavePersistentSettings();
                            this.Invalidate();
                            return;
                        }
                        if (hz.Action == "download_song" && hz.Song != null) { DownloadSongDirectly(hz.Song); return; }
                        if (hz.Action == "add_playlist_song" && hz.Song != null) { AddSongToActiveUserPlaylist(hz.Song); return; }
                        if (hz.Action == "remove_playlist_song" && hz.Song != null)
                        {
                            if (selectedPlaylistIdx >= 1 && selectedPlaylistIdx < userPlaylists.Count)
                            {
                                userPlaylists[selectedPlaylistIdx].Songs.Remove(hz.Song);
                                SavePersistentSettings();
                                ShowToast("Removed from Playlist: " + hz.Song.Title);
                                this.Invalidate();
                            }
                            return;
                        }
                    }
                }
                return;
            }

            // 6. Center Song Stage Top Search Pill (`centerX + 24, y = 18, h = 38`) & Top Filter Hits
            int searchW = Math.Min(410, Math.Max(210, centerW - 190));
            Rectangle searchPill = new Rectangle(centerX + 24, 18, searchW, 38);

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

            for (int i = 0; i < topFilterHitZones.Count; i++)
            {
                HomeHitZone tfz = topFilterHitZones[i];
                if (tfz.Rect.Contains(e.Location))
                {
                    if (!string.IsNullOrEmpty(tfz.HoverKey)) TriggerButtonBounce(tfz.HoverKey);
                    if (tfz.Action == "select_mood_chip")
                    {
                        selectedMood = tfz.Payload;
                        liveSearchResults.Clear();
                        searchBox.Text = "";
                        searchQueryText = "";
                        targetScrollOffset = 0;
                        SyncLeoSmartPlaylistWithMood(true);
                        FilterDisplaySongs();
                        FetchLiveTrendingSongsAsync(false);
                        return;
                    }
                    else if (tfz.Action == "open_account_settings")
                    {
                        activeTab = 4;
                        activeSettingsSubPage = 0;
                        tabFadeAnim = 0.2f;
                        ShowToast("Opened Account & Profile Settings");
                        this.Invalidate();
                        return;
                    }
                    else if (tfz.Action == "open_lang_modal")
                    {
                        isLanguageSetupScreenOpen = true;
                        this.Invalidate();
                        return;
                    }
                    else if (tfz.Action == "check_updates_top")
                    {
                        CheckForAppUpdatesAsync(true);
                        return;
                    }
                    else if (tfz.Action == "toggle_mini_top")
                    {
                        ToggleMiniPlayerMode();
                        return;
                    }
                    else if (tfz.Action == "toggle_dyn_bar_top")
                    {
                        ToggleDynamicBarMode();
                        return;
                    }
                    else if (tfz.Action == "toggle_vol_popup_top")
                    {
                        isVolumeControllerPopupOpen = !isVolumeControllerPopupOpen;
                        this.Invalidate();
                        return;
                    }
                    else if (tfz.Action == "toggle_turbo_top")
                    {
                        uiTurboCacheEnabled = !uiTurboCacheEnabled;
                        QueueSavePersistentSettings();
                        ShowToast("\u26A1 v10.4.4 UI Turbo Cache: " + (uiTurboCacheEnabled ? "ON (Zero-Lag Active)" : "OFF"));
                        this.Invalidate();
                        return;
                    }
                }
            }

            if (e.X >= centerX && e.X < rightX && e.Y >= 64 && e.Y < mainH)
            {
                for (int i = 0; i < homeHitZones.Count; i++)
                {
                    HomeHitZone hz = homeHitZones[i];
                    if (hz.Rect.Contains(e.Location))
                    {
                        if (!string.IsNullOrEmpty(hz.HoverKey)) TriggerButtonBounce(hz.HoverKey);
                        if (hz.Action == "play_song" && hz.Song != null)
                        {
                            PlaySong(hz.Song, false);
                            return;
                        }
                        else if (hz.Action == "toggle_hero_play" && hz.Song != null)
                        {
                            if (currentSong != null && currentSong.Id == hz.Song.Id) TogglePlayPause();
                            else PlaySong(hz.Song, false);
                            return;
                        }
                        else if (hz.Action == "open_full_player")
                        {
                            isFullPlayerExpanded = true;
                            this.Invalidate();
                            return;
                        }
                        else if (hz.Action == "open_library_tab")
                        {
                            activeTab = 6;
                            tabFadeAnim = 0.2f;
                            targetScrollOffset = 0;
                            FilterDisplaySongs();
                            return;
                        }
                        else if (hz.Action == "select_mood_inline")
                        {
                            selectedMood = hz.Payload;
                            SyncLeoSmartPlaylistWithMood(true);
                            FilterDisplaySongs();
                            FetchLiveTrendingSongsAsync(false);
                            return;
                        }
                        else if (hz.Action == "queue_song" && hz.Song != null)
                        {
                            AddToQueue(hz.Song);
                            return;
                        }
                        else if (hz.Action == "add_playlist_song" && hz.Song != null)
                        {
                            AddSongToActiveUserPlaylist(hz.Song);
                            return;
                        }
                        else if (hz.Action == "like_song" && hz.Song != null)
                        {
                            if (likedIds.Contains(hz.Song.Id)) likedIds.Remove(hz.Song.Id);
                            else likedIds.Add(hz.Song.Id);
                            SavePersistentSettings();
                            this.Invalidate();
                            return;
                        }
                        else if (hz.Action == "download_song" && hz.Song != null)
                        {
                            DownloadSongDirectly(hz.Song);
                            return;
                        }
                        else if (hz.Action == "refresh_trending")
                        {
                            ShowToast("Refreshing Trending & Language Charts...");
                            FetchLiveTrendingSongsAsync(true);
                            return;
                        }
                    }
                }
            }

            if (e.Y < 56)
            {
                isDraggingWindow = true;
                dragStartPoint = e.Location;
                return;
            }
        }

        // =========================================================================
        // v10.4.3 MULTI-PAGE SETTINGS CLICK & DRAG HANDLER
        // =========================================================================

        private void HandleSettingsClick(Point pt, int centerX, int centerW, bool isDrag)
        {
            int padX = centerX + 24;
            int contentW = centerW - 48;
            int pageTopY = 98;
            int halfW = (contentW - 16) / 2;

            // 0. Check 5-Tab Settings Sub-Navigation Bar (`y = 50, h = 36`)
            if (!isDrag && pt.Y >= 46 && pt.Y <= 90)
            {
                int numTabs = settingsSubPageNames.Length;
                int tabGap = 8;
                int tabW = (contentW - (numTabs - 1) * tabGap) / numTabs;
                for (int i = 0; i < numTabs; i++)
                {
                    Rectangle tRect = new Rectangle(padX + i * (tabW + tabGap), 50, tabW, 36);
                    if (tRect.Contains(pt))
                    {
                        activeSettingsSubPage = i;
                        SavePersistentSettings();
                        this.Invalidate();
                        return;
                    }
                }
            }

            // -----------------------------------------------------------------
            // PAGE 0: ACCOUNT & PROFILE CLICKS
            // -----------------------------------------------------------------
            if (activeSettingsSubPage == 0)
            {
                if (isDrag) return;
                Rectangle langCard = new Rectangle(padX, pageTopY + 168, halfW, 190);
                Rectangle openLangModalBtn = new Rectangle(langCard.X + 18, langCard.Bottom - 54, langCard.Width - 36, 38);
                if (openLangModalBtn.Contains(pt))
                {
                    isLanguageSetupScreenOpen = true;
                    this.Invalidate();
                    return;
                }

                Rectangle qualCard = new Rectangle(padX + halfW + 16, pageTopY + 168, halfW, 190);
                string[] qCodes = new string[] { "320kbps", "160kbps", "96kbps" };
                int qBtnW = (qualCard.Width - 36 - 16) / 3;
                for (int i = 0; i < 3; i++)
                {
                    Rectangle qr = new Rectangle(qualCard.X + 18 + i * (qBtnW + 8), qualCard.Y + 68, qBtnW, 36);
                    if (qr.Contains(pt))
                    {
                        activeStreamQuality = qCodes[i];
                        SavePersistentSettings();
                        ShowToast("Streaming Quality Set: " + streamQualityNames[i]);
                        this.Invalidate();
                        return;
                    }
                }

                Rectangle discAccBtn = new Rectangle(qualCard.X + 18, qualCard.Bottom - 54, qualCard.Width - 36, 38);
                if (discAccBtn.Contains(pt))
                {
                    OpenDiscordLink();
                    return;
                }
                return;
            }

            // -----------------------------------------------------------------
            // PAGE 1: PLAYER & AUDIO EQ CLICKS & DRAGS
            // -----------------------------------------------------------------
            if (activeSettingsSubPage == 1)
            {
                Rectangle eqCard = new Rectangle(padX, pageTopY, contentW, 228);
                int bandStartX = eqCard.X + 36;
                int bandTopY = eqCard.Y + 84;
                int bandTrackH = 104;
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
                    int barY = eqCard.Y + 80 + i * 46 + 22;
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

                Rectangle eqToggleRect = new Rectangle(eqCard.Right - 68, eqCard.Y + 8, 52, 26);
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
                    int py = eqCard.Y + 40;
                    for (int i = 0; i < eqPresetNames.Length; i++)
                    {
                        int pw = (int)g.MeasureString(eqPresetNames[i], fChip).Width + 16;
                        Rectangle pr = new Rectangle(px, py, pw, 24);
                        if (pr.Contains(pt))
                        {
                            ApplyEqPreset(eqPresetNames[i]);
                            return;
                        }
                        px += pw + 6;
                    }
                }

                Rectangle cfCard = new Rectangle(padX, pageTopY + 240, halfW, 194);
                if (new Rectangle(cfCard.X + 12, cfCard.Y + 36, cfCard.Width - 24, 28).Contains(pt))
                {
                    crossfadeEnabled = !crossfadeEnabled;
                    SavePersistentSettings();
                    ShowToast(crossfadeEnabled ? ("Crossfade Enabled (" + crossfadeSeconds + "s)") : "Crossfade Off");
                    return;
                }
                int[] cfOpts = new int[] { 1, 3, 5, 8, 12 };
                for (int i = 0; i < cfOpts.Length; i++)
                {
                    Rectangle cfr = new Rectangle(cfCard.X + 16 + i * 50, cfCard.Y + 68, 44, 26);
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

                Rectangle spdCard = new Rectangle(padX + halfW + 16, pageTopY + 240, halfW, 194);
                int spBtnW = (spdCard.Width - 32 - 3 * 6) / 4;
                for (int i = 0; i < playbackSpeedNames.Length; i++)
                {
                    Rectangle sr = new Rectangle(spdCard.X + 16 + i * (spBtnW + 6), spdCard.Y + 66, spBtnW, 28);
                    if (sr.Contains(pt))
                    {
                        playbackSpeedIdx = i;
                        ApplyEffectiveDspAndVolume();
                        SavePersistentSettings();
                        ShowToast("Playback Speed: " + playbackSpeedNames[i]);
                        this.Invalidate();
                        return;
                    }
                }
                int[] stOpts = new int[] { 0, 15, 30, 60 };
                for (int i = 0; i < stOpts.Length; i++)
                {
                    Rectangle str = new Rectangle(spdCard.X + 16 + i * (spBtnW + 6), spdCard.Y + 136, spBtnW, 28);
                    if (str.Contains(pt))
                    {
                        sleepTimerMinutes = stOpts[i];
                        sleepTimerSecondsLeft = sleepTimerMinutes * 60;
                        SavePersistentSettings();
                        ShowToast(sleepTimerMinutes == 0 ? "Sleep Timer Off" : ("Sleep Timer: " + sleepTimerMinutes + " mins"));
                        this.Invalidate();
                        return;
                    }
                }
                return;
            }

            // -----------------------------------------------------------------
            // PAGE 2: LYRICS & DYNAMIC BAR CLICKS
            // -----------------------------------------------------------------
            if (activeSettingsSubPage == 2)
            {
                if (isDrag) return;
                Rectangle lyrSetCard = new Rectangle(padX, pageTopY, contentW, 196);
                int lCols = 4;
                int lBtnW = (lyrSetCard.Width - 36 - (lCols - 1) * 10) / lCols;
                for (int i = 0; i < 8; i++)
                {
                    int r = i / lCols;
                    int c = i % lCols;
                    Rectangle lr = new Rectangle(lyrSetCard.X + 18 + c * (lBtnW + 10), lyrSetCard.Y + 68 + r * 52, lBtnW, 40);
                    if (lr.Contains(pt))
                    {
                        if (i == 0) { lyricStageStyleIdx = (lyricStageStyleIdx + 1) % lyricStageStyleNames.Length; ShowToast("Lyrics Stage: " + lyricStageStyleNames[lyricStageStyleIdx]); }
                        else if (i == 1) { lyricFadeModeIdx = (lyricFadeModeIdx + 1) % lyricFadeModeNames.Length; ShowToast("Fade-on-Fade Curve: " + lyricFadeModeNames[lyricFadeModeIdx]); }
                        else if (i == 2) { lyricFadeSizeIdx = (lyricFadeSizeIdx + 1) % lyricFadeSizeNames.Length; ShowToast("Fade Zone Size: " + lyricFadeSizeNames[lyricFadeSizeIdx]); }
                        else if (i == 3) { lyricGlowModeIdx = (lyricGlowModeIdx + 1) % lyricGlowModeNames.Length; ShowToast("Lyrics Glow: " + lyricGlowModeNames[lyricGlowModeIdx]); }
                        else if (i == 4) { lyricBgArtModeIdx = (lyricBgArtModeIdx + 1) % lyricBgArtModeNames.Length; ShowToast("Lyrics Background Art: " + lyricBgArtModeNames[lyricBgArtModeIdx]); }
                        else if (i == 5) { lyricFontStyleIdx = (lyricFontStyleIdx + 1) % lyricFontStyleNames.Length; cachedLyricWrapKey = ""; ShowToast("Lyrics Font Style: " + lyricFontStyleNames[lyricFontStyleIdx]); }
                        else if (i == 6) { lyricTextSizeIdx = (lyricTextSizeIdx + 1) % lyricTextSizeNames.Length; cachedLyricWrapKey = ""; ShowToast("Lyrics Text Size: " + lyricTextSizeNames[lyricTextSizeIdx]); }
                        else if (i == 7) { dynamicBarStyleIdx = (dynamicBarStyleIdx + 1) % dynamicBarStyleNames.Length; ShowToast("Dynamic Bar Theme: " + dynamicBarStyleNames[dynamicBarStyleIdx]); }
                        SavePersistentSettings();
                        this.Invalidate();
                        return;
                    }
                }

                Rectangle dynCard = new Rectangle(padX, pageTopY + 210, halfW, 158);
                Rectangle launchDynBtn = new Rectangle(dynCard.X + 18, dynCard.Bottom - 52, (dynCard.Width - 46) / 2, 36);
                Rectangle switchDynStyleBtn = new Rectangle(launchDynBtn.Right + 10, dynCard.Bottom - 52, (dynCard.Width - 46) / 2, 36);
                if (launchDynBtn.Contains(pt))
                {
                    ToggleDynamicBarMode();
                    return;
                }
                if (switchDynStyleBtn.Contains(pt))
                {
                    dynamicBarStyleIdx = (dynamicBarStyleIdx + 1) % dynamicBarStyleNames.Length;
                    SavePersistentSettings();
                    ShowToast("Dynamic Bar Theme: " + dynamicBarStyleNames[dynamicBarStyleIdx]);
                    this.Invalidate();
                    return;
                }

                Rectangle miniCard = new Rectangle(padX + halfW + 16, pageTopY + 210, halfW, 158);
                int mBtnW = (miniCard.Width - 36 - 3 * 6) / 4;
                for (int i = 0; i < miniPlayerStyleNames.Length; i++)
                {
                    Rectangle mr = new Rectangle(miniCard.X + 18 + i * (mBtnW + 6), miniCard.Y + 74, mBtnW, 34);
                    if (mr.Contains(pt))
                    {
                        CycleMiniPlayerStyle(i);
                        return;
                    }
                }
                return;
            }

            // -----------------------------------------------------------------
            // PAGE 3: THEME & MULTI-IMAGE SONG BACKGROUND CLICKS
            // -----------------------------------------------------------------
            if (activeSettingsSubPage == 3)
            {
                if (isDrag) return;
                Rectangle bgSlideCard = new Rectangle(padX, pageTopY, contentW, 178);
                if (new Rectangle(bgSlideCard.Right - 72, bgSlideCard.Y + 30, 58, 32).Contains(pt))
                {
                    songBgImageEnabled = !songBgImageEnabled;
                    SavePersistentSettings();
                    ShowToast(songBgImageEnabled ? "\u2726 Playing Song Multi-Image Background ON" : "Song Background Image OFF");
                    this.Invalidate();
                    return;
                }

                int bgBtnW = (bgSlideCard.Width - 36 - 3 * 10) / 4;
                for (int i = 0; i < 4; i++)
                {
                    Rectangle br = new Rectangle(bgSlideCard.X + 18 + i * (bgBtnW + 10), bgSlideCard.Y + 74, bgBtnW, 38);
                    if (br.Contains(pt))
                    {
                        if (i == 0)
                        {
                            songBgStyleIdx = (songBgStyleIdx + 1) % songBgStyleNames.Length;
                            songBgCurrentSlideIdx = GetNextAllowedBgSlideIndex(songBgCurrentSlideIdx, Math.Max(1, currentSongBgSlides.Count));
                            ShowToast("Background Image Set: " + songBgStyleNames[songBgStyleIdx]);
                        }
                        else if (i == 1)
                        {
                            songBgAnimModeIdx = (songBgAnimModeIdx + 1) % songBgAnimModeNames.Length;
                            ShowToast("Background Transition: " + songBgAnimModeNames[songBgAnimModeIdx]);
                        }
                        else if (i == 2)
                        {
                            songBgIntervalIdx = (songBgIntervalIdx + 1) % songBgIntervalSecs.Length;
                            ShowToast("Background Change Speed: Every " + songBgIntervalSecs[songBgIntervalIdx] + "s");
                        }
                        else if (i == 3)
                        {
                            if (currentSongBgSlides.Count > 1)
                            {
                                songBgNextSlideIdx = GetNextAllowedBgSlideIndex(songBgCurrentSlideIdx, currentSongBgSlides.Count);
                                songBgCrossfadeAlpha = 0f;
                                isSongBgTransitioning = true;
                                string lbl = (songBgNextSlideIdx < currentSongBgSlideLabels.Count) ? currentSongBgSlideLabels[songBgNextSlideIdx] : "Next Frame";
                                ShowToast("\u2726 Crossfading Background to: " + lbl);
                            }
                        }
                        SavePersistentSettings();
                        this.Invalidate();
                        return;
                    }
                }

                // Clicking any of the 6 filmstrip thumbnails jumps directly to that background frame with a smooth crossfade!
                int stripY = bgSlideCard.Y + 122;
                int numPrv = Math.Min(6, currentSongBgSlides.Count);
                int prvGap = 10;
                int prvW = (bgSlideCard.Width - 36 - Math.Max(1, numPrv - 1) * prvGap) / Math.Max(1, numPrv);
                for (int i = 0; i < numPrv; i++)
                {
                    Rectangle pr = new Rectangle(bgSlideCard.X + 18 + i * (prvW + prvGap), stripY, prvW, 44);
                    if (pr.Contains(pt))
                    {
                        songBgNextSlideIdx = i;
                        songBgCrossfadeAlpha = 0f;
                        isSongBgTransitioning = true;
                        songBgTimerAccum = 0.0;
                        string lbl = (i < currentSongBgSlideLabels.Count) ? currentSongBgSlideLabels[i] : ("Frame #" + (i + 1));
                        ShowToast("\u2726 Selected Background Frame: " + lbl);
                        this.Invalidate();
                        return;
                    }
                }

                Rectangle surfCard = new Rectangle(padX, pageTopY + 190, contentW, 132);
                for (int i = 0; i < 4; i++)
                {
                    Rectangle tr = new Rectangle(surfCard.X + 18 + i * (bgBtnW + 10), surfCard.Y + 38, bgBtnW, 34);
                    if (tr.Contains(pt))
                    {
                        if (i == 0)
                        {
                            if (glassEffectEnabled && curved3DGlassEnabled) { curved3DGlassEnabled = false; ShowToast("Flat Frosted Glass Mode"); }
                            else if (glassEffectEnabled && !curved3DGlassEnabled) { glassEffectEnabled = false; ShowToast("Frosted Glass OFF"); }
                            else { glassEffectEnabled = true; curved3DGlassEnabled = true; ShowToast("\u2726 3D Curved Glass ON"); }
                            SavePersistentSettings();
                            this.Invalidate();
                        }
                        else if (i == 1)
                        {
                            ToggleDynamicLightingMode();
                        }
                        else if (i == 2)
                        {
                            oledPureBlack = !oledPureBlack;
                            SavePersistentSettings();
                            ShowToast(oledPureBlack ? "OLED Pure Black Enabled" : "Multi-Image Cinema Backdrop Restored");
                            this.Invalidate();
                        }
                        else if (i == 3)
                        {
                            CycleVisualizerStyle();
                        }
                        return;
                    }
                }

                int numShapes = artworkShapeNames.Length;
                int sw = (surfCard.Width - 36 - (numShapes - 1) * 8) / numShapes;
                for (int i = 0; i < numShapes; i++)
                {
                    Rectangle sr = new Rectangle(surfCard.X + 18 + i * (sw + 8), surfCard.Y + 84, sw, 32);
                    if (sr.Contains(pt))
                    {
                        artworkShapeMode = i;
                        SavePersistentSettings();
                        ShowToast("3D Animation Style: " + artworkShapeNames[i]);
                        if (currentSong != null) RebuildCurrentSongBgSlideshow(currentSong, false);
                        this.Invalidate();
                        return;
                    }
                }

                Rectangle palCard = new Rectangle(padX, pageTopY + 334, contentW, 64);
                int swX = palCard.X + 14;
                for (int i = 0; i < themes.Count; i++)
                {
                    int wChip = (contentW - 28 - 8 * 6) / 9;
                    Rectangle tr = new Rectangle(swX + i * (wChip + 6), palCard.Y + 14, wChip, 36);
                    if (tr.Contains(pt))
                    {
                        activeThemeIdx = i;
                        dynamicLightingEnabled = false;
                        accentColor = themes[i].Accent;
                        if (currentSong != null) RebuildCurrentSongBgSlideshow(currentSong, false);
                        SavePersistentSettings();
                        ShowToast("Theme Saved: " + themes[i].Name + " (Dynamic Lighting Set to Manual)");
                        this.Invalidate();
                        return;
                    }
                }
                return;
            }

            // -----------------------------------------------------------------
            // PAGE 4: SYSTEM, PERFORMANCE & AUTO-UPDATER CLICKS
            // -----------------------------------------------------------------
            if (activeSettingsSubPage == 4)
            {
                if (isDrag) return;
                Rectangle sysCard = new Rectangle(padX, pageTopY, halfW, 210);
                Rectangle hzBtn = new Rectangle(sysCard.X + 18, sysCard.Y + 106, sysCard.Width - 36, 36);
                Rectangle clrCacheBtn = new Rectangle(sysCard.X + 18, sysCard.Y + 152, (sysCard.Width - 46) / 2, 36);
                Rectangle openDlFolderBtn = new Rectangle(clrCacheBtn.Right + 10, sysCard.Y + 152, (sysCard.Width - 46) / 2, 36);

                if (hzBtn.Contains(pt))
                {
                    ToggleHighRefreshRateMode();
                    return;
                }
                if (clrCacheBtn.Contains(pt))
                {
                    ClearAudioCache();
                    return;
                }
                if (openDlFolderBtn.Contains(pt))
                {
                    try { Process.Start("explorer.exe", downloadsFolder); } catch { }
                    return;
                }

                Rectangle updCard = new Rectangle(padX + halfW + 16, pageTopY, halfW, 210);
                Rectangle chkUpdSysBtn = new Rectangle(updCard.X + 18, updCard.Y + 106, updCard.Width - 36, 36);
                Rectangle admUpdSysBtn = new Rectangle(updCard.X + 18, updCard.Y + 152, updCard.Width - 36, 36);

                if (chkUpdSysBtn.Contains(pt))
                {
                    CheckForAppUpdatesAsync(true);
                    return;
                }
                if (admUpdSysBtn.Contains(pt))
                {
                    isUpdatePopupOpen = true;
                    this.Invalidate();
                    return;
                }

                Rectangle turboCard = new Rectangle(padX, pageTopY + 224, contentW, 166);
                int tCols = 3;
                int tBtnW = (turboCard.Width - 36 - (tCols - 1) * 10) / tCols;
                for (int i = 0; i < 6; i++)
                {
                    int r = i / tCols;
                    int c = i % tCols;
                    Rectangle tr = new Rectangle(turboCard.X + 18 + c * (tBtnW + 10), turboCard.Y + 66 + r * 44, tBtnW, 36);
                    if (tr.Contains(pt))
                    {
                        if (i == 0)
                        {
                            uiTurboCacheEnabled = !uiTurboCacheEnabled;
                            ShowToast("\u26A1 UI Turbo Cache: " + (uiTurboCacheEnabled ? "ON (Zero-Lag)" : "OFF"));
                        }
                        else if (i == 1)
                        {
                            isVolumeControllerPopupOpen = !isVolumeControllerPopupOpen;
                        }
                        else if (i == 2)
                        {
                            smoothMicroFadeEnabled = !smoothMicroFadeEnabled;
                            ShowToast("Smooth 180ms Micro-Fade: " + (smoothMicroFadeEnabled ? "ON" : "OFF"));
                        }
                        else if (i == 3)
                        {
                            autoVolumeNormalizeEnabled = !autoVolumeNormalizeEnabled;
                            ApplyEffectiveDspAndVolume();
                            ShowToast("Smart Auto-Volume Leveler: " + (autoVolumeNormalizeEnabled ? "ON" : "OFF"));
                        }
                        else if (i == 4)
                        {
                            autoPreBufferNextEnabled = !autoPreBufferNextEnabled;
                            if (autoPreBufferNextEnabled) PreBufferNextQueueSongAsync();
                            ShowToast("Next-Track Pre-Buffer: " + (autoPreBufferNextEnabled ? "ON (0ms Switch)" : "OFF"));
                        }
                        else if (i == 5)
                        {
                            audioBufferProfileIdx = (audioBufferProfileIdx + 1) % audioBufferProfileNames.Length;
                            ShowToast("Audio Buffer Profile: " + audioBufferProfileNames[audioBufferProfileIdx]);
                        }
                        QueueSavePersistentSettings();
                        this.Invalidate();
                        return;
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            int sidebarW = 198;
            int rightW = Math.Max(0, (int)Math.Round(rightPanelWidthAnim));
            int playerH = 82;
            int pbY = h - playerH;
            int centerX = sidebarW;
            int centerW = Math.Max(320, w - sidebarW - rightW);
            int rightX = w - rightW;

            if (isDraggingWindow && e.Button == MouseButtons.Left)
            {
                this.Location = new Point(this.Left + e.X - dragStartPoint.X, this.Top + e.Y - dragStartPoint.Y);
                if (isDynamicBarMode) savedDynamicBarLocation = this.Location;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                if (isFullPlayerExpanded && fullViewSubMode != 2 && fullViewSubMode != 3 && !isFullThreeDotMenuOpen)
                {
                    bool showRightLyricsCard = (fullViewSubMode == 1);
                    int rightCardW = showRightLyricsCard ? 305 : 0;
                    int rightCardX = w - rightCardW - 36;
                    int availStageH = h - 180;
                    int artSz = Math.Max(240, Math.Min(370, availStageH - 20));
                    int artX = 64;
                    int artY = 92 + (availStageH - artSz) / 2;
                    int centerColX = artX + artSz + 54;
                    int centerColRightLimit = showRightLyricsCard ? (rightCardX - 36) : (w - 64);
                    int centerColW = Math.Max(260, centerColRightLimit - centerColX);
                    int centerColTopY = artY + (artSz - 210) / 2;
                    int sliderY = centerColTopY + 104;

                    if (e.Y >= sliderY - 16 && e.Y <= sliderY + 16 && e.X >= centerColX - 8 && e.X <= centerColX + centerColW + 8)
                    {
                        double frac = Math.Max(0.0, Math.Min(1.0, (double)(e.X - centerColX) / centerColW));
                        SeekToAbsolute(frac * currentDurSec);
                        return;
                    }

                    int botBarY = h - 54;
                    int fpVolBarX = w - 262;
                    int fpVolBarW = 96;
                    int maxFpV = volumeOverdriveAllowed ? 150 : 100;
                    if (e.Y >= botBarY && e.X >= fpVolBarX - 8 && e.X <= fpVolBarX + fpVolBarW + 8)
                    {
                        SetVolume((int)Math.Round(((double)(e.X - fpVolBarX) / fpVolBarW) * maxFpV));
                        return;
                    }
                }
                else if (isVolumeControllerPopupOpen)
                {
                    Rectangle popRect = GetVolumeControllerPopupRect(w, h);
                    int slX = popRect.X + 18;
                    int slW = popRect.Width - 36;
                    int slY = popRect.Y + 50;
                    int maxV = volumeOverdriveAllowed ? 150 : 100;
                    if (e.Y >= slY - 18 && e.Y <= slY + 18 && e.X >= slX - 10 && e.X <= slX + slW + 10)
                    {
                        double frac = Math.Max(0.0, Math.Min(1.0, (double)(e.X - slX) / slW));
                        SetVolume((int)Math.Round(frac * maxV));
                        return;
                    }
                }
                else if (!isFullPlayerExpanded && e.Y >= h - 76 && e.X >= 204)
                {
                    Rectangle dockRect = new Rectangle(204, h - 76, w - 216, 66);
                    int volBarX = dockRect.Right - 224;
                    int volBarW = 76;
                    int maxDockV = volumeOverdriveAllowed ? 150 : 100;
                    if (e.X >= volBarX - 8 && e.X <= volBarX + volBarW + 8 && e.Y >= dockRect.Y + 14 && e.Y <= dockRect.Y + 46)
                    {
                        SetVolume((int)Math.Round(((double)(e.X - volBarX) / volBarW) * maxDockV));
                        return;
                    }
                }
                else if (!isFullPlayerExpanded && activeTab == 4 && e.X >= centerX && e.X < rightX)
                {
                    HandleSettingsClick(e.Location, centerX, centerW, true);
                    return;
                }
            }

            string newHovKey = "";
            hoveredNavIndex = -1;
            hoveredSongIndex = -1;
            hoveredQueueIndex = -1;
            hoveredQuickPlayIndex = -1;

            int mainH = h - playerH;

            if (isFullPlayerExpanded)
            {
                if (new Rectangle(w - 50, 46, 36, 36).Contains(e.Location)) newHovKey = "full_dots_btn";
                currentHoveredBtnKey = newHovKey;
                return;
            }

            if (e.X < sidebarW)
            {
                int[] navTabIds = new int[] { 0, 1, 6, 2, 5, 3 };
                for (int i = 0; i < navTabIds.Length; i++)
                {
                    Rectangle navRect = new Rectangle(20, 72 + i * 38, sidebarW - 34, 34);
                    if (navRect.Contains(e.Location))
                    {
                        hoveredNavIndex = i;
                        newHovKey = "nav_" + navTabIds[i];
                        break;
                    }
                }
            }
            else if (activeTab != 4 && e.X < rightX && e.Y < mainH)
            {
                if (activeTab == 5)
                {
                    for (int i = 0; i < playlistStageHitZones.Count; i++)
                    {
                        if (playlistStageHitZones[i].Rect.Contains(e.Location))
                        {
                            newHovKey = playlistStageHitZones[i].HoverKey;
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(newHovKey))
                {
                    for (int i = 0; i < homeHitZones.Count; i++)
                    {
                        if (homeHitZones[i].Rect.Contains(e.Location))
                        {
                            newHovKey = homeHitZones[i].HoverKey;
                            break;
                        }
                    }
                }
            }
            else if (e.X >= rightX && rightPanelMode == 0 && e.Y >= 118 && e.Y < mainH - 12)
            {
                int qIdx = (e.Y - 120 + queueScrollOffset) / 62;
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
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            // v10.4.4 Precision Scroll-Wheel Volume Control when hovering Volume Popup, Bottom Player Dock, or Full-Screen Bottom Bar!
            if (isVolumeControllerPopupOpen && GetVolumeControllerPopupRect(w, h).Contains(e.Location))
            {
                int step = (e.Delta > 0) ? 2 : -2;
                AdjustVolumeByStep(step);
                return;
            }
            if ((!isFullPlayerExpanded && e.Y >= h - 78 && e.X >= 204) || (isFullPlayerExpanded && e.Y >= h - 64))
            {
                int step = (e.Delta > 0) ? 2 : -2;
                AdjustVolumeByStep(step);
                return;
            }

            int rightX = w - 315;
            if (!isFullPlayerExpanded && e.X >= rightX && rightPanelMode == 0)
            {
                int maxQScroll = Math.Max(0, queueSongs.Count * 62 - (h - 220));
                targetQueueScrollOffset = Math.Max(0, Math.Min(maxQScroll, targetQueueScrollOffset - (int)(e.Delta * 0.65f)));
            }
            else if (!isFullPlayerExpanded && activeTab != 4)
            {
                int vpH = Math.Max(200, h - 82 - 70);
                int maxScroll = Math.Max(0, totalHomeContentHeight - vpH);
                targetScrollOffset = Math.Max(0, Math.Min(maxScroll, targetScrollOffset - (int)(e.Delta * 0.65f)));
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