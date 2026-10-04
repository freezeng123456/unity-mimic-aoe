using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Real WorldView/World integration with explicitly controlled setup. These
    // are diagnostic URP images, not native-client screenshots or proof that a
    // human constructed the fixture through a paid ordinary match.
    public sealed class WaterStudy061Tests : NativeInputFixture
    {
        const int Width=2048,Height=1400,Layer=29;
        string fingerprint,inventoryHash,revision;
        readonly List<EntityState> subjects=new List<EntityState>();
        readonly List<CommandReceipt> commands=new List<CommandReceipt>();
        Vector3 cameraTarget;
        [Serializable] sealed class SourceEntry {public string path,sha256;}
        [Serializable] sealed class Inventory
        {
            public string schema="river-study-source-061",revision,sourceFingerprint;
            public string sourceState="Working tree including uncommitted files; excludes only temporary Unity InitTestScene GUID fixtures and standard hidden/cache files; no clean-commit claim.";
            public string fingerprintAlgorithm="SHA256 of compact ordinal-key JSON path-to-SHA256 map, ASCII escaped; matches verification_evidence.py";
            public SourceEntry[] files;
        }
        [Serializable] sealed class EntityReceipt
        {
            public int id,owner,visibleRenderers,visibleVertices,visibleTriangles;
            public string definition,geometrySha256;
            public Vector2 simulationPosition,facing;
            public Vector3 renderedRootPosition,modelPosition,modelEuler,boundsCenter,boundsSize;
            public float surfaceHeight,hp,remaining,construction;
            public bool waterborne,visibleToPlayer;
        }
        [Serializable] sealed class CommandReceipt
        {public float time;public int player,actor,target;public string order,reason;public bool accepted;}
        [Serializable] sealed class CaptureReceipt
        {
            public string schema="river-worldview-study-capture-061";
            public string purpose="Controlled river World/WorldView integration rendered through actual GameCamera and URP. Entities are fixture-spawned and repositioned; vision is explicitly revealed; AI is disabled; naval combat uses normal orders/ticks. Not native-client, paid construction, manual input, balance, frame-time or visual-quality acceptance.";
            public string file,sha256,recordedAtUtc,appVersion,unityVersion,graphicsDevice,renderPipeline,sourceFingerprint,sourceInventorySha256,referenceRevision,study,mapId,playerCivilization,opponentCivilization;
            public int width,height,frame,seed,geometryChangedPixelSamples,fireEvents,damageEvents,activeProjectiles;
            public float simulationTime,cameraOrthographicSize,cameraAspect;
            public Vector3 cameraPosition,cameraEuler,cameraTarget;
            public EntityReceipt[] entities;public CommandReceipt[] commands;
        }

        [UnityTest] public IEnumerator ThreeActualRiverWorldViewCamerasRecordWaterEconomyCrossingAndCombat()
        {
            string output=Environment.GetEnvironmentVariable("FRONTIER_WATER_STUDY_DIR");
            if(string.IsNullOrWhiteSpace(output))output=Path.Combine(Application.temporaryCachePath,"FrontierRiverStudy061");
            Directory.CreateDirectory(output);RecordSource(output);subjects.Clear();commands.Clear();
            game.SelectedMap=MapTopology.River;game.PlayerCivilization="france";game.OpponentCivilization="ottoman";game.StartMatch();game.enabled=false;
            game.Controls.enabled=false;game.Muted=true;game.EnvironmentSoundEnabled=false;game.MusicEnabled=false;
            game.World.Players[0].age=3;game.World.Players[1].age=3;
            var camera=game.GameCamera;var previousPosition=camera.transform.position;var previousRotation=camera.transform.rotation;
            float previousSize=camera.orthographicSize,previousAspect=camera.aspect;bool previousEnabled=camera.enabled;int previousMask=camera.cullingMask;
            camera.enabled=false;camera.aspect=Width/(float)Height;camera.cullingMask=1<<Layer;
            var sun=new GameObject("River study controlled directional sun").AddComponent<Light>();sun.transform.SetParent(session.transform,false);
            sun.type=LightType.Directional;sun.intensity=1.55f;sun.color=new Color(1,.96f,.87f);sun.shadows=LightShadows.Soft;sun.cullingMask=1<<Layer;sun.transform.rotation=Quaternion.Euler(48,-28,0);
            var rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);rt.Create();
            try
            {
                var dock=Subject("dock",0,new Vector2(55,28));
                var fisher=Subject("fishing_boat",0,new Vector2(62.5f,28));
                var gun=Subject("gunboat",0,new Vector2(66,33));
                var fish=Subject("fish",-1,new Vector2(64,28));
                var shoreGuard=Subject("musketeer",0,new Vector2(53,32));
                Reveal();Order(fisher,OrderKind.Gather,fish);float fishBefore=fish.remaining;
                for(int i=0;i<100&&fish.remaining>=fishBefore;i++)game.World.Tick(.1f);
                Assert.Less(fish.remaining,fishBefore,"The rendered fishing fixture must have actually harvested through normal simulation");
                Configure(new Vector3(83,28,7),new Vector3(61,0,29),16);yield return Settle();
                var dockModel=Model(dock);Assert.Greater(Vector3.Dot(dockModel.right,new Vector3(1,0,0)),.999f,"The warehouse quay must face the western bank's water side");
                Assert.IsNotNull(Model(fisher).GetComponent<UnitRig>());Assert.IsNotNull(Model(gun).GetComponent<UnitRig>());
                yield return Capture(rt,output,"01-riverbank-dock-fishing","Riverbank warehouse, fish shoal, working fishing boat and escort gunboat; actual Gather command consumed fish",new[]{dock,fisher,gun,fish,shoreGuard});

                MoveFixture(gun,new Vector2(64,40));MoveFixture(fisher,new Vector2(63,33));fisher.orders.Clear();
                var army=new List<EntityState>();
                for(int i=0;i<6;i++)army.Add(Subject(i%3==0?"pike":"musketeer",0,new Vector2(59.5f+i%3*3,38.6f+i/3*2.5f)));
                Reveal();Configure(new Vector3(84,17,16),new Vector3(63,1.3f,40),17);yield return Settle();
                foreach(var soldier in army)
                {
                    Assert.IsTrue(game.World.IsBridge(soldier.position));Assert.AreEqual(MapTopology.BridgeHeight,Root(soldier).position.y,.005f,"Land unit must stand on the raised crossing");
                }
                Assert.AreEqual(MapTopology.WaterHeight,Root(gun).position.y,.005f,"Boat below the same crossing must remain on water");
                Assert.Less(Model(gun).GetComponentsInChildren<MeshRenderer>().Max(r=>r.bounds.max.y),MapTopology.BridgeHeight-.28f,"Actual posed ship geometry must clear the bridge's underside");
                yield return Capture(rt,output,"02-raised-crossing-and-water-channel","Six land units on the raised timber crossing, gunboat in its independent water channel, fishing launch outside the deck; low oblique inspection",army.Concat(new[]{gun,fisher}).ToArray());

                MoveFixture(gun,new Vector2(64,55));MoveFixture(shoreGuard,new Vector2(54.5f,60));
                var enemy=Subject("gunboat",1,new Vector2(65,65));gun.hp=10000;enemy.hp=10000;
                Reveal();game.World.Fog.Reveal(1,gun.position,25);Order(gun,OrderKind.Attack,enemy);Order(enemy,OrderKind.Attack,gun);Order(shoreGuard,OrderKind.Attack,enemy);
                int firstEvent=game.World.Events.Count;
                bool FlightAfterDamage()=>game.World.Events.Skip(firstEvent).Any(e=>e.kind=="Damage"&&e.target==enemy.id)&&game.World.Projectiles.Any(p=>p.target==enemy.id);
                for(int i=0;i<350&&!FlightAfterDamage();i++)game.World.Tick(.1f);
                Assert.IsTrue(FlightAfterDamage(),"Combat image must include real damage and another actual projectile in flight, not only attack orders");
                Assert.IsTrue(game.World.Events.Skip(firstEvent).Any(e=>e.kind=="Fire"&&e.entity==gun.id&&e.target==enemy.id));
                Configure(new Vector3(85,29,35),new Vector3(63,0,60),19);yield return Settle();
                yield return Capture(rt,output,"03-naval-combat-and-shore-fire","Explicit gunboat versus gunboat combat plus shore musketeer fire; fixture vessels have 10000 HP to keep the diagnostic exchange alive",new[]{gun,enemy,shoreGuard});
            }
            finally
            {
                camera.enabled=previousEnabled;camera.cullingMask=previousMask;camera.aspect=previousAspect;camera.orthographicSize=previousSize;camera.transform.SetPositionAndRotation(previousPosition,previousRotation);
                rt.Release();UnityEngine.Object.Destroy(rt);if(sun!=null)UnityEngine.Object.Destroy(sun.gameObject);
            }
        }

        [UnityTest] public IEnumerator RiverLandRiverSwitchReleasesTerrainAndHiddenDockMeshesWithoutLeavingGhosts()
        {
            game.SelectedMap=MapTopology.River;game.StartMatch();game.enabled=false;var dock=Subject("dock",0,new Vector2(55,28));dock.construction=.1f;
            Reveal();yield return Ready();yield return null;
            var terrain=session.GetComponentsInChildren<Transform>().Single(t=>t.name=="River, banks and two raised crossings");
            var dockModel=Model(dock);var oldMeshes=terrain.GetComponentsInChildren<MeshFilter>(true).Concat(dockModel.GetComponentsInChildren<MeshFilter>(true)).Select(f=>f.sharedMesh).ToArray();
            Assert.IsTrue(oldMeshes.Length>5);Assert.AreEqual(1,dockModel.GetComponentsInChildren<MeshRenderer>().Length,"Only the foundation is currently visible");
            game.SelectedMap=MapTopology.Frontier;game.StartMatch();game.enabled=false;yield return Ready();yield return null;
            Assert.IsTrue(terrain==null);Assert.IsTrue(dockModel==null);
            foreach(var mesh in oldMeshes)Assert.IsTrue(mesh==null,"Map switch must release bridge/water and never-visible dock section meshes");
            Assert.IsFalse(session.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="River, banks and two raised crossings"));
            game.SelectedMap=MapTopology.River;game.StartMatch();game.enabled=false;yield return Ready();
            Assert.AreEqual(1,session.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="River, banks and two raised crossings"));
            Assert.AreEqual(2,session.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Raised crossing at ",StringComparison.Ordinal)));
        }

        EntityState Subject(string id,int owner,Vector2 position)
        {var entity=game.World.Spawn(id,owner,position);subjects.Add(entity);return entity;}
        void MoveFixture(EntityState entity,Vector2 point)
        {entity.position=entity.previousPosition=point;entity.orders.Clear();entity.facing=Vector2.up;}
        void Reveal(){game.World.Fog.Reveal(0,new Vector2(64,48),44);}
        void Order(EntityState actor,OrderKind order,EntityState target)
        {
            var result=game.World.Issue(new GameCommand{player=actor.owner,kind=CommandKind.Order,actors=new[]{actor.id},order=order,target=target.id,point=target.position});
            commands.Add(new CommandReceipt{time=game.World.Time,player=actor.owner,actor=actor.id,target=target.id,order=order.ToString(),accepted=result.accepted,reason=result.reason});
            Assert.IsTrue(result.accepted,order+": "+result.reason);
        }
        Transform Root(EntityState entity)
        {
            var root=session.transform.Find("Simulation Entity Views/Entity "+entity.id);Assert.IsNotNull(root,"Actual WorldView entity root "+entity.id);return root;
        }
        Transform Model(EntityState entity)=>Root(entity).GetChild(0);
        void Configure(Vector3 position,Vector3 target,float size)
        {cameraTarget=target;game.GameCamera.transform.position=position;game.GameCamera.transform.LookAt(target);game.GameCamera.orthographicSize=size;}
        IEnumerator Settle()
        {
            Reveal();
            // New visuals are created by the normal WorldView LateUpdate. Let
            // interpolation settle without advancing the simulation clock.
            for(int i=0;i<12;i++)yield return null;
            yield return new WaitForSecondsRealtime(.4f); // WorldView refreshes the shared fog texture every .35 seconds.
            foreach(var t in session.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
            yield return null;
        }
        IEnumerator Capture(RenderTexture rt,string output,string name,string study,EntityState[] featured)
        {
            var camera=game.GameCamera;var renderers=featured.SelectMany(e=>Model(e).GetComponentsInChildren<MeshRenderer>()).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            Assert.IsNotEmpty(renderers);var oldActive=RenderTexture.active;var pixels=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            try
            {
                foreach(var renderer in renderers)renderer.enabled=false;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply();var control=pixels.GetPixels32();
                foreach(var renderer in renderers)renderer.enabled=true;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply();var actual=pixels.GetPixels32();int changed=0;
                for(int i=0;i<actual.Length;i+=11)if(!actual[i].Equals(control[i]))changed++;
                Assert.Greater(changed,100,"Featured actual entity meshes must change visible pixels against the same terrain-only control");
                var records=featured.Select(Record).ToArray();foreach(var entity in featured)
                {
                    var p=camera.WorldToViewportPoint(Root(entity).position+Vector3.up*.5f);
                    Assert.That(p.x,Is.InRange(.015f,.985f),entity.def.id+" center outside diagnostic frame");Assert.That(p.y,Is.InRange(.015f,.985f),entity.def.id+" center outside diagnostic frame");
                }
                Assert.AreEqual(fingerprint,SourceFingerprint(SourceEntries()),"Do not bind a capture to source modified during its run");
                byte[] png=pixels.EncodeToPNG();File.WriteAllBytes(Path.Combine(output,name+".png"),png);
                var world=game.World;var receipt=new CaptureReceipt{file=name+".png",sha256=Hash(png),recordedAtUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,unityVersion=Application.unityVersion,
                    graphicsDevice=SystemInfo.graphicsDeviceName,renderPipeline=GraphicsSettings.currentRenderPipeline?.GetType().FullName??"none",sourceFingerprint=fingerprint,sourceInventorySha256=inventoryHash,referenceRevision=revision,
                    study=study,mapId=world.MapId,seed=world.Seed,playerCivilization=world.Players[0].civilization,opponentCivilization=world.Players[1].civilization,simulationTime=world.Time,width=Width,height=Height,frame=Time.frameCount,geometryChangedPixelSamples=changed,
                    fireEvents=world.Events.Count(e=>e.kind=="Fire"),damageEvents=world.Events.Count(e=>e.kind=="Damage"),activeProjectiles=world.Projectiles.Count,cameraPosition=camera.transform.position,cameraEuler=camera.transform.eulerAngles,cameraTarget=cameraTarget,cameraOrthographicSize=camera.orthographicSize,cameraAspect=camera.aspect,entities=records,commands=commands.ToArray()};
                File.WriteAllText(Path.Combine(output,name+".json"),JsonUtility.ToJson(receipt,true),new UTF8Encoding(false));
            }
            finally {foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=true;RenderTexture.active=oldActive;UnityEngine.Object.Destroy(pixels);}
        }
        EntityReceipt Record(EntityState entity)
        {
            var model=Model(entity);var renderers=model.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();Assert.IsNotEmpty(renderers);
            var bounds=renderers[0].bounds;var record=new EntityReceipt{id=entity.id,owner=entity.owner,definition=entity.def.id,simulationPosition=entity.position,facing=entity.facing,renderedRootPosition=Root(entity).position,modelPosition=model.position,modelEuler=model.eulerAngles,surfaceHeight=game.World.SurfaceHeight(entity.position,entity.def.waterborne),hp=entity.hp,remaining=entity.remaining,construction=entity.construction,waterborne=entity.def.waterborne,visibleToPlayer=game.World.IsVisible(0,entity),visibleRenderers=renderers.Length};
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                foreach(var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;record.visibleVertices+=mesh.vertexCount;record.visibleTriangles+=mesh.triangles.Length/3;
                    var matrix=model.worldToLocalMatrix*renderer.transform.localToWorldMatrix;writer.Write(mesh.vertexCount);
                    foreach(var vertex in mesh.vertices){var p=matrix.MultiplyPoint3x4(vertex);writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}foreach(int index in mesh.triangles)writer.Write(index);
                }
                writer.Flush();record.geometrySha256=Hash(stream.ToArray());
            }
            record.boundsCenter=bounds.center;record.boundsSize=bounds.size;return record;
        }
        void RecordSource(string output)
        {
            var entries=SourceEntries();fingerprint=SourceFingerprint(entries);revision=ReadRevision(Path.GetFullPath(Path.Combine(Application.dataPath,"..")));
            var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Inventory{revision=revision,sourceFingerprint=fingerprint,files=entries},true));inventoryHash=Hash(bytes);File.WriteAllBytes(Path.Combine(output,"source-inventory-water-061.json"),bytes);
        }
        static SourceEntry[] SourceEntries()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));var entries=new List<SourceEntry>();
            foreach(string directory in new[]{"Assets","Packages","ProjectSettings","Tools"})
            {
                var folder=Path.Combine(root,directory);if(!Directory.Exists(folder))continue;
                foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))
                {
                    string relative=file.Substring(root.Length+1).Replace('\\','/');if(relative.Split('/').Any(p=>p.StartsWith(".",StringComparison.Ordinal)||p=="__pycache__"))continue;
                    if(System.Text.RegularExpressions.Regex.IsMatch(relative,@"^Assets/InitTestScene[0-9a-f-]{36}\.unity(\.meta)?$"))continue;
                    entries.Add(new SourceEntry{path=relative,sha256=Hash(File.ReadAllBytes(file))});
                }
            }
            return entries.OrderBy(e=>e.path,StringComparer.Ordinal).ToArray();
        }
        static string SourceFingerprint(SourceEntry[] entries)
        {
            var text=new StringBuilder("{");for(int i=0;i<entries.Length;i++){if(i>0)text.Append(',');Quote(text,entries[i].path);text.Append(':');Quote(text,entries[i].sha256);}text.Append('}');return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        static void Quote(StringBuilder text,string value)
        {
            text.Append('"');foreach(char c in value)
            {
                switch(c)
                {
                    case '"':text.Append("\\\"");break;case '\\':text.Append("\\\\");break;case '\b':text.Append("\\b");break;case '\f':text.Append("\\f");break;
                    case '\n':text.Append("\\n");break;case '\r':text.Append("\\r");break;case '\t':text.Append("\\t");break;
                    default:if(c<32||c>126)text.Append("\\u").Append(((int)c).ToString("x4"));else text.Append(c);break;
                }
            }text.Append('"');
        }
        static string ReadRevision(string root)
        {
            try
            {
                string git=Path.Combine(root,".git");if(File.Exists(git)){var pointer=File.ReadAllText(git).Trim();if(!pointer.StartsWith("gitdir: ",StringComparison.Ordinal))return "unavailable";git=Path.GetFullPath(Path.Combine(root,pointer.Substring(8)));}
                string head=File.ReadAllText(Path.Combine(git,"HEAD")).Trim();if(!head.StartsWith("ref: ",StringComparison.Ordinal))return head;
                string reference=head.Substring(5),common=git;var commonFile=Path.Combine(git,"commondir");if(File.Exists(commonFile))common=Path.GetFullPath(Path.Combine(git,File.ReadAllText(commonFile).Trim()));
                foreach(string directory in new[]{git,common})
                {
                    var loose=Path.Combine(directory,reference);if(File.Exists(loose))return File.ReadAllText(loose).Trim();var packed=Path.Combine(directory,"packed-refs");
                    if(File.Exists(packed))foreach(var line in File.ReadLines(packed)){var fields=line.Split(' ');if(fields.Length==2&&fields[1]==reference)return fields[0];}
                }
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
            return "unavailable";
        }
        static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
