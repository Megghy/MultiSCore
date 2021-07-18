using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MaxMind;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using OTAPI;
using Terraria;
using Terraria.Localization;
using Terraria.Net.Sockets;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace MultiSCore.Core
{
    public abstract class ServerBase
    {
        public abstract string Name { get; set; }
        public abstract string Key { get; set; }
        public abstract HookResult OnReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start, ref int length);
        public virtual void OnRecieveCustomData(MSCHooks.RecieveCustomDataEventArgs args) {
            try
            {
                var plr = args.Player;
                var reader = args.Reader;
                var mscp = plr.GetMSCPlayer();
                var key = reader.ReadString();
                if (key == Key)
                    switch (args.Type)
                    {
                        case Utils.CustomPacket.ServerList:
                            if (Key == key) plr?.SetData("MultiSCore_ServerList", JsonConvert.DeserializeObject<List<Config.ForwordServer>>(reader.ReadString()));
                            break;
                        case Utils.CustomPacket.Command:
                            Commands.HandleCommand(plr, Commands.Specifier + reader.ReadString());
                            break;
                        case Utils.CustomPacket.Chat:
                            var msg = $"[{plr.GetMSCPlayer().Server.Name}] {reader.ReadString()}: {reader.ReadString()}";
                            TShock.Players.Where(p => p != null && p.Name != plr.Name).ForEach(p => p.SendMessage(msg, Color.White));
                            TShock.Log.ConsoleInfo(msg);
                            break;
                        case Utils.CustomPacket.ConnectSuccess:
                            if (!MSCHooks.OnPlayerFinishSwitch(plr.Index, out var finishJoinArgs)) MSCPlugin.Instance.Server.OnPlayerFinishSwitch(finishJoinArgs);
                            TShock.Log.ConsoleInfo($"{plr.Name} 来自另一个加载了 {"MultiSCore".Color("B3CE95")} 插件的主服务器<host server>");
                            break;
                    }
                else
                    args.Handled = true;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"<MultiSCore> An error occurred when process customdata: {ex}");
            }
        }
        public abstract HookResult OnSendData(ref int remoteClient, ref byte[] data, ref int offset, ref int size, ref SocketSendCallback callback, ref object state);
        public virtual void OnConnectRequest(MSCHooks.PlayerJoinEventArgs args) {
            var index = args.Index;

            if (args.Key != MSCPlugin.Instance.Key)
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
        public virtual void OnPlayerLeave(LeaveEventArgs args) { }
        public virtual void OnPlayerCommand(PlayerCommandEventArgs args)
        {
            if (!args.Player.CheckCommand(args.CommandName))
            {
                args.Handled = true;
                args.Player.SendRawData(new RawDataBuilder(Utils.CustomPacket.Command).PackString(args.CommandText).GetByteData());
            }
        }
        public virtual void OnPlayerFinishSwitch(MSCHooks.PlayerFinishSwitchEventArgs args) { }
    }
}
