# Ideas

Features deliberately left out of v1, kept here so they aren't lost. Not commitments: each one needs its own decision before it becomes a ticket.

## Recording

- **Pause/resume a Recording.** Left out because Stop + a new Recording (two files) covers it, and pausing complicates the clock and the timer.
- **Naming a Recording** before or after it runs (e.g. "client X call"). Left out because asking before slows the start and asking after is a dialog most people would dismiss; renaming in Explorer via "Open folder" covers it.
- **Stereo Input.** The Working file keeps the Input mono; a stereo line-in (e.g. a music rehearsal) would need a fourth channel.

## Finalization

- **Separate tracks or files per Source.** Cheap since the Working file already keeps the Sources apart (ADR-0004): Finalization would skip the Mix.
- **Turning Leveling off**, keeping the Sources as captured.
- **Manual balance**: two sliders to correct the Leveling after the fact.
- **Offline echo cancellation** at Finalization, using the Output as the reference signal, for Recordings made with speakers (ADR-0004 makes it possible). Measure how bad the echo is with the `/prototype` spike first.
- **Windows system echo cancellation** (`IAcousticEchoCancellationControl`, Windows 11 22H2+, driver-dependent, exposed by NAudio 3). Needs communications mode, which also turns on AGC and noise suppression and may duck other sounds.

## Window

- **Level meters in the icon's colors**, one color per Source, matching the landing page. Left out of 0.1.0 because it is app work, not part of the launch material.

## Distribution

- **Signing the executable** for free through the SignPath Foundation (open-source projects, needs an application and approval), so the GitHub download stops triggering SmartScreen's "Windows protected your PC". 0.1.0 ships unsigned with a README FAQ and the SHA-256 in the release notes; look into it before the first stable release.

- **Building the MSIX for the Store**: `makeappx` + a hand-written manifest (scriptable, runs in GitHub Actions, fits "everything via CLI") vs a Visual Studio `.wapproj` (guided, but Visual Studio-only). Leaning towards `makeappx`; decide when the Store release is prepared (ADR-0007).
