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

    // UI state
    private bool showAbilitySettings;

    public MachinistWindow(Plugin plugin, string machinistImagePath, MachinistRotation rotation)
        : base("Machinist Job Interface##MachinistWindow", ImGuiWindowFlags.NoScrollbar)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 800),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.machinistImagePath = machinistImagePath;
        this.plugin = plugin;
        this.rotation = rotation;
    }

    public void Dispose() { }

    // Helper to get settings
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
                DrawAutoRotationSection();
                ImGuiHelpers.ScaledDummy(10.0f);

                DrawBurstModeSection();
                ImGuiHelpers.ScaledDummy(10.0f);

                DrawAbilitySettingsSection();
                ImGuiHelpers.ScaledDummy(10.0f);

                DrawTargetSection();
                ImGuiHelpers.ScaledDummy(10.0f);

                DrawActionButtons();
                ImGuiHelpers.ScaledDummy(10.0f);

                DrawStatusSection();
            }
        }
    }

    private void DrawHeader()
    {
        var machinistImage = Plugin.TextureProvider.GetFromFile(machinistImagePath).GetWrapOrDefault();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.75f, 0.0f, 1.0f));
        var title = "MACHINIST";
        var titleSize = ImGui.CalcTextSize(title);
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - titleSize.X) / 2);
        ImGui.Text(title);
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
        var subtitle = "Ranged Physical DPS - Auto-Rotation";
        var subtitleSize = ImGui.CalcTextSize(subtitle);
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - subtitleSize.X) / 2);
        ImGui.Text(subtitle);
        ImGui.PopStyleColor();

        ImGuiHelpers.ScaledDummy(5.0f);

        if (machinistImage != null)
        {
            var imageSize = new Vector2(80, 80);
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - imageSize.X) / 2);
            ImGui.Image(machinistImage.Handle, imageSize);
        }
    }

    private void DrawAutoRotationSection()
    {
        var statusColor = rotation.IsEnabled
            ? new Vector4(0.2f, 1.0f, 0.2f, 1.0f)
            : new Vector4(1.0f, 0.4f, 0.4f, 1.0f);

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.85f, 0.0f, 1.0f));
        ImGui.Text("Auto-Rotation Control");
        ImGui.PopStyleColor();
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            ImGui.TextColored(statusColor, rotation.IsEnabled ? "ACTIVE" : "INACTIVE");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), $"- {rotation.RotationStatus}");

            ImGuiHelpers.ScaledDummy(5.0f);

            var buttonWidth = 140f;
            var buttonHeight = 40f;

            if (rotation.IsEnabled)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 1.0f));
                if (ImGui.Button("Stop Rotation", new Vector2(buttonWidth, buttonHeight)))
                {
                    rotation.StopRotation();
                    lastActionResult = "Auto-rotation stopped";
                    lastActionTime = DateTime.Now;
                }
                ImGui.PopStyleColor(2);
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.3f, 1.0f));
                if (ImGui.Button("Start Rotation", new Vector2(buttonWidth, buttonHeight)))
                {
                    rotation.IsEnabled = true;
                    lastActionResult = "Auto-rotation started";
                    lastActionTime = DateTime.Now;
                }
                ImGui.PopStyleColor(2);
            }

            ImGui.SameLine();

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.4f, 0.1f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.5f, 0.2f, 1.0f));
            var openerLabel = rotation.IsInOpener ? "Reset Opener" : "Start Opener";
            if (ImGui.Button(openerLabel, new Vector2(buttonWidth, buttonHeight)))
            {
                if (rotation.IsInOpener)
                {
                    rotation.ResetOpener();
                    lastActionResult = "Opener reset";
                }
                else
                {
                    rotation.IsEnabled = true;
                    rotation.StartOpener();
                    lastActionResult = rotation.BurstModeEnabled
                        ? "Opener started - executing optimal burst sequence"
                        : "Cannot start opener - Burst Mode is disabled";
                }
                lastActionTime = DateTime.Now;
            }
            ImGui.PopStyleColor(2);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Execute the standard Machinist opener\n(Requires Burst Mode to be ON)");

            ImGui.SameLine();

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.4f, 0.2f, 0.6f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.5f, 0.3f, 0.7f, 1.0f));
            if (ImGui.Button("Target & Fight", new Vector2(buttonWidth, buttonHeight)))
            {
                TargetNearestEnemy();
                if (Plugin.TargetManager.Target != null)
                {
                    rotation.IsEnabled = true;
                    rotation.StartOpener();
                    lastActionResult = "Targeted enemy and started rotation";
                }
                lastActionTime = DateTime.Now;
            }
            ImGui.PopStyleColor(2);

            ImGuiHelpers.ScaledDummy(8.0f);

            // Rotation info panel
            using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.15f, 0.15f, 0.2f, 1.0f)))
            {
                using (var infoChild = ImRaii.Child("RotationInfo", new Vector2(-1, 70), true))
                {
                    if (infoChild.Success)
                    {
                        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), "Next Action:");
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0.4f, 0.9f, 1.0f, 1.0f), rotation.GetNextActionPreview());

                        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), "Last Action:");
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0.6f, 1.0f, 0.6f, 1.0f),
                            string.IsNullOrEmpty(rotation.LastAction) ? "None" : rotation.LastAction);

                        if (rotation.IsInOpener)
                        {
                            var progress = rotation.OpenerStep / 29f;
                            ImGui.ProgressBar(progress, new Vector2(-1, 14), $"Opener: {rotation.OpenerStep}/29");
                        }
                    }
                }
            }
        }
    }

    private void DrawBurstModeSection()
    {
        var burstColor = rotation.BurstModeEnabled
            ? new Vector4(1.0f, 0.5f, 0.0f, 1.0f)
            : new Vector4(0.5f, 0.5f, 0.5f, 1.0f);

        ImGui.PushStyleColor(ImGuiCol.Text, burstColor);
        ImGui.Text("Burst Mode");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "(use /pmchburst in macro)");
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            // Big burst toggle button
            var burstOn = rotation.BurstModeEnabled;
            var burstBtnColor = burstOn
                ? new Vector4(0.8f, 0.4f, 0.0f, 1.0f)
                : new Vector4(0.3f, 0.3f, 0.3f, 1.0f);
            var burstBtnHover = burstOn
                ? new Vector4(0.9f, 0.5f, 0.1f, 1.0f)
                : new Vector4(0.4f, 0.4f, 0.4f, 1.0f);

            ImGui.PushStyleColor(ImGuiCol.Button, burstBtnColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, burstBtnHover);
            ImGui.PushStyleColor(ImGuiCol.Text, burstOn ? new Vector4(1, 1, 1, 1) : new Vector4(0.7f, 0.7f, 0.7f, 1));

            var burstLabel = burstOn ? "BURST MODE: ON" : "BURST MODE: OFF";
            if (ImGui.Button(burstLabel, new Vector2(200, 45)))
            {
                rotation.ToggleBurstMode();
                lastActionResult = $"Burst Mode: {(rotation.BurstModeEnabled ? "Enabled" : "Disabled")}";
                lastActionTime = DateTime.Now;
            }
            ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Toggle burst abilities (Drill, Air Anchor, Chain Saw, etc.)\n" +
                                 "When OFF, only uses basic combo and oGCDs\n\n" +
                                 "Macro command: /pmchburst [on|off]");

            ImGui.SameLine();

            // Quick burst off button for emergencies
            if (burstOn)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.1f, 0.1f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.2f, 0.2f, 1.0f));
                if (ImGui.Button("BURST OFF", new Vector2(100, 45)))
                {
                    rotation.BurstModeEnabled = false;
                    lastActionResult = "Burst Mode disabled";
                    lastActionTime = DateTime.Now;
                }
                ImGui.PopStyleColor(2);
            }

            ImGuiHelpers.ScaledDummy(3.0f);

            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f),
                burstOn
                    ? "Full rotation with all burst abilities active"
                    : "Basic combo only - no burst tools or Hypercharge");
        }
    }

    private void DrawAbilitySettingsSection()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.8f, 1.0f, 1.0f));
        if (ImGui.CollapsingHeader("Ability Settings", showAbilitySettings ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
        {
            showAbilitySettings = true;
            ImGui.PopStyleColor();

            using (ImRaii.PushIndent(10f))
            {
                var config = plugin.Configuration;
                var settings = config.Machinist;
                var changed = false;

                // GCD Abilities
                ImGui.TextColored(new Vector4(1.0f, 0.6f, 0.2f, 1.0f), "Burst GCDs:");

                var useDrill = settings.UseDrill;
                if (ImGui.Checkbox("Drill", ref useDrill)) { settings.UseDrill = useDrill; changed = true; }
                ImGui.SameLine();
                var useAirAnchor = settings.UseAirAnchor;
                if (ImGui.Checkbox("Air Anchor", ref useAirAnchor)) { settings.UseAirAnchor = useAirAnchor; changed = true; }
                ImGui.SameLine();
                var useChainSaw = settings.UseChainSaw;
                if (ImGui.Checkbox("Chain Saw", ref useChainSaw)) { settings.UseChainSaw = useChainSaw; changed = true; }

                var useExcavator = settings.UseExcavator;
                if (ImGui.Checkbox("Excavator", ref useExcavator)) { settings.UseExcavator = useExcavator; changed = true; }
                ImGui.SameLine();
                var useFullMetalField = settings.UseFullMetalField;
                if (ImGui.Checkbox("Full Metal Field", ref useFullMetalField)) { settings.UseFullMetalField = useFullMetalField; changed = true; }

                ImGuiHelpers.ScaledDummy(5.0f);

                // oGCD Abilities
                ImGui.TextColored(new Vector4(0.6f, 0.9f, 0.6f, 1.0f), "oGCDs:");

                var useReassemble = settings.UseReassemble;
                if (ImGui.Checkbox("Reassemble", ref useReassemble)) { settings.UseReassemble = useReassemble; changed = true; }
                ImGui.SameLine();
                var useBarrelStabilizer = settings.UseBarrelStabilizer;
                if (ImGui.Checkbox("Barrel Stabilizer", ref useBarrelStabilizer)) { settings.UseBarrelStabilizer = useBarrelStabilizer; changed = true; }

                var useHypercharge = settings.UseHypercharge;
                if (ImGui.Checkbox("Hypercharge", ref useHypercharge)) { settings.UseHypercharge = useHypercharge; changed = true; }
                ImGui.SameLine();
                var useHeatBlast = settings.UseHeatBlast;
                if (ImGui.Checkbox("Heat Blast", ref useHeatBlast)) { settings.UseHeatBlast = useHeatBlast; changed = true; }

                var useWildfire = settings.UseWildfire;
                if (ImGui.Checkbox("Wildfire", ref useWildfire)) { settings.UseWildfire = useWildfire; changed = true; }
                ImGui.SameLine();
                var useGaussRound = settings.UseGaussRound;
                if (ImGui.Checkbox("Gauss Round", ref useGaussRound)) { settings.UseGaussRound = useGaussRound; changed = true; }
                ImGui.SameLine();
                var useRicochet = settings.UseRicochet;
                if (ImGui.Checkbox("Ricochet", ref useRicochet)) { settings.UseRicochet = useRicochet; changed = true; }

                ImGuiHelpers.ScaledDummy(5.0f);

                // Basic
                ImGui.TextColored(new Vector4(0.4f, 0.7f, 1.0f, 1.0f), "Basic:");
                var useBasicCombo = settings.UseBasicCombo;
                if (ImGui.Checkbox("Basic Combo (1-2-3)", ref useBasicCombo)) { settings.UseBasicCombo = useBasicCombo; changed = true; }

                if (changed)
                {
                    config.Save();
                    lastActionResult = "Settings saved";
                    lastActionTime = DateTime.Now;
                }

                ImGuiHelpers.ScaledDummy(5.0f);

                // Reset button
                if (ImGui.Button("Reset All to Default"))
                {
                    settings.UseDrill = true;
                    settings.UseAirAnchor = true;
                    settings.UseChainSaw = true;
                    settings.UseExcavator = true;
                    settings.UseFullMetalField = true;
                    settings.UseReassemble = true;
                    settings.UseBarrelStabilizer = true;
                    settings.UseHypercharge = true;
                    settings.UseHeatBlast = true;
                    settings.UseWildfire = true;
                    settings.UseGaussRound = true;
                    settings.UseRicochet = true;
                    settings.UseBasicCombo = true;
                    config.Save();
                    lastActionResult = "All abilities reset to enabled";
                    lastActionTime = DateTime.Now;
                }
            }
        }
        else
        {
            showAbilitySettings = false;
            ImGui.PopStyleColor();
        }
    }

    private void DrawTargetSection()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.4f, 0.4f, 1.0f));
        ImGui.Text("Target Management");
        ImGui.PopStyleColor();
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            var currentTarget = Plugin.TargetManager.Target;
            if (currentTarget != null)
            {
                ImGui.TextColored(new Vector4(0.4f, 1.0f, 0.4f, 1.0f), $"Current Target: {currentTarget.Name}");
                if (currentTarget is IBattleChara battleChara)
                {
                    var hpPercent = battleChara.MaxHp > 0 ? (float)battleChara.CurrentHp / battleChara.MaxHp * 100 : 0;
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), $"(HP: {hpPercent:F1}%%)");
                }
            }
            else
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "No target selected");
            }

            ImGuiHelpers.ScaledDummy(5.0f);

            if (ImGui.Button("Target Nearest Enemy", new Vector2(180, 28)))
            {
                TargetNearestEnemy();
            }

            ImGui.SameLine();

            if (ImGui.Button("Clear Target", new Vector2(120, 28)))
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
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), "Nearby Enemies (click to target):");

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
            .Take(5)
            .ToList();

        if (enemies.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "  No enemies nearby");
            return;
        }

        using (ImRaii.PushIndent(10f))
        {
            foreach (var enemy in enemies)
            {
                var distance = Vector3.Distance(localPlayer.Position, enemy.Position);
                var hpPercent = enemy.MaxHp > 0 ? (float)enemy.CurrentHp / enemy.MaxHp * 100 : 0;
                var label = $"{enemy.Name} - {distance:F1}y - HP: {hpPercent:F0}%%";

                if (ImGui.Selectable(label, Plugin.TargetManager.Target?.GameObjectId == enemy.GameObjectId))
                {
                    Plugin.TargetManager.Target = enemy;
                    lastActionResult = $"Targeted: {enemy.Name}";
                    lastActionTime = DateTime.Now;
                }
            }
        }
    }

    private void DrawActionButtons()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.7f, 1.0f, 1.0f));
        ImGui.Text("Manual Actions (use when auto-rotation is off)");
        ImGui.PopStyleColor();
        ImGui.Separator();

        if (rotation.IsEnabled)
        {
            using (ImRaii.PushIndent(10f))
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f),
                    "Auto-rotation is active. Stop it to use manual controls.");
            }
            return;
        }

        using (ImRaii.PushIndent(10f))
        {
            var buttonSize = new Vector2(145, 35);

            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1.0f, 1.0f), "Basic Combo:");
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.4f, 0.6f, 1.0f));
            if (ImGui.Button("1: Split Shot", buttonSize))
                ExecuteAction(MachinistRotation.HeatedSplitShot, "Heated Split Shot");
            ImGui.SameLine();
            if (ImGui.Button("2: Slug Shot", buttonSize))
                ExecuteAction(MachinistRotation.HeatedSlugShot, "Heated Slug Shot");
            ImGui.SameLine();
            if (ImGui.Button("3: Clean Shot", buttonSize))
                ExecuteAction(MachinistRotation.HeatedCleanShot, "Heated Clean Shot");
            ImGui.PopStyleColor();

            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.TextColored(new Vector4(1.0f, 0.6f, 0.2f, 1.0f), "Burst Tools:");
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.3f, 0.1f, 1.0f));
            if (ImGui.Button("Drill", new Vector2(100, 30)))
                ExecuteAction(MachinistRotation.Drill, "Drill");
            ImGui.SameLine();
            if (ImGui.Button("Air Anchor", new Vector2(100, 30)))
                ExecuteAction(MachinistRotation.AirAnchor, "Air Anchor");
            ImGui.SameLine();
            if (ImGui.Button("Chain Saw", new Vector2(100, 30)))
                ExecuteAction(MachinistRotation.ChainSaw, "Chain Saw");
            ImGui.SameLine();
            if (ImGui.Button("Reassemble", new Vector2(100, 30)))
                ExecuteAction(MachinistRotation.Reassemble, "Reassemble");
            ImGui.PopStyleColor();

            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), "Hypercharge:");
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.1f, 0.1f, 1.0f));
            if (ImGui.Button("Barrel Stab", new Vector2(100, 30)))
                ExecuteAction(MachinistRotation.BarrelStabilizer, "Barrel Stabilizer");
            ImGui.SameLine();
            if (ImGui.Button("Hypercharge", new Vector2(100, 30)))
                ExecuteAction(MachinistRotation.Hypercharge, "Hypercharge");
            ImGui.SameLine();
            if (ImGui.Button("Heat Blast", new Vector2(100, 30)))
                ExecuteAction(MachinistRotation.HeatBlast, "Heat Blast");
            ImGui.SameLine();
            if (ImGui.Button("Wildfire", new Vector2(100, 30)))
                ExecuteAction(MachinistRotation.Wildfire, "Wildfire");
            ImGui.PopStyleColor();

            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.TextColored(new Vector4(0.6f, 0.9f, 0.6f, 1.0f), "oGCDs:");
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.1f, 0.4f, 0.1f, 1.0f));
            if (ImGui.Button("Gauss Round", new Vector2(120, 30)))
                ExecuteAction(MachinistRotation.GaussRound, "Gauss Round");
            ImGui.SameLine();
            if (ImGui.Button("Ricochet", new Vector2(120, 30)))
                ExecuteAction(MachinistRotation.Ricochet, "Ricochet");
            ImGui.PopStyleColor();
        }
    }

    private void DrawStatusSection()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 1.0f, 1.0f));
        ImGui.Text("Action Log");
        ImGui.PopStyleColor();
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            if (!string.IsNullOrEmpty(lastActionResult))
            {
                var timeSince = DateTime.Now - lastActionTime;
                var fadeAlpha = Math.Max(0.3f, 1.0f - (float)timeSince.TotalSeconds / 5.0f);
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, fadeAlpha), $"> {lastActionResult}");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "No actions performed yet");
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
            lastActionResult = $"Targeted: {nearestEnemy.Name} ({distance:F1}y away)";
        }
        else
        {
            lastActionResult = "No enemies found nearby";
        }

        lastActionTime = DateTime.Now;
    }

    private static bool IsTargetable(IGameObject obj)
    {
        return obj.IsTargetable && !obj.IsDead;
    }

    private unsafe void ExecuteAction(uint actionId, string actionName)
    {
        var target = Plugin.TargetManager.Target;
        var targetId = target?.GameObjectId ?? 0xE0000000;

        var actionManager = ActionManager.Instance();
        if (actionManager == null)
        {
            lastActionResult = $"Error: ActionManager not available";
            lastActionTime = DateTime.Now;
            return;
        }

        var actionStatus = actionManager->GetActionStatus(ActionType.Action, actionId);
        if (actionStatus != 0)
        {
            lastActionResult = $"{actionName}: Not ready (status: {actionStatus})";
            lastActionTime = DateTime.Now;
            return;
        }

        var result = actionManager->UseAction(ActionType.Action, actionId, targetId);

        if (result)
        {
            lastActionResult = $"Used: {actionName}" + (target != null ? $" on {target.Name}" : "");
        }
        else
        {
            lastActionResult = $"{actionName}: Failed to execute";
        }

        lastActionTime = DateTime.Now;
        Plugin.Log.Information($"Action {actionName} (ID: {actionId}) - Result: {result}");
    }
}
