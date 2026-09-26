# Classify how monsters came to attack the bot in NI-07 traces:
#   respawn-on-bot: the monster's SM_NPC_INFO appeared (a spawn) within aggro+2 m of the bot and it attacked within 20 s
#   patrol:         the monster was walking (walk-mask SM_MOVE) in the 20 s before its first attack and was not the pull target
# Also counts deaths. Aggro radii from npc_templates.xml srange.
# Usage: python scripts/sim/trace/aggro_causes.py run/natural-batch/<run>... (each a folder holding one *.trace.jsonl)
import sys,re,collections,os
from trace_input import events, seconds, stamp
srange={}
for m in re.finditer(r'<npc_template npc_id="(\d+)"[^>]*?srange="(\d+)"', open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..','..','game-server','data','static_data','npcs','npc_templates.xml'),encoding='utf-8').read()):
  srange[int(m.group(1))]=int(m.group(2))
tot=collections.Counter()
for d in sys.argv[1:]:
  tm={}; seen={}; spawnnear={}; walked={}; engaged=set(); me=None; pos=None; c=collections.Counter(); targets={}; ex=[]
  for e in events(d):
    k=e['packet']; f=e.get('fields') or {}; vt=seconds(e)
    p=f.get('position')
    if isinstance(p,dict) and 'X' in p: pos=(p['X'],p['Y'])
    if k=='combat-decision' and f.get('targetObjectId'): targets[f['targetObjectId']]=vt
    if k=='SM_NPC_INFO':
      o=f['objectId']; tm[o]=f['npcId']
      if pos and srange.get(f['npcId']) and ((f['x']-pos[0])**2+(f['y']-pos[1])**2)**.5 < srange[f['npcId']]+2:
        spawnnear[o]=(vt,e['step'])
    elif k=='SM_DELETE': spawnnear.pop(f['objectId'],None); walked.pop(f['objectId'],None); engaged.discard(f['objectId'])
    elif k=='SM_MOVE' and f.get('movementMask') in (232,234): walked[f['objectId']]=vt
    elif k=='SM_ATTACK' and f.get('attackerObjId') in tm and f.get('targetObjId') not in tm:
      a=f['attackerObjId']
      if a in engaged: continue
      engaged.add(a)
      if a in spawnnear and vt-spawnnear[a][0]<20: c['respawn-on-bot']+=1; ex.append(('respawn',stamp(e),e['step'],a,tm[a]))
      elif a in walked and vt-walked[a]<20 and not (a in targets and vt-targets[a]<30): c['patrol']+=1; ex.append(('patrol',stamp(e),e['step'],a,tm[a]))
      else: c['other']+=1
    elif k=='SM_DIE': c['deaths']+=1; ex.append(('DEATH',stamp(e),e['step']))
  tot+=c
  print(d,dict(c))
  for x in ex:
    if x[0]!='patrol' or 'q2129' in x[2] or 'q2007' in x[2]: print('    ',x)
print('TOTAL',dict(tot))
