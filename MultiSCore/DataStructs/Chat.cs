using System.IO;

namespace MultiSCore.DataStructs
{
    struct Chat
    {
        public Chat(BinaryReader reader)
        {
            try
            {
                reader.BaseStream.Position = 0;
                Index = reader.ReadInt32();
                Message = reader.ReadString();
            }
            catch { Index = -1; Message = ""; }
        }
        public int Index { get; set; }
        public string Message { get; set; }
    }
}
