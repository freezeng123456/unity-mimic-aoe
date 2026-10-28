using System;
using System.Collections;
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
    // Production InputSystem pointer gestures, not direct HUD callbacks or
    // StartGuided. This finite UI check does not claim paid-route completion.
    public sealed class ClassicCivilizationMenu084Tests:NativeInputFixture
    {
        string storage;
        [UnitySetUp] public IEnumerator IsolateClassicMenuStorage()
        {
            Assert.IsFalse(game.Settings.Persistent);Assert.IsTrue(game.Progress.MemoryOnly);
            storage=Path.Combine(Path.GetTempPath(),"Frontier-classic-menu084-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(storage);
            foreach(string name in new[]{"saveDirectoryOverride","reportDirectoryOverride","replayDirectoryOverride"})
            {
                var field=typeof(GameController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(field,name);
                field.SetValue(game,Path.Combine(storage,name));
            }
            game.ReturnToMenu();yield return Ready();Assert.IsTrue(game.InMenu);Assert.IsNull(game.World);
        }
        [UnityTearDown] public IEnumerator RemoveClassicMenuStorage()
        {
            if(game!=null)game.enabled=false;
            if(!string.IsNullOrEmpty(storage)&&Directory.Exists(storage))Directory.Delete(storage,true);
            yield return null;
        }
        Button[] Buttons()=>session.GetComponentsInChildren<Button>().Where(b=>b.isActiveAndEnabled).ToArray();
        Button Named(string text)=>Buttons().Single(b=>b.name==text);
        Button Country(string prefix)=>Buttons().Single(b=>b.name.StartsWith(prefix,StringComparison.Ordinal));
        static Rect ScreenBounds(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var pixels=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            return Rect.MinMaxRect(pixels.Min(p=>p.x),pixels.Min(p=>p.y),pixels.Max(p=>p.x),pixels.Max(p=>p.y));
        }
        static string RectDiagnostic(RectTransform rect)=>rect.name+" screen="+ScreenBounds(rect).ToString("F3")+
            " local="+rect.rect.ToString("F3")+" anchored="+rect.anchoredPosition.ToString("F3")+
            " size="+rect.sizeDelta.ToString("F3")+" localScale="+rect.localScale.ToString("F3")+
            " worldScale="+rect.lossyScale.ToString("F3")+" parent="+rect.parent?.name;
        string MenuDiagnostics(Button[] buttons,RectTransform configuration)
        {
            var canvas=session.GetComponentInChildren<Canvas>();
            return "Actual menu bounds: screen="+Screen.width+"x"+Screen.height+" uiScale="+game.Options.uiScale+
                " canvasScale="+(canvas==null?"none":canvas.scaleFactor.ToString("F3"))+"\n"+
                RectDiagnostic(configuration)+"\n"+string.Join("\n",buttons.Select(b=>RectDiagnostic((RectTransform)b.transform)));
        }
        static void InParent(RectTransform child,Vector3 point,string context)
        {
            var parent=child.parent as RectTransform;Assert.IsNotNull(parent,context);
            var p=parent.InverseTransformPoint(child.TransformPoint(point));var rect=parent.rect;
            Assert.That(p.x,Is.InRange(rect.xMin-.001f,rect.xMax+.001f),context+" actual parent x");
            Assert.That(p.y,Is.InRange(rect.yMin-.001f,rect.yMax+.001f),context+" actual parent y");
        }
        static void Clickable(Button button)
        {
            Assert.IsTrue(button.isActiveAndEnabled);Assert.IsTrue(button.interactable,button.name);
            var rect=(RectTransform)button.transform;var bounds=ScreenBounds(rect);
            // Button dimensions are canvas units. The batch GameView scales a
            // normal 48-unit button to 19.2 screen pixels; keep the size check
            // in the same units as every production layout and pointer target.
            Assert.Greater(rect.rect.width,20);Assert.Greater(rect.rect.height,20);
            Assert.Greater(bounds.width,1);Assert.Greater(bounds.height,1);
            Assert.GreaterOrEqual(bounds.xMin,0);Assert.GreaterOrEqual(bounds.yMin,0);
            Assert.LessOrEqual(bounds.xMax,Screen.width);Assert.LessOrEqual(bounds.yMax,Screen.height);
            foreach(var p in new[]{rect.rect.min,new Vector2(rect.rect.xMax,rect.rect.yMin),rect.rect.max,new Vector2(rect.rect.xMin,rect.rect.yMax)})InParent(rect,p,button.name+" hitbox");
        }
        static void CompleteText(Text label)
        {
            Assert.IsTrue(label.isActiveAndEnabled);Assert.IsNotNull(label.font);
            label.font.RequestCharactersInTexture(label.text,label.fontSize,label.fontStyle);
            foreach(char c in label.text.Where(c=>c>='\u2e80').Distinct())Assert.IsTrue(label.font.HasCharacter(c),"Missing actual Chinese glyph "+c);
            Assert.Greater(label.pixelsPerUnit,0);float unit=1/label.pixelsPerUnit;
            Assert.IsFalse(float.IsNaN(unit)||float.IsInfinity(unit));
            var settings=label.GetGenerationSettings(label.rectTransform.rect.size);settings.resizeTextForBestFit=false;
            using(var full=new TextGenerator())using(var clipped=new TextGenerator())
            {
                settings.verticalOverflow=VerticalWrapMode.Overflow;Assert.IsTrue(full.Populate(label.text,settings));
                settings.verticalOverflow=VerticalWrapMode.Truncate;Assert.IsTrue(clipped.Populate(label.text,settings));
                Assert.Greater(full.characterCountVisible,0);
                Assert.AreEqual(full.characterCountVisible,clipped.characterCountVisible,label.text+" must retain every character");
                Assert.AreEqual(full.characterCountVisible,label.cachedTextGenerator.characterCountVisible,label.text+" actual cached characters");
            }
            var mesh=label.canvasRenderer.GetMesh();Assert.IsNotNull(mesh);Assert.Greater(mesh.vertexCount,0);
            var generated=label.cachedTextGenerator.verts;Assert.IsNotEmpty(generated);
            Vector2 first=(Vector2)generated[0].position*unit;Vector2 rounding=label.PixelAdjustPoint(first)-first;
            var rect=label.rectTransform.rect;float bearing=unit+.0001f;
            foreach(var vertex in mesh.vertices)
            {
                var p=vertex-(Vector3)rounding;
                Assert.That(p.x,Is.InRange(rect.xMin-bearing,rect.xMax+bearing),label.text+" glyph x");
                Assert.That(p.y,Is.InRange(rect.yMin-bearing,rect.yMax+bearing),label.text+" glyph y");
                InParent(label.rectTransform,vertex,label.text+" actual rendered glyph");
            }
        }
        Button MainEntry()
        {
            Assert.IsTrue(game.InMenu);Assert.IsFalse(game.Hud.ModalVisible);
            var buttons=Buttons();var entry=Named("CLASSIC CIVILIZATIONS");
            var configuration=session.GetComponentsInChildren<RectTransform>().Single(t=>t.gameObject.activeInHierarchy&&t.name=="Skirmish configuration");
            string diagnostics=MenuDiagnostics(buttons,configuration);TestContext.WriteLine(diagnostics);
            Clickable(entry);CompleteText(entry.GetComponentInChildren<Text>());
            var entryBounds=ScreenBounds((RectTransform)entry.transform);
            foreach(var other in buttons.Where(b=>b!=entry))Assert.IsFalse(entryBounds.Overlaps(ScreenBounds((RectTransform)other.transform)),"New entry overlaps actual menu button "+other.name+"\n"+diagnostics);
            foreach(string title in new[]{"SCENARIO WORKSHOP","MAP, SEED & OBJECTIVES","LOAD SAVED MATCH","GUIDED LESSONS","RIVER CAMPAIGN"})Assert.IsNotNull(Named(title));
            foreach(string title in new[]{"LOAD SAVED MATCH","SCENARIO WORKSHOP"}){Clickable(Named(title));CompleteText(Named(title).GetComponentInChildren<Text>());}
            Assert.IsFalse(entryBounds.Overlaps(ScreenBounds(configuration)),"New entry must not cover the existing skirmish panel\n"+diagnostics);
            return entry;
        }
        void FourReadableCards()
        {
            Assert.IsTrue(game.InMenu);Assert.IsTrue(game.Hud.ModalVisible);
            var cards=new[]{Country("英国："),Country("荷兰："),Country("法国："),Country("奥斯曼：")};
            foreach(var card in cards){Clickable(card);CompleteText(card.GetComponentInChildren<Text>());}
            for(int i=0;i<cards.Length;i++)for(int j=i+1;j<cards.Length;j++)Assert.IsFalse(ScreenBounds((RectTransform)cards[i].transform).Overlaps(ScreenBounds((RectTransform)cards[j].transform)),"Country cards cannot overlap");
            var scope=session.GetComponentsInChildren<Text>().Single(t=>t.isActiveAndEnabled&&t.text.StartsWith("四国正常开局发展与基础使用练习",StringComparison.Ordinal));CompleteText(scope);
            StringAssert.Contains("发展",Country("奥斯曼：").name);Assert.IsFalse(Country("奥斯曼：").name.Contains("步炮协同"));
            Clickable(Named("BACK / 返回主菜单"));
        }
        [UnityTest] public IEnumerator ActualClassicEntryDoesNotCoverOldMenuAndStartsAllFourNormalCountriesByPointer()
        {
            yield return Click(ButtonCenter(MainEntry()));FourReadableCards();
            yield return Click(ButtonCenter(Named("BACK / 返回主菜单")));Assert.IsFalse(game.Hud.ModalVisible);Assert.IsTrue(game.InMenu);
            var ids=new[]{ClassicCivilizationRoutes084.British,ClassicCivilizationRoutes084.Dutch,ClassicCivilizationRoutes084.French,ClassicCivilizationRoutes084.Ottoman};
            var prefixes=new[]{"英国：","荷兰：","法国：","奥斯曼："};
            for(int i=0;i<ids.Length;i++)
            {
                yield return Click(ButtonCenter(MainEntry()));FourReadableCards();
                yield return Click(ButtonCenter(Country(prefixes[i])));yield return Ready();
                var d=GuidedPlayCatalog.Find(ids[i]);Assert.IsNotNull(game.Guided);Assert.AreEqual(ids[i],game.Guided.Definition.Id);
                Assert.IsFalse(game.InMenu);Assert.IsFalse(game.Autoplay);Assert.AreEqual(d.PlayerCivilization,game.PlayerCivilization);
                var world=game.World;Assert.IsNotNull(world);Assert.AreEqual(d.PlayerCivilization,world.Players[0].civilization);Assert.AreEqual(1,world.Players[0].age);Assert.AreEqual(0,world.Time);
                Assert.AreEqual(d.MapId,world.MapId);Assert.AreEqual(d.Seed,world.Seed);Assert.IsFalse(world.HasScenario);
                Assert.AreEqual(0,game.Simulation.CaptureState().controllers.Length);Assert.AreEqual(0,game.Guided.CaptureState().commands.Length);
                var normal=new World(game.Rules,d.Seed,d.PlayerCivilization,"france",5,d.MapId);
                Assert.AreEqual(JsonUtility.ToJson(normal.Players[0]),JsonUtility.ToJson(world.Players[0]));Assert.AreEqual(normal.Entities.Count,world.Entities.Count);
                for(int n=0;n<normal.Entities.Count;n++)Assert.AreEqual(JsonUtility.ToJson(normal.Entities[n]),JsonUtility.ToJson(world.Entities[n]),"Ordinary country opening entity "+n);
                Assert.IsFalse(world.Events.Any(e=>e.kind=="Payment"||e.kind=="GuidedScenarioGrant"||e.kind=="GuidedScenarioEra"));
                TestContext.WriteLine("Actual pointer started "+ids[i]+" / "+d.PlayerCivilization+" with exact normal Age I economy and entities; no economic commands issued");
                yield return Press(Key.Escape);Assert.IsTrue(game.IsPaused);yield return Click(ButtonCenter(Named("RETURN TO MENU")));yield return Ready();
                Assert.IsTrue(game.InMenu);Assert.IsNull(game.Guided);Assert.IsNull(game.World);
            }
        }
    }
}
