# Phase 2 audit: frozen candidate rejected on unseen seeds

The Phase 2 experiment in [the pilot](bot-learning-pilot.md) is complete.
An inspectable candidate passed paired development courses and was frozen
before the reserved evaluation split was opened. On unseen generator seed 118
it completed with two deaths versus baseline's one, so it **fails the adoption
gate**. The deterministic baseline remains active. No model was trained and no
full Ishalgen journey batch was started.

## Fixed benchmark and audit trail

- The two area courses, reset, loadout, and seed split are locked in
  [Phase 0](bot-learning-phase0.md); the repeatability and short starts are
  audited in [Phase 1](bot-learning-phase1.md).
- `NaturalMauPolicyParameters` exposes pull distance, patrol wait cycles,
  single/multiple-attacker healing, timed HP potion threshold, mana reserve,
  low-HP finishing threshold, and wounded-target preference for two engaged
  attackers. Input bounds preserve the learned-skill/legal-action checks and
  the existing emergency retreat fallback. The default vector is the
  deterministic baseline. The parameter marker and normalized `policy.json`
  are recorded in each trace directory and checked by
  `summarize_mau_course.py`.
- The initial search comparisons below use the same SIM module
  `b71f17af-7a46-45ea-9daf-4cd1c24fd27f` and verify paired course-reset
  hashes. Run directories are under `run/bot-learning-phase2/current/` and
  contain the full trace, summary, policy receipt, and per-decision record.
  The final frozen comparison uses module
  `60cd466c-5ce0-4df4-b3f7-45a0cdb2b99c`; each of its pairs uses the
  same module and course-reset hash. Report JSON files retain run IDs and
  trace hashes. Root `run/` is ignored by Git, so the notable full run
  directories are also archived under `docs/bot-learning-phase2-traces/`.
- The candidate runner enforces the reserved seed split and rejects an
  evaluation run without `bot-learning-phase2-frozen.json` and a matching
  candidate source hash. The [freeze receipt](bot-learning-phase2-frozen.json)
  records the policy, module, source hashes, development report, and reserved
  seeds used for the held-out comparison.

## Search and early stops

The deterministic 12-candidate random reference search uses generator seed
20260927 and is recorded in
[`bot-learning-phase2-screen.json`](bot-learning-phase2-screen.json).
Candidates first met the short two-attacker and Hatata-with-add starts; a
candidate was stopped on a new failure or extra death in paired development
courses. A small genetic generation was unnecessary because the initial
random winner and its local variants exposed a sharp pull-distance boundary.

| Candidate and evidence | Development result | Decision |
|---|---|---|
| Random `r05`, [expanded A–C](bot-learning-phase2-development-c.json) | Hatata-with-add seed 12 had zero deaths, but generator seed 17 failed at the 3,600-game-second cap with four deaths versus baseline completion and one death. | Reject. |
| Pull-distance midpoint `m01`, [midpoint report](bot-learning-phase2-midpoint-development.json) | Generator seed 17 improved, but seed 16 failed at the cap with four deaths versus baseline completion and one death. | Reject. |
| Potion 95% `s01`, [single-factor](bot-learning-phase2-single-factor.json) and [expanded finalists](bot-learning-phase2-finalist-development.json) | Generator seeds 16/17/12 looked safe; expanded seed 14 failed at the cap with two deaths versus baseline completion and one death. | Reject. |
| Single-attacker heal 60% `s03`, same reports | Initial generator seeds 16/17/12 looked safe; expanded seed 14 completed with three deaths versus baseline one. | Reject. |
| Single-attacker heal 57% `n01`, [narrow screen](bot-learning-phase2-narrow-screen.json) | Removed deaths on generator seeds 14/16/17, but added a death on seed 12, where baseline had zero. | Reject. |
| Potion 92% `n02`, same narrow screen | Seed 14 improved, but seed 12 failed at the cap with three deaths versus baseline completion and zero deaths. | Reject. |
| Single-attacker heal 56% `n03`, same narrow screen | Seed 14 failed the course despite zero deaths; baseline completed. | Reject. |
| Initial potion 91% `n04`, same narrow screen | Passed generator seeds 14/16/17/12/11/13, but seed 15 completed with two deaths and 3,331.725 game seconds versus baseline one death and 1,254.884 seconds. | Reject the initial unconditional behavior; inspect the changed decision. |

The first changed combat decisions explain why the narrow thresholds were
tested: `s03` first changed generator seed 14 at 389/669 HP and seed 16 at
375/669; `n01` first changed seed 12 at 377/669 and seed 14 at 374/669.
`s01` first added a potion on seed 14 at 617/669 HP; `n02` first changed seed
12 at 615/669 and seed 14 at 604/669. These are observed action boundaries,
not evidence that an unchosen action was safe. Small timing differences then
changed later patrol encounters and route recovery, so the paired outcomes
remain decisive.

On `n04` generator seed 15, route failures rose from 6 to 30 versus its
paired baseline. The candidate trace contains 22 `HazardRejected` and four
`GeometryRejected` `route-failed` events, including repeated Q2007 guarded
rejoin searches. This motivated inspection of the changed decision and recovery path.

Single-factor `s02` (multiple-attacker heal 75%) added a generator seed-16
death. `s04` (mana reserve +8), `s05` (finishing threshold 10%), and `s06`
(wounded-target preference) reproduced baseline on generator seeds 16/17/12.
The same three reproduced the full baseline outcome of the
Hatata-with-add seed-12 encounter: complete, one death, two maximum observed
attackers, 1,079.298 game seconds, six potions. See
[`bot-learning-phase2-hatata-single-factor.json`](bot-learning-phase2-hatata-single-factor.json).
The random/local candidate manifests and reports remain in `docs/` for the
complete search record.

