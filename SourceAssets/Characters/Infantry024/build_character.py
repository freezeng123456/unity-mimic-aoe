"""Original Frontier infantry authored surfaces. Blender 5.2.1 LTS; no external assets.
Coordinates in this source are Unity-style X right, Y up, Z forward.
We convert them to Blender X reflected, Y back, Z up; reverse polygon winding before FBX export.
Run: blender -b --python build_character.py -- --out <directory>
"""
import bpy, math, os, sys, json, argparse
from mathutils import Vector
parser=argparse.ArgumentParser();parser.add_argument('--out',default=os.path.dirname(os.path.abspath(__file__)))
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
O=os.path.abspath(args.out);os.makedirs(O,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def U(v):return Vector((-v[0],-v[2],v[1]))
def material(name,rgb):
 m=bpy.data.materials.new(name);m.diffuse_color=(*rgb,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*rgb,1);p.inputs['Roughness'].default_value=.72;return m
coat=material('Team uniform',(.065,.25,.39));lining=material('Coat facings',(.55,.055,.035));linen=material('Warm linen',(.77,.71,.53));leather=material('Boot leather',(.07,.045,.035));hatmat=material('Felt',(.035,.044,.05));skin=material('Skin',(.62,.38,.23));hair=material('Hair',(.10,.057,.025));brass=material('Brass',(.68,.42,.11));dark=material('Eyes',(.028,.02,.013));glove=material('Hand',(.64,.43,.28))
pivots={};positions={}
def pivot(name,at,parent=None):
 o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.parent=parent;o.location=U(at)-(positions[parent.name] if parent else Vector());pivots[name]=o;positions[name]=U(at);return o
root=pivot('InfantryRoot',(0,0,0));body=pivot('Body',(0,0,0),root);head=pivot('Head pivot',(0,1.53,0),root)
for side,x in [('Left',-.3),('Right',.3)]:
 shoulder=pivot(side+' shoulder',(x,1.25,.05),root);elbow=pivot(side+' elbow',(x,.89,.05),shoulder);pivot(side+' hand',(x,.55,.05),elbow)
for side,x in [('Left',-.17),('Right',.17)]:pivot(side+' leg',(x,.65,0),root)
def mesh(name,vertices,faces,mat,parent=body,smooth=False):
 origin=positions[parent.name];data=bpy.data.meshes.new(name);data.from_pydata([U(v)-origin for v in vertices],[],[tuple(reversed(f)) for f in faces]);data.update();o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);o.parent=parent;o.location=(0,0,0);data.materials.append(mat)
 for f in data.polygons:f.use_smooth=smooth
 return o
# Deliberate elliptical cross-section rings, not scaled cubes/capsules.
def rings(name,rows,mat,parent=body,n=16,smooth=True):
 verts=[];faces=[]
 for y,rx,rz,cx,cz in rows:
  for i in range(n):
   a=2*math.pi*i/n;verts.append((cx+rx*math.cos(a),y,cz+rz*math.sin(a)))
 for j in range(len(rows)-1):
  for i in range(n):faces.append((j*n+i,(j+1)*n+i,(j+1)*n+(i+1)%n,j*n+(i+1)%n) if rows[-1][0]>rows[0][0] else (j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 faces.append(tuple(range(n)) if rows[-1][0]>rows[0][0] else tuple(reversed(range(n))));faces.append(tuple((len(rows)-1)*n+i for i in (reversed(range(n)) if rows[-1][0]>rows[0][0] else range(n))))
 return mesh(name,verts,faces,mat,parent,smooth)
def patch(name,vs,mat,parent=body):return mesh(name,vs,[tuple(range(len(vs)))],mat,parent)
def ellipsoid(name,at,scale,mat,parent=body,n=12,rows=7):
 cy=at[1];data=[]
 for j in range(rows+1):
  a=-math.pi/2+j*math.pi/rows;r=max(.001,math.cos(a));data.append((cy+scale[1]*math.sin(a),scale[0]*r,scale[2]*r,at[0],at[2]))
 return rings(name,data,mat,parent,n)
def solid_panel(name,front,thickness,mat,parent=body):
 verts=front+[(x,y,z-thickness) for x,y,z in front];n=len(front);faces=[tuple(range(n)),tuple(reversed(range(n,2*n)))];faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)];return mesh(name,verts,faces,mat,parent)
