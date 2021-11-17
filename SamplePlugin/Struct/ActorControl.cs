using System.Runtime.InteropServices;

namespace ACT.Struct
{
    public struct ActorControl
    {
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        public struct  ActorControlStruct
        {
            [FieldOffset(0x0)] public ushort category;
            [FieldOffset(0x2)] public ushort padding;
            [FieldOffset(0x4)] public uint param1;
            [FieldOffset(0x8)] public uint type;
            [FieldOffset(0xC)] public uint param3;
            [FieldOffset(0x10)] public uint param4;
            [FieldOffset(0x14)] public uint padding1;

            //跳Dot         23:????:0000:0003:伤害:????????:0
            //奇怪的Dot     23:????:BuffID:0003:伤害:ActorID:0
            //效果结束      21:????:BuffId:0000:ActorID:0:0
        }

    }
}
