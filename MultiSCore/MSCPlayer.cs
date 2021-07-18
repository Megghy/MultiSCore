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
        public string Password { get; set; }
        public string Key => Server?.Key ?? MSCPlugin.Key;
        internal bool ShouldStop = false;
        public void Reset()
        {
            ShouldStop = true;
            ForwordIndex = -1;
            Server = null;
            if((bool)(Connection?.Client.Connected)) Connection?.Client?.Shutdown(SocketShutdown.Both);
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
                if (MSCPlugin.Instance.IsHost)
                {
                    if (server.Name != MSCPlugin.Instance.Server.Name)
                    {
                        if (Utils.TryParseAddress(server.IP, out var ip))
                        {
                            if (Connection is { }) Connection.Close();
                            try
                            {
                                Connection = new();
                                Connection.Connect(server.IP, server.Port);
                                
                                Buffer = new byte[10240];
                                Server = server;

                                NetMessage.SendData(14, -1, Player.Index, null, Index, false.GetHashCode()); //隐藏原服务器玩家

                                SendDataToForword(new RawDataBuilder(1).PackString(Key).PackString(server.Name).PackString(Player.IP).PackString(MSCPlugin.Instance.Version.ToString()));  //发起连接请求
                                
                                Task.Run(RecieveLoop);
                            }
                            catch
                            {
                                TShock.Log.ConsoleError($"<MultiSCore> Can not connect to {server.IP}:{server.Port}");
                                Player?.SendErrorMsg($"无法连接至服务器 {server.Name}");
                                Dispose();
                            }

                        }
                        else Player.SendErrorMsg($"无效的服务器地址");
                    }
                    else
                        Player.SendErrorMsg($"你已在服务器 {server.Name} 中");
                }
                else
                    Player.SendErrorMsg($"禁止在 Forword Server 中直接调用 SwitchServer 函数.");
            }
            catch (Exception ex)
            {
                NetMessage.SendData(14, -1, -1, null, Index); //显示原服务器玩家 
                TShock.Log.ConsoleError($"<MultiSCore> An error occurred when switching server: {ex}");
            }
        }
        public void BackToHost()
        {
            if (MSCPlugin.Instance.IsHost)
            {
                Dispose();
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
                NetMessage.SendData(14, -1, Index, null, Index, true.GetHashCode()); //显示原服务器玩家 
                NetMessage.SendData(7, Index);
                Main.npc.ForEach(n => NetMessage.SendData(23, Index, -1, null, n.whoAmI));
                Player?.SendServerCharacter();
                if (MSCPlugin.Instance.ServerConfig.RememberLastPoint) Player?.Teleport(Player.X, Player.Y);
                else Player?.Spawn(PlayerSpawnContext.SpawningIntoWorld);
            }
        }
        public void SendDataToForword(byte[] buffer, int start = -1, int size = -1)
        {
            if (!MSCPlugin.Instance.IsHost || (!Connected && buffer[2] > 12 && buffer[2] != 93 && buffer[2] != 16 && buffer[2] != 42 && buffer[2] != 50 && buffer[2] != 38 && buffer[2] != 68 && buffer[2] != 15)) return;
            SocketAsyncEventArgs args = new();
            args.SetBuffer(buffer, start == -1 ? 0 : start, size == -1 ? buffer.Length : size);
            Connection?.Client?.SendAsync(args);
        }
        public void SendDataToClient(byte[] buffer)
        {
            Player?.SendRawData(buffer);
        }
        public void SendDataToForword(RawDataBuilder data) => SendDataToForword(data.GetByteData());
        private void RecieveLoop()
        {
            try
            {
            start:
                while (Connection?.Client is { Connected: true } && Server is { } && !ShouldStop)
                {
                    int size = Connection.Client.Receive(Buffer);
                    switch (Buffer[2])
                    {
                        case 2:
                            try
                            {
                                using (var reader = new BinaryReader(new MemoryStream(Buffer)))
                                {
                                    reader.BaseStream.Position = 3L;
                                    var reason = NetworkText.Deserialize(reader);
                                    Player?.SendInfoMsg($"你已被移出服务器 {Server.Name}: {reason}");
                                    TShock.Log.ConsoleInfo($"<MultiSCore> {Player?.Name} 被移出服务器 {Server.Name}: {reason}");
                                    BackToHost();
                                    return;
                                }
                            }
                            catch { break; }
                        case 3:
                            ForwordIndex = Buffer[2];
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
                                var type = (Utils.CustomPacket)Buffer[3];
                                reader.BaseStream.Position = 4L;
                                if (!MSCHooks.OnRecieveCustomData(Index, type, reader, out var recieveArgs))
                                    MSCPlugin.Instance.Server.OnRecieveCustomData(recieveArgs);
                            }
                            goto start;
                        case 37:
                            if (string.IsNullOrEmpty(Password))
                            {
                                Player.SendErrorMsg($"服务器 {Server.Name} 需要密码, 请输入 /msc password([c/B3CE95:p]) <[c/B3CE95:密码]>");
                                BackToHost();
                                break;
                            }
                            goto start;
                    }
                    if (Buffer != null && size != 0 && Netplay.Clients[Index].Socket.IsConnected()) Netplay.Clients[Index].Socket?.AsyncSend(Buffer, 0, size, delegate { });
                }
            }
            catch (Exception ex)
            {
                BackToHost();
                Player?.SendInfoMsg("由于不可预料的错误, 你已被传送回主服务器");
                TShock.Log.ConsoleError($"<MultiSCore> Host forwording loop error: {ex}");
            }

        }

        public void Dispose()
        {
            MSCPlugin.Instance.ForwordPlayers[Index] = null;
            Reset();
        }
    }
}