# A fitted waist, rounded chest and sloped shoulders form a single authored torso.
rings('Tailored coat',[(.65,.23,.145,0,-.012),(.80,.225,.15,0,0),(.93,.205,.147,0,0),(1.08,.245,.17,0,0),(1.22,.28,.173,0,-.005),(1.30,.27,.143,0,-.01),(1.37,.16,.103,0,-.018)],coat)
rings('Neck',[(1.33,.081,.077,0,0),(1.46,.084,.075,0,0)],skin,head,n=12)
solid_panel('Linen waistcoat',[(-.095,.76,.183),(.095,.76,.183),(.095,1.20,.206),(0,1.31,.206),(-.095,1.20,.206)],.012,linen)
# Separate red lapels and coat tails, with front openings exposing breeches.
for side in [-1,1]:
 solid_panel('Lapel '+str(side),[(side*.085,.94,.157),(side*.17,1.17,.17),(side*.13,1.34,.121),(side*.065,1.25,.203)],.016,lining)
 solid_panel('Coat tail '+str(side),[(side*.025,.69,-.005),(side*.225,.73,-.015),(side*.29,.36,-.07),(side*.065,.40,-.095)],.10,coat)
 solid_panel('Tail turnback '+str(side),[(side*.10,.41,-.093),(side*.28,.37,-.063),(side*.246,.51,-.05)],.012,lining)
for y,z in [(.81,.195),(.91,.199),(1.01,.202),(1.11,.205)]:ellipsoid('Waistcoat button',(0,y,z),(.014,.014,.007),brass,n=8,rows=4)
rings('Waist belt',[(.76,.232,.181,0,0),(.805,.227,.187,0,0)],leather,n=16)
solid_panel('Brass buckle',[(-.038,.758,.203),(.038,.758,.203),(.038,.807,.203),(-.038,.807,.203)],.012,brass)
for side,x in [('Left',-.3),('Right',.3)]:
 shoulder=pivots[side+' shoulder'];elbow=pivots[side+' elbow'];hand=pivots[side+' hand']
 rings(side+' sleeve upper',[(1.25,.107,.118,x,.05),(1.16,.106,.112,x,.05),(.99,.084,.089,x,.05),(.89,.075,.076,x,.05)],coat,shoulder,n=12)
 rings(side+' sleeve lower',[(.89,.078,.08,x,.05),(.76,.081,.082,x,.05),(.60,.069,.072,x,.05),(.55,.073,.077,x,.05)],coat,elbow,n=12)
 rings(side+' red cuff',[(.55,.079,.083,x,.05),(.625,.077,.081,x,.05)],lining,elbow,n=12)
 rings(side+' linen wrist',[(.532,.066,.066,x,.05),(.558,.071,.071,x,.05)],linen,elbow,n=12)
 rings(side+' hand mesh',[(.615,.046,.044,x,.05),(.57,.065,.046,x,.056),(.52,.057,.043,x,.067),(.49,.035,.034,x,.07)],glove,hand,n=10)
 ellipsoid(side+' thumb',(x-(.052 if x>0 else -.052),.555,.091),(.025,.054,.027),glove,hand,n=8,rows=5)
for side,x in [('Left',-.17),('Right',.17)]:
 leg=pivots[side+' leg']
 rings(side+' breeches',[(.65,.108,.12,x,0),(.52,.112,.105,x,0),(.38,.083,.088,x,.006),(.30,.072,.079,x,.01)],linen,leg,n=12)
 rings(side+' boot shaft',[(.325,.084,.09,x,0),(.29,.08,.083,x,0),(.10,.073,.081,x,.01),(.065,.085,.105,x,.035)],leather,leg,n=12)
 rings(side+' shaped boot',[(.018,.091,.16,x,.070),(.05,.094,.17,x,.070),(.10,.084,.143,x,.064),(.135,.069,.083,x,.01)],leather,leg,n=12)
# Faceted anatomical head: narrow jaw, cheek volume, forehead; explicit nose/brow/ears.
rings('Head anatomy',[(1.385,.081,.081,0,.025),(1.42,.128,.113,0,.016),(1.50,.166,.141,0,0),(1.59,.172,.146,0,-.002),(1.68,.16,.139,0,-.01),(1.735,.11,.096,0,-.022)],skin,head,n=16)
mesh('Sculpted nose',[(-.037,1.585,.133),(.037,1.585,.133),(0,1.635,.135),(0,1.555,.195),(-.033,1.535,.159),(.033,1.535,.159)],[(0,2,3),(2,1,3),(0,3,4),(1,5,3),(4,3,5)],skin,head,False)
for x in [-.071,.071]:
 ellipsoid('Eye',(x,1.600,.136),(.020,.009,.007),dark,head,n=8,rows=4)
 solid_panel('Brow',[(x-.028,1.624,.134),(x+.025,1.625,.135),(x+.02,1.64,.13),(x-.03,1.638,.13)],.006,hair,head)
 ellipsoid('Ear',(math.copysign(.168,x),1.556,-.005),(.035,.055,.032),skin,head,n=8,rows=5)
