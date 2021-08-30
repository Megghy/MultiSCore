using Microsoft.Xna.Framework;
using System;
using System.Linq;
using System.Net;
using System.Reflection;
using Terraria;
using TShockAPI;
using TShockAPI.Configuration;

namespace MultiSCore
{
    public static class Utils
    {
        public enum CustomPacket
        {
            Command
        }
        public class HostInfo
        {
            public string TRVersion { get; set; }
            public Version Version { get; set; }
            public string Key { get; set; }
        }
        public static string GetText(string key)
        {
            try { return MSCPlugin.Instance.ServerConfig.Language.Value<string>(key); }
            catch { return "null"; }
        }
        public static bool TryParseAddress(string address, out string ip)
        {
            ip = "";
            try
            {
                if (IPAddress.TryParse(address, out _))
                {
                    ip = address;
                    return true;
                }
                else
                {
                    IPHostEntry hostinfo = Dns.GetHostEntry(address);
                    if (hostinfo.AddressList.FirstOrDefault() is { } _ip)
                    {
                        ip = _ip.ToString();
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
        public static MSCPlayer GetMSCPlayer(this TSPlayer plr) => MSCPlugin.Instance.ForwordPlayers[plr.Index];
        public static RawDataBuilder GetCustomRawData(int index, CustomPacket type) => new(type, GetKey(index));
        public static RawDataBuilder GetCustomRawData(this TSPlayer plr, CustomPacket type) => GetCustomRawData(plr.Index, type);
        internal static readonly FieldInfo CacheIP = typeof(TSPlayer).GetField("CacheIP", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static T GetConfigValue<T>(string name)
        {
            try
            {
                if (TShock.VersionNum < new Version(4, 5, 0, 0))
                    return GetConfigValue_440<T>(name);
                else
                    return GetConfigValue_450<T>(name);
            }
            catch (Exception ex)
            { TShock.Log.ConsoleError($"<MultiSCore> Get config value error: {ex.Message}"); return default; }
        }
        static T GetConfigValue_450<T>(string name) => (T)typeof(TShockSettings).GetField(name).GetValue(TShock.Config.Settings);
        static T GetConfigValue_440<T>(string name) => (T)AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "TShockAPI").GetType("TShockAPI.ConfigFile").GetField(name).GetValue(typeof(TShock).GetProperty("Config").GetValue(null));
        public static string GetKey(int index)
        {
            if (MSCPlugin.Instance.ForwordInfo[index] is { } info)
                return info.Key;
            else if (MSCPlugin.Instance.ForwordPlayers[index] is { } mscp)
                return mscp.Key;
            else
                return MSCPlugin.Key;
        }
        public static HostInfo GetForwordInfo(this TSPlayer plr) => MSCPlugin.Instance.ForwordInfo[plr.Index];
        public static bool IsForwordPlayer(this TSPlayer plr) => plr.GetForwordInfo() is { };
        public static void SendMessageToHostPlayer(string text)
        {
            TShock.Players.Where(p => p != null && MSCPlugin.Instance.ForwordPlayers[p.Index] == null).ForEach(p => p.SendMessage(text, Color.White));
            TShock.Log.Info(text);
            Console.WriteLine(text);
        }
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
