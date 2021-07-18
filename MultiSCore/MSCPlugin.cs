using HttpServer;
using MultiSCore.Core;
using Newtonsoft.Json;
using OTAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace MultiSCore
{
    [ApiVersion(2, 1)]
    public class MSCPlugin : TerrariaPlugin
    {
        public MSCPlugin(Main game) : base(game) { Instance = this; }
        public override string Name => "MultiSCore";
        public override string Author => "Megghy";
        public override string Description => "一个简单的跨服传送插件";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;
        public override void Initialize()
        {
            if (TShock.VersionNum < new Version(1, 4, 2, 0)) TShock.Log.ConsoleInfo("<MultiSCore> TShock版本低于1.4.2.0, 插件将不会进行加载");
            else
                ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit);
        }
        void OnPostInit(EventArgs args)
        {
            Config.Load();
            if (ServerConfig.IsHost)
            {
                Server = new HostServer(ServerConfig.Name, ServerConfig.Key);
                ServerApi.Hooks.ServerLeave.Register(this, Server.OnPlayerLeave, int.MaxValue);
                IsHost = true;
            }
            else
            {
                Server = new ForwordServer(ServerConfig.Name, ServerConfig.Key);
                IsHost = false;
                PlayerHooks.PlayerChat += (chat) =>
                {
                    chat.Player.SendRawData(new RawDataBuilder(Utils.CustomPacket.Chat).PackString(Server.Key).PackString(chat.Player.Name).PackString(chat.RawText).GetByteData());
                };
            }

            OldGetDataHandler = Hooks.Net.ReceiveData;
            Hooks.Net.ReceiveData = Server.OnReceiveData;
            Hooks.Net.SendBytes += Server.OnSendData;

            GeneralHooks.ReloadEvent += OnReload;
            PlayerHooks.PlayerCommand += Server.OnPlayerCommand;

            ServerApi.Hooks.ServerJoin.Register(this, (join) => {
                var data = new RawDataBuilder(Utils.CustomPacket.ConnectSuccess).PackString(Key).GetByteData();
                Netplay.Clients[join.Who].Socket.AsyncSend(data, 0, data.Length, delegate { });
            });

            Commands.ChatCommands.Add(new("msc.use", OnCommand, "msc"));
        }
        public static MSCPlugin Instance;
        public bool IsHost;
        public Config ServerConfig = new();
        public ServerBase Server;
        public static string Key => Instance.Server.Key;
        internal Hooks.Net.ReceiveDataHandler OldGetDataHandler;
        public MSCPlayer[] ForwordPlayers = new MSCPlayer[256];
        void OnReload(ReloadEventArgs args)
        {
            Config.Load();
            Server.Key = ServerConfig.Key;
            Server.Name = ServerConfig.Name;
            if (IsHost)
            {
                var temp = new List<string>();
                var list = ForwordPlayers.Where(p => p is { }).ToList();
                var data = new RawDataBuilder(Utils.CustomPacket.ServerList).PackString(Key).PackString(JsonConvert.SerializeObject(ServerConfig));
                list.ForEach(p =>
                {
                    if (!temp.Contains(p.Server.Name))
                    { temp.Add(p.Server.Name); p.SendDataToForword(data); }
                });
            }
        }
        void OnCommand(CommandArgs args)
        {
            if (!IsHost)
            {
                args.Player.SendErrorMsg($"副服务器无法使用此命令");
                return;
            }
            
            var plr = args.Player;
            var cmd = args.Parameters;
            var mscp = Instance.ForwordPlayers[plr.Index];
            if (cmd.Any())
            {
                switch (cmd[0].ToLower())
                {
                    case "tp":
                    case "t":
                        if (cmd.Count > 1)
                        {
                            if (Instance.ServerConfig.Servers.FirstOrDefault(s => s.Name.ToLower().Contains(cmd[1])) is { } server)
                            {
                                if (!string.IsNullOrEmpty(server.Permission) && plr.HasPermission(server.Permission))
                                    plr.SendErrorMsg($"你没有权限进入服务器 {server.Name}");
                                else
                                {
                                    if (mscp != null && mscp.Server.Name == server.Name)
                                        plr.SendErrorMsg($"你已处于服务器 {server.Name} 中");
                                    else
                                    {
                                        plr.SendInfoMsg($"正在传送至服务器 {server.Name}");
                                        TShock.Log.ConsoleInfo($"<MultiSCore> 玩家 {plr.Name} 准备传送至服务器 {server.Name}");
                                        mscp?.Dispose();
                                        ForwordPlayers[plr.Index] = new(plr.Index);
                                        ForwordPlayers[plr.Index].SwitchServer(server);
                                    }
                                }
                            }
                            else plr.SendErrorMsg($"未找到含有关键词 {cmd[1]} 的服务器");
                        }
                        else plr.SendErrorMsg($"无效的格式.\r\n" +
                   $"/msc tp([c/B3CE95:t]) <[c/B3CE95:服务器名]>  --  传送到指定服务器");
                        break;
                    case "back":
                    case "b":
                        if (mscp != null)
                        {
                            mscp.BackToHost();
                            plr.SendSuccessMsg($"已返回主服务器");
                            TShock.Log.ConsoleInfo($"<MultiSCore> 玩家 {plr.Name} 返回主服务器");
                        }
                        else plr.SendErrorMessage("你已处于主服务器中");
                        break;
                    case "list":
                    case "l":
                        plr.SendSuccessMsg($"可用的服务器: {string.Join(", ", ServerConfig.Servers.Where(s => s.Visible).Select(s => s.Name))}");
                        break;
                    case "password":
                    case "p":
                        if (mscp is { })
                        {
                            if (cmd.Count > 1)
                                mscp.SendDataToForword(new RawDataBuilder(38).PackString(cmd[1]));
                            else
                                plr.SendErrorMsg($"无效的格式.\r\n" +
                   $"/msc password([c/B3CE95:p]) <[c/B3CE95:密码]>  --  向当前连接到的服务器发送指定的密码");
                        }
                        else plr.SendInfoMsg($"你尚未进入任何服务器");
                        break;
                    default:
                        sendHelpText();
                        break;
                }
            }
            else sendHelpText();
            void sendHelpText()
            {
                plr.SendErrorMsg($"无效的命令.\r\n" +
                    $"/msc tp([c/B3CE95:t]) <[c/B3CE95:服务器名]>  --  传送到指定服务器\r\n" +
                    $"/msc back([c/B3CE95:b])  --  传送回主服务器\r\n" +
                    $"/msc list([c/B3CE95:l])  --  列出所有可用的服务器"
                    );
            }
        }
    }
}
