using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.CoopRuntimeContract;

public static partial class ContractRunner
{
    private static void RunGarrisonDisplayTests()
    {
        Type displayType = typeof(Main).Assembly.GetType(
            "ImprovedGarrisons.ImprovedGarrisonsUI.UIElements.ActualGarrisonVM", throwOnError: true)!;
        object display = Activator.CreateInstance(displayType)!;
        MethodInfo refresh = displayType.GetMethod("RefreshRoster")!;
        CharacterObject archer = CreateDisplayCharacter("display_archer", "Archer");
        CharacterObject outsider = CreateDisplayCharacter("display_outsider", "Outsider");
        TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
        roster.AddToCounts(archer, 7, woundedCount: 2);
        roster.AddToCounts(outsider, 5, woundedCount: 1);
        Dictionary<CharacterObject, int> targets = new() { [archer] = 50 };

        refresh.Invoke(display, new object?[] { roster, 500 });
        IList rows = (IList)displayType.GetProperty("Troops")!.GetValue(display)!;
        Assert(rows.Count == 2, "The actual garrison omitted a troop type absent from the template.");
        Assert(GetDisplayInt(display, "TotalCount") == 12, "Actual total must count stationed troops, including wounded, once.");
        Assert(GetDisplayInt(display, "Capacity") == 500, "Actual capacity did not use the supplied live garrison limit.");
        Assert(GetDisplayInt(rows[0]!, "Count") == 7 && GetDisplayInt(rows[0]!, "WoundedCount") == 2,
            "Actual row used template targets or counted wounded twice.");
        Assert((string)rows[1]!.GetType().GetProperty("Name")!.GetValue(rows[1])! == "Outsider",
            "The read-only garrison row lost the troop name.");

        object unchangedRow = rows[1]!;
        roster.AddToCounts(archer, 3);
        refresh.Invoke(display, new object?[] { roster, 550 });
        Assert(GetDisplayInt(display, "TotalCount") == 15 && GetDisplayInt(display, "Capacity") == 550,
            "The actual garrison did not refresh after the roster/capacity changed.");
        Assert(GetDisplayInt(rows[0]!, "Count") == 10 && ReferenceEquals(rows[1], unchangedRow),
            "Refreshing did not update the count in place while retaining unchanged rows.");
        Assert(targets.Count == 1 && targets[archer] == 50 && roster.GetTroopCount(outsider) == 5,
            "Reading actual garrison data changed desired targets or stationed troops.");

        roster.AddToCounts(outsider, -5, woundedCount: -1);
        refresh.Invoke(display, new object?[] { roster, 550 });
        Assert(rows.Count == 1 && GetDisplayInt(display, "TotalCount") == 10,
            "Removed garrison troops remained visible in the actual roster.");
        refresh.Invoke(display, new object?[] { TroopRoster.CreateDummyTroopRoster(), 250 });
        Assert(rows.Count == 0 && GetDisplayInt(display, "TotalCount") == 0 && GetDisplayInt(display, "Capacity") == 250,
            "An empty garrison must show zero troops while retaining its capacity.");
        refresh.Invoke(display, new object?[] { null, 0 });
        Assert((bool)displayType.GetProperty("IsEmpty")!.GetValue(display)! && rows.Count == 0,
            "A missing garrison must produce an empty read-only display.");
        Console.WriteLine("PASS actual_garrison_display_counts_refresh_and_template_isolation");
    }

    private static int GetDisplayInt(object target, string property) =>
        (int)target.GetType().GetProperty(property)!.GetValue(target)!;

    private static CharacterObject CreateDisplayCharacter(string id, string name)
    {
        CharacterObject character = (CharacterObject)RuntimeHelpers.GetUninitializedObject(typeof(CharacterObject));
        character.StringId = id;
        typeof(TaleWorlds.Core.BasicCharacterObject).GetField("_basicName", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(character, new TextObject(name));
        return character;
    }
}
