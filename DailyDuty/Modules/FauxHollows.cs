﻿using System;
using System.Numerics;
using DailyDuty.Addons;
using DailyDuty.Configuration.Components;
using DailyDuty.Interfaces;
using DailyDuty.Localization;
using DailyDuty.UserInterface.Components;
using DailyDuty.UserInterface.Components.InfoBox;
using DailyDuty.Utilities;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using ImGuiNET;

namespace DailyDuty.Modules;

public class FauxHollowsSettings : GenericSettings
{
    public Setting<bool> EnableClickableLink = new(true);
    public Setting<bool> IncludeRetelling = new(true);
    public int FauxHollowsCompleted;
}

internal class FauxHollows : IModule
{
    public ModuleName Name => ModuleName.UnrealTrial;
    public IConfigurationComponent ConfigurationComponent { get; }
    public IStatusComponent StatusComponent { get; }
    public ILogicComponent LogicComponent { get; }
    public ITodoComponent TodoComponent { get; }
    public ITimerComponent TimerComponent { get; }

    private static FauxHollowsSettings Settings => Service.ConfigurationManager.CharacterConfiguration.FauxHollows;
    public GenericSettings GenericSettings => Settings;

    public FauxHollows()
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
                .AddTitle(Strings.Module.FauxHollows.Retelling)
                .AddConfigCheckbox(Strings.Module.FauxHollows.Retelling, Settings.IncludeRetelling, Strings.Module.FauxHollows.RetellingHelp)
                .Draw();

            InfoBox.Instance
                .AddTitle(Strings.Module.FauxHollows.ClickableLinkLabel)
                .AddString(Strings.Module.FauxHollows.ClickableLink)
                .AddConfigCheckbox(Strings.Common.Enabled, Settings.EnableClickableLink)
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

            InfoBox.Instance
                .AddTitle(Strings.Common.Target)
                .BeginTable()
                .BeginRow()
                .AddString(Strings.Module.FauxHollows.Completions)
                .AddString($"{Settings.FauxHollowsCompleted} / {GetRequiredCompletionCount()}", ParentModule.LogicComponent.GetModuleStatus().GetStatusColor())
                .EndRow()
                .EndTable()
                .Draw();

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
                Settings.FauxHollowsCompleted = GetRequiredCompletionCount();
            }
            ImGui.EndDisabled();
        }

        private static int GetRequiredCompletionCount()
        {
            return Settings.IncludeRetelling.Value ? 2 : 1;
        }
    }

    private class ModuleLogicComponent : ILogicComponent
    {
        public IModule ParentModule { get; }
        public DalamudLinkPayload? DalamudLinkPayload { get; }

        public ModuleLogicComponent(IModule parentModule)
        {
            ParentModule = parentModule;

            DalamudLinkPayload = Service.PayloadManager.AddChatLink(ChatPayloads.OpenPartyFinder, OpenPartyFinder);

            Service.AddonManager.Get<WeeklyPuzzleAddon>().Show += OnShow;
        }
        
        public void Dispose()
        {
            Service.AddonManager.Get<WeeklyPuzzleAddon>().Show -= OnShow;
        }

        private void OnShow(object? sender, IntPtr e)
        {
            if (!Settings.Enabled.Value) return;

            Settings.FauxHollowsCompleted += 1;
            Service.ConfigurationManager.Save();
        }

        private void OpenPartyFinder(uint arg1, SeString arg2)
        {
            Service.ChatManager.SendCommandUnsafe("partyfinder");
        }

        public string GetStatusMessage() => $"{Strings.Module.FauxHollows.TrialAvailable}";

        public DateTime GetNextReset() => Time.NextWeeklyReset();

        public void DoReset()
        {
            Settings.FauxHollowsCompleted = 0;
        }

        public ModuleStatus GetModuleStatus() => Settings.FauxHollowsCompleted >= GetRequiredCompletionCount() ? ModuleStatus.Complete : ModuleStatus.Incomplete;

        private static int GetRequiredCompletionCount()
        {
            return Settings.IncludeRetelling.Value ? 2 : 1;
        }
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

        public string GetShortTaskLabel() => Strings.Module.FauxHollows.Label;

        public string GetLongTaskLabel() => Strings.Module.FauxHollows.Label;
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