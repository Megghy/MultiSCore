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
            var index = buffer.whoAmI;
            if ((Netplay.Clients[index].State < 10 && packetid > 12 && packetid != 93 && packetid != 16 && packetid != 42 && packetid != 50 && packetid != 38 && packetid != 68 && packetid != 15) || (Netplay.Clients[index].State == 0 && packetid != 1 && packetid != 15))
            {
                TShock.Log.ConsoleInfo($"当前状态下操作无效 - {(PacketTypes)packetid}, State: {Netplay.Clients[index].State}");
                return HookResult.Cancel;
            }
            buffer.reader.BaseStream.Position = start + 1;
            try
            {
                switch (packetid)
                {
                    case 15:
                        var type = (Utils.CustomPacket)buffer.reader.ReadByte();
                        if (!MSCHooks.OnRecieveCustomData(index, type, buffer.reader, out var recieveArgs))
                            OnRecieveCustomData(recieveArgs);
                        return HookResult.Cancel;
                    case 1:
                        var key = buffer.reader.ReadString();
                        if (key.StartsWith("Terraria") && !MSCPlugin.Instance.ServerConfig.AllowDirectJoin)
                        {
                            NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral("此服务器不允许直接连接"));
                        }
                        else
                        {
                            if (!MSCHooks.OnPlayerJoin(index, buffer.reader.ReadString(), key, buffer.reader.ReadString(), out var joinArgs)) OnConnectRequest(joinArgs);
                        }
                        return HookResult.Cancel;
                    default:
                        return MSCPlugin.Instance.OldGetDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
                }
            }
            catch (Exception ex) { TShock.Log.ConsoleError($"<MultiSCore> Forword recieve packet error: {ex.Message}"); return HookResult.Cancel; }
        }
        public override void OnRecieveCustomData(MSCHooks.RecieveCustomDataEventArgs args)
        {
            base.OnRecieveCustomData(args);
        }
        public override void OnPlayerCommand(PlayerCommandEventArgs args)
        {
            base.OnPlayerCommand(args);
        }
        public override void OnPlayerFinishSwitch(MSCHooks.PlayerFinishSwitchEventArgs args)
        {
            Main.npc.ForEach(n => NetMessage.SendData(23, args.Index, -1, null, n.whoAmI));
        }
        public override HookResult OnSendData(ref int remoteClient, ref byte[] data, ref int offset, ref int size, ref SocketSendCallback callback, ref object state)
        {
            if (data[2] == 129 && size < 5 && !MSCHooks.OnPlayerFinishSwitch(remoteClient, out var finishJoinArgs)) MSCPlugin.Instance.Server.OnPlayerFinishSwitch(finishJoinArgs);
            return HookResult.Continue;  //连接到的服务器不需要对senddata作出更改 顺着socket发回去就行了
        }
    }
}
