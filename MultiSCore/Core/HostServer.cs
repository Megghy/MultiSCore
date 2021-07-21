using OTAPI;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

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
            var mscp = MSCPlugin.Instance.ForwordPlayers[buffer.whoAmI];
            if (mscp.Back)
                switch (packetid)
                {
                    case 4:
                    case 16:
                    case 42:
                    case 50:
                    case 68:
                    case 5:
                        return HookResult.Cancel;
                    case 6:
                        mscp.Dispose();
                        return HookResult.Cancel;
                }
            else 
                mscp.SendDataToForword(buffer.readBuffer, start - 2, length + 2);
            return HookResult.Cancel;
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
