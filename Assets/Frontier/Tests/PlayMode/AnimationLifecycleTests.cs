using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Frontier.Tests
{
    public sealed class AnimationLifecycleTests
    {
        [UnityTest] public IEnumerator ObservedShotDeathPauseAndPoolResetUseEventTime()
        {
            var root=new GameObject("Animation lifecycle test");
            try
            {
                var game=root.AddComponent<GameController>();yield return null;game.StartMatch();var world=game.World;world.Paused=true;world.Time=10;
                var entity=world.Spawn("musketeer",0,new Vector2(31,30));world.Fog.Reveal(0,entity.position,8);yield return null;
                var model=game.View.GetComponentsInChildren<UnitRig>().Single(r=>r.transform.parent.name=="Entity "+entity.id);
                entity.facing=Vector2.up;world.Emit("Fire",0,entity.id,"Ranged");entity.facing=Vector2.right;yield return null;
                Assert.Greater(Vector3.Dot(model.transform.forward,Vector3.forward),.999f,"Shot keeps the observed firing direction after a new facing order");
                var hand=model.RightHand.position;var modelAt=model.transform.position;yield return null;yield return null;
                Assert.Less(Vector3.Distance(hand,model.RightHand.position),.00001f);Assert.AreEqual(modelAt,model.transform.position);
                world.Remove(entity,1);yield return null;Assert.IsTrue(model.gameObject.activeInHierarchy);
                world.Time=10.325f;yield return null;Assert.Greater(Quaternion.Angle(Quaternion.identity,model.transform.rotation),30);
                world.Time=10.8f;yield return null;Assert.IsFalse(model.gameObject.activeInHierarchy);
                var replacement=world.Spawn("musketeer",0,new Vector2(31,30));yield return null;
                var reused=game.View.GetComponentsInChildren<UnitRig>().Single(r=>r.transform.parent.name=="Entity "+replacement.id);
                Assert.AreSame(model,reused);Assert.Less(Quaternion.Angle(Quaternion.identity,reused.transform.rotation),.01f,"Pooled corpse must not tilt the replacement");
                game.ReturnToMenu();Assert.IsFalse(model.gameObject.activeInHierarchy);game.StartMatch();world=game.World;world.Paused=true;yield return null;
                Assert.IsFalse(game.View.GetComponentsInChildren<UnitRig>().Any(r=>r.transform.parent.name=="Entity "+replacement.id));
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
    }
}
