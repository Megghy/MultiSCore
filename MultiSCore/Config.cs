using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TShockAPI;

namespace MultiSCore
{
    public class Config
    {
        public static void Load()
        {
            try
            {
                var path = Path.Combine(TShock.SavePath, "MSCConfig.json");
                if (!File.Exists(path)) FileTools.CreateIfNot(path, JsonConvert.SerializeObject(new Config()
                {
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
                else MSCPlugin.Instance.ServerConfig = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
                if (MSCPlugin.Instance.ServerConfig.Servers.Any(s => s.Key.StartsWith("Terraria")))
                {
                    TShock.Log.ConsoleInfo("<MultiSCore> 使用Terrariaxxx进入原版服务器可能造成一些问题");
                }
                TShock.Log.ConsoleInfo("<MultiSCore> Read config success.");
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError("<MultiSCore> Failed to read config: " + ex.Message);
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
        public string Key { get; set; }
        public string Name { get; set; }
        public bool AllowOthorServerJoin { get; set; }
        public bool AllowDirectJoin { get; set; }
        public bool RememberLastPoint { get; set; }
        public List<ForwordServer> Servers { get; set; }
    }
}
