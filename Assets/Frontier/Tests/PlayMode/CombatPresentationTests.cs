using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class CombatPresentationTests
    {
        [UnityTest] public IEnumerator ProjectileCannotExposeTheHiddenMiddleOfItsFlight()
        {
            var go=new GameObject("Projectile fog test");
            try
            {
                var game=go.AddComponent<GameController>();yield return null;game.StartMatch();
                var w=game.World;w.Paused=true;w.Time=5;
                Vector2 start=new Vector2(48,32),end=new Vector2(80,32),middle=(start+end)*.5f;
                w.Fog.Reveal(0,start,2);w.Fog.Reveal(0,end,2);
                Assert.AreNotEqual(VisibilityState.Visible,w.Fog.At(0,middle));
                w.Projectiles.Add(new ProjectileState{start=start,end=end,firedAt=0,hitAt=10});yield return null;
                Assert.AreEqual(0,game.View.GetComponentsInChildren<LineRenderer>(true).Count(l=>l.name=="Projectile"&&l.gameObject.activeSelf));
                w.Fog.Reveal(0,middle,3);yield return null;
                Assert.AreEqual(1,game.View.GetComponentsInChildren<LineRenderer>(true).Count(l=>l.name=="Projectile"&&l.gameObject.activeSelf));
            }
            finally{Object.Destroy(go);}
            yield return null;
        }
        [UnityTest] public IEnumerator EventTimeVisibilityPauseAndPoolSurviveMatchReset()
        {
            var go=new GameObject("Feedback test");var feedback=go.AddComponent<CombatFeedback>();
            try
            {
                var w=new World(Catalog.Load());feedback.Bind(w,0,go.transform);
                Vector2 hidden=new Vector2(100,100);w.Emit("Fire",1,0,"Ranged",1,0,hidden);
                w.Fog.Reveal(0,hidden,3);feedback.Tick();
                Assert.AreEqual(0,feedback.ActiveCount,"Revealing a location later must not replay its previously hidden shot");
                Vector2 point=new Vector2(28,28);
                for(int i=0;i<50;i++)w.Emit("Fire",0,0,"Ranged",1,0,point);
                feedback.Tick();Assert.AreEqual(CombatFeedback.Capacity,feedback.ActiveCount);
                w.Paused=true;for(int i=0;i<10;i++)w.Tick(.1f);feedback.Tick();
                Assert.AreEqual(CombatFeedback.Capacity,feedback.ActiveCount);
                w.Paused=false;for(int i=0;i<4;i++)w.Tick(.1f);feedback.Tick();
                Assert.AreEqual(0,feedback.ActiveCount);Assert.AreEqual(CombatFeedback.Capacity,feedback.PooledCount);
                w.Emit("Impact",0,0,"Projectile impact",1,0,point);feedback.Tick();Assert.AreEqual(1,feedback.ActiveCount);
                w.Fog.Tick(new System.Collections.Generic.List<EntityState>());feedback.Tick();Assert.AreEqual(0,feedback.ActiveCount,"Losing current vision must hide existing effects too");
                feedback.Bind(new World(Catalog.Load()),0,go.transform);yield return null;
                Assert.AreEqual(0,feedback.ActiveCount);Assert.AreEqual(0,feedback.PooledCount);
                Assert.AreEqual(0,go.transform.childCount,"Reset releases all effect objects");
            }
            finally{Object.Destroy(go);}
            yield return null;
        }
        [UnityTest] public IEnumerator AudioRoutesMeleeAndNoticesAndRejectsPreviouslyHiddenShots()
        {
            var go=new GameObject("Audio test");
            try
            {
                var game=go.AddComponent<GameController>();yield return null;game.StartMatch();
                var w=game.World;var audio=game.Audio;audio.ResetMatch();
                var source=w.Entities.First(e=>e.owner==0&&(e.def.tags&Tags.Explorer)!=0);
                w.Emit("Fire",0,source.id,"Melee");audio.OnEvent(w.Events.Last());
                Assert.AreEqual(EventAudio.SoundCategory.Melee,audio.LastCategory);Assert.AreEqual(1,audio.PlayedCount);
                w.Emit("Fire",1,0,"Ranged",0,0,new Vector2(100,100));var hidden=w.Events.Last();w.Fog.Reveal(0,hidden.position,3);audio.OnEvent(hidden);
                Assert.AreEqual(1,audio.PlayedCount);
                w.Emit("ShipmentArrived",0,0,"Delivery",0,0,new Vector2(120,120));audio.OnEvent(w.Events.Last());
                Assert.AreEqual(EventAudio.SoundCategory.Notice,audio.LastCategory);Assert.AreEqual(2,audio.PlayedCount,"Own strategic notices must not disappear with camera distance");
                game.ReturnToMenu();Assert.AreEqual(0,audio.PlayedCount);game.StartMatch();
                Assert.AreEqual(12,audio.SourceCount,"Restart reuses the bounded audio pool");
            }
            finally{Object.Destroy(go);}
            yield return null;
        }
    }
}