solid_panel('Mouth',[(-.035,1.477,.15),(.035,1.477,.15),(.033,1.485,.15),(-.033,1.485,.15)],.004,hair,head)
# Hair occupies the back of the head rather than painting the face dark.
verts=[];faces=[]
for y,rx,rz,cz in [(1.42,.128,.113,.016),(1.50,.166,.141,0),(1.59,.172,.146,-.002),(1.68,.16,.139,-.01),(1.735,.11,.096,-.022)]:
 for i in range(9):
  a=math.pi+math.pi*i/8;verts.append((rx*1.03*math.cos(a),y,cz+rz*1.03*math.sin(a)-.003))
for j in range(4):
 for i in range(8):faces.append((j*9+i,(j+1)*9+i,(j+1)*9+i+1,j*9+i+1))
mesh('Hair at nape',verts,faces,hair,head,True)
# A three-corner outline and folded brim, with an oval crown, not a conical hat.
verts=[];n=36
for inner in [True,False]:
 for i in range(n):
  a=2*math.pi*i/n;wave=(1+math.cos(3*(a-math.pi/2)))/2
  radius=.17 if inner else .235+.105*wave;y=1.755 if inner else 1.785+.065*(1-wave)
  verts.append((radius*math.cos(a),y,radius*.91*math.sin(a)))
faces=[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)];brim=mesh('Tricorne folded brim',verts,faces,hatmat,head,True)
mod=brim.modifiers.new('Brim thickness','SOLIDIFY');mod.thickness=.012
rings('Tricorne crown',[(1.756,.173,.153,0,0),(1.86,.162,.142,0,-.006),(1.913,.125,.115,0,-.012)],hatmat,head,n=24)
ellipsoid('Cockade',(0,1.833,.204),(.045,.04,.014),lining,head,n=12,rows=5)
ellipsoid('Cockade center',(0,1.833,.22),(.02,.02,.01),brass,head,n=10,rows=4)
# Merge rigid surfaces by pivot and material to avoid one renderer per tiny button.
for obj in list(bpy.context.scene.objects):
 if obj.type=='MESH':
  bpy.context.view_layer.objects.active=obj
  for modifier in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=modifier.name)
groups={}
for obj in list(bpy.context.scene.objects):
 if obj.type=='MESH':groups.setdefault((obj.parent.name,obj.data.materials[0].name),[]).append(obj)
for (parent_name,mat_name),objects in groups.items():
 bpy.ops.object.select_all(action='DESELECT')
 for obj in objects:obj.select_set(True)
 bpy.context.view_layer.objects.active=objects[0]
 if len(objects)>1:bpy.ops.object.join()
 bpy.context.object.name=parent_name+'__'+mat_name
# Source and export contain only original mesh/pivot data. Cameras/lights are study-only.
bpy.context.view_layer.update();bpy.ops.wm.save_as_mainfile(filepath=os.path.join(O,'FrontierInfantry.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:o.select_set(o.type in {'MESH','EMPTY'})
bpy.ops.export_scene.fbx(filepath=os.path.join(O,'FrontierInfantry.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True)
deps=bpy.context.evaluated_depsgraph_get();tris=verts_count=0
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 m=o.evaluated_get(deps).to_mesh();m.calc_loop_triangles();tris+=len(m.loop_triangles);verts_count+=len(m.vertices);o.evaluated_get(deps).to_mesh_clear()
json.dump({'blender':bpy.app.version_string,'triangles':tris,'vertices':verts_count,'objects':len(bpy.context.scene.objects),'coordinateSource':'X right Y up Z forward; converted to Blender -X/-Z/Y with winding reversal to account for Unity handedness before FBX -Z/Y export','materials':sorted({m.name for obj in bpy.context.scene.objects if obj.type=='MESH' for m in obj.data.materials}),'status':'Original study, requires Unity import and animation validation'},open(os.path.join(O,'metrics.json'),'w'),indent=2)
# Fixed, lit front/side/three-quarter studies. Blender camera local +Y tracks global +Z.
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1000;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.15,.19,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
scene.view_settings.view_transform='AgX'
def area(name,at,energy,size):
 d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=U(at);o.rotation_euler=(U((0,1,0))-o.location).to_track_quat('-Z','Y').to_euler()
area('Key',(3,4,4),450,4);area('Fill',(-3,2,2),220,3);area('Rim',(0,3,-3),350,3)
d=bpy.data.cameras.new('Study camera');cam=bpy.data.objects.new('Study camera',d);bpy.context.collection.objects.link(cam);scene.camera=cam;d.type='ORTHO';d.ortho_scale=2.35
for name,at in [('front',(0,1.0,5)),('side',(5,1.0,0)),('three-quarter',(3,2.2,5))]:
 cam.location=U(at);cam.rotation_euler=(U((0,1,0))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=os.path.join(O,name+'.png');bpy.ops.render.render(write_still=True)
