# PROTOTYPE findings: loopback spike (interim, updated after each test)

Machine: Windows 11 Pro 26200, .NET 10.0.12, NAudio 3.1.0. Run reports live in `runs/` (git-ignored, local only).

## Devices tried

| Role | Device | Mix format | Form factor / bus |
|---|---|---|---|
| Input | Microphone Array (Intel Smart Sound Technology) | 48 kHz, 2 ch, 32-bit float, Extensible, mask 0x3 | Microphone / INTELAUDIO |
| Output | Altoparlanti (Realtek Audio) | 48 kHz, 2 ch, 32-bit float, Extensible, mask 0x3 | Speakers / INTELAUDIO |
| Output | Headphones (BD86), Bluetooth earbuds | 48 kHz, 2 ch, 32-bit float, Extensible, mask 0x3 | Headphones / INTELAUDIO |
| Input (listed, not yet used) | Headset (BD86), earbuds mic | 48 kHz, 2 ch, 32-bit float, **plain IeeeFloat (not Extensible)** | Headset / INTELAUDIO, default communications |
| Input (smoke only) | Steam Streaming Microphone (virtual) | 44.1 kHz, 1 ch, 32-bit float, Extensible, mask 0x4 | Microphone / ROOT |

## Results so far

### Test 1: Output silent for ~10 s (runs `silence-stamp`, `silence-naive`)
- After pause, Loopback capture kept delivering **all-zero packets** (100/s, never flagged `AUDCLNT_BUFFERFLAGS_SILENT`) for ~10 s, then **no packets at all** (1632 ms / 686 ms) until playback resumed. Likely the player keeps its stream open for a while; loopback goes quiet only once no render stream is active. The engine must handle both.
- The device position keeps advancing through a no-data gap (jump 1619 ms vs 1632 ms without data; 0 timeline breaks), with a residual step of ~4–8 ms against QPC at resume.
- `stamp` fill (packet placed at its capture QPC timestamp): gap padded (229 ms at resume), offset of both Sources 0 ms throughout. ADR-0005 holds.
- `naive` fill (silence only on engine underrun): the engine started consuming before the Sources' first packets (~350 ms after start), so the latency cushion shrank to ~5 ms; relative offset between Sources 5 ms at start, 15 ms after the gap. Alignment depends on arrival accidents.
- **ADR-0005 refinement:** packets must be placed by capture timestamp (QPC / device position), not appended in arrival order.

### Tests 2 and 3: Output master volume vs per-app volume (run `volume`, steady 1 kHz tone)
- Master 81% → 19% → 81%: loopback RMS unchanged (−25.9 dBFS). Pre-volume confirmed on Realtek. ADR-0002 holds.
- Browser per-app volume 100% → 20%: loopback RMS −25.9 → −36.2 dBFS (−10.3 dB; the slider is not linear amplitude).
- System sounds (the volume-change "ding") show up in the loopback.
- **For Leveling (ADR-0004):** per-app volume can change mid-Recording, so a single gain per Source is not enough; Leveling should adapt over time windows.

### Test 6a: speakers + built-in array, no call (runs `echo-default`, `echo-raw`, reanalysed with `--analyze --win 1`)
- Echo delay of the Output inside the Input in the Working file: **~102 ms** (range 100–115 ms across three runs), not "a few tens of ms" as BRIEF §5 says.
- Before cancellation converges: echo gain −10.6 dB (raw, speakers at 50%), NCC 0.47.
- The Intel SST array suppresses the echo (and a steady tone) by 40–60 dB within ~3 s, **also in RAW mode** (`WithRawMode()` accepted, no behaviour change): always-on endpoint/DSP processing, not a system APO.
- Echo is hardware-dependent: on a mic without DSP, expect ~−10…−30 dB at ~100 ms for the whole call. Offline AEC (IDEAS) would need a lag search of at least 150–200 ms.

### Test 5a: Bluetooth earbuds as Output, headset mic opened by Windows Sound Recorder mid-run (run `bt-switch`)
- Windows 11 lists one render endpoint (Headphones) and one capture endpoint (Headset) for the earbuds; no separate Hands-Free endpoint. Bus is INTELAUDIO (Bluetooth audio offload through Intel SST), so the bus property does not identify Bluetooth; form factor Headphones/Headset does.
- Switch to Hands-Free (~24.5 s): Loopback capture delivered **no data for 2194 ms**; switch back (~46.1 s): **742 ms**. The stream was **not invalidated** (no RecordingStopped, no reopen) and its format stayed 48 kHz float stereo: the audio engine converts.
- The endpoint volume reading jumped 51% → 67% during Hands-Free and back to 51% after (separate volume per profile).
- **Device position is unreliable across profile switches:** it reset at each switch (−42.8 s, −6.5 s) and during Hands-Free it advanced at the device rate (a third of the stream rate, i.e. 16 kHz) while packets still carried 48 kHz frames. QPC timestamps stayed consistent: `stamp` placement kept both Sources at 0 ms offset. The engine must place packets by QPC, not by device position.
- Loopback level unchanged across the switch, and a crude high-frequency ratio per second shows no narrowing during Hands-Free: the loopback is taken before the Bluetooth codec, so the Output side of the Recording stays full-band. The Input side through the headset mic will be narrowband (to check in test 5b).
- ~2.9 s of Output were lost in total around the two switches (the engine itself was not rendering; the listener hears the dropout too).

