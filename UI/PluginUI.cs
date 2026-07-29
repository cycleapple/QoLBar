using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using Newtonsoft.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace QoLBar;

public class PluginUI : IDisposable
{
    public bool IsVisible => true;

    public readonly List<BarUI> bars;

#if DEBUG
    public bool configOpen = true;
#else
        public bool configOpen = false;
#endif
    public void ToggleConfig() => configOpen = !configOpen;

    private bool lastConfigPopupOpen = false;
    private bool configPopupOpen = false;
    public bool IsConfigPopupOpen() => configPopupOpen || lastConfigPopupOpen;
    public void SetConfigPopupOpen() => configPopupOpen = true;

    public static readonly Vector2 defaultSpacing = new(8, 4);

    private bool _displayOutsideMain = true;

    public static Vector2 mousePos = ImGui.GetMousePos();

    public PluginUI()
    {
        bars = new List<BarUI>();
        for (int i = 0; i < QoLBar.Config.BarCfgs.Count; i++)
            bars.Add(new BarUI(i));

        if (!QoLBar.Config.FirstStart) return;

        AddBar(new BarCfg { Editing = true });
        AddDemoBar();
    }

    public void Reload()
    {
        Dispose();

        bars.Clear();
        for (int i = 0; i < QoLBar.Config.BarCfgs.Count; i++)
            bars.Add(new BarUI(i));
    }

    public void Draw()
    {
        if (!IsVisible) return;

        mousePos = ImGui.GetMousePos();

        IconBrowserUI.Draw();

        if (QoLBar.Config.AlwaysDisplayBars || QoLBar.IsLoggedIn())
        {
            foreach (var bar in bars)
                bar.Draw();
        }
        lastConfigPopupOpen = configPopupOpen;
        configPopupOpen = false;

        if (configOpen)
            DrawPluginConfig();

        PieUI.Draw();
        ImGuiEx.DoSlider();
    }

    private void DrawPluginConfig()
    {
        if (!ImGuiEx.SetBoolOnGameFocus(ref _displayOutsideMain)) return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(610, 650) * ImGuiHelpers.GlobalScale, ImGuiHelpers.MainViewport.Size);
        ImGui.Begin("QoL Bar 設定", ref configOpen);

        ImGuiEx.ShouldDrawInViewport(out _displayOutsideMain);

