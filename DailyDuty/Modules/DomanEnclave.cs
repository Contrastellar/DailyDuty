﻿using System;
using DailyDuty.Configuration.Components;
using DailyDuty.DataStructures;
using DailyDuty.Interfaces;
using DailyDuty.Localization;
using DailyDuty.System;
using DailyDuty.UserInterface.Components;
using DailyDuty.UserInterface.Components.InfoBox;
using DailyDuty.Utilities;
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility.Signatures;

namespace DailyDuty.Modules;

internal class DomanEnclaveSettings : GenericSettings
{
    public int DonatedThisWeek;
    public int WeeklyAllowance;

    public Setting<bool> EnableClickableLink = new(true);
}

internal class DomanEnclave : IModule
{
    public ModuleName Name => ModuleName.DomanEnclave;
    public IConfigurationComponent ConfigurationComponent { get; }
    public IStatusComponent StatusComponent { get; }
    public ILogicComponent LogicComponent { get; }
    public ITodoComponent TodoComponent { get; }
    public ITimerComponent TimerComponent { get; }

    private static DomanEnclaveSettings Settings => Service.ConfigurationManager.CharacterConfiguration.DomanEnclave;
    public GenericSettings GenericSettings => Settings;

    public DomanEnclave()
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
                .AddTitle(Strings.Module.DomanEnclave.ClickableLinkLabel)
                .AddString(Strings.Module.DomanEnclave.ClickableLink)
                .AddConfigCheckbox(Strings.Module.DomanEnclave.ClickableLinkLabel, Settings.EnableClickableLink)
                .Draw();

            InfoBox.DrawNotificationOptions(this);
        }
    }

    private class ModuleStatusComponent : IStatusComponent
    {
        public IModule ParentModule { get; }

        public ISelectable Selectable =>
            new StatusSelectable(ParentModule, this, ParentModule.LogicComponent.GetModuleStatus);

        public ModuleStatusComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public void Draw()
        {
            if (ParentModule.LogicComponent is not ModuleLogicComponent logicModule) return;

            var moduleStatus = logicModule.GetModuleStatus();

            InfoBox.Instance
                .AddTitle(Strings.Status.Label)
                .BeginTable()
                .BeginRow()
                .AddString(Strings.Status.ModuleStatus)
                .AddString(moduleStatus.GetTranslatedString(), moduleStatus.GetStatusColor())
                .EndRow()
                .BeginRow()
                .AddString(Strings.Module.DomanEnclave.BudgetRemaining)
                .AddString(logicModule.GetRemainingBudget().ToString(), logicModule.GetRemainingBudget() == 0 ? Colors.Green : Colors.Orange)
                .EndRow()
                .BeginRow()
                .AddString(Strings.Module.DomanEnclave.CurrentAllowance)
                .AddString(Settings.WeeklyAllowance.ToString())
                .EndRow()
                .EndTable()
                .Draw();

            if (moduleStatus == ModuleStatus.Unknown)
            {
                InfoBox.Instance
                    .AddTitle(Strings.Module.DomanEnclave.UnknownStatusLabel)
                    .AddString(Strings.Module.DomanEnclave.UnknownStatus, Colors.Orange)
                    .Draw();
            }
        }
    }

    private unsafe class ModuleLogicComponent : ILogicComponent
    {
        public IModule ParentModule { get; }
        public DalamudLinkPayload? DalamudLinkPayload { get; } = Service.TeleportManager.GetPayload(TeleportLocation.DomanEnclave);

        private delegate DomanEnclaveStruct* GetDataDelegate();

        [Signature("E8 ?? ?? ?? ?? 48 85 C0 74 09 0F B6 B8")]
        private readonly GetDataDelegate getDomanEnclaveStruct = null!;

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
            if (!DataAvailable()) return;

            UpdateWeeklyAllowance();
            UpdateDonatedThisWeek();
        }

        public string GetStatusMessage()
        {
            if (GetModuleStatus() == ModuleStatus.Unknown) return Strings.Module.DomanEnclave.UnknownStatus;

            return $"{GetRemainingBudget()} {Strings.Module.DomanEnclave.GilRemaining}";
        }

        public DateTime GetNextReset() => Time.NextWeeklyReset();

        public void DoReset() => Settings.DonatedThisWeek = 0;

        public ModuleStatus GetModuleStatus()
        {
            if (!ModuleInitialized()) return ModuleStatus.Unknown;

            return GetRemainingBudget() == 0 ? ModuleStatus.Complete : ModuleStatus.Incomplete;
        }

        private void UpdateWeeklyAllowance()
        {
            var allowance = GetWeeklyAllowance();

            if (Settings.WeeklyAllowance != allowance)
            {
                Settings.WeeklyAllowance = allowance;
                Service.ConfigurationManager.Save();
            }
        }
        private void UpdateDonatedThisWeek()
        {
            var donatedThisWeek = GetDonatedThisWeek();

            if (Settings.DonatedThisWeek != donatedThisWeek)
            {
                Settings.DonatedThisWeek = donatedThisWeek;
                Service.ConfigurationManager.Save();
            }
        }

        public int GetRemainingBudget() => Settings.WeeklyAllowance - Settings.DonatedThisWeek;
        private ushort GetDonatedThisWeek() => getDomanEnclaveStruct()->Donated;
        private ushort GetWeeklyAllowance() => getDomanEnclaveStruct()->Allowance;
        private bool DataAvailable() => GetWeeklyAllowance() != 0;
        private bool ModuleInitialized() => Settings.WeeklyAllowance != 0;
    }

    private class ModuleTodoComponent : ITodoComponent
    {
        public IModule ParentModule { get; }
        public CompletionType CompletionType => CompletionType.Weekly;
        public bool HasLongLabel => false;

        public ModuleTodoComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public string GetShortTaskLabel() => Strings.Module.DomanEnclave.Label;

        public string GetLongTaskLabel()  => Strings.Module.DomanEnclave.Label;
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