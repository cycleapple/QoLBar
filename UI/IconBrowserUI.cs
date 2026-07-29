using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace QoLBar;

public static class IconBrowserUI
{
    public static bool iconBrowserOpen = false;
    public static bool doPasteIcon = false;
    public static int pasteIcon = 0;

    private static bool _tabExists = false;
    private static int _i, _columns;
    private static string _name;
    private static float _iconSize;
    private static string _tooltip;
    private static bool _useLowQuality = false;
    private static List<(int, int)> _iconList;
    private static bool _displayOutsideMain = true;

    private const int iconMax = 250_000;
    private static HashSet<int> _iconExistsCache;
    private static readonly Dictionary<string, List<int>> _iconCache = new();

    public static void ToggleIconBrowser() => iconBrowserOpen = !iconBrowserOpen;

    public static void Draw()
    {
        if (!ImGuiEx.SetBoolOnGameFocus(ref _displayOutsideMain)) return;

        if (!iconBrowserOpen) { doPasteIcon = false; return; }

        var iconSize = 48 * ImGuiHelpers.GlobalScale;
        ImGui.SetNextWindowSizeConstraints(new Vector2((iconSize + ImGui.GetStyle().ItemSpacing.X) * 11 + ImGui.GetStyle().WindowPadding.X * 2 + 8), ImGuiHelpers.MainViewport.Size); // whyyyyyyyyyyyyyyyyyyyy
        ImGui.Begin("Icon Browser", ref iconBrowserOpen);

        ImGuiEx.ShouldDrawInViewport(out _displayOutsideMain);

        if (ImGuiEx.AddHeaderIconButton("RebuildIconCache", TextureDictionary.FrameIconID + 105, 1.0f, Vector2.Zero, 0, 0xFFFFFFFF, "nhg"))
            BuildCache(true);

        if (ImGui.BeginTabBar("Icon Tabs", ImGuiTabBarFlags.NoTooltip))
        {
            BeginIconList(" ★ ", iconSize);
            AddIcons(0, 100, "系統");
            AddIcons(62_000, 62_600, "職業");
            AddIcons(62_800, 62_900, "套裝列表");
            AddIcons(66_000, 66_400, "巨集");
            AddIcons(90_000, 100_000, "公會徽章／符號");
            AddIcons(114_000, 114_100, "New Game+");
            AddIcons(TextureDictionary.FrameIconID, TextureDictionary.FrameIconID + 3000, "其他");
            EndIconList();

            BeginIconList("自訂", iconSize);
            ImGuiEx.SetItemTooltip("將圖片放入「%%AppData%%\\XIVLauncher\\pluginConfigs\\QoLBar\\icons」\n" +
                                   "即可載入為可用圖示；檔名格式必須為「#.副檔名」（# > 0）。\n" +
                                   "例如「1.jpg」「2.png」「3.png」「732487.jpg」等。");
            if (_tabExists)
            {
                if (ImGui.Button("重新整理自訂圖示"))
                    QoLBar.Plugin.AddUserIcons();
                ImGui.SameLine();
                if (ImGui.Button("開啟圖示資料夾"))
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = QoLBar.Config.GetPluginIconPath(),
                        UseShellExecute = true
                    });
            }
            foreach (var kv in QoLBar.GetUserIcons())
                AddIcons(kv.Key, kv.Key + 1);
            _tooltip = "";
            EndIconList();

            BeginIconList("Misc", iconSize);
            AddIcons(60_000, 61_000, "UI");
            AddIcons(61_200, 61_250, "標記");
            AddIcons(61_290, 61_390, "標記 2");
            AddIcons(61_390, 62_000, "UI 2");
            AddIcons(62_600, 62_620, "高解析度公會旗幟");
            AddIcons(63_900, 64_000, "地圖標記");
            AddIcons(64_500, 64_600, "貼圖");
            AddIcons(65_000, 65_900, "貨幣");
            AddIcons(76_300, 78_000, "集體動作");
            AddIcons(180_000, 180_060, "貼圖／陸行鳥競賽");
            EndIconList();

            BeginIconList("Misc 2", iconSize);
            AddIcons(62_900, 63_200, "成就／討伐筆記");
            AddIcons(65_900, 66_000, "釣魚");
            AddIcons(66_400, 66_500, "標籤");
            AddIcons(67_000, 68_000, "時尚品鑑");
            AddIcons(71_000, 71_500, "任務");
            AddIcons(72_000, 72_500, "BLU UI");
            AddIcons(72_500, 76_000, "Bozja UI");
            AddIcons(76_000, 76_200, "麻將");
            AddIcons(80_000, 80_200, "任務日誌");
            AddIcons(80_730, 81_000, "古武日誌");
            AddIcons(83_000, 84_000, "公會階級");
            EndIconList();

