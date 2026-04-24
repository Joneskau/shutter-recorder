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
