using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    // Actual InputSystem gestures -> production UGUI -> StartGuided.
    // No SpawnHere, direct StartGuided, progress unlock, grant or scenario mutation.
    // Screenshots are original stable rendered frames, not completion evidence.
    public sealed class CivilizationRoutes084UiTests:NativeInputFixture
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        const string EvidenceVariable="FRONTIER084_CIV_UI_EVIDENCE";
        static readonly string[] Routes={ClassicCivilizationRoutes084.British,ClassicCivilizationRoutes084.Dutch,ClassicCivilizationRoutes084.French,ClassicCivilizationRoutes084.Ottoman};
        string storage,evidence;
        Receipt receipt;
        [Serializable] sealed class RouteRow
        {
            public string id,civilization,initialInstruction;
            public int age,workers,entities,controllers,commands;
            public float worldTime;
            public Cost resources;
            public bool exactNormalOpening,findButtonClicked,locateButtonClicked;
        }
        [Serializable] sealed class Receipt
        {
            public string test,status,error,sourceFingerprint;
            public string scope="Actual paginated menu and four normal Age I UI starts; InputSystem/UGUI, not OS-native input or paid-route completion";
            public string futureInstructionScope="All later catalog instructions are checked against the actual guide text style by complete TextGenerator geometry; no later stage or screenshot is manufactured";
            public List<string> pages=new List<string>(),screenshots=new List<string>();
            public List<RouteRow> starts=new List<RouteRow>();
        }
        [UnitySetUp] public IEnumerator IsolateCivilizationUiStorage()
        {
            Assert.IsFalse(game.Settings.Persistent);Assert.IsTrue(game.Progress.MemoryOnly);
            storage=Path.Combine(Path.GetTempPath(),"Frontier-civilization084-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(storage);
            foreach(string name in new[]{"saveDirectoryOverride","reportDirectoryOverride","replayDirectoryOverride"})
            {
                var field=typeof(GameController).GetField(name,Private);Assert.IsNotNull(field,name);
                field.SetValue(game,Path.Combine(storage,name));
            }
            // Root supplies an owned run directory when raw screenshots are wanted.
            // No fallback writes into the user's project or persistent account.
            evidence=Environment.GetEnvironmentVariable(EvidenceVariable);
            if(!string.IsNullOrEmpty(evidence)){evidence=Path.GetFullPath(evidence);Directory.CreateDirectory(evidence);}
            receipt=new Receipt{test=TestContext.CurrentContext.Test.Name,sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT")};
            game.ReturnToMenu();yield return Ready();
            Assert.IsTrue(game.InMenu);Assert.IsNull(game.World);
            foreach(string id in Routes){Assert.IsNotNull(GuidedPlayCatalog.Find(id),id+" must be integrated");Assert.IsTrue(game.Progress.IsUnlocked(id),"New country routes begin unlocked");}
        }
        [UnityTearDown] public IEnumerator FinishCivilizationUiEvidence()
        {
            if(receipt!=null&&!string.IsNullOrEmpty(evidence))
            {
                receipt.status=TestContext.CurrentContext.Result.Outcome.Status.ToString();receipt.error=TestContext.CurrentContext.Result.Message;
                File.WriteAllText(Path.Combine(evidence,receipt.test+".json"),JsonUtility.ToJson(receipt,true));
            }
            if(game!=null)game.enabled=false;
            if(!string.IsNullOrEmpty(storage)&&Directory.Exists(storage))Directory.Delete(storage,true);
            yield return null;
        }
        Button[] Buttons()=>session.GetComponentsInChildren<Button>().Where(b=>b.isActiveAndEnabled).ToArray();
        Text[] Texts()=>session.GetComponentsInChildren<Text>().Where(t=>t.isActiveAndEnabled).ToArray();
        Button ButtonNamed(string title)=>Buttons().Single(b=>b.name==title);
        Button ButtonPrefix(string prefix)=>Buttons().Single(b=>b.name.StartsWith(prefix,StringComparison.Ordinal));
        Button RouteButton(GuidedPlayDefinition d)=>Buttons().Single(b=>b.name.StartsWith(d.Title+"\n",StringComparison.Ordinal));
        static Rect Bounds(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var pixels=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            return Rect.MinMaxRect(pixels.Min(p=>p.x),pixels.Min(p=>p.y),pixels.Max(p=>p.x),pixels.Max(p=>p.y));
        }
        static void VisibleBounds(RectTransform rect,string context)
        {
            var bounds=Bounds(rect);Assert.Greater(bounds.width,1,context);Assert.Greater(bounds.height,1,context);
            Assert.GreaterOrEqual(bounds.xMin,-1,context);Assert.GreaterOrEqual(bounds.yMin,-1,context);
            Assert.LessOrEqual(bounds.xMax,Screen.width+1,context);Assert.LessOrEqual(bounds.yMax,Screen.height+1,context);
        }
        static void InsideParent(RectTransform child,Vector3 localPoint,string context)
        {
            var parent=child.parent as RectTransform;Assert.IsNotNull(parent,context+" actual parent container");
            var p=parent.InverseTransformPoint(child.TransformPoint(localPoint));var rect=parent.rect;
            // Geometry must stay inside its padded parent/button; this epsilon
            // covers only transform arithmetic, not a glyph or layout overflow.
            Assert.That(p.x,Is.InRange(rect.xMin-.001f,rect.xMax+.001f),context+" outside parent horizontally");
            Assert.That(p.y,Is.InRange(rect.yMin-.001f,rect.yMax+.001f),context+" outside parent vertically");
        }
        void Clickable(Button button)
        {
            Assert.IsTrue(button.interactable,button.name);var rect=(RectTransform)button.transform;
            VisibleBounds(rect,button.name);Assert.Greater(rect.rect.width,20);
            foreach(var corner in new[]{rect.rect.min,new Vector2(rect.rect.xMax,rect.rect.yMin),rect.rect.max,new Vector2(rect.rect.xMin,rect.rect.yMax)})InsideParent(rect,corner,button.name+" actual hitbox");
        }
        static float FontPixel(Text label,string context)
        {
            Assert.Greater(label.pixelsPerUnit,0,context+" positive native font scale");
            Assert.IsFalse(float.IsInfinity(label.pixelsPerUnit)||float.IsNaN(label.pixelsPerUnit),context+" finite native font scale");
            return 1f/label.pixelsPerUnit;
        }
        static void GlyphBounds(Text label,Vector3 point,float fontPixel,string context)
        {
            var rect=label.rectTransform.rect;float bearing=fontPixel+.0001f;
            // Unity's glyph bearing may extend by one font pixel beyond its
            // advance rectangle. Do not confuse that with clipped characters.
            Assert.That(point.x,Is.InRange(rect.xMin-bearing,rect.xMax+bearing),context+" glyph overflows horizontally beyond one font pixel");
            Assert.That(point.y,Is.InRange(rect.yMin-bearing,rect.yMax+bearing),context+" glyph overflows vertically beyond one font pixel");
            InsideParent(label.rectTransform,point,context+" actual padded container");
        }
        static int CompleteGlyphsAndStyle(Text label,string text,string context)
        {
            Assert.IsNotNull(label.font,context);label.font.RequestCharactersInTexture(text,label.fontSize,label.fontStyle);
            foreach(char c in text.Where(c=>c>='\u2e80').Distinct())Assert.IsTrue(label.font.HasCharacter(c),context+" missing Chinese glyph "+c);
            // Use Text's actual generation scale. Like UGUI Text.OnPopulateMesh,
            // divide generated font-pixel vertices by pixelsPerUnit for UI space.
            // The strict full-vs-clipped character count still detects truncation.
            var settings=label.GetGenerationSettings(label.rectTransform.rect.size);settings.resizeTextForBestFit=false;
            float fontPixel=FontPixel(label,context);
            using(var full=new TextGenerator())using(var clipped=new TextGenerator())
            {
                settings.verticalOverflow=VerticalWrapMode.Overflow;Assert.IsTrue(full.Populate(text,settings),context);
                Assert.Greater(full.characterCountVisible,0,context);Assert.Greater(full.vertexCount,0,context);
                settings.verticalOverflow=VerticalWrapMode.Truncate;Assert.IsTrue(clipped.Populate(text,settings),context);
                Assert.AreEqual(full.characterCountVisible,clipped.characterCountVisible,context+" must generate the complete title/instruction inside its actual text region");
                foreach(var vertex in full.verts)GlyphBounds(label,vertex.position*fontPixel,fontPixel,context+" full text");
                return full.characterCountVisible;
            }
        }
        static void ActualChineseText(Text label,string context)
        {
            Assert.IsTrue(label.isActiveAndEnabled,context);VisibleBounds(label.rectTransform,context);
            int complete=CompleteGlyphsAndStyle(label,label.text,context);
            Assert.AreEqual(complete,label.cachedTextGenerator.characterCountVisible,context+" actual UGUI generator must retain every generated character");
            var mesh=label.canvasRenderer.GetMesh();Assert.IsNotNull(mesh,context+" actual CanvasRenderer mesh");Assert.Greater(mesh.vertexCount,0,context);
            Assert.Greater(mesh.bounds.size.x,.001f,context);Assert.Greater(mesh.bounds.size.y,.001f,context);
            float fontPixel=FontPixel(label,context);var generated=label.cachedTextGenerator.verts;
            Assert.IsNotEmpty(generated,context+" actual native glyph vertices");
            // Match the bundled UGUI Text.OnPopulateMesh rounding exactly rather
            // than adding another arbitrary tolerance for pixel alignment.
            Vector2 first=(Vector2)generated[0].position*fontPixel;
            Vector2 rounding=label.PixelAdjustPoint(first)-first;
            foreach(var p in mesh.vertices)
            {
                GlyphBounds(label,p-(Vector3)rounding,fontPixel,context+" rendered text before exact pixel alignment");
                InsideParent(label.rectTransform,p,context+" actual rendered glyph");
            }
        }
        IEnumerator Screenshot(string name)
        {
            if(string.IsNullOrEmpty(evidence))yield break;
            // Ready lets this unchanged UI complete multiple native rendered frames;
            // capture the stable backbuffer without a batch-mode EndOfFrame wait.
            yield return Ready();var texture=ScreenCapture.CaptureScreenshotAsTexture();Assert.IsNotNull(texture,name+" actual screen capture");
            try
            {
                Assert.AreEqual(Screen.width,texture.width);Assert.AreEqual(Screen.height,texture.height);
                var bytes=texture.EncodeToPNG();Assert.Greater(bytes.Length,256,name+" readable PNG bytes");
                string path=Path.Combine(evidence,name+".png");File.WriteAllBytes(path,bytes);Assert.Greater(new FileInfo(path).Length,256);receipt.screenshots.Add(path);
            }
            finally{UnityEngine.Object.Destroy(texture);}
        }
        IEnumerator OpenLessons()
        {
            Assert.IsTrue(game.InMenu);var button=ButtonNamed("GUIDED LESSONS");Clickable(button);
            yield return Click(ButtonCenter(button));Assert.IsTrue(game.Hud.ModalVisible);Assert.IsNotNull(ButtonPrefix("NEXT 1/"));
        }
        IEnumerator RoutePage(GuidedPlayDefinition d)
        {
            yield return OpenLessons();int guard=0;
            while(!Buttons().Any(b=>b.name.StartsWith(d.Title+"\n",StringComparison.Ordinal)))
            {
                var next=ButtonPrefix("NEXT ");Assert.IsTrue(next.interactable,"Actual paging must expose "+d.Title);Assert.Less(guard++,8);
                yield return Click(ButtonCenter(next));
            }
            var route=RouteButton(d);Clickable(route);ActualChineseText(route.GetComponentInChildren<Text>(),d.Id+" actual menu tile");
        }
        void AssertNormalOpening(GuidedPlayDefinition d,RouteRow row)
        {
            var world=game.World;Assert.IsNotNull(game.Guided);Assert.AreEqual(d.Id,game.Guided.Definition.Id);Assert.IsFalse(game.InMenu);Assert.IsFalse(game.Autoplay);
            Assert.AreEqual(d.PlayerCivilization,game.PlayerCivilization);Assert.AreEqual(d.PlayerCivilization,world.Players[0].civilization);Assert.AreEqual(MapTopology.Winding,world.MapId);
            Assert.AreEqual(0,world.Time,"UI startup must preserve the exact unadvanced opening");Assert.AreEqual(1,world.Players[0].age);
            Assert.AreEqual(0,game.Simulation.CaptureState().controllers.Length);Assert.AreEqual(0,game.Guided.CaptureState().commands.Length);Assert.IsFalse(world.HasScenario);
            var normal=new World(game.Rules,d.Seed,d.PlayerCivilization,"france",5,d.MapId);
            for(int p=0;p<world.Players.Length;p++)Assert.AreEqual(JsonUtility.ToJson(normal.Players[p]),JsonUtility.ToJson(world.Players[p]),"Normal opening economy/player "+p);
            Assert.AreEqual(normal.Entities.Count,world.Entities.Count);
            for(int i=0;i<normal.Entities.Count;i++)Assert.AreEqual(JsonUtility.ToJson(normal.Entities[i]),JsonUtility.ToJson(world.Entities[i]),"Normal original opening entity "+i);
            Assert.AreEqual(6,world.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker));Assert.IsEmpty(world.Players[0].technologies);
            Assert.IsFalse(world.Events.Any(e=>e.kind=="Payment"||e.kind=="UnitTrained"||e.kind=="AutomaticWorkerProduced"||e.kind=="ManorWorkerProduced"||e.kind=="GuidedScenarioGrant"||e.kind=="GuidedScenarioEra"));
            row.id=d.Id;row.civilization=d.PlayerCivilization;row.age=world.Players[0].age;row.workers=6;row.entities=world.Entities.Count;row.controllers=0;row.commands=0;row.worldTime=world.Time;row.resources=world.Players[0].resources;row.initialInstruction=game.Guided.Stage.Instruction;row.exactNormalOpening=true;
        }
        static Vector2 CameraClamp(Vector2 p)=>new Vector2(Mathf.Clamp(p.x,7,121),Mathf.Clamp(p.y,7,121));
        [UnityTest] public IEnumerator ActualPagingKeepsLegacyLessonsAndFindsAllFourCountryRouteTiles()
        {
            yield return OpenLessons();var lessons=GuidedPlayCatalog.Definitions.Where(d=>d.Kind==GuidedPlayKind.Lesson).ToArray();int pages=Mathf.CeilToInt(lessons.Length/4f);var found=new List<string>();
            Assert.AreEqual(11,lessons.Length);Assert.AreEqual(3,pages);
            for(int page=0;page<pages;page++)
            {
                Assert.IsNotNull(ButtonNamed("NEXT "+(page+1)+"/"+pages));var expected=lessons.Skip(page*4).Take(4).ToArray();
                foreach(var d in expected){var tile=RouteButton(d);Clickable(tile);ActualChineseText(tile.GetComponentInChildren<Text>(),d.Id+" actual paged tile");found.Add(d.Id);}
                var tiles=expected.Select(RouteButton).ToArray();for(int i=0;i<tiles.Length;i++)for(int j=i+1;j<tiles.Length;j++)Assert.IsFalse(Bounds((RectTransform)tiles[i].transform).Overlaps(Bounds((RectTransform)tiles[j].transform)),"Actual menu hitboxes must not overlap");
                receipt.pages.Add("page "+(page+1)+"/"+pages+": "+string.Join(",",expected.Select(d=>d.Id)));yield return Screenshot("civilization084-library-page-"+(page+1));
                if(page+1<pages)yield return Click(ButtonCenter(ButtonPrefix("NEXT ")));else Assert.IsFalse(ButtonPrefix("NEXT ").interactable);
            }
            CollectionAssert.AreEqual(lessons.Select(d=>d.Id),found);foreach(string id in Routes)Assert.AreEqual(1,found.Count(x=>x==id));
            for(int page=pages-1;page>0;page--){yield return Click(ButtonCenter(ButtonNamed("PREVIOUS")));Assert.IsNotNull(ButtonNamed("NEXT "+page+"/"+pages));}
            Assert.IsFalse(ButtonNamed("PREVIOUS").interactable);yield return Click(ButtonCenter(ButtonNamed("MENU")));Assert.IsTrue(game.InMenu);Assert.IsFalse(game.Hud.ModalVisible);
        }
        [UnityTest] public IEnumerator FourCountriesStartThroughActualMenuAndKeepNormalOpeningChineseBriefingAndObjectiveButtons()
        {
            foreach(string id in Routes)
            {
                var d=GuidedPlayCatalog.Find(id);yield return RoutePage(d);var tile=RouteButton(d);
                yield return Click(ButtonCenter(tile));yield return Ready();var row=new RouteRow();receipt.starts.Add(row);AssertNormalOpening(d,row);
                yield return new WaitForSecondsRealtime(.25f);yield return Ready();
                var guide=Texts().Single(t=>t.transform.parent.name=="Guided objective");StringAssert.Contains(d.Title,guide.text);StringAssert.Contains(game.Guided.Stage.Instruction,guide.text);ActualChineseText(guide,id+" current actual guide");
                foreach(var stage in d.Stages)CompleteGlyphsAndStyle(guide,d.Title+" · "+GuidedPlayStatus.Running+"\n"+stage.Title+": "+stage.Instruction,id+" authored instruction style "+stage.Id);
                yield return Screenshot(id+"-normal-age1-ui");
                var objective=CameraClamp(game.Guided.ObjectivePoint);game.Controls.Focus(new Vector2(111,111));Assert.Greater(Vector2.Distance(game.Controls.CameraFocus,objective),10);
                var find=ButtonNamed("FIND OBJECTIVE");Clickable(find);yield return Click(ButtonCenter(find));Assert.Less(Vector2.Distance(objective,game.Controls.CameraFocus),.001f);row.findButtonClicked=true;
                var briefing=ButtonNamed("MISSION BRIEFING");Clickable(briefing);yield return Click(ButtonCenter(briefing));Assert.IsTrue(game.Hud.ModalVisible);
                var title=Texts().Single(t=>t.text==d.Title);ActualChineseText(title,id+" actual briefing title");
                var setup=Texts().Single(t=>t.text==d.StartingSetupDescription);ActualChineseText(setup,id+" actual opening briefing");
                var checklist=Texts().Single(t=>t.text.Contains(d.Stages[0].Title)&&t.text.Contains(d.Stages[d.Stages.Count-1].Title));ActualChineseText(checklist,id+" actual full checklist");
                foreach(var stage in d.Stages)StringAssert.Contains(stage.Title,checklist.text);
                yield return Screenshot(id+"-actual-briefing-ui");
                game.Controls.Focus(new Vector2(111,111));var locate=ButtonNamed("LOCATE");Clickable(locate);yield return Click(ButtonCenter(locate));Assert.IsFalse(game.Hud.ModalVisible);Assert.Less(Vector2.Distance(objective,game.Controls.CameraFocus),.001f);row.locateButtonClicked=true;
                Assert.AreEqual(0,game.World.Time);Assert.AreEqual(0,game.Guided.CaptureState().stageIndex);Assert.AreEqual(0,game.Guided.CaptureState().commands.Length,"Viewing/locating UI cannot pretend to finish a paid goal");
                yield return Press(Key.Escape);Assert.IsTrue(game.IsPaused);yield return Click(ButtonCenter(ButtonNamed("RETURN TO MENU")));Assert.IsTrue(game.InMenu);Assert.IsNull(game.Guided);yield return Ready();
            }
            Assert.AreEqual(4,receipt.starts.Count);Assert.IsTrue(receipt.starts.All(r=>r.exactNormalOpening&&r.findButtonClicked&&r.locateButtonClicked));
        }
    }
}
