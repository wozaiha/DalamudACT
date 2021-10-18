using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Game.ClientState.Objects.Enums;
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

        public class ACTBattle
        {
            public ACTBattle(long time1, long time2, Dictionary<uint, long> damage)
            {
                StartTime = time1;
                EndTime = time2;
                Damage = damage;
            }

            public long StartTime;
            public long EndTime;
            public Dictionary<uint, long> Damage;

            public long Duration()
            {
                return (EndTime - StartTime) switch
                {
                    <= 0 => 0, //战斗中
                    _ => EndTime - StartTime //战斗结束
                };
            }
        }

        public List<ACTBattle> Battles = new();


        public CEHelper(DalamudPluginInterface pluginInterface)
        {
            DalamudApi.Initialize(this, pluginInterface);


            Configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(DalamudApi.PluginInterface);

            DalamudApi.GameNetwork.NetworkMessage += NetWork;

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

        Dictionary<uint, long> DOT(IntPtr ptr)
        {
            var dat = Marshal.PtrToStructure<ActorControl>(ptr);
            var dic = new Dictionary<uint, long>();
            if (dat.category == 23 && dat.param2 == 3)
            {
                dic.Add(0xE000_0000, dat.param3);
            }

            return dic;
        }


        unsafe Dictionary<uint, long> AOE(IntPtr ptr, uint target)
        {
            var index = 0;
            while (*(long*)(ptr + 562 + index * 8) != 0x0000_0000_0000_0000) index++;
            var dic = new Dictionary<uint, long>();
            for (int i = 0; i < index; i++)
            {
                dic.Add(target,
                    (*(byte*)(ptr + i * 64 + 46) << 16) + *(ushort*)(ptr + i * 64 + 48));
                //PluginLog.Information((*(long*)(ptr + 560 + i * 8)).ToString("X") +" "+(*(byte*)(ptr+i*64+46)<<16)+*(ushort*)(ptr+i*64+48));
            }

            return dic;
        }

        Dictionary<uint, long> Single(IntPtr ptr, uint target)
        {
            var dic = new Dictionary<uint, long>();
            //var targetID = (uint)Marshal.ReadInt32(ptr, 112);
            //var actionID = (uint)Marshal.ReadInt32(ptr, 8);
            var effecttype = Marshal.ReadByte(ptr, 42); //03=伤害 0xE=BUFF 04=治疗
            //var direct = Marshal.ReadByte(ptr, 43); // 1=暴击  2=直击  3=直爆
            var damage = (Marshal.ReadByte(ptr, 46) << 16) + (ushort)Marshal.ReadInt16(ptr, 48);

            if (effecttype == 3) dic.Add(target, damage);
            //PluginLog.Information($"@{actionID}:{effecttype:X}:{direct}:{damage}");

            return dic;
        }

        #endregion

        void NetWork(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId,
            NetworkMessageDirection direction)
        {
            if (Battles.Count == 0)
                Battles.Add(new ACTBattle(0,
                    0, new Dictionary<uint, long>()));

            if (direction != NetworkMessageDirection.ZoneDown) return;

            var time = DateTimeOffset.Now.ToUnixTimeSeconds();

            if ((DalamudApi.ClientState.LocalPlayer.StatusFlags & StatusFlags.InCombat) != 0 &&
                Battles[^1].StartTime == 0)     //开始战斗
            {
                Battles[^1].StartTime = time;
            }
            //结束战斗
            if ((DalamudApi.ClientState.LocalPlayer.StatusFlags & StatusFlags.InCombat) != 0 &&
                     Battles[^1].StartTime != 0) Battles[^1].EndTime = time;
                

            var dic = opCode switch
            {
                0x6E => Single(dataPtr, targetActorId),
                0x1D8 => DOT(dataPtr),
                0x3C0 => AOE(dataPtr, targetActorId),
                _ => null
            };
            if (dic == null) return;
            
            foreach (var (key, value) in dic)
            {
                if (DalamudApi.PartyList.Length > 1 &&
                    DalamudApi.PartyList.FirstOrDefault(x => x.ObjectId == key) == default) continue;

                if (Battles[^1].Duration() > 1 && time - Battles[^1].EndTime >1)    //下一场战斗
                {
                    Battles.Add(new ACTBattle(0,
                        0, new Dictionary<uint, long>()));
                    if (Battles.Count > 3) Battles.RemoveAt(0);
                    Battles[^1].StartTime = time;
                }
                if (Battles[^1].Damage.ContainsKey(key)) Battles[^1].Damage[key] += value;
                else Battles[^1].Damage.Add(key, value);
            }
        }


        public void Dispose()
        {
            DalamudApi.GameNetwork.NetworkMessage -= NetWork;
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