# One mixed MP3 via Windows Media Foundation

A Recording file is a single MP3, 48 kHz stereo at 128 kbps (~1 MB/min), holding the Mix of both Sources, not separate channels or files. It is encoded with the MP3 encoder that ships with Windows through Media Foundation (exposed by NAudio), so Duettino carries no native encoder such as LAME. When that encoder is missing (Windows N/KN editions without the Media Feature Pack), Finalization writes a mixed 16-bit stereo WAV instead and tells the user how to install the Media Feature Pack.

## Considered Options

- **Bundle LAME**: rejected, an extra native dependency (and licence) for a case that only hits N editions.
- **Fail Finalization on N editions**: rejected, the user would get no playable file.
- **Separate tracks or files per Source**: out of scope for v1, see `docs/IDEAS.md`.

## Consequences

- The encoder only takes 16-bit PCM, mono or stereo, at the output sample rate, so the Mix is converted to 16-bit stereo 48 kHz before encoding.
- Format conversion (resampling, channel up/downmix) is done in managed code, not with Media Foundation's resampler, so recording works on machines without Media Foundation.
