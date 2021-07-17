using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using HttpServer;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using OTAPI;
using Terraria;
using Terraria.Net.Sockets;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace MultiSCore.Core
{
    public class HostServer : IServer
    {
        public HostServer(string name, string key)
        {
            Name = name;
            Key = key;
        }

        public string Name { get; set; }
        public string Key { get; set; }
        public void OnConnectRequest(MSCHooks.PlayerJoinEventArgs args)
        {
            //主机的连接请求不用管
        }
        public void OnPlayerLeave(LeaveEventArgs args)
        {
            if (MSCPlugin.Instance.ForwordPlayers[args.Who] is { } mscp)
            {
                mscp.Dispose();
            }
        }

        public HookResult OnReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start, ref int length)
        {
            if ((Netplay.Clients[buffer.whoAmI].State < 10 && packetid > 12 && packetid != 93 && packetid != 16 && packetid != 42 && packetid != 50 && packetid != 38 && packetid != 68) || (Netplay.Clients[buffer.whoAmI].State == 0 && packetid != 1))
            {
                TShock.Log.ConsoleInfo($"无效的操作 - {(PacketTypes)packetid}");
                return HookResult.Cancel;
            }
            if (MSCPlugin.Instance.ForwordPlayers[buffer.whoAmI] is { } mscp)
            {
                mscp.SendDataToForword(buffer.readBuffer, start - 2, length + 2);
                return HookResult.Cancel;
            }
            return MSCPlugin.Instance.OldGetDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
        }
        public void OnRecieveCustomData(MSCHooks.RecieveCustomDataEventArgs args)
        {
            try
            {
                var plr = args.Player;
                var reader = args.Reader;
                switch (args.Type)
                {
                    case Utils.CustomPacket.Command:
                        Commands.HandleCommand(plr, Commands.Specifier + reader.ReadString());
                        break;
                    case Utils.CustomPacket.Chat:
                        var msg = $"[{plr.GetMSCPlayer().Server.Name}] {reader.ReadString()}: {reader.ReadString()}";
                        TShock.Players.Where(p => p != null && p.Name != plr.Name).ForEach(p => p.SendMessage(msg, Color.White));
                        TShock.Log.Info(msg);
                        Console.WriteLine(msg);
                        break;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"<MultiSCore> An error occurred when process customdata: {ex}");
            }
        }
        public void OnPlayerCommand(PlayerCommandEventArgs args)
        {
            //主机玩家使用命令不用管
        }
        public void OnPlayerFinishSwitch(MSCHooks.PlayerFinishSwitchEventArgs args)
        {
            if (MSCPlugin.Instance.ForwordPlayers[args.Index] is { } mscp && mscp.Server.SpawnX == -1 && mscp.Server.SpawnY == -1)
            {
                mscp.SendDataToForword(new RawDataBuilder(Utils.CustomPacket.Command).PackString("MultiSCore_Spawn")); //如果没设置出生位置则传送到出生点
                mscp.SendDataToForword(new RawDataBuilder(Utils.CustomPacket.ServerList).PackString(MSCPlugin.Instance.Key).PackString(JsonConvert.SerializeObject(MSCPlugin.Instance.ServerConfig.Servers))); //发送服务器列表
            }
        }
        public HookResult OnSendData(ref int remoteClient, ref byte[] data, ref int offset, ref int size, ref SocketSendCallback callback, ref object state)
        {
            if (MSCPlugin.Instance.ForwordPlayers[remoteClient] is { } mscp && mscp.Server.Name != MSCPlugin.Instance.Server.Name)
            {
                return HookResult.Cancel;
            }
            return HookResult.Continue;
        }
    }
}
