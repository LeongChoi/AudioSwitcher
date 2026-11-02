using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AudioSwitcherModern
{
    // All COM objects are created, used and released on AudioWorker's MTA thread.
    internal sealed class DeviceInfo
    {
        public string Id, Name;
        public bool Input, IsDefault, Muted;
        public int Volume;
    }
    internal sealed class SessionInfo
    {
        public string DeviceId, Id, Name;
        public int Volume;
        public bool Muted;
    }
    internal sealed class AudioSnapshot
    {
        public List<DeviceInfo> Devices = new List<DeviceInfo>();
        public List<SessionInfo> Sessions = new List<SessionInfo>();
        public string Error, Warning;
    }
    internal static class CoreAudio
    {
        static readonly Guid EndpointVolumeId = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        static readonly Guid SessionManagerId = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
        static readonly Guid Context = new Guid("90FEBEC6-5309-49DF-92E4-4BC0542A0F8D");
        internal static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
        }
        static void Check(int hr) { Marshal.ThrowExceptionForHR(hr); }
        static IMMDeviceEnumerator Enumerator() { return (IMMDeviceEnumerator)new MMDeviceEnumerator(); }
        static string DefaultId(IMMDeviceEnumerator en, int flow)
        {
            IMMDevice device = null;
            try { if (en.GetDefaultAudioEndpoint(flow, 1, out device) < 0) return "";
                string id; Check(device.GetId(out id)); return id; }
            finally { Release(device); }
        }
        static string Name(IMMDevice device)
        {
            IPropertyStore props = null;
            PropVariant value = new PropVariant();
            try {
                Check(device.OpenPropertyStore(0, out props));
                PropertyKey key = new PropertyKey { Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
                Check(props.GetValue(ref key, out value));
                return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) : "Audio device";
            } finally { PropVariantClear(ref value); Release(props); }
        }
        static IAudioEndpointVolume Endpoint(IMMDevice device)
        {
            object result; Guid iid = EndpointVolumeId;
            Check(device.Activate(ref iid, 23, IntPtr.Zero, out result));
            return (IAudioEndpointVolume)result;
        }
        internal static AudioSnapshot Read()
        {
            AudioSnapshot snapshot = new AudioSnapshot();
            IMMDeviceEnumerator en = null;
            try {
                en = Enumerator();
                for (int flow = 0; flow <= 1; flow++) {
                    string defaultId = DefaultId(en, flow);
                    IMMDeviceCollection devices = null;
                    try {
                        Check(en.EnumAudioEndpoints(flow, 1, out devices));
                        uint count; Check(devices.GetCount(out count));
                        for (uint i = 0; i < count; i++) {
                            IMMDevice device = null; IAudioEndpointVolume volume = null;
                            try {
                                Check(devices.Item(i, out device)); string id; Check(device.GetId(out id));
                                DeviceInfo info = new DeviceInfo { Id = id, Name = Name(device), Input = flow == 1, IsDefault = id == defaultId };
                                volume = Endpoint(device); float level; bool muted;
                                Check(volume.GetMasterVolumeLevelScalar(out level)); Check(volume.GetMute(out muted));
                                info.Volume = (int)Math.Round(level * 100); info.Muted = muted; snapshot.Devices.Add(info);
                                if (flow == 0 && info.IsDefault) {
                                    try { ReadSessions(device, id, snapshot.Sessions); }
                                    catch (Exception ex) { snapshot.Warning = "App mixer unavailable: " + ex.Message; }
                                }
                            } catch (COMException) { /* A device can disappear during enumeration. */ }
                            finally { Release(volume); Release(device); }
                        }
                    } finally { Release(devices); }
                }
            } catch (Exception ex) { snapshot.Error = ex.Message; }
            finally { Release(en); }
            return snapshot;
        }
        static void ReadSessions(IMMDevice device, string deviceId, List<SessionInfo> list)
        {
            object managerObject = null; IAudioSessionEnumerator sessions = null;
            try {
                Guid iid = SessionManagerId; Check(device.Activate(ref iid, 23, IntPtr.Zero, out managerObject));
                Check(((IAudioSessionManager2)managerObject).GetSessionEnumerator(out sessions));
                int count; Check(sessions.GetCount(out count));
                for (int i = 0; i < count; i++) {
                    IAudioSessionControl sessionBase = null;
                    try {
                        Check(sessions.GetSession(i, out sessionBase));
                        IAudioSessionControl2 session = (IAudioSessionControl2)sessionBase;
                        int state; Check(session.GetState(out state));
                        if (state == 2) continue;
                        uint pid; string id; Check(session.GetProcessId(out pid)); Check(session.GetSessionInstanceIdentifier(out id));
                        string name = "System sounds";
                        if (pid != 0) {
                            try { using (Process process = Process.GetProcessById((int)pid)) { name = process.ProcessName; } }
                            catch { name = "App " + pid; }
                        }
                        float level; bool muted; ISimpleAudioVolume volume = (ISimpleAudioVolume)session;
                        Check(volume.GetMasterVolume(out level)); Check(volume.GetMute(out muted));
                        list.Add(new SessionInfo { DeviceId = deviceId, Id = id, Name = name, Volume = (int)Math.Round(level * 100), Muted = muted });
                    } catch (COMException) { }
                    catch (InvalidCastException) { /* An individual session need not expose volume controls. */ }
                    finally { Release(sessionBase); }
                }
            } finally { Release(sessions); Release(managerObject); }
        }
        internal static void SetDeviceVolume(string id, int? level, bool? mute)
        {
            IMMDeviceEnumerator en = null; IMMDevice device = null; IAudioEndpointVolume volume = null;
            try {
                en = Enumerator(); Check(en.GetDevice(id, out device)); volume = Endpoint(device); Guid context = Context;
                if (level.HasValue) Check(volume.SetMasterVolumeLevelScalar(Math.Max(0, Math.Min(100, level.Value)) / 100f, ref context));
                if (mute.HasValue) Check(volume.SetMute(mute.Value, ref context));
            } finally { Release(volume); Release(device); Release(en); }
        }
        internal static void SetSessionVolume(string deviceId, string sessionId, int? level, bool? mute)
        {
            IMMDeviceEnumerator en = null; IMMDevice device = null; object manager = null; IAudioSessionEnumerator sessions = null;
            try {
                en = Enumerator(); Check(en.GetDevice(deviceId, out device)); Guid iid = SessionManagerId;
                Check(device.Activate(ref iid, 23, IntPtr.Zero, out manager)); Check(((IAudioSessionManager2)manager).GetSessionEnumerator(out sessions));
                int count; Check(sessions.GetCount(out count));
                for (int i = 0; i < count; i++) {
                    IAudioSessionControl sessionBase = null;
                    try {
                        Check(sessions.GetSession(i, out sessionBase));
                        IAudioSessionControl2 session = (IAudioSessionControl2)sessionBase;
                        string id; Check(session.GetSessionInstanceIdentifier(out id));
                        if (id != sessionId) continue;
                        ISimpleAudioVolume volume = (ISimpleAudioVolume)session; Guid context = Context;
                        if (level.HasValue) Check(volume.SetMasterVolume(Math.Max(0, Math.Min(100, level.Value)) / 100f, ref context));
                        if (mute.HasValue) Check(volume.SetMute(mute.Value, ref context));
                        return;
                    } finally { Release(sessionBase); }
                }
            } finally { Release(sessions); Release(manager); Release(device); Release(en); }
        }
        internal static void SetDefault(string id, bool communications)
        {
            object policy = null;
            try {
                policy = new PolicyConfigClient(); IPolicyConfig config = (IPolicyConfig)policy;
                Check(config.SetDefaultEndpoint(id, 0)); Check(config.SetDefaultEndpoint(id, 1));
                if (communications) Check(config.SetDefaultEndpoint(id, 2));
            } finally { Release(policy); }
        }
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant value);
    }

    [StructLayout(LayoutKind.Sequential)] internal struct PropertyKey { public Guid Format; public uint Id; }
    // PROPVARIANT is 24 bytes on x64 and 16 on x86; a larger out buffer is safe on x86.
    [StructLayout(LayoutKind.Explicit, Size = 24)] internal struct PropVariant
    { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] internal class MMDeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    { [PreserveSig] int GetCount(out uint count); [PreserveSig] int Item(uint index, out IMMDevice device); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore props);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint count);
        [PreserveSig] int VolumeStepUp(ref Guid context);
        [PreserveSig] int VolumeStepDown(ref Guid context);
        [PreserveSig] int QueryHardwareSupport(out uint mask);
        [PreserveSig] int GetVolumeRange(out float min, out float max, out float step);
    }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr guid, uint flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
        [PreserveSig] int RegisterSessionNotification(IntPtr callback);
        [PreserveSig] int UnregisterSessionNotification(IntPtr callback);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string session, IntPtr callback);
        [PreserveSig] int UnregisterDuckNotification(IntPtr callback);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    { [PreserveSig] int GetCount(out int count); [PreserveSig] int GetSession(int index, out IAudioSessionControl session); }
    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid group);
        [PreserveSig] int SetGroupingParam(ref Guid group, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr callback);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr callback);
    }
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid group);
        [PreserveSig] int SetGroupingParam(ref Guid group, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr callback);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr callback);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint processId);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }
    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid context);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
    // Windows' default-device policy interface is undocumented, as in many audio switchers.
    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] internal class PolicyConfigClient { }
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int defaultFormat, out IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr endpoint, IntPtr mix);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int defaultPeriod, out long period, out long minimum);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, ref long period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
    }
}
