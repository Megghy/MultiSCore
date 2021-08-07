using OTAPI;
using System;
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
            int index = buffer.whoAmI;
            var mscp = MSCPlugin.Instance.ForwordPlayers[index];
            if (mscp.Back)
                switch (packetid)
                {
                    case 4:
                    case 16:
                    case 42:
                    case 50:
                    case 68:
                        return HookResult.Cancel;
                    case 5:
                        if (mscp.Server.RememberHostInventory)
                            return HookResult.Cancel;
                        else
                            break;
                    case 6:
                        mscp.Dispose();
                        return HookResult.Cancel;
                }
            else
                switch (packetid)
                {
                    case 82:
                        if (MSCPlugin.Instance.ForwordPlayers[index] is { } mscp_Chat)
                        {
                            buffer.reader.BaseStream.Position = start + 1;
                            if (buffer.reader.ReadByte() == 1)
                            {
                                buffer.reader.BaseStream.Position = start + 3;
                                if (buffer.reader.ReadString() == "Say")
                                {
                                    buffer.reader.BaseStream.Position = start + 7;
                                    var text = buffer.reader.ReadString();
                                    if (text.StartsWith(Commands.Specifier) || text.StartsWith(Commands.SilentSpecifier))
                                    {
                                        var cmdName = string.Empty;
                                        if (text.Contains(" "))
                                        {
                                            cmdName = text.Split(' ')[0].Remove(0, text.StartsWith(Commands.Specifier) ? Commands.Specifier.Length : Commands.SilentSpecifier.Length);
                                        }
                                        else cmdName = text.Remove(0, text.StartsWith(Commands.Specifier) ? Commands.Specifier.Length : Commands.SilentSpecifier.Length);
                                        if (cmdName.ToLower() == "msc" || mscp_Chat.Server.GlobalCommand.Contains(cmdName))  //如果存在于globalCommand则阻止发送
                                        {
                                            Commands.HandleCommand(TShock.Players[index], text);
                                            return HookResult.Cancel;
                                        }
                                    }
                                    else
                                    {
                                        Utils.SendMessageToHostPlayer($"[{mscp_Chat.Server.Name}] {TShock.Players[index].Name}: {text}");
                                    }
                                }
                            }
                        }
                        break;
                }
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
                    mscp.SendDataToClient(new RawDataBuilder(65).PackByte(new BitsByte() { value = 0 }).PackInt16((short)mscp.ForwordIndex).PackSingle((float)mscp.SpawnX * 16).PackSingle((float)mscp.SpawnY * 16).PackByte(1).GetByteData());
                else
                    mscp.SendDataToClient(new RawDataBuilder(65).PackByte(new BitsByte() { value = 0 }).PackInt16((short)mscp.ForwordIndex).PackSingle((float)mscp.Server.SpawnX * 16).PackSingle((float)mscp.Server.SpawnY * 16).PackByte(1).GetByteData());
                if (args.Player.ContainsData("MultiSCore_Switching"))
                    args.Player.RemoveData("MultiSCore_Switching");
                mscp.Connected = true;
                TShock.Log.ConsoleInfo(string.Format(Utils.GetText("Log_ConnectSuccess"), args.Player.Name, mscp.Server.Name));
                args.Player.SendSuccessMsg(string.Format(Utils.GetText("Prompt_ConnectSuccess"), mscp.Server.Name));
            }
        }
    }
}
