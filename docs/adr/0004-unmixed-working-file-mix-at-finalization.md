# Unmixed Working file, Mix at Finalization

While a Recording runs, Duettino writes the two Sources side by side, unmixed, to a WAV Working file whose header is flushed periodically; Leveling, the Mix and MP3 encoding all happen at Finalization, after Stop. Writing WAV rather than MP3 live means a crash loses at most a few seconds, and keeping the Sources apart means their relative loudness, which is unpredictable (mic gain, per-app volume), can be fixed after the fact by automatic Leveling instead of gain sliders the user must get right beforehand.

## Considered Options

- **Encode MP3 live**: rejected, a crash would leave a truncated or unreadable file.
- **Mix live into a stereo WAV** (the original brief's pipeline, optionally with two gain sliders): rejected, balance mistakes would be baked in.

## Consequences

- The Working file is larger than a mixed one (3 channels, Input mono + Output stereo, 16-bit 48 kHz: roughly 17 MB/min instead of 10, around 1 GB for an hour) and is not meant to be played as is.
- It sits in the destination folder, next to where the Recording file will land (`<name>.working.wav`), so it survives an uninstall and its disk usage is visible.
- Separate tracks per Source and offline echo cancellation (with the Output as reference) become cheap later, since Finalization has both Sources.
- An Orphan Working file left by a crash still holds everything needed for Recovery.
