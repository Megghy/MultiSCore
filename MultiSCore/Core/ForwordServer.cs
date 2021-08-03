using ClientApi.Networking;
using Rests;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace MultiSCore.Core
{
    public class ForwordServer
    {
        public ForwordServer(string name, string key)
        {
            Name = name;
            Key = key;
        }
        public static string Name { get; set; }
        public static string Key { get; set; }
        public static void OnPlayerFinishSwitch(MSCHooks.PlayerFinishSwitchEventArgs args)
        {
            Main.npc.ForEach(n => NetMessage.SendData(23, args.Index, -1, null, n.whoAmI));
        }
        public static void OnGreetPlayer(GreetPlayerEventArgs args)
        {
            TShock.Players.Where(p => p != null).ForEach(p => TShock.Players.ForEach(_p => p.SendData(PacketTypes.PlayerActive, null, p.Index, (MSCPlugin.Instance.ForwordPlayers[p.Index] == null).GetHashCode())));
            if (MSCPlugin.Instance.ForwordInfo[args.Who] is { } && !MSCHooks.OnPlayerFinishSwitch(args.Who, out var finishJoinArgs))
                OnPlayerFinishSwitch(finishJoinArgs);
            TShock.Players[args.Who].IgnoreSSCPackets = false;
        }
    }
}
