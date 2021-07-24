using ClientApi.Networking;
using Rests;
using Terraria;
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
    }
}
