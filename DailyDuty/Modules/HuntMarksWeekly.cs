﻿using System;
using System.Linq;
using System.Numerics;
using DailyDuty.Configuration.Components;
using DailyDuty.DataStructures;
using DailyDuty.Interfaces;
using DailyDuty.Localization;
using DailyDuty.UserInterface.Components;
using DailyDuty.UserInterface.Components.InfoBox;
using DailyDuty.Utilities;
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Utility.Signatures;
using ImGuiNET;

namespace DailyDuty.Modules;

public class HuntMarksWeeklySettings : GenericSettings
{
    public TrackedHunt[] TrackedHunts = 
    {
        new(HuntMarkType.RealmRebornElite, TrackedHuntState.Unobtained, new Setting<bool>(false)),
        new(HuntMarkType.HeavenswardElite, TrackedHuntState.Unobtained, new Setting<bool>(false)),
        new(HuntMarkType.StormbloodElite, TrackedHuntState.Unobtained, new Setting<bool>(false)),
        new(HuntMarkType.ShadowbringersElite, TrackedHuntState.Unobtained, new Setting<bool>(false)),
        new(HuntMarkType.EndwalkerElite, TrackedHuntState.Unobtained, new Setting<bool>(false)),
    };
}

internal class HuntMarksWeekly : IModule
{
    public ModuleName Name => ModuleName.HuntMarksWeekly;
    public IConfigurationComponent ConfigurationComponent { get; }
    public IStatusComponent StatusComponent { get; }
    public ILogicComponent LogicComponent { get; }
    public ITodoComponent TodoComponent { get; }
    public ITimerComponent TimerComponent { get; }

    private static HuntMarksWeeklySettings Settings => Service.ConfigurationManager.CharacterConfiguration.HuntMarksWeekly;
    public GenericSettings GenericSettings => Settings;

    public HuntMarksWeekly()
    {
        ConfigurationComponent = new ModuleConfigurationComponent(this);
        StatusComponent = new ModuleStatusComponent(this);
        LogicComponent = new ModuleLogicComponent(this);
        TodoComponent = new ModuleTodoComponent(this);
        TimerComponent = new ModuleTimerComponent(this);
    }

    public void Dispose()
    {
        LogicComponent.Dispose();
    }

    private class ModuleConfigurationComponent : IConfigurationComponent
    {
        public IModule ParentModule { get; }
        public ISelectable Selectable => new ConfigurationSelectable(ParentModule, this);

        public ModuleConfigurationComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public void Draw()
        {
            InfoBox.DrawGenericSettings(this);

            InfoBox.Instance
                .AddTitle(Strings.Module.HuntMarks.TrackedHunts)
                .AddList(Settings.TrackedHunts)
                .Draw();

            InfoBox.DrawNotificationOptions(this);
        }
    }

    private class ModuleStatusComponent : IStatusComponent
    {
        public IModule ParentModule { get; }

        public ISelectable Selectable => new StatusSelectable(ParentModule, this, ParentModule.LogicComponent.GetModuleStatus);

        public ModuleStatusComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public void Draw()
        {
            InfoBox.DrawGenericStatus(this);

            if (Settings.TrackedHunts.Any(hunt => hunt.Tracked.Value))
            {
                InfoBox.Instance
                    .AddTitle(Strings.Module.HuntMarks.TrackedHuntsStatus)
                    .BeginTable()
                    .AddRows(Settings.TrackedHunts.Where(row => row.Tracked.Value))
                    .EndTable()
                    .Draw();
            }
            else
            {
                InfoBox.Instance
                    .AddTitle(Strings.Module.HuntMarks.TrackedHuntsStatus)
                    .AddString(Strings.Module.HuntMarks.NoHuntsTracked, Colors.Orange)
                    .Draw();
            }

            InfoBox.Instance
                .AddTitle(Strings.Module.HuntMarks.ForceComplete)
                .AddAction(ForceCompleteButton)
                .Draw();
        }

