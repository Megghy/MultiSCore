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
        public string Key { get; set; }
        public HookResult OnReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start, ref int length);
        public void OnRecieveCustomData(MSCHooks.RecieveCustomDataEventArgs args);
        public void OnSendData(SendBytesEventArgs args);
        public void OnConnectRequest(MSCHooks.PlayerJoinEventArgs args);
        public void OnPlayerLeave(LeaveEventArgs args);
        public void OnPlayerCommand(PlayerCommandEventArgs args);
    }
}
