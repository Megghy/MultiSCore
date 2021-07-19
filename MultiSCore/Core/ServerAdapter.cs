using MaxMind;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using OTAPI;
using System;
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
    public class ServerAdapter
    {
        public ServerAdapter(string name, string key)
        {
            Name = name;
            Key = key;
        }
        public string Name { get; set; }
        public string Key { get; set; }
        public void OnConnectRequest(MSCHooks.PlayerJoinEventArgs args)
        {
            var index = args.Index;
            if (!MSCPlugin.Instance.ServerConfig.AllowOthorServerJoin)
                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral($"此服务器不允许被其他服务器连接"));
            else if (args.Key != MSCPlugin.Key)
            {
                TShock.Log.ConsoleInfo($"<MultiSCore> 无效的秘钥: {args.Key}");
                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral("无效的秘钥"));
            }
            else if (args.Name != Name)
            {
                TShock.Log.ConsoleInfo($"不匹配的服务器名: {args.Key}");
                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral("不匹配的服务器名"));
            }
            else
            {
                if (args.Version != MSCPlugin.Instance.Version)
                    TShock.Log.ConsoleInfo($"<MultiSCore> {args.IP} 来自不同版本的 MultiSCore 代理, 本机: {MSCPlugin.Instance.Version}, 对方: {args.Version}. 这可能造成某些错误");
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
                    TShock.Log.ConsoleInfo($"<MultiSCore> 此玩家来自另一个启用了 MultiSCore 插件的服务器");
                }
            }
        }
        public HookResult OnReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start, ref int length)
        {
            if ((Netplay.Clients[buffer.whoAmI].State < 10 && packetid > 12 && packetid != 93 && packetid != 16 && packetid != 42 && packetid != 50 && packetid != 38 && packetid != 68 && packetid != 15) || (Netplay.Clients[buffer.whoAmI].State == 0 && packetid != 1 && packetid != 15))
            {
                TShock.Log.ConsoleInfo($"无效的操作 - {(PacketTypes)packetid}");
                return HookResult.Cancel;
            }
            var index = buffer.whoAmI;
            var reader = buffer.reader;
            reader.BaseStream.Position = start + 1;
            try
            {
                switch (packetid)
                {
                    case 15:
                        if (!MSCHooks.OnRecieveCustomData(index, (Utils.CustomPacket)reader.ReadByte(), reader, out var recieveArgs))
                            OnRecieveCustomData(recieveArgs);
                        return HookResult.Cancel;
                    case 1:
                        var key = reader.ReadString();
                        if (key.StartsWith("Terraria"))
                        {
                            if (MSCPlugin.Instance.ServerConfig.AllowDirectJoin) 
                                return MSCPlugin.Instance.OldGetDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length); //让tr自己处理加入事件
                            else
                                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral("此服务器不允许直接连接"));
                            return HookResult.Cancel;
                        }
                        if (!MSCHooks.OnPlayerJoin(index, reader.ReadString(), key, reader.ReadString(), reader.ReadString(), out var joinArgs)) OnConnectRequest(joinArgs);
                        return HookResult.Cancel;
                    default:
                        if (MSCPlugin.Instance.ForwordPlayers[buffer.whoAmI] is { } mscp)
                        {
                            mscp.SendDataToForword(buffer.readBuffer, start - 2, length + 2);
                            return HookResult.Cancel;
                        }
                        return MSCPlugin.Instance.OldGetDataHandler(buffer, ref packetid, ref readoffset, ref start, ref length);
                }
            }
            catch (Exception ex) { TShock.Log.ConsoleError($"<MultiSCore> Host recieve packet error: {ex.Message}"); return HookResult.Cancel; }

        }
        public void OnRecieveCustomData(MSCHooks.RecieveCustomDataEventArgs args)
        {
            try
            {
                var plr = args.Player;
                var reader = args.Reader;
                var key = reader.ReadString();
                if (plr.GetKey() == key)
                    switch (args.Type)
                    {
                        case Utils.CustomPacket.ServerInfo:
                            MSCPlugin.Instance.ForwordInfo[args.Index] = JsonConvert.DeserializeObject<Config>(reader.ReadString());
                            break;
                        case Utils.CustomPacket.Command:
                            var cmd = reader.ReadString();
                            if (cmd == "MultiSCore_Spawn")
                                plr?.Spawn(PlayerSpawnContext.SpawningIntoWorld);
                            else
                                Commands.HandleCommand(plr, cmd.StartsWith(Commands.Specifier) || cmd.StartsWith(Commands.SilentSpecifier) ? cmd : Commands.Specifier + cmd);
                            break;
                        case Utils.CustomPacket.Chat:
                            var msg = $"[{plr.GetMSCPlayer().Server.Name}] {reader.ReadString()}: {reader.ReadString()}";
                            TShock.Players.Where(p => p != null && p.Name != plr.Name).ForEach(p => p.SendMessage(msg, Color.White));
                            TShock.Log.ConsoleInfo(msg);
                            break;
                        case Utils.CustomPacket.ConnectSuccess:
                            if (!MSCHooks.OnPlayerFinishSwitch(args.Index, out var finishJoinArgs))
                                OnPlayerFinishSwitch(finishJoinArgs);
                            break;
                    }
                else
                {
                    args.Handled = true;
                    TShock.Log.ConsoleInfo($"<MultiSCore> Failed to check customdata's key. Type: {args.Type}, key: {key}");
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"<MultiSCore> An error occurred when process customdata: {ex}");
            }
        }
        public HookResult OnSendData(ref int remoteClient, ref byte[] data, ref int offset, ref int size, ref SocketSendCallback callback, ref object state)
        {
            if (MSCPlugin.Instance.ForwordPlayers[remoteClient] is { } mscp)
            {
                return HookResult.Cancel;
            }
            return HookResult.Continue;
        }
        public void OnPlayerLeave(LeaveEventArgs args)
        {
            MSCPlugin.Instance.ForwordPlayers[args.Who]?.Dispose();
            MSCPlugin.Instance.ForwordInfo[args.Who] = null;
        }
        public void OnPlayerChat(PlayerChatEventArgs args)
        {
            if (args.Player.IsForwordPlayer())
                args.Player.SendRawData(args.Player.GetCustomRawData(Utils.CustomPacket.Chat).PackString(args.Player.Name).PackString(args.RawText).GetByteData());
        }
        public void OnPlayerCommand(PlayerCommandEventArgs args)
        {
            if (!args.Player.RealPlayer) return;
            if (!args.Player.CheckCommand(args.CommandName))
            {
                args.Handled = true;
                args.Player.SendRawData(args.Player.GetCustomRawData(Utils.CustomPacket.Command).PackString(args.CommandText).GetByteData());
            }
        }
        public void OnPlayerFinishSwitch(MSCHooks.PlayerFinishSwitchEventArgs args)
        {
            args.Player.RemoveData("MultiSCore_Switching");
            if (args.Player.IsForwordPlayer())
                ForwordServer.OnPlayerFinishSwitch(args);
            else HostServer.OnPlayerFinishSwitch(args);
        }
    }
}
