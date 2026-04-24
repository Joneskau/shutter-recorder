namespace Shutter.Core;

public interface IDeviceHealthService
{
    /// <summary>
    /// Returns the resolved device to use. 
    /// If preferred device is unavailable, returns the default 
    /// and raises a fallback notification.
    /// </summary>
    DeviceResolution ResolveDevice(string? preferredDeviceId);
}

public record DeviceResolution(
    string? DeviceId,
    string? DeviceName,
    bool IsFallback,
    string? OriginalDeviceName
);
