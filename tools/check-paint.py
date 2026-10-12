"""Validate original paint meshes/textures and the private world mesh cache."""
from pathlib import Path
import json,math,struct,argparse
p=argparse.ArgumentParser();p.add_argument('--game-dir',default=r'E:\SteamLibrary\steamapps\common\Prospice Demo');a=p.parse_args();root=Path(__file__).resolve().parents[1]
for name in ['bucket','sprayer']:
 d=json.loads((root/'assets/paint'/(name+'.json')).read_text());triangles=0
 for part in d['Parts']:
  v,n,uv=part['Vertices'],part['Normals'],part['UV'];assert len(v)%9==0 and len(n)==len(v) and len(uv)*3==len(v)*2
  assert all(math.isfinite(x) for x in v+n+uv);assert part['Slot'] in range(4)
  assert all(abs(x)<1 for x in v);triangles+=len(v)//9
 assert triangles<6000
 print(f'PASS original {name}: {triangles} triangles, finite positions/normals/UVs, material slots and metre scale')
colors=['black','white','red','yellow','green','cyan','blue','magenta']
for name in colors:
 data=(root/'assets/paint'/(name+'.png')).read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n';assert struct.unpack('>II',data[16:24])==(256,256)
print('PASS eight 256x256 color textures')
path=Path(a.game_dir)/'BepInEx/cache/prospice-paint-511B68EBDE54.bin';world=0;total=0
with path.open('rb') as f:
 def read(fmt):return struct.unpack(fmt,f.read(struct.calcsize(fmt)))
 assert f.read(8)==b'PPAINT01';assert f.read(64)==b'511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D'
 count,=read('<i');assert 0<count<65536
 for _ in range(count):
  length,=read('<i');assert 0<length<4096;name=f.read(length).decode();nv,ns,*bounds=read('<ii6f');assert 0<nv<=2000000 and 0<ns<=4096 and all(math.isfinite(x) for x in bounds)
  if name.startswith('Combined Mesh ('):world+=1
  values=read('<'+'f'*(nv*5));assert all(math.isfinite(x) for x in values)
  for s in range(ns):
   ni,=read('<i');assert ni%3==0 and 0<=ni<=12000000;indices=read('<'+'i'*ni);assert all(0<=i<nv for i in indices);total+=ni//3
 assert f.tell()==path.stat().st_size
assert world>0
print(f'PASS private paint cache: {count} meshes including {world} static batches, {total} valid triangles; finite geometry and exact length/hash')
