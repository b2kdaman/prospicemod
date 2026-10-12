import bpy, math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[1]/'assets'/'paint';out=Path(__file__).resolve().parents[1]/'artifacts'/'paint-preview';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'paint-equipment.blend'))
originals=list(bpy.context.scene.objects)
# Export builder lays the sprayer to the right of the bucket in the authoring file.
buckets=[o for o in originals if o.name in ['Bucket shell','Lower seam','Upper rim','Lid','Lid color swatch','Brand','Contents']]
sprayer=[o for o in originals if o not in buckets]
colors=['202124','F2F0E8','D83A36','F2C842','39965B','35B5BF','3465CE','B944A1'];names=['Black','White','Red','Yellow','Green','Cyan','Blue','Magenta']
def aim(obj,point):obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
def area(name,loc,power,size):
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;aim(o,(0,0,.1))
for o in originals:o.hide_render=True
for i,(name,color) in enumerate(zip(names,colors)):
 offset=Vector(((i%4-1.5)*.36,(i//4)*.42,0))
 for src in buckets:
  o=src.copy();o.data=src.data.copy();bpy.context.collection.objects.link(o);o.hide_render=False;o.location+=offset
  if int(src.get('slot',0))==2 and name in ['White','Yellow','Cyan']:
   ink=src.data.materials[0].copy();ink.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.07,.08,.09,1);o.data.materials.clear();o.data.materials.append(ink)
  if int(src.get('slot',0))==1:
   mat=src.data.materials[0].copy();o.data.materials.clear();o.data.materials.append(mat);bs=mat.node_tree.nodes.get('Principled BSDF');tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(root/(name.lower()+'.png')));mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
 bpy.ops.object.text_add(location=(offset.x,offset.y-.17,.002),rotation=(0,0,0));t=bpy.context.object;t.data.body=name.upper();t.data.align_x='CENTER';t.data.size=.018;t.data.materials.append(bpy.data.materials['Ink'])
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.005));ground=bpy.context.object;mat=bpy.data.materials.new('Studio slate');mat.diffuse_color=(.045,.055,.07,1);mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.045,.055,.07,1);ground.data.materials.append(mat)
area('Key',(-1,-1.5,2.5),180,2);area('Fill',(1,.4,1.5),90,1.5);area('Rim',(-.5,1.8,1.5),140,1)
bpy.ops.object.camera_add(location=(.7,-1.3,1.2));cam=bpy.context.object;aim(cam,(0,.15,.07));cam.data.type='ORTHO';cam.data.ortho_scale=1.7;bpy.context.scene.camera=cam
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True;scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.world.color=(.18,.18,.18);scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.render.filepath=str(out/'paint-bucket-colors.png');bpy.ops.render.render(write_still=True)
# Hero close-up of the exact sprayer mesh, plus a red bucket for scale.
for o in list(scene.objects):
 if o.type in {'MESH','FONT'} and o!=ground:o.hide_render=True
for o in sprayer:o.hide_render=False
for o in buckets:o.hide_render=False
cam.location=(.7,-.9,.55);aim(cam,(.10,0,.16));cam.data.ortho_scale=.78;scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.filepath=str(out/'paint-sprayer-and-bucket.png');bpy.ops.render.render(write_still=True)
