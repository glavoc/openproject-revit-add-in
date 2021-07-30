using System;
using System.IO;

namespace OpenProject.Shared
{
  public static class ConfigurationConstant
  {
    private const string _openProjectRevitAddInFolderName = "OpenProject.Revit";

    public static readonly string OpenProjectApplicationData =
      Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        _openProjectRevitAddInFolderName);
  }
}
