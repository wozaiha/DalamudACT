using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.CompilerServices;

namespace ACT.Struct
{
    [StructLayout(LayoutKind.Explicit, Size = 0x2A)]
    public struct Header     
    {

        [FieldOffset(0x0)]ulong animationTargetId; // who the animation targets

        [FieldOffset(0x8)]uint actionId; // what the casting player casts, shown in battle log/ui
        [FieldOffset(0xC)]uint globalSequence; // seems to only increment on retail?

        [FieldOffset(0x10)]float animationLockTime; // maybe? doesn't seem to do anything
        [FieldOffset(0x14)]uint someTargetId; // always 00 00 00 E0, 0x0E000000 is the internal def for INVALID TARGET ID

        [FieldOffset(0x18)]ushort sourceSequence; // if 0, always shows animation, otherwise hides it. counts up by 1 for each animation skipped on a caster
        [FieldOffset(0x1A)]ushort rotation;
        [FieldOffset(0x1C)]ushort actionAnimationId; // the animation that is played by the casting character
        [FieldOffset(0x1E)]byte variation; // variation in the animation
        [FieldOffset(0x1F)]byte effectDisplayType;

        [FieldOffset(0x20)]byte unknown20; // is read by handler, runs code which gets the LODWORD of animationLockTime (wtf?)
        [FieldOffset(0x21)]byte effectCount; // ignores effects if 0, otherwise parses all of them
        [FieldOffset(0x22)]ushort padding0;

        [FieldOffset(0x24)]public uint padding1;
        [FieldOffset(0x28)]public ushort padding2;    
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x8)]
    public struct EffectEntry //0x8
    {
        [FieldOffset(0x0)]public byte type;
        [FieldOffset(0x1)]public byte param1;
        [FieldOffset(0x2)]public byte param2;
        [FieldOffset(0x3)]public byte param3;
        [FieldOffset(0x4)]public byte param4;
        [FieldOffset(0x5)]public byte param5;
        [FieldOffset(0x6)]public ushort param0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x6)]
    public struct Ender  //0x6
    {
        [FieldOffset(0x0)]public ushort padding1;
        [FieldOffset(0x2)]public uint padding2;
    }



    public unsafe struct Ability1
    {
        public Header header;
        public fixed byte enrty[64];
        public ulong targetId;
    }

    public unsafe struct Ability8
    {
        public Header header;
        public fixed byte enrty[8*8*8];
        public fixed ulong targetId[8];
    }

    public unsafe struct Ability16
    {
        public Header header;
        public fixed byte enrty[16*8*8];
        public fixed ulong targetId[16];
    }

    public unsafe struct Ability32
    {
        public Header header;
        public fixed byte enrty[32*8*8];
        public fixed ulong targetId[32];
    }

    public unsafe struct Ability64
    {
        public Header header;
        public fixed byte enrty[64*8];
        public fixed ulong targetId[64];
    }

}
