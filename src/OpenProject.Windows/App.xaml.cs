using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpenProject.Shared.Logging;
using OpenProject.Windows.Services;
using Serilog;

namespace OpenProject.Windows
{
  /// <summary>
  /// Root application class. Initializes and keeps state of the IoC container.
  /// </summary>
  public partial class App
  {
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
      _serviceProvider = ServiceProviderConfiguration
        .ConfigureIoCContainer()
        .BuildServiceProvider();

      Logger.ConfigureLogger("OpenProject.Log..txt");
    }

    private void OnStartUp(object sender, StartupEventArgs e)
    {
      Log.Information("OpenProject Browser for Revit started.");
      _serviceProvider.GetService<MainWindowView>().Show();
    }
  }
}
