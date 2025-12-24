using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace SamplePlugin;

public class MachinistRotation : IDisposable
{
    // Machinist Action IDs
    public const uint SplitShot = 2866;
    public const uint SlugShot = 2868;
    public const uint CleanShot = 2873;
    public const uint HeatedSplitShot = 7411;
    public const uint HeatedSlugShot = 7412;
    public const uint HeatedCleanShot = 7413;
    public const uint Drill = 16498;
    public const uint AirAnchor = 16500;
    public const uint ChainSaw = 25788;
    public const uint Excavator = 36981;
    public const uint FullMetalField = 36982;
    public const uint GaussRound = 2874;
    public const uint Ricochet = 2890;
    public const uint DoubleCheck = 36979;
    public const uint Checkmate = 36980;
    public const uint Hypercharge = 17209;
    public const uint HeatBlast = 7410;
    public const uint BlazingShot = 36978;
    public const uint Wildfire = 2878;
    public const uint Reassemble = 2876;
    public const uint BarrelStabilizer = 7414;
    public const uint RookAutoturret = 2864;
    public const uint AutomatonQueen = 16501;

    // Reference to settings
    private MachinistSettings Settings => Plugin.PluginInterface.GetPluginConfig() is Configuration config
        ? config.Machinist
        : new MachinistSettings();

    // Rotation state
    public bool IsEnabled { get; set; }
    public bool IsInOpener { get; private set; }
    public int OpenerStep { get; private set; }
    public string LastAction { get; private set; } = "";
    public string NextAction { get; private set; } = "";
    public string RotationStatus { get; private set; } = "Idle";
    public bool IsTargetingSuppressed { get; private set; }
    private DateTime openerStartTime = DateTime.MinValue;
    private DateTime openerStepStart = DateTime.MinValue;

    // Game gauge data
    public int CurrentHeat { get; private set; }
    public int CurrentBattery { get; private set; }
    public bool IsOverheated { get; private set; }
    public byte OverheatStacks { get; private set; }

    // Combo state from game
    private float comboTimer;
    private uint lastComboAction;

    // Targeting state
    private bool targetLockout;

    // Timing for weaving
    private DateTime lastGcdTime = DateTime.MinValue;
    private DateTime lastActionTime = DateTime.MinValue;
    private const float GCD = 2.5f;
    private const float WeaveWindow = 0.7f;

    // Opener sequence
    private readonly List<(uint ActionId, bool IsOGcd, string Name)> openerSequence =
    [
        (Reassemble, true, "Reassemble"),
        (AirAnchor, false, "Air Anchor"),
        (GaussRound, true, "Gauss Round"),
        (Ricochet, true, "Ricochet"),
        (Drill, false, "Drill"),
        (BarrelStabilizer, true, "Barrel Stabilizer"),
        (ChainSaw, false, "Chain Saw"),
        (GaussRound, true, "Gauss Round"),
        (Ricochet, true, "Ricochet"),
        (Excavator, false, "Excavator"),
        (Reassemble, true, "Reassemble"),
        (FullMetalField, false, "Full Metal Field"),
        (GaussRound, true, "Gauss Round"),
        (Ricochet, true, "Ricochet"),
        (Hypercharge, true, "Hypercharge"),
        (HeatBlast, false, "Heat Blast"),
        (Wildfire, true, "Wildfire"),
        (HeatBlast, false, "Heat Blast"),
        (GaussRound, true, "Gauss Round"),
        (HeatBlast, false, "Heat Blast"),
        (Ricochet, true, "Ricochet"),
        (HeatBlast, false, "Heat Blast"),
        (GaussRound, true, "Gauss Round"),
        (HeatBlast, false, "Heat Blast"),
        (Ricochet, true, "Ricochet"),
    ];

