using MultiSCore.Core;
using OTAPI;
using System;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace MultiSCore
{
    [ApiVersion(2, 1)]
    public class MSCMain : TerrariaPlugin
    {
        public MSCMain(Main game) : base(game) { Instance = this; }
        public override string Name => "MultiSCore";
        public override string Author => "Megghy";
        public override string Description => "一个简单的跨服传送插件";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;
        public override void Initialize()
        {
            Config.Load();
            if (ServerConfig.IsHost)
            {
                Server = new HostServer(ServerConfig.Name, ServerConfig.Key);
                IsHost = true;
            }
            else
            {
                Server = new ForwordServer(ServerConfig.Name, ServerConfig.Key);
                IsHost = false;
            }

            OldGetDataHandler = Hooks.Net.ReceiveData;
            Hooks.Net.ReceiveData = Server.OnReceiveData;

            ServerApi.Hooks.NetSendBytes.Register(this, Server.OnSendData, int.MaxValue);
            ServerApi.Hooks.ServerLeave.Register(this, Server.OnPlayerLeave, int.MaxValue);
            ServerApi.Hooks.ServerConnect.Register(this, Server.OnPlayerConnect, int.MaxValue);

            TShockAPI.Hooks.GeneralHooks.ReloadEvent += Config.Load;
            TShockAPI.Hooks.PlayerHooks.PlayerCommand += Server.OnPlayerCommand;

            Commands.ChatCommands.Add(new("msc.use", OnCommand, "msc"));
        }
        public static MSCMain Instance;
        public bool IsHost;
        public Config ServerConfig = new();
        internal IServer Server;
        internal Hooks.Net.ReceiveDataHandler OldGetDataHandler;
        public MSCPlayer[] ForwordPlayers = new MSCPlayer[256];
        void OnCommand(CommandArgs args)
        {
            var plr = args.Player;
            var cmd = args.Parameters;
            var mscp = Instance.ForwordPlayers[plr.Index];
            if (cmd.Any())
            {
                switch (cmd[0].ToLower())
                {
                    case "tp":
                        if (cmd.Count > 1)
                        {
                            if (Instance.ServerConfig.Servers.FirstOrDefault(s => s.Name.ToLower().Contains(cmd[1])) is { } server)
                            {
                                if (mscp != null && mscp.Server.Name == server.Name)
                                {
                                    plr.SendErrorMsg($"你已处于服务器 {server.Name} 中");
                                    return;
                                }
                                plr.SendInfoMsg($"正在传送至服务器 {server.Name}");
                                if (mscp == null) ForwordPlayers[plr.Index] = new(plr.Index);
                                ForwordPlayers[plr.Index].SwitchServer(server);
                            }
                        }
                        break;
                    case "back":
                        if (mscp != null)
                        {
                            mscp.BackToHost();
                            plr.SendSuccessMsg($"已返回主服务器");
                        }
                        else plr.SendErrorMessage("你已处于主服务器中");
                        break;
                }
            }
            else
            {
                ForwordPlayers[plr.Index] = new(plr.Index);
                ForwordPlayers[plr.Index].SwitchServer(ServerConfig.Servers.First());
            }
        }
    }
}
