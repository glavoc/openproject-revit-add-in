using System;
using System.Configuration;
using System.Windows;

namespace OpenProject
{
  public static class UserSettings
  {
    public static string BCFierAppDataFolder { get; } =
      System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BCFier");

    /// <summary>
    /// Retrieves the user setting with the specified key, if nothing is found returns an empty string
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    private static string Get(string key)
    {
      try
      {
        Configuration config = GetConfig();

        if (config == null)
          return string.Empty;

        KeyValueConfigurationElement element = config.AppSettings.Settings[key];
        if (element != null)
        {
          var value = element.Value;
          if (!string.IsNullOrEmpty(value))
            return value;
        }
        else
        {
          config.AppSettings.Settings.Add(key, "");
          config.Save(ConfigurationSaveMode.Modified);
        }
      }
      catch (Exception ex1)
      {
        MessageBox.Show("exception: " + ex1);
      }

      return string.Empty;
    }

    /// <summary>
    /// Retrieves the user setting with the specified key and converts it to bool
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defValue">If the key is not found or invalid return this</param>
    /// <returns></returns>
    public static bool GetBool(string key, bool defValue = false)
    {
      return bool.TryParse(Get(key), out var value) ? value : defValue;
    }

    /// <summary>
    /// The configuration file used to store our settings
    /// Saved in a location accessible by all modules
    /// </summary>
    /// <returns></returns>
    private static Configuration GetConfig()
    {
      var settings = System.IO.Path.Combine(BCFierAppDataFolder, "settings.config");
      var configMap = new ExeConfigurationFileMap { ExeConfigFilename = settings };
      Configuration config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);

      if (config == null)
        MessageBox.Show("Error loading the Configuration file.", "Configuration Error", MessageBoxButton.OK,
          MessageBoxImage.Error);
      return config;
    }
  }
}
