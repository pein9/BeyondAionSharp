using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Meet Kvasir (204053) and choose your mission.
/// -- Choice 2 --
/// Talk with Aegir (204301).
/// Get the Golden Helmet of Urgasch (182207037) (Statue of Urgasch, 700268) and take it to Aegir.
/// -- Choice 1 --
/// Talk with Aegir.
/// Defeat the fierce creatures of Morheim:
/// Guzzling Kurin (212396) (3),
/// Klaw Scouter (212611) (3),
/// Dark Lake Spirit (212408) (3).
/// Report the result to Aegir.
/// -- Choice 0 --
/// Talk with Garm (204089).
/// Defeat Spirit of Underground Arena in the Triniel Underground Arena(10):
/// Warrior Spirit (213583, 290048, 211987, 290047, 290050, 211986, 290049),
/// Mage Spirit (213584, 211982).
/// You succeeded! Talk with Garm.
/// You failed! Talk with Garm again.
///
/// @author Hellboy, aion4Free, Gigi, vlog
/// </summary>
public class _2947FollowingThrough : AbstractQuestHandler
{
	public _2947FollowingThrough() : base(2947)
	{
	}

	public override void Register()
	{
		int[] npcs = { 204053, 204301, 204089, 700268 };
		int[] mobs = { 212396, 212611, 212408, 213583, 290048, 211987, 290047, 290050, 211986, 290049, 213584, 211982 };
		qe.RegisterOnQuestCompleted(questId);
		qe.RegisterOnQuestTimerEnd(questId);
		qe.RegisterOnEnterWorld(questId);
		qe.RegisterOnDie(questId); // D36
		foreach (int npc in npcs)
		{
			qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
		}
		foreach (int mob in mobs)
		{
			qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
		}
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs == null)
			return false;
		int var = qs.GetQuestVarById(0);
		int targetId = env.GetTargetId();
		int dialogActionId = env.GetDialogActionId();

