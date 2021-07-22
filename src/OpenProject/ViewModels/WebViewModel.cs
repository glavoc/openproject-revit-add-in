using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CefSharp.Wpf;
using OpenProject.Api;
using OpenProject.WebViewIntegration;
using OpenProject.Windows;

namespace OpenProject.ViewModels
{
  public sealed class WebViewModel
  {
    private readonly JavaScriptBridge _javaScriptBridge;
    private readonly BrowserManager _browserManager;

    public ChromiumWebBrowser Browser => _browserManager.Browser;

    public WebViewModel(BrowserManager browserManager, JavaScriptBridge javaScriptBridge)
    {
      _browserManager = browserManager;
      _javaScriptBridge = javaScriptBridge;

      _browserManager.Initialize();
      InitializeIpcConnection();

      if (UserSettings.GetBool("checkupdates"))
        CheckUpdates();
    }

    private void InitializeIpcConnection()
    {
      var commandLineArgs = Environment.GetCommandLineArgs();

      if (commandLineArgs.All(arg => arg != "ipc")) return;

      var args = commandLineArgs.SkipWhile(arg => arg != "ipc").Skip(1).Take(2).ToList();
      if (args.Count < 2) return;

      var serverPort = int.Parse(args[0]);
      var clientPort = int.Parse(args[1]);
      IpcManager.StartIpcCommunication(_javaScriptBridge, serverPort, clientPort);
    }

    private static void CheckUpdates()
    {
      Task.Run(() =>
      {
        try
        {
          GitHubRelease release = GitHubRest.GetLatestRelease();
          if (release == null) return;

          var onlineIsNewer = new Api.Version(release.tag_name).CompareTo(new Api.Version(VersionsService.Version)) > 0;
          if (!onlineIsNewer) return;

          void ConfirmAndDownloadUpdate()
          {
            var dialog = new NewVersion
            {
              WindowStartupLocation = WindowStartupLocation.CenterScreen,
              Description =
              {
                Text = release.name + " has been released on " + release.published_at.ToLongDateString() +
                       "\ndo you want to check it out now?"
              }
            };
            dialog.ShowDialog();
            if (!dialog.DialogResult.HasValue || !dialog.DialogResult.Value) return;

            var downloadUrl = release.html_url.Replace("&", "^&");
            Process.Start(new ProcessStartInfo("cmd", $"/c start {downloadUrl}") { CreateNoWindow = true });
          }

          Application.Current.Dispatcher.Invoke(ConfirmAndDownloadUpdate);
        }
        catch (Exception ex1)
        {
          Console.WriteLine("exception: " + ex1);
        }
      });
    }
  }
}
