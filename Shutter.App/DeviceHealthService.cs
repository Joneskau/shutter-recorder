using System;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Shutter.Core;

namespace Shutter.App;

public class DeviceHealthService : IDeviceHealthService, IMMNotificationClient, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;

    public event Action<string, string>? DeviceRemoved;
    public event Action<string, string>? DeviceAdded;

    public DeviceHealthService()
    {
        _enumerator = new MMDeviceEnumerator();
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public DeviceResolution ResolveDevice(string? preferredDeviceId)
    {
        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        
        if (string.IsNullOrWhiteSpace(preferredDeviceId) || preferredDeviceId == "default")
        {
            var defaultDevice = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            return new DeviceResolution(defaultDevice?.ID, defaultDevice?.FriendlyName, false, null);
        }

        var preferred = devices.FirstOrDefault(d => d.ID == preferredDeviceId);
        if (preferred != null)
        {
            return new DeviceResolution(preferred.ID, preferred.FriendlyName, false, null);
        }

        // Fallback scenario
        var fallback = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        return new DeviceResolution(
            fallback?.ID, 
            fallback?.FriendlyName, 
            true, 
            "Configured Device" // We don't have the original name if it's missing, but we can do our best or just say "Configured Device"
        );
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        if (newState == DeviceState.Active)
        {
            TryNotifyAdded(deviceId);
        }
        else if (newState == DeviceState.Unplugged || newState == DeviceState.NotPresent || newState == DeviceState.Disabled)
        {
            TryNotifyRemoved(deviceId);
        }
    }

    public void OnDeviceAdded(string pwstrDeviceId)
    {
        TryNotifyAdded(pwstrDeviceId);
    }

    public void OnDeviceRemoved(string deviceId)
    {
        TryNotifyRemoved(deviceId);
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) { }

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

    private void TryNotifyAdded(string deviceId)
    {
        try
        {
            var device = _enumerator.GetDevice(deviceId);
            if (device.DataFlow == DataFlow.Capture)
            {
                DeviceAdded?.Invoke(deviceId, device.FriendlyName);
            }
        }
        catch { }
    }

    private void TryNotifyRemoved(string deviceId)
    {
        // When removed, we might not be able to get the FriendlyName anymore,
        // so we just pass the ID and let the UI handle it.
        DeviceRemoved?.Invoke(deviceId, "A microphone");
    }

    public void Dispose()
    {
        _enumerator.UnregisterEndpointNotificationCallback(this);
        _enumerator.Dispose();
    }
}
