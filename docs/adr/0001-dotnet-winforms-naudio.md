# C# / .NET 10, WinForms and NAudio 3

Duettino is a small native Windows app built on .NET 10 with WinForms for the UI and NAudio 3.x for audio. .NET gives a native executable that starts instantly, the .NET 10 SDK is already on the dev machine, and NAudio is the reference audio library on .NET, exposing both WASAPI capture and Loopback capture.

## Considered Options

- **Python + tkinter**: rejected, packaging with PyInstaller is heavier and slower to start.
- **WPF / WinUI 3**: rejected, more machinery than a one-window app needs; WinUI 3 also drags in the Windows App SDK.
- **NAudio 2.2.x**: rejected in favour of 3.x. NAudio 3 uses `ComWrappers`, so COM objects created on the WinForms (STA) thread can be used from worker threads without apartment errors, and it exposes Windows 11's system echo cancellation reference (`IAcousticEchoCancellationControl`) should we ever want it.

## Consequences

- WinForms cannot be trimmed (`NETSDK1175`), so a self-contained build stays around 50 MB compressed.
- NAudio 3 needs a Windows target framework (`net10.0-windows10.0.19041.0`) for WASAPI and Media Foundation. Its capture classes `WasapiCapture`/`WasapiLoopbackCapture` are obsolete: Duettino uses `WasapiRecorder`, whose per-packet callback carries the buffer flags and capture timestamp the engine needs (ADR-0005).
