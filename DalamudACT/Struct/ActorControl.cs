﻿using System.Runtime.InteropServices;

namespace DalamudACT.Struct
{
    public struct ActorControl
    {
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        public struct ActorControlStruct
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
    public enum ActorControlCategory : ushort
    {
        HoT = 0x603,
        DoT = 0x605,
        CancelAbility = 0x0f,
        Death = 0x06,
        TargetIcon = 0x22,
        Tether = 0x23,
        GainEffect = 0x14,
        LoseEffect = 0x15,
        UpdateEffect = 0x16,
        Targetable = 0x36,
        DirectorUpdate = 0x6d,
        SetTargetSign = 0x1f6,
        LimitBreak = 0x1f9
    }
}