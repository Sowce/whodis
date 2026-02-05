using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
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
    public string? DutyName = null;
    private readonly Plugin plugin;
    private static readonly Vector2 IconSize = new Vector2(33, 33);
    //private ISharedImmediateTexture? lfgNone;
    private Vector2? lfgNoneUv0;
    private Vector2? lfgNoneUv1;
    private readonly ISharedImmediateTexture tomestoneLogo;

    public MainWindow(Plugin plugin, string tomestoneLogoPath)
        : base("##whodis",
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoBringToFrontOnFocus
            | ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoNav, true)
    {
        this.Size = Vector2.Zero;
        this.RespectCloseHotkey = false;
        this.AllowClickthrough = true;

        this.tomestoneLogo = Plugin.TextureProvider.GetFromFile(tomestoneLogoPath);

        this.plugin = plugin;
    }

    public override bool DrawConditions()
    {
        var lfgAddon = Plugin.GameGui.GetAddonByName("LookingForGroupDetail");

        return characters != null && !characters.All(chr => !chr.HasValue) && lfgAddon != null && lfgAddon.IsVisible;
    }

    public void Dispose() { }

    public override void OnClose()
    {
        DutyName = null;
        base.OnClose();
    }

    public unsafe override void Draw()
    {
        var lfgAddon = Plugin.GameGui.GetAddonByName("LookingForGroupDetail");

        if (DutyName != null)
        {
            ImGui.Text(DutyName);
            ImGui.Separator();
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

        using (var table = ImRaii.Table("oomf", characters!.Length / 4))
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
                    var cursorStart = ImGui.GetCursorPos();

                    if (_character == null)
                    {
                        var icon = Plugin.TextureProvider.GetFromGameIcon(62574).GetWrapOrDefault();
                        if (icon != null)
                        {
                            ImGui.Image(icon.Handle, IconSize);
                        }
                        else
                        {
                            ImGui.Dummy(IconSize);
                        }


                        var lfgNone = Plugin.TextureProvider.GetFromGame("ui/uld/LFG_hr1.tex");
                        if (lfgNone.TryGetWrap(out var lfgTex, out var _))
                        {
                            ImGui.SetCursorPos(cursorStart);
                            if (!lfgNoneUv0.HasValue || !lfgNoneUv1.HasValue)
                            {
                                lfgNoneUv0 = new Vector2(0f / lfgTex.Width, 272f / lfgTex.Height);
                                lfgNoneUv1 = new Vector2(56f / lfgTex.Width, 328f / lfgTex.Height);
                            }

                            ImGui.SetCursorPos(ImGui.GetCursorPos() + IconSize * 0.05f);

                            ImGui.Image(
                                lfgTex.Handle,
                                IconSize * 0.90f,
                                lfgNoneUv0.Value,
                                lfgNoneUv1.Value
                            );

                            ImGui.SetCursorPos(ImGui.GetCursorPos() - IconSize * 0.05f);
                        }

                        continue;
                    }

                    var character = _character.Value;
                    var jobIcon = Plugin.TextureProvider.GetFromGameIcon(character.JobIcon).GetWrapOrDefault();

                    if (jobIcon != null)
                    {
                        ImGui.Image(jobIcon.Handle, IconSize);
                    }
                    else
                    {
                        ImGui.Dummy(IconSize);
                    }

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

                        if (ImGui.IsMouseHoveringRect(cursorStart, new Vector2(ImGui.GetItemRectMax().X, tooltipHitboxHeight), false))
                        {
                            if (character.oldNames != null)
                            {
                                ImGui.SetTooltip(String.Join("\n", character.oldNames));
                            }

                            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            {
                                ImGui.SetClipboardText(character.Name);
                            }
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
