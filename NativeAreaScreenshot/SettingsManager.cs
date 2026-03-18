using System;
using System.IO;
using Newtonsoft.Json;

namespace NativeAreaScreenshot
{
    public class AppSettings
    {
        public string SaveFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AreaScreenshots");
    }

    public class SettingsManager
    {
        private static SettingsManager _instance;
        public static SettingsManager Instance => _instance ??= new SettingsManager();

        private readonly string _settingsPath;
        private AppSettings _settings;

        private SettingsManager()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NativeAreaScreenshot");
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
            _settingsPath = Path.Combine(appData, "settings.json");
            Load();
        }

        public string SaveFolder 
        { 
            get => _settings.SaveFolder;
            set => _settings.SaveFolder = value;
        }

        public void Load()
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsPath);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    _settings = new AppSettings();
                }
            }
            else
            {
                _settings = new AppSettings();
            }
        }

        public void Save()
        {
            string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            File.WriteAllText(_settingsPath, json);
        }
    }
}
