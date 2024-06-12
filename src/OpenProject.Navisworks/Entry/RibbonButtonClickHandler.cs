//using Autodesk.Revit.UI;
using Autodesk.Navisworks.Api;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComBridge = Autodesk.Navisworks.Api.ComApi.ComApiBridge;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using OpenProjectNavisworks.Services;
using OpenProject.Shared;
using Serilog;
using ZetaIpc.Runtime.Helper;
using System.Windows.Controls;
using OpenProjectNavisworks.Data;
using System.Windows.Media.Media3D;
using static Autodesk.Navisworks.Gui.Roamer.CommandLineConfig;
using OpenProject.Shared.Logging;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;


namespace OpenProjectNavisworks.Entry;
  public static class RibbonButtonClickHandler
  {
#if N2024
    public const string NavisAPIVersion = "21";
    public const string NavisVersion = "2024";
#elif N2023
    public const string NavisAPIVersion = "20";
    public const string NavisVersion = "2023";
#elif N2022
    public const string NavisAPIVersion = "19";
    public const string NavisVersion = "2022";
#elif N2021
    public const string NavisAPIVersion = "18";
    public const string NavisVersion = "2021";
#elif N2020
    public const string NavisAPIVersion = "17";
    public const string NavisVersion = "2020";
#else
    public const string NavisAPIVersion = "16";
    public const string NavisVersion = "2019";
#endif

    private static Process _opBrowserProcess;
    public static IpcHandler IpcHandler { get; private set; }

    public static int OpenMainPluginWindow(Document commandData, ref string message)
    {
      try
      {
            Logger.ConfigureLogger("OpenProject.Navisworks.Log..txt");
            EnsureExternalOpenProjectAppIsRunning(commandData);
            Log.Information("EnsureExternalOpenProjectAppIsRunning");
            IpcHandler.SendBringBrowserToForegroundRequestToDesktopApp();
            Log.Information("SendBringBrowserToForegroundRequestToDesktopApp");

            return 1;
      }
      catch (Exception exception)
      {
            Logger.ConfigureLogger("OpenProject.Navisworks.Log..txt");

            message = exception.Message;
            Log.Error(exception, message);
            return 0;
      }
    }

    public static int OpenSettingsPluginWindow(Document commandData, ref string message)
    {
      try
      {
        EnsureExternalOpenProjectAppIsRunning(commandData);
        IpcHandler.SendOpenSettingsRequestToDesktopApp();
        IpcHandler.SendBringBrowserToForegroundRequestToDesktopApp();
        return 1;
      }
      catch (Exception exception)
      {
        message = exception.Message;
        Logger.ConfigureLogger("OpenProject.Navisworks.Log..txt");
        Log.Error(exception, message);
        return 0;
      }
    }

    private static async void EnsureExternalOpenProjectAppIsRunning(Document commandData)
    {
        Logger.ConfigureLogger("OpenProject.Navisworks.Log..txt");

        //Version check
        if (!Autodesk.Navisworks.Api.Application.Version.ApiMajor.ToString().Contains(NavisAPIVersion))
        {
            MessageHandler.ShowWarning(
              "Unexpected version",
              "The Navisworks version does not match the expectations.",
              $"This Add-In was built and tested only for Navisworks {NavisVersion}. Further usage is at your own risk");
        }

      if (_opBrowserProcess is { HasExited: false })
        return;
        else
        {
            // Clear dict after closing browser window
            NavisworksUtils.ModelItems = null;
        }

        
      IpcHandler = new IpcHandler(Autodesk.Navisworks.Api.Application.ActiveDocument);
      var revitServerPort = IpcHandler.StartLocalServerAndReturnPort();

      var openProjectBrowserExecutablePath = GetOpenProjectBrowserExecutable();
      if (!File.Exists(openProjectBrowserExecutablePath))
        throw new SystemException("Browser executable not found.");

      var opBrowserServerPort = FreePortHelper.GetFreePort();
      var processArguments = $"ipc {opBrowserServerPort} {revitServerPort}";
      Environment.CurrentDirectory = System.IO.Path.GetDirectoryName(openProjectBrowserExecutablePath);
      _opBrowserProcess = Process.Start(openProjectBrowserExecutablePath, processArguments);
      IpcHandler.StartLocalClient(opBrowserServerPort);
      Log.Information("IPC bridge started between port {port1} and {port2}.",
        opBrowserServerPort, revitServerPort);


        // Initialize document ModelItems dictionary -- for hiding objects. Disabled, because not optimized.
        NavisworksUtils.ModelModelItems = null;
        if (NavisworksUtils.ModelModelItems == null)
        {
            //NavisworksUtils.ModelItems = null;
            //await GenerateModelItemDictAsync(Model.NavisworksWrapper.Document);
            //MessageHandler.ShowWarning("Need to wait",
            //                          "Dictionary of objects is in process, need to wait. This can take several minutes",
            //                          "Dictionary of objects is in process, need to wait. This can take several minutes");
            //NavisworksUtils.ModelModelItems = new Dictionary<string, Dictionary<string, ModelItem>>();
            // BuildModelDictionary(Model.NavisworksWrapper.Document).Wait();
            //BuildModelDictionary(Model.NavisworksWrapper.Document);
            //NavisworksUtils.ModelItems = await BuildModelDictionaryAsync();
            //GenerateModelItemDict(Model.NavisworksWrapper.Document);
        }

    }

    private static string GetOpenProjectBrowserExecutable()
    {
      var currentAssemblyPathUri = Assembly.GetExecutingAssembly().CodeBase;
      var currentAssemblyPath = Uri.UnescapeDataString(new Uri(currentAssemblyPathUri).AbsolutePath).Replace("/", "\\");
      var currentFolder = Path.GetDirectoryName(currentAssemblyPath) ?? string.Empty;

      return Path.Combine(currentFolder, ConfigurationConstant.OpenProjectBrowserExecutablePath);
    }



    // TODO: Next functions are for hiding objects feature. Disabled for now, because not optimized --------------------------------------------------------------------
    static void GenerateModelItemDict(Document doc)
    {
        Dictionary<string, ModelItem> modelItems = new Dictionary<string, ModelItem>();
        List<ModelItem> hiddenModelItems = new List<ModelItem>();
        foreach (ModelItem mi in doc.Models.RootItemDescendantsAndSelf)
        {
            var k = mi.InstanceGuid;
            if (k != null)
            {
                var kStr = k.ToString();
                if (!modelItems.ContainsKey(kStr))
                    modelItems.Add(kStr, mi);
                // if (mi.IsHidden)
                //   hiddenModelItems.Add(mi);
            }

        }
        NavisworksUtils.ModelItems = modelItems;
        //NavisworksUtils.HiddenModelItems = hiddenModelItems;
        MessageHandler.ShowWarning(
              "Object dictionary is not ready yet!",
              "Object dictionary is ready, you can use ViewPoint now",
              "Object dictionary is ready");
    }

    static async Task GenerateModelItemDictAsync(Document doc)
    {
        await Task.Run(() => GenerateModelItemDict(doc));
    }


    static async Task<bool> BuildModelDictionary(Document doc)
    {

        // Get all models
        //var models = GetDocumentModels(doc);
        var models = GetItemFromLastModelBranch(doc.Models.ToList());

        // Async process each model
        //foreach (var model in models)
        //{
        //    Task.Run(() => ProcessModelAsync(model));
        //}
        foreach (var md in models)
        {
            if( md == null)
            {
                var stop =1 ;
            }
           // ProcessModelAsync(md);
        }

        var tasks = models.Where(t => t != null).Select(ProcessModelAsync);
        await Task.WhenAll(tasks);

        MessageHandler.ShowWarning(
              "Object dictionary is not ready yet!",
              "Object dictionary is ready, you can use ViewPoint now",
              "Object dictionary is ready");

        var dct = NavisworksUtils.ModelModelItems;

        return true;
    }
    static void ProcessModel(Autodesk.Navisworks.Api.Model model)
    {
        // Go through each ModelItem in model
        Dictionary<string, ModelItem> modelItems = new Dictionary<string, ModelItem>();

        var rootItem = model.RootItem;
        var descendants = rootItem.Descendants;

        foreach (ModelItem item in descendants)
        {
            
            // Getting GUID attribute for each ModelItem
            var guid = item.InstanceGuid.ToString();

            if (guid != null)
            {
                if (!modelItems.ContainsKey(guid))
                {
                    modelItems.Add(guid, item);
                }
            }
        }
        var modelFileName = Path.GetFileName(model.SourceFileName);
        if (!NavisworksUtils.ModelModelItems.ContainsKey(modelFileName))
            NavisworksUtils.ModelModelItems.Add(modelFileName, modelItems);
    }

    static async Task ProcessModelAsync(Autodesk.Navisworks.Api.Model model)
    {
        await Task.Run(() => ProcessModel(model));
    }

    public static List<Autodesk.Navisworks.Api.Model> GetDocumentModels(Document doc)
    {
        var models = doc.Models;


        if (models.Count == 1)
        {
            var mainModel = models[0];
            var mainModelModelItem = mainModel.RootItem;
            var mainModelChildren = mainModelModelItem.Children;
            return models[0].RootItem.Children.Select(ch => ch.Model).ToList();
        }
        else
        {
            return models.ToList();
        }

    }
    public static List<Autodesk.Navisworks.Api.Model> GetItemFromLastModelBranch(List<Autodesk.Navisworks.Api.Model> models)
    {
        List<Autodesk.Navisworks.Api.Model> modelsResult = new List<Autodesk.Navisworks.Api.Model>();
        foreach (var model in models) 
        {
            var modelChildren = model.RootItem.Children.ToList();
            if (modelChildren.Count == 0 || modelChildren[0].Model == null)
            {
                modelsResult.Add(model);
            }
            else
            {
                modelsResult.AddRange(GetItemFromLastModelBranch(modelChildren.Select(t => t.Model).ToList()));
            }
        }
        return modelsResult;   
    }
}

