"""Rebuild original paint equipment in Blender 5.0; no game assets are used."""
import bpy, math, json, os, random
from pathlib import Path
from mathutils import Vector
root=Path(globals().get('args',{}).get('output',Path(__file__).resolve().parents[1]/'assets'/'paint'))
root.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
colors=['202124','F2F0E8','D83A36','F2C842','39965B','35B5BF','3465CE','B944A1']
names=['Black','White','Red','Yellow','Green','Cyan','Blue','Magenta']
materials=[]
for name,color,metal in [('Steel',(.32,.34,.36,1),.8),('Paint',(.84,.12,.1,1),0),('Ink',(.94,.93,.88,1),0),('Grip',(.035,.04,.045,1),0)]:
 m=bpy.data.materials.new(name);m.diffuse_color=color;m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=color;bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=.5
 materials.append(m)
def finish(o,name,mat):
 o.name=name;o.data.materials.append(materials[mat]);o['slot']=mat
 if o.type=='MESH':
  for p in o.data.polygons:p.use_smooth=True
 return o
def cyl(name,r,depth,loc,mat=0,rotation=(0,0,0)):
 bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=r,depth=depth,location=loc,rotation=rotation);return finish(bpy.context.object,name,mat)
def cube(name,scale,loc,mat):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 b=o.modifiers.new('Soft edges','BEVEL');b.width=.004;b.segments=2;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=b.name);return finish(o,name,mat)
def torus(name,major,minor,loc,mat=0):
 bpy.ops.mesh.primitive_torus_add(major_segments=32,minor_segments=6,major_radius=major,minor_radius=minor,location=loc);return finish(bpy.context.object,name,mat)
def text(name,value,loc,size,mat=2):
 bpy.ops.object.text_add(location=loc,rotation=(math.pi/2,0,0));o=bpy.context.object;o.name=name;o.data.body=value;o.data.align_x='CENTER';o.data.size=size;o.data.extrude=.0001
 bpy.ops.object.convert(target='MESH');return finish(bpy.context.object,name,mat)
def export(name,objects):
 floor=min((o.matrix_world @ Vector(v)).z for o in objects if o.type=="MESH" for v in o.bound_box)
 for o in objects:o.location.z-=floor
 bpy.context.view_layer.update()
 parts=[]
 for o in objects:
  if o.type!='MESH':continue
  mesh=o.data;mesh.calc_loop_triangles();uv=mesh.uv_layers.active;verts=[];norm=[];uvs=[]
  for tri in mesh.loop_triangles:
   for li in tri.loops:
    loop=mesh.loops[li];v=o.matrix_world@mesh.vertices[loop.vertex_index].co;n=o.matrix_world.to_3x3().inverted().transposed()@mesh.vertices[loop.vertex_index].normal;n.normalize()
    verts.extend((v.x,v.z,-v.y));norm.extend((n.x,n.z,-n.y));q=uv.data[li].uv if uv else (0,0);uvs.extend(q)
  parts.append(dict(Name=o.name,Slot=int(o['slot']),Vertices=verts,Normals=norm,UV=uvs))
 (root/(name+'.json')).write_text(json.dumps(dict(Parts=parts),separators=(',',':')))
 return sum(len(p['Vertices'])//9 for p in parts)
# 1-liter handle-free tin with simple lid and rolled seams.
cyl('Bucket shell',.065,.15,(0,0,.08),1);torus('Lower seam',.064,.003,(0,0,.008));torus('Upper rim',.066,.003,(0,0,.155))
cyl('Lid',.064,.004,(0,0,.158));cyl('Lid color swatch',.043,.001,(0,0,.161),1)
text('Brand','PROSPICE',(0,-.0655,.091),.011);text('Contents','PAINT / 1 L',(0,-.0657,.073),.008)
bucket=list(bpy.context.scene.objects)
# User-requested 2x physical bucket dimensions, including lid and label.
for o in bucket:o.location*=2;o.scale*=2
bpy.context.view_layer.update()
a=export('bucket',bucket)
for o in bucket:o.hide_render=True;o.hide_set(True)
# Cast-metal body, reinforced face and recessed nozzle stay within the held-tool envelope.
cube('Sprayer housing',(.076,.128,.06),(0,.002,.145),0)
cube('Rear bumper',(.078,.012,.062),(0,.068,.145),3)
cube('Front gasket',(.078,.006,.064),(0,-.064,.145),3)
cube('Reinforced front plate',(.080,.008,.066),(0,-.071,.145),0)
cyl('Nozzle collar',.022,.019,(0,-.083,.145),0,(math.pi/2,0,0))
cyl('Nozzle recess',.016,.002,(0,-.093,.145),3,(math.pi/2,0,0))
cyl('Nozzle tip',.009,.006,(0,-.095,.145),0,(math.pi/2,0,0))
cyl('Nozzle aperture',.0035,.001,(0,-.0985,.145),3,(math.pi/2,0,0))
for x in [-.030,.030]:
 for z in [.123,.167]:
  cyl('Face bolt',.004,.002,(x,-.076,z),0,(math.pi/2,0,0))
  cube('Bolt slot',(.0038,.001,.001),(x,-.0772,z),3)
# Recessed side vents and restrained red identification panels.
for side in [-1,1]:
 cube('Color side panel',(.002,.060,.020),(side*.038,.017,.143),1)
 for y in [-.038,-.026,-.014]:
  cube('Cooling slot',(.0015,.004,.024),(side*.039,y,.146),3)
cube('Grip',(.032,.036,.079),(0,.043,.086),3)
for z in [.056,.066,.076,.086]:
 cube('Grip rib',(.034,.038,.003),(0,.043,z),3)
cyl('Reservoir',.041,.075,(0,-.010,.055),0)
torus('Reservoir seam',.041,.002,(0,-.010,.019))
cyl('Reservoir cap',.043,.008,(0,-.010,.096),3)
torus('Reservoir shoulder',.041,.002,(0,-.010,.090))
cube('Trigger',(.014,.013,.028),(0,.016,.102),0)
cube('Trigger guard base',(.020,.046,.005),(0,.026,.067),0)
cube('Trigger guard front',(.020,.005,.034),(0,.005,.084),0)
# Keep the cast housing facets crisp; reserve smooth shading for curved parts.
for o in bpy.context.scene.objects:
 if o not in bucket and o.type=='MESH' and any(w in o.name for w in ['housing','plate','panel','bumper','gasket']):
  for face in o.data.polygons:face.use_smooth=False
sprayer=[o for o in bpy.context.scene.objects if o not in bucket];b=export('sprayer',sprayer)
for color,name in zip(colors,names):
 rgb=[int(color[i:i+2],16)/255 for i in (0,2,4)];image=bpy.data.images.new('Paint_'+name,width=256,height=256,alpha=True)
 random.seed(510);pixels=[]
 for y in range(256):
  for x in range(256):
   noise=random.uniform(-.018,.018);border=y<8 or y>247
   pixels.extend([max(0,min(1,(.13 if border else c)+noise)) for c in rgb]+[1])
 image.pixels=pixels;image.filepath_raw=str(root/(name.lower()+'.png'));image.file_format='PNG';image.save()
# Keep both model sources visible in the saved authoring file, separated for inspection.
for o in bucket:o.hide_set(False);o.hide_render=False
for o in sprayer:o.location.x+=.23
bpy.ops.wm.save_as_mainfile(filepath=str(root/'paint-equipment.blend'))
print(json.dumps({'bucket_triangles':a,'sprayer_triangles':b,'textures':8,'output':str(root)}))
