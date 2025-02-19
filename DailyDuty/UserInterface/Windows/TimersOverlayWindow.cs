﻿using DailyDuty.Configuration.OverlaySettings;
using DailyDuty.Configuration;
using DailyDuty.Interfaces;
using DailyDuty.Utilities;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DailyDuty.Configuration.Components;
using DailyDuty.Localization;
using Dalamud.Utility;

namespace DailyDuty.UserInterface.Windows;

internal class TimersOverlayWindow : Window, IDisposable
{
    private static TimersOverlaySettings Settings => Service.ConfigurationManager.CharacterConfiguration.TimersOverlay;

    private List<ITimerComponent> trackedTasks = new();

    public TimersOverlayWindow() : base($"###DailyDutyTimersOverlayWindow+{Service.ConfigurationManager.CharacterConfiguration.CharacterData.Name}")
    {
        Service.ConfigurationManager.OnCharacterDataAvailable += UpdateWindowTitle;

        Flags |= ImGuiWindowFlags.NoBringToFrontOnFocus;
        Flags |= ImGuiWindowFlags.NoFocusOnAppearing;
        Flags |= ImGuiWindowFlags.NoNavFocus;
    }

    public void Dispose()
    {
        Service.ConfigurationManager.OnCharacterDataAvailable -= UpdateWindowTitle;
    }

    private void UpdateWindowTitle(object? sender, CharacterConfiguration e)
    {
        WindowName = $"###DailyDutyTimersOverlayWindow+{e.CharacterData.Name}";
    }

    public override void PreOpenCheck()
    {
        if (Settings.Enabled.Value) IsOpen = true;
        if (!Settings.Enabled.Value) IsOpen = false;
        if (!Service.ConfigurationManager.CharacterDataLoaded) IsOpen = false;
        if (Service.ClientState.IsPvP) IsOpen = false;
        if (Condition.InCutsceneOrQuestEvent()) IsOpen = false;
        if (Condition.IsBoundByDuty() && Settings.HideWhileInDuty.Value) IsOpen = false;

        trackedTasks = GetTrackedTasks();

        if (Settings.HideCompleted.Value && trackedTasks.Any())
        {
            trackedTasks.RemoveAll(module => module.ParentModule.LogicComponent.GetModuleStatus() == ModuleStatus.Complete);

            if (!trackedTasks.Any()) IsOpen = false;
        }
    }

    public override void PreDraw()
    {
        var bgColor = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
        ImGui.PushStyleColor(ImGuiCol.WindowBg, bgColor with {W = Settings.Opacity.Value});

        var borderColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Border];
        ImGui.PushStyleColor(ImGuiCol.Border, borderColor with {W = Settings.Opacity.Value});

