&lt;div align=&quot;center&quot;&gt;
  &lt;img src=&quot;assets/leo_logo_64.png&quot; alt=&quot;Leo Music Studio Logo&quot; width=&quot;84&quot; height=&quot;84&quot; /&gt;
  &lt;h1&gt;Leo Music Studio&lt;/h1&gt;
  &lt;p&gt;&lt;strong&gt;Native Go GPU Desktop Music &amp;amp; Music-Video Studio&lt;/strong&gt;&lt;br/&gt;
  Built with Material 3 Liquid Glass, Dual Song Engine (YouTube HD + 320 kbps Failover), Native 1080p Music Video, 4-Cue Binaural 8D Orbital 360°, and 6 Research-Backed Motion Physics Engines.&lt;/p&gt;

  &lt;p&gt;
    &lt;a href=&quot;https://github.com/LGM356/leo-music/releases&quot;&gt;&lt;img src=&quot;https://img.shields.io/github/v/release/LGM356/leo-music?style=for-the-badge&amp;color=7c4dff&amp;label=Latest%20Release&quot; alt=&quot;Latest Release&quot; /&gt;&lt;/a&gt;
    &lt;a href=&quot;https://github.com/LGM356/leo-music/releases&quot;&gt;&lt;img src=&quot;https://img.shields.io/github/downloads/LGM356/leo-music/total?style=for-the-badge&amp;color=00bfa5&amp;label=Downloads&quot; alt=&quot;Downloads&quot; /&gt;&lt;/a&gt;
    &lt;img src=&quot;https://img.shields.io/badge/Go-Native%20GPU%20UI-00ADD8?style=for-the-badge&amp;logo=go&amp;logoColor=white&quot; alt=&quot;Go Native GPU UI&quot; /&gt;
    &lt;img src=&quot;https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-222?style=for-the-badge&quot; alt=&quot;Platform&quot; /&gt;
  &lt;/p&gt;

  &lt;p&gt;
    &lt;a href=&quot;https://github.com/LGM356/leo-music/releases&quot;&gt;Download Latest&lt;/a&gt; •
    &lt;a href=&quot;#screenshots&quot;&gt;Screenshots&lt;/a&gt; •
    &lt;a href=&quot;#key-features&quot;&gt;Features&lt;/a&gt; •
    &lt;a href=&quot;#youtube-audio--video-pipeline&quot;&gt;Architecture&lt;/a&gt; •
    &lt;a href=&quot;#quick-start--1-click-build&quot;&gt;Quick Start&lt;/a&gt; •
    &lt;a href=&quot;https://github.com/LGM356/leo-music/issues/new&quot;&gt;Report an Issue&lt;/a&gt;
  &lt;/p&gt;
&lt;/div&gt;

---

## Screenshots

### Home Studio — Speed Dial, Quick Picks & Liquid Glass Dock


### Now Playing — Native 1080p Music Video, Wavy Scrubber & Live Up Next Queue


### Floating Liquid Glass Player Bar


### Leo Studio Pro — Liquid Glass Blur & Optical Customization


| Motion Physics Engine | Studio Audio DSP Effects |
| :---: | :---: |
|  |  |

---

## Key Features

### Dual Song Engine & Resilient Streaming
- **YouTube HD + 320 kbps Automatic Failover**: Plays directly via YouTube Music InnerTube (`WEB_REMIX` / `visionOS`) with `yt-dlp` fallback and automatic failover to the built-in 320 kbps Leo Engine (`JioSaavn`) if a stream drops.
- **Wi-Fi Drop Protection**: 6 MB RAM read-ahead PCM ring buffer (~32 seconds of uncompressed audio) keeps music playing smoothly through brief network hiccups.
- **3-Song Smart Preload**: Pre-resolves and caches upcoming tracks over pooled HTTP/2 connections for instant track switching.

### Native Original Music Video Player
- **In-App 480p / 720p / 1080p Playback**: Automatically matches the official music video for any playing track and streams audio-locked frames directly onto the GPU.
- **Edge-to-Edge Full-Screen Mode**: Floating controls, quality switcher, and seamless toggle between Studio Audio and Music Video without interrupting playback.

### Studio Audio DSP Suite (48 kHz Real-Time PCM)
- **8D Orbital 360° — Leo Special (Binaural HRTF)**: 4-cue spatial engine combining Interaural Time Difference (ITD) micro-delay, front/back pinna head-shadow filtering, equal-power azimuth panning, and asymmetric 3D room reflections with live **Orbit Speed** and **Room Depth** controls.
- **Spotify-Style Overlap Crossfade**: Equal-power cosine/sine end-of-track overlap mixing (zero self-crossfade when seeking).
- **10-Band Parametric EQ & Bit-Exact Pass-Through**: Smooth animated EQ transitions with a bit-exact flat pass-through fast path when EQ and DSP are off.
- **Clean Studio Effects**: Smooth envelope-following **Night Cinema Dynamic Limiter**, bass-preserving **Instant Karaoke Vocal Cut**, **Analog Vinyl Warmth**, **Cathedral Hall Stereo Echo**, and **Instant Skip Silence**.

