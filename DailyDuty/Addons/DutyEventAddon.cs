﻿using System;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;

namespace DailyDuty.Addons;

internal unsafe class DutyEventAddon : IDisposable
{
    private delegate byte DutyEventDelegate(void* a1, void* a2, ushort* a3);

    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B D9 49 8B F8 41 0F B7 08", DetourName = nameof(ProcessNetworkPacket))]
    private readonly Hook<DutyEventDelegate>? dutyEventHook = null;

    public event EventHandler<uint>? DutyStarted;
    public event EventHandler<uint>? DutyCompleted;
    public event EventHandler<uint>? DutyWipe;
    public event EventHandler<uint>? DutyRecommence;

    public DutyEventAddon()
    {
        SignatureHelper.Initialise(this);

        dutyEventHook?.Enable();
    }

    public void Dispose()
    {
        dutyEventHook?.Dispose();
    }

    private byte ProcessNetworkPacket(void* a1, void* a2, ushort* a3)
    {
        try
        {
            if (!Service.ConfigurationManager.CharacterDataLoaded) return dutyEventHook!.Original(a1, a2, a3);

            var category = *a3;
            var type = *(uint*)(a3 + 4);

            // DirectorUpdate Category
            if (category == 0x6D)
            {
                switch (type)
                {
                    // Duty Commenced
                    case 0x40000001:
                        DutyStarted?.Invoke(this, Service.ClientState.TerritoryType);
                        break;

                    // Party Wipe
                    case 0x40000005:
                        DutyWipe?.Invoke(this, Service.ClientState.TerritoryType);
                        break;

                    // Duty Recommence
                    case 0x40000006:
                        DutyRecommence?.Invoke(this, Service.ClientState.TerritoryType);
                        break;

                    // Duty Completed
                    case 0x40000003:
                        DutyCompleted?.Invoke(this, Service.ClientState.TerritoryType);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, "Failed to get duty status");
        }

        return dutyEventHook!.Original(a1, a2, a3);
    }
}