using System;
using System.Linq;
using CefSharp.Wpf;
using OpenProject.Browser.WebViewIntegration;

namespace OpenProject.Browser.ViewModels
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
  }
}
