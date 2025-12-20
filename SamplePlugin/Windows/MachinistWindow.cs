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

    // Machinist Action IDs
    private const uint HeatedSplitShot = 7411;
    private const uint HeatedSlugShot = 7412;
    private const uint HeatedCleanShot = 7413;
    private const uint Drill = 16498;
    private const uint AirAnchor = 16500;
    private const uint ChainSaw = 25788;
    private const uint GaussRound = 2874;
    private const uint Ricochet = 2890;
    private const uint Hypercharge = 17209;
    private const uint HeatBlast = 7410;
    private const uint Wildfire = 2878;
    private const uint Reassemble = 2876;
    private const uint BarrelStabilizer = 7414;

    // Combo state tracking
    private int currentComboStep;
    private string lastActionResult = "";
    private DateTime lastActionTime = DateTime.MinValue;

    public MachinistWindow(Plugin plugin, string machinistImagePath)
        : base("Machinist Job Interface##MachinistWindow", ImGuiWindowFlags.NoScrollbar)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 650),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.machinistImagePath = machinistImagePath;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Header with job icon/image
        DrawHeader();

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        using (var child = ImRaii.Child("MachinistContent", Vector2.Zero, false))
        {
            if (child.Success)
            {
                // Target Section
                DrawTargetSection();

                ImGuiHelpers.ScaledDummy(10.0f);

                // Action Buttons Section
                DrawActionButtons();

                ImGuiHelpers.ScaledDummy(10.0f);

                // Combo Info Section
                DrawComboInfo();

                ImGuiHelpers.ScaledDummy(10.0f);

                // Status/Result display
                DrawStatusSection();
            }
        }
    }

    private void DrawHeader()
    {
        var machinistImage = Plugin.TextureProvider.GetFromFile(machinistImagePath).GetWrapOrDefault();

        // Title
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.75f, 0.0f, 1.0f));
        var title = "MACHINIST";
        var titleSize = ImGui.CalcTextSize(title);
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - titleSize.X) / 2);
        ImGui.Text(title);
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
        var subtitle = "Ranged Physical DPS - Action Controller";
        var subtitleSize = ImGui.CalcTextSize(subtitle);
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - subtitleSize.X) / 2);
        ImGui.Text(subtitle);
        ImGui.PopStyleColor();

        ImGuiHelpers.ScaledDummy(5.0f);

        // Display the machinist image centered
        if (machinistImage != null)
        {
            var imageSize = new Vector2(80, 80);
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - imageSize.X) / 2);
            ImGui.Image(machinistImage.Handle, imageSize);
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
            // Current target display
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

            // Target buttons
            if (ImGui.Button("Target Nearest Enemy", new Vector2(180, 30)))
            {
                TargetNearestEnemy();
            }

            ImGui.SameLine();

            if (ImGui.Button("Clear Target", new Vector2(120, 30)))
            {
                Plugin.TargetManager.Target = null;
                lastActionResult = "Target cleared";
            }

            ImGuiHelpers.ScaledDummy(5.0f);

            // List nearby enemies
            DrawNearbyEnemies();
        }
    }

    private void DrawNearbyEnemies()
    {
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), "Nearby Enemies (click to target):");

        var localPlayer = Plugin.ObjectTable.FirstOrDefault(o => o is IPlayerCharacter);
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
                }
            }
        }
    }

    private void DrawActionButtons()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.7f, 1.0f, 1.0f));
        ImGui.Text("Basic Combo Actions");
        ImGui.PopStyleColor();
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            // Combo buttons with visual feedback
            var buttonSize = new Vector2(150, 40);

            // Step 1: Heated Split Shot
            var step1Color = currentComboStep == 0
                ? new Vector4(0.2f, 0.6f, 0.2f, 1.0f)
                : new Vector4(0.3f, 0.3f, 0.3f, 1.0f);
            ImGui.PushStyleColor(ImGuiCol.Button, step1Color);
            if (ImGui.Button("1: Heated Split Shot", buttonSize))
            {
                ExecuteAction(HeatedSplitShot, "Heated Split Shot");
                currentComboStep = 1;
            }
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Potency: 220\nGenerates 5 Heat");

            ImGui.SameLine();

            // Step 2: Heated Slug Shot
            var step2Color = currentComboStep == 1
                ? new Vector4(0.2f, 0.6f, 0.2f, 1.0f)
                : new Vector4(0.3f, 0.3f, 0.3f, 1.0f);
            ImGui.PushStyleColor(ImGuiCol.Button, step2Color);
            if (ImGui.Button("2: Heated Slug Shot", buttonSize))
            {
                ExecuteAction(HeatedSlugShot, "Heated Slug Shot");
                currentComboStep = 2;
            }
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Potency: 140 (Combo: 340)\nGenerates 5 Heat");

            ImGui.SameLine();

            // Step 3: Heated Clean Shot
            var step3Color = currentComboStep == 2
                ? new Vector4(0.2f, 0.6f, 0.2f, 1.0f)
                : new Vector4(0.3f, 0.3f, 0.3f, 1.0f);
            ImGui.PushStyleColor(ImGuiCol.Button, step3Color);
            if (ImGui.Button("3: Heated Clean Shot", buttonSize))
            {
                ExecuteAction(HeatedCleanShot, "Heated Clean Shot");
                currentComboStep = 0;
            }
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Potency: 160 (Combo: 440)\nGenerates 5 Heat + 10 Battery");
        }

        ImGuiHelpers.ScaledDummy(10.0f);

        // Burst abilities
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.6f, 0.2f, 1.0f));
        ImGui.Text("Burst Abilities");
        ImGui.PopStyleColor();
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            var burstSize = new Vector2(110, 35);

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.3f, 0.1f, 1.0f));

            if (ImGui.Button("Reassemble", burstSize))
                ExecuteAction(Reassemble, "Reassemble");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Guarantees critical direct hit\nRecast: 55s");

            ImGui.SameLine();

            if (ImGui.Button("Drill", burstSize))
                ExecuteAction(Drill, "Drill");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Potency: 600\nRecast: 20s");

            ImGui.SameLine();

            if (ImGui.Button("Air Anchor", burstSize))
                ExecuteAction(AirAnchor, "Air Anchor");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Potency: 600\nBattery +20\nRecast: 40s");

            ImGui.SameLine();

            if (ImGui.Button("Chain Saw", burstSize))
                ExecuteAction(ChainSaw, "Chain Saw");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Potency: 600\nBattery +20\nRecast: 60s");

            ImGui.PopStyleColor();
        }

        ImGuiHelpers.ScaledDummy(10.0f);

        // Hypercharge abilities
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
        ImGui.Text("Hypercharge Window");
        ImGui.PopStyleColor();
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            var hyperSize = new Vector2(130, 35);

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.1f, 0.1f, 1.0f));

            if (ImGui.Button("Barrel Stabilizer", hyperSize))
                ExecuteAction(BarrelStabilizer, "Barrel Stabilizer");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Generates 50 Heat\nRecast: 120s");

            ImGui.SameLine();

            if (ImGui.Button("Hypercharge", hyperSize))
                ExecuteAction(Hypercharge, "Hypercharge");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Consumes 50 Heat\nEnables Heat Blast for 8s");

            ImGui.SameLine();

            if (ImGui.Button("Heat Blast", hyperSize))
                ExecuteAction(HeatBlast, "Heat Blast");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Potency: 200\nRecast: 1.5s\nReduces oGCD cooldowns by 15s");

            if (ImGui.Button("Wildfire", hyperSize))
                ExecuteAction(Wildfire, "Wildfire");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Marks target\n240 potency per weaponskill (max 6)");

            ImGui.PopStyleColor();
        }

        ImGuiHelpers.ScaledDummy(10.0f);

        // oGCD abilities
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.9f, 0.6f, 1.0f));
        ImGui.Text("oGCD Weaving");
        ImGui.PopStyleColor();
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            var ogcdSize = new Vector2(120, 35);

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.1f, 0.4f, 0.1f, 1.0f));

            if (ImGui.Button("Gauss Round", ogcdSize))
                ExecuteAction(GaussRound, "Gauss Round");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Potency: 130\n3 charges");

            ImGui.SameLine();

            if (ImGui.Button("Ricochet", ogcdSize))
                ExecuteAction(Ricochet, "Ricochet");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Potency: 130 (AoE)\n3 charges");

            ImGui.PopStyleColor();
        }
    }

    private void DrawComboInfo()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.9f, 0.3f, 1.0f));
        ImGui.Text("Combo State");
        ImGui.PopStyleColor();
        ImGui.Separator();

        using (ImRaii.PushIndent(10f))
        {
            var comboText = currentComboStep switch
            {
                0 => "Ready for: Heated Split Shot (Step 1)",
                1 => "Ready for: Heated Slug Shot (Step 2)",
                2 => "Ready for: Heated Clean Shot (Step 3)",
                _ => "Unknown state"
            };

            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), comboText);

            // Visual combo indicator
            ImGui.Text("Combo Progress: ");
            ImGui.SameLine();

            for (var i = 0; i < 3; i++)
            {
                var color = i < currentComboStep
                    ? new Vector4(0.2f, 0.8f, 0.2f, 1.0f)
                    : i == currentComboStep
                        ? new Vector4(1.0f, 0.8f, 0.0f, 1.0f)
                        : new Vector4(0.3f, 0.3f, 0.3f, 1.0f);

                ImGui.TextColored(color, i < currentComboStep ? "[*]" : i == currentComboStep ? "[>]" : "[ ]");
                if (i < 2)
                    ImGui.SameLine();
            }

            if (ImGui.Button("Reset Combo"))
            {
                currentComboStep = 0;
                lastActionResult = "Combo reset";
            }
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
        }
    }

    private void TargetNearestEnemy()
    {
        var localPlayer = Plugin.ObjectTable.FirstOrDefault(o => o is IPlayerCharacter);
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

        // Use FFXIVClientStructs to execute the action
        var actionManager = ActionManager.Instance();
        if (actionManager == null)
        {
            lastActionResult = $"Error: ActionManager not available";
            lastActionTime = DateTime.Now;
            return;
        }

        // Check if action is available
        var actionStatus = actionManager->GetActionStatus(ActionType.Action, actionId);
        if (actionStatus != 0)
        {
            lastActionResult = $"{actionName}: Not ready (status: {actionStatus})";
            lastActionTime = DateTime.Now;
            return;
        }

        // Execute the action
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
