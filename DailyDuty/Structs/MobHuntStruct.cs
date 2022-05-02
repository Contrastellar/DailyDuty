﻿using System;
using System.Runtime.InteropServices;
using DailyDuty.Enums;
using DailyDuty.Localization;
using Lumina.Excel.GeneratedSheets;

// ReSharper disable InconsistentNaming

namespace DailyDuty.Structs
{
    public enum HuntMarkType
    {
        RealmReborn_LevelOne = 0,
        Heavensward_LevelOne = 1,
        Heavensward_LevelTwo = 2,
        Heavensward_LevelThree = 3,
        RealmReborn_Elite = 4,
        Heavensward_Elite = 5,
        Stormblood_LevelOne = 6,
        Stormblood_LevelTwo = 7,
        Stormblood_LevelThree = 8,
        Stormblood_Elite = 9,
        Shadowbringers_LevelOne = 10,
        Shadowbringers_LevelTwo = 11,
        Shadowbringers_LevelThree = 12,
        Shadowbringers_Elite = 13,
        Endwalker_LevelOne = 14,
        Endwalker_LevelTwo = 15,
        Endwalker_LevelThree = 16,
        Endwalker_Elite = 17
    }

    public class HuntData
    {
        public byte HuntID { get; init; }
        public HuntMarkType HuntType { get; init; }
        public KillCounts KillCounts { get; init; } = new();
        public HuntInfo TargetInfo
        {
            get
            {
                var orderTypeSheet = Service.DataManager.GetExcelSheet<MobHuntOrderType>()!;
                var huntOrderSheet = Service.DataManager.GetExcelSheet<MobHuntOrder>()!;

                var indexOffset = orderTypeSheet.GetRow((uint)HuntType)!.OrderStart.Row;
                var targetRow = indexOffset + HuntID - 1;

                if (IsElite)
                {
                    return new HuntInfo
                    {
                        [0] = huntOrderSheet.GetRow(targetRow, 0)!,
                    };
                }
                else
                {
                    return new HuntInfo
                    {
                        [0] = huntOrderSheet.GetRow(targetRow, 0)!,
                        [1] = huntOrderSheet.GetRow(targetRow, 1)!,
                        [2] = huntOrderSheet.GetRow(targetRow, 2)!,
                        [3] = huntOrderSheet.GetRow(targetRow, 3)!,
                        [4] = huntOrderSheet.GetRow(targetRow, 4)!,
                    };
                }
            }
        }

        public bool Obtained { get; init; }
        public bool IsElite => HuntType is 
            HuntMarkType.Endwalker_Elite or 
            HuntMarkType.Shadowbringers_Elite or 
            HuntMarkType.Stormblood_Elite or 
            HuntMarkType.Heavensward_Elite or 
            HuntMarkType.RealmReborn_Elite;
        }

    public class HuntInfo
    {
        private MobHuntOrder[] Raw { get; set; } = new MobHuntOrder[5];

        public MobHuntOrder this[int i]
        {
            get => Raw[i];
            set => Raw[i] = value;
        }
    }

    public class KillCounts
    {
        private int[] Raw { get; set; } = new int[5];

        public int this[int i]
        {
            get => Raw[i];
            set => Raw[i] = value;
        }
        
    }

    //[Signature("D1 48 8D 0D ?? ?? ?? ?? 48 83 C4 20 5F E9 ?? ?? ?? ??", ScanType = ScanType.StaticAddress)]
    [StructLayout(LayoutKind.Explicit, Size = 0x198)]
    public unsafe struct MobHuntStruct
    {
        [FieldOffset(0x00)] private fixed byte Raw[0x198];

        [FieldOffset(0x1A)] private fixed byte ID[18];
        [FieldOffset(0x2C)] private fixed int CurrentKills[18 * 5];
        [FieldOffset(0x194)] private readonly int Flags;

        public HuntData Get(HuntMarkType type)
        {
            int index = (int)type;

            return new HuntData
            {
                KillCounts = new KillCounts
                {
                    [0] = CurrentKills[(index * 5) + 0],
                    [1] = CurrentKills[(index * 5) + 1],
                    [2] = CurrentKills[(index * 5) + 2],
                    [3] = CurrentKills[(index * 5) + 3],
                    [4] = CurrentKills[(index * 5) + 4]
                },
                HuntID = ID[index],
                Obtained = (Flags & (1 << index)) != 0,
                HuntType = type
            };
        }
    }

    public static class HuntMarkTypeExtensions
    {
        public static string GetLabel(this HuntMarkType value)
        {
            var expansionLabel = value.GetExpansion().GetLabel();
            var level = value.GetLevel();

            return expansionLabel + " " + level;
        }

        public static ExpansionType GetExpansion(this HuntMarkType type)
        {
            switch (type)
            {
                case HuntMarkType.RealmReborn_Elite:
                case HuntMarkType.RealmReborn_LevelOne:
                    return ExpansionType.RealmReborn;

                case HuntMarkType.Heavensward_LevelOne:
                case HuntMarkType.Heavensward_LevelTwo:
                case HuntMarkType.Heavensward_LevelThree:
                case HuntMarkType.Heavensward_Elite:
                    return ExpansionType.Heavensward;

                case HuntMarkType.Stormblood_LevelOne:
                case HuntMarkType.Stormblood_LevelTwo:
                case HuntMarkType.Stormblood_LevelThree:
                case HuntMarkType.Stormblood_Elite:
                    return ExpansionType.Stormblood;

                case HuntMarkType.Shadowbringers_LevelOne:
                case HuntMarkType.Shadowbringers_LevelTwo:
                case HuntMarkType.Shadowbringers_LevelThree:
                case HuntMarkType.Shadowbringers_Elite:
                    return ExpansionType.Shadowbringers;

                case HuntMarkType.Endwalker_LevelOne:
                case HuntMarkType.Endwalker_LevelTwo:
                case HuntMarkType.Endwalker_LevelThree:
                case HuntMarkType.Endwalker_Elite:
                    return ExpansionType.Endwalker;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static string GetLevel(this HuntMarkType type)
        {
            switch (type)
            {
                case HuntMarkType.RealmReborn_LevelOne:
                case HuntMarkType.Heavensward_LevelOne:
                case HuntMarkType.Stormblood_LevelOne:
                case HuntMarkType.Shadowbringers_LevelOne:
                case HuntMarkType.Endwalker_LevelOne:
                    return Strings.Common.LevelOneLabel;

                case HuntMarkType.Shadowbringers_LevelTwo:
                case HuntMarkType.Endwalker_LevelTwo:
                case HuntMarkType.Heavensward_LevelTwo:
                case HuntMarkType.Stormblood_LevelTwo:
                    return Strings.Common.LevelTwoLabel;


                case HuntMarkType.Stormblood_LevelThree:
                case HuntMarkType.Shadowbringers_LevelThree:
                case HuntMarkType.Endwalker_LevelThree:
                case HuntMarkType.Heavensward_LevelThree:
                    return Strings.Common.LevelThreeLabel;


                case HuntMarkType.Heavensward_Elite:
                case HuntMarkType.Stormblood_Elite:
                case HuntMarkType.Endwalker_Elite:
                case HuntMarkType.Shadowbringers_Elite:
                case HuntMarkType.RealmReborn_Elite:
                    return Strings.Common.EliteLabel;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}