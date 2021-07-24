﻿using MaxMind;
using Microsoft.Xna.Framework;
using OTAPI;
using System;
using System.Linq;
using System.Net;
using Terraria;
using Terraria.Localization;
using Terraria.Net.Sockets;
using TerrariaApi.Server;
using TShockAPI;

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
                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral(Utils.GetText("Log_DontAllowOthorServerJoin")));
            else if (args.Key != MSCPlugin.Key)
            {
                TShock.Log.ConsoleInfo(string.Format(Utils.GetText("Log_UnknownKey"), args.Key));
                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral(Utils.GetText("Log_UnknownKey_SendToForword")));
            }
            else if (args.Name != Name)
            {
                TShock.Log.ConsoleInfo(string.Format(Utils.GetText("Log_MismatchedServerName"), args.Name));
                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral(Utils.GetText("Log_MismatchedServerName_SendToForword")));
            }
            else
            {
                if (args.Version != MSCPlugin.Instance.Version)
                    TShock.Log.ConsoleInfo(string.Format(Utils.GetText("Log_MismatchedServerVersion"), args.IP, MSCPlugin.Instance.Version, args.Version));
                if (Netplay.IsBanned(Netplay.Clients[index].Socket.GetRemoteAddress()))
                    NetMessage.TrySendData(2, index, -1, Lang.mp[3].ToNetworkText());
                else
                {
                    if (TShock.ShuttingDown)
                        NetMessage.SendData(2, index, -1, NetworkText.FromLiteral("Server shutting down."));
                    else
                    {
                        TSPlayer tsplayer = new(index);
                        Utils.CacheIP?.SetValue(tsplayer, args.IP);
                        if (TShock.Utils.GetActivePlayerCount() + 1 > Utils.GetConfigValue<int>("MaxSlots") + Utils.GetConfigValue<int>("ReservedSlots"))
                            tsplayer.Disconnect(Utils.GetConfigValue<string>("ServerFullNoReservedReason"));
                        else if (!FileTools.OnWhitelist(tsplayer.IP))
                            tsplayer.Disconnect(Utils.GetConfigValue<string>("WhitelistKickReason"));
                        else if (TShock.Geo != null)
                        {
                            string text = TShock.Geo.TryGetCountryCode(IPAddress.Parse(tsplayer.IP));
                            tsplayer.Country = text == null ? "N/A" : GeoIPCountry.GetCountryNameByCode(text);
                            if (text == "A1" && Utils.GetConfigValue<bool>("KickProxyUsers"))
                            {
                                tsplayer.Disconnect("Proxy connections are not allowed.");
                                return;
                            }
                        }
                        else
                            TShock.Players[index] = tsplayer;
                    }
                    Netplay.Clients[index].State = 1;
                    MSCPlugin.Instance.ForwordInfo[index] = new() { Version = args.Version, Key = args.Key };
                    NetMessage.TrySendData(3, index);
                    TShock.Log.ConsoleInfo(Utils.GetText("Log_FromAnothorMultiSCore"));
                }
            }
        }
        public HookResult OnReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start, ref int length)
        {
            if ((Netplay.Clients[buffer.whoAmI].State < 10 && packetid > 12 && packetid != 93 && packetid != 16 && packetid != 42 && packetid != 50 && packetid != 38 && packetid != 68 && packetid != 15) || (Netplay.Clients[buffer.whoAmI].State == 0 && packetid != 1 && packetid != 15))
            {
                return HookResult.Cancel;
            }
            var index = buffer.whoAmI;
            var reader = buffer.reader;
            var position = buffer.reader.BaseStream.Position;
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
                                NetMessage.TrySendData(2, index, -1, NetworkText.FromLiteral(Utils.GetText("Log_DontAllowDirectJoin")));
                            return HookResult.Cancel;
                        }
                        if (!MSCHooks.OnPlayerJoin(index, reader.ReadString(), key, reader.ReadString(), reader.ReadString(), out var joinArgs)) OnConnectRequest(joinArgs);
                        return HookResult.Cancel;
                }
                buffer.reader.BaseStream.Position = position;
                if (MSCPlugin.Instance.ForwordPlayers[buffer.whoAmI] is { } mscp)
                {
                    HostServer.OnReceiveData(buffer, ref packetid, ref readoffset, ref start, ref length);
                    return HookResult.Cancel;
                }
                else 
                    return MSCPlugin.Instance.OldGetDataHandler(buffer, ref packetid, ref readoffset, ref start, ref length);
            }
            catch (Exception ex) { TShock.Log.ConsoleError($"<MultiSCore> Recieve packet error: {ex.Message}"); return HookResult.Cancel; }

        }
        public void OnRecieveCustomData(MSCHooks.RecieveCustomDataEventArgs args)
        {
            try
            {
                var plr = args.Player;
                var reader = args.Reader;
                var key = reader.ReadString();
                if (Utils.GetKey(args.Index) == key)
                    switch (args.Type)
                    {
                        case Utils.CustomPacket.Command:
                            var cmd = reader.ReadString();
                            Commands.HandleCommand(plr, cmd.StartsWith(Commands.Specifier) || cmd.StartsWith(Commands.SilentSpecifier) ? cmd : Commands.Specifier + cmd);
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
            if (MSCPlugin.Instance.ForwordPlayers[remoteClient] is { Back: false } mscp)
                return HookResult.Cancel;
            else
                return MSCPlugin.Instance.OldSendDataHandler.Invoke(ref remoteClient, ref data, ref offset, ref size, ref callback, ref state);
        }
        public void OnPlayerLeave(LeaveEventArgs args)
        {
            MSCPlugin.Instance.ForwordPlayers[args.Who]?.Dispose();
            MSCPlugin.Instance.ForwordInfo[args.Who] = null;
        }
    }
}
