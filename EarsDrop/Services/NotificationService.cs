using System;
using System.Diagnostics;
using System.IO;

namespace EarsDrop.Services;

public interface INotificationService
{
    void ShowNotification(string title, string message, string? filePath = null);
    void OpenFileLocation(string filePath);
}

public sealed class NotificationService : INotificationService
{
    public void ShowNotification(string title, string message, string? filePath = null)
    {
        Console.WriteLine($"[NOTIFICATION] {title}: {message}");

        try
        {
            // On Linux / desktop native notify-send fallback
            if (OperatingSystem.IsLinux())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notify-send",
                    Arguments = $"\"{title}\" \"{message}\"",
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Ignore native notification delivery errors
        }
    }

    public void OpenFileLocation(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var folder = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(folder))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch
        {
            // Fallback
        }
    }
}
