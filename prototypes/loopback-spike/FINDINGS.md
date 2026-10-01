# PROTOTYPE findings: loopback spike (interim, updated after each test)

Machine: Windows 11 Pro 26200 (tests on 2026-09-30) then 26300 after an overnight update (call tests on 2026-10-01), .NET 10.0.12, NAudio 3.1.0. Run reports live in `runs/` (git-ignored, local only).

## Devices tried

| Role | Device | Mix format | Form factor / bus |
|---|---|---|---|
| Input | Microphone Array (Intel Smart Sound Technology) | 48 kHz, 2 ch, 32-bit float, Extensible, mask 0x3 | Microphone / INTELAUDIO |
| Output | Altoparlanti (Realtek Audio) | 48 kHz, 2 ch, 32-bit float, Extensible, mask 0x3 | Speakers / INTELAUDIO |
| Output | Headphones (BD86), Bluetooth earbuds | 48 kHz, 2 ch, 32-bit float, Extensible, mask 0x3 | Headphones / INTELAUDIO |
| Input | Headset (BD86), earbuds mic | 48 kHz, 2 ch, 32-bit float, **plain IeeeFloat (not Extensible)** | Headset / INTELAUDIO, default communications |
| Output | Cuffie (Realtek Audio), wired headset on the jack | 48 kHz, 2 ch, 32-bit float, Extensible, mask 0x3 | Headphones / INTELAUDIO |
| Input | Microfono jack (Realtek Audio), wired headset mic | 48 kHz, 2 ch, 32-bit float, Extensible, mask 0x3 | Microphone / INTELAUDIO |
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
- Loopback level unchanged across the switch. **Correction (2026-10-01):** a crude high-frequency ratio first suggested no narrowing; a proper band analysis of the same file shows energy in 8–12 kHz falling from −22/−27 dB (A2DP) to **−53/−57 dB during Hands-Free** and back to −16/−27 dB after. In Hands-Free the audio engine mixes at the device rate (16 kHz), so **everything** on the Output, not only the call, is band-limited to ~8 kHz in the loopback.
- ~2.9 s of Output were lost in total around the two switches (the engine itself was not rendering; the listener hears the dropout too).

### Tests 8, 9 and part of 7: 12-minute run, Bluetooth Output + built-in array Input, music (run `long-bt`)
- Clock-driven Working file exactly 720.01 s for 720.01 s of clock; no overflow, no drift correction needed.
- Drift against QPC: Input +0.1 ppm over 504 s; Output 0 to +1 ppm over the first 215 s (later runs broken by events below). Input vs Output ≈ 1 ppm (~4 ms/hour). **Caveat:** the earbuds are rendered through the same Intel SST DSP as the built-in array, so this is not a truly independent clock; no USB device was available to test one.
- Between two tracks (~227 s) the player stopped its stream: Output level fell, 35 all-zero packets, then **4.6 s without data**. Here the device position **froze** (did not advance) during the gap, unlike the Realtek test 1 where it advanced: position behaviour during silence differs per driver. QPC placement kept the Output at 0 ms offset.
- At 216 s both Sources glitched at once (a system hiccup): Input lost 20 ms (two `DATA_DISCONTINUITY`, position +10 ms twice), Output 50 ms; delivery delays up to 42 ms. With the 40 ms tolerance then in use, the 20 ms Input loss was **not** padded, leaving the Input 20 ms early for the rest of the run. QPC placement is accurate to well under 1 ms, so the tolerance can be ~10 ms (now the default); on `DATA_DISCONTINUITY` the lost frames should be padded exactly.
- Spike bug found here and fixed in `d846f23`: timeline breaks without a position jump were not detected, so the reported Output drift of this run (−1299 ppm) is an artefact of the 4.6 s freeze, not drift.
- Working file 197.8 MB for 12 min (16.5 MB/min, matches ADR-0004); raw native Sources ~23 MB/min each.
- **MP3 via Media Foundation (test 9):** 12 min of 16-bit stereo 48 kHz Mix encoded at 128 kbps in 2.83 s (0.24 s per minute of audio), 11.25 MB (0.94 MB/min). ADR-0003 holds.

### Test 6b: real call, speakers + built-in array (run `call-speakers`, 300 s)
- Clean run: Working file 300.02 s for 300.02 s of clock, no gaps, delivery max 24 ms, drift Input vs Output −0.1 ppm.
- One 10 ms Input loss (`DATA_DISCONTINUITY`, position +10 ms) at 70 s was padded exactly by the 10 ms tolerance: final offset 0 ms (the 40 ms tolerance of the long run would have left it 10 ms off).
- Echo: while the remote speaker talked (Output ≈ −17 dBFS), the Input stayed at −55…−58 dBFS; on 30 one-second windows no echo correlation above noise (one |NCC| 0.21 at a negative delay, i.e. double-talk, not echo). With the call running, echo of the speakers in the Input is suppressed by ≥ 40 dB on this laptop, as in test 6a after convergence.
- The far end is gated by the call app: Output drops to −60…−74 dBFS whenever the remote side is silent (not true silence, no loopback gaps).

