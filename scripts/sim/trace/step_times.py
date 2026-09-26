# Virtual and real time per quest (step prefix ni07-qNNNN) for two NI-07 traces side by side.
# Usage: python scripts/sim/trace/step_times.py run/natural-batch/<runA> run/natural-batch/<runB>
import json,glob,sys,collections,datetime
def t(v):
  h,m,s=v.split(':'); return int(h)*3600+int(m)*60+float(s)
def prof(d):
  f=glob.glob(d+'/*.jsonl')[0]; vt=collections.OrderedDict(); rt=collections.OrderedDict(); prev=None
  first=last=None
  for l in open(f,encoding='utf-8'):
    try: e=json.loads(l)
    except: continue
    st=e.get('step',''); q=st.split('-')[1] if st.startswith('ni07-') else st
    v=t(e['vt']); r=datetime.datetime.fromisoformat(e['ts'].replace('Z','+00:00')).timestamp()
    if first is None: first=(v,r)
    if prev and prev[0]!=q: pass
    if q not in vt: vt[q]=[v,v]; rt[q]=[r,r]
    vt[q][1]=v; rt[q][1]=r; last=(v,r)
  return vt,rt,first,last
a=prof(sys.argv[1]); b=prof(sys.argv[2])
print(f"total vt {a[3][0]-a[2][0]:.0f}s rt {a[3][1]-a[2][1]:.0f}s | vt {b[3][0]-b[2][0]:.0f}s rt {b[3][1]-b[2][1]:.0f}s")
keys=list(dict.fromkeys(list(a[0])+list(b[0])))
for k in keys:
  va=a[0].get(k); vb=b[0].get(k); ra=a[1].get(k); rb=b[1].get(k)
  fa=f"{va[1]-va[0]:7.0f} {ra[1]-ra[0]:5.0f}" if va else " "*13
  fb=f"{vb[1]-vb[0]:7.0f} {rb[1]-rb[0]:5.0f}" if vb else " "*13
  if (va and (va[1]-va[0]>120 or ra[1]-ra[0]>10)) or (vb and (vb[1]-vb[0]>120 or rb[1]-rb[0]>10)): print(f"{k:14} {fa} | {fb}")
