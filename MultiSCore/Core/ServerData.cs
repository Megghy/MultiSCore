using OTAPI;
using System;
using System.IO;
using System.Net;
using Terraria;
using TerrariaApi.Server;

namespace MultiSCore.Core
{
    class TempServer : IServer
    {
        public TempServer(string name, IPAddress ip, int port, string key)
        {
            Name = name;
            IP = ip;
            Port = port;
            Key = key;
        }
        public string Name { get; set; }
        public IPAddress IP { get; set; }
        public int Port { get; set; }
        public string Key { get; set; }

        public void OnConnectRequest(int index, string key)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerConnect(ConnectEventArgs args)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerLeave(LeaveEventArgs args)
        {
            throw new NotImplementedException();
        }

        public HookResult OnReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start, ref int length)
        {
            throw new NotImplementedException();
        }

        public void OnRecieveCustomData(Utils.CustomPacket type, BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        public void OnSendData(SendBytesEventArgs args)
        {
            throw new NotImplementedException();
        }
    }
}
