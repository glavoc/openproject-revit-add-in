using System.Threading.Tasks;
using System.Windows;
using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using OpenProject.Browser.Services;
using OpenProject.Shared;
using Serilog;

namespace OpenProject.Browser.WebViewIntegration
{
  public class JavaScriptBridge
  {
    private readonly OpenProjectInstanceValidator _instanceValidator;


    /// <summary>
    /// This is the name of the global window object that's set in JavaScript, e.g.
    /// 'window.RevitBridge'.
    /// </summary>
    public const string REVIT_BRIDGE_JAVASCRIPT_NAME = "RevitBridge";

    public const string REVIT_READY_EVENT_NAME = "revit.plugin.ready";

    private bool _isLoaded;

    public JavaScriptBridge(OpenProjectInstanceValidator instanceValidator)
    {
      _instanceValidator = instanceValidator;
    }

    private ChromiumWebBrowser _webBrowser;

    public event WebUIMessageReceivedEventHandler OnWebUIMessageReveived;

    public delegate void WebUIMessageReceivedEventHandler(object sender, WebUIMessageEventArgs e);

    public event AppForegroundRequestReceivedEventHandler OnAppForegroundRequestReceived;

    public delegate void AppForegroundRequestReceivedEventHandler(object sender);

    private void ChangeLoadingState(object sender, object eventArgs) => _isLoaded = true;

    public void SetWebBrowser(ChromiumWebBrowser webBrowser)
    {
      if (_webBrowser != null)
      {
        _webBrowser.LoadingStateChanged -= ChangeLoadingState;
      }

      _webBrowser = webBrowser;
      _webBrowser.LoadingStateChanged += ChangeLoadingState;
    }


    public void SendMessageToRevit(string messageType, string trackingId, string messagePayload)
    {
      if (!_isLoaded)
      {
        Log.Warning("Failed to send message to Revit: bridge web browser not loaded");
        return;
      }

      Log.Information("Sending message of type {A} to Revit", messageType);

      if (messageType == MessageTypes.INSTANCE_SELECTED)
      {
        // This is the case at the beginning when the user selects which instance of OpenProject
        // should be accessed. We're not relaying this to Revit.
        HandleInstanceNameReceived(messagePayload);
      }
      else if (messageType == MessageTypes.ADD_INSTANCE)
      {
        // Simply save the instance to the white list and do nothing else.
        ConfigurationHandler.SaveSelectedInstance(messagePayload);
      }
      else if (messageType == MessageTypes.REMOVE_INSTANCE)
      {
        ConfigurationHandler.RemoveSavedInstance(messagePayload);
      }
      else if (messageType == MessageTypes.ALL_INSTANCES_REQUESTED)
      {
        var allInstances = JsonConvert.SerializeObject(ConfigurationHandler.LoadAllInstances());
        SendMessageToOpenProject(MessageTypes.ALL_INSTANCES, trackingId, allInstances);
      }
      else if (messageType == MessageTypes.FOCUS_REVIT_APPLICATION)
      {
        RevitMainWindowHandler.SetFocusToRevit();
      }
      else if (messageType == MessageTypes.GO_TO_SETTINGS)
      {
        VisitUrl(LandingIndexPageUrl());
      }
      else if (messageType == MessageTypes.SET_BROWSER_TO_FOREGROUND)
      {
        OnAppForegroundRequestReceived?.Invoke(this);
      }
      else if (messageType == MessageTypes.VALIDATE_INSTANCE)
      {
        Task.Run(async () => await ValidateInstanceAsync(trackingId, messagePayload));
      }
      else
      {
        var eventArgs = new WebUIMessageEventArgs(messageType, trackingId, messagePayload);
        OnWebUIMessageReveived?.Invoke(this, eventArgs);
        // For some UI operations, revit should be focused
        RevitMainWindowHandler.SetFocusToRevit();
      }

      // Hacky solution to directly send focus back to OP.
      if (messageType == MessageTypes.VIEWPOINT_DATA)
      {
        OnAppForegroundRequestReceived?.Invoke(this);
      }
    }

    public void SendMessageToOpenProject(string messageType, string trackingId, string messagePayload)
    {
      if (!_isLoaded)
      {
        Log.Warning("Failed to send message to OpenProject: bridge web browser not loaded");
        return;
      }

      Log.Information("Sending message of type {A} to OpenProject", messageType);

      if (messageType == MessageTypes.CLOSE_DESKTOP_APPLICATION)
      {
        // This message means we should exit the application
        System.Environment.Exit(0);
        return;
      }
      else if (messageType == MessageTypes.GO_TO_SETTINGS)
      {
        VisitUrl(LandingIndexPageUrl());
      }
      else if (messageType == MessageTypes.SET_BROWSER_TO_FOREGROUND)
      {
        OnAppForegroundRequestReceived?.Invoke(this);
      }
      else if (messageType == MessageTypes.VIEWPOINT_GENERATED)
      {
        OnAppForegroundRequestReceived?.Invoke(this);
      }

      var messageData = JsonConvert.SerializeObject(new { messageType, trackingId, messagePayload });
      var encodedMessage = JsonConvert.ToString(messageData);
      Application.Current.Dispatcher.Invoke(() =>
      {
        _webBrowser?.GetMainFrame()
          .ExecuteJavaScriptAsync($"{REVIT_BRIDGE_JAVASCRIPT_NAME}.sendMessageToOpenProject({encodedMessage})");
      });
    }

    private void VisitUrl(string url)
    {
      Application.Current.Dispatcher.Invoke(() => { _webBrowser.Address = url; });
    }

    private static string LandingIndexPageUrl() => EmbeddedLandingPageHandler.GetEmbeddedLandingPageIndexUrl();

    private void HandleInstanceNameReceived(string instanceName)
    {
      ConfigurationHandler.SaveSelectedInstance(instanceName);
      VisitUrl(instanceName);
    }

    private async Task ValidateInstanceAsync(string trackingId, string message)
    {
      var (isValid, instanceBaseUrl) = await _instanceValidator.IsValidOpenProjectInstanceAsync(message);

      SendMessageToOpenProject(MessageTypes.VALIDATED_INSTANCE,
        trackingId,
        JsonConvert.SerializeObject(new { isValid, instanceBaseUrl }));
    }
  }
}
