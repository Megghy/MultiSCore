using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent.NetModules;
using Terraria.Localization;
using TShockAPI;

namespace MultiSCore
{
    public class MSCPlayer : IDisposable
    {
        public MSCPlayer(int index)
        {
            Index = index;
        }
        public int Index { get; set; }
        public int ForwordIndex { get; set; }
        public TSPlayer Player { get { return TShock.Players[Index]; } set { } }
        public bool Connected { get; set; } = false;
        public Config.ForwordServer Server { get; set; }
        public TcpClient Connection { get; set; }
        public byte[] Buffer { get; set; }
        public string IP { get; set; } = "";
        public void Reset()
        {
            Connection?.Client?.Shutdown(SocketShutdown.Both);
            Connection?.Client?.Close();
            Connection?.Close();
            Connection = null;
            Buffer = null;
            Connected = false;
        }
        public void SendMessage(string text, Color color = default)
        {
            color = color == default ? Color.White : color;
            Terraria.Net.NetManager.Instance.SendToClient(NetTextModule.SerializeServerMessage(NetworkText.FromLiteral(text), color, (byte)Index), Index);
        }
        public void SwitchServer(Config.ForwordServer server)
        {
            try
            {
                if (server.Name == MSCMain.Instance.Server.Name)
                {
                    SendMessage($"<MultiSCore> 你已在服务器 {server.Name} 中");
                    return;
                }
                if (Connection is { Connected: true }) Connection.Close();
                Player.TPlayer.active = false;
                NetMessage.SendData(14, -1, Player.Index, null, Index, false.GetHashCode()); //隐藏原服务器玩家

                Connection = new();
                Buffer = new byte[1024];
                Server = server;

                Connection.Connect(server.IP, server.Port);
                SendDataToForword(new RawDataBuilder(1).PackString(server.Key).PackString(Player.IP));  //发起连接请求
                SendDataToForword(new RawDataBuilder(Utils.CustomPacket.ServerList).PackString(server.Key).PackString(JsonConvert.SerializeObject(MSCMain.Instance.ServerConfig.Servers)));  //发送服务器列表
                Task.Run(RecieveLoop);
            }
            catch (Exception ex)
            {
                NetMessage.SendData(14, -1, -1, null, Index); //显示原服务器玩家 
                TShock.Log.ConsoleError(ex.Message);
            }
        }
        public void BackToHost()
        {
            Dispose();
            if (MSCMain.Instance.IsHost)
            {
                int sectionX = Netplay.GetSectionX(0);
                int sectionX2 = Netplay.GetSectionX(Main.maxTilesX);
                int sectionX3 = Netplay.GetSectionX(0);
                int sectionX4 = Netplay.GetSectionX(Main.maxTilesY);
                for (int i = sectionX; i <= sectionX2; i++)
                {
                    for (int j = sectionX3; j <= sectionX4; j++)
                    {
                        Netplay.Clients[Index].TileSections[i, j] = false;
                    }
                }
                Player.Spawn(PlayerSpawnContext.SpawningIntoWorld);
                NetMessage.SendData(7, Index);
            }
        }
        public void SendDataToForword(byte[] buffer, int start = -1, int size = -1)
        {
            SocketAsyncEventArgs args = new();
            args.SetBuffer(buffer, start == -1 ? 0 : start, size == -1 ? buffer.Length : size);
            Connection?.Client.SendAsync(args);
        }
        public void SendDataToClient(byte[] buffer)
        {
            Player.SendRawData(buffer);
        }
        public void SendDataToForword(RawDataBuilder data) => SendDataToForword(data.GetByteData());
        private void RecieveLoop()
        {
            try
            {
                TShock.Log.ConsoleInfo($"<MultiSCore> 开始监听并转发 {Player.Name} 的来自远端服务器 {Server.Name} 的数据包");
                while (Connection is { Connected: true })
                {
                    int size = Connection.Client.Receive(Buffer);
                    switch (Buffer[2])
                    {
                        case 3:
                            ForwordIndex = Buffer[3];
                            break;
                        case 7:
                            if (!Connected)
                            {
                                SendDataToForword(new RawDataBuilder(8).PackInt32(Server.SpawnX).PackInt32(Server.SpawnY));
                                SendDataToForword(new RawDataBuilder(12).PackByte((byte)ForwordIndex).PackInt16((short)Server.SpawnX).PackInt16((short)Server.SpawnY).PackInt32(0).PackByte(1));
                                if (Server.SpawnX != -1 && Server.SpawnY != -1)
                                {
                                    var b = new RawDataBuilder(65).PackByte(new BitsByte() { value = 0 }).PackInt16((short)Index).PackSingle((float)Server.SpawnX * 16).PackSingle((float)Server.SpawnY * 16).PackByte(1).GetByteData();
                                    SendDataToClient(b);
                                    SendDataToForword(b);
                                }
                                Connected = true;
                            }
                            break;
                        case 15:
                            using (var reader = new BinaryReader(new MemoryStream(Buffer)))
                            {
                                reader.BaseStream.Position = 4L;
                                MSCMain.Instance.Server.OnRecieveCustomData(Index, (Utils.CustomPacket)Buffer[3], reader);
                            }
                            break;
                    }
                    Netplay.Clients[Index].Socket.AsyncSend(Buffer, 0, size, delegate { });
                }
                TShock.Log.ConsoleInfo($"<MultiSCore> 监听{Player?.Name}结束");
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(ex.Message);
            }
            
        }

        public void Dispose()
        {
            Reset();
            MSCMain.Instance.ForwordPlayers[Index] = null;
        }
    }
}
