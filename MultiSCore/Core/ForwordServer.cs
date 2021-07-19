using Newtonsoft.Json;
using OTAPI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Terraria;
using Terraria.Localization;
using Terraria.Net.Sockets;
using TShockAPI;
using TShockAPI.Hooks;

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
        public static void OnRecieveCustomData(MSCHooks.RecieveCustomDataEventArgs args)
        {
            if (!args.Handled)
                switch (args.Type)
                {
                    
                }
        }
        public static void OnPlayerFinishSwitch(MSCHooks.PlayerFinishSwitchEventArgs args)
        {
            Main.npc.ForEach(n => NetMessage.SendData(23, args.Index, -1, null, n.whoAmI));
        }
    }
}
