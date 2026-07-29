using System;
using System.Numerics;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using static QoLBar.BarCfg;
using static QoLBar.ShCfg;

namespace QoLBar;

// I hate this file with a passion
public static class ConfigEditorUI
{
    private static int _inputPos = 0;
    private static unsafe int GetCursorPosCallback(ImGuiInputTextCallbackDataPtr data)
    {
        _inputPos = data.CursorPos;
        return 0;
    }

    private static void DrawInsertPrivateCharPopup(ref string input)
    {
        if (ImGui.BeginPopup($"Private Use Popup##{ImGui.GetCursorPos()}"))
        {
            var temp = input;
            static void InsertString(ref string str, string ins)
            {
                var bytes = Encoding.UTF8.GetBytes(str).ToList();
                _inputPos = Math.Min(_inputPos, bytes.Count);
                var newBytes = Encoding.UTF8.GetBytes(ins);
                for (int i = 0; i < newBytes.Length; i++)
                    bytes.Insert(_inputPos++, newBytes[i]);
                str = Encoding.UTF8.GetString(bytes.ToArray());
            }

            var bI = 0;
            void DrawButton(int i)
            {
                if (bI % 15 != 0)
                    ImGui.SameLine();

                var str = $"{(char)i}";
                ImGui.SetWindowFontScale(1.5f);
                if (ImGui.Button(str, new Vector2(36 * ImGuiHelpers.GlobalScale)))
                {
                    InsertString(ref temp, str);
                    QoLBar.Config.Save();
                }
                ImGui.SetWindowFontScale(1);
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.SetWindowFontScale(4);
                    ImGui.TextUnformatted(str);
                    ImGui.SetWindowFontScale(1);
                    ImGui.TextUnformatted($"{(Dalamud.Game.Text.SeIconChar)i}");
                    ImGui.EndTooltip();
                }

                bI++;
            }


            for (var i = 0xE020; i <= 0xE02B; i++)
                DrawButton(i);
            for (var i = 0xE031; i <= 0xE035; i++)
                DrawButton(i);
            for (var i = 0xE038; i <= 0xE044; i++)
                DrawButton(i);
            for (var i = 0xE048; i <= 0xE04E; i++)
                DrawButton(i);
            for (var i = 0xE050; i <= 0xE08A; i++)
                DrawButton(i);
            for (var i = 0xE08F; i <= 0xE0C6; i++)
                DrawButton(i);
            for (var i = 0xE0D0; i <= 0xE0DB; i++)
                DrawButton(i);

            input = temp;

            ImGui.EndPopup();
        }
    }

    private static void AddRightClickPrivateUsePopup(ref string input)
    {
        if (ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"Private Use Popup##{ImGui.GetCursorPos()}");

        DrawInsertPrivateCharPopup(ref input);
    }

    public static void AutoPasteIcon(ShCfg sh)
    {
        if (!IconBrowserUI.iconBrowserOpen || !IconBrowserUI.doPasteIcon) return;

        var split = sh.Name.Split(new[] { "##" }, 2, StringSplitOptions.None);
        var split2 = split[0].Split(new[] { "::" }, 2, StringSplitOptions.None);
        sh.Name = $"{split2[0]}::{IconBrowserUI.pasteIcon}" + (split.Length > 1 ? $"##{split[1]}" : "");
        QoLBar.Config.Save();
        IconBrowserUI.doPasteIcon = false;
    }

    public static void EditShortcutConfigBase(ShCfg sh, bool editing, bool hasIcon)
    {
        EditShortcutName(sh, editing);
        ImGuiEx.SetItemTooltip("在名稱開頭或結尾使用 ::x（x 為數字）即可顯示圖示，例如「::2914」。\n" +
                               "在名稱任意位置加入 ##，後方文字會成為工具提示，\n例如「名稱##這是工具提示」。"
                               + (hasIcon ?
                                   "\n\nIcons accept arguments between \"::\" and their ID. I.e. \"::f21\".\n" +
                                   "\t' f ' - Applies the hotbar frame.\n" +
                                   "\t' n ' - Removes the hotbar frame.\n" +
                                   "\t' l ' - Uses the low resolution icon.\n" +
                                   "\t' h ' - Uses the high resolution icon if it exists.\n" +
                                   "\t' g ' - Changes the icon to grayscale.\n" +
                                   "\t' r ' - Reverses the icon."
                                   : string.Empty));

        var _t = (int)sh.Type;
        ImGui.TextUnformatted("類型");
        ImGui.RadioButton("指令", ref _t, 0);
        ImGui.SameLine(ImGui.GetWindowWidth() / 3);
        ImGui.RadioButton("分類", ref _t, 1);
        ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
        ImGui.RadioButton("間隔", ref _t, 2);
        if (_t != (int)sh.Type)
        {
            sh.Type = (ShortcutType)_t;
            if (sh.Type == ShortcutType.Category)
                sh.SubList ??= new List<ShCfg>();

            if (editing)
                QoLBar.Config.Save();
        }

        if (sh.Type != ShortcutType.Spacer && (sh.Type != ShortcutType.Category || sh.Mode == ShortcutMode.Default))
        {
            var height = ImGui.GetFontSize() * Math.Min(sh.Command.Split('\n').Length + 1, 7) + ImGui.GetStyle().FramePadding.Y * 2; // ImGui issue #238: can't disable multiline scrollbar and it appears a whole line earlier than it should, so thats cool I guess

            unsafe
            {
                if (ImGui.InputTextMultiline("指令##Input", ref sh.Command, 65535, new Vector2(0, height), ImGuiInputTextFlags.CallbackAlways, GetCursorPosCallback) && editing)
                    QoLBar.Config.Save();
            }
            AddRightClickPrivateUsePopup(ref sh.Command);
            ImGuiEx.SetItemTooltip("可按右鍵加入遊戲特殊符號；此外，\n" +
                                   "下列自訂指令只能由捷徑執行。\n" +
                                   "\t' //m0 ' - Executes individual macro #0 (up to //m99).\n" +
                                   "\t' //m100 ' - Executes shared macro #0 (up to //m199).\n" +
                                   "\t' //m ' - Begins or ends a custom macro. The following lines\n" +
                                   "will execute as a macro instead of a shortcut (allowing for\n" +
                                   "/wait, /macrolock, etc), until //m is used again, up to 30 lines.\n" +
                                   "\t' //i <ID/Name> ' - Uses an item, cannot be used with //m.\n" +
                                   "\t' // <Comment> ' - Adds a comment.");
        }
    }

    public static unsafe bool EditShortcutName(ShCfg sh, bool editing)
    {
        var ret = ImGui.InputText("名稱", ref sh.Name, 256, ImGuiInputTextFlags.CallbackAlways, GetCursorPosCallback);
        AddRightClickPrivateUsePopup(ref sh.Name);

        if (ret && editing)
            QoLBar.Config.Save();

        return ret;
    }

    public static bool EditShortcutMode(ShortcutUI sh)
    {
        var _m = (int)sh.Config.Mode;
        ImGui.TextUnformatted("模式");
        ImGuiEx.SetItemTooltip("變更按下時的行為。\n" +
                               "注意：不建議用於包含子分類的分類。");

        ImGui.RadioButton("預設", ref _m, 0);
        ImGuiEx.SetItemTooltip("預設行為；分類必須設為此模式才能編輯其中的捷徑！");

        ImGui.SameLine(ImGui.GetWindowWidth() / 3);
        ImGui.RadioButton("依序", ref _m, 1);
        ImGuiEx.SetItemTooltip("每次按下時依序執行下一行或下一個捷徑。");

        ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
        ImGui.RadioButton("隨機", ref _m, 2);
        ImGuiEx.SetItemTooltip("按下時隨機執行一行或一個捷徑。");

        if (_m != (int)sh.Config.Mode)
        {
            sh.Config.Mode = (ShortcutMode)_m;

            if (sh.Config.Mode == ShortcutMode.Random)
            {
                var c = Math.Max(1, (sh.Config.Type == ShortcutType.Category) ? sh.children.Count : sh.Config.Command.Split('\n').Length);
                sh.Config._i = (int)(QoLBar.FrameCount % c);
            }
            else
            {
                sh.Config._i = 0;
            }

            QoLBar.Config.Save();

            return true;
        }

        return false;
    }

    public static bool EditShortcutColor(ShortcutUI sh)
    {
        var color = ImGui.ColorConvertU32ToFloat4(sh.Config.Color);
        color.W += sh.Config.ColorAnimation / 255f; // Temporary
        if (ImGui.ColorEdit4("顏色", ref color, ImGuiColorEditFlags.NoDragDrop | ImGuiColorEditFlags.AlphaPreviewHalf))
        {
            sh.Config.Color = ImGui.ColorConvertFloat4ToU32(color);
            sh.Config.ColorAnimation = Math.Max((int)Math.Round(color.W * 255) - 255, 0);
            QoLBar.Config.Save();
            return true;
        }
        else
        {
            return false;
        }
    }

    public static void EditShortcutCategoryOptions(ShortcutUI sh)
    {
        if (ImGui.SliderInt("按鈕寬度", ref sh.Config.CategoryWidth, 0, 200))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip("設為 0 時依文字寬度調整。");

        if (ImGui.SliderInt("欄數", ref sh.Config.CategoryColumns, 0, 12))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip("每列達到多少個捷徑後換至下一列。\n" +
                               "設為 0 表示不限制。");

        if (ImGui.DragFloat("縮放", ref sh.Config.CategoryScale, 0.002f, 0.7f, 2f, "%.2f"))
            QoLBar.Config.Save();

        if (ImGui.DragFloat("字型縮放", ref sh.Config.CategoryFontScale, 0.0018f, 0.5f, 1.0f, "%.2f"))
            QoLBar.Config.Save();

        var spacing = new Vector2(sh.Config.CategorySpacing[0], sh.Config.CategorySpacing[1]);
        if (ImGui.DragFloat2("間距", ref spacing, 0.12f, 0, 32, "%.f"))
        {
            sh.Config.CategorySpacing[0] = (int)spacing.X;
            sh.Config.CategorySpacing[1] = (int)spacing.Y;
            QoLBar.Config.Save();
        }

        if (ImGui.Checkbox("游標停留時開啟", ref sh.Config.CategoryOnHover))
            QoLBar.Config.Save();
        ImGui.SameLine(ImGui.GetWindowWidth() / 2);
        if (ImGui.Checkbox("游標移開時關閉", ref sh.Config.CategoryHoverClose))
            QoLBar.Config.Save();

        if (ImGui.Checkbox("選取後保持開啟", ref sh.Config.CategoryStaysOpen))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip("按下分類內的捷徑時保持分類開啟。\n若捷徑會與其他插件互動，可能無法生效。");
        ImGui.SameLine(ImGui.GetWindowWidth() / 2);
        if (ImGui.Checkbox("無背景", ref sh.Config.CategoryNoBackground))
            QoLBar.Config.Save();
    }

    public static void EditShortcutIconOptions(ShortcutUI sh)
    {
        if (ImGui.DragFloat("縮放", ref sh.Config.IconZoom, 0.005f, 1.0f, 5.0f, "%.2f"))
            QoLBar.Config.Save();

        var offset = new Vector2(sh.Config.IconOffset[0], sh.Config.IconOffset[1]);
        if (ImGui.DragFloat2("偏移", ref offset, 0.0005f, -0.5f, 0.5f, "%.3f"))
        {
            sh.Config.IconOffset[0] = offset.X;
            sh.Config.IconOffset[1] = offset.Y;
            QoLBar.Config.Save();
        }

        var r = (float)(sh.Config.IconRotation * 180 / Math.PI) % 360;
        if (ImGui.DragFloat("旋轉", ref r, 0.2f, -360, 360, "%.f"))
        {
            if (r < 0)
                r += 360;
            sh.Config.IconRotation = (float)(r / 180 * Math.PI);
            QoLBar.Config.Save();
        }

        static string formatName(Lumina.Excel.Sheets.Action a) => a.RowId switch
        {
            0 => "無",
            847 => "[847] 道具",
            _ => $"[{a.RowId}] {a.Name}"
        };
        if (ImGuiEx.ExcelSheetCombo<Lumina.Excel.Sheets.Action>("冷卻時間技能 ID", out var action, s => s.GetRowOrDefault(sh.Config.CooldownAction) is { } a ? formatName(a) : sh.Config.CooldownAction.ToString(),
            ImGuiComboFlags.None, (a, s) => (a.RowId == 0 || a is { CooldownGroup: > 0, ClassJobCategory.RowId: > 0 }) && formatName(a).Contains(s, StringComparison.CurrentCultureIgnoreCase),
            a => ImGui.Selectable(formatName(a), sh.Config.CooldownAction == a.RowId)))
        {
            sh.Config.CooldownAction = action.Value.RowId;

            if (sh.Config.CooldownAction == 0)
                sh.Config.CooldownStyle = 0;

            QoLBar.Config.Save();
        }

        if (sh.Config.CooldownAction > 0)
        {
            var save = ImGui.CheckboxFlags("##CooldownNumber", ref sh.Config.CooldownStyle, (int)ImGuiEx.IconSettings.CooldownStyle.Number);
            ImGuiEx.SetItemTooltip("顯示數字");
            ImGui.SameLine();
            save |= ImGui.CheckboxFlags("##CooldownDisable", ref sh.Config.CooldownStyle, (int)ImGuiEx.IconSettings.CooldownStyle.Disable);
            ImGuiEx.SetItemTooltip("變暗（強制顯示圖示邊框）");
            ImGui.SameLine();
            save |= ImGui.CheckboxFlags("##CooldownDefault", ref sh.Config.CooldownStyle, (int)ImGuiEx.IconSettings.CooldownStyle.Cooldown);
            ImGuiEx.SetItemTooltip("預設冷卻轉圈（強制顯示圖示邊框）");
            ImGui.SameLine();
            save |= ImGui.CheckboxFlags("##CooldownGCD", ref sh.Config.CooldownStyle, (int)ImGuiEx.IconSettings.CooldownStyle.GCDCooldown);
            ImGuiEx.SetItemTooltip("橘色公共冷卻轉圈");
            ImGui.SameLine();
            save |= ImGui.CheckboxFlags("Cooldown Style Flags##CooldownCharge", ref sh.Config.CooldownStyle, (int)ImGuiEx.IconSettings.CooldownStyle.ChargeCooldown);
            ImGuiEx.SetItemTooltip("積蓄次數轉圈");
            if (save)
                QoLBar.Config.Save();
        }
    }

    public static void EditBarGeneralOptions(BarUI bar)
    {
        if (ImGui.InputText("名稱", ref bar.Config.Name, 256))
            QoLBar.Config.Save();

        var _dock = (int)bar.Config.DockSide;
        if (ImGui.Combo("停靠位置", ref _dock, (ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0
                ? "上\0右\0下\0左\0不固定"
                : "上\0右\0下\0左"))
        {
            bar.Config.DockSide = (BarDock)_dock;
            if (bar.Config.DockSide == BarDock.Undocked && bar.Config.Visibility == BarVisibility.Slide)
                bar.Config.Visibility = BarVisibility.Always;
            bar.Config.Position[0] = 0;
            bar.Config.Position[1] = 0;
            bar.Config.LockedPosition = false;
            QoLBar.Config.Save();
            bar.SetupPivot();
        }

        if (bar.IsDocked)
        {
            var topbottom = bar.Config.DockSide == BarDock.Top || bar.Config.DockSide == BarDock.Bottom;
            var _align = (int)bar.Config.Alignment;
            ImGui.Text("對齊");
            ImGui.RadioButton(topbottom ? "左" : "上", ref _align, 0);
            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
            ImGui.RadioButton("中央", ref _align, 1);
            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
            ImGui.RadioButton(topbottom ? "右" : "下", ref _align, 2);
            if (_align != (int)bar.Config.Alignment)
            {
                bar.Config.Alignment = (BarAlign)_align;
                QoLBar.Config.Save();
                bar.SetupPivot();
            }

            var _visibility = (int)bar.Config.Visibility;
            ImGui.Text("顯示方式");
            ImGui.RadioButton("滑入", ref _visibility, 0);
            ImGui.SameLine(ImGui.GetWindowWidth() / 3);
            ImGui.RadioButton("立即", ref _visibility, 1);
            ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
            ImGui.RadioButton("永遠顯示", ref _visibility, 2);
            if (_visibility != (int)bar.Config.Visibility)
            {
                bar.Config.Visibility = (BarVisibility)_visibility;
                QoLBar.Config.Save();
            }

            if ((bar.Config.Visibility != BarVisibility.Always) && ImGui.DragFloat("觸發區域比例", ref bar.Config.RevealAreaScale, 0.01f, 0.0f, 1.0f, "%.2f"))
                QoLBar.Config.Save();
        }
        else
        {
            var _visibility = (int)bar.Config.Visibility;
            ImGui.Text("顯示方式");
            ImGui.RadioButton("立即", ref _visibility, 1);
            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
            ImGui.RadioButton("永遠顯示", ref _visibility, 2);
            if (_visibility != (int)bar.Config.Visibility)
            {
                bar.Config.Visibility = (BarVisibility)_visibility;
                QoLBar.Config.Save();
            }
        }

        Keybind.KeybindInput(bar.Config);

        if (ImGui.Checkbox("編輯模式", ref bar.Config.Editing))
        {
            if (!bar.Config.Editing)
                Game.ExecuteCommand("/echo <se> 可在快捷列本體（黑色背景）按右鍵重新開啟此設定選單，也可按住 Shift 再按右鍵新增捷徑。");
            QoLBar.Config.Save();
        }
        ImGui.SameLine(ImGui.GetWindowWidth() / 2);
        if (ImGui.Checkbox("點擊穿透", ref bar.Config.ClickThrough))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip("警告：啟用後將無法直接與此快捷列互動。\n" +
                               "若要再次編輯設定，請在一般設定中使用\n" +
                               "快捷列名稱旁的「O」按鈕。");

        if (ImGui.Checkbox("鎖定位置", ref bar.Config.LockedPosition))
            QoLBar.Config.Save();
        if (bar.IsDocked && bar.Config.Visibility != BarVisibility.Always)
        {
            ImGui.SameLine(ImGui.GetWindowWidth() / 2);
            if (ImGui.Checkbox("保持喚醒", ref bar.Config.Hint))
                QoLBar.Config.Save();
            ImGuiEx.SetItemTooltip("避免快捷列進入休眠，但會提高 CPU 負載。");
        }

        if (!bar.Config.LockedPosition)
        {
            var pos = bar.VectorPosition;
            var area = bar.UsableArea;
            var max = (area.X > area.Y) ? area.X : area.Y;
            if (ImGui.DragFloat2(bar.IsDocked ? "偏移" : "位置", ref pos, 1, -max, max, "%.f"))
            {
                bar.Config.Position[0] = Math.Min(pos.X / area.X, 1);
                bar.Config.Position[1] = Math.Min(pos.Y / area.Y, 1);
                QoLBar.Config.Save();
                if (bar.IsDocked)
                    bar.SetupPivot();
                else
                    bar._setPos = true;
            }
        }
    }

    public static void EditBarStyleOptions(BarUI bar)
    {
        if (ImGui.SliderInt("按鈕寬度", ref bar.Config.ButtonWidth, 0, 200))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip("設為 0 時依文字寬度調整。");

        if (ImGui.SliderInt("欄數", ref bar.Config.Columns, 0, 12))
            QoLBar.Config.Save();
        ImGuiEx.SetItemTooltip("每列達到多少個捷徑後換至下一列。\n" +
                               "設為 0 表示不限制。");

        if (ImGui.DragFloat("縮放", ref bar.Config.Scale, 0.002f, 0.7f, 2.0f, "%.2f"))
            QoLBar.Config.Save();

        if (ImGui.DragFloat("字型縮放", ref bar.Config.FontScale, 0.0018f, 0.5f, 1.0f, "%.2f"))
            QoLBar.Config.Save();

        var spacing = new Vector2(bar.Config.Spacing[0], bar.Config.Spacing[1]);
        if (ImGui.DragFloat2("間距", ref spacing, 0.12f, 0, 32, "%.f"))
        {
            bar.Config.Spacing[0] = (int)spacing.X;
            bar.Config.Spacing[1] = (int)spacing.Y;
            QoLBar.Config.Save();
        }

        if (ImGui.Checkbox("無背景", ref bar.Config.NoBackground))
            QoLBar.Config.Save();
    }

    public static void DisplayRightClickDeleteMessage(string text = "按右鍵即可刪除！") =>
        DalamudApi.ShowNotification($"\t\t\t{text}\t\t\t\n\n", NotificationType.Info);
}
