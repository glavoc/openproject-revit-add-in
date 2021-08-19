using System.Collections.Generic;
using Newtonsoft.Json;

namespace OpenProject.Browser.Settings
{
  public static class OpenProjectSettingsExtensions
  {
    public static List<string> GetOpenProjectInstances(this IOpenProjectSettings settings)
    {
      if (settings.OpenProjectInstances == null)
      {
        return null;
      }

      return JsonConvert.DeserializeObject<List<string>>(settings.OpenProjectInstances);
    }

    public static void SetOpenProjectInstances(this IOpenProjectSettings settings, List<string> openProjectInstances)
    {
      if (openProjectInstances == null)
      {
        settings.OpenProjectInstances = null;
      }
      else
      {
        settings.OpenProjectInstances = JsonConvert.SerializeObject(openProjectInstances);
      }
    }
  }
}
