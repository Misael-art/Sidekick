using System.Runtime.InteropServices;

namespace Ajudante.Platform.Hardware;

public enum AudioEndpointKind
{
    Output,
    Microphone
}

public sealed record AudioEndpointState(int VolumePercent, bool Muted);

public static class AudioEndpointController
{
    public static AudioEndpointState GetState(AudioEndpointKind kind)
    {
        return WithEndpoint(kind, endpoint =>
        {
            ThrowIfFailed(endpoint.GetMasterVolumeLevelScalar(out var volume));
            ThrowIfFailed(endpoint.GetMute(out var muted));
            return new AudioEndpointState((int)Math.Round(volume * 100), muted);
        });
    }

    public static AudioEndpointState SetVolume(AudioEndpointKind kind, int percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        return WithEndpoint(kind, endpoint =>
        {
            var context = Guid.Empty;
            ThrowIfFailed(endpoint.SetMasterVolumeLevelScalar(clamped / 100f, ref context));
            ThrowIfFailed(endpoint.GetMute(out var muted));
            return new AudioEndpointState(clamped, muted);
        });
    }

    public static AudioEndpointState AdjustVolume(AudioEndpointKind kind, int deltaPercent)
    {
        return WithEndpoint(kind, endpoint =>
        {
            ThrowIfFailed(endpoint.GetMasterVolumeLevelScalar(out var current));
            var next = Math.Clamp(current + (deltaPercent / 100f), 0f, 1f);
            var context = Guid.Empty;
            ThrowIfFailed(endpoint.SetMasterVolumeLevelScalar(next, ref context));
            ThrowIfFailed(endpoint.GetMute(out var muted));
            return new AudioEndpointState((int)Math.Round(next * 100), muted);
        });
    }

    public static AudioEndpointState SetMute(AudioEndpointKind kind, bool muted)
    {
        return WithEndpoint(kind, endpoint =>
        {
            var context = Guid.Empty;
            ThrowIfFailed(endpoint.SetMute(muted, ref context));
            ThrowIfFailed(endpoint.GetMasterVolumeLevelScalar(out var volume));
            return new AudioEndpointState((int)Math.Round(volume * 100), muted);
        });
    }

    public static AudioEndpointState ToggleMute(AudioEndpointKind kind)
    {
        return WithEndpoint(kind, endpoint =>
        {
            ThrowIfFailed(endpoint.GetMute(out var muted));
            var context = Guid.Empty;
            ThrowIfFailed(endpoint.SetMute(!muted, ref context));
            ThrowIfFailed(endpoint.GetMasterVolumeLevelScalar(out var volume));
            return new AudioEndpointState((int)Math.Round(volume * 100), !muted);
        });
    }

    private static T WithEndpoint<T>(AudioEndpointKind kind, Func<IAudioEndpointVolume, T> action)
    {
        var endpoint = GetEndpoint(kind);
        try
        {
            return action(endpoint);
        }
        finally
        {
            if (Marshal.IsComObject(endpoint))
                Marshal.FinalReleaseComObject(endpoint);
        }
    }

    private static IAudioEndpointVolume GetEndpoint(AudioEndpointKind kind)
    {
        var enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
        IMMDevice? device = null;
        object? endpoint = null;
        try
        {
            var dataFlow = kind == AudioEndpointKind.Output ? EDataFlow.Render : EDataFlow.Capture;
            ThrowIfFailed(enumerator.GetDefaultAudioEndpoint(dataFlow, ERole.Multimedia, out device));
            var endpointVolumeId = typeof(IAudioEndpointVolume).GUID;
            ThrowIfFailed(device.Activate(ref endpointVolumeId, CLSCTX_ALL, IntPtr.Zero, out endpoint));
            return (IAudioEndpointVolume)endpoint;
        }
        finally
        {
            if (device is not null && Marshal.IsComObject(device))
                Marshal.FinalReleaseComObject(device);

            if (Marshal.IsComObject(enumerator))
                Marshal.FinalReleaseComObject(enumerator);
        }
    }

    private static void ThrowIfFailed(int hr)
    {
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);
    }

    private const int CLSCTX_ALL = 23;

    private enum EDataFlow
    {
        Render = 0,
        Capture = 1,
        All = 2
    }

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
        int OpenPropertyStore(int access, out IntPtr properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out uint state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint channelCount);
        int SetMasterVolumeLevel(float level, ref Guid eventContext);
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channelNumber, float level, ref Guid eventContext);
        int SetChannelVolumeLevelScalar(uint channelNumber, float level, ref Guid eventContext);
        int GetChannelVolumeLevel(uint channelNumber, out float level);
        int GetChannelVolumeLevelScalar(uint channelNumber, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
        int GetVolumeStepInfo(out uint step, out uint stepCount);
        int VolumeStepUp(ref Guid eventContext);
        int VolumeStepDown(ref Guid eventContext);
        int QueryHardwareSupport(out uint hardwareSupportMask);
        int GetVolumeRange(out float volumeMin, out float volumeMax, out float volumeIncrement);
    }
}
