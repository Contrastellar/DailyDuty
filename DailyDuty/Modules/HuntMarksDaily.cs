﻿using System;
using System.Linq;
using DailyDuty.Configuration.Components;
using DailyDuty.Configuration.Enums;
using DailyDuty.Configuration.ModuleSettings;
using DailyDuty.DataStructures;
using DailyDuty.Interfaces;
using DailyDuty.Modules.Enums;
using DailyDuty.System.Localization;
using DailyDuty.UserInterface.Components;
using DailyDuty.UserInterface.Components.InfoBox;
using DailyDuty.Utilities;
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility.Signatures;

namespace DailyDuty.Modules;

internal class HuntMarksDaily : IModule
{
    public ModuleName Name => ModuleName.HuntMarksDaily;
    public IConfigurationComponent ConfigurationComponent { get; }
    public IStatusComponent StatusComponent { get; }
    public ILogicComponent LogicComponent { get; }
    public ITodoComponent TodoComponent { get; }
    public ITimerComponent TimerComponent { get; }

    private static HuntMarksDailySettings Settings => Service.ConfigurationManager.CharacterConfiguration.HuntMarksDaily;
    public GenericSettings GenericSettings => Settings;

    public HuntMarksDaily()
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

        private readonly InfoBox optionsInfoBox = new();
        private readonly InfoBox huntTracking = new();
        private readonly InfoBox notificationOptionsInfoBox = new();

        public ModuleConfigurationComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public void Draw()
        {
            optionsInfoBox
                .AddTitle(Strings.Configuration.Options)
                .AddConfigCheckbox(Strings.Common.Enabled, Settings.Enabled)
                .Draw();

            huntTracking
                .AddTitle(Strings.Module.HuntMarks.TrackedHunts)
                .AddAction(() =>
                {
                    var checkboxAction = Actions.GetConfigCheckboxAction;

                    foreach (var roulette in Settings.TrackedHunts)
                        checkboxAction(roulette.HuntType.GetLabel(), roulette.Tracked).Invoke();
                })
                .Draw();

            notificationOptionsInfoBox
                .AddTitle(Strings.Configuration.NotificationOptions)
                .AddConfigCheckbox(Strings.Configuration.OnLogin, Settings.NotifyOnLogin)
                .AddConfigCheckbox(Strings.Configuration.OnZoneChange, Settings.NotifyOnZoneChange)
                .Draw();
        }
    }

    private class ModuleStatusComponent : IStatusComponent
    {
        public IModule ParentModule { get; }

        public ISelectable Selectable => new StatusSelectable(ParentModule, this, ParentModule.LogicComponent.GetModuleStatus);

        private readonly InfoBox status = new();
        private readonly InfoBox trackedHunts = new();

        public ModuleStatusComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public void Draw()
        {
            if (ParentModule.LogicComponent is not ModuleLogicComponent logicModule) return;

            var moduleStatus = logicModule.GetModuleStatus();

            status
                .AddTitle(Strings.Status.Label)
                .BeginTable()

                .AddRow(
                    Strings.Status.ModuleStatus,
                    moduleStatus.GetLocalizedString(),
                    secondColor: moduleStatus.GetStatusColor())
                .EndTable()
                .Draw();

            if (Settings.TrackedHunts.Any(hunt => hunt.Tracked.Value))
            {
                trackedHunts
                    .AddTitle(Strings.Module.HuntMarks.TrackedHuntsStatus)
                    .BeginTable()
                    .AddRows(Settings.TrackedHunts
                        .Where(row => row.Tracked.Value)
                        .Select(row => row.GetInfoBoxTableRow()))
                    .EndTable()
                    .Draw();
            }
            else
            {
                trackedHunts
                    .AddTitle(Strings.Module.HuntMarks.TrackedHuntsStatus)
                    .AddString(Strings.Module.HuntMarks.NoHuntsTracked, Colors.Orange)
                    .Draw();
            }
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

            Service.Framework.Update += FrameworkOnUpdate;
        }

        public void Dispose()
        {
            Service.Framework.Update -= FrameworkOnUpdate;
        }

        private void FrameworkOnUpdate(Framework framework)
        {
            foreach (var hunt in Settings.TrackedHunts)
            {
                UpdateState(hunt);
            }
        }

        private void UpdateState(TrackedHunt hunt)
        {
            var data = huntData->Get(hunt.HuntType);

            switch (hunt.State)
            {
                case TrackedHuntState.Unobtained when data.Obtained:
                    hunt.State = TrackedHuntState.Obtained;
                    Service.ConfigurationManager.Save();
                    break;

                case TrackedHuntState.Obtained when !data.Obtained && !AllTargetsKilled(data):
                    hunt.State = TrackedHuntState.Unobtained;
                    Service.ConfigurationManager.Save();
                    break;

                case TrackedHuntState.Obtained when AllTargetsKilled(data):
                    hunt.State = TrackedHuntState.Killed;
                    Service.ConfigurationManager.Save();
                    break;
            }
        }

        public string GetStatusMessage() => $"{GetIncompleteCount()} {Strings.Module.HuntMarks.HuntsRemaining}";

        public DateTime GetNextReset() => Time.NextDailyReset();

        public void DoReset()
        {
            foreach (var hunt in Settings.TrackedHunts)
            {
                hunt.State = TrackedHuntState.Unobtained;
            }
        }

        public ModuleStatus GetModuleStatus() => GetIncompleteCount() == 0 ? ModuleStatus.Complete : ModuleStatus.Incomplete;

        private int GetIncompleteCount() => Settings.TrackedHunts.Count(hunt => hunt.Tracked.Value && hunt.State != TrackedHuntState.Killed);

        private static bool AllTargetsKilled(HuntData data)
        {
            var targetInfo = data.TargetInfo;

            for (var i = 0; i < 5; ++i)
            {
                if (targetInfo[i]?.NeededKills != data.KillCounts[i])
                    return false;
            }
            
            return true;
        }
    }

    private class ModuleTodoComponent : ITodoComponent
    {
        public IModule ParentModule { get; }
        public CompletionType CompletionType => CompletionType.Daily;
        public bool HasLongLabel => true;

        public ModuleTodoComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public string GetShortTaskLabel() => Strings.Module.HuntMarks.DailyLabel;

        public string GetLongTaskLabel()
        {
            var strings = Settings.TrackedHunts
                .Where(hunt => hunt.Tracked.Value && hunt.State != TrackedHuntState.Killed)
                .Select(hunt => hunt.HuntType.GetLabel());

            return string.Join("\n", strings);
        }
    }


    private class ModuleTimerComponent : ITimerComponent
    {
        public IModule ParentModule { get; }

        public ModuleTimerComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public TimeSpan GetTimerPeriod() => TimeSpan.FromDays(1);

        public DateTime GetNextReset() => Time.NextDailyReset();
    }
}