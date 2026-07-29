using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace QoLBar.Conditions;

public class RoleCondition : ICondition, IDrawableCondition, IArgCondition, IConditionCategory
{
    public static readonly Dictionary<int, string> roleDictionary = new()
    {
        [1] = "防護職業",
        [2] = "近戰職業",
        [3] = "遠程職業",
        [4] = "治療職業",
        [30] = "戰鬥精英",
        [31] = "魔法導師",
        [32] = "大地使者",
        [33] = "能工巧匠"
    };

    public string ID => "r";
    public string ConditionName => "職責";
    public string CategoryName => "職責";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => DalamudApi.ClientState.LocalPlayer is { } player
        && ((uint)arg < 30 ? player.ClassJob.ValueNullable?.Role : player.ClassJob.ValueNullable?.ClassJobCategory.RowId) == (uint)arg;
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        roleDictionary.TryGetValue((int)cndCfg.Arg, out string s);
        if (!ImGui.BeginCombo("##Role", s)) return;

        foreach (var (id, name) in roleDictionary)
        {
            if (!ImGui.Selectable(name, id == cndCfg.Arg)) continue;

            cndCfg.Arg = id;
            QoLBar.Config.Save();
        }
        ImGui.EndCombo();
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => roleDictionary.FirstOrDefault(kv => Check(kv.Key)).Key;
}