### Liquid Glass UI & 6 Motion Physics Engines
- **6 Research-Backed Spring Engines**: Switch live between **Liquid Jelly** (default), **M3 Expressive**, **Fluid Rubber**, **Hyper-Bounce**, **Magnetic Pull**, and **Zero-G Silk** — combining asymmetric press/release springs, volume-preserving squash-and-stretch, cursor-reactive tilt, and hover-only magnetic pull at 0% idle GPU.
- **Satin Liquid Glass (0–100 Blur + 5 Styles)**: Smooth blur intensity slider (`Frost`, `Gradient`, `Forest`, `Ocean`, `Sunset`) with specular rim highlights and high-contrast controls.
- **Ultra-Fast Lite Mode**: One-click toggle that disables glass effects and animations for older laptops and maximum battery life.

### Deep Optimization & Security Hardening
- **Low-RAM Runtime & FFmpeg Tuning**: Configured with Go's soft heap cap (`GOMEMLIMIT=140 MiB`, `GOGC=35`, periodic `FreeOSMemory`), lean artwork/video caches, and low-thread FFmpeg decoding (`-threads 1` audio / `-threads 2 -filter_threads 1` video).
- **Hardened Networking & Local Sync**: Enforces TLS 1.2+ with LRU session caching and shared HTTP/2 connection pools across all outbound clients, restricts **Leo Connect** LAN Sync to private/loopback IPs with strict server timeouts, validates GitHub HTTPS domains on auto-updates, and sanitizes all `yt-dlp` / `ffmpeg` arguments.
- **Windows Dedicated GPU Selector**: Built-in Windows `UserGpuPreferences` auto-high and manual GPU preference controls.

### Ecosystem & Power-User Tools
- **Leo Connect (LAN Sync)**: Discover and pair devices on your local network to hand off playback, sync queues, and share Continue Listening podcast progress.
- **AI Control (MCP)**: Built-in `--mcp` Model Context Protocol bridge for controlling playback from AI assistants.
- **GitHub Auto-Updater**: Checks [`LGM356/leo-music`](https://github.com/LGM356/leo-music/releases) automatically for new releases.

---

## YouTube Audio & Video Pipeline

1. **Metadata & Discovery**: Queries YouTube Music InnerTube (`music.youtube.com`, `WEB_REMIX`) over a pooled TLS 1.2+ HTTP/2 transport while caching track metadata for Dual-Engine failover.
2. **Audio Resolution**:
   - Uses signed-in `yt-dlp` (with `0600` temporary cookies) when Account Audio is enabled, or fast anonymous InnerTube (`visionOS`) extraction selecting the highest-bitrate default-language non-DRC `audio/*` stream verified with a `Range: bytes=0-0` probe.
   - Falls back cleanly through JS-less `yt-dlp`, solver `yt-dlp`, and the 320 kbps JioSaavn engine.
3. **Real-Time Audio Output**: `ffmpeg` decodes to 48 kHz 16-bit stereo PCM → RAM Read-Ahead Buffer → Skip Silence → 10-Band EQ + 8D Binaural HRTF + Studio DSP → Equal-Power Crossfade → `ebitengine/oto/v3`.
4. **Video Sync**: `yt-dlp` resolves a direct MP4/WebM stream (`youtube:skip=hls`), `ffmpeg` decodes audio-muted (`-an`) 24 fps RGBA frames into a lean 12-frame buffer, and the GPU renderer locks each frame's PTS to the live audio clock.

---

## Quick Start & 1-Click Build

### Option 1: Download Prebuilt Release
Grab the latest package from the [Releases page](https://github.com/LGM356/leo-music/releases).

### Option 2: 1-Click Windows Build & Launch
1. Clone or download this repository:
   ```bash
   git clone https://github.com/LGM356/leo-music.git
   cd leo-music
   ```
2. Double-click **`Build_And_Run_Leo_v11.bat`** — it automatically checks/sets up Go, `ffmpeg`, and `yt-dlp`, compiles `LeoMusicStudio.exe`, creates the **`Leo Music`** Desktop shortcut with the Leo logo, and launches the app.

### Option 3: Manual Build (Windows / macOS / Linux)
Requires **Go 1.24+**, **`ffmpeg`**, and **`yt-dlp`** on `PATH`:
```bash
go mod tidy
go build -o LeoMusicStudio .
./LeoMusicStudio
```

---

## Keyboard Shortcuts

| Action | Shortcut |
| :--- | :--- |
| Play / Pause | `Space` |
| Next / Previous Track | `Shift + N` / `Shift + P` |
| Seek -10s / +10s | `Left` / `Right` |
| Volume Up / Down | `Up` / `Down` |
| Mute / Unmute | `M` |
| Toggle Full-Screen | `F` |
| Speed Dial Instant Play | `Alt + 1` – `Alt + 9` |
| Search | `/` or `Ctrl + K` |

---

## Updates & Support

- **Repository**: [https://github.com/LGM356/leo-music](https://github.com/LGM356/leo-music)
- **Releases**: [https://github.com/LGM356/leo-music/releases](https://github.com/LGM356/leo-music/releases)
- **Bug Reports & Feature Requests**: [Open an Issue](https://www.google.com/search?q=https%3A%2F%2Fgithub.com%2FLGM356%2Fleo-music%2Fissues%2Fnew)
