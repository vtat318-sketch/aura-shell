using System.Runtime.InteropServices;

namespace Aura.Shell.Interop;

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorCom { }

// Only the leading vtable slots we call are declared; order must match mmdeviceapi.h.
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
}

// endpointvolume.h
[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IntPtr notify);
    void UnregisterControlChangeNotify(IntPtr notify);
    void GetChannelCount(out uint count);
    void SetMasterVolumeLevel(float levelDb, ref Guid context);
    void SetMasterVolumeLevelScalar(float level, ref Guid context);
    void GetMasterVolumeLevel(out float levelDb);
    void GetMasterVolumeLevelScalar(out float level);
    void SetChannelVolumeLevel(uint channel, float levelDb, ref Guid context);
    void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
    void GetChannelVolumeLevel(uint channel, out float levelDb);
    void GetChannelVolumeLevelScalar(uint channel, out float level);
    void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

/// <summary>Master volume of the default playback device via Core Audio. Failures are swallowed (no device = 0%).</summary>
internal sealed class SystemVolume
{
    private const int eRender = 0, eMultimedia = 1, CLSCTX_ALL = 23;
    private IAudioEndpointVolume? _endpoint;

    private IAudioEndpointVolume? Endpoint()
    {
        if (_endpoint != null) return _endpoint;
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out var device);
            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object o);
            _endpoint = (IAudioEndpointVolume)o;
        }
        catch { _endpoint = null; }
        return _endpoint;
    }

    /// <summary>Call when the default device may have changed (e.g. headphones plugged in).</summary>
    public void Reset() => _endpoint = null;

    public float Level
    {
        get
        {
            var v = Endpoint();
            if (v == null) return 0;
            try { v.GetMasterVolumeLevelScalar(out float level); return level; }
            catch { _endpoint = null; return 0; }
        }
        set
        {
            var v = Endpoint();
            if (v == null) return;
            try { var ctx = Guid.Empty; v.SetMasterVolumeLevelScalar(Math.Clamp(value, 0f, 1f), ref ctx); }
            catch { _endpoint = null; }
        }
    }

    public bool Muted
    {
        get
        {
            var v = Endpoint();
            if (v == null) return false;
            try { v.GetMute(out bool m); return m; }
            catch { _endpoint = null; return false; }
        }
        set
        {
            var v = Endpoint();
            if (v == null) return;
            try { var ctx = Guid.Empty; v.SetMute(value, ref ctx); }
            catch { _endpoint = null; }
        }
    }
}
