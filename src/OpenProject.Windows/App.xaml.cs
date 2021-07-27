using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpenProject.Windows.Services;

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
    }

    private void OnStartUp(object sender, StartupEventArgs e) => _serviceProvider.GetService<MainWindowView>().Show();
  }
}