## Code correction and limits

During development, a normal Java `NpcMoveController` patrol route was
misclassified as a pursuing enemy when it walked more than ten meters from its
first observed position without an `SM_ATTACK`. The bot now excludes NPCs with
an observed patrol path from that displacement-only pursuer test. Actual
attackers and overlapping aggro circles remain checked. The corrected binary
is the one used for the paired comparisons above; earlier exploratory runs
remain archived but are not compared across binaries.

## Frozen finalist and held-out gate

The first changed action on initial `n04` seed 15 was an early potion with no
observed aggressor. The final policy therefore applies its 91% timed-potion
threshold only under observed aggression. It keeps the baseline 90% threshold
when the single engaged target is below 60% HP, and favors a ready, learned
Infernal Blaze control cast before an early potion. This is still the `n04`
parameter vector, with the behavior and source frozen in the receipt above;
the baseline vector remains unchanged. The fixed rules preserve skill legality
and emergency fallback. The final [paired development report](bot-learning-phase2-duration91-development.json)
records 15 completed pairs across both courses: five baseline deaths versus
four candidate deaths, 10,111.395 versus 9,208.276 game seconds, and 129
versus 125 potions. No candidate pair added a death or failure. That was
enough to freeze the candidate, not evidence of held-out safety.

The [evaluation report](bot-learning-phase2-evaluation.json) contains all 40
paired full-course runs on the previously untouched seeds 101–120. Both
policies completed 39/40 courses; generator seed 115 failed under both and is
retained as an outcome. The 20 Hatata pairs matched exactly. On the generator
leg, baseline versus candidate totals were 17 versus 18 deaths, 186 versus
192 route failures, 28,607.796 versus 29,687.550 game seconds, and 351 versus
363 potions. Maximum observed attacker counts matched in every pair. Generator
seeds 108 and 113 saved 31.530 and 10.301 game seconds, respectively, without
changing deaths; seed 113 used one more potion. Seed 118 added a death, six
route failures, 11 potions, and 1,121.585 game seconds. Its baseline completed
with one death in 1,298.504 game seconds; the candidate completed with two
deaths in 2,420.089. This single paired death regression fails the stated
safety gate, regardless of the development gain.

The [familiar regression report](bot-learning-phase2-regression.json) contains
all eight course pairs for seeds 1/3/4/5 on the same frozen module. Every
status, death count, route-failure count, potion count, maximum observed
attacker count, and game time matched baseline. The completed reports carry
the freeze hash, module ID, policy receipts, reset hashes, run IDs, and trace
hashes. Full run directories for all changed unseen outcomes (108, 113, 118)
and the shared seed-115 failure are in the
[trace archive manifest](bot-learning-phase2-traces/manifest.json), with
per-file SHA-256 checksums. The remaining full traces remain in the local
`run/bot-learning-phase2/current/` directories. The
[checkout hash receipt](bot-learning-phase2-checkout-hashes.json) records both
original byte hashes and LF-normalized hashes so the frozen evidence can be
audited after Git changes line endings. The verifier checks every local trace
when available and always checks the four committed paired archives.

**Decision:** Phase 2 evaluation is complete, but no candidate is adopted.
Continue using the deterministic baseline. The seed-118 diagnosis below
separates the combat loss from its later Q2007 route retries. Any new
candidate needs a new development/freeze/evaluation cycle with fresh
unseen seeds; seeds 101–120 are now spent and must not be reused as a held-out
gate. Do not run the full frozen journey on this failed area candidate.

## Seed 118 failure diagnosis

The [deterministic trace comparison](bot-learning-phase2-seed118-diagnosis.json)
checks both archived trace hashes against the evaluation report. The first
different combat action is on Q2007 rejoin encounter 9 at 00:14:18.423: at
603/669 HP, one observed attacker, and a target at 64% HP, baseline casts the
ready Hallowed Strike (1615), while the candidate uses its 91% timed potion.
Both finish that encounter at 00:14:31.730. Encounter 10 begins at the same
00:15:08.695 virtual time with the same *client-observed* combat state.

In encounter 10, a second Mau attacks during Smite. Baseline receives a
successful Smite result at 00:15:17.612 and sees the target at 27% HP at turn
8. Candidate receives `SM_SKILL_CANCEL` for Smite (4013) at that same virtual
time and sees the target at 45%. Its next potion at 514/669 HP would also be
selected by the baseline 90% threshold; the earlier 91% choice was in the
previous encounter. The candidate then repeatedly attempts Healing Light while
the client reports an abnormal state or cast cancellations, and dies at
00:15:34.250. Both policies had already incurred eight hazard-rejected route
searches by then. The candidate's extra six searches occur on the subsequent
bind rejoin (`ni07-q2007-rejoin-700086-2`); they are downstream of the death,
not the cause of this death.

The traces establish the action difference and the later canceled Smite, but
do not expose enough server scheduling or random state to prove which earlier
event made that Smite cancel. The matching client-observed encounter start is
not a full world-state reset. If this pilot resumes, a narrow experiment could
preserve ready instant control before an above-baseline early potion and test
that on paired development courses and the two-attacker short start. A
distinct safety line could examine repeated heal refusals under two attackers.
Neither change is adopted from this diagnosis. Further level 9 tuning is
paused while the project moves to Ascension and subsequent leveling/questing.
A future finalist requires a new freeze and fresh unseen seeds; seed 118 is
now diagnostic data.
