using System;
using System.IO;

namespace OpenProject.Shared
{
  public static class ConfigurationConstant
  {
    public static string OpenProjectApplicationData =
      Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OpenProject.Revit");
  }
}
