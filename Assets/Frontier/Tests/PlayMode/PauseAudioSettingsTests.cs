using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    public sealed class PauseAudioSettingsTests : NativeInputFixture
    {
        private void Label(string prefix,string expected)
        {Assert.AreEqual(expected,FindButton(prefix).GetComponentInChildren<Text>().text);}
        [UnityTest] public IEnumerator NativeCategoryClicksUpdateLabelsWithoutChangingMasterMuteOrSimulation()
        {
            game.TogglePause();yield return Ready();float time=game.World.Time;
            Assert.IsTrue(game.EnvironmentSoundEnabled);Assert.IsTrue(game.MusicEnabled);Assert.IsFalse(game.Muted);
            yield return Click(ButtonCenter(FindButton("ENVIRONMENT:")));Assert.IsFalse(game.EnvironmentSoundEnabled);Assert.IsTrue(game.MusicEnabled);Assert.IsFalse(game.Muted);Label("ENVIRONMENT:","ENVIRONMENT: OFF");Label("MUSIC:","MUSIC: ON");
            yield return Click(ButtonCenter(FindButton("MUSIC:")));Assert.IsFalse(game.MusicEnabled);Label("MUSIC:","MUSIC: OFF");
            yield return Click(ButtonCenter(FindButton("SOUND:")));Assert.IsTrue(game.Muted);Label("SOUND:","SOUND: OFF");
            yield return Click(ButtonCenter(FindButton("MUSIC:")));Assert.IsTrue(game.MusicEnabled);Assert.IsTrue(game.Muted);Assert.IsFalse(game.EnvironmentSoundEnabled);Label("MUSIC:","MUSIC: ON");
            yield return Click(ButtonCenter(FindButton("SOUND:")));Assert.IsFalse(game.Muted);Assert.IsTrue(game.MusicEnabled);Assert.IsFalse(game.EnvironmentSoundEnabled);Label("ENVIRONMENT:","ENVIRONMENT: OFF");
            yield return Click(ButtonCenter(FindButton("ENVIRONMENT:")));Assert.IsTrue(game.EnvironmentSoundEnabled);Label("ENVIRONMENT:","ENVIRONMENT: ON");
            Assert.IsTrue(game.IsPaused);Assert.IsTrue(game.Hud.ModalVisible);game.World.Tick(10);Assert.AreEqual(time,game.World.Time);
            yield return Click(ButtonCenter(FindButton("RESUME")));Assert.IsFalse(game.IsPaused);Assert.IsFalse(game.Hud.ModalVisible);
            Assert.IsTrue(game.MusicEnabled);Assert.IsTrue(game.EnvironmentSoundEnabled);Assert.IsFalse(game.Muted);
        }
        [UnityTest] public IEnumerator AllPauseControlsFitTheirPanelAndStayDisjointAfterSettingRebuild()
        {
            game.TogglePause();yield return Ready();AssertLayout();
            yield return Click(ButtonCenter(FindButton("ENVIRONMENT:")));AssertLayout();
            yield return Click(ButtonCenter(FindButton("MUSIC:")));AssertLayout();
        }
        private void AssertLayout()
        {
            var panel=(RectTransform)FindButton("RESUME").transform.parent;
            var buttons=panel.GetComponentsInChildren<Button>().Where(b=>b.transform.parent==panel).ToArray();Assert.AreEqual(8,buttons.Length);
            Rect Bounds(RectTransform rect)
            {
                var corners=new Vector3[4];rect.GetWorldCorners(corners);var a=panel.InverseTransformPoint(corners[0]);var b=panel.InverseTransformPoint(corners[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
            }
            foreach(var button in buttons)
            {var rect=Bounds((RectTransform)button.transform);Assert.IsTrue(panel.rect.Contains(rect.min));Assert.IsTrue(panel.rect.Contains(rect.max));}
            for(int i=0;i<buttons.Length;i++)for(int j=i+1;j<buttons.Length;j++)Assert.IsFalse(Bounds((RectTransform)buttons[i].transform).Overlaps(Bounds((RectTransform)buttons[j].transform)),buttons[i].name+" overlaps "+buttons[j].name);
        }
    }
}
