# Classify how monsters came to attack the bot in NI-07 traces:
#   respawn-on-bot: the monster's SM_NPC_INFO appeared (a spawn) within aggro+2 m of the bot and it attacked within 20 s
#   patrol:         the monster was walking (walk-mask SM_MOVE) in the 20 s before its first attack and was not the pull target
# Also counts deaths. Aggro radii from npc_templates.xml srange.
# Usage: python scripts/sim/trace/aggro_causes.py run/natural-batch/<run>... (each a folder holding one *.trace.jsonl)
import json,glob,sys,re,collections,os
def t(v):
  h,m,s=v.split(':'); return int(h)*3600+int(m)*60+float(s)
srange={}
for m in re.finditer(r'<npc_template npc_id="(\d+)"[^>]*?srange="(\d+)"', open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..','..','game-server','data','static_data','npcs','npc_templates.xml'),encoding='utf-8').read()):
  srange[int(m.group(1))]=int(m.group(2))
tot=collections.Counter()
for d in sys.argv[1:]:
  fs=glob.glob(d+'/*.jsonl')
  if not fs: continue
  tm={}; seen={}; spawnnear={}; walked={}; engaged=set(); me=None; pos=None; c=collections.Counter(); targets={}; ex=[]
  for l in open(fs[0],encoding='utf-8'):
    e=json.loads(l); k=e['packet']; f=e.get('fields') or {}; vt=t(e['vt'])
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
      if a in spawnnear and vt-spawnnear[a][0]<20: c['respawn-on-bot']+=1; ex.append(('respawn',e['vt'],e['step'],a,tm[a]))
      elif a in walked and vt-walked[a]<20 and not (a in targets and vt-targets[a]<30): c['patrol']+=1; ex.append(('patrol',e['vt'],e['step'],a,tm[a]))
      else: c['other']+=1
    elif k=='SM_DIE': c['deaths']+=1; ex.append(('DEATH',e['vt'],e['step']))
  tot+=c
  print(d,dict(c))
  for x in ex:
    if x[0]!='patrol' or 'q2129' in x[2] or 'q2007' in x[2]: print('    ',x)
print('TOTAL',dict(tot))
