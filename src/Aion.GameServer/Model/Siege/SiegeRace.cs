using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Siege;

/// <summary>
/// Siege race identifiers — maps each to a raceId int and an L10n id.
/// Java parity: model/siege/SiegeRace (Sarynth). UPPER_CASE names match Java; GHENCHMAN_LIGHT/DARK mirror the
/// Race enum's henchman-balaur factions used by AgentSiege.
/// </summary>
public enum SiegeRace
{
    ELYOS,
    ASMODIANS,
    BALAUR,
    GHENCHMAN_LIGHT,
    GHENCHMAN_DARK,
}

/// <summary>
/// Extension/static helpers mirroring SiegeRace's Java instance/static methods.
/// Java parity: model/siege/SiegeRace::getRaceId, ::getL10nId, L10n default ::getL10n, ::getByRace.
/// </summary>
public static class SiegeRaceExtensions
{
    // Java parity: SiegeRace::getRaceId() — matches Java constructor raceId
    public static int GetRaceId(this SiegeRace race) => race switch
    {
        SiegeRace.ELYOS => (int)Model.Race.ELYOS,         // Race.ELYOS.getRaceId() = 0
        SiegeRace.ASMODIANS => (int)Model.Race.ASMODIANS, // Race.ASMODIANS.getRaceId() = 1
        SiegeRace.BALAUR => 2,
        SiegeRace.GHENCHMAN_LIGHT => (int)Model.Race.GHENCHMAN_LIGHT,
        SiegeRace.GHENCHMAN_DARK => (int)Model.Race.GHENCHMAN_DARK,
        _ => 2,
    };

    // Java parity: SiegeRace::getL10nId() — matches Java constructor l10nId. The C#-only henchman members have none.
    public static int GetL10nId(this SiegeRace race) => race switch
    {
        SiegeRace.ELYOS => Model.Race.ELYOS.GetL10nId(),
        SiegeRace.ASMODIANS => Model.Race.ASMODIANS.GetL10nId(),
        SiegeRace.BALAUR => 900242,
        _ => 0,
    };

    // Java parity: L10n default getL10n() via ChatUtil.l10n(l10nId)
    public static string? GetL10n(this SiegeRace race) => race.GetL10nId() == 0 ? null : ChatUtil.L10n(race.GetL10nId());

    // Java parity: SiegeRace::getByRace(Race race)
    public static SiegeRace GetByRace(Model.Race race) => race switch
    {
        Model.Race.ASMODIANS => SiegeRace.ASMODIANS,
        Model.Race.ELYOS => SiegeRace.ELYOS,
        Model.Race.GHENCHMAN_LIGHT => SiegeRace.GHENCHMAN_LIGHT,
        Model.Race.GHENCHMAN_DARK => SiegeRace.GHENCHMAN_DARK,
        _ => SiegeRace.BALAUR,
    };
}
