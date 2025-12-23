using Dalamud.Configuration;
using System;

namespace SamplePlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    // Machinist Rotation Settings
    public MachinistSettings Machinist { get; set; } = new();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

[Serializable]
public class MachinistSettings
{
    // ===== SINGLE TARGET COMBO ABILITIES =====
    // These abilities will be woven into the single-target rotation when enabled

    // Burst GCDs (main damage abilities)
    public bool UseDrill { get; set; } = true;
    public bool UseAirAnchor { get; set; } = true;
    public bool UseChainSaw { get; set; } = true;
    public bool UseExcavator { get; set; } = true;
    public bool UseFullMetalField { get; set; } = true;

    // Buff/Support oGCDs
    public bool UseReassemble { get; set; } = true;
    public bool UseBarrelStabilizer { get; set; } = true;

    // Hypercharge Window
    public bool UseHypercharge { get; set; } = true;
    public bool UseHeatBlast { get; set; } = true;
    public bool UseWildfire { get; set; } = true;

    // Filler oGCDs (weave between GCDs)
    public bool UseGaussRound { get; set; } = true;
    public bool UseRicochet { get; set; } = true;

    // ===== OPENER SETTINGS =====
    public bool UseOpener { get; set; } = true;  // Whether to use opener sequence at start

    // ===== BURST PHASE SETTINGS =====
    public bool DisableBurstPhase { get; set; } = false;  // When true, skips Hypercharge/Wildfire burst windows
}
