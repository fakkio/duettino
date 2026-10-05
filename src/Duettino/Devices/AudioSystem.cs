using System.Runtime.InteropServices;
using Duettino.Engine;
using Duettino.Selection;

namespace Duettino.Devices;

/// <summary>
/// The device catalogue the window reads; when the Windows audio system can't be reached (no Windows Audio service,
/// as on Windows Server Core), a catalogue with no device and <see cref="Unavailable"/> saying why.
/// </summary>
sealed record AudioSystem(IDeviceCatalogue Catalogue, Exception? Unavailable)
{
    /// <summary>
    /// Opens the catalogue and reads it once, so an audio system that fails only when read is caught here too.
    /// Only COM errors mean audio is unavailable: anything else is a bug, and is thrown.
    /// </summary>
    public static AudioSystem Open(Func<IDeviceCatalogue> create)
    {
        IDeviceCatalogue? catalogue = null;
        try
        {
            catalogue = create();
            foreach (var source in new[] { Source.Input, Source.Output })
            {
                catalogue.Active(source);
                catalogue.DefaultId(source);
            }
            return new(catalogue, null);
        }
        catch (COMException ex)
        {
            (catalogue as IDisposable)?.Dispose();
            return new(new NoDevices(), ex);
        }
    }

    sealed class NoDevices : IDeviceCatalogue
    {
        public event Action? Changed { add { } remove { } }

        public IReadOnlyList<AudioEndpoint> Active(Source source) => [];

        public string? DefaultId(Source source) => null;
    }
}
