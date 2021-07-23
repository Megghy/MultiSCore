using Microsoft.Xna.Framework;
using Rests;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent.NetModules;
using Terraria.ID;
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
        public string Password;
        public string Key => Server?.Key ?? MSCPlugin.Key;

        internal bool IsVanillaServer = false;
        internal bool ShouldStop = false;
        internal bool Back = false;

        internal PlayerData DataBackup;
        internal int PlayerDifficulty;
        public void Reset()
        {
            try
            {
                ShouldStop = true;
                ForwordIndex = -1;
                Server = null;
                if (Connection != null)
                {
                    Connection?.Client.Shutdown(SocketShutdown.Both);
                    Connection?.Client.Close();
                    Connection?.Close();
                    Connection = null;
                }
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

                            Server = server;

                            MSCPlugin.Instance.ForwordPlayers[Index]?.Dispose();
                            MSCPlugin.Instance.ForwordPlayers[Index] = this;
                            Task.Run(StartReceiveData);

                            if (server.Key.StartsWith("Terraria") && int.TryParse(server.Key.Remove(0, 8), out _) && server.Key.Length == 11)
                                IsVanillaServer = true;

                            NetMessage.SendData(14, -1, Player.Index, null, Index, false.GetHashCode()); //隐藏原服务器玩家
                            Player.SetData("MultiSCore_LastPosition", new Point(Player.TileX, Player.TileY));
                            Player.SetData("MultiSCore_SpawnPosition", new Point(Player.TPlayer.SpawnX, Player.TPlayer.SpawnY)); //有时候出生点位置会失效
                            DataBackup = new PlayerData(null);
                            DataBackup.CopyCharacter(Player);
                            PlayerDifficulty = Player.TPlayer.difficulty;

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
                    if (Player.ContainsData("MultiSCore_Switching"))
                        Player.RemoveData("MultiSCore_Switching");

                    Player.SendRawData(new RawDataBuilder(3).PackByte((byte)Index).PackByte((byte)true.GetHashCode()).GetByteData()); //修改玩家slot

                    TShock.Players.Where(p => p != null && p.Index != Index).ForEach(p =>
                    {
                        NetMessage.SendData(14, Index, -1, null, p.Index, true.GetHashCode());//显示原服务器玩家 
                        NetMessage.SendData(4, Index, -1, null, p.Index); //还原其他玩家信息
                    });
                    Main.npc.ForEach(n => NetMessage.SendData(23, Index, -1, null, n.whoAmI)); //还原npc数据
                    if (Server.RememberHostInventory)
                    {
                        var sscStatue = Main.ServerSideCharacter;
                        Main.ServerSideCharacter = true;
                        NetMessage.SendData(7, Index);   //不这样的话没法把背包发回去
                        Player.TPlayer.difficulty = (byte)PlayerDifficulty;
                        DataBackup.RestoreCharacter(Player);
                        Player.IgnoreSSCPackets = false;
                        Main.ServerSideCharacter = sscStatue;
                    }
                    if (MSCPlugin.Instance.ServerConfig.RememberLastPoint && Player != null)
                    {
                        var p = Player.GetData<Point>("MultiSCore_LastPosition");
                        Player.Teleport(p.X * 16, p.Y * 16);
                        Player.RemoveData("MultiSCore_LastPosition");
                    }
                    else
                        Player?.Spawn(PlayerSpawnContext.RecallFromItem);
                    NetMessage.SendData(7, Index);  //重置ssc状态/发送世界信息
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
            SocketAsyncEventArgs args = new();
            args.SetBuffer(buffer, start == -1 ? 0 : start, size == -1 ? buffer.Length : size);
            Connection?.Client?.SendAsync(args);
        }
        public void SendDataToClient(byte[] buffer)
        {
            Player?.SendRawData(buffer);
        }
        public void SendDataToForword(RawDataBuilder data) => SendDataToForword(data.GetByteData());
        void StartReceiveData()
        {
            try
            {
                var buffer = new byte[51200];
                while (!ShouldStop && Connection is { Connected: true } && Player is { ConnectionAlive: true })
                {
                    CheckBuffer(Connection.Client.Receive(buffer), buffer);
                }
                buffer = null;
            }
            catch(SocketException)
            {
                if (Connected)
                {
                    BackToHost();
                    Player?.SendInfoMsg("由于不可预料的错误, 你已被传送回主服务器");
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"<MultiSCore> Host recieve packet error: {ex}");
            }
        }
        void CheckBuffer(int size, byte[] buffer)
        {
            try
            {
                if (size == 0) return;
                var length = buffer[0];
                if (size > length && buffer[2] != 10)
                {
                    var position = 0;
                    while (position < size)
                    {
                        var tempLength = buffer[position];
                        if (buffer[position + 2] == 10 || tempLength == 0 || buffer[position + 1] != 0)
                            break;  //俺实在拿10号包没办法
                        if (!ProcessData(buffer, position, size))
                            Array.Clear(buffer, position, tempLength);
                        position += tempLength;
                    }
                }
                else if (!ProcessData(buffer, 0, size))
                    return;
                Netplay.Clients[Index].Socket.AsyncSend(buffer, 0, size, Netplay.Clients[Index].ServerWriteCallBack);
            }
            catch { }
        }
        /// <summary>
        /// 返回是否发送给客户端
        /// </summary>
        /// <param name="tempBuffer"></param>
        /// <param name="startIndex"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        bool ProcessData(byte[] tempBuffer, int startIndex, int size)
        {
            try
            {
                switch (tempBuffer[startIndex + 2])
                {
                    case 2:
                        using (var reader = new BinaryReader(new MemoryStream(tempBuffer, startIndex, tempBuffer[startIndex])))
                        {
                            reader.BaseStream.Position = 3L;
                            var reason = NetworkText.Deserialize(reader);
                            Player?.SendInfoMsg($"你已被移出服务器 {Server.Name}: {reason}");
                            TShock.Log.ConsoleInfo($"<MultiSCore> {Player?.Name} 被移出服务器 {Server.Name}: {reason}");
                            BackToHost();
                            return false;
                        }
                    case 3:
                        ForwordIndex = tempBuffer[startIndex + 3];
                        return true;
                    case 7:
                        if (!Connected)
                        {
                            SendDataToForword(new RawDataBuilder(8).PackInt32(Server.SpawnX).PackInt32(Server.SpawnY));
                            SendDataToForword(new RawDataBuilder(12).PackByte((byte)ForwordIndex).PackInt16((short)Server.SpawnX).PackInt16((short)Server.SpawnY).PackInt32(0).PackByte(1));
                        }
                        return true;
                    case 15:
                        using (var reader = new BinaryReader(new MemoryStream(tempBuffer, startIndex, tempBuffer[startIndex])))
                        {
                            var type = (Utils.CustomPacket)tempBuffer[startIndex + 3];
                            reader.BaseStream.Position = 4L;
                            if (!MSCHooks.OnRecieveCustomData(Index, type, reader, out var recieveArgs))
                                MSCPlugin.Instance.Server.OnRecieveCustomData(recieveArgs);
                        }
                        return false;
                    case 37:
                        if (string.IsNullOrEmpty(Password))
                            Player.SendErrorMsg($"服务器 {Server.Name} 需要密码, 请输入 /msc password([c/B3CE95:p]) <[c/B3CE95:密码]>");
                        return false;
                    default: return true;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"<MultiSCore> Host process packet error: {ex}");
                return false;
            }
        }
        public void Dispose()
        {
            Reset();
            MSCPlugin.Instance.ForwordPlayers[Index] = null;
        }
    }
}
