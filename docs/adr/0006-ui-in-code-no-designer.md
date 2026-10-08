# UI written in code, no WinForms designer

The window is laid out in plain C# (e.g. `TableLayoutPanel`), with no `.Designer.cs` files. It is tiny (two drop-downs, two level meters, a button, a few labels), so code is easier to read and for an agent to change than generated designer code, and the reference IDE is JetBrains Rider, whose WinForms designer is less reliable on modern .NET than Visual Studio's. Build, test and publish all go through the `dotnet` CLI.