		if (qs.GetStatus() == QuestStatus.START)
		{
			switch (targetId)
			{
				case 204053: // Kvasir
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 0)
							{
								return SendQuestDialog(env, 1011);
							}
							else if (var == 4)
							{
								return SendQuestDialog(env, 1019);
							}
							return false;
						case DialogAction.SETPRO12:
							if (var == 0)
							{
								return DefaultCloseDialog(env, 0, 4); // 4
							}
							else if (var == 4)
							{
								return DefaultCloseDialog(env, 4, 4);
							}
							return false;
						case DialogAction.FINISH_DIALOG:
							if (var == 0)
							{
								return DefaultCloseDialog(env, 0, 0);
							}
							break;
					}
					break;
				case 204301: // Aegir
					switch (dialogActionId)
					{
						case DialogAction.USE_OBJECT:
							if (var == 7)
							{
								return SendQuestDialog(env, 3739);
							}
							return false;
						case DialogAction.SELECT_QUEST_REWARD:
							UpdateQuestStatus(env);
							return SendQuestDialog(env, 6);
					}
					break;
				case 204089: // Garm
					switch (dialogActionId)
					{
						case DialogAction.USE_OBJECT:
							if (var == 6)
							{
								return SendQuestDialog(env, 1779);
							}
							return false;
						case DialogAction.QUEST_SELECT:
							if (var == 4)
							{
								return SendQuestDialog(env, 1693);
							}
							else if (qs.GetQuestVarById(4) == 10)
							{
								return SendQuestDialog(env, 2034);
							}
							return false;
						case DialogAction.SETPRO3:
							if (var == 4 || var == 6)
							{
								ResetArena(player); // D34: every attempt starts in a new arena
								// D35 (deviation 157): Garm sends the player in, as 4.8 retail's text says ("I'll send you to the Arena")
								// and Java's Elyos twin Q1922 does. Java's Q2947 only sets var 5 and leaves the walk to the entrance 700368.
								WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.TRINIEL_UNDERGROUND_ARENA.GetId(), player);
								TeleportService.TeleportTo(player, newInstance, 276, 293, 163, (byte)90, TeleportAnimation.NONE);
								ChangeQuestStep(env, var, 5, false); // 5
								return CloseDialogWindow(env);
							}
							return false;
						case DialogAction.SETPRO4:
							qs.SetQuestVarById(0, 7);
							qs.SetStatus(QuestStatus.REWARD);
							UpdateQuestStatus(env);
							return DefaultCloseDialog(env, 7, 7); // 7
					}
					break;
				case 700268: // Statue of Urgasch
					if (var == 9 && dialogActionId == DialogAction.USE_OBJECT)
					{
						return true; // loot
					}
					break;
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 204301) // Aegir
			{
				if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
					return SendQuestDialog(env, 3739);
				else
				{
					qs.SetRewardGroup(1); // not variable anymore (previously you had 3 choices how to finish the quest), so always reward group 1
					return SendQuestEndDialog(env);
				}
			}
		}
		return false;
	}

	public override bool OnKillEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			int var = qs.GetQuestVarById(0);
			if (var == 2)
			{
				int var1 = qs.GetQuestVarById(1);
				int var2 = qs.GetQuestVarById(2);
				int var3 = qs.GetQuestVarById(3);
				switch (env.GetTargetId())
				{
					case 212396: // Guzzling Kurin
						if (var2 == 3 && var3 == 3)
						{
							qs.SetQuestVar(3); // 3
							UpdateQuestStatus(env);
							PlayQuestMovie(env, 168);
							return true;
						}
						ChangeQuestStep(env, var1, var1 + 1, false, 1); // 1: 1 - 3
						return true;
					case 212611: // Klaw Scouter
						if (var1 == 3 && var3 == 3)
						{
							qs.SetQuestVar(3); // 3
							UpdateQuestStatus(env);
							PlayQuestMovie(env, 168);
							return true;
						}
						ChangeQuestStep(env, var2, var2 + 1, false, 2); // 2: 1 - 3
						return true;
					case 212408: // Dark Lake Spirit
						if (var1 == 3 && var2 == 3)
						{
							qs.SetQuestVar(3); // 3
							UpdateQuestStatus(env);
							return true;
						}
						ChangeQuestStep(env, var3, var3 + 1, false, 3); // 3: 1 - 3
						break;
				}
			}
			else if (var == 5)
			{
				int var4 = qs.GetQuestVarById(4);
				int[] mobs = { 213583, 290048, 211987, 290047, 290050, 211986, 290049, 213584, 211982 };
				if (var4 < 9)
				{
					return DefaultOnKillEvent(env, mobs, 0, 9, 4); // 4: 1 - 9
				}
				else if (var4 == 9)
				{
					DefaultOnKillEvent(env, mobs, 9, 10, 4); // 4: 10
					QuestService.QuestTimerEnd(env);
					PlayQuestMovie(env, 168);
					return true;
				}
			}
		}
		return false;
	}

	// D34 (deviation 156): the engine runs this hook when any quest timer ends, and Java asks only for START and fewer than
	// ten kills. Another quest's timer then sent a player who was anywhere on this quest to Garm at var 6. Only the player's own
	// attempt in the arena fails here. The failed arena is then reset, so the next attempt does not find its dead spirits.
	public override bool OnQuestTimerEndEvent(QuestEnv env)
	{
		if (!IsOnTheArenaAttempt(env))
			return false;
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			int var4 = qs.GetQuestVarById(4);
			if (var4 != 10)
			{
				qs.SetQuestVar(6);
				UpdateQuestStatus(env);
				TeleportService.TeleportTo(player, 120010000, 1006.1f, 1526, 222.2f, (byte)90);
				ResetArena(player);
				return true;
			}
		}
		return false;
	}

	// D36 (deviation 158): in 4.8 retail "deaths will incur death penalty as normal, and the player must speak to Garm again to
	// retry". Java has no die hook, so a player who revived in place went on with the same timer and kills. A death on the
	// player's own attempt now fails it as the timer does, but without the teleport: the player revives in the ordinary way.
	public override bool OnDieEvent(QuestEnv env)
	{
		if (!IsOnTheArenaAttempt(env))
			return false;
		QuestService.QuestTimerEnd(env);
		QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
		qs.SetQuestVar(6);
		UpdateQuestStatus(env);
		return true;
	}

	public override bool OnEnterWorldEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			int var = qs.GetQuestVarById(0);
			int var4 = qs.GetQuestVars().GetVarById(4);
			if (var == 6 && player.GetWorldId() != 320090000)
				ResetArena(player); // D36: an attempt that failed at a death is reset once the player is out of its arena
			if (var == 5 && var4 != 10)
			{
				if (player.GetWorldId() != 320090000)
				{
					QuestService.QuestTimerEnd(env); // left as Java has it: it ends whichever quest timer is running
					qs.SetQuestVar(6);
					UpdateQuestStatus(env);
					ResetArena(player); // D34
					return true;
				}
				else
				{
					PlayQuestMovie(env, 167);
					QuestService.QuestTimerStart(env, 240);
					return true;
				}
			}
		}
		return false;
	}

	public override void OnMovieEndEvent(QuestEnv env, int movieId)
	{
		if (movieId == 168)
		{
			TeleportService.TeleportTo(env.GetPlayer(), 120010000, 1006.1f, 1526, 222.2f, (byte)90);
		}
		else if (movieId == 167)
		{
			if (IsOnTheArenaAttempt(env)) // D34: a late movie end starts no timer for a player who failed or left
				QuestService.QuestTimerStart(env, 240);
		}
	}

	private bool IsOnTheArenaAttempt(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		return qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 5 && qs.GetQuestVarById(4) != 10
			&& player.GetWorldId() == 320090000;
	}

	// D34: PortalService returns a solo player to the instance registered to them, which a failed attempt keeps for 600 s with
	// its dead spirits. Once the player is out of it and nobody is inside, destroy it, as its own checker would do later.
	private static void ResetArena(Player player)
	{
		WorldMapInstance arena = InstanceService.GetRegisteredInstance(320090000, player.GetObjectId());
		if (arena != null && player.GetWorldMapInstance() != arena && arena.GetPlayersInside().Count == 0)
			InstanceService.DestroyInstance(arena);
	}

	public override void OnQuestCompletedEvent(QuestEnv env)
	{
		DefaultOnQuestCompletedEvent(env, 2946);
	}
}
