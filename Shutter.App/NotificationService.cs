using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.WinUI.Notifications;

namespace Shutter.App;

public sealed class NotificationService : IDisposable
{
    public NotificationService()
    {
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
    }

    public void ShowSaved(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;

        new ToastContentBuilder()
            .AddText("Recording saved")
            .AddText(Path.GetFileName(path))
            .AddButton(new ToastButton().SetContent("Open Folder")
                .AddArgument("action", "openFolder")
                .AddArgument("path", directory))
            .AddButton(new ToastButton().SetContent("Play")
                .AddArgument("action", "play")
                .AddArgument("path", path))
            .Show();
    }

    public void ShowFallback(string? preferredName, string? fallbackName)
    {
        new ToastContentBuilder()
            .AddText("Microphone Fallback")
            .AddText($"Configured mic '{preferredName ?? "Unknown"}' not found.")
            .AddText($"Using '{fallbackName ?? "Default Microphone"}' instead. Edit config to update.")
            .Show();
    }

    public void ShowDeviceChanged(string deviceName, bool isAdded)
    {
        var status = isAdded ? "reconnected" : "disconnected";
        var description = isAdded 
            ? $"Recording will use your preferred mic." 
            : $"Recording will use default until reconnected.";

        new ToastContentBuilder()
            .AddText($"Microphone {status}")
            .AddText($"'{deviceName}' {status}.")
            .AddText(description)
            .Show();
    }

    public void ShowError(string message)
    {
        new ToastContentBuilder()
            .AddText("Recording Error")
            .AddText(message)
            .Show();
    }

    public void ShowStealthEnabled(string toggleHotkey, string quitHotkey)
    {
        new ToastContentBuilder()
            .AddText("Stealth mode on")
            .AddText($"Toggle: {toggleHotkey} · Quit: {quitHotkey}")
            .Show(toast => toast.ExpirationTime = DateTime.Now.AddMinutes(30));
    }

    private static void OnToastActivated(ToastNotificationActivatedEventArgsCompat args)
    {
        var parsed = ToastArguments.Parse(args.Argument);
        if (!parsed.TryGetValue("action", out var action) || !parsed.TryGetValue("path", out var path))
        {
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (action == "openFolder")
            {
                OpenFolder(path);
            }
            else if (action == "play")
            {
                OpenFile(path);
            }
        });
    }

    private static void OpenFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
    }

    private static void OpenFile(string path)
    {
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    public void Dispose()
    {
        ToastNotificationManagerCompat.OnActivated -= OnToastActivated;
    }
}
