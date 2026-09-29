# Duettino — Project brief

> English translation of the original Italian brief. Where it disagrees with GLOSSARY.md or docs/adr/, those win.

> Starting document, the outcome of a brainstorming session. It is the input for
> `/grill-with-docs` (to derive `GLOSSARY.md` and the ADRs), then `/to-spec` → `/to-tickets`.
> No code has been written yet.

## 0. Name

**Duettino**: Italian for "a little duet", i.e. the two Sources (what you say and what you hear)
together, in a small, lightweight app.

- **Repo:** `duettino`
- **GitHub description:** *Lightweight Windows recorder: mix any input device with whatever is
  playing on any output device into one MP3. Calls, meetings, videos, anything. No drivers.*
  The feature is generic; calls are the main example (and the word people search for),
  not the only use. The README will give the full explanation (see §11).
- **Why not "Duetto":** there are 85 GitHub repositories with that name (almost all
  irrelevant), but above all Google is dominated by [Duetto Research](https://www.duettocloud.com/en-us/),
  a hotel software company. The name would be impossible to find and there would be a small
  risk of overlapping with their trademark. "Duettino" was free (0 repositories) in September 2026.
- **Discarded:** *Earshot* (already used by a commercial app that does almost the same thing,
  [tryearshot.app](https://tryearshot.app/)), *BothSides*, *HeadsetRec*, *TwoWay*.

## 1. Problem

I want to record the work calls (Teams, Zoom, Meet, …) I make from a Windows PC.

- **Without headphones** it is easy: the voices come out of the speakers and a phone recording
  the room is enough.
- **With headphones** it becomes impossible: the other people's voices only come out of the
  headphones, and no external recorder can hear them.

I need a **simple, lightweight** app that records **an Input device of my choice** (usually
the microphone) together with **whatever is playing on an Output device of my choice**, into
a single audio file.

Headphones are the case that created the need, but they are **not a requirement**: the Output
can also be the PC speakers, an HDMI monitor, a USB dock…, and the Input any microphone, even
on a different device from the Output.

Calls are also just the original case: the app records **anything** playing on the Output
together with the Input, whether you are on a call or not. Other uses: webinars and online
lessons, commentated videos, gaming sessions with your own voice, music practice over a
backing track, remote podcasts.

## 2. Alternatives evaluated (September 2026)

| Tool | Why not |
|---|---|
| OBS Studio | Does the job, but it is very heavy and full of features we don't need. |
| Xbox Game Bar | Awkward; geared towards recording games/video. |
| [Audacity](https://windowsforum.com/news/record-windows-11-system-audio-in-audacity-with-wasapi-loopback.415374/) | Captures WASAPI loopback, but **only one device at a time**: it doesn't mix mic + output. |
| [AudioCapture](https://github.com/masonasons/AudioCapture) | The closest (mic + system audio in one file), but no ready-made binary, you have to build it, ~40 stars, does much more than needed (per-process capture). |
| [Reco](https://github.com/dosxnjos/reco) | Mic + loopback to MP3, but ~810 MB because of the built-in Whisper transcription. |
| [teamsrec-capture](https://github.com/drzdez/teamsrec-capture) | Tied to Teams, niche project. |
| [wasamix](https://github.com/ytchenak/wasamix) | Mixes into a virtual cable, doesn't record to a file. |
| ScreenSnap Pro, EaseUS RecExperts | Commercial, screen-oriented. |
| Teams/Zoom built-in recording | Notifies everyone, ends up on OneDrive/cloud, not always available. |

**Conclusion:** no solution is simple, lightweight and ready to use all at once → we build it.

## 3. Decisions taken

Candidates to become ADRs during `/grill-with-docs`.

1. **Stack: C# / .NET 10 + WinForms + NAudio.**
   Native executable, instant startup, lightweight. NAudio is the reference audio library
   on .NET and already exposes both microphone capture (WASAPI) and Loopback capture.
   The .NET 10 SDK is already installed on the machine. Python + tkinter discarded (packaging
   with PyInstaller is heavier and slower to start).
2. **No virtual driver.** We use Windows' native **WASAPI loopback** to "listen" to the
   Output device. No Stereo Mix, no VB-Cable.
3. **UI: a single small window**, no tray for now.
   ```
   ┌─ Duettino ─────────────────────┐
   │ Input:    [Webcam mic       ▼] │
   │  ▮▮▮▮▮▯▯▯▯▯                    │
   │ Output:   [Realtek speakers ▼] │
   │  ▮▮▮▯▯▯▯▯▯▯                    │
   │                                │
   │   [ ● Record ]      00:12:34   │
   │ Save to: Documents\Recordings  │
   └────────────────────────────────┘
   ```
4. **Recording file: a single MP3**, 48 kHz stereo, 128 kbps (~1 MB/min, ~60 MB/hour).
5. **The two voices mixed together** in the same audio (not separate channels, not separate files).
6. **MP3 encoding via Windows Media Foundation** (the system MP3 encoder, exposed
   by NAudio), so no extra native dependency such as LAME.
7. **Input and Output free and independent.** Any active capture device as the Input and
   any active playback device as the Output, without assuming they are the same piece of
   hardware (USB headset, speakers + webcam microphone, etc.).
8. **WAV Working file during the Recording**, converted to MP3 on Stop.
   Reason: crash resilience. If the app or the PC crashes, the WAV up to that point
   remains recoverable. The price is temporary disk space (~10 MB/min).
9. **UI written in code, without the WinForms visual designer.** The window is tiny
   (two dropdowns, two meters, one button, a few labels): a layout in code
   (e.g. `TableLayoutPanel`) is more readable, easier to change, also for an agent, and
   doesn't depend on the designer. Reference IDE: **JetBrains Rider**, whose WinForms
   designer on modern .NET projects is less reliable than Visual Studio's.
   Build, test and publish all go through the `dotnet` CLI. Visual Studio is installed and
   remains available if needed (see §12 for MSIX packaging).

## 4. How it works (pipeline)

```
 Input ──(WASAPI capture)────► buffer ─► convert format ─┐
                                                         ├─► MIX ─► Working file (WAV)
 Output ─(loopback capture)──► buffer ─► convert format ─┘                 │
                                                                           ▼ (on Stop)
                                                                   conversion → MP3
```

1. **Two captures in parallel:** the chosen Input (WASAPI capture) and the chosen Output
   (Loopback capture).
2. **Format conversion:** the two Sources almost always arrive in different formats
   (e.g. Input mono 16 kHz, Output stereo 48 kHz, sometimes 5.1/7.1 or 4-channel mic arrays).
   Both are brought to 48 kHz stereo float (resampling + channel downmix/upmix).
3. **Mix:** sum of the two Sources, with controlled clipping when going to 16 bit.
4. **Writing:** a clock-paced "engine" pulls from the Mix in real time and writes
   the Working file. The WAV header is updated periodically (flush), so the file
   stays valid even after a crash.
5. **Finalization:** on Stop the WAV is converted to MP3 in the background (a one-hour call
   may take a few seconds), then the WAV is deleted. If the conversion fails, the WAV
   is kept.
6. **Level meters:** peak for each Source, updated ~10 times per second.

## 5. Known technical pitfalls

- **Loopback goes quiet during silence.** When no sound is coming out of the Output device,
  Windows doesn't deliver silence packets: *it delivers nothing*. If the Recording's pace
  were driven by incoming data, the two Sources would drift out of sync. We need an
  internal clock that drives the writing and fills the gaps with silence, with a small
  safety latency (~200–300 ms) to absorb jitter.
- **Clock drift.** Input and Output have different hardware clocks (all the more so
  if they are different devices): over an hour they can
  diverge by fractions of a second. The buffers must tolerate slight underruns (→ silence)
  and overflows (→ discard), without growing forever.
- **Bluetooth headsets and the "Hands-Free" profile.** On some setups, when the BT headset's
  mic activates, audio switches to a different endpoint ("Headset / Hands-Free")
  from the stereo one. If I record the wrong endpoint, I capture silence. Windows 11
  tends to unify them, but this must be checked with the real headset. Hence the idea of a
  "Default communication device" option (see open questions).
- **Echo with speakers.** If the Output is the speakers, the Input (microphone) also picks up
  the other people's voices coming out of the speakers: in the Mix those voices appear **twice**,
  once from the loopback and once from the microphone a few tens of ms later, producing an
  echo. The call app cancels echo only on *its own* stream, not on the one we capture.
  Possible answers: accept it (it stays understandable), use Windows' "communications" mode
  to get system echo cancellation where the driver offers it, or our own echo cancellation
  (e.g. WebRTC AEC or SpeexDSP): **the loopback is exactly the reference signal** the
  algorithm needs. See open questions.
- **The call app may use an output other than the default one.** The user must
  choose the Output that Teams/Zoom actually uses.
- **Device unplugged during a Recording** (jack pulled out, BT dropping): the
  capture stops with an error → the app must stop cleanly, save what it has and
  warn the user.
- **"Extensible" formats.** Devices' mix format is often declared as
  *WaveFormatExtensible* (32-bit float) and must be recognised and handled correctly.
- **Windows MP3 encoder:** it accepts 16-bit PCM at 44.1/48 kHz, so the Working file
  must be written in that format.
- **"Microphone in use" icon.** If the meters were always active (even when not
  recording), Windows would continuously show the microphone indicator. Current
  decision: meters active **only while recording**.

## 6. Proposed glossary (seed for `GLOSSARY.md`)

- **Recording**: a session between "Record" and "Stop", producing a Recording file.
- **Input**: the chosen capture device; usually a microphone, but it can be any audio
  source (line-in, virtual cable…). _Avoid_: mic, microphone, mic source.
- **Output**: the chosen playback device whose sound is captured
  (headphones, speakers, HDMI…). _Avoid_: headphones, speakers, system audio.
- **Loopback capture**: capturing what is being played on an Output.
- **Mix**: the combination of Input and Output into a single audio stream.
- **Working file**: the WAV written during the Recording. _Avoid_: temp, buffer.
- **Finalization**: converting the Working file into the final MP3 Recording file.

## 7. Open questions (to tackle with `/grill-with-docs`)

1. **Relative volumes:** your own voice often ends up much louder or quieter than the
   others. Two gain sliders (Input / Output), yes or no? Automatic level matching,
   perhaps?
2. **Devices at startup:** preselect the Windows defaults or remember the last choice
   (settings file in `%AppData%`)? Add a "Default communications" entry to the dropdowns
   that follows Windows?
3. **Folder and file name:** proposal `Documents\Recordings\Call_AAAA-MM-GG_HH-mm.mp3`,
   with a changeable folder and an "Open folder" button. Do we want to be able to name the
   Recording (e.g. "call with client X") before or after?
4. **Closing during a Recording:** confirm + save? What happens to a Finalization
   in progress?
5. **Crash recovery:** at startup, does the app look for orphan Working files and offer to
   finalize them?
6. **Pause/resume:** needed?
7. **Distribution:** single-file *framework-dependent* executable (small, requires the
   .NET 10 Desktop Runtime) or *self-contained* (no prerequisites, ~70+ MB)?
   And do we target the Microsoft Store from v1 or later on? (see §12)
8. **UI language:** Italian only, or English too?
9. **Echo with speakers** (see §5): in v1 do we accept it, warn the user ("with speakers
   you may hear an echo: headphones are better"), try system echo cancellation, or
   implement our own using the loopback as the reference?

## 8. Out of scope (v1)

- Tray icon and global keyboard shortcuts.
- Automatic start when Teams/Zoom launches.
- Transcription, summaries, AI.
- Per-application (per-process) capture.
- Separate channels or files for the two voices (a possible future evolution: it's a
  small change in the mix stage).
- Video/screen recording.
- Support for systems other than Windows 10/11.

## 9. Testing and verification

**Proposed test seam:** cleanly separate the "devices" layer (enumeration and
WASAPI captures, thin and not automatically testable) from the **recording engine**
(format conversion, mix, clock, writing). The engine receives two abstract audio
streams and an injectable clock, so it can be tested with synthetic signals:

- different formats (mono/stereo/multichannel, 16/44.1/48 kHz, PCM/float) → consistent
  48 kHz stereo result;
- a Source that goes quiet (no data) → silence in the file, correct duration, no drift;
- simulated drift between the Sources over long durations → no unbounded buffer growth;
- a sum beyond full scale → clipping, no wrap-around;
- Working file still valid if the process is interrupted after a flush.

**Manual verification** (checklist with real hardware):

- real call with wired headphones and with Bluetooth headphones (check the Hands-Free endpoint);
- real call with the PC **speakers** and a separate microphone (how bad the echo is);
- Input and Output on different devices (e.g. webcam mic + USB headphones);
- long call (≥ 1 hour): voice sync at the end, file size, Finalization time;
- headphones unplugged mid-Recording;
- closing the app during a Recording.

## 10. How to proceed

0. **Reserve the name "Duettino" on the Microsoft Store** (Partner Center, free, see §12)
   before someone else takes it.
1. ~~**Create the repo**~~ done: `fakkio/duettino`, with skills, `AGENTS.md` and
   `docs/agents/` already configured (GitHub issue tracker, triage labels,
   `GLOSSARY.md` + `docs/adr/`).
2. **`/grill-with-docs`** on this brief: close the open questions (§7), produce
   `GLOSSARY.md` from the glossary (§6) and the ADRs from the decisions (§3).
3. **`/prototype`** (throwaway spike, recommended): a console app that records 30 seconds
   of Input + Output loopback to WAV. It serves to check right away, on real devices,
   the riskiest pitfalls: loopback silence, Bluetooth endpoints and echo with speakers.
4. **`/to-spec`**: v1 spec published as an issue.
5. **`/to-tickets`**: split it into vertical tickets. Suggested order:
   1. WinForms skeleton + device list;
   2. recording engine (format conversion + mix + clock) with tests;
   3. end-to-end recording to WAV;
   4. Finalization to MP3;
   5. level meters and timer;
   6. error handling (unplugged device, closing, recovery);
   7. persistent settings and packaging.
6. **`/implement`** / **`/tdd`** ticket by ticket.
7. **Public release** (see §11): first release with a downloadable binary on GitHub,
   then winget, then promotion.

## 11. Where to make it known

The audience is people with the same problem as ours who can't find answers. The rule is
**answer people who are searching, don't spam**: always disclose that the app is ours and
give the explanation first, then the link.

**Prerequisites before promoting:**
- a GitHub release with a ready-to-download `.exe` (no "build it yourself":
  that is exactly AudioCapture's flaw);
- a README with a **clear, detailed explanation** (what it does in one sentence: "records
  what you say and what you hear"; then how it works, who it's for, possible uses beyond
  calls), a 10-second GIF, a prominent "Download" and a FAQ section
  ("why not Stereo Mix?", "does it work with Bluetooth headphones?", "and with speakers?");
- for the SEO angle, the phrases people actually search for, in the README: *record Teams call
  with headphones*, *record mic and speakers at the same time*, *Audio Hijack for Windows*,
  *OBS alternative for audio only*.

**Where these questions are already being asked (answer there):**
- SuperUser / Stack Exchange: questions about *record microphone and system audio simultaneously*;
- Microsoft Q&A and Tech Community (threads on Teams recording and missing Stereo Mix);
- Tom's Guide forum (e.g. [this thread](https://forums.tomsguide.com/threads/recording-software-that-can-record-from-2-sound-outputs-plus-microphone-are-there-any.342760/post-1500656));
- Quora (e.g. [this question](https://www.quora.com/How-do-I-record-internal-and-external-audio-simultaneously-on-a-PC));
- Adobe Audition forums on "stereo mix + microphone";
- Reddit: look for existing threads in r/software, r/Windows11, r/techsupport, r/MicrosoftTeams, r/Zoom.

**Alternatives sites:**
- **AlternativeTo**: list Duettino as an alternative to OBS Studio, Audacity,
  Xbox Game Bar and above all **Audio Hijack**. Audio Hijack is Mac-only and many people
  look for the Windows equivalent: it's the strongest angle.

**Microsoft Store** (see §12): the Store listing is also a searchable showcase
("call recorder", "record Teams").

**One-command install:**
- **winget** (PR to `microsoft/winget-pkgs`): the most important channel, because it makes
  `winget install duettino` possible and gives credibility;
- **Scoop** (`extras` bucket) and **Chocolatey**.

**GitHub:**
- topics: `call-recorder`, `audio-recorder`, `wasapi`, `wasapi-loopback`, `naudio`,
  `teams`, `zoom`, `windows`, `dotnet`, `winforms`;
- submit it to curated lists (awesome-windows, awesome-dotnet and open-source audio software lists).

**Communities (launch post, once per place):**
- Reddit: r/software, r/opensource, r/Windows11, r/podcasting, r/csharp and r/dotnet
  (the technical angle here), r/ItalyInformatica;
- Hacker News "Show HN";
- Product Hunt.

**Freeware sites** (free listing, they bring traffic from Google):
- Softpedia, MajorGeeks, Neowin (software news), FileHorse.

**Technical content** (brings visits over time):
- an article on dev.to / Medium / Hashnode: *"Windows WASAPI loopback goes silent when nothing
  plays, and how to mix it with the mic without drift"*. It's the project's non-obvious
  technical problem and brings organic visits from developers;
- possibly an answer on Stack Overflow to questions about NAudio loopback + mixing, with
  a link to the code.

## 12. Publishing on the Microsoft Store

**Yes, it can be published.** The Store accepts Win32/.NET desktop apps like Duettino in two ways:

| | **MSIX package** (recommended) | **EXE/MSI installer** |
|---|---|---|
| Code signing | **The Store does it for free** | Requires a paid code-signing certificate (hundreds of €/year) |
| Where the file lives | Uploaded to the Store | Hosted by us (versioned HTTPS URL) |
| Updates | Automatic via the Store | Managed by us |
| Install/uninstall | Clean, isolated | Depends on the installer |

**Choice: MSIX.** It avoids the certificate cost and also removes the SmartScreen warning
("unrecognized app") that instead hits the unsigned `.exe` downloaded from GitHub.

**What it involves:**
- **Developer account:** free for individuals (since September 2024 Microsoft has dropped
  the registration fee for individual developers). To be verified at sign-up time.
- **Reserve the name** "Duettino" right away in Partner Center: the reservation is free.
- **Self-contained package:** the Store has no .NET 10 Desktop runtime package
  to depend on, so the package must include it (~70+ MB, acceptable).
- **`microphone` capability** declared in the manifest. As a packaged app, Duettino
  appears in *Settings → Privacy → Microphone* with its own toggle: the app must
  handle the "microphone access denied" case well, with a clear message. Loopback
  capture of the Output doesn't require a capability.
- **Full trust** (`runFullTrust`): normal for packaged desktop apps; WASAPI and
  loopback work unchanged.
- **Privacy policy** required (the app uses the microphone): a simple page is enough
  (e.g. GitHub Pages or a file in the repo) stating that everything stays local,
  no network, no telemetry.
- **Age rating questionnaire**, screenshots and description for the listing.

**How to produce the MSIX** (decision to be taken, ADR candidate):
- **Visual Studio packaging project** (`.wapproj`): the simplest and most guided, but
  it's Visual Studio-specific and doesn't build with the `dotnet` CLI alone. This is the
  case where Visual Studio comes in handy.
- **Windows SDK `makeappx` + hand-written manifest**: written once, scriptable and
  also runs in GitHub Actions. It's the option consistent with "everything from the CLI"
  and with the agentic workflow.
- Proposal: scripted `makeappx`; Visual Studio only as a fallback.

**Dual channel:**
- **Store**: for regular users (MSIX, automatic updates, no security warnings);
- **GitHub Releases + winget**: portable `.exe` for those who don't use the Store. winget can
  also install directly from the `msstore` source.

**When:** not necessarily from v1. Proposal: v1 on GitHub Releases, Store from the
first "stable" version after some real-world use. The name, however, must be reserved right away.

## 13. Open questions resolved (`/grill-with-docs`, 2026-09-29)

The §7 questions, closed. Decisions that qualified became ADRs in `docs/adr/`; deferred
features went to `docs/IDEAS.md`; vocabulary went to `GLOSSARY.md`.

1. **Relative volumes:** the Working file keeps the two Sources unmixed (3 channels:
   Input mono + Output stereo); Leveling and the Mix happen at Finalization (ADR-0004).
   Leveling is automatic, measured only where a Source is actually active, with the
   boost capped (e.g. +12 dB) so a silent Input's room noise isn't blown up. No sliders,
   no on/off switch in v1.
2. **Devices at startup:** remember the last Input and Output by device ID (settings in
   `%AppData%`); on first run, or if a remembered device is gone, fall back to the Windows
   default (multimedia) devices and show that the fallback happened. No "Default
   communications" entry: Windows 11 unified the Bluetooth stereo and Hands-Free endpoints.
3. **Folder and file name:** default folder `Documents\Duettino`, changeable, with an
   "Open folder" button; file name `Duettino_YYYY-MM-DD_HH-mm-ss.mp3` (a `_2` suffix on
   collision; no `Call_` prefix, calls aren't the only use). No naming of a Recording in v1.
   The Working file sits in the same folder as `<name>.working.wav`.
4. **Closing:** during a Recording, ask "Stop and save?", then stop, finalize and close.
   During Finalization, keep the window open ("Saving…") and close when done; closing again
   exits and leaves an Orphan Working file. On Windows shutdown/logoff, stop without asking,
   finalize the WAV header and exit, leaving an Orphan Working file.
5. **Crash recovery:** at startup, for each Orphan Working file, offer
   "Recover / Delete / Later".
6. **Pause/resume:** not in v1 (IDEAS).
7. **Distribution:** self-contained single-file (compressed, ≈50 MB) on every channel;
   v1 on GitHub Releases and winget, Store from the first stable release (ADR-0007).
   Without the Windows MP3 encoder (N editions), Finalization writes a stereo WAV
   (ADR-0003).
8. **UI language:** English and Italian from v1, following the Windows display language,
   English as fallback.
9. **Echo with speakers:** accepted in v1, no in-app warning (Windows can't reliably tell
   speakers from headphones); a README FAQ entry covers it. Offline echo cancellation at
   Finalization is in IDEAS, pending the `/prototype` measurements.

## Legal note

In Italy, recording a conversation you take part in is generally lawful for personal use;
distributing it is another matter. The app doesn't need to do anything about it, but
it's good to know.