    // Damage ability checks for readiness validation
    private readonly (uint ActionId, string Name)[] damageActions =
    [
        (Drill, "Drill"),
        (AirAnchor, "Air Anchor"),
        (ChainSaw, "Chain Saw"),
        (Excavator, "Excavator"),
        (FullMetalField, "Full Metal Field"),
        (Hypercharge, "Hypercharge"),
        (HeatBlast, "Heat Blast"),
        (BlazingShot, "Blazing Shot"),
        (Wildfire, "Wildfire"),
        (GaussRound, "Gauss Round"),
        (DoubleCheck, "Double Check"),
        (Ricochet, "Ricochet"),
        (Checkmate, "Checkmate"),
        (Reassemble, "Reassemble"),
        (BarrelStabilizer, "Barrel Stabilizer"),
        (HeatedSplitShot, "Heated Split Shot"),
        (HeatedSlugShot, "Heated Slug Shot"),
        (HeatedCleanShot, "Heated Clean Shot"),
        (SplitShot, "Split Shot"),
        (SlugShot, "Slug Shot"),
        (CleanShot, "Clean Shot"),
        (RookAutoturret, "Rook Autoturret"),
        (AutomatonQueen, "Automaton Queen"),
    ];

    public MachinistRotation()
    {
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }

    public void OnComboButtonPressed()
    {
        StartRotation(false);
    }

    public bool StartRotation(bool allowOpener = true, bool allowAutoTarget = true)
    {
        if (IsEnabled)
            return true;

        targetLockout = false;
        IsTargetingSuppressed = false;

        if (!EnsureTarget(allowAutoTarget))
            return false;

        UpdateDamageReadinessPreview();

        IsEnabled = true;

        if (allowOpener && Settings.UseOpener)
        {
            StartOpener();
        }
        else
        {
            ResetOpener();
            RotationStatus = "Running";
        }

        // Ensure we are not throttled on start
        lastActionTime = DateTime.Now.AddSeconds(-2);
        lastGcdTime = DateTime.Now.AddSeconds(-2);

        Plugin.Log.Information("Auto-rotation started");
        return true;
    }

    public void StartOpener()
    {
        IsInOpener = true;
        OpenerStep = 0;
        RotationStatus = "Opener";
        openerStartTime = DateTime.Now;
        openerStepStart = DateTime.Now;
        Plugin.Log.Information("Opener started");
    }

    public void StopRotation(bool clearTarget = false)
    {
        IsEnabled = false;
        IsInOpener = false;
        OpenerStep = 0;

        targetLockout = clearTarget;
        IsTargetingSuppressed = clearTarget;

        if (clearTarget)
        {
            Plugin.TargetManager.Target = null;
            RotationStatus = "Stopped (target cleared)";
            NextAction = "Target cleared";
        }
        else
        {
            RotationStatus = "Stopped";
        }

        Plugin.Log.Information(clearTarget
            ? "Rotation stopped and target cleared"
            : "Rotation stopped");
    }

    public bool ContinueRotation(bool allowAutoTarget)
    {
        targetLockout = false;
        IsTargetingSuppressed = false;

        if (!EnsureTarget(allowAutoTarget))
            return false;

        UpdateDamageReadinessPreview();

        IsEnabled = true;
        ResetOpener();

        // Ensure we are not throttled on resume
        lastActionTime = DateTime.Now.AddSeconds(-2);
        lastGcdTime = DateTime.Now.AddSeconds(-2);

        RotationStatus = "Running";
        Plugin.Log.Information("Auto-rotation continued");
        return true;
    }

    public void ResetOpener()
    {
        IsInOpener = false;
        OpenerStep = 0;
        RotationStatus = IsEnabled ? "Running" : "Idle";
        openerStartTime = DateTime.MinValue;
        openerStepStart = DateTime.MinValue;
    }

    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        if (!IsEnabled)
            return;

        // Read game state
        ReadGameState();

        if (!EnsureTarget(Settings.AutoTargetNearest))
            return;

