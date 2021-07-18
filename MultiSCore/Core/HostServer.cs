using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using OTAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Net.Sockets;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace MultiSCore.Core
{
    public class HostServer : ServerBase
    {
        public HostServer(string name, string key)
        {
            Name = name;
            Key = key;
        }

        public override string Name { get; set; }
        public override string Key { get; set; }
        public override void OnPlayerLeave(LeaveEventArgs args)
        {
            if (MSCPlugin.Instance.ForwordPlayers[args.Who] is { } mscp)
            {
                mscp.Dispose();
            }
        }
        public override HookResult OnReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start, ref int length)
        {
            if ((Netplay.Clients[buffer.whoAmI].State < 10 && packetid > 12 && packetid != 93 && packetid != 16 && packetid != 42 && packetid != 50 && packetid != 38 && packetid != 68) || (Netplay.Clients[buffer.whoAmI].State == 0 && packetid != 1))
            {
                TShock.Log.ConsoleInfo($"无效的操作 - {(PacketTypes)packetid}");
                return HookResult.Cancel;
            }
            var index = buffer.whoAmI;
            buffer.reader.BaseStream.Position = start + 1;
            try
            {
                switch (packetid)
                {
                    case 15:
                        var type = (Utils.CustomPacket)buffer.reader.ReadByte();
                        if (!MSCHooks.OnRecieveCustomData(index, type, buffer.reader, out var recieveArgs))
                            OnRecieveCustomData(recieveArgs);
                        break;
                    case 1:
                        var key = buffer.reader.ReadString();
                        if (key.StartsWith("Terraria") && !MSCPlugin.Instance.ServerConfig.AllowDirectJoin)
                        {
                            NetMessage.TrySendData(2, index, -1, Terraria.Localization.NetworkText.FromLiteral("此服务器不允许直接连接"));
                        }
                        else
                        {
                            if (!MSCHooks.OnPlayerJoin(index, buffer.reader.ReadString(), key, buffer.reader.ReadString(), out var joinArgs)) OnConnectRequest(joinArgs);
                        }
                        break;
                    default:
                        if (MSCPlugin.Instance.ForwordPlayers[buffer.whoAmI] is { } mscp)
                        {
                            mscp.SendDataToForword(buffer.readBuffer, start - 2, length + 2);
                            return HookResult.Cancel;
                        }
                        return MSCPlugin.Instance.OldGetDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
                        break;
                }
            }
            catch (Exception ex) { TShock.Log.ConsoleError($"<MultiSCore> Host recieve packet error: {ex.Message}"); return HookResult.Cancel; }
            
        }
        public override void OnRecieveCustomData(MSCHooks.RecieveCustomDataEventArgs args)
        {
            base.OnRecieveCustomData(args);
            if(!args.Handled)
                switch (args.Type)
                {
                    case Utils.CustomPacket.ConnectSuccess:
                        TShock.Log.ConsoleInfo($"{args.Player.Name} 来自另一个加载了 {"MultiSCore".Color("B3CE95")} 插件的主服务器<host server>");
                        break;
                }
        }
        public override void OnPlayerCommand(PlayerCommandEventArgs args)
        {
            base.OnPlayerCommand(args);
        }
        public override void OnPlayerFinishSwitch(MSCHooks.PlayerFinishSwitchEventArgs args)
        {
            if (MSCPlugin.Instance.ForwordPlayers[args.Index] is { } mscp && (mscp.Server.SpawnX == -1 || mscp.Server.SpawnY == -1))
            {
                mscp.SendDataToForword(new RawDataBuilder(Utils.CustomPacket.Command).PackString(mscp.Key).PackString("MultiSCore_Spawn")); //如果没设置出生位置则传送到出生点
                mscp.SendDataToForword(new RawDataBuilder(Utils.CustomPacket.ServerList).PackString(mscp.Key).PackString(JsonConvert.SerializeObject(MSCPlugin.Instance.ServerConfig.Servers))); //发送服务器列表
                mscp.SendDataToForword(new RawDataBuilder(Utils.CustomPacket.ServerList).PackString(mscp.Key)); //发送成功连接
                TShock.Log.ConsoleInfo($"<MultiSCore> {args.Player.Name} 成功传送.");
                args.Player.SendSuccessMsg($"成功传送到服务器 {mscp.Server.Name}");
            }
        }
        public override HookResult OnSendData(ref int remoteClient, ref byte[] data, ref int offset, ref int size, ref SocketSendCallback callback, ref object state)
        {
            if (MSCPlugin.Instance.ForwordPlayers[remoteClient] is { } mscp && mscp.Server.Name != MSCPlugin.Instance.Server.Name)
            {
                return HookResult.Cancel;
            }
            return HookResult.Continue;
        }
    }
}
