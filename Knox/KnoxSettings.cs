using Newtonsoft.Json;
using System;
using System.IO;

namespace Knox
{
    public class KnoxSettings
    {
        public string TenantId { get; set; }
        public string ClientId { get; set; }

        private static string GetSettingsFilePath()
        {
            var userDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configFilePath = Path.Combine(userDataFolder, "Knox", "knox.json");
            return configFilePath;
        }

        private static void CreateDefaultSettingsIfNeeded(string configFilePath)
        {
            // Create the directory to save the settings in if it doesn't exist yet
            var fileInfo = new FileInfo(configFilePath);
            if (!fileInfo.Directory.Exists)
            {
                fileInfo.Directory.Create();
            }

            // If the settings file doesn't exist yet, create an empty one
            if (!fileInfo.Exists)
            {
                var defaultSettings = new KnoxSettings();
                Save(defaultSettings);
            }
        }

        private static KnoxSettings instance;
        public static KnoxSettings Current
        { 
            get
            {
                if (instance == null)
                {
                    var configFilePath = GetSettingsFilePath();
                    CreateDefaultSettingsIfNeeded(configFilePath);
                    var fileContents = File.ReadAllText(configFilePath);
                    instance = JsonConvert.DeserializeObject<KnoxSettings>(fileContents);
                }
                return instance;
            }
        }

        public static void Save(KnoxSettings settingsToSave)
        {
            var settingsJson = JsonConvert.SerializeObject(settingsToSave);
            File.WriteAllText(GetSettingsFilePath(), settingsJson);
        }
    }
}