        if (ImGui.BeginTabBar("Config Tabs"))
        {
            if (ImGui.BeginTabItem("快捷列管理"))
            {
                DrawBarManager();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("條件組"))
            {
                ConditionSetUI.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("備份"))
            {
                DrawBackupManager();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("設定"))
            {
                DrawSettingsMenu();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("偵錯"))
            {
                DrawDebugMenu();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private void DrawBarManager()
    {
        Vector2 textsize = new(-1, 0);
        float textx = 0.0f;

        var letterButtonSize = new Vector2(ImGui.CalcTextSize("O").X + ImGui.GetStyle().FramePadding.X * 2, 0);

        ImGui.Columns(3, "QoLBarsList", false);
        for (int i = 0; i < bars.Count; i++)
        {
            ImGui.PushID(i);

            var bar = QoLBar.Config.BarCfgs[i];

            ImGui.Text($"#{i + 1}");
            ImGui.SameLine();

            textx = ImGui.GetCursorPosX();

            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##Name", ref bar.Name, 32))
                QoLBar.Config.Save();

            textsize = ImGui.GetItemRectSize();

            ImGui.NextColumn();

            if (ImGui.Button("O", letterButtonSize))
                ImGui.OpenPopup($"BarConfig##{i}");
            ImGuiEx.SetItemTooltip("選項");
            bars[i].DrawConfig();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(27 * ImGuiHelpers.GlobalScale);
            if (ImGui.Button(bar.Hidden ? "R" : "H", letterButtonSize))
                bars[i].IsHidden = !bars[i].IsHidden;
            ImGuiEx.SetItemTooltip(bar.Hidden ? "顯示" : "隱藏");
            ImGui.SameLine();
            var preview = ((bar.ConditionSet >= 0) && (bar.ConditionSet < QoLBar.Config.CndSetCfgs.Count)) ? $"[{bar.ConditionSet + 1}] {QoLBar.Config.CndSetCfgs[bar.ConditionSet].Name}" : "條件組";
            if (ImGui.BeginCombo("##Condition", preview))
            {
                if (ImGui.Selectable("無", bar.ConditionSet == -1))
                {
                    bar.ConditionSet = -1;
                    QoLBar.Config.Save();
                }
                for (int idx = 0; idx < QoLBar.Config.CndSetCfgs.Count; idx++)
                {
                    if (ImGui.Selectable($"[{idx + 1}] {QoLBar.Config.CndSetCfgs[idx].Name}", idx == bar.ConditionSet))
                    {
                        bar.ConditionSet = idx;
                        QoLBar.Config.Save();
                    }
                }
                ImGui.EndCombo();
            }
            ImGuiEx.SetItemTooltip("套用條件組，以控制這個快捷列顯示的時機。\n" +
                                   "適合讓多個快捷列在相同條件下同時顯示。\n" +
                                   "可在此視窗上方的「條件組」分頁建立條件組。");

            ImGui.NextColumn();

            if (ImGui.Button("↑"))
                ShiftBar(i, false);
            ImGui.SameLine();
            if (ImGui.Button("↓"))
                ShiftBar(i, true);
            ImGui.SameLine();
            if (ImGui.Button("匯出"))
                ImGui.SetClipboardText(ExportBar(i, false));
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("以精簡設定匯出至剪貼簿（可能隨更新變動）。\n" +
                                 "按右鍵可匯出全部設定（字串較長，格式不變）。");

                if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                    ImGui.SetClipboardText(ExportBar(i, true));
            }

            if (bars.Count > 1)
            {
                ImGui.SameLine();
                if (ImGui.Button(QoLBar.Config.ExportOnDelete ? "剪下" : "刪除"))
                    ConfigEditorUI.DisplayRightClickDeleteMessage();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"在此按鈕按右鍵即可刪除快捷列 #{i + 1}！" +
                                     (QoLBar.Config.ExportOnDelete ? "\n刪除前會先將快捷列匯出至剪貼簿。" : ""));

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                    {
                        ImGui.ClearWindowFocus(); // Kill focus to prevent ImGui from overwriting text box on deletes
                        RemoveBar(i);
                    }
                }
            }

            ImGui.Separator();
            ImGui.NextColumn();

            ImGui.PopID();
        }

