using Microsoft.Xna.Framework;
using System;
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
            SendPlayerIP,
            ServerList,
            Chat,
            Command
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
        internal static readonly FieldInfo CacheIP = typeof(TSPlayer).GetField("CacheIP", BindingFlags.Instance | BindingFlags.NonPublic);
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
