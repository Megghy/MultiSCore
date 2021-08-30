using MultiSCore.Core;
using OTAPI;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
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
            Config.Load();
            Server = new(ServerConfig.Name, ServerConfig.Key);

            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit);
            GeneralHooks.ReloadEvent += OnReload;

            ServerApi.Hooks.NetGreetPlayer.Register(this, ForwordServer.OnGreetPlayer);
            ServerApi.Hooks.ServerLeave.Register(this, Server.OnPlayerLeave, int.MaxValue);
        }
        void OnPostInit(EventArgs args)
        {
            OldGetDataHandler = Hooks.Net.ReceiveData;
            Hooks.Net.ReceiveData = Server.OnReceiveData;
            OldSendDataHandler = Hooks.Net.SendBytes;
            Hooks.Net.SendBytes = Server.OnSendData;

            Commands.ChatCommands.Add(new("msc.use", OnCommand, "msc") { AllowServer = false });
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInit);
                if (OldGetDataHandler is not null)
                    Hooks.Net.ReceiveData = OldGetDataHandler;
                if (OldSendDataHandler is not null)
                    Hooks.Net.SendBytes = OldSendDataHandler;

                GeneralHooks.ReloadEvent -= OnReload;

                ServerApi.Hooks.NetGreetPlayer.Deregister(this, ForwordServer.OnGreetPlayer);
                ServerApi.Hooks.ServerLeave.Deregister(this, Server.OnPlayerLeave);
            }
        }
        public static MSCPlugin Instance;
        public Config ServerConfig = new();
        public ServerAdapter Server;
        public static string Key => Instance.Server.Key;
        internal Hooks.Net.ReceiveDataHandler OldGetDataHandler;
        internal Hooks.Net.SendBytesHandler OldSendDataHandler;
        /// <summary>
        /// 使用反代前往其他服务器的玩家
        /// </summary>
        public MSCPlayer[] ForwordPlayers = new MSCPlayer[256];
        /// <summary>
        /// 使用反代进入此服务器的玩家
        /// </summary>
        public Utils.HostInfo[] ForwordInfo = new Utils.HostInfo[256];
        public string[] VersionInfo = new string[256];
        void OnReload(ReloadEventArgs args)
        {
            Config.Load();
            Server.Key = ServerConfig.Key;
            Server.Name = ServerConfig.Name;
        }
        void OnCommand(CommandArgs args)
        {
            var plr = args.Player;
            var cmd = args.Parameters;
            var mscp = Instance.ForwordPlayers[plr.Index];
            if (plr.IsForwordPlayer())
            {
                plr.SendRawData(plr.GetCustomRawData(Utils.CustomPacket.Command).PackString(args.Message).GetByteData());
                return;
            }
            if (cmd.Any())
            {
                switch (cmd[0].ToLower())
                {
                    case "tp":
                    case "t":
                        if (plr.GetData<string>("MultiSCore_Switching") is { })
                        {
                            plr.SendErrorMsg(Utils.GetText("Command_IsSwitching"));
                            return;
                        }
                        if (cmd.Count > 1)
                        {
                            if (Instance.ServerConfig.Servers.FirstOrDefault(s => s.Name == cmd[1] || s.Name.ToLower().StartsWith(cmd[1])) is { } server)
                            {
                                if (!string.IsNullOrEmpty(server.Permission) && plr.HasPermission(server.Permission))
                                    plr.SendErrorMsg(string.Format(Utils.GetText("Command_NoPermission"), server.Name));
                                else
                                {
                                    if (mscp != null && mscp.Server.Name == server.Name)
                                        plr.SendErrorMsg(string.Format(Utils.GetText("Command_AlreadyIn"), server.Name));
                                    else
                                    {
                                        plr.SetData("MultiSCore_Switching", "");
                                        plr.SendInfoMsg(string.Format(Utils.GetText("Command_Switch"), server.Name));
                                        TShock.Log.ConsoleInfo(string.Format(Utils.GetText("Log_Switch"), plr.Name, server.Name));
                                        new MSCPlayer(plr.Index).SwitchServer(server);
                                    }
                                }
                            }
                            else plr.SendErrorMsg(string.Format(Utils.GetText("Command_ServerNotFound"), cmd[1]));
                        }
                        else plr.SendErrorMsg($"{Utils.GetText("Prompt_InvalidFormat")}\r\n{Utils.GetText("Help_Tp")}");
                        break;
                    case "back":
                    case "b":
                        if (mscp != null)
                        {
                            mscp.BackToHost();
                            plr.SendSuccessMsg(Utils.GetText("Command_Back"));
                            TShock.Log.ConsoleInfo(string.Format(Utils.GetText("Log_Back"), plr.Name));
                        }
                        else plr.SendErrorMsg(Utils.GetText("Command_NotJoined"));
                        break;
                    case "list":
                    case "l":
                        plr.SendSuccessMsg($"{Utils.GetText("Command_AviliableServer")}{string.Join(", ", ServerConfig.Servers.Where(s => s.Visible).Select(s => s.Name))}");
                        break;
                    case "password":
                    case "p":
                        if (mscp is { })
                        {
                            if (cmd.Count > 1)
                                mscp.SendDataToForword(new RawDataBuilder(38).PackString(cmd[1]));
                            else
                                plr.SendErrorMsg($"{Utils.GetText("Prompt_InvalidFormat")}\r\n{Utils.GetText("Help_Password")}");
                        }
                        else plr.SendInfoMsg(Utils.GetText("Command_NotJoined"));
                        break;
                    case "command":
                    case "cmd":
                    case "c":
                        if (mscp is { })
                        {
                            if (cmd.Count > 1)
                            {
                                cmd.RemoveAt(0);
                                Commands.HandleCommand(plr, string.Join(" ", cmd));
                            }
                            else
                                plr.SendErrorMsg($"{Utils.GetText("Prompt_InvalidFormat")}\r\n{Utils.GetText("Help_Command")}");
                        }
                        else plr.SendInfoMsg(Utils.GetText("Command_NotJoined"));
                        break;
                    case "online":
                    case "playing":
                    case "o":
                        var sb = new StringBuilder(); //todo
                        break;
                    default:
                        sendHelpText();
                        break;
                }
            }
            else sendHelpText();
            void sendHelpText()
            {
                plr.SendInfoMsg($"{Utils.GetText("Prompt_InvalidFormat")}\r\n" +
                    $"{Utils.GetText("Help_Tp")}\r\n" +
                    $"{Utils.GetText("Help_Back")}\r\n" +
                    $"{Utils.GetText("Help_List")}\r\n" +
                    $"{Utils.GetText("Help_Command")}"
                    );
            }
        }
    }
}
