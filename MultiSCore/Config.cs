using Newtonsoft.Json;
using System;
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
                    AllowDirectJoin = false,
                    Servers = new()
                    {
                        new() { Visible = true, Permission = "", IP = "127.0.0.1", Port = 7776, Name = "game", SpawnX = -1, SpawnY = -1, GlobalCommand = new() { "online", "who" } }
                    }
            }, Formatting.Indented));
                else MSCPlugin.Instance.ServerConfig = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
                TShock.Log.ConsoleInfo("<MultiSCore> Read config success.");
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError("<MultiSCore> Failed to read config: " + ex.Message);
            }
        }
        public class ForwordServer
        {
            public bool Visible { get; set; }
            public string Permission { get; set; }
            public string IP { get; set; }
            public int Port { get; set; }
            public string Name { get; set; }
            public int SpawnX { get; set; }
            public int SpawnY { get; set; }
            public List<string> GlobalCommand { get; set; }
        }
        public bool IsHost { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public bool RememberLastPoint { get; set; }
        public bool AllowDirectJoin { get; set; }
        public List<ForwordServer> Servers { get; set; }
    }
}
