using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Linq;
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
        public int Index;
        public int ForwordIndex;
        public TSPlayer Player { get { return TShock.Players[Index]; } set { } }
        public bool Connected = false;
        public Config.ForwordServer Server;
        public TcpClient Connection;
        public byte[] Buffer;
        public string Password;
        public string Key => Server?.Key ?? MSCPlugin.Key;
        internal bool ShouldStop = false;
        internal bool Back = false;
        public void Reset()
        {
            try
            {
                ShouldStop = true;
                ForwordIndex = -1;
                Server = null;
                Connection?.Client.Shutdown(SocketShutdown.Both);
                Connection?.Client.Close();
                Connection?.Close();
                Connection = null;
                Buffer = null;
                Connected = false;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"<MultiSCore> Reset MSCPlayer error: {ex}");
            }
        }
        public void SendMessage(string text, Color color = default)
        {
            color = color == default ? Color.White : color;
            Terraria.Net.NetManager.Instance.SendToClient(NetTextModule.SerializeServerMessage(NetworkText.FromLiteral(text), color, (byte)Index), Index);
        }
        public void SwitchServer(Config.ForwordServer server)
        {
            Task.Run(() =>
            {
                try
                {
                    if (!Player.IsForwordPlayer())
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

                                    MSCPlugin.Instance.ForwordPlayers[Index]?.Dispose();
                                    MSCPlugin.Instance.ForwordPlayers[Index] = this;
                                    Task.Run(RecieveLoop);

                                    NetMessage.SendData(14, -1, Player.Index, null, Index, false.GetHashCode()); //隐藏原服务器玩家
                                    Player.SetData("MultiSCore_LastPosition", new Point(Player.TileX, Player.TileY));
                                    Player.SetData("MultiSCore_SpawnPosition", new Point(Player.TPlayer.SpawnX, Player.TPlayer.SpawnY)); //有时候出生点位置会失效

                                    SendDataToForword(new RawDataBuilder(1).PackString(Key).PackString(server.Name).PackString(Player.IP).PackString(MSCPlugin.Instance.Version.ToString()));  //发起连接请求
                                }
                                catch
                                {
                                    TShock.Log.ConsoleError($"<MultiSCore> Can not connect to {server.IP}:{server.Port}");
                                    Player?.SendErrorMsg($"无法连接至服务器 {server.Name}");
                                    Reset();
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
                Player.RemoveData("MultiSCore_Switching");
            });
        }
        public void BackToHost()
        {
            Task.Run(() =>
            {
                try
                {
                    if (Player == null)
                        return;
                    Back = true;
                    ShouldStop = true;
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
                    var sscStatue = Main.ServerSideCharacter;
                    Main.ServerSideCharacter = true;
                    Player.SendRawData(new RawDataBuilder(3).PackByte((byte)Index).PackByte((byte)true.GetHashCode()).GetByteData()); //修改玩家slot
                    NetMessage.SendData(7, Index);   //不这样的话没法把背包发回去
                    TShock.Players.Where(p => p != null && p.Index != Index).ForEach(p =>
                    {
                        NetMessage.SendData(14, Index, -1, null, p.Index, true.GetHashCode());//显示原服务器玩家 
                        NetMessage.SendData(4, Index, -1, null, p.Index); //还原其他玩家信息
                    });
                    Main.npc.ForEach(n => NetMessage.SendData(23, Index, -1, null, n.whoAmI)); //还原npc数据
                    Player?.SendServerCharacter();
                    if (MSCPlugin.Instance.ServerConfig.RememberLastPoint && Player != null)
                    {
                        var p = Player.GetData<Point>("MultiSCore_LastPosition");
                        Player.Teleport(p.X * 16, p.Y * 16);
                        Player.RemoveData("MultiSCore_LastPosition");
                    }
                    else
                        Player?.Spawn(PlayerSpawnContext.SpawningIntoWorld);
                    Main.ServerSideCharacter = sscStatue;
                    NetMessage.SendData(7, Index);  //重置ssc状态
                    //Dispose(); 在收到玩家发来的6号包时再卸载
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"<MultiSCore> Back to host error: {ex}");
                }
            });
        }
        public RawDataBuilder GetCustomRawData(Utils.CustomPacket type) => Utils.GetCustomRawData(Index, type);
        public void SendDataToForword(byte[] buffer, int start = -1, int size = -1)
        {
            if (!Connected && buffer[2] > 12 && buffer[2] != 93 && buffer[2] != 16 && buffer[2] != 42 && buffer[2] != 50 && buffer[2] != 38 && buffer[2] != 68 && buffer[2] != 15) return;
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
                int size = 0;
            start:
                while (Connection?.Client is { Connected: true } && !ShouldStop)
                {
                    try { size = Connection.Client.Receive(Buffer); } catch { return; }
                    if (Buffer is null) 
                        return;
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
                            ForwordIndex = Buffer[3];
                            if (Connected)
                                goto start;
                            else
                                break;
                        case 7:
                            if (!Connected)
                            {
                                SendDataToForword(new RawDataBuilder(8).PackInt32(Server.SpawnX).PackInt32(Server.SpawnY));
                                SendDataToForword(new RawDataBuilder(12).PackByte((byte)ForwordIndex).PackInt16((short)Server.SpawnX).PackInt16((short)Server.SpawnY).PackInt32(0).PackByte(1));
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
                            break;
                        case 37:
                            if (string.IsNullOrEmpty(Password))
                                Player.SendErrorMsg($"服务器 {Server.Name} 需要密码, 请输入 /msc password([c/B3CE95:p]) <[c/B3CE95:密码]>");
                            goto start;
                        case 129:
                            if (!MSCHooks.OnPlayerFinishSwitch(Index, out var finishJoinArgs))
                                MSCPlugin.Instance.Server.OnPlayerFinishSwitch(finishJoinArgs);
                            break;
                    }
                    Netplay.Clients[Index].Socket.AsyncSend(Buffer, 0, size, delegate { });
                }
            }
            catch (Exception ex)
            {
                if (MSCPlugin.Instance.ForwordPlayers[Index] != null)
                {
                    BackToHost();
                    Player?.SendInfoMsg("由于不可预料的错误, 你已被传送回主服务器");
                }
                if (ex != null) TShock.Log.ConsoleError($"<MultiSCore> Host forwording loop error: {ex}");
            }
        }

        public void Dispose()
        {
            Reset();
            MSCPlugin.Instance.ForwordPlayers[Index] = null;
        }
    }
}
