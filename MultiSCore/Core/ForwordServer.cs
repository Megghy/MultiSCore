using MaxMind;
using Newtonsoft.Json;
using OTAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.Localization;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
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
        public IPAddress IP { get; set; }
        public int Port { get; set; }
        public string Key { get; set; }
        public List<Config.ForwordServer> TempServerList { get; set; } = new();
        public void OnConnectRequest(int index, string key, string ip)
        {
            if (key != Key)
            {
                TShock.Log.ConsoleInfo($"<MultiSCore> 无效的秘钥: {key}");
                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral("无效的秘钥"));
                return;
            }
            if (Netplay.IsBanned(Netplay.Clients[index].Socket.GetRemoteAddress()))
            {
                NetMessage.TrySendData(2, index, -1, Lang.mp[3].ToNetworkText(), 0, 0f, 0f, 0f, 0, 0, 0);
            }
            else
            {
                if (TShock.ShuttingDown)
                {
                    NetMessage.SendData(2, index, -1, NetworkText.FromLiteral("服务器正在关闭"), 0, 0f, 0f, 0f, 0, 0, 0);
                }
                else
                {
                    TSPlayer tsplayer = new(index);
                    Utils.CacheIP?.SetValue(tsplayer, ip);
                    if (TShock.Utils.GetActivePlayerCount() + 1 > TShock.Config.Settings.MaxSlots + TShock.Config.Settings.ReservedSlots)
                    {
                        tsplayer.Disconnect(TShock.Config.Settings.ServerFullNoReservedReason);
                    }
                    else
                    {
                        if (!FileTools.OnWhitelist(tsplayer.IP))
                        {
                            tsplayer.Disconnect(TShock.Config.Settings.WhitelistKickReason);
                        }
                        else
                        {
                            if (TShock.Geo != null)
                            {
                                string text = TShock.Geo.TryGetCountryCode(IPAddress.Parse(tsplayer.IP));
                                tsplayer.Country = text == null ? "N/A" : GeoIPCountry.GetCountryNameByCode(text);
                                bool flag4 = text == "A1" && TShock.Config.Settings.KickProxyUsers;
                                if (flag4)
                                {
                                    tsplayer.Disconnect("不允许代理连接.");
                                    return;
                                }
                            }
                            TShock.Players[index] = tsplayer;
                        }
                    }
                }
                Netplay.Clients[index].State = 1;
                NetMessage.TrySendData(3, index);
            }
        }
        public void OnPlayerConnect(ConnectEventArgs args)
        {
            if (TShock.ShuttingDown)
            {
                NetMessage.SendData(2, args.Who, -1, NetworkText.FromLiteral("服务器正在关闭"), 0, 0f, 0f, 0f, 0, 0, 0);
                args.Handled = true;
            }
            else
            {
                TSPlayer tsplayer = new(args.Who);
                if (TShock.Utils.GetActivePlayerCount() + 1 > TShock.Config.Settings.MaxSlots + TShock.Config.Settings.ReservedSlots)
                {
                    tsplayer.Disconnect(TShock.Config.Settings.ServerFullNoReservedReason);
                    args.Handled = true;
                }
                else
                {
                    if (!FileTools.OnWhitelist(tsplayer.IP))
                    {
                        tsplayer.Disconnect(TShock.Config.Settings.WhitelistKickReason);
                        args.Handled = true;
                    }
                    else
                    {
                        if (TShock.Geo != null)
                        {
                            string text = TShock.Geo.TryGetCountryCode(IPAddress.Parse(tsplayer.IP));
                            tsplayer.Country = ((text == null) ? "N/A" : GeoIPCountry.GetCountryNameByCode(text));
                            bool flag4 = text == "A1" && TShock.Config.Settings.KickProxyUsers;
                            if (flag4)
                            {
                                tsplayer.Disconnect("Proxies are not allowed.");
                                args.Handled = true;
                                return;
                            }
                        }
                        TShock.Players[args.Who] = tsplayer;
                    }
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
                if ((Netplay.Clients[index].State < 10 && packetid > 12 && packetid != 93 && packetid != 16 && packetid != 42 && packetid != 50 && packetid != 38 && packetid != 68 && packetid != 15) || (Netplay.Clients[index].State == 0 && packetid != 1 && packetid != 15))
                {
                    TShock.Log.ConsoleInfo($"无效的操作 - {(PacketTypes)packetid}, State - {Netplay.Clients[index].State}");
                    return HookResult.Cancel;
                }
                using (var reader = new BinaryReader(new MemoryStream(buffer.readBuffer, index, length + 2)))
                {
                    switch (packetid)
                    {
                        case 15:
                            reader.BaseStream.Position = 4L;
                            OnRecieveCustomData(index, (Utils.CustomPacket)buffer.readBuffer[3], reader);
                            return HookResult.Cancel;
                        case 1:
                            reader.BaseStream.Position = 3L;
                            var key = reader.ReadString();
                            if (key.StartsWith("Terraria")) NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral("此服务器不允许直接连接"));
                            else OnConnectRequest(index, key, reader.ReadString());
                            return HookResult.Cancel;
                        default:
                            return MSCMain.Instance.OldGetDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
                    }
                }
            }
            catch (Exception ex) { TShock.Log.ConsoleError($"<MultiSCore> Forword Recieve Error: {ex.Message}"); return HookResult.Cancel; }
        }
        public void OnRecieveCustomData(int index, Utils.CustomPacket type, BinaryReader reader)
        {
            try {
                switch (type)
                {
                    case Utils.CustomPacket.ServerList:
                        var key = reader.ReadString();
                        var ss = reader.ReadString();
                        if(Key == key) TempServerList = JsonConvert.DeserializeObject<List<Config.ForwordServer>>(ss);
                        TShock.Log.ConsoleError(ss);
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
                TShock.Log.ConsoleError($"<MultiSCore> 处理玩家命令时发生错误: {ex}");
            }
        }
        public void OnSendData(SendBytesEventArgs args)
        {
            //连接到的服务器不需要对senddata作出更改 顺着socket发回去就行了
        }
    }
}
