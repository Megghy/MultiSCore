using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using TShockAPI;
using TShockAPI.Hooks;

namespace MultiSCore
{
    public class Config
    {
        public static void Load()
        {
            try
            {
                var path = Path.Combine(TShock.SavePath, "MSCConfig.json");
                if (!File.Exists(path)) FileTools.CreateIfNot(path, JsonConvert.SerializeObject(new Config() { 
                    IsHost = true,
                    Key = Guid.NewGuid().ToString(),
                    Name = "host",
                    RememberLastPoint = true,
                    AllowDirectJoin = true,
                    Servers = new()
                    {
                        new() { Key = "1145141919810", Visible = true, Permission = "", IP = "yfeil.top", Port = 7777, Name = "yfeil", SpawnX = -1, SpawnY = -1, GlobalCommand = new() { "online", "who" } }
                    }
            }, Formatting.Indented));
                else MSCPlugin.Instance.ServerConfig = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
                if (MSCPlugin.Instance.ServerConfig.Servers.Any(s => s.Key.StartsWith("Terraria")))
                {
                    TShock.Log.ConsoleInfo("<MultiSCore> 服务器秘钥禁止使用Terraria开头");
                    Console.ReadKey();
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
            public List<string> GlobalCommand { get; set; } = new();
        }
        public bool IsHost { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public bool RememberLastPoint { get; set; }
        public bool AllowDirectJoin { get; set; }
        public List<ForwordServer> Servers { get; set; }
    }
}
