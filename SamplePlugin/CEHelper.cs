using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.Network;
using Dalamud.Logging;
using Dalamud.Plugin;

namespace CEHelper
{
    public sealed class CEHelper : IDalamudPlugin
    {
        public string Name => "CEHelper";

        public Configuration Configuration;
        private PluginUI PluginUi;
        public Dictionary<long, long> Damage;

        public CEHelper(DalamudPluginInterface pluginInterface)
        {
            DalamudApi.Initialize(this, pluginInterface);
            Damage = new Dictionary<long, long>();

            Configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(DalamudApi.PluginInterface);

            DalamudApi.GameNetwork.NetworkMessage += NetWork;
            DalamudApi.ClientState.TerritoryChanged += TerryChange;

            PluginUi = new PluginUI(this);
        }

        #region damage functions

        struct ActorControl
        {
            public ushort category;
            ushort padding;
            public uint param1;
            public uint param2;
            public uint param3;
            public uint param4;

            uint padding1;
            //跳Dot      23:????:0000:0003:伤害:ActorID:0
            //效果结束   21:????:BUFF:0000:ActorID:0:0
        }

        Dictionary<long, long> DOT(IntPtr ptr, uint target)
        {
            var dat = Marshal.PtrToStructure<ActorControl>(ptr);
            var dic = new Dictionary<long, long>();
            if (dat.category == 23 && dat.param2 == 3)
            {
                dic.Add(((long)0xE000_0000 << 32) + target, dat.param3);
            }

            return dic;
        }


        unsafe Dictionary<long, long> AOE(IntPtr ptr, uint target)
        {
            var index = 0;
            while (*(long*)(ptr + 562 + index * 8) != 0x0000_0000_0000_0000) index++;
            var dic = new Dictionary<long, long>();
            for (int i = 0; i < index; i++)
            {
                dic.Add(((long)target << 32) + *(long*)(ptr + 560 + i * 8),
                    (*(byte*)(ptr + i * 64 + 46) << 16) + *(ushort*)(ptr + i * 64 + 48));
                //PluginLog.Information((*(long*)(ptr + 560 + i * 8)).ToString("X") +" "+(*(byte*)(ptr+i*64+46)<<16)+*(ushort*)(ptr+i*64+48));
            }

            return dic;
        }

        Dictionary<long, long> Single(IntPtr ptr, uint target)
        {
            var dic = new Dictionary<long, long>();
            var targetID = (uint)Marshal.ReadInt32(ptr, 112);
            var actionID = (uint)Marshal.ReadInt32(ptr, 8);
            var effecttype = Marshal.ReadByte(ptr, 42); //03=伤害 0xE=BUFF 04=治疗
            var direct = Marshal.ReadByte(ptr, 43); // 1=暴击  2=直击  3=直爆
            var damage = (Marshal.ReadByte(ptr, 46) << 16) + (ushort)Marshal.ReadInt16(ptr, 48);

            if (effecttype == 3) dic.Add(((long)target << 32) + targetID, damage);
            //PluginLog.Information($"@{actionID}:{effecttype:X}:{direct}:{damage}");

            return dic;
        }


        void NetWork(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId,
            NetworkMessageDirection direction)
        {
            if (direction == NetworkMessageDirection.ZoneDown)
            {
                var dic = opCode switch
                {
                    0x6E => Single(dataPtr, targetActorId),
                    0x1D8 => DOT(dataPtr, targetActorId),
                    0x3C0 => AOE(dataPtr, targetActorId),
                    _ => null
                };

                if (dic == null) return;
                PluginLog.Information(opCode.ToString("X"));
                foreach (var dmg in dic)
                {
                    if (Damage.ContainsKey(dmg.Key)) Damage[dmg.Key] += dmg.Value;
                    else Damage.Add(dmg.Key, dmg.Value);
                }
            }
        }

        #endregion


        private void TerryChange(object? sender, ushort territory)
        {
            Damage.Clear();
        }


        public void Dispose()
        {
            DalamudApi.GameNetwork.NetworkMessage -= NetWork;
            DalamudApi.ClientState.TerritoryChanged -= TerryChange;
            PluginUi.Dispose();
            DalamudApi.Dispose();
        }

        [Command("/cehelper")]
        [HelpMessage("Show config window of CEHelper.")]
        private void ToggleConfig(string command, string args)
        {
            PluginUi.DrawConfigUI();
        }
    }
}