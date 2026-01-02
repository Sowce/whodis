using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using Lumina.Excel.Sheets;
using Microsoft.Data.Sqlite;
using SamplePlugin.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SamplePlugin;

struct NamePullResult
{
    public string Name;
    public List<string>? OldNames;
}

public unsafe sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IDataManager DataSheets { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    private static List<ClassJob> JobSheet;

    private static SqliteConnection SQLiteConnection;

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("MaraudersMap");
    private MainWindow MainWindow { get; init; }
    private uint? baseJobIconId = null;

    public Plugin()
    {
        var dataDbPath = Path.Combine(PluginInterface.GetPluginConfigDirectory(), "..", "PlayerTrack", "data.db");

        if (!File.Exists(dataDbPath))
        {
            dataDbPath = Path.Combine(PluginInterface.GetPluginConfigDirectory(), "data.db");
        }

        if (!File.Exists(dataDbPath))
            throw new FileNotFoundException("Cannot find data source.");

        SQLiteConnection = new SqliteConnection($"Data Source={Path.GetFullPath(dataDbPath)}");

        if (SQLiteConnection == null)
            throw new FileNotFoundException("Cannot find data source.");

        JobSheet = DataSheets.GetExcelSheet<ClassJob>().ToList();

        SQLiteConnection.Open();

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(MainWindow);

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "LookingForGroupDetail", OnLookingForGroup);
        AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "LookingForGroupDetail", OnLookingForGroupClosed);
    }

    public void Dispose()
    {
        SQLiteConnection.Close();

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;

        WindowSystem.RemoveAllWindows();

        AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "LookingForGroupDetail");
        AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, "LookingForGroupDetail");

        MainWindow.Dispose();
    }

    private void OnLookingForGroupClosed(AddonEvent type, AddonArgs args)
    {
        var lfgDetail = GameGui.GetAddonByName<AddonLookingForGroupDetail>("LookingForGroupDetail");
        if (lfgDetail != null && lfgDetail->IsVisible)
            return;
        MainWindow.IsOpen = false;
    }

    private void OnLookingForGroup(AddonEvent type, AddonArgs args)
    {
        if (null == baseJobIconId && PlayerState != null)
        {
            baseJobIconId = (uint)PartyListNumberArray.Instance()->PartyMembers[0].ClassIconId - PlayerState.ClassJob.RowId;
        }

        AgentLookingForGroup.Detailed lfg = AgentLookingForGroup.Instance()->LastViewedListing;
        var _charList = new CharacterRow?[lfg.NumberOfParties * 8];


        for (int i = 0; i < lfg.NumberOfParties * 8; i++)
        {
            if (lfg.SlotFlags[i] == 0)
            {
                continue;
            }

            if (lfg.MemberContentIds[i] == 0)
            {
                _charList[i] = new CharacterRow()
                {
                    Name = "Empty",
                    JobIcon = 62145,
                    Party = (byte)Math.Floor((decimal)i / 8),
                };
                continue;
            }

            var cachedName = GetNameFromContentID(lfg.MemberContentIds[i]);

            if (lfg.MemberContentIds[i] == lfg.LeaderContentId)
            {
                if (cachedName == null)
                {
                    cachedName = new NamePullResult() { Name = lfg.LeaderString };
                }
                else if (cachedName.Value.Name != lfg.LeaderString)
                {
                    cachedName = new NamePullResult()
                    {
                        Name = lfg.LeaderString,
                        OldNames = cachedName.Value.OldNames != null ?
                            new List<string>() { cachedName.Value.Name }.Concat(cachedName.Value.OldNames).ToList()
                            : new List<string>() { cachedName.Value.Name }
                    };
                }
            }

            _charList[i] = new CharacterRow()
            {
                JobIcon = GetJobIconId(lfg.Jobs[i]),
                Name = cachedName?.Name ?? "???",
                oldNames = cachedName?.OldNames,
                Party = (byte)Math.Floor((decimal)i / 8),
            };
        }

        MainWindow.characters = _charList;

        if (_charList.All(chr => !chr.HasValue))
        {
            MainWindow.IsOpen = false;
            return;
        }

        MainWindow.IsOpen = true;
    }

    private uint GetJobIconId(uint JobId)
    {
        if (baseJobIconId == null)
            return 62100 + JobId;
        return (uint)baseJobIconId + JobId;
    }

    private NamePullResult? GetNameFromContentID(ulong contentId)
    {
        if (contentId == 0)
            return null;

        if (contentId == PlayerState.ContentId)
        {
            return new NamePullResult() { Name = PlayerState.CharacterName, OldNames = null };
        }

        using var command = SQLiteConnection.CreateCommand();
        command.CommandText = "SELECT id,name FROM players WHERE content_id=@cid";
        command.Parameters.AddWithValue("@cid", contentId);

        var reader = command.ExecuteReader();

        if (!reader.HasRows)
            return null;

        reader.Read();
        var pid = reader.GetInt64(0);
        var returnValue = reader.GetString(1);

        reader.Close();

        command.CommandText = "SELECT player_name FROM player_name_world_histories WHERE player_id=@pid";
        command.Parameters.AddWithValue("@pid", pid);

        reader = command.ExecuteReader();

        if (!reader.HasRows)
            return new NamePullResult() { Name = returnValue, OldNames = null };

        var names = new List<string>();

        while (reader.Read())
        {
            var name = reader.GetString(0);
            if (returnValue != name
                && !names.Contains(name)
                && !JobSheet.Any(job => job.Name.ToString().ToLower() == name.ToLower())
                )
                names.Add(name);
        }

        reader.Close();

        if (names.Count > 0 && JobSheet.Any(job => job.Name.ToString().ToLower() == returnValue.ToLower()))
        {
            returnValue = names[0];
            names.RemoveAt(0);
        }

        return new NamePullResult() { Name = returnValue, OldNames = names.Count > 0 ? names : null };
    }
}
