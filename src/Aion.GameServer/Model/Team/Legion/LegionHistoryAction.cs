using System;
using System.Collections.Generic;

namespace Aion.GameServer.Model.Team.Legion;

/// <summary>
/// Java parity: model/team/legion/LegionHistoryAction. Java enum with per-instance fields (id + nested Type)
/// → class-enum (static instances) preserving the nested Type enum and id/type accessors.
/// </summary>
public sealed class LegionHistoryAction
{
    public enum Type
    {
        LEGION,
        REWARD,
        WAREHOUSE,
    }

    public static readonly LegionHistoryAction CREATE = new LegionHistoryAction(nameof(CREATE), 0, Type.LEGION); // No parameters
    public static readonly LegionHistoryAction JOIN = new LegionHistoryAction(nameof(JOIN), 1, Type.LEGION); // Parameter: name
    public static readonly LegionHistoryAction KICK = new LegionHistoryAction(nameof(KICK), 2, Type.LEGION); // Parameter: name
    public static readonly LegionHistoryAction LEVEL_UP = new LegionHistoryAction(nameof(LEVEL_UP), 3, Type.LEGION); // Parameter: legion level
    public static readonly LegionHistoryAction APPOINTED = new LegionHistoryAction(nameof(APPOINTED), 4, Type.LEGION); // Parameter: legion level
    public static readonly LegionHistoryAction EMBLEM_REGISTER = new LegionHistoryAction(nameof(EMBLEM_REGISTER), 5, Type.LEGION); // No parameters
    public static readonly LegionHistoryAction EMBLEM_MODIFIED = new LegionHistoryAction(nameof(EMBLEM_MODIFIED), 6, Type.LEGION); // No parameters
    // 7 to 10 are not used anymore or never implemented
    public static readonly LegionHistoryAction DEFENSE = new LegionHistoryAction(nameof(DEFENSE), 11, Type.REWARD); // Parameter: name = kinah amount, description = fortress id
    public static readonly LegionHistoryAction OCCUPATION = new LegionHistoryAction(nameof(OCCUPATION), 12, Type.REWARD); // Parameter: name = kinah amount, description = fortress id
    public static readonly LegionHistoryAction LEGION_RENAME = new LegionHistoryAction(nameof(LEGION_RENAME), 13, Type.LEGION); // Parameter: old name, new name
    public static readonly LegionHistoryAction CHARACTER_RENAME = new LegionHistoryAction(nameof(CHARACTER_RENAME), 14, Type.LEGION); // Parameter: old name, new name
    public static readonly LegionHistoryAction ITEM_DEPOSIT = new LegionHistoryAction(nameof(ITEM_DEPOSIT), 15, Type.WAREHOUSE); // Parameter: name
    public static readonly LegionHistoryAction ITEM_WITHDRAW = new LegionHistoryAction(nameof(ITEM_WITHDRAW), 16, Type.WAREHOUSE); // Parameter: name
    public static readonly LegionHistoryAction KINAH_DEPOSIT = new LegionHistoryAction(nameof(KINAH_DEPOSIT), 17, Type.WAREHOUSE); // Parameter: name
    public static readonly LegionHistoryAction KINAH_WITHDRAW = new LegionHistoryAction(nameof(KINAH_WITHDRAW), 18, Type.WAREHOUSE); // Parameter: name

    private readonly string name;
    private readonly byte id;
    private readonly Type type;

    private LegionHistoryAction(string name, int id, Type type)
    {
        this.name = name;
        this.id = (byte)id;
        this.type = type;
    }

    // Java Enum.toString() supplies the constant name persisted by LegionDAO.InsertHistory.
    public override string ToString() => name;

    public byte GetId()
    {
        return id;
    }

    public Type GetType_()
    {
        return type;
    }

    // Java parity: enum values() declaration order.
    private static readonly LegionHistoryAction[] VALUES =
    {
        CREATE, JOIN, KICK, LEVEL_UP, APPOINTED, EMBLEM_REGISTER, EMBLEM_MODIFIED,
        DEFENSE, OCCUPATION, LEGION_RENAME, CHARACTER_RENAME, ITEM_DEPOSIT, ITEM_WITHDRAW, KINAH_DEPOSIT, KINAH_WITHDRAW,
    };

    public static IReadOnlyList<LegionHistoryAction> Values()
    {
        return VALUES;
    }

    // Java parity: enum valueOf(String name) — maps the declared constant name to its instance.
    public static LegionHistoryAction ValueOf(string name) => name switch
    {
        "CREATE" => CREATE,
        "JOIN" => JOIN,
        "KICK" => KICK,
        "LEVEL_UP" => LEVEL_UP,
        "APPOINTED" => APPOINTED,
        "EMBLEM_REGISTER" => EMBLEM_REGISTER,
        "EMBLEM_MODIFIED" => EMBLEM_MODIFIED,
        "DEFENSE" => DEFENSE,
        "OCCUPATION" => OCCUPATION,
        "LEGION_RENAME" => LEGION_RENAME,
        "CHARACTER_RENAME" => CHARACTER_RENAME,
        "ITEM_DEPOSIT" => ITEM_DEPOSIT,
        "ITEM_WITHDRAW" => ITEM_WITHDRAW,
        "KINAH_DEPOSIT" => KINAH_DEPOSIT,
        "KINAH_WITHDRAW" => KINAH_WITHDRAW,
        _ => throw new ArgumentException("No LegionHistoryAction with name " + name),
    };
}
