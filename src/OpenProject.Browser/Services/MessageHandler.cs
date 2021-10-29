using System;
using System.Diagnostics;
using System.Windows;
using Serilog;

namespace OpenProject.Browser.Services
{
  public static class MessageHandler
  {
    public static void ShowError(string message)
    {
      Log.Error(message);
      MessageBox.Show(message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static void ShowError(Exception exception, string message)
    {
      Log.Error(exception, message);
      MessageBox.Show(message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static void ShowUpdateDialog(string oldVersion, string newVersion, DateTime releaseDate, string downloadUrl)
    {
      Log.Information("New version {new} available. Currently installed: {old}", newVersion, oldVersion);
      var messageBoxText = $"A new release '{newVersion}' was released at {releaseDate.ToLongDateString()}. " +
                           $"'{oldVersion}' is currently installed.\nDo you want to download the new version?";
      MessageBoxResult messageBoxResult = MessageBox.Show(
        messageBoxText,
        "New version available",
        MessageBoxButton.YesNo,
        MessageBoxImage.Information
      );

      if (messageBoxResult != MessageBoxResult.Yes) return;

      Process.Start(new ProcessStartInfo("cmd", $"/c start {downloadUrl}") { CreateNoWindow = true });
      Log.Information("Download of new version {new} started.", newVersion);
    }
  }
}
