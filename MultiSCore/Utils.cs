using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Terraria;
using TShockAPI;

namespace MultiSCore
{
    public static class Utils
    {
        public enum CustomPacket
        {
            ConnectSuccess,
            Spawn
        }
        public class HostInfo
        {
            public Version Version { get; set; }
            public string Key { get;set; }
        }
        public static bool TryParseAddress(string address, out string ip)
        {
            ip = "";
            try
            {
                IPHostEntry hostinfo = Dns.GetHostEntry(address);
                if (hostinfo.AddressList.FirstOrDefault() is { } _ip)
                {
                    ip = _ip.ToString();
                    return true;
                }
            }
            catch { }
            return false;
        }
        public static MSCPlayer GetMSCPlayer(this TSPlayer plr) => MSCPlugin.Instance.ForwordPlayers[plr.Index];
        public static RawDataBuilder GetCustomRawData(int index, CustomPacket type) => new(type, GetKey(index));
        public static RawDataBuilder GetCustomRawData(this TSPlayer plr, CustomPacket type) => GetCustomRawData(plr.Index, type);
        internal static readonly FieldInfo CacheIP = typeof(TSPlayer).GetField("CacheIP", BindingFlags.Instance | BindingFlags.NonPublic);
        public static string GetKey(int index)
        {
            if (MSCPlugin.Instance.ForwordInfo[index] is { } info)
                return info.Key;
            else if (MSCPlugin.Instance.ForwordPlayers[index] is { } mscp)
                return mscp.Key;
            else
                return MSCPlugin.Key;
        }
        public static string GetKey(this TSPlayer plr) => GetKey(plr.Index);
        public static HostInfo GetForwordInfo(this TSPlayer plr) => MSCPlugin.Instance.ForwordInfo[plr.Index];
        public static bool IsForwordPlayer(this TSPlayer plr) => plr.GetForwordInfo() is { };
        public static bool IsForwordPlayer(int index) => MSCPlugin.Instance.ForwordInfo[index] is { };
        internal static readonly string ServerPrefix = $"<[C/A8D9D0:MultiSCore]> ";
        public static void SendSuccessMsg(this TSPlayer tsp, object text, bool playsound = true)
        {
            tsp?.SendMessage(ServerPrefix + text, new Color(120, 194, 96));
            if (playsound) NetMessage.PlayNetSound(new NetMessage.NetSoundInfo(tsp.TPlayer.position, 122, -1, 0.62f), tsp.Index);
        }

        public static void SendInfoMsg(this TSPlayer tsp, object text)
        {
            tsp?.SendMessage(ServerPrefix + text, new Color(216, 212, 82));
        }

        public static void SendErrorMsg(this TSPlayer tsp, object text)
        {
            tsp?.SendMessage(ServerPrefix + text, new Color(195, 83, 83));
        }

        public static void SendMsg(this TSPlayer tsp, object text, Color color = default)
        {
            color = color == default ? new Color(212, 239, 245) : color;
            tsp?.SendMessage(ServerPrefix + text, color);
        }
    }
}
