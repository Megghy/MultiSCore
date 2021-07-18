using Newtonsoft.Json;
using OTAPI;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.Net.Sockets;
using TShockAPI;
using TShockAPI.Hooks;

namespace MultiSCore.Core
{
    public class ForwordServer : ServerBase
    {
        public ForwordServer(string name, string key)
        {
            Name = name;
            Key = key;
        }
        public override string Name { get; set; }
        public override string Key { get; set; }
        public override HookResult OnReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start, ref int length)
        {
            if (base.OnReceiveData(buffer, ref packetid, ref readoffset, ref start, ref length) == HookResult.Continue)
                return MSCPlugin.Instance.OldGetDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
            else
                return HookResult.Cancel;
        }
        public override void OnRecieveCustomData(MSCHooks.RecieveCustomDataEventArgs args)
        {
            base.OnRecieveCustomData(args);
            if (!args.Handled)
                switch (args.Type)
                {
                    case Utils.CustomPacket.ConnectSuccess:
                        TShock.Log.ConsoleInfo($"<MultiSCore> {args.Player.Name} 来自另一个启用了 MultiSCore 插件的服务器 {args.Player.GetData<Config>("MultiSCore_ServerInfo")?.Name}");
                        if (!MSCHooks.OnPlayerFinishSwitch(args.Index, out var finishJoinArgs)) MSCPlugin.Instance.Server.OnPlayerFinishSwitch(finishJoinArgs);
                        break;
                }
        }
        public override void OnPlayerFinishSwitch(MSCHooks.PlayerFinishSwitchEventArgs args)
        {
            Main.npc.ForEach(n => NetMessage.SendData(23, args.Index, -1, null, n.whoAmI));
        }
    }
}
