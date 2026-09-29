# Duettino

Duettino records what you say and what you hear on a Windows PC into one audio file: a chosen Input mixed with whatever is playing on a chosen Output.

## Language

### Recording

**Recording**:
One session between Record and Stop. It leaves a Working file behind, which Finalization turns into a Recording file.
_Avoid_: session, call, take

**Recording file**:
The audio file a Recording ends up as, the thing the user keeps.
_Avoid_: output, output file, export, MP3

### Sources

**Source**:
Either of the two things a Recording listens to: the Input or the Output.
_Avoid_: channel, track, stream

**Input**:
The capture device chosen for a Recording: usually a microphone, but any capture device will do (line-in, virtual cable…).
_Avoid_: mic, microphone, mic source

**Output**:
The playback device chosen for a Recording, whose sound is captured (headphones, speakers, HDMI…). Never the file a Recording produces.
_Avoid_: headphones, speakers, system audio

**Loopback capture**:
Capturing what is playing on an Output, as opposed to what a microphone hears.
_Avoid_: system audio capture, Stereo Mix

### Files and Finalization

**Working file**:
The file written while a Recording runs, holding the two Sources side by side, not yet mixed.
_Avoid_: temp file, buffer, WAV

**Orphan Working file**:
A Working file whose Recording never reached Finalization, typically because the app or the PC crashed.
_Avoid_: leftover, stale file

**Recovery**:
Finalizing an Orphan Working file, at the user's request, in a later run of the app.
_Avoid_: repair, restore

**Finalization**:
Turning a Working file into a Recording file: Leveling, Mix, encoding, then removing the Working file.
_Avoid_: export, conversion, save

**Leveling**:
Bringing each Source to a comparable loudness before the Mix, so neither side drowns the other.
_Avoid_: normalization, gain, volume

**Mix**:
The single audio stream obtained by adding the leveled Sources together.
_Avoid_: merge, blend, downmix