        var target = Plugin.TargetManager.Target as IBattleChara;
        if (target == null)
            return;

        UpdateDamageReadinessPreview();

        // Throttle to prevent spam (tighter window for responsiveness)
        var timeSinceAction = (float)(DateTime.Now - lastActionTime).TotalSeconds;
        if (timeSinceAction < 0.03f)
            return;

        if (IsInOpener)
            ExecuteOpener(target.GameObjectId);
        else
            ExecuteRotation(target.GameObjectId);
    }

    public bool TargetNearestEnemy(out IBattleChara? target)
    {
        target = FindNearestEnemy();
        if (target != null)
        {
            Plugin.TargetManager.Target = target;
            return true;
        }

        return false;
    }

    private bool EnsureTarget(bool allowAutoTarget)
    {
        if (targetLockout)
        {
            RotationStatus = "Targeting paused";
            NextAction = "Press Continue Combo";
            return false;
        }

        var target = Plugin.TargetManager.Target;

        if (IsValidTarget(target))
            return true;

        if (allowAutoTarget && TargetNearestEnemy(out var newTarget) && newTarget != null)
        {
            RotationStatus = $"Targeted {newTarget.Name}";
            return true;
        }

        RotationStatus = "No Target";
        NextAction = "Select target...";
        return false;
    }

    private IBattleChara? FindNearestEnemy()
    {
        var localPlayer = Plugin.ClientState.LocalPlayer;
        if (localPlayer == null)
            return null;

        return Plugin.ObjectTable
            .OfType<IBattleNpc>()
            .Where(IsTargetableEnemy)
            .OrderBy(o => Vector3.Distance(localPlayer.Position, o.Position))
            .FirstOrDefault();
    }

    private static bool IsTargetableEnemy(IBattleNpc npc)
    {
        if (npc.BattleNpcKind != BattleNpcSubKind.Enemy)
            return false;

        return IsValidTarget(npc);
    }

    private static bool IsValidTarget(IGameObject? target)
    {
        if (target is not IBattleChara battleTarget)
            return false;

        if (battleTarget.IsDead || !battleTarget.IsTargetable)
            return false;

        if (battleTarget is IBattleNpc npc)
            return npc.BattleNpcKind == BattleNpcSubKind.Enemy;

        return false;
    }

    private unsafe void ReadGameState()
    {
        try
        {
            // Read job gauge
            var jobGauge = JobGaugeManager.Instance();
            if (jobGauge != null)
            {
                var gaugeData = (byte*)jobGauge;
                CurrentHeat = gaugeData[8];
                CurrentBattery = gaugeData[10];
                IsOverheated = gaugeData[12] != 0;
                OverheatStacks = gaugeData[14];
            }

            // Read combo state
            var comboPtr = (ComboDetail*)ActionManager.Instance();
            if (comboPtr != null)
            {
                comboTimer = comboPtr->Timer;
                lastComboAction = comboPtr->Action;
            }
        }
        catch { }
    }

    private unsafe void ExecuteOpener(ulong targetId)
    {
        if (OpenerStep >= openerSequence.Count)
        {
            IsInOpener = false;
            RotationStatus = "Running";
            return;
        }

        // Fail-safe: exit opener if it takes too long (eg, missing abilities)
        if (openerStartTime != DateTime.MinValue && DateTime.Now - openerStartTime > TimeSpan.FromSeconds(15))
        {
            Plugin.Log.Warning("Opener timeout reached - switching to normal rotation");
            ResetOpener();
            return;
        }

        var (actionId, isOGcd, actionName) = openerSequence[OpenerStep];

        if (!IsActionEnabledInSettings(actionId))
        {
            OpenerStep++;
            openerStepStart = DateTime.Now;
            return;
        }

        NextAction = $"[Opener] {actionName}";

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return;

        // Skip opener steps that stay unusable for too long (eg, cooldown/locked)
        openerStepStart = openerStepStart == DateTime.MinValue ? DateTime.Now : openerStepStart;
        var stuckOnStep = DateTime.Now - openerStepStart > TimeSpan.FromSeconds(3);
        var actionStatus = actionManager->GetActionStatus(ActionType.Action, actionId);
        var actionUsable = actionStatus == 0;

        if (!actionUsable && stuckOnStep)
        {
            Plugin.Log.Warning($"Skipping opener action {actionName} - unusable for 3+ seconds (status {actionStatus})");
            OpenerStep++;
            openerStepStart = DateTime.Now;
            return;
        }

        // Check if ready and use
        if (actionUsable)
        {
            if (actionManager->UseAction(ActionType.Action, actionId, targetId))
            {
                LastAction = actionName;
                lastActionTime = DateTime.Now;
                if (!isOGcd) lastGcdTime = DateTime.Now;
                OpenerStep++;
                openerStepStart = DateTime.Now;
                RotationStatus = $"Opener {OpenerStep}/{openerSequence.Count}";
                Plugin.Log.Information($"Opener: {actionName}");
            }
        }
    }

    private unsafe void ExecuteRotation(ulong targetId)
    {
        var actionManager = ActionManager.Instance();
        if (actionManager == null) return;

        RotationStatus = $"Heat:{CurrentHeat} Batt:{CurrentBattery}";

        var timeSinceGcd = (float)(DateTime.Now - lastGcdTime).TotalSeconds;
        bool canWeave = timeSinceGcd >= WeaveWindow && timeSinceGcd < (GCD - 0.5f);

        // === PRIORITY 1: Heat Blast during Overheated ===
        if (IsOverheated)
        {
            // Weave Wildfire after first Heat Blast (skip if burst disabled)
            if (canWeave && IsReadyAndEnabled(actionManager, Wildfire))
            {
                if (UseAction(actionManager, Wildfire, targetId, "Wildfire", true))
                    return;
            }

            // Weave Gauss/Ricochet between Heat Blasts
            if (canWeave)
            {
                if (IsReadyAndEnabled(actionManager, GaussRound))
                    if (UseAction(actionManager, GaussRound, targetId, "Gauss Round", true)) return;
                if (IsReadyAndEnabled(actionManager, DoubleCheck))
                    if (UseAction(actionManager, DoubleCheck, targetId, "Double Check", true)) return;
                if (IsReadyAndEnabled(actionManager, Ricochet))
                    if (UseAction(actionManager, Ricochet, targetId, "Ricochet", true)) return;
                if (IsReadyAndEnabled(actionManager, Checkmate))
                    if (UseAction(actionManager, Checkmate, targetId, "Checkmate", true)) return;
            }

            // Use Heat Blast / Blazing Shot
            if (IsReadyAndEnabled(actionManager, BlazingShot))
                if (UseAction(actionManager, BlazingShot, targetId, "Blazing Shot", false)) return;
            if (IsReadyAndEnabled(actionManager, HeatBlast))
                if (UseAction(actionManager, HeatBlast, targetId, "Heat Blast", false)) return;
            return;
        }

        // === PRIORITY 2: Weave oGCDs ===
        if (canWeave)
        {
            // Barrel Stabilizer - use on cooldown
            if (IsReadyAndEnabled(actionManager, BarrelStabilizer))
                if (UseAction(actionManager, BarrelStabilizer, targetId, "Barrel Stabilizer", true)) return;

            // Reassemble before tools
            if (IsReadyAndEnabled(actionManager, Reassemble))
            {
                bool toolReady = IsReadyAndEnabled(actionManager, Drill) ||
                                IsReadyAndEnabled(actionManager, AirAnchor) ||
                                IsReadyAndEnabled(actionManager, ChainSaw);
                if (toolReady)
                    if (UseAction(actionManager, Reassemble, targetId, "Reassemble", true)) return;
            }

            // Hypercharge - NEVER OVERCAP HEAT (skip if burst disabled, unless overcapping)
            if (IsReadyAndEnabled(actionManager, Hypercharge))
            {
                bool mustUse = CurrentHeat >= 100; // Prevent overcap - use even if burst disabled
                bool shouldUse = CurrentHeat >= 50 && !Settings.DisableBurstPhase;

                if (mustUse || shouldUse)
                    if (UseAction(actionManager, Hypercharge, targetId, "Hypercharge", true)) return;
            }

            // Gauss Round / Ricochet - dump charges
            if (IsReadyAndEnabled(actionManager, GaussRound))
                if (UseAction(actionManager, GaussRound, targetId, "Gauss Round", true)) return;
            if (IsReadyAndEnabled(actionManager, DoubleCheck))
                if (UseAction(actionManager, DoubleCheck, targetId, "Double Check", true)) return;
            if (IsReadyAndEnabled(actionManager, Ricochet))
                if (UseAction(actionManager, Ricochet, targetId, "Ricochet", true)) return;
            if (IsReadyAndEnabled(actionManager, Checkmate))
                if (UseAction(actionManager, Checkmate, targetId, "Checkmate", true)) return;
        }

        // === PRIORITY 3: Full Metal Field (proc from Barrel Stabilizer) ===
        if (IsReadyAndEnabled(actionManager, FullMetalField))
            if (UseAction(actionManager, FullMetalField, targetId, "Full Metal Field", false)) return;

        // === PRIORITY 4: Burst Tools ===
        // Air Anchor > Drill > Chain Saw > Excavator
        if (IsReadyAndEnabled(actionManager, AirAnchor))
            if (UseAction(actionManager, AirAnchor, targetId, "Air Anchor", false)) return;

        if (IsReadyAndEnabled(actionManager, Drill))
            if (UseAction(actionManager, Drill, targetId, "Drill", false)) return;

        if (IsReadyAndEnabled(actionManager, ChainSaw))
            if (UseAction(actionManager, ChainSaw, targetId, "Chain Saw", false)) return;

        if (IsReadyAndEnabled(actionManager, Excavator))
            if (UseAction(actionManager, Excavator, targetId, "Excavator", false)) return;

        // === PRIORITY 5: 1-2-3 Combo ===
        // Use game's combo state to determine next action
        if (comboTimer > 0)
        {
            // Combo is active - continue it
            if (lastComboAction == SplitShot || lastComboAction == HeatedSplitShot)
            {
                // Next is Slug Shot
                if (IsReadyAndEnabled(actionManager, HeatedSlugShot))
                    if (UseAction(actionManager, HeatedSlugShot, targetId, "Heated Slug Shot", false)) return;
                if (IsReadyAndEnabled(actionManager, SlugShot))
                    if (UseAction(actionManager, SlugShot, targetId, "Slug Shot", false)) return;
            }
            else if (lastComboAction == SlugShot || lastComboAction == HeatedSlugShot)
            {
                // Next is Clean Shot
                if (IsReadyAndEnabled(actionManager, HeatedCleanShot))
                    if (UseAction(actionManager, HeatedCleanShot, targetId, "Heated Clean Shot", false)) return;
                if (IsReadyAndEnabled(actionManager, CleanShot))
                    if (UseAction(actionManager, CleanShot, targetId, "Clean Shot", false)) return;
            }
        }

        // Start new combo with Split Shot
        if (IsReadyAndEnabled(actionManager, HeatedSplitShot))
            if (UseAction(actionManager, HeatedSplitShot, targetId, "Heated Split Shot", false)) return;
        if (IsReadyAndEnabled(actionManager, SplitShot))
            UseAction(actionManager, SplitShot, targetId, "Split Shot", false);
    }

    private unsafe bool IsReadyAndEnabled(ActionManager* am, uint actionId) =>
        IsActionEnabledInSettings(actionId) && am->GetActionStatus(ActionType.Action, actionId) == 0;

    private unsafe void UpdateDamageReadinessPreview()
    {
        if (IsInOpener)
            return;

        var actionManager = ActionManager.Instance();
        if (actionManager == null)
            return;

        if (HasReadyDamageAbility(actionManager, out var readyAction))
        {
            if (string.IsNullOrEmpty(NextAction))
                NextAction = readyAction;
            RotationStatus = IsEnabled ? RotationStatus : "Ready";
        }
        else
        {
            RotationStatus = "Waiting for damage abilities";
            NextAction = "All damage actions cooling down";
        }
    }

    private unsafe bool UseAction(ActionManager* am, uint actionId, ulong targetId, string name, bool isOGcd)
    {
        if (!IsActionEnabledInSettings(actionId))
            return false;

        if (am->UseAction(ActionType.Action, actionId, targetId))
        {
            LastAction = name;
            NextAction = name;
            lastActionTime = DateTime.Now;
            if (!isOGcd) lastGcdTime = DateTime.Now;
            Plugin.Log.Information($"{(isOGcd ? "oGCD" : "GCD")}: {name}");
            return true;
        }
        return false;
    }

    private bool IsActionEnabledInSettings(uint actionId)
    {
        // Check individual ability toggle first
        var abilityEnabled = actionId switch
        {
            Drill => Settings.UseDrill,
            AirAnchor => Settings.UseAirAnchor,
            ChainSaw => Settings.UseChainSaw,
            Excavator => Settings.UseExcavator,
            FullMetalField => Settings.UseFullMetalField,
            Reassemble => Settings.UseReassemble,
            BarrelStabilizer => Settings.UseBarrelStabilizer,
            Hypercharge => Settings.UseHypercharge,
            Wildfire => Settings.UseWildfire,
            GaussRound or DoubleCheck => Settings.UseGaussRound,
            Ricochet or Checkmate => Settings.UseRicochet,
            HeatBlast or BlazingShot => Settings.UseHeatBlast,
            _ => true
        };

        // Check burst phase override for Hypercharge/Wildfire
        if (Settings.DisableBurstPhase && (actionId == Hypercharge || actionId == Wildfire))
            return false;

        return abilityEnabled;
    }

    private unsafe bool HasReadyDamageAbility(ActionManager* am, out string readyAction)
    {
        foreach (var (actionId, actionName) in damageActions)
        {
            if (!IsActionEnabledInSettings(actionId))
                continue;

            if (am->GetActionStatus(ActionType.Action, actionId) == 0)
            {
                readyAction = actionName;
                return true;
            }
        }

        readyAction = string.Empty;
        return false;
    }

    public string GetNextActionPreview()
    {
        if (!IsEnabled) return "Disabled";
        if (IsInOpener && OpenerStep < openerSequence.Count)
            return $"[Opener] {openerSequence[OpenerStep].Name}";
        return string.IsNullOrEmpty(NextAction) ? "Ready" : NextAction;
    }

    public int GetEnabledAbilityCount()
    {
        int count = 0;
        if (Settings.UseDrill) count++;
        if (Settings.UseAirAnchor) count++;
        if (Settings.UseChainSaw) count++;
        if (Settings.UseExcavator) count++;
        if (Settings.UseFullMetalField) count++;
        if (Settings.UseReassemble) count++;
        if (Settings.UseBarrelStabilizer) count++;
        if (Settings.UseHypercharge) count++;
        if (Settings.UseHeatBlast) count++;
        if (Settings.UseWildfire) count++;
        if (Settings.UseGaussRound) count++;
        if (Settings.UseRicochet) count++;
        return count;
    }
}

// Structure to read combo state from game memory
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
internal struct ComboDetail
{
    [System.Runtime.InteropServices.FieldOffset(0x60)] public float Timer;
    [System.Runtime.InteropServices.FieldOffset(0x64)] public uint Action;
}
