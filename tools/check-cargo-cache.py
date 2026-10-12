"""Offline validation of the private cargo surface cache. Does not launch Unity."""
import argparse, math, struct
from pathlib import Path
HASH='511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D'
def sub(a,b):return tuple(x-y for x,y in zip(a,b))
def add(a,b):return tuple(x+y for x,y in zip(a,b))
def mul(a,s):return tuple(x*s for x in a)
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def main():
 p=argparse.ArgumentParser();p.add_argument('--game-dir',default=r'E:\SteamLibrary\steamapps\common\Prospice Demo');a=p.parse_args()
 path=Path(a.game_dir)/'BepInEx/cache'/('prospice-cargo-'+HASH[:12]+'.bin')
 meshes=[]
 with path.open('rb') as f:
  def read(fmt):return struct.unpack(fmt,f.read(struct.calcsize(fmt)))
  assert f.read(8)==b'PCARGO01' and f.read(64).decode()==HASH
  count,=read('<i');assert 0<count<=4096
  for _ in range(count):
   length,=read('<i');name=f.read(length).decode();n,s,*bounds=read('<ii6f');assert 0<n<=250000 and 0<s<=256
   raw=read('<'+'f'*(n*3));vertices=list(zip(raw[::3],raw[1::3],raw[2::3]))
   raw=read('<'+'f'*(n*2));uv=list(zip(raw[::2],raw[1::2]));triangles=[]
   assert all(math.isfinite(x) for v in vertices+uv for x in v)
   for _ in range(s):
    k,=read('<i');assert k%3==0 and 0<=k<=1500000;indices=read('<'+'i'*k);assert all(0<=i<n for i in indices)
    triangles.extend(zip(indices[::3],indices[1::3],indices[2::3]))
   if name=='Bed' and n==5160:meshes.append((vertices,uv,triangles))
  assert f.tell()==path.stat().st_size
 assert len(meshes)==1,'Expected one unambiguous authored Bed surface'
 vertices,uv,triangles=meshes[0];samples=0;uvs=[]
 # Cast along gravity through real authored triangles for three vehicle attitudes.
 for gx,gz in ((0,0),(.3,.15),(-.25,.2)):
  gravity=(gx,-1,gz)
  for ix in range(11):
   for iz in range(17):
    target=(-.65+ix*1.3/10,1.279,-2.55+iz*1.9/16);origin=sub(target,mul(gravity,.5))
    hits=[]
    for ia,ib,ic in triangles:
     va,vb,vc=vertices[ia],vertices[ib],vertices[ic];e1=sub(vb,va);e2=sub(vc,va);h=cross(gravity,e2);det=dot(e1,h)
     if det<=1e-8:continue
     s=sub(origin,va);u=dot(s,h)/det
     if u<0 or u>1:continue
     q=cross(s,e1);v=dot(gravity,q)/det;t=dot(e2,q)/det
     if v<0 or u+v>1 or t<0:continue
     hits.append((t,add(origin,mul(gravity,t)),tuple(uv[ia][j]*(1-u-v)+uv[ib][j]*u+uv[ic][j]*v for j in (0,1))))
    assert hits,'Missing deck intersection';t,point,tex=min(hits)
    assert abs(point[1]-1.279)<.0001,'Ray selected something other than exposed floor'
    assert abs((point[1]-1.262413725)*1.5-.0248794125)<.0001,'Authored floor/collider clearance changed'
    uvs.append(tex);samples+=1
 assert max(x for x,y in uvs)-min(x for x,y in uvs)>.01
 assert max(y for x,y in uvs)-min(y for x,y in uvs)>.01
 print(f'PASS {count} cached meshes: finite geometry, indices, lengths and game hash; {samples} authored deck gravity intersections and nonconstant UV coordinates.')
if __name__=='__main__':main()
