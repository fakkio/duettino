# WASAPI loopback, no virtual audio drivers

The Output is captured with Windows' native WASAPI Loopback capture. Duettino never asks the user to install or enable anything: no Stereo Mix, no VB-Cable or other virtual cable. Stereo Mix is missing or disabled on many machines and only follows one device, and virtual cables require a driver install and re-routing the call app's audio, which is exactly the friction Duettino exists to remove.

## Consequences

- Loopback capture delivers no data at all while nothing plays, so the recording engine cannot be paced by incoming data (see ADR-0005).
- By default loopback is taken before the Output's master volume and mute, so the listening volume does not change the Recording; per-app volume does.
