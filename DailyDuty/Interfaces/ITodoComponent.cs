﻿using DailyDuty.Configuration.Components;
using DailyDuty.Localization;
using DailyDuty.UserInterface.Components.InfoBox;

namespace DailyDuty.Interfaces;

public interface ITodoComponent : IInfoBoxTableConfigurationRow
{
    IModule ParentModule { get; }
    CompletionType CompletionType { get; }
    bool HasLongLabel { get; }
    string GetShortTaskLabel();
    string GetLongTaskLabel();

    void IInfoBoxTableConfigurationRow.GetConfigurationRow(InfoBoxTable owner)
    {
        if (HasLongLabel)
        {
            owner
                .BeginRow()
                .AddConfigCheckbox(ParentModule.Name.GetTranslatedString(), ParentModule.GenericSettings.TodoTaskEnabled)
                .AddConfigCheckbox(Strings.UserInterface.Todo.UseLongLabel, ParentModule.GenericSettings.TodoUseLongLabel)
                .EndRow();
        }
        else
        {
            owner
                .BeginRow()
                .AddConfigCheckbox(ParentModule.Name.GetTranslatedString(), ParentModule.GenericSettings.TodoTaskEnabled)
                .EndRow();
        }
    }
}