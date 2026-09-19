using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.Bots.Protocol;

public readonly record struct BotPassportClaim(int Id, int ArriveTime);

public static partial class GameClientPackets
{
	public static BotClientPacket ClaimPassports(params BotPassportClaim[] claims)
	{
		ArgumentNullException.ThrowIfNull(claims);
		if (claims.Length > short.MaxValue) throw new ArgumentOutOfRangeException(nameof(claims));
		return Create<CM_ATREIAN_PASSPORT>(w =>
		{
			w.H((short)claims.Length);
			foreach (var claim in claims) { w.D(claim.Id); w.D(claim.ArriveTime); }
		});
	}
}
