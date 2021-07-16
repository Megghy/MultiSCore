using MaxMind;
using Newtonsoft.Json;
using OTAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Terraria;
using Terraria.Localization;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace MultiSCore.Core
{
    public class ForwordServer : IServer
    {
        public ForwordServer(string name, string key)
        {
            Name = name;
            Key = key;
        }
        public string Name { get; set; }
        public string Key { get; set; }
        public List<Config.ForwordServer> TempServerList { get; set; } = new();
        public void OnConnectRequest(MSCHooks.PlayerJoinEventArgs args)
        {
            var index = args.Index;
            
            if (args.Key != Key)
            {
                TShock.Log.ConsoleInfo($"<MultiSCore> 无效的秘钥: {args.Key}");
                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral("无效的秘钥"));
            }
            else
            {
                if (Netplay.IsBanned(Netplay.Clients[index].Socket.GetRemoteAddress()))
                    NetMessage.TrySendData(2, index, -1, Lang.mp[3].ToNetworkText());
                else
                {
                    if (TShock.ShuttingDown)
                        NetMessage.SendData(2, index, -1, NetworkText.FromLiteral("服务器正在关闭"));
                    else
                    {
                        TSPlayer tsplayer = new(index);
                        Utils.CacheIP?.SetValue(tsplayer, args.IP);
                        if (TShock.Utils.GetActivePlayerCount() + 1 > TShock.Config.Settings.MaxSlots + TShock.Config.Settings.ReservedSlots)
                            tsplayer.Disconnect(TShock.Config.Settings.ServerFullNoReservedReason);
                        else if (!FileTools.OnWhitelist(tsplayer.IP))
                            tsplayer.Disconnect(TShock.Config.Settings.WhitelistKickReason);
                        else if (TShock.Geo != null)
                        {
                            string text = TShock.Geo.TryGetCountryCode(IPAddress.Parse(tsplayer.IP));
                            tsplayer.Country = text == null ? "N/A" : GeoIPCountry.GetCountryNameByCode(text);
                            if (text == "A1" && TShock.Config.Settings.KickProxyUsers)
                            {
                                tsplayer.Disconnect("不允许代理连接.");
                                return;
                            }
                        }
                        else
                            TShock.Players[index] = tsplayer;
                    }
                    Netplay.Clients[index].State = 1;
                    NetMessage.TrySendData(3, index);
                }
            }
        }
        public void OnPlayerLeave(LeaveEventArgs args)
        {
            //非主服务器玩家退出不用管
        }
        public HookResult OnReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start, ref int length)
        {
            try
            {
                var index = buffer.whoAmI;
                if ((Netplay.Clients[index].State < 10 && packetid > 12 && packetid != 93 && packetid != 16 && packetid != 42 && packetid != 50 && packetid != 38 && packetid != 68 && packetid != 15 && packetid != 8 && packetid != 12) || (Netplay.Clients[index].State == 0 && packetid != 1 && packetid != 15))
                {
                    TShock.Log.ConsoleInfo($"无效的操作 - {(PacketTypes)packetid}, State - {Netplay.Clients[index].State}");
                    return HookResult.Cancel;
                }
                using (var reader = new BinaryReader(new MemoryStream(buffer.readBuffer, index, length + 2)))
                {
                    switch (packetid)
                    {
                        case 15:
                            var type = (Utils.CustomPacket)buffer.readBuffer[3];
                            var args = new MSCHooks.RecieveCustomDataEventArgs(index, type, reader);
                            if (!MSCHooks.OnRecieveCustomData(args))
                                OnRecieveCustomData(args);
                            return HookResult.Cancel;
                        case 1:
                            reader.BaseStream.Position = 3L;
                            var key = reader.ReadString();
                            if (key.StartsWith("Terraria"))
                            {
                                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral("此服务器不允许直接连接"));
                            }
                            else
                            {
                                var joinArgs = new MSCHooks.PlayerJoinEventArgs(index, key, reader.ReadString());
                                if (!MSCHooks.OnPlayerJoin(joinArgs)) OnConnectRequest(joinArgs);
                            }
                            return HookResult.Cancel;
                        default:
                            return MSCMain.Instance.OldGetDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
                    }
                }
            }
            catch (Exception ex) { TShock.Log.ConsoleError($"<MultiSCore> Forword recieve error: {ex.Message}"); return HookResult.Cancel; }
        }
        public void OnRecieveCustomData(MSCHooks.RecieveCustomDataEventArgs args)
        {
            try
            {
                var plr = args.Player;
                var reader = args.Reader;
                switch (args.Type)
                {
                    case Utils.CustomPacket.ServerList:
                        var key = reader.ReadString();
                        if (Key == key) TempServerList = JsonConvert.DeserializeObject<List<Config.ForwordServer>>(reader.ReadString());
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
            try
            {
                if (args.CommandName == "msc" || (TempServerList?.FirstOrDefault(s => s.Name == Name) is { } server && server.GlobalCommand.Contains(args.CommandName)))
                {
                    args.Player.SendRawData(new RawDataBuilder(Utils.CustomPacket.Command).PackString(args.CommandText).GetByteData());
                    args.Handled = true;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"<MultiSCore> An error occurred when process player command: {ex}");
            }
        }
        public void OnSendData(SendBytesEventArgs args)
        {
            //连接到的服务器不需要对senddata作出更改 顺着socket发回去就行了
        }
    }
}
