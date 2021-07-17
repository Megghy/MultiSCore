using MaxMind;
using Newtonsoft.Json;
using OTAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Terraria;
using Terraria.Localization;
using Terraria.Net.Sockets;
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
        public void OnConnectRequest(MSCHooks.PlayerJoinEventArgs args)
        {
            var index = args.Index;

            if (args.Key != Key)
            {
                TShock.Log.ConsoleInfo($"<MultiSCore> 无效的秘钥: {args.Key}");
                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral("无效的秘钥"));
            }
            else if (args.Name != Name)
            {
                TShock.Log.ConsoleInfo($"<MultiSCore> 不匹配的服务器名: {args.Key}");
                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral("不匹配的服务器名"));
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
                if ((Netplay.Clients[index].State < 10 && packetid > 12 && packetid != 93 && packetid != 16 && packetid != 42 && packetid != 50 && packetid != 38 && packetid != 68 && packetid != 15) || (Netplay.Clients[index].State == 0 && packetid != 1 && packetid != 15))
                {
                    TShock.Log.ConsoleInfo($"当前状态下操作无效 - {(PacketTypes)packetid}, State: {Netplay.Clients[index].State}");
                    return HookResult.Cancel;
                }

                var reader = buffer.reader;
                reader.BaseStream.Position = start + 1;
                switch (packetid)
                {
                    case 15:
                        var type = (Utils.CustomPacket)reader.ReadByte();
                        if (!MSCHooks.OnRecieveCustomData(index, type, reader, out var recieveArgs))
                            OnRecieveCustomData(recieveArgs);
                        return HookResult.Cancel;
                    case 1:
                        var key = reader.ReadString();
                        if (key.StartsWith("Terraria") && !MSCPlugin.Instance.ServerConfig.AllowDirectJoin)
                        {
                            NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral("此服务器不允许直接连接"));
                        }
                        else
                        {
                            if (!MSCHooks.OnPlayerJoin(index, reader.ReadString(), key, reader.ReadString(), out var joinArgs)) OnConnectRequest(joinArgs);
                        }
                        return HookResult.Cancel;
                    default:
                        return MSCPlugin.Instance.OldGetDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
                }
            }
            catch (Exception ex) { TShock.Log.ConsoleError($"<MultiSCore> Forword recieve packet error: {ex.Message}"); return HookResult.Cancel; }
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
                        if (Key == key) plr?.SetData("MultiSCore_ServerList", JsonConvert.DeserializeObject<List<Config.ForwordServer>>(reader.ReadString()));
                        break;
                    case Utils.CustomPacket.Command:
                        var cmd = reader.ReadString();
                        if (cmd == "MultiSCore_Spawn")
                            plr?.Spawn(PlayerSpawnContext.SpawningIntoWorld);
                        else
                            Commands.HandleCommand(plr, Commands.Specifier + cmd);
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
                if (args.CommandName == "msc" || args.Player.GetData<List<Config.ForwordServer>>("MultiSCore_ServerList") is { Count: > 0 } servers && servers.FirstOrDefault(s => s.Name == Name) is { } server && server.GlobalCommand.Contains(args.CommandName))
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
        public void OnPlayerFinishSwitch(MSCHooks.PlayerFinishSwitchEventArgs args)
        {
            Main.npc.ForEach(n => NetMessage.SendData(23, args.Index, -1, null, n.whoAmI));
        }
        public HookResult OnSendData(ref int remoteClient, ref byte[] data, ref int offset, ref int size, ref SocketSendCallback callback, ref object state)
        {
            if (data[2] == 129 && size < 5 && !MSCHooks.OnPlayerFinishSwitch(remoteClient, out var finishJoinArgs)) MSCPlugin.Instance.Server.OnPlayerFinishSwitch(finishJoinArgs);
            return HookResult.Continue;  //连接到的服务器不需要对senddata作出更改 顺着socket发回去就行了
        }
    }
}
