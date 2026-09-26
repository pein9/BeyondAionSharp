# For each death in a NI-07 trace: the decisions and the attackers in the window before it.
# Usage: python scripts/sim/trace/death_profile.py run/natural-batch/<run> [window seconds, default 90]
import json,sys,collections
from trace_input import events, seconds, stamp
window=float(sys.argv[2]) if len(sys.argv)>2 else 90
ev=list(events(sys.argv[1]))
tm={}; me=None
for e in ev:
  f=e.get('fields') or {}
  if e['packet']=='SM_NPC_INFO': tm.setdefault(f['objectId'],f['npcId'])
deaths=[i for i,e in enumerate(ev) if e['packet']=='SM_DIE']
skip={'navigation-decision','hazard-route-search','global-route-search','travel-plan','travel-plan-unavailable','road-route-selected'}
for di in deaths:
  dt=seconds(ev[di]); print('=== death at',stamp(ev[di]),ev[di]['step'])
  att=collections.Counter()
  for e in ev[:di]:
    if seconds(e)<dt-window: continue
    k=e['packet']; f=e.get('fields') or {}
    if k=='SM_ATTACK' and f.get('targetObjId') not in tm: att[(f['attackerObjId'],tm.get(f['attackerObjId']))]+=1
    if e['dir']!='action' or k in skip: continue
    if k=='combat-decision':
      print(' ',stamp(e),'CD',f.get('action'),f.get('skillId'),'tgt',f.get('targetObjectId'),'hp',f.get('hp'),'/',f.get('maxHp'),'mp',f.get('mp'),'att',f.get('observedAttackers'),'emerg',f.get('inEmergency'),'tHp%',f.get('targetHpPercent'))
    else:
      g={a:b for a,b in f.items() if a not in ('candidates','hazardCircles','start','destination','objective')}
      print(' ',stamp(e),k,json.dumps(g)[:240])
  print('  attackers (id,template):hits',dict(att))
