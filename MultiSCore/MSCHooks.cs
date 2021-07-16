using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace MultiSCore
{
    public class MSCHooks
    {
        public struct PlayerJoinEventArgs
        {
            public PlayerJoinEventArgs(int index, string key, string ip)
            {
                Handled = false;
                Index = index;
                Key = key;
                IP = ip;
            }
            public int Index { get; internal set; } 
            public string Key { get; internal set; }
            public string IP { get; internal set; }
            public bool Handled { get; set; }
        }
        public struct RecieveCustomDataEventArgs
        {
            public RecieveCustomDataEventArgs(int index, Utils.CustomPacket type, BinaryReader reader)
            {
                Handled = false;
                Reader = reader;
                Type = type;
                Index = index;
            }
            public int Index { get; internal set; }
            public TSPlayer Player { get { return TShock.Players[Index]; } internal set { } }
            public Utils.CustomPacket Type { get; internal set; }
            public BinaryReader Reader { get; internal set; }
            public bool Handled { get; set; }
        }
        public delegate void PlayerJoinEvent(PlayerJoinEventArgs args);
        public static event PlayerJoinEvent PlayerJoin;
        public delegate void RecieveCustomDataEvent(RecieveCustomDataEventArgs args);
        public static event RecieveCustomDataEvent RecieveCustomData;
        internal static bool OnPlayerJoin(PlayerJoinEventArgs args)
        {
            PlayerJoin?.Invoke(args);
            return args.Handled;
        }
        
        internal static bool OnRecieveCustomData(RecieveCustomDataEventArgs args)
        {
            args.Reader.BaseStream.Position = 4L;
            RecieveCustomData?.Invoke(args);
            args.Reader.BaseStream.Position = 4L;
            return args.Handled;
        }
    }
}
