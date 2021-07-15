using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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
        public IPAddress IP { get; set; }
        public int Port { get; set; }
        public string Key { get; set; }
        public void OnConnectRequest(int index, string key, string ip)
        {
            //主机的连接请求不用管
        }
        public void OnPlayerConnect(ConnectEventArgs args) 
        {
            //主服务器玩家连接不用管
        }

        public void OnPlayerLeave(LeaveEventArgs args)
        {
            if (MSCMain.Instance.ForwordPlayers[args.Who] is { } mscp)
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
            if (MSCMain.Instance.ForwordPlayers[buffer.whoAmI] is { } mscp)
            {
                mscp.SendDataToForword(buffer.readBuffer, start - 2, length + 2);
                return HookResult.Cancel;
            }
            return MSCMain.Instance.OldGetDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
        }
        public void OnRecieveCustomData(int index, Utils.CustomPacket type, BinaryReader reader)
        {
            try
            {
                var plr = TShock.Players[index];
                switch (type)
                {
                    case Utils.CustomPacket.Command:
                        var aaa = reader.ReadString();
                        Commands.HandleCommand(plr, Commands.Specifier + aaa);
                        break;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"<MultiSCore> 接收CustomData时发生错误: {ex}");
            }
            
        }
        public void OnPlayerCommand(PlayerCommandEventArgs args)
        {

        }
        public void OnSendData(SendBytesEventArgs args)
        {
            if (MSCMain.Instance.ForwordPlayers[args.Socket.Id] is { } mscp && mscp.Server.Name != MSCMain.Instance.Server.Name)
            {
                args.Handled = true;
            }
        }
    }
}
