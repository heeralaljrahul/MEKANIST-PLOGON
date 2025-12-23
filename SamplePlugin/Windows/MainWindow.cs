using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("Machinist Plugin##MainWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 150),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.75f, 0.0f, 1.0f), "Machinist Rotation Plugin");
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(10.0f);

        if (ImGui.Button("Open Machinist Window", new Vector2(-1, 40)))
        {
            plugin.ToggleMachinistUi();
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        // Show rotation status
        var rotation = plugin.MachinistRotation;
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Rotation Status:");
        ImGui.SameLine();
        if (rotation.IsEnabled)
        {
            ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), "ACTIVE");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Inactive");
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "Commands: /pmachinist, /pmycommand");
    }
}
