using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using OTAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terraria;
using Terraria.Net.Sockets;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace MultiSCore.Core
{
    public class HostServer
    {
        public static void OnPlayerLeave(LeaveEventArgs args)
        {
            if (MSCPlugin.Instance.ForwordPlayers[args.Who] is { } mscp)
            {
                mscp.Dispose();
            }
        }
        public static HookResult OnReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start, ref int length)
        {
            if (MSCPlugin.Instance.ForwordPlayers[buffer.whoAmI] is { } mscp)
            {
                mscp.SendDataToForword(buffer.readBuffer, start - 2, length + 2);
                return HookResult.Cancel;
            }
            return MSCPlugin.Instance.OldGetDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);

        }
        public static void OnRecieveCustomData(MSCHooks.RecieveCustomDataEventArgs args)
        {
            if(!args.Handled)
                switch (args.Type)
                {
                    
                }
        }
        public static void OnPlayerFinishSwitch(MSCHooks.PlayerFinishSwitchEventArgs args)
        {
            if (args.Player?.GetForwordInfo() is { } info) TShock.Log.ConsoleInfo($"<MultiSCore> 注意: {args.Player.Name} 来自另一个加载了 MultiSCore 插件的主服务器 {info}, 这可能导致某些问题");
            if (MSCPlugin.Instance.ForwordPlayers[args.Index] is { } mscp)
            {
                if (mscp.Server.SpawnX == -1 || mscp.Server.SpawnY == -1)
                    mscp.SendDataToForword(mscp.GetCustomRawData(Utils.CustomPacket.Spawn)); //如果没设置出生位置则传送到出生点
                else
                    mscp.SendDataToClient(new RawDataBuilder(65).PackByte(new BitsByte() { value = 0 }).PackInt16((short)mscp.ForwordIndex).PackSingle((float)mscp.Server.SpawnX * 16).PackSingle((float)mscp.Server.SpawnY * 16).PackByte(1).GetByteData());

                TShock.Log.ConsoleInfo($"<MultiSCore> {args.Player.Name} 成功传送.");
                args.Player.SendSuccessMsg($"成功传送到服务器 {mscp.Server.Name}");
            }
        }
    }
}
