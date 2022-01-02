using System.Collections.Generic;

namespace ACT
{
    public class Potency
    {
        public static readonly float[] Muti = new[]
        {
            0f, //冒险者
            2.24f / 3f, //剑术师
            2.56f / 3f, //格斗家
            1.12f, //斧术师
            2.8f / 3f, //枪术师
            3.04f / 3f, //弓箭手
            1.0f, //幻术师
            1.0f, //咒术师
            0f, //刻木匠
            0f, //锻铁匠
            0f, //铸甲匠
            0f, //雕金匠
            0f, //制革匠
            0f, //裁衣匠
            0f, //炼金术士
            0f, //烹调师
            0f, //采矿工
            0f, //园艺工
            0f, //捕鱼人
            2.24f / 3f, //骑士
            2.56f / 3f, //武僧
            1.12f, //战士
            2.8f / 3f, //龙骑士
            3.04f / 3f, //诗人
            1f, //白魔法师
            1f, //黑魔法师 天语没算
            1.0f, //秘术师
            1f, //召唤师
            1f, //学者
            1.0f, //双剑师
            2.56f / 3f, //忍者
            0.88f, //机工士
            2.96f / 3f, //暗黑骑士
            1f, //占星术士
            0.88f, //武士 
            1f, //赤魔法师
            0f, //青魔法师
            2.8f / 3f, //绝枪战士
            1.04f, //舞者
            3.2f / 3f, //Reaper
            1f //Sage
        };

        public static readonly uint[] BaseSkill =
        {
            0, //冒险者
            7, //剑术师
            7, //格斗家
            7, //斧术师
            7, //枪术师
            7, //弓箭手
            7, //幻术师
            7, //咒术师
            0, //刻木匠
            0, //锻铁匠
            0, //铸甲匠
            0, //雕金匠
            0, //制革匠
            0, //裁衣匠
            0, //炼金术士
            0, //烹调师
            0, //采矿工
            0, //园艺工
            0, //捕鱼人
            7, //骑士
            7, //武僧
            7, //战士
            7, //龙骑士
            8, //诗人
            3568, //白魔法师
            3577, //黑魔法师 天语没算
            7, //秘术师
            8, //召唤师 也许要换？ 3579
            3584, //学者
            7, //双剑师
            7, //忍者
            8, //机工士
            7, //暗黑骑士
            3598, //占星术士
            7, //武士 
            7, //赤魔法师
            7, //青魔法师
            7, //绝枪战士
            7, //舞者
            7, //Reaper
            24283 //Sage
        };

        public static Dictionary<uint, float> SkillPot = new()
        {
            { 7, 110f },
            { 8, 100f },
            { 3577, 300f * 1.8f }, //火4 @ 3层火
            { 3568, 230 }, //垒石
            { 7431, 270 }, //崩石
            { 16533, 290 }, //闪耀
            { 25859, 310 }, //闪耀3
            { 3584, 220 }, //气炎法
            { 7435, 240 }, //魔炎法
            { 16541, 255 }, //死炎法
            { 25865, 295 }, //炎法4
            { 3598, 160 }, //灾星
            { 7442, 190 }, //祸星
            { 16555, 230 }, //煞星
            { 25871, 250 }, //落星
            { 24283, 300 }, //Dosis
            { 24306, 320 }, //Dosis2
            { 24312, 330 } //Dosis3
        };

        public static Dictionary<uint, uint> BuffToAction = new()
        {
            { 749, 3639 }, //腐秽大地
            { 501, 2270 }, //土遁之术
            { 861, 2878 }, //野火
            { 2706, 25837 }, //风宝宝
            { 1205, 7418 },//FlameThrower
            
        };

        public static Dictionary<uint, uint> DotPot = new()
        {
            { 248, 30 }, //厄运流转
            { 725, 65 }, //沥血剑
            //{ 749, 60 }, //腐秽大地
            { 1837, 60 }, //音速破
            { 1838, 60 }, //弓形冲波

            { 144, 60 }, //烈风
            { 1871, 60 }, //天辉
            { 17865, 40 }, //猛毒菌
            { 1895, 70 }, //蛊毒法
            { 843, 50 }, //炽灼
            { 1881, 55 }, //焚灼
            { 2614, 40 }, //Eukrasian Dosis
            { 2615, 60 }, //Eukrasian Dosis2
            { 2616, 70 }, //Eukrasian Dosis3

            { 246, 70 }, //破碎拳
            { 118, 40 }, //樱花怒放
            //{ 508, 0 }, //影牙
            //{ 501, 70 }, //土遁之术
            { 1228, 30 }, //彼岸花(回天1.5x)

            { 129, 20 }, //风蚀箭
            { 1201, 25 }, //狂风蚀箭
            { 124, 15 }, //毒咬箭
            { 1200, 20 }, //烈毒咬箭

            { 161, 35 }, //闪雷 1
            { 163, 35 }, //暴雷 3
            { 162, 15 }, //震雷 2
            { 1210, 20 }, //霹雷 AOE 4

            { 1866, 50 }//喷毒
        };
    }

    internal enum Job : uint
    {
        ADV = 0,
        GLA = 1,
        PGL = 2,
        MRD = 3,
        LNC = 4,
        ARC = 5,
        CNJ = 6,
        THM = 7,
        CRP = 8,
        BSM = 9,
        ARM = 10,
        GSM = 11,
        LTW = 12,
        WVR = 13,
        ALC = 14,
        CUL = 15,
        MIN = 16,
        BTN = 17,
        FSH = 18,
        PLD = 19,
        MNK = 20,
        WAR = 21,
        DRG = 22,
        BRD = 23,
        WHM = 24,
        BLM = 25,
        ACN = 26,
        SMN = 27,
        SCH = 28,
        ROG = 29,
        NIN = 30,
        MCH = 31,
        DRK = 32,
        AST = 33,
        SAM = 34,
        RDM = 35,
        BLU = 36,
        GNB = 37,

        DNC = 38,
        RPR = 39,
        SGE = 40
    }
}