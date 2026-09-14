using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed class WorldView : MonoBehaviour
    {
        private GameController game;
        private Transform terrain,entityRoot,effectsRoot,caravan;
        private Transform tradeRoad;
        private CombatFeedback combatFeedback;
        private int animationEventCursor;
        private readonly Dictionary<int,EntityVisual> visuals=new Dictionary<int,EntityVisual>();
        private readonly Dictionary<string,Stack<EntityVisual>> pool=new Dictionary<string,Stack<EntityVisual>>();
        private readonly List<LineRenderer> projectiles=new List<LineRenderer>();
        private readonly List<LineRenderer> rallyFlags=new List<LineRenderer>();
        private readonly List<PulseVisual> pulses=new List<PulseVisual>();
        private Texture2D fogTexture;
        public Texture2D Minimap { get; private set; }
        private Color32[] fogPixels=new Color32[128*128],mapPixels=new Color32[128*128];
        private float nextFog;
        private Material fogMaterial,lineMaterial;
        private LineRenderer preview;
        private List<GameObject> rocks=new List<GameObject>();
        private readonly List<Mesh> outcropMeshes=new List<Mesh>();
        private MaterialPropertyBlock terrainTint;
        public void Initialize(GameController controller)
        {
            game=controller;terrainTint=new MaterialPropertyBlock();terrain=new GameObject("Authored Frontier Terrain").transform;terrain.SetParent(transform);
            entityRoot=new GameObject("Simulation Entity Views").transform;entityRoot.SetParent(transform);
            effectsRoot=new GameObject("Transient Effects").transform;effectsRoot.SetParent(transform);combatFeedback=effectsRoot.gameObject.AddComponent<CombatFeedback>();
            var mesh=new Mesh{name="128 metre frontier",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();
            for(int z=0;z<128;z+=4)for(int x=0;x<128;x+=4)
            {
                int start=vertices.Count;vertices.Add(new Vector3(x,0,z));vertices.Add(new Vector3(x,0,z+4));vertices.Add(new Vector3(x+4,0,z+4));vertices.Add(new Vector3(x+4,0,z));
                triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                float v=Mathf.PerlinNoise(x*.09f,z*.09f);Color c=Color.Lerp(new Color(.25f,.33f,.16f),new Color(.44f,.45f,.25f),v);
                // Vertex colors are authored terrain tint; a separate texture drives the same tint in URP.
                colors.AddRange(new[]{c,c,c,c});
            }
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);mesh.RecalculateNormals();
            var ground=new GameObject("Ground");ground.transform.SetParent(terrain,false);ground.AddComponent<MeshFilter>().sharedMesh=mesh;
            var groundMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));groundMat.SetFloat("_Smoothness",0);
            var terrainTexture=new Texture2D(128,128,TextureFormat.RGB24,false);var texels=new Color[128*128];
            for(int z=0;z<128;z++)for(int x=0;x<128;x++){float v=Mathf.PerlinNoise(x*.07f,z*.07f);Color c=Color.Lerp(new Color(.29f,.37f,.18f),new Color(.48f,.47f,.28f),v);float path=Mathf.Abs(x-(18+z*.88f+Mathf.Sin(z*.1f)*4));if(path<2.3f)c=Color.Lerp(c,new Color(.56f,.49f,.32f),.8f);texels[z*128+x]=c;}
            terrainTexture.SetPixels(texels);terrainTexture.Apply();groundMat.mainTexture=terrainTexture;
            var uv=new Vector2[vertices.Count];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(vertices[i].x/128,vertices[i].z/128);mesh.uv=uv;
            ground.AddComponent<MeshRenderer>().sharedMaterial=groundMat;
            fogTexture=new Texture2D(128,128,TextureFormat.RGBA32,false);fogTexture.filterMode=FilterMode.Bilinear;fogTexture.wrapMode=TextureWrapMode.Clamp;
            Minimap=new Texture2D(128,128,TextureFormat.RGBA32,false);Minimap.filterMode=FilterMode.Point;
            var fog=GameObject.CreatePrimitive(PrimitiveType.Quad);fog.name="Three-state visibility surface";fog.transform.SetParent(terrain,false);fog.transform.position=new Vector3(64,.08f,64);fog.transform.rotation=Quaternion.Euler(90,0,0);fog.transform.localScale=new Vector3(128,128,1);Destroy(fog.GetComponent<Collider>());
            fogMaterial=new Material(Shader.Find("Frontier/Fog"));fogMaterial.mainTexture=fogTexture;fog.GetComponent<Renderer>().sharedMaterial=fogMaterial;
            lineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));lineMaterial.color=Color.white;
            preview=NewLine("Building footprint",.12f);preview.loop=true;preview.positionCount=4;preview.gameObject.SetActive(false);
            for(int i=0;i<128*128;i++)fogPixels[i]=new Color32(9,17,21,0);fogTexture.SetPixels32(fogPixels);fogTexture.Apply();
        }
        public void ResetWorld()
        {
            combatFeedback.Bind(game.World,0,effectsRoot);animationEventCursor=game.World?.Events.Count??0;
            foreach(var visual in visuals.Values)Release(visual);visuals.Clear();
            ClearOutcrops();
            foreach(var line in projectiles)line.gameObject.SetActive(false);
            foreach(var line in rallyFlags)line.gameObject.SetActive(false);
            foreach(var p in pulses)Destroy(p.line.gameObject);pulses.Clear();preview.gameObject.SetActive(false);nextFog=0;
            if(caravan!=null)Destroy(caravan.gameObject);if(tradeRoad!=null)Destroy(tradeRoad.gameObject);
            if(game.World?.Rules.landmarks!=null)CreateTradeViews();
            if(game.World!=null)foreach(var r in game.World.TerrainObstacles)
            {
                var footprint=GridNavigation.BlockedFootprint(new Vector2(r.x,r.y),r.z);
                var rock=new GameObject("Navigation outcrop");rock.transform.SetParent(terrain,false);
                rock.transform.localPosition=new Vector3(footprint.center.x,0,footprint.center.y);
                var mesh=OutcropArt.CreateMesh(footprint.width*.5f,rocks.Count);outcropMeshes.Add(mesh);
                rock.AddComponent<MeshFilter>().sharedMesh=mesh;
                rock.AddComponent<MeshRenderer>().sharedMaterial=ProceduralArt.Mat(new Color(.38f,.39f,.33f));
                rocks.Add(rock);
            }
        }
        private void ClearOutcrops()
        {
            foreach(var mesh in outcropMeshes)if(mesh!=null)Destroy(mesh);outcropMeshes.Clear();
            foreach(var rock in rocks)if(rock!=null)Destroy(rock);rocks.Clear();
        }
        private void OnDestroy(){ClearOutcrops();}
        private void CreateTradeViews()
        {
            var route=game.World.Rules.landmarks.route;var vertices=new List<Vector3>();var indices=new List<int>();
            for(int i=1;i<route.Length;i++)
            {
                var a=route[i-1];var b=route[i];var direction=(b-a).normalized;var side=new Vector2(-direction.y,direction.x)*.6f;int n=vertices.Count;
                foreach(var point in new[]{a-side,b-side,b+side,a+side})vertices.Add(new Vector3(point.x,.025f,point.y));
                indices.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
            }
            tradeRoad=ProceduralArt.MeshPart(terrain,"Caravan road",vertices.ToArray(),indices.ToArray(),new Color(.52f,.43f,.27f)).transform;
            caravan=new GameObject("Neutral caravan").transform;caravan.SetParent(terrain,false);var start=game.World.CaravanPosition;caravan.position=new Vector3(start.x,0,start.y);
            Color wood=new Color(.33f,.21f,.11f);ProceduralArt.Part(caravan,"Wagon bed",PrimitiveType.Cube,new Vector3(0,.8f,0),new Vector3(1.5f,.3f,2.2f),wood);
            ProceduralArt.Part(caravan,"Canvas cover",PrimitiveType.Cube,new Vector3(0,1.4f,0),new Vector3(1.45f,1,1.9f),new Color(.73f,.66f,.48f));
            foreach(float x in new[]{-.85f,.85f})foreach(float z in new[]{-.7f,.7f}){var wheel=ProceduralArt.Part(caravan,"Wagon wheel",PrimitiveType.Cylinder,new Vector3(x,.48f,z),new Vector3(.85f,.09f,.85f),wood);wheel.transform.localRotation=Quaternion.Euler(0,0,90);}
            caravan.gameObject.SetActive(false);
        }
        private void Release(EntityVisual visual)
        {
            visual.root.gameObject.SetActive(false);if(!pool.TryGetValue(visual.key,out var stack)){stack=new Stack<EntityVisual>();pool[visual.key]=stack;}stack.Push(visual);
        }
        private EntityVisual Acquire(EntityState entity)
        {
            string key=entity.def.id+"_"+entity.owner;EntityVisual v;
            if(pool.TryGetValue(key,out var stack)&&stack.Count>0)v=stack.Pop();
            else
            {
                var root=new GameObject("Entity "+entity.id).transform;root.SetParent(entityRoot,false);
                var model=ProceduralArt.BuildEntity(entity,root);
                var circle=NewLine("Selection",.055f,root);circle.useWorldSpace=false;circle.loop=true;circle.positionCount=32;
                float radius=entity.def.kind==EntityKind.Building?Mathf.Max(entity.def.width,entity.def.depth)*.7f:entity.def.radius+ .35f;
                for(int i=0;i<32;i++)circle.SetPosition(i,new Vector3(Mathf.Sin(i*Mathf.PI/16)*radius,.12f,Mathf.Cos(i*Mathf.PI/16)*radius));
                var health=new GameObject("Health").transform;health.SetParent(root,false);health.localPosition=new Vector3(0,entity.def.kind==EntityKind.Building?5:2.8f,0);
                ProceduralArt.Part(health,"Health backing",PrimitiveType.Cube,Vector3.zero,new Vector3(1.65f,.16f,.035f),new Color(.12f,.15f,.12f));
                var bar=ProceduralArt.Part(health,"Health remaining",PrimitiveType.Cube,new Vector3(0,0,-.04f),new Vector3(1.55f,.11f,.045f),new Color(.3f,.74f,.34f)).transform;
                v=new EntityVisual{key=key,root=root,model=model,circle=circle,health=health,bar=bar,rig=model.GetComponent<UnitRig>()};
            }
            v.root.gameObject.SetActive(true);v.root.name="Entity "+entity.id;v.root.position=new Vector3(entity.position.x,0,entity.position.y);v.model.localScale=Vector3.one;v.model.localPosition=Vector3.zero;
            v.firedAt=v.hurtAt=v.deathAt=-1000;v.pickReady=false;v.pickRootPosition=v.root.position;v.facing=entity.facing.sqrMagnitude>.001f?Quaternion.LookRotation(new Vector3(entity.facing.x,0,entity.facing.y)):Quaternion.identity;v.model.rotation=v.facing;v.rig?.RestoreInitialPose();return v;
        }
        private void LateUpdate()
        {
            var world=game.World;if(game.InMenu||world==null)return;ConsumeAnimationEvents(world);
            foreach(var entity in world.Entities)
            {
                bool show=entity.Alive&&world.IsVisible(0,entity)&&!(entity.def.id=="trade_socket"&&world.TradingPostAt(entity.id)!=null);
                if(!visuals.TryGetValue(entity.id,out var visual))
                {if(!show)continue;visual=Acquire(entity);visuals.Add(entity.id,visual);}
                if(!show)
                {
                    if(!entity.Alive&&entity.def.kind==EntityKind.Unit&&world.Time-visual.deathAt<.7f&&world.Fog.At(0,entity.position)==VisibilityState.Visible)
                    {visual.root.gameObject.SetActive(true);visual.circle.gameObject.SetActive(false);visual.health.gameObject.SetActive(false);ApplyPose(visual,entity,world);continue;}
                    if(!entity.Alive){Release(visual);visuals.Remove(entity.id);}else visual.root.gameObject.SetActive(false);
                    continue;
                }
                visual.root.gameObject.SetActive(true);if(!world.Paused)visual.root.position=Vector3.Lerp(visual.root.position,new Vector3(entity.position.x,0,entity.position.y),.55f);
                if((visual.root.position-new Vector3(entity.position.x,0,entity.position.y)).sqrMagnitude>16)visual.root.position=new Vector3(entity.position.x,0,entity.position.y);
                if(!world.Paused&&world.Time-visual.firedAt>=.22f&&entity.def.kind==EntityKind.Unit&&entity.facing.sqrMagnitude>.01f)visual.facing=Quaternion.Slerp(visual.facing,Quaternion.LookRotation(new Vector3(entity.facing.x,0,entity.facing.y)),.35f);
                visual.model.localScale=Vector3.one*(entity.Complete?1:Mathf.Lerp(.2f,1,entity.construction));
                // Units use their projected selection radius. Only static building/resource
                // models need this envelope; do not enumerate animated unit renderers.
                if(entity.def.kind!=EntityKind.Unit)
                {
                    var scale=visual.model.lossyScale;
                    if((scale-visual.pickScale).sqrMagnitude>.0001f||!visual.pickReady)
                    {visual.pickBounds=EntityPickGeometry.WorldBounds(visual.model);visual.pickScale=scale;visual.pickReady=true;}
                    else visual.pickBounds.center+=visual.root.position-visual.pickRootPosition;
                    visual.pickRootPosition=visual.root.position;
                }
                bool selected=game.Controls.Selected.Contains(entity.id);visual.circle.gameObject.SetActive(selected);TintLine(visual.circle,entity.owner==1?new Color(1,.4f,.25f):new Color(.8f,.87f,.4f));
                visual.health.gameObject.SetActive(selected||entity.hp<world.MaxHp(entity)*.97f||!entity.Complete);
                visual.health.rotation=game.GameCamera.transform.rotation;float health=Mathf.Clamp01(entity.hp/world.MaxHp(entity));visual.bar.localScale=new Vector3(1.55f*health,.11f,.045f);visual.bar.localPosition=new Vector3((health-1)*.775f,0,-.04f);
                if(entity.def.kind==EntityKind.Unit)ApplyPose(visual,entity,world);
            }
            if(Time.unscaledTime>=nextFog){nextFog=Time.unscaledTime+.35f;UpdateFog(world);}
            UpdateProjectiles(world);UpdateRallyFlags(world);combatFeedback.Tick();UpdatePreview();
            if(caravan!=null){var p=world.CaravanPosition;caravan.gameObject.SetActive(world.Fog.At(0,p)==VisibilityState.Visible);var delta=new Vector3(p.x,0,p.y)-caravan.position;if(delta.sqrMagnitude>.00001f)caravan.rotation=Quaternion.LookRotation(delta);caravan.position=new Vector3(p.x,0,p.y);}
            for(int i=pulses.Count-1;i>=0;i--)
            {var pulse=pulses[i];float elapsed=Time.unscaledTime-pulse.time;if(elapsed>1){Destroy(pulse.line.gameObject);pulses.RemoveAt(i);continue;}SetRing(pulse.line,pulse.point,.5f+elapsed*1.3f);}
        }
        private void ConsumeAnimationEvents(World world)
        {
            for(;animationEventCursor<world.Events.Count;animationEventCursor++)
            {
                var ev=world.Events[animationEventCursor];if((ev.visibilityMask&1)==0)continue;
                if(ev.kind!="Fire"&&ev.kind!="Damage"&&ev.kind!="Death")continue;
                int id=ev.kind=="Damage"?ev.target:ev.entity;
                if(!visuals.TryGetValue(id,out var visual))
                {
                    var entity=world.Get(id);
                    if(entity==null||entity.def.kind!=EntityKind.Unit||world.Time-ev.time>=.7f||world.Fog.At(0,entity.position)!=VisibilityState.Visible)continue;
                    visual=Acquire(entity);visuals.Add(id,visual);
                }
                if(ev.kind=="Fire"){visual.firedAt=ev.time;if(ev.facing.sqrMagnitude>.001f)visual.facing=Quaternion.LookRotation(new Vector3(ev.facing.x,0,ev.facing.y));}
                else if(ev.kind=="Damage")visual.hurtAt=ev.time;
                else if(ev.kind=="Death")visual.deathAt=ev.time;
            }
        }
        private void UpdateRallyFlags(World world)
        {
            int used=0;
            foreach(int id in game.Controls.Selected)
            {
                var building=world.Get(id);
                if(building==null||!building.Alive||building.owner!=0||building.def.kind!=EntityKind.Building)continue;
                if(rallyFlags.Count<=used)rallyFlags.Add(NewLine("Owned rally intention",.09f));
                var flag=rallyFlags[used++];flag.gameObject.SetActive(true);flag.positionCount=5;
                // This is the player's remembered destination, not a query of a
                // hidden target. It remains a valid intention marker in the fog.
                Vector2 p=building.rally;flag.SetPosition(0,new Vector3(p.x,.15f,p.y));flag.SetPosition(1,new Vector3(p.x,3,p.y));
                flag.SetPosition(2,new Vector3(p.x+1.15f,2.6f,p.y));flag.SetPosition(3,new Vector3(p.x,2.2f,p.y));flag.SetPosition(4,new Vector3(p.x,3,p.y));
                TintLine(flag,building.rallyTarget==0?new Color(.95f,.79f,.3f):new Color(.3f,.92f,.8f));
            }
            for(int i=used;i<rallyFlags.Count;i++)rallyFlags[i].gameObject.SetActive(false);
        }
        private static void ApplyPose(EntityVisual visual,EntityState entity,World world)
        {
            var pose=UnitAnimation.Evaluate(entity,world.Time,visual.firedAt,visual.hurtAt,visual.deathAt,world.Get(entity.work.target));UnitAnimation.Apply(visual.rig,pose);
            float bob=pose.moving?Mathf.Abs(Mathf.Sin(pose.locomotionPhase))*.035f:0;
            visual.model.localPosition=new Vector3(0,bob-pose.death*.28f,0);
            visual.model.rotation=visual.facing*Quaternion.Euler(pose.hurt*-9,0,pose.death*84);
        }
        private void UpdateFog(World world)
        {
            foreach(var rock in rocks)
            {
                var point=rock.transform.position;var state=world.Fog.At(0,new Vector2(point.x,point.z));
                rock.SetActive(state!=VisibilityState.Unexplored);
                terrainTint.SetColor("_BaseColor",state==VisibilityState.Visible?new Color(.38f,.39f,.33f):new Color(.17f,.18f,.15f));
                rock.GetComponent<Renderer>().SetPropertyBlock(terrainTint);
            }
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                var visibility=world.Fog.At(0,new Vector2(x,y));int i=y*128+x;
                fogPixels[i]=new Color32(12,21,25,(byte)(visibility==VisibilityState.Visible?0:visibility==VisibilityState.Explored?170:252));
                mapPixels[i]=visibility==VisibilityState.Unexplored?new Color32(12,22,27,255):visibility==VisibilityState.Explored?new Color32(44,53,43,255):new Color32(87,103,57,255);
            }
            foreach(var e in world.Entities)
            {
                if(!e.Alive||!world.IsVisible(0,e))continue;
                int r=e.def.kind==EntityKind.Building?2:1;
                Color32 c=e.def.kind==EntityKind.Resource?(e.def.resource==ResourceKind.Coin?new Color32(210,188,108,255):new Color32(134,146,72,255)):e.owner==0?new Color32(88,179,231,255):new Color32(231,99,72,255);
                for(int dy=-r;dy<=r;dy++)for(int dx=-r;dx<=r;dx++){int x=Mathf.RoundToInt(e.position.x)+dx,y=Mathf.RoundToInt(e.position.y)+dy;if(x>=0&&x<128&&y>=0&&y<128)mapPixels[y*128+x]=c;}
            }
            var focus=game.Controls.CameraFocus;for(int i=-14;i<=14;i++){MapPixel((int)focus.x+i,(int)focus.y-11);MapPixel((int)focus.x+i,(int)focus.y+11);}for(int i=-11;i<=11;i++){MapPixel((int)focus.x-14,(int)focus.y+i);MapPixel((int)focus.x+14,(int)focus.y+i);}
            fogTexture.SetPixels32(fogPixels);fogTexture.Apply(false);Minimap.SetPixels32(mapPixels);Minimap.Apply(false);
        }
        private void MapPixel(int x,int y){if(x>=0&&x<128&&y>=0&&y<128)mapPixels[y*128+x]=new Color32(225,223,180,255);}
        private void UpdateProjectiles(World world)
        {
            int used=0;foreach(var p in world.Projectiles)
            {
                float t=Mathf.InverseLerp(p.firedAt,p.hitAt,world.Time);var target=world.Get(p.target);Vector2 end=target!=null&&world.IsVisible(0,target)?target.position:p.end;
                Vector3 start=new Vector3(p.start.x,1.25f,p.start.y),finish=new Vector3(end.x,.8f,end.y);Vector3 pos=Vector3.Lerp(start,finish,t);pos.y+=Mathf.Sin(t*Mathf.PI)*(p.splash>0?1.5f:.15f);
                Vector3 tail=pos-(finish-start).normalized*(p.splash>0?.65f:.4f);
                if(world.Fog.At(0,new Vector2(pos.x,pos.z))!=VisibilityState.Visible||world.Fog.At(0,new Vector2(tail.x,tail.z))!=VisibilityState.Visible)continue;
                while(projectiles.Count<=used)projectiles.Add(NewLine("Projectile",.055f));var line=projectiles[used++];line.gameObject.SetActive(true);line.positionCount=2;
                line.SetPosition(0,pos);line.SetPosition(1,tail);TintLine(line,new Color(1,.83f,.4f));
            }
            for(int i=used;i<projectiles.Count;i++)projectiles[i].gameObject.SetActive(false);
        }
        private void UpdatePreview()
        {
            var id=game.Controls.BuildingToPlace;preview.gameObject.SetActive(!string.IsNullOrEmpty(id));if(string.IsNullOrEmpty(id))return;
            var d=game.Rules.Entity(id);var p=game.World.BuildPoint(0,id,game.Controls.GroundMouse);bool valid=game.World.CanBuild(0,id,p)=="";
            TintLine(preview,valid?new Color(.5f,1,.35f):new Color(1,.22f,.12f));
            preview.SetPosition(0,new Vector3(p.x-d.width/2,.2f,p.y-d.depth/2));preview.SetPosition(1,new Vector3(p.x+d.width/2,.2f,p.y-d.depth/2));preview.SetPosition(2,new Vector3(p.x+d.width/2,.2f,p.y+d.depth/2));preview.SetPosition(3,new Vector3(p.x-d.width/2,.2f,p.y+d.depth/2));
        }
        public void Pulse(Vector2 point,int target,bool attack)
        {
            if(game.World==null)return;if(target!=0){var e=game.World.Get(target);if(e!=null)point=e.position;}
            var line=NewLine("Order acknowledged",.08f);line.loop=true;line.positionCount=32;TintLine(line,attack?new Color(1,.33f,.1f):new Color(.92f,.83f,.4f));
            pulses.Add(new PulseVisual{line=line,point=point,time=Time.unscaledTime});
        }
        private void TintLine(LineRenderer line,Color color){line.startColor=line.endColor=color;terrainTint.SetColor("_BaseColor",color);line.SetPropertyBlock(terrainTint);}
        private LineRenderer NewLine(string name,float width,Transform parent=null)
        {
            var go=new GameObject(name);go.transform.SetParent(parent??effectsRoot,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;line.widthMultiplier=width;line.useWorldSpace=true;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
        private static void SetRing(LineRenderer line,Vector2 p,float radius){for(int i=0;i<line.positionCount;i++)line.SetPosition(i,new Vector3(p.x+Mathf.Sin(i*Mathf.PI/16)*radius,.16f,p.y+Mathf.Cos(i*Mathf.PI/16)*radius));}
        public bool TryGetPickBounds(int id,out Bounds bounds)
        {if(visuals.TryGetValue(id,out var v)&&v.pickReady&&v.root.gameObject.activeInHierarchy){bounds=v.pickBounds;return true;}bounds=default;return false;}
        private class EntityVisual {public string key;public Transform root,model,health,bar;public UnitRig rig;public Quaternion facing;public float firedAt=-1000,hurtAt=-1000,deathAt=-1000;public LineRenderer circle;public Bounds pickBounds;public Vector3 pickScale,pickRootPosition;public bool pickReady;}
        private class PulseVisual {public LineRenderer line;public Vector2 point;public float time;}
    }
}
