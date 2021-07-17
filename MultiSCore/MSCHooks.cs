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
            public PlayerJoinEventArgs(int index,string name, string key, string ip)
            {
                Handled = false;
                Index = index;
                Name = name;
                Key = key;
                IP = ip;
            }
            public int Index { get; internal set; }
            public string Name { get; internal set; }
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
        public struct PlayerFinishSwitchEventArgs
        {
            public PlayerFinishSwitchEventArgs(int index)
            {
                Handled = false;
                Index = index;
            }
            public int Index { get; internal set; }
            public TSPlayer Player { get { return TShock.Players[Index]; } internal set { } }
            public bool Handled { get; set; }
        }
        public delegate void PlayerJoinEvent(PlayerJoinEventArgs args);
        public static event PlayerJoinEvent PlayerJoin;
        public delegate void RecieveCustomDataEvent(RecieveCustomDataEventArgs args);
        public static event RecieveCustomDataEvent RecieveCustomData;
        public delegate void PlayerFinishSwitchEvent(PlayerFinishSwitchEventArgs args);
        public static event PlayerFinishSwitchEvent PlayerFinishJoin;
        internal static bool OnPlayerJoin(int index, string name, string key, string ip, out PlayerJoinEventArgs args)
        {
            args = new(index, name, key, ip);
            PlayerJoin?.Invoke(args);
            return args.Handled;
        }

        internal static bool OnRecieveCustomData(int index, Utils.CustomPacket type, BinaryReader reader, out RecieveCustomDataEventArgs args)
        {
            var position = MSCPlugin.Instance.IsHost ? 4L : reader.BaseStream.Position;
            args = new(index, type, reader);
            RecieveCustomData?.Invoke(args);
            args.Reader.BaseStream.Position = position;
            return args.Handled;
        }
        internal static bool OnPlayerFinishSwitch(int index, out PlayerFinishSwitchEventArgs args)
        {
            args = new(index);
            PlayerFinishJoin?.Invoke(args);
            return args.Handled;
        }
    }
}
