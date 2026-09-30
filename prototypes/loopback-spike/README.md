# PROTOTYPE: loopback spike (throwaway, never merged)

Hardware spike for Duettino, living only on branch `prototype/loopback-spike`. It checks on real devices the risks in `docs/BRIEF.md` §5 against ADR-0001..0005: a clock-driven engine writing an Input + Loopback capture of an Output into a 3-channel Working file, with live and end-of-run measurements.

```
dotnet run                     # interactive: pick Input, Output, seconds, label, MP3
dotnet run -- --list           # only list devices and their mix formats
dotnet run -- --in 1 --out 1 --seconds 600 --label long --mp3
```

Other options: `--fill naive|stamp` (default `stamp`), `--latency 250`, `--tol 10`, `--gap 50`, `--buffer 100` (ms), `--raw` (Input without system audio effects) or `--comms` (Input opened as a communications stream), `--no-echo`, `--no-mp3`.
`dotnet run -- --analyze runs/<dir> --win 1` re-runs the echo estimate on an existing Working file with shorter windows.
While recording: `M` marks an event in the log, `Q` stops early.

Each run writes to `runs/<timestamp>-<label>/` (git-ignored): `working.wav` (Input mono + Output stereo, 16-bit 48 kHz), `input.raw.wav` and `output.raw.wav` (native mix format, one extra file per reopening), `mix.wav`, optional `mix.mp3`, and `report.txt` with everything printed on screen.

Fill modes:
- `stamp`: each packet is placed at its WASAPI capture QPC timestamp; gaps become silence and drift beyond `--tol` is padded or dropped.
- `naive`: data is appended as it comes and silence is written only when the engine finds a Source's buffer empty (the literal reading of ADR-0005).
