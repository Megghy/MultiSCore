using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace MultiSCore
{
    public class MSCHooks
    {
        public class PlayerJoinEventArgs
        {
            public int Index { get; internal set; } 
            public string Key { get; internal set; }
            public bool Handled { get; set; } = false;
        }
        public delegate void PlayerJoinEvent(PlayerJoinEventArgs args);
        public static event PlayerJoinEvent PlayerJoin;
        internal static bool OnPlayerJoin(PlayerJoinEventArgs args)
        {
            PlayerJoin?.Invoke(args);
            return args.Handled;
        }
    }
}
