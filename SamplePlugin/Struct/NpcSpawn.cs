namespace DalamudACT.Struct
{
    internal struct NpcSpawn
    {
        private uint gimmickId; // needs to be existing in the map, mob will snap to it
        private byte u2b;
        private byte u2ab;
        private byte gmRank;
        private byte u3b;

        private byte aggressionMode; // 1 passive, 2 aggressive
        private byte onlineStatus;
        private byte u3c;
        private byte pose;

        private uint u4;

        private long targetId;
        private uint u6;
        private uint u7;
        private long mainWeaponModel;
        private long secWeaponModel;
        private long craftToolModel;

        private uint u14;
        private uint u15;
        private uint bNPCBase;
        public uint bNPCName;
        private uint levelId;
        private uint u19;
        private uint directorId;
        public uint spawnerId;
        private uint parentActorId;
        private uint hPMax;
        private uint hPCurr;
        private uint displayFlags;
        private ushort fateID;
        private ushort mPCurr;
        private ushort unknown1; // 0
        private ushort unknown2; // 0 or pretty big numbers > 30000
        private ushort modelChara;
        private ushort rotation;
        private ushort activeMinion;
        private byte spawnIndex;
        private byte state;
        private byte persistantEmote;
        private byte modelType;
        private byte subtype;
    }
}