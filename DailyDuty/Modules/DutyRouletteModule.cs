﻿using System;
using System.Linq;
using DailyDuty.Data.Components;
using DailyDuty.Data.ModuleSettings;
using DailyDuty.Enums;
using DailyDuty.Interfaces;
using DailyDuty.Localization;
using DailyDuty.Utilities;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyDuty.Modules
{
    internal unsafe class DutyRouletteModule :
        IDisposable,
        ILoginNotification,
        IZoneChangeThrottledNotification,
        ICompletable,
        IDailyResettable,
        IUpdateable
    {
        public static DutyRouletteSettings Settings => Service.CharacterConfiguration.DutyRoulette;
        public CompletionType Type => CompletionType.Daily;
        public GenericSettings GenericSettings => Settings;
        public string DisplayName => Strings.Module.DutyRouletteLabel;

        public Action? ExpandedDisplay => () =>
        {
            var settings = Service.SystemConfiguration.Windows.Todo;

            foreach (var tracked in Settings.TrackedRoulettes)
            {
                if (!tracked.Completed && tracked.Tracked)
                {
                    ImGui.TextColored(settings.Colors.IncompleteColor, $"{tracked.Type} Roulette");
                }
                else if (tracked.Completed && tracked.Tracked && settings.ShowTasksWhenComplete)
                {
                    ImGui.TextColored(settings.Colors.CompleteColor, $"{tracked.Type} Roulette");
                }
            }
        };

        private delegate IntPtr OpenRouletteToDutyDelegate(AgentInterface* agent, byte a2, byte a3);
        private delegate byte IsRouletteIncompleteDelegate(AgentInterface* agent, byte a2);

        [Signature("E9 ?? ?? ?? ?? 8B 93 ?? ?? ?? ?? 48 83 C4 20")]
        private readonly OpenRouletteToDutyDelegate openRouletteDuty = null!;

        [Signature("48 83 EC 28 84 D2 75 07 32 C0", ScanType = ScanType.Text)]
        private readonly IsRouletteIncompleteDelegate isRouletteIncomplete = null!;

        [Signature("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 74 0C 48 8D 4C 24", ScanType = ScanType.StaticAddress)]
        private readonly AgentInterface* rouletteBasePointer = null!;

        private readonly AgentInterface* agentContentsFinder = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ContentsFinder);

        private readonly DalamudLinkPayload openDutyFinder;

        public DutyRouletteModule()
        {
            SignatureHelper.Initialise(this);

            Service.PluginInterface.RemoveChatLinkHandler((uint)ChatPayloads.OpenRouletteDutyFinder);
            openDutyFinder = Service.PluginInterface.AddChatLinkHandler((uint) ChatPayloads.OpenRouletteDutyFinder, OpenRouletteDutyFinder);
        }

        public void Dispose()
        {
            Service.PluginInterface.RemoveChatLinkHandler((uint) ChatPayloads.OpenRouletteDutyFinder);
        }

        private void OpenRouletteDutyFinder(uint arg1, SeString arg2)
        {
            openRouletteDuty(agentContentsFinder, GetFirstMissingRoulette(), 0);
        }

        public bool IsCompleted() => RemainingRoulettesCount() == 0;

        public void SendNotification()
        {
            if (!IsCompleted() && !Condition.IsBoundByDuty())
            {
                Chat.Print(Strings.Module.DutyRouletteLabel,
                    $"{RemainingRoulettesCount()} " + (RemainingRoulettesCount() > 1
                        ? Strings.Module.DutyRouletteRoulettesRemaining
                        : Strings.Module.DutyRouletteRoulettesRemainingSingular),
                    Settings.EnableClickableLink ? openDutyFinder : null);
            }
        }

        public void Update()
        {
            foreach (var trackedRoulette in Settings.TrackedRoulettes)
            {
                var rouletteStatus = isRouletteIncomplete(rouletteBasePointer, (byte) trackedRoulette.Type) == 0;

                if (trackedRoulette.Completed != rouletteStatus)
                {
                    trackedRoulette.Completed = rouletteStatus;

                    Service.LogManager.LogMessage(ModuleType.DutyRoulette, $"{trackedRoulette.Type} Completed");

                    if (IsCompleted())
                    {
                        Service.LogManager.LogMessage(ModuleType.DutyRoulette, "All Tasks Completed");
                    }

                    Service.CharacterConfiguration.Save();
                }
            }
        }

        void IResettable.ResetThis()
        {
            Service.LogManager.LogMessage(ModuleType.DutyRoulette, "Daily Reset - Resetting");

            foreach (var task in Settings.TrackedRoulettes)
            {
                task.Completed = false;
            }
        }

        //
        //  Implementation
        //

        private int RemainingRoulettesCount()
        {
            return Settings.TrackedRoulettes
                .Where(r => r.Tracked)
                .Count(r => !r.Completed);
        }

        private byte GetFirstMissingRoulette()
        {
            foreach (var trackedRoulette in Settings.TrackedRoulettes)
            {
                if (trackedRoulette is {Completed: false, Tracked: true})
                {
                    return (byte) trackedRoulette.Type;
                }
            }

            return (byte)RouletteType.Leveling;
        }
    }
}