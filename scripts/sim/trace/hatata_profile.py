# Profile the Hatata step (Q2129 step 3) of NI-07 runs: duration, kills, and every attacker with whether it was
# a respawn of something killed during the step (first seen within 3 m of where a same-template monster that died
# in the step was first seen).
# Usage: python scripts/sim/trace/hatata_profile.py run/natural-batch/<run>...
import json,glob,sys,collections
def t(v):
  h,m,s=v.split(':'); return int(h)*3600+int(m)*60+float(s)
def d2(a,b): return ((a[0]-b[0])**2+(a[1]-b[1])**2)**.5
for d in sys.argv[1:]:
  fs=glob.glob(d+'/*.jsonl')
  if not fs: print(d,'no trace'); continue
  first={}; firstpos={}; tmpl={}; killed=[]; step0=step1=None; attackers=collections.OrderedDict(); me=None
  deaths=[]; retreats=0; lastpos={}
  for l in open(fs[0],encoding='utf-8'):
    e=json.loads(l); k=e.get('packet'); f=e.get('fields') or {}; st=e.get('step','')
    if k=='SM_NPC_INFO':
      o=f['objectId']; tmpl[o]=f['npcId']
      if o not in first: first[o]=t(e['vt']); firstpos[o]=(f['x'],f['y'])
      lastpos[o]=(f['x'],f['y'])
    elif k=='SM_MOVE': lastpos[f['objectId']]=(f['x'],f['y'])
    elif k=='SmAttackStatus' and f.get('hpOrMp')==0 and f['objectId'] in tmpl:
      killed.append((t(e['vt']),f['objectId'],tmpl[f['objectId']],firstpos.get(f['objectId'])))
    if st.startswith('ni07-q2129-3'):
      if step0 is None: step0=t(e['vt'])
      step1=t(e['vt'])
      if k=='SM_ATTACK' and f.get('attackerObjId') in tmpl:
        a=f['attackerObjId']
        if a not in attackers:
          resp=[x for x in killed if x[2]==tmpl[a] and x[3] and firstpos.get(a) and d2(x[3],firstpos[a])<3 and first[a]>x[0] and x[0]>=step0-120]
          attackers[a]=(e['vt'],tmpl[a],round(first[a]-step0),'RESPAWN' if resp else '', firstpos.get(a) and tuple(round(c) for c in firstpos[a]))
      if k=='combat-retreat-route': retreats+=1
      if k=='accept-client-death-and-revive-at-bound-obelisk': deaths.append(e['vt'])
  if step0 is None: print(d,'no hatata step'); continue
  kills=[x for x in killed if step0<=x[0]<=step1]
  print(f"{d}: step {round(step1-step0)} s, kills {len(kills)}, retreats {retreats}, deaths {len(deaths)}")
  for a,v in attackers.items(): print('    attacker',a,v)
