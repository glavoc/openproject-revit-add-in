using Autodesk.Navisworks.Gui;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Internal;
using OpenProject.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using iabi.BCF.APIObjects.V21;
using Newtonsoft.Json.Converters;
using OpenProjectNavisworks.Extensions;
using OpenProjectNavisworks.Services;
using OpenProject.Shared.BcfApi;
using ZetaIpc.Runtime.Client;
using ZetaIpc.Runtime.Server;
using ZetaIpc.Runtime.Helper;
using OpenProjectNavisworks.Data;

namespace OpenProjectNavisworks.Entry;
public class IpcHandler
{
    private readonly Document _uiApp;
    private Action<string> _sendData;
    private static readonly object _callbackStackLock = new();
    private static readonly Stack<Action> _callbackStack = new();

    public IpcHandler(Document uiApp)
    {
        _uiApp = uiApp ?? throw new ArgumentNullException(nameof(uiApp));


        Autodesk.Navisworks.Api.Application.Idle += (_, _) =>
        {
            lock (_callbackStackLock)
            {
                if (!_callbackStack.Any()) return;

                _callbackStack.Pop().Invoke();
            }
        };
    }

    public int StartLocalServerAndReturnPort()
    {
        var freePort = FreePortHelper.GetFreePort();
        var server = new IpcServer();
        server.Start(freePort);
        server.ReceivedRequest += (_, e) =>
        {
            var eventArgs = JsonConvert.DeserializeObject<WebUiMessageEventArgs>(e.Request);
            if (eventArgs == null) return;

            var localMessageType = eventArgs.MessageType;
            var localTrackingId = eventArgs.TrackingId;
            var localMessagePayload = eventArgs.MessagePayload;

            lock (_callbackStackLock)
            {
                _callbackStack.Push(() =>
              {
                  switch (localMessageType)
                  {
                      case MessageTypes.VIEWPOINT_DATA:
                          {
                              try
                              {
                                  BcfViewpointWrapper bcfViewpoint = MessageDeserializer.DeserializeBcfViewpoint(
                                  new WebUiMessageEventArgs(localMessageType, localTrackingId, localMessagePayload));
                                  
                              // TODO: (if else) Hiding objects feature
                              //if(NavisworksUtils.ModelItems != null) 
                                  OpenViewpointEventHandler.ShowBcfViewpoint(bcfViewpoint);
                              //else
                              //{
                              //    MessageHandler.ShowWarning(
                              //    "Object dictionary is not ready yet!",
                              //    "Object dictionary is ready, you can use ViewPoint now",
                              //    "Object dictionary is ready");
                              //}
                              }
                              catch (Exception exception)
                              {
                                  MessageHandler.ShowError(exception, "Error opening a viewpoint.");
                              }

                              break;
                          }
                      case MessageTypes.VIEWPOINT_GENERATION_REQUESTED:
                          try
                          {

                                AddViewpoint(localTrackingId);

                          }
                          catch (Exception exception)
                          {
                              MessageHandler.ShowError(exception, "Error generating a viewpoint.");
                          }

                          break;
                  }
              });
            }
        };

        return freePort;
    }

    public void StartLocalClient(int ipcWinServerPort)
    {
        var client = new IpcClient();
        client.Initialize(ipcWinServerPort);
        _sendData = message =>
        {
            try
            {
                client.Send(message);
            }
            catch (System.Net.WebException)
            {
                // We can ignore the WebException, it's raised after
                // the shutdown event due to the other side just closing
                // the open TCP connection. This is what's expected😊
            }
        };
    }

    public void SendShutdownRequestToDesktopApp()
    {
        var eventArgs = new WebUiMessageEventArgs(MessageTypes.CLOSE_DESKTOP_APPLICATION, "0", string.Empty);
        var jsonEventArgs = JsonConvert.SerializeObject(eventArgs);
        _sendData(jsonEventArgs);
    }

    public void SendOpenSettingsRequestToDesktopApp()
    {
        var eventArgs = new WebUiMessageEventArgs(MessageTypes.GO_TO_SETTINGS, "0", string.Empty);
        var jsonEventArgs = JsonConvert.SerializeObject(eventArgs);
        _sendData(jsonEventArgs);
    }

    public void SendBringBrowserToForegroundRequestToDesktopApp()
    {
        var eventArgs = new WebUiMessageEventArgs(MessageTypes.SET_BROWSER_TO_FOREGROUND, "0", string.Empty);
        var jsonEventArgs = JsonConvert.SerializeObject(eventArgs);
        _sendData(jsonEventArgs);
    }

    /// <summary>
    /// Generates a viewpoint from the active view and sends the data as an event to bridge.
    /// </summary>
    /// <param name="trackingId">The local message tracking id.</param>
    private void AddViewpoint(string trackingId)
    {
        JObject payload = GenerateJsonViewpoint();

        // TODO: remove hack of snapshot data once #39135 is deployed
        payload["snapshot"] = payload["snapshot"]?["snapshot_data"];

        var eventArgs = new WebUiMessageEventArgs(MessageTypes.VIEWPOINT_GENERATED, trackingId, payload.ToString());
        var so = JsonConvert.SerializeObject(eventArgs);
        _sendData(so);
    }

    private JObject GenerateJsonViewpoint()
    {
        var serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        serializerSettings.Converters.Add(new StringEnumConverter(new SnakeCaseNamingStrategy(), false));

        Viewpoint_POST viewpoint = _uiApp.GenerateViewpoint();
        return JObject.Parse(JsonConvert.SerializeObject(viewpoint, serializerSettings));
    }
}

