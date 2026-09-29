# Clock-driven recording engine

The recording engine is paced by its own clock, not by the data the Sources deliver: on every tick it takes one tick's worth of audio from each Source's buffer, fills any gap with silence, and writes it, running a small safety latency (~200–300 ms) behind real time to absorb jitter. Loopback capture delivers nothing at all while the Output is silent, and the Input and Output run on independent hardware clocks, so pacing by incoming data would drift the two Sources apart.

## Consequences

- Each Source's buffer is bounded: an underrun yields silence, an overflow drops the excess, so clock drift over hours cannot grow memory without limit.
- The engine takes the clock and the two Sources as injected abstractions, so silence, drift and format mismatches can be tested with synthetic signals, without audio hardware.
