﻿namespace DalamudACT.Struct
{
    internal struct ActorCast
    {
        public ushort action_id;
        public byte skillType;
        public byte unknown;
        public uint unknown_1; // action id or mount id
        public float cast_time;
        public uint target_id;
        public ushort rotation;
        public ushort flag; // 1 = interruptible blinking cast bar
        public uint unknown_2;
        public ushort posX;
        public ushort posY;
        public ushort posZ;
        public ushort unknown_3;
    }
};