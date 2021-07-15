using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using OTAPI;
using Terraria;
using Terraria.Net.Sockets;
using TerrariaApi.Server;
using TShockAPI.Hooks;

namespace MultiSCore.Core
{
    public interface IServer
    {
        public string Name { get; set; }
        public IPAddress IP{ get; set; }
        public int Port { get; set; }
        public string Key { get; set; }
        public HookResult OnReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start, ref int length);
        public void OnRecieveCustomData(int index, Utils.CustomPacket type, BinaryReader reader);
        public void OnSendData(SendBytesEventArgs args);
        public void OnConnectRequest(int index, string key, string ip);
        public void OnPlayerConnect(ConnectEventArgs args);
        public void OnPlayerLeave(LeaveEventArgs args);
        public void OnPlayerCommand(PlayerCommandEventArgs args);
    }
}
