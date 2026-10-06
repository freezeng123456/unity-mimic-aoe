using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AiGatherRoutes074Tests
    {
        sealed class Fixture
        {
            public readonly World World;public readonly EntityState Worker;
            public readonly AiWorkforce Workforce;public readonly List<EntityState> Workers;
            public Fixture(RuleCatalog rules=null)
            {
                World=new World(rules??SnapshotCodec.Clone(Catalog.Load()),7401,"coast","coast",3);
                Worker=World.Entities.First(e=>e.owner==0&&e.Worker);
                // Declared one-worker/source fixture, before any decisions/ticks.
                foreach(var e in World.Entities.Where(e=>e.def.resource!=ResourceKind.None||World.Guardian(e)||
                    e.owner==0&&(e.Worker&&e.id!=Worker.id||(e.def.tags&Tags.Explorer)!=0)).ToArray())World.Remove(e);
                Workers=new List<EntityState>{Worker};Workforce=new AiWorkforce(World,0);
            }
            public EntityState Tree(Vector2 position)
            {var source=World.Spawn("tree",-1,position);World.Fog.Reveal(0,position,1);return source;}
            public List<EntityState> Threats(EntityState threat)
            {World.Fog.Reveal(0,threat.position,1);return new List<EntityState>{threat};}
            public void Assign(List<EntityState> threats)=>Workforce.Assign(Workers,new Cost(0,1000,0),threats);
        }
        static void Gather(World world,EntityState worker,EntityState source)
        {
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Gather,target=source.id});
            Assert.IsTrue(result.accepted,result.reason);
        }
        static bool AnyUnsafe(List<Vector2> path,World world,List<EntityState> threats)
            =>Enumerable.Range(1,path.Count-1).Any(i=>!AiResourceContinuity072.SegmentSafe(world,0,path[i-1],path[i],threats));

        [Test] public void OrdinaryGatherRejectsDangerousRouteDespiteSafeEndpointsAndRealFilteredAlternative()
        {
            var a=new Fixture();var w=a.World;var source=a.Tree(new Vector2(60,28));
            var guardian=w.Spawn("guardian",-1,new Vector2(42,37));var threats=a.Threats(guardian);
            var goal=w.WorkPoint(source,a.Worker);var ordinary=w.Navigation.FindPath(a.Worker.position,goal,a.Worker.def.radius);
            Assert.IsTrue(AiResourceContinuity072.Safe(w,0,a.Worker.position,threats));
            Assert.IsTrue(AiResourceContinuity072.Safe(w,0,source.position,threats));
            Assert.GreaterOrEqual(ordinary.Count,2);Assert.IsTrue(AnyUnsafe(ordinary,w,threats));
            var alternative=w.Navigation.FindPath(a.Worker.position,goal,a.Worker.def.radius,
                p=>AiResourceContinuity072.Safe(w,0,p,threats),(from,to)=>AiResourceContinuity072.SegmentSafe(w,0,from,to,threats));
            Assert.GreaterOrEqual(alternative.Count,3);Assert.IsFalse(AnyUnsafe(alternative,w,threats),"The safe alternate route exists, but ordinary Gather does not command it");
            string before=JsonUtility.ToJson(w.CaptureState());
            Assert.IsFalse(new AiGatherRoutes074(w,0,threats).CanGather(a.Worker,source));
            var survey=new AiResourceContinuity072(w,0).Evaluate(w.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),a.Workers,threats);
            Assert.AreEqual(0,survey.VisibleWoodRemaining,.001f,"The recovery forecast must use the same commandable ordinary route");
            Assert.AreEqual(before,JsonUtility.ToJson(w.CaptureState()),"Admission and source previews do not alter orders, paths, wallets or navigation snapshots");
            a.Assign(threats);Assert.IsFalse(a.Workforce.HasSafeSource(ResourceKind.Wood));Assert.IsEmpty(a.Worker.orders);
            Gather(w,a.Worker,source);w.Tick(.1f);
            Assert.GreaterOrEqual(a.Worker.path.Count,2);Assert.IsTrue(AnyUnsafe(a.Worker.path,w,threats),"Manual human Gather remains legal and uses its unchanged ordinary route");
        }

        [Test] public void AlternateSafeResourceWinsAndOrdinaryWorkerActuallyEarnsWoodWithoutEnteringDanger()
        {
            var a=new Fixture();var w=a.World;var unsafeSource=a.Tree(new Vector2(60,28));var safeSource=a.Tree(new Vector2(29,15));
            var guardian=w.Spawn("guardian",-1,new Vector2(42,37));var threats=a.Threats(guardian);var wallet=w.Players[0].resources;
            a.Assign(threats);Assert.AreEqual(OrderKind.Gather,a.Worker.orders.Peek().kind);Assert.AreEqual(safeSource.id,a.Worker.orders.Peek().target);
            Assert.IsTrue(a.Workforce.HasSafeSource(ResourceKind.Wood));Assert.AreEqual(JsonUtility.ToJson(wallet),JsonUtility.ToJson(w.Players[0].resources));
            for(int i=0;i<250;i++)
            {
                a.Assign(threats);w.Tick(.1f);
                Assert.IsTrue(a.Worker.Alive);Assert.IsTrue(w.Navigation.IsWalkable(a.Worker.position,a.Worker.def.radius));
                Assert.GreaterOrEqual(Vector2.Distance(a.Worker.position,guardian.position),12);
            }
            w.FlushEconomyLog();Assert.Greater(w.Players[0].gathered.wood,1);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Gather"&&e.entity==a.Worker.id&&e.target==safeSource.id&&e.amount>0));
            Assert.IsFalse(w.Events.Any(e=>e.kind=="Gather"&&e.entity==a.Worker.id&&e.target==unsafeSource.id));
        }

        [Test] public void NewlyVisibleRouteThreatOverridesFutureRebalanceAndSatisfiedWoodQuota()
        {
            var a=new Fixture();var w=a.World;a.Worker.position=new Vector2(20,28);a.Worker.previousPosition=a.Worker.position;
            var source=a.Tree(new Vector2(60,28));var guardian=w.Spawn("guardian",-1,new Vector2(42,37));w.Fog.Tick(w.Entities);w.Fog.Reveal(0,source.position,1);
            Assert.IsFalse(w.IsVisible(0,guardian));var threats=new List<EntityState>{guardian};
            a.Assign(threats);Assert.AreEqual(source.id,a.Worker.orders.Peek().target);Assert.Greater(a.Workforce.CaptureState().rebalanceAt,w.Time);
            w.Tick(.1f);w.Fog.Reveal(0,guardian.position,1);w.Fog.Reveal(0,source.position,1);Assert.IsTrue(w.IsVisible(0,guardian));
            Assert.IsTrue(w.CanGather(a.Worker,source),"The visible available target still passes ordinary Gather authority");
            Assert.IsTrue(AiResourceContinuity072.Safe(w,0,a.Worker.position,threats));Assert.IsTrue(AiResourceContinuity072.Safe(w,0,source.position,threats));
            int first=w.Events.Count;a.Assign(threats);
            Assert.IsFalse(a.Worker.orders.Any(o=>o.kind==OrderKind.Gather&&o.target==source.id));
            Assert.IsTrue(w.Events.Skip(first).Any(e=>e.kind=="Command"&&e.entity==a.Worker.id),"A real accepted replacement must interrupt the route before the 12-second rebalance");
            Assert.AreEqual(0,w.Players[0].gathered.wood);
        }

        [Test] public void ExistingCachedGatherDetourIsCheckedEvenWhenFreshOrdinaryRouteIsSafe()
        {
            var a=new Fixture();var w=a.World;var source=a.Tree(new Vector2(29,15));
            var guardian=w.Spawn("guardian",-1,new Vector2(42,37));var threats=a.Threats(guardian);Gather(w,a.Worker,source);
            var goal=w.WorkPoint(source,a.Worker);
            // Declared cached route input represents a previously legal detour
            // which newly observed weapon geometry now covers.
            a.Worker.path.Add(a.Worker.position);a.Worker.path.Add(guardian.position);a.Worker.path.Add(goal);
            a.Worker.pathIndex=1;a.Worker.pathGoal=goal;a.Worker.pathVersion=w.Navigation.Version;
            var routes=new AiGatherRoutes074(w,0,threats);Assert.IsTrue(routes.OrdinaryPathSafe(a.Worker,goal));
            Assert.IsFalse(routes.CanGather(a.Worker,source));int first=w.Events.Count;a.Assign(threats);
            Assert.IsFalse(a.Worker.orders.Any(o=>o.kind==OrderKind.Gather&&o.target==source.id));
            Assert.IsTrue(w.Events.Skip(first).Any(e=>e.kind=="Command"&&e.entity==a.Worker.id));
        }

        [TestCase("guardian",-1,true,false,false,false)]
        [TestCase("guardian",-1,false,false,false,true)]
        [TestCase("guardian",-1,true,true,false,true)]
        [TestCase("guardian",-1,true,false,true,true)]
        [TestCase("musketeer",-1,true,false,false,true)]
        [TestCase("villager",1,true,false,false,true)]
        public void RouteAdmissionUsesExactVisibleEconomicThreatSemantics(string definition,int owner,bool visible,bool dead,bool unarmed,bool admitted)
        {
            var rules=SnapshotCodec.Clone(Catalog.Load());if(unarmed){var d=rules.Entity(definition);d.damage=d.meleeDamage=d.siegeDamage=0;}
            var a=new Fixture(rules);var w=a.World;var source=a.Tree(new Vector2(60,28));
            var threat=w.Spawn(definition,owner,visible?new Vector2(42,37):new Vector2(55,28));
            w.Fog.Tick(w.Entities);w.Fog.Reveal(0,source.position,1);if(visible)w.Fog.Reveal(0,threat.position,1);
            if(dead)w.Remove(threat);else Assert.AreEqual(visible,w.IsVisible(0,threat));
            var threats=new List<EntityState>{threat};Assert.AreEqual(!admitted,AiResourceContinuity072.EconomicThreat(w,0,threat));
            Assert.AreEqual(admitted,new AiGatherRoutes074(w,0,threats).CanGather(a.Worker,source));
            a.Assign(threats);Assert.AreEqual(admitted,a.Workforce.HasSafeSource(ResourceKind.Wood));
            Assert.AreEqual(admitted,a.Worker.orders.Any(o=>o.kind==OrderKind.Gather&&o.target==source.id));
        }

        [Test] public void ContinuousThreatBoundaryEqualityMatchesExistingSafetyRules()
        {
            var a=new Fixture();var guardian=a.World.Spawn("guardian",-1,new Vector2(42,37));var threats=a.Threats(guardian);
            var routes=new AiGatherRoutes074(a.World,0,threats);
            Assert.IsTrue(routes.PointSafe(new Vector2(42,49)));Assert.IsFalse(routes.PointSafe(new Vector2(42,48.99f)));
            Assert.IsTrue(routes.SegmentSafe(new Vector2(20,49),new Vector2(64,49)));
            Assert.IsFalse(routes.SegmentSafe(new Vector2(20,48.99f),new Vector2(64,48.99f)));
        }

        [Test] public void NewPhysicalBlockadeInterruptsExistingGatherAndDoesNotForecastUnreachableWood()
        {
            var a=new Fixture();var w=a.World;var source=a.Tree(new Vector2(60,28));var threats=new List<EntityState>();a.Assign(threats);
            Assert.AreEqual(source.id,a.Worker.orders.Peek().target);w.Tick(.1f);
            for(int y=0;y<128;y++)w.Navigation.SetTerrainBlocked(new Vector2(42,y),0,true);
            w.Fog.Reveal(0,source.position,1);Assert.IsTrue(w.CanGather(a.Worker,source));
            var goal=w.WorkPoint(source,a.Worker);Assert.IsEmpty(w.Navigation.FindPath(a.Worker.position,goal,a.Worker.def.radius));
            a.Assign(threats);Assert.IsFalse(a.Workforce.HasSafeSource(ResourceKind.Wood));
            Assert.IsFalse(a.Worker.orders.Any(o=>o.kind==OrderKind.Gather&&o.target==source.id));
            Assert.AreEqual(0,new AiResourceContinuity072(w,0).Evaluate().VisibleWoodRemaining,.001f);
            for(int i=0;i<100;i++)w.Tick(.1f);Assert.AreEqual(0,w.Players[0].gathered.wood);
        }

        [Test] public void ReachableProjectedWorkPointOutsideGatherRangeCannotForecastOrAssignWoodAndClearingRestoresIncome()
        {
            var a=new Fixture();var w=a.World;var threats=new List<EntityState>();
            a.Worker.position=new Vector2(29,25);a.Worker.previousPosition=a.Worker.position;
            var source=a.Tree(new Vector2(40,28));
            // Declared terrain change isolates the source's extraction perimeter,
            // while its projected ordinary WorkPoint remains reachable and safe.
            w.Navigation.SetTerrainBlocked(source.position,3,true);
            var goal=w.WorkPoint(source,a.Worker);
            Assert.AreEqual(new Vector2(36,28),goal);
            Assert.GreaterOrEqual(w.Navigation.FindPath(a.Worker.position,goal,a.Worker.def.radius).Count,2);
            Assert.IsTrue(new AiGatherRoutes074(w,0,threats).OrdinaryPathSafe(a.Worker,goal));
            Assert.IsTrue(w.CanGather(a.Worker,source),"Manual Gather authority is independent of extraction arrival");
            var start=a.Worker.position;Gather(w,a.Worker,source);
            for(int i=0;i<160;i++)w.Tick(.1f);
            Assert.Greater((a.Worker.position-start).sqrMagnitude,1,"The real ordinary command moves toward the reachable projection");
            Assert.Greater(w.EdgeDistance(a.Worker,source),1.5f);
            w.FlushEconomyLog();Assert.AreEqual(0,w.Players[0].gathered.wood);
            Assert.IsFalse(w.Events.Any(e=>e.kind=="Gather"&&e.target==source.id&&e.amount>0));
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{a.Worker.id},order=OrderKind.Stop}).accepted);
            w.Fog.Reveal(0,source.position,1);
            Assert.IsFalse(new AiGatherRoutes074(w,0,threats).CanGather(a.Worker,source),"A reachable projected goal outside real income range is not an available source");
            Assert.AreEqual(0,new AiResourceContinuity072(w,0).Evaluate().VisibleWoodRemaining,.001f);
            a.Assign(threats);Assert.IsFalse(a.Workforce.HasSafeSource(ResourceKind.Wood));Assert.IsEmpty(a.Worker.orders);

            w.Navigation.SetTerrainBlocked(source.position,3,false);w.Fog.Reveal(0,source.position,1);
            Assert.IsTrue(new AiGatherRoutes074(w,0,threats).CanGather(a.Worker,source));
            Assert.Greater(new AiResourceContinuity072(w,0).Evaluate().VisibleWoodRemaining,0);
            a.Assign(threats);Assert.AreEqual(source.id,a.Worker.orders.Peek().target);
            for(int i=0;i<150;i++){w.Tick(.1f);a.Assign(threats);}
            w.FlushEconomyLog();Assert.Greater(w.Players[0].gathered.wood,1);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Gather"&&e.entity==a.Worker.id&&e.target==source.id&&e.amount>0));
        }

        [Test] public void DepletionAutoRetargetToUnsafeResourceIsCaughtAtNextReview()
        {
            var a=new Fixture();var w=a.World;a.Worker.position=new Vector2(39,28);a.Worker.previousPosition=a.Worker.position;
            var first=a.Tree(new Vector2(40,28));first.remaining=.01f;var next=a.Tree(new Vector2(52,28));
            var guardian=w.Spawn("guardian",-1,new Vector2(57,37));var threats=a.Threats(guardian);Gather(w,a.Worker,first);
            w.Tick(.1f);Assert.IsFalse(first.Alive);Assert.AreEqual(.01f,w.Players[0].gathered.wood,.001f);
            w.Tick(.1f);Assert.AreEqual(next.id,a.Worker.orders.Peek().target,"Ordinary depletion retarget remains unchanged between AI reviews");
            w.Fog.Reveal(0,guardian.position,1);Assert.IsTrue(w.IsVisible(0,guardian));
            Assert.IsTrue(AiResourceContinuity072.Safe(w,0,a.Worker.position,threats));int events=w.Events.Count;a.Assign(threats);
            Assert.IsFalse(a.Worker.orders.Any(o=>o.kind==OrderKind.Gather&&o.target==next.id));
            Assert.IsTrue(w.Events.Skip(events).Any(e=>e.kind=="Command"&&e.entity==a.Worker.id));
            Assert.AreEqual(.01f,w.Players[0].gathered.wood,.001f);
        }

        [Test] public void SafeGatherColdWorkforceContinuesWithIdenticalRealIncomeAndOrders()
        {
            var a=new Fixture();var w=a.World;var source=a.Tree(new Vector2(29,15));var threats=new List<EntityState>();a.Assign(threats);
            for(int i=0;i<35;i++){w.Tick(.1f);a.Assign(threats);}
            var cold=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));var worker=cold.Get(a.Worker.id);
            var ai=new AiWorkforce(cold,0);ai.RestoreState(SnapshotCodec.Clone(a.Workforce.CaptureState()));
            for(int i=0;i<200;i++){w.Tick(.1f);cold.Tick(.1f);a.Assign(threats);ai.Assign(new List<EntityState>{worker},new Cost(0,1000,0),threats);}
            w.FlushEconomyLog();cold.FlushEconomyLog();Assert.Greater(w.Players[0].gathered.wood,1);
            Assert.AreEqual(JsonUtility.ToJson(w.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(a.Workforce.CaptureState()),JsonUtility.ToJson(ai.CaptureState()));
            Assert.AreEqual(source.id,worker.orders.Peek().target);
        }

        [Test] public void OrdinaryEightWorkerOccupancyAndPauseEndAuthorityRemainIntact()
        {
            var a=new Fixture();var w=a.World;var source=a.Tree(new Vector2(29,15));
            for(int i=0;i<8;i++)a.Workers.Add(w.Spawn("villager",0,new Vector2(30+i%3,22+i/3)));
            a.Assign(new List<EntityState>());Assert.AreEqual(8,a.Workers.Count(e=>e.orders.Any(o=>o.kind==OrderKind.Gather&&o.target==source.id)));
            w.Paused=true;string paused=JsonUtility.ToJson(w.CaptureState());a.Assign(new List<EntityState>());
            Assert.AreEqual(paused,JsonUtility.ToJson(w.CaptureState()));
            w.Paused=false;w.Winner=1;string ended=JsonUtility.ToJson(w.CaptureState());a.Assign(new List<EntityState>());
            Assert.AreEqual(ended,JsonUtility.ToJson(w.CaptureState()));
            Assert.AreEqual(0,w.Players[0].gathered.wood,"Assignment alone cannot credit resources");
        }

        [Test] public void HeldWorkersAndGatherWithQueuedFollowupRemainOutsideAutomaticReassignment()
        {
            var held=new Fixture();var heldSource=held.Tree(new Vector2(29,15));var threats=new List<EntityState>();
            Assert.IsTrue(held.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{held.Worker.id},order=OrderKind.Hold}).accepted);
            string beforeHold=JsonUtility.ToJson(held.World.CaptureState());held.Assign(threats);
            Assert.AreEqual(beforeHold,JsonUtility.ToJson(held.World.CaptureState()));Assert.IsTrue(held.Worker.hold);
            Assert.IsFalse(new AiGatherRoutes074(held.World,0,threats).CanGather(held.Worker,heldSource));
            Assert.IsTrue(held.World.CanGather(held.Worker,heldSource),"Manual replacement of Hold still passes ordinary Gather authority");

            var queued=new Fixture();var source=queued.Tree(new Vector2(60,28));var guardian=queued.World.Spawn("guardian",-1,new Vector2(42,37));
            var visible=queued.Threats(guardian);Gather(queued.World,queued.Worker,source);
            Assert.IsTrue(queued.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{queued.Worker.id},
                order=OrderKind.Move,point=new Vector2(32,15),append=true}).accepted);
            Assert.AreEqual(2,queued.Worker.orders.Count);string beforeQueue=JsonUtility.ToJson(queued.World.CaptureState());queued.Assign(visible);
            Assert.AreEqual(beforeQueue,JsonUtility.ToJson(queued.World.CaptureState()),"Even a covered route cannot erase the manually queued followup through automatic reassignment");
            Assert.IsFalse(new AiGatherRoutes074(queued.World,0,visible).CanGather(queued.Worker,source));
            Assert.AreEqual(0,new AiResourceContinuity072(queued.World,0).Evaluate().VisibleWoodRemaining,.001f);
            Assert.AreEqual(2,queued.Worker.orders.Count);Assert.AreEqual(OrderKind.Move,queued.Worker.orders.Last().kind);
        }

        [Test] public void QueuedGatherersKeepTheirActualEightSlotsWithoutInflatingAvailableWorkerQuota()
        {
            var a=new Fixture();var w=a.World;var source=a.Tree(new Vector2(29,15));
            var committed=new List<EntityState>();
            for(int i=0;i<8;i++)
            {
                var worker=w.Spawn("villager",0,new Vector2(30+i%3,22+i/3));a.Workers.Add(worker);committed.Add(worker);Gather(w,worker,source);
                Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},
                    order=OrderKind.Move,point=new Vector2(35,15),append=true}).accepted);
            }
            a.Assign(new List<EntityState>());Assert.IsEmpty(a.Worker.orders,"The available ninth worker cannot take a slot already reserved by real queued Gather orders");
            Assert.AreEqual(8,a.Workers.Count(e=>e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather&&e.orders.Peek().target==source.id));
            foreach(var worker in committed)Assert.AreEqual(2,worker.orders.Count);
        }
    }
}