        if (Settings.AutoResize.Value)
        {
            Flags = DrawFlags.AutoResize;
            Flags |= Settings.LockWindowPosition.Value ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None;
        }
        else
        {
            Flags = DrawFlags.ManualSize;
            Flags |= Settings.LockWindowPosition.Value ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None;
        }
    }

    public override void Draw()
    {
        if (trackedTasks.Count == 0)
        {
            ImGui.TextColored(Colors.Orange, Strings.UserInterface.Timers.NoTimersEnabledWarning);
        }
        else
        {
            DrawAllTimers(trackedTasks);
        }
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
        ImGui.PopStyleColor();
    }

    private List<ITimerComponent> GetTrackedTasks()
    {
        var tasks = Service.ModuleManager.GetTimerComponents().ToList();

        tasks.RemoveAll(module => !module.ParentModule.GenericSettings.Enabled.Value);
        tasks.RemoveAll(module => !module.ParentModule.GenericSettings.TimerTaskEnabled.Value);

        return tasks;
    }

    private void DrawAllTimers(IEnumerable<ITimerComponent> countdownTimers)
    {
        var timers = countdownTimers.ToArray();
        var totalWidth = (int)ImGui.GetContentRegionAvail().X;
        var spacing = (int)ImGui.GetStyle().ItemSpacing.X;
        var i = 0;

        while(i < timers.Length)
        {
            var width = 0;
            var count = 0;
            var resizeCount = 0;

            for(var j = i; j < timers.Length; j++)
            {
                var timer = timers[j];
                var timerSettings = timer.ParentModule.GenericSettings.TimerSettings;
                var w = timerSettings.Size.Value + (count > 0 ? spacing : 0);
                if(count > 0 && width + w > totalWidth)
                    break;

                count++;
                width += w;
                if(timerSettings.StretchToFit.Value)
                    resizeCount++;
            }

            var add = resizeCount > 0 ? ((totalWidth - width) / resizeCount) : 0;

            for(var j = i; j < i + count; j++)
            {
                var timer = timers[j];
                var timerSettings = timer.ParentModule.GenericSettings.TimerSettings;

                if(timerSettings.StretchToFit.Value)
                {
                    timerSettings.Size.Value += add;
                    DrawTimer(timer);
                    timerSettings.Size.Value -= add;
                }
                else
                {
                    DrawTimer(timer);
                }

                if(j < i + count - 1)
                    ImGui.SameLine();
            }

            i += count;
        }
    }

    private void DrawTimer(ITimerComponent timer)
    {
        var timerSettings = timer.ParentModule.GenericSettings.TimerSettings;

        ImGui.PushStyleColor(ImGuiCol.FrameBg, timerSettings.BackgroundColor.Value);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, timerSettings.ForegroundColor.Value);

        ImGui.BeginGroup();

        var remainingTime = timer.GetNextReset() - DateTime.UtcNow;
        var deltaTime = 1.0f - (float)(remainingTime / timer.GetTimerPeriod());
        var cursorStart = ImGui.GetCursorPos();
        ImGui.ProgressBar(deltaTime, new Vector2(timerSettings.Size.Value, 20), "");

        DrawLabel(timer, timerSettings, cursorStart);

        DrawTime(timerSettings, remainingTime, cursorStart);

        ImGui.EndGroup();

        ImGui.PopStyleColor();
        ImGui.PopStyleColor();
    }

    private static void DrawTime(TimerSettings timerSettings, TimeSpan remainingTime, Vector2 cursorStart)
    {
        if (!timerSettings.HideTime.Value)
        {
            if (remainingTime >= TimeSpan.Zero)
            {
                var timeText = Time.FormatTimespan(remainingTime, timerSettings.TimerStyle.Value);
                var timeTextSize = ImGui.CalcTextSize(timeText);
                ImGui.SetCursorPos(cursorStart with {X = cursorStart.X + timerSettings.Size.Value - 5.0f - timeTextSize.X});
                ImGui.TextColored(timerSettings.TimeColor.Value, timeText);
            }
            else
            {
                var timeTextSize = ImGui.CalcTextSize(Strings.UserInterface.Timers.AvailableNow);
                ImGui.SetCursorPos(cursorStart with {X = cursorStart.X + timerSettings.Size.Value - 5.0f - timeTextSize.X});
                ImGui.TextColored(timerSettings.TimeColor.Value, Strings.UserInterface.Timers.AvailableNow);
            }
        }
    }

    private static void DrawLabel(ITimerComponent timer, TimerSettings timerSettings, Vector2 cursorStart)
    {
        if (!timerSettings.HideLabel.Value)
        {
            ImGui.SetCursorPos(cursorStart with {X = cursorStart.X + 5.0f});

            if (timerSettings.UseCustomName.Value)
            {
                ImGui.TextColored(timerSettings.TextColor.Value, timerSettings.CustomName.Value);
            }
            else
            {
                ImGui.TextColored(timerSettings.TextColor.Value, timer.ParentModule.Name.GetTranslatedString());
            }
        }
    }
}