            BeginIconList("Actions", iconSize);
            AddIcons(100, 4_000, "職業");
            AddIcons(5_100, 8_000, "特性");
            AddIcons(8_000, 9_000, "時尚");
            AddIcons(9_000, 10_000, "PvP");
            AddIcons(61_100, 61_200, "活動");
            AddIcons(61_250, 61_290, "副本／討伐殲滅戰");
            AddIcons(64_000, 64_200, "情感動作");
            AddIcons(64_200, 64_325, "FC");
            AddIcons(64_325, 64_500, "情感動作 2");
            AddIcons(64_600, 64_800, "Eureka");
            AddIcons(64_800, 65_000, "NPC");
            AddIcons(70_000, 70_200, "陸行鳥競賽");
            EndIconList();

            BeginIconList("Mounts & Minions", iconSize);
            AddIcons(4_000, 4_400, "坐騎");
            AddIcons(4_400, 5_100, "寵物");
            AddIcons(59_000, 59_400, "更多坐騎");
            AddIcons(59_400, 60_000, "寵物道具");
            AddIcons(68_000, 68_400, "坐騎一覽");
            AddIcons(68_400, 69_000, "寵物一覽");
            EndIconList();

            BeginIconList("Items", iconSize);
            AddIcons(20_000, 30_000, "一般");
            AddIcons(50_000, 54_400, "房屋");
            AddIcons(58_000, 59_000, "時尚");
            EndIconList();

            BeginIconList("Equipment", iconSize);
            AddIcons(30_000, 50_000, "裝備");
            AddIcons(54_400, 58_000, "特殊裝備");
            EndIconList();

            BeginIconList("Aesthetics", iconSize);
            AddIcons(130_000, 142_000);
            EndIconList();

            BeginIconList("Statuses", iconSize);
            AddIcons(10_000, 20_000);
            EndIconList();

            BeginIconList("Garbage", iconSize, true);
            AddIcons(61_000, 61_100, "標誌");
            AddIcons(62_620, 62_800, "世界地圖");
            AddIcons(63_200, 63_900, "區域地圖");
            AddIcons(66_500, 67_000, "栽培筆記");
            AddIcons(69_000, 70_000, "坐騎／寵物足跡");
            AddIcons(70_200, 71_000, "生產／採集筆記");
            AddIcons(76_200, 76_300, "粉絲節");
            AddIcons(78_000, 80_000, "釣魚筆記");
            AddIcons(80_200, 80_730, "筆記");
            AddIcons(81_000, 82_060, "筆記 2");
            AddIcons(84_000, 85_000, "怪物狩獵");
            AddIcons(85_000, 90_000, "UI 3");
            AddIcons(150_000, 170_000, "教學");
            //AddIcons(170_000, 180_000, "Placeholder"); // TODO: 170k - 180k are blank placeholder files, check if they get used in EW
            EndIconList();

            BeginIconList("Spoilers", iconSize, true);
            AddIcons(82_100, 83_000, "九宮幻卡"); // Out of order because people might want to use these
            AddIcons(82_060, 82_100, "親信戰友");
            AddIcons(120_000, 130_000, "彈出文字");
            AddIcons(142_000, 150_000, "日文彈出文字");
            AddIcons(180_060, 180_100, "親信戰友名稱");
            AddIcons(181_000, 181_500, "首領名稱");
            AddIcons(181_500, iconMax, "預留位置");
            EndIconList();

            BeginIconList("Spoilers 2", iconSize, true);
            AddIcons(71_500, 72_000, "製作人員名單");
            AddIcons(100_000, 114_000, "任務圖片");
            AddIcons(114_100, 120_000, "New Game+");
            EndIconList();