### Test 5b: real call, Bluetooth earbuds as Output and their mic as Input, call already in Hands-Free (run `call-bt`, 300 s)
- Input opened in the endpoint's plain `IeeeFloat` 48 kHz stereo format (not Extensible) and handled fine; no RecordingStopped, no reopen, no gaps on either Source, Working file 300.01 s for 300.01 s.
- Both device positions counted at **16 kHz** for the whole run (one third of the 48 kHz stream). The spike logged ~60 000 false "jumps" and could not compute drift; fixed afterwards by learning the position rate against QPC.
- Input band: energy in 4–8 kHz −22 dB, above 8 kHz −57/−66 dB: wideband speech (mSBC, 16 kHz), not 8 kHz narrowband. Built-in array for comparison: −27 dB above 8 kHz.
- Output band during the call: 8–12 kHz at −83 dB (speakers call: −59 dB), consistent with the 16 kHz engine mix in Hands-Free.
- Input delivered **7080 all-zero packets (~71 s of exact digital zero)** between phrases: the Hands-Free mic path gates silence to true zeros (or the call app muted it). Leveling must not treat exact zeros as signal.
- **Loopback QPC stamps led arrival by ~16 ms** (delivery median −15.9 ms) in Hands-Free, versus +6 ms on Realtek and +0.1 ms on Bluetooth A2DP: what the loopback QPC stamp refers to differs per endpoint path, so relative alignment between Sources is only known to within ~±20 ms. Fine for a Mix; offline AEC would need its own delay search anyway.
- No echo (headset). Output endpoint volume read 38% (the Hands-Free volume).

### Test 4: real call, wired headset on the jack (headphones as Output, headset mic as Input) (run `call-wired`, 300 s)
- Plugging the jack created two new endpoints, "Cuffie (Realtek)" and "Microfono jack (Realtek)", separate from the speakers and the built-in array, and **Windows moved both default and default-communications roles to them on its own**. Device indices shift when hardware is plugged in: the app must key devices by endpoint ID, and the defaults can change under the user.
- Clean run: Working file 300.03 s for 300.03 s, no reopen, no data loss; one 53–57 ms delivery stall on both Sources at once at 148.7 s (system hiccup, positions continuous). Drift ≈ 0 (Input +0.4 ppm; the Output figure in the report is an artefact of a spike bug, see below).
- **Measurable echo through the headset:** NCC 0.31–0.44 on 5 of 6 windows, delay **75–105 ms (median 90 ms)**, echo gain **−37 dB** relative to the loopback (headphones at 26%). The jack mic has no DSP echo suppression, so leakage from the earcups reaches the Recording. At −37 dB below the remote voice it should be inaudible in the Mix, but it confirms that echo depends on hardware and is not always suppressed.
- Spike bug found here and fixed: the position-rate learner flipped to 44.1 kHz on a single irregular packet; it now needs 5 consecutive agreeing packets.

### Test 5c: real call, Bluetooth again, Input opened as a communications stream (`--comms`) (run `call-bt-comms`, 300 s)
- The fixed position-rate learner switched both Sources to 16 kHz after 5 packets. **Hands-Free drift: Input +0.4 ppm (225 s), Output +0.7 ppm (300 s), relative −0.3 ppm (~1 ms/hour).** Same Intel SST clock domain as before, so still not an independent-clock test.
- Opening a second communications stream on the headset mic alongside the call app worked: no error, no reopen. Input behaviour similar to the default mode: between phrases the mic is gated to ≈ −120 dBFS, with exact digital zeros growing in the second half (5105 all-zero packets). `--comms` brings no visible benefit over the default mode here.
- Largest system stall of all runs at 225 s: Input 106 ms without data (delivery delay up to 97 ms) plus a 10 ms loss padded exactly; Output 50–52 ms. Still well inside the 250 ms safety latency.
- Loopback QPC lead of ~16 ms in Hands-Free confirmed (delivery median −15.8 ms). No echo.

## Numbers for the spec (so far)
- First packet ~350–450 ms after `StartRecording` on both Sources.
- Delivery delay (arrival minus capture QPC): Input median 1 ms (max 9); Loopback median 6 ms, p99 17 ms, max 17.6 ms. A 250 ms safety latency is ample.
- Packet period 10 ms, `WasapiRecorder` buffer 100 ms, `LatencyMilliseconds` 100.
- Drift between the built-in array and the Realtek Output: ~1 ppm (~4 ms/hour).
- Mix without Leveling clips (loopback peaks reach 0 dBFS): the Mix needs headroom or a limiter.
- MP3 encoding: 0.24 s per minute of audio (≈ 15 s for an hour).
- Stamp tolerance: ~10 ms is enough (QPC placement error < 1 ms); lost frames flagged by `DATA_DISCONTINUITY` should be padded exactly.
- System hiccups of 40–110 ms without data occur, often on both Sources at once (worst: 106 ms, delivery delay 97 ms); the 250 ms safety latency absorbs them, and it should not go below ~150 ms.

## NAudio 3.1 surprises
- `WasapiCapture` and `WasapiLoopbackCapture` are `[Obsolete]`; the replacement is `WasapiRecorderBuilder` → `WasapiRecorder` (loopback via `.WithLoopbackCapture()` on a render device).
- `WasapiRecorder.DataAvailable` is a zero-copy `ReadOnlySpan<byte>` callback that also gives WASAPI buffer flags, device position and QPC position per packet: exactly what a clock-driven engine needs.
- The package targets `net9.0`; WASAPI/Media Foundation types need a Windows TFM (`net10.0-windows10.0.19041.0`).
- `MMDevice.AudioClient` is obsolete (`CreateAudioClient()`); `MMDeviceEnumerator.CreateNotificationClient()` gives device-change events without hand-written COM.
- First Input packets after start are all-zero (4 packets); the first packet of each stream carries `DATA_DISCONTINUITY`.

## Pending
 test 7 with truly independent clocks (no USB device available).
