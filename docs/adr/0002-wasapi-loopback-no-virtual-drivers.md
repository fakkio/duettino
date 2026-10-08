# WASAPI loopback, no virtual audio drivers

The Output is captured with Windows' native WASAPI Loopback capture. Duettino never asks the user to install or enable anything: no Stereo Mix, no VB-Cable or other virtual cable. Stereo Mix is missing or disabled on many machines and only follows one device, and virtual cables require a driver install and re-routing the call app's audio, which is exactly the friction Duettino exists to remove.

## Consequences

- Loopback capture goes quiet in two ways: while an app keeps a render stream open it delivers packets of exact zeros (never flagged as silent); when no stream is active it delivers no data at all. So the recording engine cannot be paced by incoming data (see ADR-0005).
- Loopback is taken before the Output's master volume and mute, so the listening volume does not change the Recording; per-app volume does (confirmed on the loopback spike).
- When a Bluetooth headset switches to Hands-Free, everything played on that Output reaches the loopback band-limited to ~8 kHz, and there are 0.7–2.2 s without data at each switch. Nothing to do about it short of a different Output.