            ImGui.EndTabBar();
        }
        ImGui.End();

        if (iconBrowserOpen) return;
        QoLBar.CleanTextures(false);
    }

    private static bool BeginIconList(string name, float iconSize, bool useLowQuality = false)
    {
        _tooltip = "Contains:";
        if (ImGui.BeginTabItem(name))
        {
            _name = name;
            _tabExists = true;
            _i = 0;
            _columns = (int)((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X) / (iconSize + ImGui.GetStyle().ItemSpacing.X)); // WHYYYYYYYYYYYYYYYYYYYYY
            _iconSize = iconSize;
            _iconList = new List<(int, int)>();

            if (useLowQuality)
                _useLowQuality = true;
        }
        else
        {
            _tabExists = false;
        }

        return _tabExists;
    }

    private static void EndIconList()
    {
        if (_tabExists)
        {
            if (!string.IsNullOrEmpty(_tooltip))
                ImGuiEx.SetItemTooltip(_tooltip);
            BuildTabCache();
            DrawIconList();
            ImGui.EndTabItem();
        }
        else if (!string.IsNullOrEmpty(_tooltip))
        {
            ImGuiEx.SetItemTooltip(_tooltip);
        }
    }

    private static void AddIcons(int start, int end, string desc = "")
    {
        _tooltip += $"\n\t{start} -> {end - 1}{(!string.IsNullOrEmpty(desc) ? ("   " + desc) : "")}";
        if (_tabExists)
            _iconList.Add((start, end));
    }

    private static void DrawIconList()
    {
        if (_columns <= 0) return;

        ImGui.BeginChild($"{_name}##IconList");

        var cache = _iconCache[_name];

        ImGuiListClipperPtr clipper;
        unsafe { clipper = new(ImGuiNative.ImGuiListClipper()); }
        clipper.Begin((cache.Count - 1) / _columns + 1, _iconSize + ImGui.GetStyle().ItemSpacing.Y);

        var iconSize = new Vector2(_iconSize);
        var settings = new ImGuiEx.IconSettings { size = iconSize };
        while (clipper.Step())
        {
            for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
            {
                var start = row * _columns;
                var end = Math.Min(start + _columns, cache.Count);
                for (int i = start; i < end; i++)
                {
                    var icon = cache[i];
                    ShortcutUI.DrawIcon(icon, settings, _useLowQuality ? "ln" : "n");
                    if (ImGui.IsItemClicked())
                    {
                        doPasteIcon = true;
                        pasteIcon = icon;
                        ImGui.SetClipboardText($"::{icon}");
                    }

                    if (ImGui.IsItemHovered())
                    {
                        var tex = QoLBar.TextureDictionary[icon];
                        if (!ImGui.IsMouseDown(ImGuiMouseButton.Right))
                            ImGui.SetTooltip($"{icon}");
                        else if (tex != null && tex.Handle != nint.Zero)
                        {
                            ImGui.BeginTooltip();
                            ImGui.Image(tex.Handle, new Vector2(700 * ImGuiHelpers.GlobalScale));
                            ImGui.EndTooltip();
                        }
                    }
                    if (_i % _columns != _columns - 1)
                        ImGui.SameLine();
                    _i++;
                }
            }
        }

        clipper.Destroy();

        ImGui.EndChild();
    }

    private static void BuildTabCache()
    {
        if (_iconCache.ContainsKey(_name)) return;
        DalamudApi.LogInfo($"Building Icon Browser cache for tab \"{_name}\"");

        var cache = _iconCache[_name] = new();
        foreach (var (start, end) in _iconList)
        {
            for (int icon = start; icon < end; icon++)
            {
                if (_iconExistsCache.Contains(icon))
                    cache.Add(icon);
            }
        }

        DalamudApi.LogInfo($"Done building tab cache! {cache.Count} icons found.");
    }

    public static void BuildCache(bool rebuild)
    {
        DalamudApi.LogInfo("Building Icon Browser cache");

        _iconCache.Clear();
        _iconExistsCache = !rebuild ? QoLBar.Config.LoadIconCache() ?? new() : new();

        if (_iconExistsCache.Count == 0)
        {
            for (int i = 0; i < iconMax; i++)
            {
                if (TextureDictionary.IconExists((uint)i))
                    _iconExistsCache.Add(i);
            }

            _iconExistsCache.Remove(125052); // Remove broken image (TextureFormat R8G8B8X8 is not supported for image conversion)

            QoLBar.Config.SaveIconCache(_iconExistsCache);
        }

        foreach (var kv in QoLBar.textureDictionaryLR.GetUserIcons())
            _iconExistsCache.Add(kv.Key);

        foreach (var kv in QoLBar.textureDictionaryLR.GetTextureOverrides())
            _iconExistsCache.Add(kv.Key);

        DalamudApi.LogInfo($"Done building cache! {_iconExistsCache.Count} icons found.");
    }
}
