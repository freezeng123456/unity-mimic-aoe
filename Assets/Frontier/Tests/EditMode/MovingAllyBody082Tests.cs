using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Isolated movement mechanics fixtures. These deliberately clear an arena;
    // they do not replace the unchanged-resource normal paid-state V3 diagnosis.
    public sealed class MovingAllyBody082Tests
    {
        static World Arena()
        {
            var w=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.Frontier);
            foreach(var e in w.Entities.Where(e=>e.def.id!="town").ToArray())w.Remove(e);
            foreach(var obstacle in w.TerrainObstacles)w.Navigation.SetBlocked(new Vector2(obstacle.x,obstacle.y),obstacle.z,false);
            return w;
        }
        static void Move(World w,EntityState e,Vector2 point)
        {
            var result=w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{e.id},order=OrderKind.Move,point=point});
            Assert.IsTrue(result.accepted,result.reason);Assert.AreEqual(point,e.orders.Peek().point,"This clear arena must preserve the public requested point");
        }
        static float SegmentDistance(Vector2 a,Vector2 b,Vector2 point)
        {
            var d=b-a;float t=d.sqrMagnitude>1e-12f?Mathf.Clamp01(Vector2.Dot(point-a,d)/d.sqrMagnitude):0;
            return Vector2.Distance(a+d*t,point);
        }
        static void Step(World w,EntityState[] actors,float dt,bool allowInitialContact=false)
        {
            int lossesBefore=w.Players[0].losses;
            var before=actors.ToDictionary(e=>e.id,e=>e.position);var order=w.Entities.Select((e,i)=>new{id=e.id,index=i}).ToDictionary(e=>e.id,e=>e.index);
            w.Tick(dt);
            foreach(var e in actors)
            {
                Assert.IsTrue(e.Alive&&e.garrisonedIn==0);
                Assert.IsTrue(w.Navigation.IsWalkable(e.position,e.def.radius));
                Assert.IsTrue(w.Navigation.ClearLine(before[e.id],e.position,e.def.radius));
                Assert.LessOrEqual(Vector2.Distance(before[e.id],e.position),w.Speed(e)*dt+.0001f,"No collision repair may snap or accelerate an actor");
                foreach(var other in actors.Where(x=>x.id!=e.id))
                {
                    // World resolves actors sequentially. Earlier actors have
                    // already committed their current point at this actual step.
                    Vector2 disc=order[other.id]<order[e.id]?other.position:before[other.id];
                    float radius=e.def.radius+other.def.radius,start=Vector2.Distance(before[e.id],disc);
                    if(allowInitialContact&&start<radius)
                    {
                        Assert.GreaterOrEqual(Vector2.Distance(e.position,disc),start-.00001f,"An existing contact may not deepen");
                        Assert.LessOrEqual(Vector2.Dot(e.position-before[e.id],disc-before[e.id]),.000001f,"An existing contact must separate monotonically");
                    }
                    else Assert.GreaterOrEqual(SegmentDistance(before[e.id],e.position,disc),radius-.00001f,"The actual submitted step must never sweep through an allied body");
                }
            }
            Assert.AreEqual(lossesBefore,w.Players[0].losses,"No additional loss after explicit arena preparation");
            if(!allowInitialContact)Assert.GreaterOrEqual(Vector2.Distance(actors[0].position,actors[1].position),actors[0].def.radius+actors[1].def.radius-.00001f);
        }
        static void Finish(World w,EntityState[] actors,Dictionary<int,Vector2> goals,int ticks=400)
        {
            for(int i=0;i<ticks&&actors.Any(e=>e.orders.Count>0);i++)Step(w,actors,.1f);
            foreach(var e in actors)
            {Assert.IsEmpty(e.orders,"Head-on or overtaking traffic must complete: "+e.id+" "+e.position+" "+e.status);Assert.Less(Vector2.Distance(e.position,goals[e.id]),.051f,"Actual exact destination arrival");}
        }
        [Test] public void OpposingWorkerAndCannonPassAndFinishWithoutSweepingThroughEachOther()
        {
            var w=Arena();var cannon=w.Spawn("cannon",0,new Vector2(44,64));var worker=w.Spawn("villager",0,new Vector2(62,64));
            var actors=new[]{cannon,worker};var goals=new Dictionary<int,Vector2>{{cannon.id,new Vector2(62,64)},{worker.id,new Vector2(44,64)}};
            foreach(var e in actors)Move(w,e,goals[e.id]);Finish(w,actors,goals);
        }
        [TestCase(false)] [TestCase(true)]
        public void FasterWorkerOvertakesMovingCannonWithoutPenetrationInEitherUpdateOrder(bool workerCreatedFirst)
        {
            var w=Arena();EntityState cannon,worker;
            if(workerCreatedFirst){worker=w.Spawn("villager",0,new Vector2(40,64.55f));cannon=w.Spawn("cannon",0,new Vector2(44,64));}
            else{cannon=w.Spawn("cannon",0,new Vector2(44,64));worker=w.Spawn("villager",0,new Vector2(40,64.55f));}
            var actors=new[]{cannon,worker};var goals=new Dictionary<int,Vector2>{{cannon.id,new Vector2(76,64)},{worker.id,new Vector2(82,64.55f)}};
            foreach(var e in actors)Move(w,e,goals[e.id]);Finish(w,actors,goals);
            Assert.Greater(worker.position.x,cannon.position.x,"The faster worker actually passed, rather than both waiting forever");
        }
        [Test] public void PreexistingMovingContactEscapesMonotonicallyIncludingSubCentimetreSteps()
        {
            var w=Arena();var left=w.Spawn("hussar",0,new Vector2(50,64));var right=w.Spawn("hussar",0,new Vector2(50.9f,64));
            var actors=new[]{left,right};var goals=new Dictionary<int,Vector2>{{left.id,new Vector2(42,64)},{right.id,new Vector2(59,64)}};
            float radius=left.def.radius+right.def.radius,start=Vector2.Distance(left.position,right.position);Assert.Less(start,radius,"Explicit existing-contact fixture");
            foreach(var e in actors)Move(w,e,goals[e.id]);Step(w,actors,.001f,true);
            Assert.GreaterOrEqual(Vector2.Distance(left.position,right.position),start-.00001f,"Sub-centimetre motion must not deepen the initial contact");
            for(int i=0;i<100&&Vector2.Distance(left.position,right.position)<radius;i++)Step(w,actors,.1f,true);
            Assert.GreaterOrEqual(Vector2.Distance(left.position,right.position),radius,"Existing contact must be able to separate");Finish(w,actors,goals);
        }
    }
}