        private void ForceCompleteButton()
        {
            var keys = ImGui.GetIO().KeyShift && ImGui.GetIO().KeyCtrl;

            ImGui.TextColored(Colors.Orange, Strings.Module.HuntMarks.ForceCompleteHelp);

            ImGuiHelpers.ScaledDummy(15.0f);

            var textSize = ImGui.CalcTextSize(Strings.Module.HuntMarks.NoUndo);
            var cursor = ImGui.GetCursorPos();
            var availableArea = InfoBox.Instance.InnerWidth;

            ImGui.SetCursorPos(cursor with {X = cursor.X + availableArea / 2.0f - textSize.X / 2.0f});
            ImGui.TextColored(Colors.Orange, Strings.Module.HuntMarks.NoUndo);

            ImGui.BeginDisabled(!keys);
            if (ImGui.Button(Strings.Module.HuntMarks.ForceComplete, new Vector2(InfoBox.Instance.InnerWidth, 23.0f * ImGuiHelpers.GlobalScale)))
            {
                foreach (var element in Settings.TrackedHunts)
                {
                    element.State = TrackedHuntState.Killed;
                }
            }
            ImGui.EndDisabled();
        }
    }

    private unsafe class ModuleLogicComponent : ILogicComponent
    {
        public IModule ParentModule { get; }
        public DalamudLinkPayload? DalamudLinkPayload => null;

        [Signature("D1 48 8D 0D ?? ?? ?? ?? 48 83 C4 20 5F E9 ?? ?? ?? ??", ScanType = ScanType.StaticAddress)]
        private readonly MobHuntStruct* huntData = null;

        public ModuleLogicComponent(IModule parentModule)
        {
            ParentModule = parentModule;

            SignatureHelper.Initialise(this);

            Service.Framework.Update += OnFrameworkUpdate;
        }

        public void Dispose()
        {
            Service.Framework.Update -= OnFrameworkUpdate;
        }

        private void OnFrameworkUpdate(Framework framework)
        {
            if (!Service.ConfigurationManager.CharacterDataLoaded) return;

            foreach (var hunt in Settings.TrackedHunts)
            {
                UpdateState(hunt);
            }
        }

        public string GetStatusMessage() => $"{GetIncompleteCount()} {Strings.Module.HuntMarks.HuntsRemaining}";

        public DateTime GetNextReset() => Time.NextWeeklyReset();

        public void DoReset()
        {
            foreach (var hunt in Settings.TrackedHunts)
            {
                hunt.State = TrackedHuntState.Unobtained;
            }
        }

        public ModuleStatus GetModuleStatus() => GetIncompleteCount() == 0 ? ModuleStatus.Complete : ModuleStatus.Incomplete;

        private void UpdateState(TrackedHunt hunt)
        {
            var data = huntData->Get(hunt.HuntType);

            switch (hunt.State)
            {
                case TrackedHuntState.Unobtained when data.Obtained:
                    hunt.State = TrackedHuntState.Obtained;
                    Service.ConfigurationManager.Save();
                    break;

                case TrackedHuntState.Obtained when !data.Obtained && data.KillCounts[0] != 1:
                    hunt.State = TrackedHuntState.Unobtained;
                    Service.ConfigurationManager.Save();
                    break;

                case TrackedHuntState.Obtained when data.KillCounts[0] == 1:
                    hunt.State = TrackedHuntState.Killed;
                    Service.ConfigurationManager.Save();
                    break;
            }
        }

        private int GetIncompleteCount()
        {
            return Settings.TrackedHunts.Count(hunt => hunt.Tracked.Value && hunt.State != TrackedHuntState.Killed);
        }
    }

    private class ModuleTodoComponent : ITodoComponent
    {
        public IModule ParentModule { get; }
        public CompletionType CompletionType => CompletionType.Weekly;
        public bool HasLongLabel => true;

        public ModuleTodoComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public string GetShortTaskLabel() => Strings.Module.HuntMarks.WeeklyLabel;

        public string GetLongTaskLabel()
        {
            var strings = Settings.TrackedHunts
                .Where(hunt => hunt.Tracked.Value && hunt.State != TrackedHuntState.Killed)
                .Select(hunt => hunt.HuntType.GetLabel())
                .ToList();

            return strings.Any() ? string.Join("\n", strings) : Strings.Module.HuntMarks.WeeklyLabel;
        }
    }

    private class ModuleTimerComponent : ITimerComponent
    {
        public IModule ParentModule { get; }

        public ModuleTimerComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public TimeSpan GetTimerPeriod() => TimeSpan.FromDays(7);

        public DateTime GetNextReset() => Time.NextWeeklyReset();
    }
}