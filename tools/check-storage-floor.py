"""Read-only regression: compare the authored bed mesh with collider-projected grid height.
Never launches Prospice. IDs describe the verified demo asset only; runtime uses no IDs.
"""
import json, math, re
from pathlib import Path
from inspect_game import archive_index, extract_node, UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
root = Path(__file__).resolve().parents[1]
archive = Path(r"E:\SteamLibrary\steamapps\common\Prospice Demo\Project Prospice_Data\data.unity3d")
with archive.open("rb") as f:
    idx, blocks, start = archive_index(f)
    def load(name):
        return extract_node(f, blocks, start, next(n for n in idx["nodes"] if n["name"] == name))
    level = UnityPy.load(load("level3"))
    objects = {o.path_id: o for o in level.objects}
    marker = objects[3570].read_typetree()
    floor = objects[5499].read_typetree()
    scale = objects[3789].read_typetree()["m_LocalScale"]["y"]
    env = UnityPy.Environment()
    for name in ("sharedassets3.assets", "sharedassets3.assets.resS"):
        env.load_file(load(name), name=name)
mesh = next(o for o in env.objects if o.path_id == 565).read()
handler = MeshHandler(mesh); handler.process()
vertices = handler.m_Vertices
triangles = []
for group in handler.get_triangles():
    for indices in group:
        a, b, c = (vertices[i] for i in indices)
        det = (b[2]-c[2])*(a[0]-c[0]) + (c[0]-b[0])*(a[2]-c[2])
        if abs(det) < 1e-9: continue
        triangles.append((a,b,c,det,min(a[0],b[0],c[0]),max(a[0],b[0],c[0]),min(a[2],b[2],c[2]),max(a[2],b[2],c[2])))
def render_height(x,z):
    heights=[]
    for a,b,c,det,x0,x1,z0,z1 in triangles:
        if not x0 <= x <= x1 or not z0 <= z <= z1: continue
        u=((b[2]-c[2])*(x-c[0])+(c[0]-b[0])*(z-c[2]))/det
        v=((c[2]-a[2])*(x-c[0])+(a[0]-c[0])*(z-c[2]))/det
        if min(u,v,1-u-v) >= -1e-6: heights.append(u*a[1]+v*b[1]+(1-u-v)*c[1])
    return max(heights) if heights else None
source = (root/'src/Prospice.Placement/TruckStorageGrid.cs').read_text()
clearance = float(re.search(r'CargoSurfaceClearance = ([.0-9]+)f', source).group(1))
physical = floor['m_Center']['y'] + floor['m_Size']['y']/2
# Interior avoids wheel arches and rails: isolate the flat floor which was missing.
old_hidden = new_visible = total = 0
minimum_clearance = math.inf
maximum_floor_gap = 0
for ix in range(33):
    for iz in range(49):
        x = -.65 + ix * 1.3/32; z = -2.55 + iz * 1.9/48
        y = render_height(x,z)
        if y is None: raise AssertionError('Missing authored floor')
        total += 1
        old_hidden += physical + .012/scale < y
        new_visible += physical + clearance/scale > y
        minimum_clearance = min(minimum_clearance, (physical + clearance/scale-y)*scale)
        maximum_floor_gap = max(maximum_floor_gap,(y-physical)*scale)
result=dict(samples=total,old_grid_hidden=old_hidden,new_grid_visible=new_visible,
            maximum_mesh_above_collider_m=maximum_floor_gap,minimum_new_clearance_m=minimum_clearance,
            marker_world_width_m=marker['m_LocalScale']['x']*scale,
            marker_world_length_m=marker['m_LocalScale']['z']*scale)
print(json.dumps(result,indent=2))
assert old_hidden == total and new_visible == total
