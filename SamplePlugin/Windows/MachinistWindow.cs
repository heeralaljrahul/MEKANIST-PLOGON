using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace SamplePlugin.Windows;

public class MachinistWindow : Window, IDisposable
{
    private readonly string machinistImagePath;
    private readonly Plugin plugin;
    private readonly MachinistRotation rotation;

    // Manual action tracking
    private string lastActionResult = "";
    private DateTime lastActionTime = DateTime.MinValue;

    public MachinistWindow(Plugin plugin, string machinistImagePath, MachinistRotation rotation)
        : base("Machinist - Single Target Combo##MachinistWindow", ImGuiWindowFlags.NoScrollbar)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(480, 650),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.machinistImagePath = machinistImagePath;
        this.plugin = plugin;
        this.rotation = rotation;
    }

    public void Dispose() { }

    private MachinistSettings Settings => plugin.Configuration.Machinist;

    public override void Draw()
    {
        DrawHeader();
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        using (var child = ImRaii.Child("MachinistContent", Vector2.Zero, false))
        {
            if (child.Success)
            {
                DrawComboStartSection();
                ImGuiHelpers.ScaledDummy(10.0f);

                DrawAbilityToggles();
                ImGuiHelpers.ScaledDummy(10.0f);

                DrawTargetSection();
                ImGuiHelpers.ScaledDummy(10.0f);

                DrawStatusSection();
            }
        }
    }

    private void DrawHeader()
    {
        var machinistImage = Plugin.TextureProvider.GetFromFile(machinistImagePath).GetWrapOrDefault();

        // Row with image and title
        if (machinistImage != null)
        {
            var imageSize = new Vector2(50, 50);
            ImGui.Image(machinistImage.Handle, imageSize);
            ImGui.SameLine();
        }

        ImGui.BeginGroup();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.75f, 0.0f, 1.0f));
        ImGui.Text("MACHINIST");
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
        ImGui.Text("Single Target DPS Combo");
        ImGui.PopStyleColor();
        ImGui.EndGroup();

        ImGui.SameLine(ImGui.GetWindowWidth() - 160);

        // Status indicator
        var statusColor = rotation.IsEnabled
            ? new Vector4(0.2f, 1.0f, 0.2f, 1.0f)
            : new Vector4(0.6f, 0.6f, 0.6f, 1.0f);
        ImGui.TextColored(statusColor, rotation.IsEnabled ? "ACTIVE" : "INACTIVE");
    }

    private void DrawComboStartSection()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.9f, 1.0f, 1.0f));
        ImGui.Text("Start Combo");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "(Press any combo button to auto-start)");
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            // Current target display
            var currentTarget = Plugin.TargetManager.Target;
            if (currentTarget != null)
            {
                ImGui.Text("Target:");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.4f, 1.0f, 0.4f, 1.0f), $"{currentTarget.Name}");
                if (currentTarget is IBattleChara battleChara)
                {
                    var hpPercent = battleChara.MaxHp > 0 ? (float)battleChara.CurrentHp / battleChara.MaxHp * 100 : 0;
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), $"({hpPercent:F0}%% HP)");
                }
            }
            else
            {
                ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.5f, 1.0f), "No target - Select an enemy first!");
            }

            ImGuiHelpers.ScaledDummy(8.0f);

            // Big combo start buttons
            var buttonSize = new Vector2(140, 50);

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.4f, 0.7f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.5f, 0.8f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.6f, 0.9f, 1.0f));

            if (ImGui.Button("1: Split Shot", buttonSize))
            {
                OnComboButtonClick(MachinistRotation.HeatedSplitShot, "Heated Split Shot");
            }
            ImGui.SameLine();
            if (ImGui.Button("2: Slug Shot", buttonSize))
            {
                OnComboButtonClick(MachinistRotation.HeatedSlugShot, "Heated Slug Shot");
            }
            ImGui.SameLine();
            if (ImGui.Button("3: Clean Shot", buttonSize))
            {
                OnComboButtonClick(MachinistRotation.HeatedCleanShot, "Heated Clean Shot");
            }

            ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Pressing any combo button will:\n1. Execute that ability on your selected target\n2. Auto-start the rotation to continue attacking");

            ImGuiHelpers.ScaledDummy(5.0f);

            // Control buttons
            if (rotation.IsEnabled)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 1.0f));
                if (ImGui.Button("Stop Rotation", new Vector2(120, 30)))
                {
                    rotation.StopRotation();
                    lastActionResult = "Rotation stopped";
                    lastActionTime = DateTime.Now;
                }
                ImGui.PopStyleColor(2);

                ImGui.SameLine();

                if (rotation.IsInOpener)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.4f, 0.1f, 1.0f));
                    if (ImGui.Button("Skip Opener", new Vector2(100, 30)))
                    {
                        rotation.ResetOpener();
                        lastActionResult = "Opener skipped";
                        lastActionTime = DateTime.Now;
                    }
                    ImGui.PopStyleColor();
                }
            }

            // Rotation info
            if (rotation.IsEnabled)
            {
                ImGuiHelpers.ScaledDummy(5.0f);
                using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.15f, 0.15f, 0.2f, 1.0f)))
                {
                    using (var infoChild = ImRaii.Child("RotationInfo", new Vector2(-1, 85), true))
                    {
                        if (infoChild.Success)
                        {
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Status:");
                            ImGui.SameLine();
                            ImGui.TextColored(new Vector4(0.4f, 1.0f, 0.4f, 1.0f), rotation.RotationStatus);

                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Next:");
                            ImGui.SameLine();
                            ImGui.TextColored(new Vector4(0.4f, 0.9f, 1.0f, 1.0f), rotation.GetNextActionPreview());

                            // Heat gauge display
                            var heatColor = rotation.CurrentHeat >= 100
                                ? new Vector4(1.0f, 0.2f, 0.2f, 1.0f)  // Red when at max (overcap risk)
                                : rotation.CurrentHeat >= 50
                                    ? new Vector4(1.0f, 0.6f, 0.0f, 1.0f)  // Orange when ready for Hypercharge
                                    : new Vector4(0.4f, 0.8f, 1.0f, 1.0f); // Blue normally
                            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, heatColor);
                            ImGui.ProgressBar(rotation.CurrentHeat / 100f, new Vector2(-1, 14), $"Heat: {rotation.CurrentHeat}/100");
                            ImGui.PopStyleColor();

                            if (rotation.IsInOpener)
                            {
                                var progress = rotation.OpenerStep / 25f;
                                ImGui.ProgressBar(progress, new Vector2(-1, 12), $"Opener: {rotation.OpenerStep}/25");
                            }
                        }
                    }
                }
            }
        }
    }

    private void DrawAbilityToggles()
    {
        var enabledCount = rotation.GetEnabledAbilityCount();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.8f, 0.2f, 1.0f));
        ImGui.Text("Combo Settings");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), $"({enabledCount}/12 abilities enabled)");
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            var config = plugin.Configuration;
            var settings = config.Machinist;
            var changed = false;

            // === MAIN TOGGLES (prominent) ===
            ImGuiHelpers.ScaledDummy(3.0f);

            // Disable Burst Phase - BIG RED TOGGLE
            using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0.3f, 0.1f, 0.1f, 1.0f)))
            using (ImRaii.PushColor(ImGuiCol.FrameBgHovered, new Vector4(0.4f, 0.15f, 0.15f, 1.0f)))
            using (ImRaii.PushColor(ImGuiCol.CheckMark, new Vector4(1.0f, 0.3f, 0.3f, 1.0f)))
            {
                var disableBurst = settings.DisableBurstPhase;
                if (ImGui.Checkbox("DISABLE BURST PHASE", ref disableBurst))
                {
                    settings.DisableBurstPhase = disableBurst;
                    changed = true;
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When checked, the rotation will NOT use:\n- Hypercharge\n- Wildfire\n\nUseful for saving burst for specific phases.");

            if (settings.DisableBurstPhase)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "(Burst abilities skipped!)");
            }

            ImGuiHelpers.ScaledDummy(5.0f);

            // Opener toggle
            using (ImRaii.PushColor(ImGuiCol.CheckMark, new Vector4(0.9f, 0.6f, 1.0f, 1.0f)))
            {
                var useOpener = settings.UseOpener;
                if (ImGui.Checkbox("Use Opener Sequence", ref useOpener))
                {
                    settings.UseOpener = useOpener;
                    changed = true;
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When enabled, starting the combo will execute\nthe optimal opener sequence first.");

            ImGuiHelpers.ScaledDummy(10.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            // === INDIVIDUAL ABILITY TOGGLES ===
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Individual Ability Toggles:");
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "Check = ability will be used in combo, Uncheck = skipped");
            ImGuiHelpers.ScaledDummy(5.0f);

            // Burst GCDs
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.5f, 0.2f, 1.0f));
            ImGui.Text("Burst Tools");
            ImGui.PopStyleColor();

            changed |= DrawAbilityToggle("Drill", settings.UseDrill, v => settings.UseDrill = v, "High potency single-target attack\n580 potency, 20s cooldown");
            ImGui.SameLine();
            changed |= DrawAbilityToggle("Air Anchor", settings.UseAirAnchor, v => settings.UseAirAnchor = v, "High potency + Battery gauge\n580 potency, 40s cooldown, +20 Battery");
            ImGui.SameLine();
            changed |= DrawAbilityToggle("Chain Saw", settings.UseChainSaw, v => settings.UseChainSaw = v, "High potency attack\n580 potency, 60s cooldown");

            changed |= DrawAbilityToggle("Excavator", settings.UseExcavator, v => settings.UseExcavator = v, "Proc from Chain Saw");
            ImGui.SameLine();
            changed |= DrawAbilityToggle("Full Metal Field", settings.UseFullMetalField, v => settings.UseFullMetalField = v, "Proc from Barrel Stabilizer");

            ImGuiHelpers.ScaledDummy(8.0f);

            // Hypercharge Window (dimmed if burst is disabled)
            var burstDisabled = settings.DisableBurstPhase;
            if (burstDisabled)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.3f, 0.3f, 1.0f));
                ImGui.Text("Hypercharge Window (DISABLED by burst toggle)");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.Text("Hypercharge Window");
                ImGui.PopStyleColor();
            }

            using (ImRaii.Disabled(burstDisabled))
            {
                changed |= DrawAbilityToggle("Hypercharge", settings.UseHypercharge, v => settings.UseHypercharge = v, "Activates Overheated state for Heat Blasts\nCosts 50 Heat, use during burst windows");
                ImGui.SameLine();
                changed |= DrawAbilityToggle("Heat Blast", settings.UseHeatBlast, v => settings.UseHeatBlast = v, "Fast 1.5s GCD during Hypercharge\nMust use all 5 stacks");
                ImGui.SameLine();
                changed |= DrawAbilityToggle("Wildfire", settings.UseWildfire, v => settings.UseWildfire = v, "Explodes based on weaponskills used\nUse during Hypercharge for max damage");
            }

            ImGuiHelpers.ScaledDummy(8.0f);

            // Support oGCDs
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.9f, 0.6f, 1.0f));
            ImGui.Text("Buffs & oGCDs");
            ImGui.PopStyleColor();

            changed |= DrawAbilityToggle("Reassemble", settings.UseReassemble, v => settings.UseReassemble = v, "Guarantees crit + direct hit on next GCD\nUse before Drill/Air Anchor/Chain Saw");
            ImGui.SameLine();
            changed |= DrawAbilityToggle("Barrel Stabilizer", settings.UseBarrelStabilizer, v => settings.UseBarrelStabilizer = v, "Generates +50 Heat instantly\nGrants Full Metal Field proc");

            changed |= DrawAbilityToggle("Gauss Round", settings.UseGaussRound, v => settings.UseGaussRound = v, "oGCD damage, weave between GCDs\n3 charges, don't overcap");
            ImGui.SameLine();
            changed |= DrawAbilityToggle("Ricochet", settings.UseRicochet, v => settings.UseRicochet = v, "oGCD damage, weave between GCDs\n3 charges, don't overcap");

            if (changed)
            {
                config.Save();
                lastActionResult = "Settings updated";
                lastActionTime = DateTime.Now;
            }

            ImGuiHelpers.ScaledDummy(10.0f);

            // Quick presets
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Quick Presets:");

            if (ImGui.Button("Enable All", new Vector2(90, 25)))
            {
                SetAllAbilities(settings, true);
                settings.DisableBurstPhase = false;
                config.Save();
                lastActionResult = "All abilities enabled";
                lastActionTime = DateTime.Now;
            }
            ImGui.SameLine();
            if (ImGui.Button("Disable All", new Vector2(90, 25)))
            {
                SetAllAbilities(settings, false);
                config.Save();
                lastActionResult = "All abilities disabled (1-2-3 combo only)";
                lastActionTime = DateTime.Now;
            }
            ImGui.SameLine();
            if (ImGui.Button("No Burst", new Vector2(90, 25)))
            {
                SetAllAbilities(settings, true);
                settings.DisableBurstPhase = true;
                config.Save();
                lastActionResult = "Burst phase disabled";
                lastActionTime = DateTime.Now;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Enables all abilities but disables Hypercharge/Wildfire burst");
        }
    }

    private static bool DrawAbilityToggle(string label, bool value, Action<bool> setter, string tooltip)
    {
        var localValue = value;
        var changed = ImGui.Checkbox(label, ref localValue);
        if (changed)
        {
            setter(localValue);
        }
        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(tooltip))
            ImGui.SetTooltip(tooltip);
        return changed;
    }

    private static void SetAllAbilities(MachinistSettings settings, bool enabled)
    {
        settings.UseDrill = enabled;
        settings.UseAirAnchor = enabled;
        settings.UseChainSaw = enabled;
        settings.UseExcavator = enabled;
        settings.UseFullMetalField = enabled;
        settings.UseReassemble = enabled;
        settings.UseBarrelStabilizer = enabled;
        settings.UseHypercharge = enabled;
        settings.UseHeatBlast = enabled;
        settings.UseWildfire = enabled;
        settings.UseGaussRound = enabled;
        settings.UseRicochet = enabled;
    }

    private void DrawTargetSection()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.6f, 1.0f, 1.0f));
        ImGui.Text("Target Selection");
        ImGui.PopStyleColor();
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            if (ImGui.Button("Target Nearest Enemy", new Vector2(160, 28)))
            {
                TargetNearestEnemy();
            }
            ImGui.SameLine();
            if (ImGui.Button("Clear Target", new Vector2(100, 28)))
            {
                Plugin.TargetManager.Target = null;
                lastActionResult = "Target cleared";
                lastActionTime = DateTime.Now;
            }

            ImGuiHelpers.ScaledDummy(5.0f);
            DrawNearbyEnemies();
        }
    }

    private void DrawNearbyEnemies()
    {
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Nearby Enemies:");

        var localPlayer = Plugin.ClientState.LocalPlayer;
        if (localPlayer == null)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "  Player not loaded");
            return;
        }

        var enemies = Plugin.ObjectTable
            .OfType<IBattleNpc>()
            .Where(o => o.BattleNpcKind == BattleNpcSubKind.Enemy && IsTargetable(o))
            .OrderBy(o => Vector3.Distance(localPlayer.Position, o.Position))
            .Take(4)
            .ToList();

        if (enemies.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "  No enemies nearby");
            return;
        }

        using (ImRaii.PushIndent(5f))
        {
            foreach (var enemy in enemies)
            {
                var distance = Vector3.Distance(localPlayer.Position, enemy.Position);
                var hpPercent = enemy.MaxHp > 0 ? (float)enemy.CurrentHp / enemy.MaxHp * 100 : 0;
                var isCurrentTarget = Plugin.TargetManager.Target?.GameObjectId == enemy.GameObjectId;

                var label = $"{enemy.Name} ({distance:F0}y, {hpPercent:F0}%%)";

                if (isCurrentTarget)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 1.0f, 0.4f, 1.0f));
                }

                if (ImGui.Selectable(label, isCurrentTarget))
                {
                    Plugin.TargetManager.Target = enemy;
                    lastActionResult = $"Targeted: {enemy.Name}";
                    lastActionTime = DateTime.Now;
                }

                if (isCurrentTarget)
                {
                    ImGui.PopStyleColor();
                }
            }
        }
    }

    private void DrawStatusSection()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.9f, 1.0f));
        ImGui.Text("Log");
        ImGui.PopStyleColor();
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            if (!string.IsNullOrEmpty(lastActionResult))
            {
                var timeSince = DateTime.Now - lastActionTime;
                var fadeAlpha = Math.Max(0.4f, 1.0f - (float)timeSince.TotalSeconds / 5.0f);
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, fadeAlpha), $"> {lastActionResult}");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "Ready - select a target and press a combo button");
            }

            if (rotation.IsEnabled && !string.IsNullOrEmpty(rotation.LastAction))
            {
                ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), $"> [Auto] {rotation.LastAction}");
            }
        }
    }

    private void TargetNearestEnemy()
    {
        var localPlayer = Plugin.ClientState.LocalPlayer;
        if (localPlayer == null)
        {
            lastActionResult = "Error: Player not loaded";
            lastActionTime = DateTime.Now;
            return;
        }

        var nearestEnemy = Plugin.ObjectTable
            .OfType<IBattleNpc>()
            .Where(o => o.BattleNpcKind == BattleNpcSubKind.Enemy && IsTargetable(o))
            .OrderBy(o => Vector3.Distance(localPlayer.Position, o.Position))
            .FirstOrDefault();

        if (nearestEnemy != null)
        {
            Plugin.TargetManager.Target = nearestEnemy;
            var distance = Vector3.Distance(localPlayer.Position, nearestEnemy.Position);
            lastActionResult = $"Targeted: {nearestEnemy.Name} ({distance:F0}y)";
        }
        else
        {
            lastActionResult = "No enemies nearby";
        }

        lastActionTime = DateTime.Now;
    }

    private static bool IsTargetable(IGameObject obj)
    {
        return obj.IsTargetable && !obj.IsDead;
    }

    /// <summary>
    /// Called when user clicks a combo button - executes the action and auto-starts rotation
    /// </summary>
    private unsafe void OnComboButtonClick(uint actionId, string actionName)
    {
        var target = Plugin.TargetManager.Target;

        if (target == null)
        {
            lastActionResult = "No target selected! Select an enemy first.";
            lastActionTime = DateTime.Now;
            return;
        }

        var targetId = target.GameObjectId;

        var actionManager = ActionManager.Instance();
        if (actionManager == null)
        {
            lastActionResult = "Error: ActionManager not available";
            lastActionTime = DateTime.Now;
            return;
        }

        var actionStatus = actionManager->GetActionStatus(ActionType.Action, actionId);
        if (actionStatus != 0)
        {
            lastActionResult = $"{actionName}: Not ready";
            lastActionTime = DateTime.Now;
            return;
        }

        var result = actionManager->UseAction(ActionType.Action, actionId, targetId);

        if (result)
        {
            lastActionResult = $"Started combo with {actionName} on {target.Name}";

            // Auto-start the rotation
            rotation.OnComboButtonPressed();
        }
        else
        {
            lastActionResult = $"{actionName}: Failed to execute";
        }

        lastActionTime = DateTime.Now;
        Plugin.Log.Information($"Combo button: {actionName} (ID: {actionId}) - Result: {result}");
    }
}