### Tests 8, 9 and part of 7: 12-minute run, Bluetooth Output + built-in array Input, music (run `long-bt`)
- Clock-driven Working file exactly 720.01 s for 720.01 s of clock; no overflow, no drift correction needed.
- Drift against QPC: Input +0.1 ppm over 504 s; Output 0 to +1 ppm over the first 215 s (later runs broken by events below). Input vs Output ≈ 1 ppm (~4 ms/hour). **Caveat:** the earbuds are rendered through the same Intel SST DSP as the built-in array, so this is not a truly independent clock; no USB device was available to test one.
- Between two tracks (~227 s) the player stopped its stream: Output level fell, 35 all-zero packets, then **4.6 s without data**. Here the device position **froze** (did not advance) during the gap, unlike the Realtek test 1 where it advanced: position behaviour during silence differs per driver. QPC placement kept the Output at 0 ms offset.
- At 216 s both Sources glitched at once (a system hiccup): Input lost 20 ms (two `DATA_DISCONTINUITY`, position +10 ms twice), Output 50 ms; delivery delays up to 42 ms. With the 40 ms tolerance then in use, the 20 ms Input loss was **not** padded, leaving the Input 20 ms early for the rest of the run. QPC placement is accurate to well under 1 ms, so the tolerance can be ~10 ms (now the default); on `DATA_DISCONTINUITY` the lost frames should be padded exactly.
- Spike bug found here and fixed in `d846f23`: timeline breaks without a position jump were not detected, so the reported Output drift of this run (−1299 ppm) is an artefact of the 4.6 s freeze, not drift.
- Working file 197.8 MB for 12 min (16.5 MB/min, matches ADR-0004); raw native Sources ~23 MB/min each.
- **MP3 via Media Foundation (test 9):** 12 min of 16-bit stereo 48 kHz Mix encoded at 128 kbps in 2.83 s (0.24 s per minute of audio), 11.25 MB (0.94 MB/min). ADR-0003 holds.

## Numbers for the spec (so far)
- First packet ~350–450 ms after `StartRecording` on both Sources.
- Delivery delay (arrival minus capture QPC): Input median 1 ms (max 9); Loopback median 6 ms, p99 17 ms, max 17.6 ms. A 250 ms safety latency is ample.
- Packet period 10 ms, `WasapiRecorder` buffer 100 ms, `LatencyMilliseconds` 100.
- Drift between the built-in array and the Realtek Output: ~1 ppm (~4 ms/hour).
- Mix without Leveling clips (loopback peaks reach 0 dBFS): the Mix needs headroom or a limiter.
- MP3 encoding: 0.24 s per minute of audio (≈ 15 s for an hour).
- Stamp tolerance: ~10 ms is enough (QPC placement error < 1 ms); lost frames flagged by `DATA_DISCONTINUITY` should be padded exactly.
- System hiccups of 40–60 ms without data occur on both Sources at once; the 250 ms safety latency absorbs them.

## NAudio 3.1 surprises
- `WasapiCapture` and `WasapiLoopbackCapture` are `[Obsolete]`; the replacement is `WasapiRecorderBuilder` → `WasapiRecorder` (loopback via `.WithLoopbackCapture()` on a render device).
- `WasapiRecorder.DataAvailable` is a zero-copy `ReadOnlySpan<byte>` callback that also gives WASAPI buffer flags, device position and QPC position per packet: exactly what a clock-driven engine needs.
- The package targets `net9.0`; WASAPI/Media Foundation types need a Windows TFM (`net10.0-windows10.0.19041.0`).
- `MMDevice.AudioClient` is obsolete (`CreateAudioClient()`); `MMDeviceEnumerator.CreateNotificationClient()` gives device-change events without hand-written COM.
- First Input packets after start are all-zero (4 packets); the first packet of each stream carries `DATA_DISCONTINUITY`.

## Pending
- Test 4 wired headphones in a real call; test 5b Bluetooth with the headset mic as Input in a real call; test 6b speakers in a real call; test 7 with truly independent clocks (no USB device available).
