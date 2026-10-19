using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Custom.Companion;

/// <summary>
/// What a companion wears, carries and has learned (D41, docs/companion-bots.md). No Java counterpart. The profiles
/// are generated from the group world by <c>scripts/sim/make-companion-profiles.py</c> into
/// <c>game-server/data/companions</c>, one file per race, class and level; they are never edited by hand.
/// </summary>
public sealed class CompanionProfile
{
    public const int SchemaVersion = 1;

    public sealed record Worn(long Slot, int ItemId, long Count, int Enchant);
    public sealed record Carried(int ItemId, long Count);
    public sealed record Learned(int Id, int Level);

    public required Race Race { get; init; }
    public required PlayerClass PlayerClass { get; init; }
    public required int Level { get; init; }
    public required Gender Gender { get; init; }
    public required IReadOnlyDictionary<string, double> Appearance { get; init; }
    public required IReadOnlyList<Worn> Equipment { get; init; }
    public required IReadOnlyList<Carried> Inventory { get; init; }
    public required IReadOnlyList<Learned> Skills { get; init; }

    /// <summary>Every profile of a folder. A file that cannot be read whole is an error: a companion is never made from half a profile.</summary>
    public static IReadOnlyList<CompanionProfile> LoadAll(string directory)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException("The companion profiles are not at " + directory + ".");
        var profiles = new List<CompanionProfile>();
        foreach (string path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                profiles.Add(Parse(File.ReadAllText(path)));
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
            {
                throw new InvalidDataException("The companion profile " + Path.GetFileName(path) + " cannot be read: " + e.Message, e);
            }
        }
        return profiles;
    }

    public static CompanionProfile Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion)
            throw new InvalidDataException("A companion profile of schema " + root.GetProperty("schemaVersion").GetInt32() + " is not known.");
        var profile = new CompanionProfile
        {
            Race = Enum.Parse<Race>(root.GetProperty("race").GetString()!),
            PlayerClass = Enum.Parse<PlayerClass>(root.GetProperty("playerClass").GetString()!),
            Level = root.GetProperty("level").GetInt32(),
            Gender = Enum.Parse<Gender>(root.GetProperty("gender").GetString()!),
            Appearance = root.GetProperty("appearance").EnumerateObject().ToDictionary(field => field.Name, field => field.Value.GetDouble()),
            Equipment = root.GetProperty("equipment").EnumerateArray().Select(worn => new Worn(worn.GetProperty("slot").GetInt64(),
                worn.GetProperty("itemId").GetInt32(), worn.GetProperty("count").GetInt64(), worn.GetProperty("enchant").GetInt32())).ToArray(),
            Inventory = root.GetProperty("inventory").EnumerateArray()
                .Select(carried => new Carried(carried.GetProperty("itemId").GetInt32(), carried.GetProperty("count").GetInt64())).ToArray(),
            Skills = root.GetProperty("skills").EnumerateArray()
                .Select(skill => new Learned(skill.GetProperty("id").GetInt32(), skill.GetProperty("level").GetInt32())).ToArray(),
        };
        // A column the server's appearance does not have would be lost silently, and the face would not be the one played.
        profile.NewAppearance();
        return profile;
    }

    /// <summary>The appearance of the character the profile came from. A field is a column of <c>player_appearance</c>.</summary>
    public PlayerAppearance NewAppearance()
    {
        var appearance = new PlayerAppearance();
        foreach ((string column, double value) in Appearance)
        {
            MethodInfo setter = typeof(PlayerAppearance).GetMethod(SetterOf(column), BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidDataException("The appearance has no field for the column " + column + ".");
            Type parameter = setter.GetParameters().Single().ParameterType;
            setter.Invoke(appearance, [parameter == typeof(float) ? (object)(float)value : checked((int)value)]);
        }
        return appearance;
    }

    /// <summary>Map database column names, including the two legacy names in played companion profiles, to appearance setters.</summary>
    private static string SetterOf(string column)
    {
        if (column == "jaw_height")
            return "SetChinHeight";
        if (column == "chin_jut")
            return "SetCheekBones";
        if (column == "height")
            return "SetHeightScale";
        return "Set" + string.Concat(column.Split('_').Select(part => part == "rgb" ? "RGB" : char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
