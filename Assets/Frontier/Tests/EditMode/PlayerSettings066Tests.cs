using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frontier.Tests
{
    public sealed class PlayerSettings066Tests
    {
        private string directory,path;
        [SetUp] public void SetUp(){directory=Path.Combine(Path.GetTempPath(),"Frontier-preferences-"+Guid.NewGuid().ToString("N"));path=Path.Combine(directory,"player-settings.json");Directory.CreateDirectory(directory);}
        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        private void Raw(string json)=>File.WriteAllText(path,json,new UTF8Encoding(false,true));
        private static string Envelope(PlayerSettings settings,int version=1)=>"{\"schemaVersion\":"+version+",\"settings\":"+JsonUtility.ToJson(settings)+"}";

        [Test] public void EveryPreferenceAndBindingSurvivesAColdServiceWithoutAliasingDrafts()
        {
            var service=PlayerSettingsService.Load(directory);Assert.IsTrue(service.Persistent);Assert.AreEqual(path,service.Path);Assert.IsFalse(File.Exists(path));
            var draft=service.Current;draft.masterVolume=.7f;draft.sfxVolume=.4f;draft.musicVolume=.2f;draft.environmentVolume=.6f;draft.uiScale=1.3f;
            draft.muted=true;draft.musicEnabled=false;draft.environmentEnabled=false;draft.friendEnemyPatterns=true;draft.highContrastMinimap=true;draft.subtitles=false;draft.eventHints=false;
            draft.hotkeys.Single(b=>b.action==PlayerAction.AttackMove).key=Key.Q;
            int changes=0;service.Changed+=()=>changes++;Assert.IsTrue(service.TryUpdate(draft,out var error),error);Assert.AreEqual(1,changes);Assert.AreEqual(1,service.Revision);
            string frozen=JsonUtility.ToJson(service.Current);draft.masterVolume=0;draft.hotkeys[0].key=Key.Z;
            var copy=service.Current;copy.musicVolume=1;copy.hotkeys[0].key=Key.C;Assert.AreEqual(frozen,JsonUtility.ToJson(service.Current));
            var cold=PlayerSettingsService.Load(directory);Assert.AreEqual("",cold.LoadError);Assert.AreEqual(frozen,JsonUtility.ToJson(cold.Current));Assert.AreEqual(0,cold.Revision);
            Assert.AreEqual(PlayerAction.AttackMove,cold.Bindings.Resolve(Key.Q));Assert.AreEqual(PlayerAction.None,cold.Bindings.Resolve(Key.A));Assert.AreEqual("Q",cold.BindingLabel(PlayerAction.AttackMove));
            Assert.IsTrue(service.TryUpdate(service.Current,out error),error);Assert.AreEqual(1,changes,"A no-op Save must not invalidate live input");Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
        }
        [Test] public void ReservedKeysConflictsAndInvalidRangesLeaveBytesAndRevisionUntouched()
        {
            var service=PlayerSettingsService.Load(directory);Assert.IsTrue(service.TryRebind(PlayerAction.AttackMove,Key.Q,out _));byte[] original=File.ReadAllBytes(path);int revision=service.Revision;
            foreach(var key in new[]{Key.Escape,Key.F8,Key.F9,Key.F10,Key.Digit0,Key.Digit1,Key.Digit9,Key.LeftShift,Key.RightCtrl,Key.Tab,Key.Enter,Key.Period,Key.None,(Key)9999})
                Assert.IsFalse(service.TryRebind(PlayerAction.AttackMove,key,out var reason),"Reserved key "+key+": "+reason);
            Assert.IsFalse(service.TryRebind(PlayerAction.AttackMove,Key.S,out _));Assert.IsFalse(service.TryRebind(PlayerAction.None,Key.Z,out _));
            foreach(float bad in new[]{-.01f,1.01f,float.NaN,float.PositiveInfinity}){var draft=service.Current;draft.sfxVolume=bad;Assert.IsFalse(service.TryUpdate(draft,out _));}
            foreach(float bad in new[]{.849f,1.301f,float.NaN}){var draft=service.Current;draft.uiScale=bad;Assert.IsFalse(service.TryUpdate(draft,out _));}
            var duplicate=service.Current;duplicate.hotkeys[1].action=duplicate.hotkeys[0].action;Assert.IsFalse(service.TryUpdate(duplicate,out _));
            CollectionAssert.AreEqual(original,File.ReadAllBytes(path));Assert.AreEqual(revision,service.Revision);
        }
        [Test] public void DefaultIdleWorkerAliasIsRetiredWithItsPrimaryKeyWhenRebound()
        {
            var service=PlayerSettingsService.Load(directory);Assert.AreEqual(PlayerAction.IdleWorker,service.Bindings.Resolve(Key.V));Assert.AreEqual(PlayerAction.IdleWorker,service.Bindings.Resolve(Key.Period));Assert.AreEqual("V / .",service.BindingLabel(PlayerAction.IdleWorker));
            Assert.IsTrue(service.TryRebind(PlayerAction.IdleWorker,Key.Y,out var error),error);Assert.AreEqual(PlayerAction.None,service.Bindings.Resolve(Key.V));Assert.AreEqual(PlayerAction.None,service.Bindings.Resolve(Key.Period));Assert.AreEqual(PlayerAction.IdleWorker,service.Bindings.Resolve(Key.Y));
        }
        [Test] public void InvalidAndFutureFilesFallBackWithoutChangingOriginalThenExplicitSavePreservesThem()
        {
            string valid=Envelope(PlayerSettings.Defaults());
            var invalid=new[]{"", "{", Envelope(PlayerSettings.Defaults(),2),valid.Replace("\"schemaVersion\":1","\"schemaVersion\":1,\"schemaVersion\":1"),valid.Replace("\"masterVolume\":1.0","\"masterVolume\":true"),valid.Replace("\"subtitles\":true","\"subtitles\":\"true\""),valid+"garbage"};
            // Unity's float formatting is not part of the rejection fixture.
            invalid[4]="{\"schemaVersion\":1,\"settings\":{\"masterVolume\":true}}";
            foreach(string raw in invalid)
            {
                Raw(raw);byte[] original=File.ReadAllBytes(path);var service=PlayerSettingsService.Load(directory);
                Assert.IsNotEmpty(service.LoadError,"Fixture must actually be invalid");Assert.AreEqual(JsonUtility.ToJson(PlayerSettings.Defaults()),JsonUtility.ToJson(service.Current));CollectionAssert.AreEqual(original,File.ReadAllBytes(path));
                var repaired=service.Current;repaired.uiScale=.85f;Assert.IsTrue(service.TryUpdate(repaired,out var error),error);Assert.IsNotEmpty(service.PreservedUnreadablePath);CollectionAssert.AreEqual(original,File.ReadAllBytes(service.PreservedUnreadablePath));
                Assert.AreEqual("",service.LoadError);Assert.AreEqual(.85f,PlayerSettingsService.Load(directory).Current.uiScale);Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
            }
        }
        [Test] public void OversizedInvalidUtf8AndDuplicateNestedFieldsAreRejectedWithoutRewrite()
        {
            var bytes=new byte[]{0x7b,0xff,0x7d};File.WriteAllBytes(path,bytes);Assert.IsNotEmpty(PlayerSettingsService.Load(directory).LoadError);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            using(var file=new FileStream(path,FileMode.Create))file.SetLength(PlayerSettingsService.MaxFileBytes+1);
            Assert.IsNotEmpty(PlayerSettingsService.Load(directory).LoadError);Assert.AreEqual(PlayerSettingsService.MaxFileBytes+1,new FileInfo(path).Length);
            Raw(Envelope(PlayerSettings.Defaults()).Replace("\"muted\":false","\"muted\":false,\"muted\":true"));string original=File.ReadAllText(path);Assert.IsNotEmpty(PlayerSettingsService.Load(directory).LoadError);Assert.AreEqual(original,File.ReadAllText(path));
        }
        [Test] public void FailedWriteDoesNotChangeLivePreferencesOrDestroyTheBlockingFile()
        {
            string blocker=Path.Combine(directory,"blocked");File.WriteAllText(blocker,"existing data");var service=PlayerSettingsService.Load(blocker);var draft=service.Current;draft.masterVolume=.5f;
            int changes=0;service.Changed+=()=>changes++;Assert.IsFalse(service.TryUpdate(draft,out var error));Assert.IsNotEmpty(error);Assert.AreEqual(1,service.Current.masterVolume);Assert.AreEqual(0,service.Revision);Assert.AreEqual(0,changes);Assert.AreEqual("existing data",File.ReadAllText(blocker));
        }
        [Test] public void DefaultEditorServiceDoesNotReadOrWritePlayerPreferences()
        {var first=PlayerSettingsService.Load();Assert.IsFalse(first.Persistent);Assert.AreEqual("",first.Path);var draft=first.Current;draft.uiScale=1.2f;Assert.IsTrue(first.TryUpdate(draft,out _));Assert.AreEqual(1,PlayerSettingsService.Load().Current.uiScale);}
        [Test] public void CaptionsRemainUsefulWhileMutedAndRespectEnemyVisibilityAndIndependentHintFlags()
        {
            var service=PlayerSettingsService.Load(directory);var draft=service.Current;draft.muted=true;Assert.IsTrue(service.TryUpdate(draft,out _));
            var world=new World(Catalog.Load(),314159,"france","river",5,"frontier");var captions=service.Captions;
            captions.OnEvent(world,0,new SimEvent{kind="AgeAdvanced",player=1,position=new Vector2(120,120),message="Private enemy age"});Assert.AreEqual("",captions.Text);
            captions.OnEvent(world,0,new SimEvent{kind="AgeAdvanced",player=0,position=new Vector2(120,120)});StringAssert.Contains("Age advanced",captions.Text);
            captions.Clear();var fire=new SimEvent{kind="Fire",player=1,position=new Vector2(120,120),time=world.Time,visibilityMask=0};captions.OnEvent(world,0,fire);Assert.AreEqual("",captions.Text);
            world.Fog.Reveal(0,fire.position,8);fire.visibilityMask=1;captions.OnEvent(world,0,fire);StringAssert.Contains("Gunfire",captions.Text);
            captions.Hint("Select <b>settlers</b>");StringAssert.Contains("(b)",captions.Text);StringAssert.DoesNotContain("<b>",captions.Text);
            draft=service.Current;draft.eventHints=false;Assert.IsTrue(service.TryUpdate(draft,out _));StringAssert.DoesNotContain("Hint",captions.Text);StringAssert.Contains("Gunfire",captions.Text);
            captions.Hint("This must not appear");StringAssert.DoesNotContain("appear",captions.Text);draft=service.Current;draft.subtitles=false;Assert.IsTrue(service.TryUpdate(draft,out _));Assert.AreEqual("",captions.Text);
            draft=service.Current;draft.eventHints=true;Assert.IsTrue(service.TryUpdate(draft,out _));for(int i=0;i<5;i++)captions.Hint("Step "+i);Assert.AreEqual(3,captions.Text.Split('\n').Length);StringAssert.DoesNotContain("Step 0",captions.Text);captions.Tick(Time.unscaledTime+5);Assert.AreEqual("",captions.Text);
        }
    }
}
