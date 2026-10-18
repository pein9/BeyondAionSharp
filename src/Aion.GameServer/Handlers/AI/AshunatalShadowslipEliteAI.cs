using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/aturamSkyFortress/AshunatalShadowslipAI (@author xTz), the AI Java gives the elite Ashunatal Shadowslip
/// (219183) of Aturam Sky Fortress: door 2 closed while she fights, opened when she goes home or dies (door 17 too), and after
/// skill 19417 she walks off on route 3002400001 and is gone. Java's AI name went to the retail pattern of the other Ashunatal
/// (217376, <see cref="AshunatalShadowslipAI"/>), so this port runs under its own name; see docs/retail-ai-fidelity.md.
/// </summary>
[AIName("ashunatal_shadowslip_elite")]
public class AshunatalShadowslipEliteAI : AggressiveNpcAI
{
	private readonly AtomicBoolean isHome = new AtomicBoolean(true);
	private bool canThink = true;

	public AshunatalShadowslipEliteAI(Npc owner) : base(owner)
	{
	}

	public override bool CanThink()
	{
		return canThink;
	}

	protected override void HandleCreatureAggro(Creature creature)
	{
		base.HandleCreatureAggro(creature);
		if (isHome.CompareAndSet(true, false))
		{
			GetPosition().GetWorldMapInstance().SetDoorState(2, true); // this actually closes it on client side (wtf)
		}
	}

	protected override void HandleBackHome()
	{
		isHome.Set(true);
		base.HandleBackHome();
		GetPosition().GetWorldMapInstance().SetDoorState(2, false); // this actually opens it on client side (wtf)
	}

	protected override void HandleDied()
	{
		base.HandleDied();
		// ashunatal got killed early, therefore she could not spawn her shadow which usually would open the door
		GetPosition().GetWorldMapInstance().SetDoorState(17, false); // this actually opens it on client side (wtf)
		GetPosition().GetWorldMapInstance().SetDoorState(2, false); // this actually opens it on client side (wtf)
	}

	public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
	{
		base.OnEndUseSkill(skillTemplate, skillLevel);
		if (skillTemplate.GetSkillId() == 19417)
		{
			canThink = false;
			ThreadPoolManager.GetInstance().Schedule(_ =>
			{
				GetSpawnTemplate().SetWalkerId("3002400001");
				GetOwner().SetState(CreatureState.ACTIVE, true);
				WalkManager.StartWalking(this);
				PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
				ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().Delete(); return ValueTask.CompletedTask; }, 4000L);
				return ValueTask.CompletedTask;
			}, 3000L);
		}
	}
}
