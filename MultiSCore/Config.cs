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
        public static void Load(ReloadEventArgs args = null)
        {
            try
            {
                var path = Path.Combine(TShock.SavePath, "MSCConfig.json");
                if (!File.Exists(path)) FileTools.CreateIfNot(path, JsonConvert.SerializeObject(new Config() { 
                    IsHost = true,
                    Key = Guid.NewGuid().ToString(),
                    Name = "host",
                    Servers = new()
                    {
                        new() { Visible = true, IP = "127.0.0.1", Port = 7776, Key = Guid.NewGuid().ToString(), Name = "game", SpawnX = -1, SpawnY = -1, GlobalCommand = new() { "online", "who" } }
                    }
            }, Formatting.Indented));
                else MSCMain.Instance.ServerConfig = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
                TShock.Log.ConsoleInfo("<MultiSCore> 成功读取配置文件.");
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError("<MultiSCore> 读取配置文件失败.\r\n" + ex.Message);
            }
        }
        public struct ForwordServer
        {
            public bool Visible { get; set; }
            public string IP { get; set; }
            public int Port { get; set; }
            public string Key { get; set; }
            public string Name { get; set; }
            public int SpawnX { get; set; }
            public int SpawnY { get; set; }
            public List<string> GlobalCommand { get; set; }
        }
        public bool IsHost { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public List<ForwordServer> Servers { get; set; }
    }
}
