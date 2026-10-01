# Clock-driven recording engine, packets placed by capture timestamp

The recording engine is paced by its own clock, not by the data the Sources deliver: it writes the Working file a fixed safety latency behind real time, places each packet from a Source at the position given by its capture timestamp (QPC), and fills whatever is left uncovered with silence. Loopback capture may deliver nothing at all while the Output is silent, delivery runs in stalls of up to ~100 ms, and the Input and Output run on independent hardware clocks, so pacing by incoming data, or appending packets in arrival order, drifts the two Sources apart.

## Considered Options

- **Append packets in arrival order, silence only on underrun**: rejected, the loopback spike showed the latency cushion collapsing to ~5 ms and the Sources drifting 5–15 ms apart after a single gap.
- **Place packets by the device position WASAPI reports**: rejected, it counts at the device rate (16 kHz) in Bluetooth Hands-Free, resets at each profile switch, and freezes or keeps advancing during silence depending on the driver. Capture timestamps stayed consistent in every case.

## Consequences

- Safety latency of 250 ms (never below ~150 ms: the worst delivery delay seen was 97 ms).
- Frames Windows reports as lost (`DATA_DISCONTINUITY`) are padded exactly; the placement tolerance is ~10 ms, since a 20 ms loss left unpadded stays as a permanent offset.
- Each Source's buffer is bounded (2 s was ample): an underrun yields silence, an overflow drops the excess, so clock drift over hours cannot grow memory without limit.
- Alignment between the Sources is only as good as the loopback timestamps, which lead or lag arrival differently per endpoint path (−16 to +6 ms): within ~±20 ms, fine for a Mix.
- The engine takes the clock and the two Sources (packets with their timestamps and flags) as injected abstractions, so silence, gaps, losses and drift can be tested with synthetic signals, without audio hardware. Evidence: branch `prototype/loopback-spike` @ `6f84614`.
