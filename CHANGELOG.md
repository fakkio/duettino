# Changelog

All notable changes to Duettino are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Record what you say and what you hear into one file: pick any Input (a microphone, line-in or virtual cable) and any Output (headphones, speakers, HDMI…), press Record, and both are captured at once, the Output through Windows' own Loopback capture with no drivers to install.
- Stop produces one MP3 Recording file (48 kHz stereo, 128 kbps) in `Documents\Duettino`, named after its start time, never overwriting an existing file; on Windows N editions without the Media Feature Pack it is a WAV file, with a message explaining how to get MP3.
- Level both sides to comparable loudness before mixing them, so neither voice drowns the other, without boosting the room noise of a silent microphone and without clipping.
- Record devices whatever format Windows delivers them in: any sample rate, mono, stereo or surround, 16-bit or float.
- Keep both sides in sync for hours, through system hiccups, audio lost by Windows and the slight speed difference between devices.
- Show a level meter for each side and the elapsed time while recording.
- Remember the chosen Input, Output and destination folder; use the Windows default device, and say so, when a remembered device is not connected; update the device lists as devices are plugged in or out; change the folder and open it from the window.
- Keep recording when a device goes mid-Recording (jack pulled, Bluetooth earbuds back in their case), falling back to the Windows default device and returning to the chosen one once it is back; record silence while no device is left.
- Say how to allow the microphone when the Windows privacy settings deny it, and start recording it as soon as it is allowed.
- Stop cleanly on an error Duettino can't recover from, keeping what was recorded and saying why.
- Offer to recover, delete or keep for later the Recordings a crash left unfinished, at the next launch.
- Ask "Stop and save?" before closing the window during a Recording, finish saving before closing, and stop without asking when Windows shuts down or logs off, leaving the Recording for recovery.
- Show the window in Italian on an Italian Windows, in English everywhere else.
- Ship as a single executable that runs without .NET installed.
- Open the window with a notice, instead of crashing, on a Windows without the audio service.
- Lay the window out right at any display scaling, and keep it right when it moves between screens with different scaling.
- Release Duettino as open source under the MIT License.
- Give Duettino its own icon, two strands joining into one ribbon, on the executable in Explorer and on the window's title bar, taskbar button and Alt+Tab, sharp at any display scaling.
- Explain Duettino in a README: what it records and how, who it's for, what it takes care of, a FAQ (Stereo Mix, Bluetooth headphones, speakers, privacy, SmartScreen, Windows N, the law, other tools), a one-click download and how to build it.
- Publish the README as an English landing page, with the light or dark theme following the system and a toggle, and a short privacy page (not live until the first release).
- Give the landing page its own look: a big headline with colored bands behind "what you say" and "what you hear", a diagram of both joining into one MP3, and a Download button.

[Unreleased]: https://github.com/fakkio/duettino/commits/develop
