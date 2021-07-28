using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using OpenProject.Shared;
using Serilog;

namespace OpenProject.Revit.Entry
{
  [Transaction(TransactionMode.Manual)]
  public class AppMain : IExternalApplication
  {
    private readonly string _path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    #region Revit IExternalApplciation Implementation

    /// <summary>
    /// Startup
    /// </summary>
    /// <param name="application"></param>
    /// <returns></returns>
    public Result OnStartup(UIControlledApplication application)
    {
      try
      {
        ConfigureLogger();

        // Tab
        const string tabName = "OpenProject";
        const string panelName = "BCF management";
        application.CreateRibbonTab(tabName);
        RibbonPanel panel = application.CreateRibbonPanel(tabName, panelName);

        // Button Data
        RibbonItem browserButtonItem = panel.AddItem(
          new PushButtonData("OpenProject",
            "OpenProject",
            Path.Combine(_path, "OpenProject.Revit.dll"),
            "OpenProject.Revit.Entry.CmdMain"));

        if (browserButtonItem is PushButton browserButton)
        {
          browserButton.Image = LoadPngImgSource("OpenProject.Revit.Assets.OpenProjectLogo16.png");
          browserButton.LargeImage = LoadPngImgSource("OpenProject.Revit.Assets.OpenProjectLogo32.png");
          browserButton.ToolTip = "OpenProject browser";
        }

        RibbonItem settingsButtonItem = panel.AddItem(
          new PushButtonData("Settings",
            "Settings",
            Path.Combine(_path, "OpenProject.Revit.dll"),
            "OpenProject.Revit.Entry.CmdMainSettings"));

        if (settingsButtonItem is PushButton settingsButton)
        {
          settingsButton.Image = LoadPngImgSource("OpenProject.Revit.Assets.Settings32.png");
          settingsButton.LargeImage = LoadPngImgSource("OpenProject.Revit.Assets.Settings32.png");
          settingsButton.ToolTip = "OpenProject Revit Add-in settings";
        }
      }
      catch (Exception exception)
      {
        MessageBox.Show("exception: " + exception);
        return Result.Failed;
      }

      return Result.Succeeded;
    }

    private void ConfigureLogger()
    {
      var logFilePath = Path.Combine(
        ConfigurationConstant.OpenProjectApplicationData,
        "logs",
        "OpenProject.Revit.Log..txt");

      Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
        .WriteTo.Console()
        .CreateLogger();
    }

    /// <summary>
    /// Shut Down
    /// </summary>
    /// <param name="application"></param>
    /// <returns></returns>
    public Result OnShutdown(UIControlledApplication application)
    {
      try
      {
        RibbonButtonClickHandler.IpcHandler?.SendShutdownRequestToDesktopApp();
      }
      catch
      {
        // TODO -> What to do when Bcfier.Win can't be stopped?
      }

      return Result.Succeeded;
    }

    #endregion

    #region Private Members

    /// <summary>
    /// Get System Architecture
    /// </summary>
    /// <returns></returns>
    static string ProgramFilesx86()
    {
      if (8 == IntPtr.Size || (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"))))
        return Environment.GetEnvironmentVariable("ProgramFiles(x86)");

      return Environment.GetEnvironmentVariable("ProgramFiles");
    }


    /// <summary>
    /// Load an Image Source from File
    /// </summary>
    /// <param name="sourceName"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    private ImageSource LoadPngImgSource(string resourceName)
    {
      try
      {
        // Assembly & Stream
        var assembly = typeof(AppMain).Assembly;
        var icon = assembly.GetManifestResourceStream(resourceName);

        // Decoder
        PngBitmapDecoder m_decoder =
          new PngBitmapDecoder(icon, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);

        // Source
        ImageSource m_source = m_decoder.Frames[0];
        return (m_source);
      }
      catch
      {
      }

      // Fail
      return null;
    }

    #endregion
  }
}
