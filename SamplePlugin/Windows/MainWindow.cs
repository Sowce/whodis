using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using System.Numerics;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    public CharacterRow?[]? characters;
    private readonly Plugin plugin;
    private static readonly Vector2 IconSize = new Vector2(33, 33);

    public MainWindow(Plugin plugin)
        : base("##whodis", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove)
    {
        this.Size = Vector2.Zero;
        this.RespectCloseHotkey = false;
        this.AllowClickthrough = true;
        this.SizeCondition = ImGuiCond.Always;

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (characters == null)
        {
            return;
        }

        if (characters.All(chr => !chr.HasValue))
        {
            IsOpen = false;
            return;

        }

        var lfgAddon = Plugin.GameGui.GetAddonByName("LookingForGroupDetail");
        if (lfgAddon == null)
        {
            return;
        }

        if (lfgAddon.Position != new Vector2(0, 0))
        {
            if (lfgAddon.X + lfgAddon.ScaledWidth + ImGui.GetWindowWidth() > ImGuiHelpers.MainViewport.Size.X)
            {
                this.Position = new Vector2(lfgAddon.X - ImGui.GetWindowWidth(), lfgAddon.Y + (lfgAddon.ScaledHeight - ImGui.GetWindowHeight()) / 2 - 4);
            }
            else
            {
                this.Position = new Vector2(lfgAddon.X + lfgAddon.ScaledWidth, lfgAddon.Y + (lfgAddon.ScaledHeight - ImGui.GetWindowHeight()) / 2 - 4);
            }
        }

        using (var table = ImRaii.Table("oomf", characters.Length / 4))
        {
            for (var i = 0; i < characters.Length / 8; i++)
            {
                ImGui.TableSetupColumn("" + i * 2, ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("" + i * 2 + 1, ImGuiTableColumnFlags.WidthStretch);
            }

            for (var rowIdx = 0; rowIdx < 8; rowIdx++)
            {
                ImGui.TableNextRow();

                for (var columIdx = 0; columIdx < characters.Length / 8; columIdx++)
                {
                    var _character = characters[columIdx * 8 + rowIdx];
                    ImGui.TableSetColumnIndex(columIdx * 2);

                    if (_character == null)
                    {

                        var icon = Plugin.TextureProvider.GetFromGameIcon(62574).GetWrapOrDefault();
                        if (icon != null)
                        {
                            ImGui.Image(icon.Handle, IconSize);
                        }

                        using var iconFont = ImRaii.PushFont(UiBuilder.IconFont);
                        var iconTextSize = ImGui.CalcTextSize(FontAwesomeIcon.Ban.ToIconString());

                        ImGui.SameLine((float)Math.Floor(IconSize.X / 2));
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (IconSize.Y - iconTextSize.Y) / 2);

                        ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFFFFF);
                        ImGui.Text(FontAwesomeIcon.Ban.ToIconString());
                        ImGui.PopStyleColor();

                        iconFont?.Dispose();

                        continue;
                    }

                    var character = _character.Value;
                    var jobIcon = Plugin.TextureProvider.GetFromGameIcon(character.JobIcon).GetWrapOrDefault();

                    if (jobIcon != null)
                    {
                        ImGui.Image(jobIcon.Handle, IconSize);
                    }

                    var cursorStart = ImGui.GetItemRectMin();
                    var tooltipHitboxHeight = ImGui.GetItemRectMax().Y;

                    ImGui.TableSetColumnIndex(columIdx * 2 + 1);

                    if (character.Name != null)
                    {
                        var nameText = $"{character.Name}{(character.oldNames != null ? "*" : "")} "; // trailing space is on purpose, easier than padding :3

                        ImGuiHelpers.GetButtonSize(nameText);

                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ImGui.GetItemRectSize().Y - ImGui.CalcTextSize(nameText).Y - ImGui.GetStyle().ItemInnerSpacing.Y) / 2);

                        ImGui.PushStyleColor(ImGuiCol.Text, character.Name == "Empty" ? 0x80FFFFFF : 0xFFFFFFFF);
                        ImGui.Text(nameText);
                        ImGui.PopStyleColor();

                        if (character.oldNames != null && ImGui.IsMouseHoveringRect(cursorStart, new Vector2(ImGui.GetItemRectMax().X, tooltipHitboxHeight), false))
                        {
                            ImGui.SetTooltip(String.Join("\n", character.oldNames));
                        }
                    }
                    else
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ImGui.GetItemRectSize().Y - ImGui.CalcTextSize("???").Y - ImGui.GetStyle().ItemInnerSpacing.Y) / 2);
                        ImGui.TextUnformatted("???");
                    }
                }
            }
        }
    }
}
