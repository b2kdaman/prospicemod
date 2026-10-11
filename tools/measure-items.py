"""Read-only inventory of ordinary item collision dimensions from every serialized asset."""
import sys,json,math,csv
from pathlib import Path
from inspect_game import archive_index,extract_node,UnityPy
ROOT=Path(r'E:\SteamLibrary\steamapps\common\Prospice Demo')
OUT=Path(__file__).resolve().parents[1]/'local'/'item-measurements'
OUT.mkdir(parents=True,exist_ok=True)
def v(d):return [d[k] for k in ('x','y','z')]
def rot(p,q):
 u=v(q); w=q['w']; cross=lambda a,b:[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
 t=[2*a for a in cross(u,p)]; c=cross(u,t)
 return [p[i]+w*t[i]+c[i] for i in range(3)]
def ref(o,d,current):
 fid=d['m_FileID']; name=current
 if fid: name=o.assets_file.externals[fid-1].path.replace('\\','/').split('/')[-1]
 return (name,d['m_PathID'])
meshes={}; records=[]; script={}
with (ROOT/'Project Prospice_Data/data.unity3d').open('rb') as f:
 idx,blocks,start=archive_index(f)
 nodes=[n for n in idx['nodes'] if n['flags']&4]
 for n in nodes:
  print('Measuring',n['name'],flush=True)
  env=UnityPy.load(extract_node(f,blocks,start,n)); objects=list(env.objects)
  if n['name']=='globalgamemanagers.assets':
   for o in objects:
    if o.type.name=='MonoScript':script[o.path_id]=o.read().m_ClassName
  gos={}; transforms={}; colliders={}; components={}; rigid=set(); eligible=set(); excluded=set()
  for o in objects:
   k=o.type.name
   if k=='Mesh':
    d=o.read(); a=d.m_LocalAABB; meshes[(n['name'],o.path_id)]=([a.m_Center.x,a.m_Center.y,a.m_Center.z],[a.m_Extent.x,a.m_Extent.y,a.m_Extent.z])
   elif k in ('GameObject','Transform','BoxCollider','SphereCollider','CapsuleCollider','MeshCollider','Rigidbody'):
    d=o.read_typetree()
    if k=='GameObject':gos[o.path_id]=d;continue
    gid=d['m_GameObject']['m_PathID']
    if k=='Transform':transforms[o.path_id]=d
    elif k=='Rigidbody':rigid.add(gid)
    else:colliders.setdefault(gid,[]).append((k,d,ref(o,d['m_Mesh'],n['name']) if k=='MeshCollider' else None))
   elif k=='MonoBehaviour':
    d=o.read(check_read=False); fid=d.m_Script.file_id
    if fid<1 or not o.assets_file.externals[fid-1].path.endswith('globalgamemanagers.assets'):continue
    name=script.get(d.m_Script.path_id,'');gid=d.m_GameObject.path_id
    components.setdefault(gid,[]).append(name)
    if name in ('Pickupable','Packable'):eligible.add(gid)
    if any(s in name.lower() for s in ('anchor','winch','whinch','deploy')):excluded.add(gid)
  bygo={d['m_GameObject']['m_PathID']:tid for tid,d in transforms.items()}
  for gid in sorted(eligible & rigid):
   tid=bygo.get(gid)
   if tid is None:continue
   subtree=[]
   def walk(t):
    subtree.append(t)
    for child in transforms[t]['m_Children']:
     if child['m_PathID'] in transforms:walk(child['m_PathID'])
   walk(tid)
   if any(transforms[t]['m_GameObject']['m_PathID'] in excluded for t in subtree):continue
   shapes=[]
   for t in subtree:
    g=transforms[t]['m_GameObject']['m_PathID']
    if g!=gid and g in rigid:continue
    for kind,d,m in colliders.get(g,[]):
     if not d['m_Enabled'] or d['m_IsTrigger']:continue
     chain=[];cur=t
     while cur!=tid:
      chain.append(transforms[cur]);cur=transforms[cur]['m_Father']['m_PathID']
     shapes.append((kind,d,m,chain))
   if shapes:records.append(dict(asset=n['name'],id=gid,name=gos[gid]['m_Name'],scale=v(transforms[tid]['m_LocalScale']),shapes=shapes))
  del env,objects
rows=[]
for r in records:
 pts=[];missing=[];approx=False
 for kind,d,m,chain in r['shapes']:
  if kind=='BoxCollider':center=v(d['m_Center']);ext=[a*.5 for a in v(d['m_Size'])]
  elif kind=='MeshCollider':
   if m not in meshes:missing.append(str(m));continue
   center,ext=meshes[m];approx=True
  else:
   center=v(d['m_Center']);ext=[d['m_Radius']]*3
   if kind=='CapsuleCollider':ext[d['m_Direction']]=max(d['m_Height']*.5,d['m_Radius'])
   approx=True
  for i in range(8):
   p=[center[j]+ext[j]*(1 if i&(1<<j) else -1) for j in range(3)]
   for t in chain:
    p=rot([p[j]*v(t['m_LocalScale'])[j] for j in range(3)],t['m_LocalRotation']);p=[p[j]+v(t['m_LocalPosition'])[j] for j in range(3)]
   pts.append([p[j]*r['scale'][j] for j in range(3)])
 dims=[max(p[j] for p in pts)-min(p[j] for p in pts) for j in range(3)] if pts else [0]*3
 rows.append(dict(asset=r['asset'],id=r['id'],name=r['name'],x=dims[0],y=dims[1],z=dims[2],bounds_approximation=approx,unresolved_meshes=missing))
(OUT/'items.json').write_text(json.dumps(rows,indent=2))
with (OUT/'items.csv').open('w',newline='') as f:
 w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
print(json.dumps(rows,indent=2))

# Evaluate each distinct size once; repeated lost-cargo scene instances must not bias the grid.
profiles=sorted(set(tuple(round(a[k],3) for k in ('x','y','z')) for a in rows if not a['unresolved_meshes']))
dimensions=[d for p in profiles for d in p if d>.01]
candidates=[]
for step in [.05,.0625,.1,.125,.15,.2,.25]:
 errors=sorted((math.ceil(d/step-1e-4)*step-d)/d for d in dimensions)
 candidates.append(dict(step=step,mean_padding=sum(errors)/len(errors),worst_padding=max(errors)))
selected=max(c['step'] for c in candidates if c['mean_padding']<=.05 and c['worst_padding']<=.25)
report=dict(records=len(rows),unique_sizes=len(profiles),unresolved=sum(bool(a['unresolved_meshes']) for a in rows),selected_step=selected,candidates=candidates)
(OUT/'grid-calculation.json').write_text(json.dumps(report,indent=2))
print('GRID CALCULATION',json.dumps(report))