        ImGui.Spacing();
        ImGui.SameLine(textx);
        if (ImGui.Button("+", textsize))
            AddBar(new BarCfg { Editing = true });
        ImGui.NextColumn();
        ImGui.NextColumn();
        if (ImGui.Button("匯入", textsize))
            ImportBar(ImGuiEx.TryGetClipboardText());
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("從剪貼簿匯入快捷列，或將單一捷徑匯入為新快捷列。\n" +
                             "按右鍵可新增展示各項功能的範例快捷列。");

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                AddDemoBar();
        }

        ImGui.Columns(1);
    }

    private void DrawSettingsMenu()
    {
        var halfWidth = ImGui.GetWindowWidth() / 2;
        var thirdWidth = ImGui.GetWindowWidth() / 3;
        var quarterWidth = ImGui.GetWindowWidth() / 4;
        if (ImGui.Checkbox("刪除時匯出", ref QoLBar.Config.ExportOnDelete))
            QoLBar.Config.Save();
        ImGui.SameLine(halfWidth);
        if (ImGui.Checkbox("永遠顯示快捷列", ref QoLBar.Config.AlwaysDisplayBars))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip("即使尚未登入角色，快捷列仍會保持顯示。");

        if (ImGui.Checkbox("圖示使用熱鍵欄邊框", ref QoLBar.Config.UseIconFrame))
            QoLBar.Config.Save();
        ImGui.SameLine(halfWidth);
        var _ = QoLBar.Config.UseHRIcons;
        if (ImGui.Checkbox("使用高解析度圖示", ref _))
        {
            QoLBar.Config.UseHRIcons = _;
            QoLBar.Config.Save();
        }
        ImGuiEx.SetItemTooltip("改為載入高解析度圖示。請注意：若在圖示瀏覽器開啟「劇透」分頁，\n" +
                               "關閉前可能會使用多達 5 GB 記憶體！");

        if (ImGui.Checkbox("停用條件快取", ref QoLBar.Config.NoConditionCache))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip("取消條件檢查之間的 100 毫秒延遲，但會提高 CPU 負載。");
        ImGui.SameLine(halfWidth);
        ImGui.SetNextItemWidth(quarterWidth);
        if (ImGui.InputFloat("快捷列字型大小", ref QoLBar.Config.FontSize, 1, 8, "%.f", ImGuiInputTextFlags.EnterReturnsTrue))
        {
            QoLBar.Config.FontSize = Math.Min(Math.Max(QoLBar.Config.FontSize, 1), QoLBar.MaxFontSize);
            QoLBar.Config.Save();
            QoLBar.SetupFont();
        }
        ImGuiEx.SetItemTooltip($"預設：{QoLBar.DefaultFontSize}");

        ImGui.SetNextItemWidth(quarterWidth);
        if (ImGui.InputInt("備份間隔", ref QoLBar.Config.BackupTimer))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip("從上次儲存後經過多少分鐘進行備份。設為 0 可停用。");

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted("圓盤選單設定");
        ImGui.SetNextItemWidth(quarterWidth);
        if (ImGui.DragInt("不透明度", ref QoLBar.Config.PieOpacity, 0.2f, 0, 255))
            QoLBar.Config.Save();
        ImGui.SameLine(halfWidth);
        if (ImGui.Checkbox("交錯排列角度", ref QoLBar.Config.PieAlternateAngle))
            QoLBar.Config.Save();
        /*ImGui.SetNextItemWidth(quarterWidth);
        var offset = (float)(QoLBar.Config.PieAngleOffset / Math.PI * 180);
        if (ImGui.DragFloat("Pie Angle Offset", ref offset, 0.1f, -10, 370, "%.1f"))
        {
            offset = (offset % 360 + 360) % 360;
            QoLBar.Config.PieAngleOffset = (float)(offset / 180f * Math.PI);
            QoLBar.Config.Save();
        }*/

        if (ImGui.Checkbox("顯示於畫面中央", ref QoLBar.Config.PiesAlwaysCenter))
        {
            if (!QoLBar.Config.PiesAlwaysCenter)
            {
                QoLBar.Config.PiesMoveMouse = false;
                QoLBar.Config.PiesReturnMouse = false;
                QoLBar.Config.PiesReadjustMouse = false;
            }
            QoLBar.Config.Save();
        }
        ImGui.SameLine(halfWidth);
        if (QoLBar.Config.PiesAlwaysCenter && ImGui.Checkbox("開啟時將滑鼠移至中央", ref QoLBar.Config.PiesMoveMouse))
        {
            if (!QoLBar.Config.PiesMoveMouse)
            {
                QoLBar.Config.PiesReturnMouse = false;
                QoLBar.Config.PiesReadjustMouse = false;
            }
            QoLBar.Config.Save();
        }

        if (QoLBar.Config.PiesMoveMouse && ImGui.Checkbox("關閉時移回滑鼠", ref QoLBar.Config.PiesReturnMouse))
        {
            if (!QoLBar.Config.PiesReturnMouse)
                QoLBar.Config.PiesReadjustMouse = false;
            QoLBar.Config.Save();
        }
        ImGui.SameLine(halfWidth);
        if (QoLBar.Config.PiesReturnMouse && ImGui.Checkbox("重新校正原滑鼠位置", ref QoLBar.Config.PiesReadjustMouse))
            QoLBar.Config.Save();
        ImGui.SameLine();

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted("不套用下列 Dalamud 隱藏介面設定");
        if (ImGui.Checkbox("遊戲介面已關閉", ref QoLBar.Config.OptOutGameUIOffHide))
        {
            QoLBar.Config.Save();
            QoLBar.Plugin.CheckHideOptOuts();
        }
        ImGui.SameLine(thirdWidth);
        if (ImGui.Checkbox("過場動畫中", ref QoLBar.Config.OptOutCutsceneHide))
        {
            QoLBar.Config.Save();
            QoLBar.Plugin.CheckHideOptOuts();
        }
        ImGui.SameLine(thirdWidth * 2);
        if (ImGui.Checkbox("集體動作模式中", ref QoLBar.Config.OptOutGPoseHide))
        {
            QoLBar.Config.Save();
            QoLBar.Plugin.CheckHideOptOuts();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted("暫時性設定，啟用風險請自行承擔");
        ImGui.Checkbox("允許匯入條件", ref Importing.allowImportConditions);
        ImGui.SameLine(halfWidth);
        ImGui.Checkbox("允許匯入快捷鍵", ref Importing.allowImportHotkeys);
        ImGui.Checkbox("允許匯出敏感條件組", ref Importing.allowExportingSensitiveConditionSets);
        ImGuiEx.SetItemTooltip("允許匯出包含角色 ID 等個人資訊的條件組。");
    }

    private void DrawBackupManager()
    {
        var path = QoLBar.Config.GetPluginBackupPath();
        var configFile = Configuration.ConfigFile;

        if (ImGui.Button("開啟資料夾"))
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        ImGui.SameLine();
        if (ImGui.Button("建立備份"))
            BackupFile(configFile);

        ImGui.Separator();
        ImGui.Columns(3, "QoLBarBackups", false);
        if (!string.IsNullOrEmpty(path))
        {
            var i = 0;
            var directory = new DirectoryInfo(path);
            foreach (var file in directory.GetFiles())
            {
                if (file.Extension == ".json")
                {
                    ImGui.PushID(i);

                    ImGui.TextUnformatted(file.Name);
                    ImGui.NextColumn();
                    ImGui.TextUnformatted(file.LastWriteTime.ToString());
                    ImGui.NextColumn();
                    ImGui.SmallButton("載入");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"按兩下此按鈕，以 {file.Name}\n" +
                                         "覆寫並重新載入目前設定。");

                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            QoLBar.Config.LoadConfig(file);
                    }

                    ImGui.SameLine();
                    if (ImGui.SmallButton("刪除"))
                        ConfigEditorUI.DisplayRightClickDeleteMessage("按兩下右鍵即可刪除！");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"在此按鈕按兩下右鍵即可刪除 {file.Name}");

                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Right))
                            DeleteFile(file);
                    }

                    ImGui.Separator();
                    ImGui.NextColumn();

                    ImGui.PopID();
                    i++;
                }
            }
        }
        ImGui.Columns(1);
    }

    private string debug_SerializedImport = string.Empty;
    private string debug_DeserializedImport = string.Empty;
    private unsafe void DrawDebugMenu()
    {
        ImGui.TextUnformatted("遊戲資料指標");
        ImGui.Indent();
        ImGui.Columns(3, "DebugPointers", false);

        ImGui.TextUnformatted("UI Module (Text Input Active)");
        ImGui.NextColumn();
        ImGuiEx.TextCopyable($"{(nint)Game.uiModule:X}");
        ImGui.NextColumn();
        ImGui.TextUnformatted($"{Game.IsGameTextInputActive}");
        ImGui.NextColumn();

        ImGui.TextUnformatted("Rapture Shell Module");
        ImGui.NextColumn();
        ImGuiEx.TextCopyable($"{(nint)Game.raptureShellModule:X}");
        ImGui.NextColumn();
        ImGui.NextColumn();

        ImGui.TextUnformatted("Rapture Macro Module");
        ImGui.NextColumn();
        ImGuiEx.TextCopyable($"{(nint)Game.raptureMacroModule:X}");
        ImGui.NextColumn();
        ImGui.TextUnformatted($"{Game.IsMacroRunning}");
        ImGui.NextColumn();

        ImGui.TextUnformatted("Addon Config (HUD Layout #)");
        ImGui.NextColumn();
        ImGuiEx.TextCopyable($"{(nint)Game.addonConfig:X}");
        ImGui.NextColumn();
        ImGui.TextUnformatted($"{Game.CurrentHUDLayout}");
        ImGui.NextColumn();

        ImGui.TextUnformatted("Item Context Menu Agent");
        ImGui.NextColumn();
        ImGuiEx.TextCopyable($"{(nint)Game.agentInventoryContext:X}");
        ImGui.NextColumn();
        ImGui.NextColumn();

        ImGui.Columns(1);
        ImGui.Unindent();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.TreeNodeEx("匯出資料編輯器", ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.NoTreePushOnOpen))
        {
            var available = ImGui.GetContentRegionAvail();
            ImGui.TextUnformatted("序列化字串");
            ImGui.SetNextItemWidth(available.X);
            if (ImGui.InputText("##Serialized", ref debug_SerializedImport, 1000000, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.NoHorizontalScroll))
            {
                var import = Importing.TryImport(debug_SerializedImport);
                if (import.bar != null)
                    debug_DeserializedImport = JsonConvert.SerializeObject(import.bar, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects });
                else if (import.shortcut != null)
                    debug_DeserializedImport = JsonConvert.SerializeObject(import.shortcut, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects });
            }

            ImGui.TextUnformatted("反序列化字串");
            if (ImGui.InputTextMultiline("##Deserialized", ref debug_DeserializedImport, 1000000, new Vector2(available.X, available.Y / 2)))
            {
                try
                {
                    debug_SerializedImport = Importing.ExportBar(Importing.ImportObject<BarCfg>(Importing.CompressString(debug_DeserializedImport)), true);
                }
                catch
                {
                    try
                    {
                        debug_SerializedImport = Importing.ExportShortcut(Importing.ImportObject<ShCfg>(Importing.CompressString(debug_DeserializedImport)), true);
                    }
                    catch { }
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        Keybind.DrawDebug();
    }

    private void AddBar(BarCfg bar)
    {
        QoLBar.Config.BarCfgs.Add(bar);
        bars.Add(new BarUI(bars.Count));
        QoLBar.Config.Save();
    }

    private void RemoveBar(int i)
    {
        if (QoLBar.Config.ExportOnDelete)
            ImGui.SetClipboardText(Importing.ExportBar(QoLBar.Config.BarCfgs[i], false));

        bars[i].Dispose();
        bars.RemoveAt(i);
        QoLBar.Config.BarCfgs.RemoveAt(i);
        QoLBar.Config.Save();
        RefreshBarIndexes();
    }

    private void ShiftBar(int i, bool increment)
    {
        if (!increment ? i > 0 : i < (bars.Count - 1))
        {
            var j = (increment ? i + 1 : i - 1);
            var b = bars[i];
            bars.RemoveAt(i);
            bars.Insert(j, b);

            var b2 = QoLBar.Config.BarCfgs[i];
            QoLBar.Config.BarCfgs.RemoveAt(i);
            QoLBar.Config.BarCfgs.Insert(j, b2);
            QoLBar.Config.Save();
            RefreshBarIndexes();
        }
    }

    public string ExportBar(int i, bool saveAllValues) => Importing.ExportBar(QoLBar.Config.BarCfgs[i], saveAllValues);

    public void ImportBar(string import)
    {
        var imports = Importing.TryImport(import, true);
        if (imports.bar != null)
            AddBar(imports.bar);
        else if (imports.shortcut != null)
        {
            var bar = new BarCfg { Editing = true };
            bar.ShortcutList.Add(imports.shortcut);
            AddBar(bar);
        }
    }

    public void AddDemoBar()
    {
        var prev = Importing.allowImportHotkeys;
        Importing.allowImportHotkeys = true;
        ImportBar("H4sIAAAAAAAEALVa/W7byBF/lTVVIFecLJOURH2gKJAoyTlFAwd2DodrnT9W5Ercmtxld5dWdIcDDu0D9QX6THmGzuySlGgrsS26QCKL5HK+d+Y3s/rV+4PZFsybe8zre8vQm//a3IGrvifgy+nrN+8vTuFC/9Wb/323QNcL8Dl59fIS" +
                  "Lo03D/tenL305kHf4xfwgj/w+/D/E1xeenO88uKrAh5M+6NPv/UP0HtHaE4oMeyzITE1bC3V1tEOvi7EQmZSscS+1et9dO8KsmQkrp78o9SGZPyGEbjPYyn25PXmo3A2mgXDyex4uT8opjXJGVlJRfIyM7zIGGFxKjWsiGHFGV6Qc5Zl8lq4C5" +
                  "MyxU687kyNtKxI0CdhnwwJF0SqhKkW66BmG9ZfhvA87+KtltaUKCoSmVtRWpx/liXZcHFS88XrTGo2GAycBOHREvR6VcQsMypuyLI0xjq3W/jpclkHX693Lm+Z4mJNZMEEatsnW1AAI4xmWpLCGmFhVEa+J1fgC9kE7A18DoNgPP567M7nke9P" +
                  "gzvxOJmG4/Ew8o9W5AcpIerl8qQh/AhCn76x6if4An8W7k/zDnw/9+ZGleygGG8+s7g0DAIy4bc8KWlGchorSXp+HSFn5GPKNcRHlhH29eUQO2e5f7xna0l0SjEhPEaKO0utBIH/DDLEkI1gn1jCdwSAf7T13HKFD3uRyfgGvm8oN7vtbFMK+R" +
                  "PeHAR/bucW8uXfv9cU7O1XW1JqDOZrD+96oLFJZWmAqyjzJVO74NYFi/lqSzYpj1NSUGU0kSskDGk1z2Gv26UbWWaJy62QhlQJuwJ1cFbjQhtGk0fmuAfiL/Tt8gsXb4/cyy7dk912/tGqjzd1LZ5VC6sGWJ9RvT0hf8F6oZmx2grqUuw1bNYA" +
                  "TAa5Tku4JdHGldkerFHz+SqY9noXkEMs0ZXiTKDdtKlDwN2q7hwZYsAmGvlB0Ou9piJmNekEL0guDe+SHVEHVOK9LIUhChzPjGl40Bgs5B5dVo+uvW6aBMMRaPKCrMgLQtW6zBkQ/26lwCF/bFWYl5Wbm0VryTQAALNhYG90HPgNI9b603oMPZ" +
                  "rLBCM8lRv7wJKAGEi4LjK6ZUkX6QVKH6L0oiW9kKRR4GjiGRIfIvGsRTwDTaAcyaxET3dikSKLCbJIWyxSvk6fi8caeYyQx7rFY63oVsc062YkhdTHSF21qCsGBV0zckZWGS86sTiF8Kzz9gYzbp27bSj1gio70qJgVBG+shmTJgmUFSlYK4QX" +
                  "KYtvdmG4VHKjmXqhIXoXliZEsKFLDNsMiAkXtRKp4Vt5FzUiwCABQqkKNlOxddDZZXgqbI7aIZMoGHfndgFbEBCjhsqCbgHcdFJxCf2h74+GwbATF3+MO+Tnqpr9ItErwlaiv3nz8eB4bDWfz/wwGO9o65SvjE0vShooNM6LOV3vg/twCmzCsO" +
                  "EzmY6Hs2n4GE2m0KDsuJXaMsBPuWa2BkEYVPqh6zZNW9FS9BR0m/Xxc9oIMRoPQY4IZHtYjGAchpiO30tXGw3UUV1HTCLLJTQ8EDExAuE44/ENdmSQY6kAHJdtQbgCcMYtzUqmK+Gme14IRla26ImOGEXg5CvZoJYEoQ/XTbxCZ+cHE0TStjOd" +
                  "OMb+YLyzymiKnMfBE4tTNAxGLoprfkA/cvRPg07x5T8FtR8mMYkg+yHaARxb0BgSnk0WGV8j/rCuw54NkofFMjZa61a7C9/OoluEtJ8Cd5Fv2y3pEBQkXAdkca+B8AIKOV6uM6oR2FU4VQnYHgDvdCqVAfANt8SKr5um/J8ys1Cw9t/0OYBqEE" +
                  "RtoApd07AmEPr9AN+Ir2AhhmH8Fl+NooPWWFS4lbxiKb3lUuleb5FSsW5UlwlDBWkDcV0TkznwmpN4G2eYMACuQeGGWxwKkTBMCeiuarPoB/FrywcxSgBloquva5FfIFZz+tV1oBPddyJWDIs9zeZdaRmmTa+Hnw/aKDi+BoctLPDRpvfGOc7o" +
                  "dVvqXFi1YLrD+Gj46K6s05Do0k6FOvnhodbKwsjOrca4E4QCAtFTDNp9JNNOMA2SgIWHhXwpIMsbqMp2Xqr38qp74CAfbMWt2+B1inFIkGZFSrGGYAOAmAOWEMCCD7onHEdQobEzuaRcLOXmuWbF4Rjak4M0j9/t4Rj627cUEM5BwsMOhGeVFd" +
                  "7ShLWpjo6nGvko7l2Cx+PmMAoq/e9TjTpQhUb4EiLvPtFJB6II73H4dcimjyvkh+kCrPtB4eDgPtlZB7LQMSy29BDV4HiwGEawu14Bpj5EtsPeimBvfSgVnmIcINxhg0WwwX5KublHF9H6dOqPZ9D91Xw67Ldohnah0FMfkP/4HWfn9ZPAJc8c" +
                  "x4R3GhzXmrk2B1Ppoc7oQGb9qiEeVyQezE44Px19HZV+Y2YNTTp5J1aSfPnXfx5M9gspEo5zIXLFDJSYN4Ki9tgbJ1zb70uKNYZqOwMBQykuS000Ns66Xw9dc1IWNYq/vkO1GoRI97TISqhUFa7vCvbOpblhW5S7Alt01znUk/LGg9z09w8WSS" +
                  "oN6DYg7xCNL3Heo2ulkz6s1rgCyROqGE7JN5C8Bl1FfvO5AAFBHigveGDh7IsG3+FHrOj7cXddvQWWdCdmOLwShKGR3ZK8MwTPG7HcV9tn2s7TiiML265BOmSf3WOj3A07zhI7ga8rWnhYYR8oRq2Yzsg4ja8U7SrzK8gWZaFxpMeqgLIHMqWR" +
                  "gI94bHf5ktrJBoSnlWYvBLElLRJEWLtDlExSN54jKyXzyvgVn/9jHL9my3JN3jNRYruIg8Wm5eOwlRWqA3z3ZLJvfEMi2JllnNqzndjwW1bHcldJP3BIk2eAthIOwqHEuj3qcl62Z0oFh34XVmAA0SRxMeDkwFyBO6yeSz2DWOSSYUzGaCmQyQ" +
                  "pqW+vqeABDAOBwELpQtbltt+UKgMgY0H0b0W7p0N7R5LuwbtaRZsKYHUJ3kndRncjZs0HMBLUcYEYhjbWkO5Jzx3o8bjrIxrHXnn372uuD6xkkaHea1z7Ks2mwPj3sKvUVygJuh3ZDQVjhkOrL7//d+V9Bq1ElXAxKexB31pw+2nprx1ogG8MU" +
                  "co/cUwT8Vqu13199e3K1sLulVLQ6aqunTBDEx7eX5zxhbublyNREb7nmFlysVsT9VKc+5/1RpPvvHKgAl7DZBR56GlndqSh4Lq0eyAH4I45EbvqYczGgrttCiFqGR576we3EQhBqf1SwBIgyhc6lcOPyWTQZjcbw+nA4DWajCYIcad2gq8HZ6s" +
                  "oNzuCOHaz1Q7Ced4vgbxAMgJv32/8AKNeQw+UkAAA=");
        Importing.allowImportHotkeys = prev;
    }

    private void RefreshBarIndexes()
    {
        for (int i = 0; i < bars.Count; i++)
            bars[i].ID = i;
    }

    public void SetBarHidden(int i, bool toggle, bool b = false)
    {
        if (i < 0 || i >= bars.Count)
            QoLBar.PrintError($"快捷列 #{i + 1} 不存在。");
        else
        {
            if (toggle)
                bars[i].IsHidden = !bars[i].IsHidden;
            else
                bars[i].IsHidden = b;
        }
    }

    public void SetBarHidden(string name, bool toggle, bool b = false)
    {
        var found = false;
        for (int i = 0; i < bars.Count; ++i)
        {
            if ((QoLBar.Config.BarCfgs[i]).Name == name)
            {
                found = true;
                SetBarHidden(i, toggle, b);
            }
        }
        if (!found)
            QoLBar.PrintError($"快捷列「{name}」不存在。");
    }

    private void BackupFile(FileInfo file, string name = "", bool overwrite = false)
    {
        try
        {
            if (file.Extension != ".json")
                throw new InvalidOperationException("File must be json!");

            if (string.IsNullOrEmpty(name))
                name = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");

            var path = QoLBar.Config.GetPluginBackupPath() + $"\\{name}.json";
            file.CopyTo(path, overwrite);
            DalamudApi.LogInfo($"Saved file to {path}");
        }
        catch (Exception e)
        {
            QoLBar.PrintError($"儲存失敗：{e.Message}");
        }
    }

    private void DeleteFile(FileInfo file)
    {
        try
        {
            if (file.Extension != ".json")
                throw new InvalidOperationException("File must be json!");

            file.Delete();
            DalamudApi.LogInfo($"Deleted file {file.FullName}");
        }
        catch (Exception e)
        {
            QoLBar.PrintError($"刪除失敗：{e.Message}");
        }
    }

    public void Dispose()
    {
        foreach (var bar in bars)
            bar.Dispose();
    }

    // Helper function
    public static void DrawExternalWindow(Action draw, bool noViewport)
    {
        if (noViewport)
            ImGuiHelpers.ForceNextWindowMainViewport();
        QoLBar.Font.Pop();
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, defaultSpacing);
        ImGuiEx.PushFontSize(QoLBar.DefaultFontSize);
        draw();
        ImGuiEx.PopFontSize();
        ImGui.PopStyleVar();
        QoLBar.Font.Push();
    }
}
