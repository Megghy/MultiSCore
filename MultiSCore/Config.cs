using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Terraria.ID;
using TShockAPI;

namespace MultiSCore
{
    public class Config
    {
        public static void Load()
        {
            try
            {
                var directoryPath = Path.Combine(TShock.SavePath, "MultiSCore");
                var configPath = Path.Combine(directoryPath, "MSCConfig.json");
                if (!Directory.Exists(directoryPath) || !File.Exists(configPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    CreateFiles();
                }
                MSCPlugin.Instance.ServerConfig = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));
                var languagePath = Path.Combine(directoryPath, MSCPlugin.Instance.ServerConfig.LanguageFileName);
                if (File.Exists(languagePath))
                    MSCPlugin.Instance.ServerConfig.Language = JObject.Parse(File.ReadAllText(languagePath));
                else
                {
                    TShock.Log.ConsoleError($"File {MSCPlugin.Instance.ServerConfig.LanguageFileName} doesn't exist");
                    MSCPlugin.Instance.ServerConfig.Language = JObject.Parse(Encoding.Default.GetString(Properties.Resources.zh_cn));
                }
                if (MSCPlugin.Instance.ServerConfig.Servers.Any(s => s.Key.StartsWith("Terraria")))
                {
                    TShock.Log.ConsoleInfo(Utils.GetText("Log_VanillaWarn"));
                }
                TShock.Log.ConsoleInfo("<MultiSCore> Read config success.");
                void CreateFiles()
                {
                    FileTools.CreateIfNot(configPath, JsonConvert.SerializeObject(new Config()
                    {
                        LanguageFileName = "zh_cn.json",
                        AllowDirectJoin = true,
                        AllowOthorServerJoin = false,
                        Key = Guid.NewGuid().ToString(),
                        Name = "host",
                        RememberLastPoint = true,
                        Servers = new()
                        {
                            new() { Key = "1145141919810", Visible = true, Permission = "", IP = "yfeil.top", Port = 7777, Name = "yfeil", SpawnX = -1, SpawnY = -1, RememberHostInventory = true, GlobalCommand = new() { "online", "who" } }
                        }
                    }, Formatting.Indented));
                    File.WriteAllBytes(Path.Combine(directoryPath, "zh_cn.json"), Properties.Resources.zh_cn);
                    File.WriteAllBytes(Path.Combine(directoryPath, "en_us.json"), Properties.Resources.en_us);
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError("<MultiSCore> Failed to read config: " + ex.Message);
                Console.ReadKey();
            }
        }
        public class ForwordServer
        {
            public string Key { get; set; }
            public bool Visible { get; set; }
            public string Permission { get; set; }
            public string IP { get; set; }
            public int Port { get; set; }
            public string Name { get; set; }
            public int SpawnX { get; set; }
            public int SpawnY { get; set; }
            public bool RememberHostInventory { get; set; }
            public List<string> GlobalCommand { get; set; } = new();
        }
        [JsonIgnore]
        public JObject Language { get; set; }
        public string LanguageFileName { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public bool AllowOthorServerJoin { get; set; }
        public bool AllowDirectJoin { get; set; }
        public bool RememberLastPoint { get; set; }
        public List<ForwordServer> Servers { get; set; }
    }
}
