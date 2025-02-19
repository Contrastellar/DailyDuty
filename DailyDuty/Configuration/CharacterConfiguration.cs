﻿using System;
using System.IO;
using DailyDuty.Configuration.Components;
using DailyDuty.Configuration.OverlaySettings;
using DailyDuty.Modules;
using DailyDuty.Utilities;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DailyDuty.Configuration;

[Serializable]
internal class CharacterConfiguration
{
    public int Version { get; set; } = 2;

    public CharacterData CharacterData = new();
    public bool HideDisabledModulesInSelectWindow = false;

    public TodoOverlaySettings TodoOverlay = new();
    public TimersOverlaySettings TimersOverlay = new();

    public BeastTribeSettings BeastTribe = new();
    public CustomDeliverySettings CustomDelivery = new();
    public DomanEnclaveSettings DomanEnclave = new();
    public DutyRouletteSettings DutyRoulette = new();
    public FashionReportSettings FashionReport = new();
    public HuntMarksDailySettings HuntMarksDaily = new();
    public HuntMarksWeeklySettings HuntMarksWeekly = new();
    public JumboCactpotSettings JumboCactpot = new();
    public LevequestSettings Levequest = new();
    public MiniCactpotSettings MiniCactpot = new();
    public TreasureMapSettings TreasureMap = new();
    public WondrousTailsSettings WondrousTails = new();
    public ChallengeLogSettings ChallengeLog = new();
    public RaidsNormalSettings RaidsNormal = new();
    public RaidsAllianceSettings RaidsAlliance = new();
    public FauxHollowsSettings FauxHollows = new();
    public GrandCompanySupplySettings GrandCompanySupply = new();
    public GrandCompanyProvisionSettings GrandCompanyProvision = new();

    public void Save()
    {
        if (CharacterData.LocalContentID != 0)
        {
            Log.Verbose($"{DateTime.Now} - {CharacterData.Name} Saved");

            var configFileInfo = GetConfigFileInfo(CharacterData.LocalContentID);

            var serializedContents = JsonConvert.SerializeObject(this, Formatting.Indented);

            var writer = new StreamWriter(configFileInfo.FullName);
            writer.Write(serializedContents);
            writer.Dispose();
        }
        else
        {
            Log.Verbose("Tried to save a config with invalid LocalContentID");
        }
    }

    private static FileInfo GetConfigFileInfo(ulong contentID)
    {
        var pluginConfigDirectory = Service.PluginInterface.ConfigDirectory;

        return new FileInfo(pluginConfigDirectory.FullName + $@"\{contentID}.json");
    }

    public static CharacterConfiguration Load(ulong contentID)
    {
        var configFileInfo = GetConfigFileInfo(contentID);

        if (configFileInfo.Exists)
        {
            var reader = new StreamReader(configFileInfo.FullName);
            var fileText = reader.ReadToEnd();
            reader.Dispose();

            var versionNumber = GetConfigFileVersion(fileText);

            return versionNumber switch
            {
                2 => LoadExistingCharacterConfiguration(contentID, fileText),
                1 => GenerateMigratedCharacterConfiguration(fileText),
                _ => CreateNewCharacterConfiguration()
            };
        }
        else
        {
            return CreateNewCharacterConfiguration();
        }
    }

    private static CharacterConfiguration GenerateMigratedCharacterConfiguration(string fileText)
    {
        CharacterConfiguration migratedConfiguration;

        try
        {
            migratedConfiguration = ConfigMigration.Convert(fileText);
            migratedConfiguration.Save();
        }
        catch (Exception e)
        {
            PluginLog.Warning(e, "Unable to Migrate Configuration, generating new configuration instead.");
            migratedConfiguration = CreateNewCharacterConfiguration();
        }

        return migratedConfiguration;
    }

    private static CharacterConfiguration LoadExistingCharacterConfiguration(ulong contentID, string fileText)
    {
        var loadedCharacterConfiguration = JsonConvert.DeserializeObject<CharacterConfiguration>(fileText);

        if (loadedCharacterConfiguration == null)
        {
            throw new FileLoadException($"Unable to load configuration file for contentID: {contentID}");
        }

        return loadedCharacterConfiguration;
    }

    private static CharacterConfiguration CreateNewCharacterConfiguration()
    {
        var newCharacterConfiguration = new CharacterConfiguration();

        var playerData = Service.ClientState.LocalPlayer;
        var contentId = Service.ClientState.LocalContentId;

        var playerName = playerData?.Name.TextValue ?? "Unknown";
        var playerWorld = playerData?.HomeWorld.GameData?.Name.ToString() ?? "UnknownWorld";

        PluginLog.Information($"Creating new configuration file for: {contentId}, {playerName}, {playerWorld}");
        
        newCharacterConfiguration.CharacterData = new CharacterData()
        {
            Name = playerName,
            LocalContentID = contentId,
            World = playerWorld,
        };

        newCharacterConfiguration.Save();
        return newCharacterConfiguration;
    }

    private static int GetConfigFileVersion(string fileText)
    {
        try
        {
            var json = JObject.Parse(fileText);
            return json.GetValue("Version")?.Value<int>() ?? 0;
        }
        catch (Exception e)
        {
            PluginLog.Warning(e, "Unable to read configuration version.");
            return 0;
        }
    }
}