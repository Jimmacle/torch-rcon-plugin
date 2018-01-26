using System;
using System.IO;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace RconPlugin
{
    //Max size 4096 bytes
    public struct RconPacket
    {
        private static int HEADER_SIZE = 8;
        private static int END_TERMINATION = 2;

        public int Size;
        public int Id;
        public PacketType Type;
        public string Body;

        public RconPacket(int id, PacketType type, string body = null)
        {
            Size = 0;
            Id = id;
            Type = type;
            Body = body ?? string.Empty;
        }

        public static RconPacket FromBytes(byte[] buffer, int offset, int length)
        {
            //TODO validate byte length

            var packet = new RconPacket();
            packet.Size = BitConverter.ToInt32(buffer, offset);
            offset += sizeof(int);
            packet.Id = BitConverter.ToInt32(buffer, offset);
            offset += sizeof(int);
            packet.Type = (PacketType)BitConverter.ToInt32(buffer, offset);
            offset += sizeof(int);
            var readLength = packet.Size - (HEADER_SIZE + END_TERMINATION);
            packet.Body = Encoding.ASCII.GetString(buffer, offset, readLength);

            return packet;
        }

        public byte[] GetBytes()
        {
            var strBytes = Encoding.ASCII.GetBytes(Body);
            Size = strBytes.Length + HEADER_SIZE + END_TERMINATION;
            if (Size > 4096)
                throw new InvalidOperationException("The packet is too large. (max 4KiB)");

            using (var ms = new MemoryStream(Size + 4))
            {
                ms.Write(BitConverter.GetBytes(Size), 0, sizeof(int));
                ms.Write(BitConverter.GetBytes(Id), 0, sizeof(int));
                ms.Write(BitConverter.GetBytes((int)Type), 0, sizeof(int));
                ms.Write(strBytes, 0, strBytes.Length);
                ms.Write(BitConverter.GetBytes('\0'), 0, 2);

                return ms.ToArray();
            }
        }
    }
}
