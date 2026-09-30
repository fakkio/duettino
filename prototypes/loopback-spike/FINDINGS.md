# PROTOTYPE findings: loopback spike (interim, updated after each test)

Machine: Windows 11 Pro 26200, .NET 10.0.12, NAudio 3.1.0. Run reports live in `runs/` (git-ignored, local only).

## Devices tried

| Role | Device | Mix format | Form factor / bus |
|---|---|---|---|
| Input | Microphone Array (Intel Smart Sound Technology) | 48 kHz, 2 ch, 32-bit float, Extensible, mask 0x3 | Microphone / INTELAUDIO |
| Output | Altoparlanti (Realtek Audio) | 48 kHz, 2 ch, 32-bit float, Extensible, mask 0x3 | Speakers / INTELAUDIO |
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

## Numbers for the spec (so far)
- First packet ~350–450 ms after `StartRecording` on both Sources.
- Delivery delay (arrival minus capture QPC): Input median 1 ms (max 9); Loopback median 6 ms, p99 17 ms, max 17.6 ms. A 250 ms safety latency is ample.
- Packet period 10 ms, `WasapiRecorder` buffer 100 ms, `LatencyMilliseconds` 100.
- Drift between the built-in array and the Realtek Output: ~1 ppm (~4 ms/hour).
- Mix without Leveling clips (loopback peaks reach 0 dBFS): the Mix needs headroom or a limiter.
- MP3 via Media Foundation works on this machine (smoke run; timing on a long run pending).

## NAudio 3.1 surprises
- `WasapiCapture` and `WasapiLoopbackCapture` are `[Obsolete]`; the replacement is `WasapiRecorderBuilder` → `WasapiRecorder` (loopback via `.WithLoopbackCapture()` on a render device).
- `WasapiRecorder.DataAvailable` is a zero-copy `ReadOnlySpan<byte>` callback that also gives WASAPI buffer flags, device position and QPC position per packet: exactly what a clock-driven engine needs.
- The package targets `net9.0`; WASAPI/Media Foundation types need a Windows TFM (`net10.0-windows10.0.19041.0`).
- `MMDevice.AudioClient` is obsolete (`CreateAudioClient()`); `MMDeviceEnumerator.CreateNotificationClient()` gives device-change events without hand-written COM.
- First Input packets after start are all-zero (4 packets); the first packet of each stream carries `DATA_DISCONTINUITY`.

## Pending
- Test 4 wired headphones in a real call; test 5 Bluetooth (format switch when the headset mic opens); test 6b speakers in a real call; test 7 devices on different hardware; test 8 long run (≥ 10 min, drift); test 9 MP3 timing on a long run.
