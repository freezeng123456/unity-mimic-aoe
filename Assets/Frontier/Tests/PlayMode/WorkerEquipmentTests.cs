using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Real simulation work + native rig transitions, in controlled fixtures.
    public sealed class WorkerEquipmentTests
    {
        GameObject root;World w;EntityState worker;UnitRig rig;
        [UnitySetUp] public IEnumerator SetUp()
        {
            root=new GameObject("Worker equipment fixture");w=new World(Catalog.Load(),5091,"coast","coast",5);
            foreach(var e in w.Entities.Where(e=>(e.def.tags&Tags.Guardian)!=0).ToArray())w.Remove(e);
            worker=w.Spawn("villager",0,new Vector2(33,28));w.Fog.Tick(w.Entities);
            rig=ProceduralArt.BuildEntity(worker,root.transform).GetComponent<UnitRig>();yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown(){Object.Destroy(root);yield return null;}
        void Order(OrderKind kind,EntityState target=null,Vector2 point=default)
        {
            w.Fog.Tick(w.Entities);var r=w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=kind,target=target?.id??0,point=point});Assert.IsTrue(r.accepted,r.reason);
        }
        AnimationPose Apply(EntityState target=null,EntityState actor=null)
        {
            var funds=w.Players[0].resources;int events=w.Events.Count;
            var pose=UnitAnimation.Evaluate(actor??worker,w.Time,workTarget:target);UnitAnimation.Apply(rig,pose);
            Assert.AreEqual(funds,w.Players[0].resources);Assert.AreEqual(events,w.Events.Count,"Presentation cannot create economy or simulation events");return pose;
        }
        void Active(WorkerTool expected)
        {
            Assert.AreEqual(expected,rig.WorkerEquipment.CurrentTool);
            var roots=rig.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==rig.WeaponPivot||t.name=="Basket").ToArray();
            Assert.AreEqual(expected==WorkerTool.None?0:1,roots.Count(t=>t.gameObject.activeInHierarchy),"Only current work equipment may be active");
        }
        EntityState Resource(string id)
        {
            var r=w.Spawn(id,id=="farm"?0:-1,new Vector2(40,40));
            worker.position=r.position-Vector2.right*((r.def.kind==EntityKind.Building?r.def.width*.5f:r.def.radius)+worker.def.radius+.7f);
            return r;
        }
        [UnityTest] public IEnumerator RealResourceIncomeSelectsDistinctToolsIncludingCratesAndFarm()
        {
            foreach(var item in new[]{("tree",WorkerTool.Axe),("mine",WorkerTool.Pick),("berry",WorkerTool.Basket),("crate_wood",WorkerTool.Basket),("farm",WorkerTool.Basket)})
            {
                var r=Resource(item.Item1);var funds=w.Players[0].gathered;Order(OrderKind.Gather,r);w.Tick(.1f);
                var now=w.Players[0].gathered;Assert.Greater(now.food+now.wood+now.coin,funds.food+funds.wood+funds.coin);
                Assert.AreEqual(UnitPoseState.Gather,Apply(r).state);Active(item.Item2);
                var hand=rig.RightHand.position;w.Tick(.1f);w.Tick(.1f);Apply(r);Assert.Greater(Vector3.Distance(hand,rig.RightHand.position),.005f,"Actual work at later simulation time must change the gesture");
                Order(OrderKind.Stop);Apply();Active(WorkerTool.None);w.Remove(r);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator PaidBuildRepairAndUnpaidWaitKeepPoseConsistentWithProgress()
        {
            w.Players[0].resources=new Cost(1000,1000,1000);float wood=w.Players[0].resources.wood;
            var result=w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="house",point=new Vector2(36,28)});Assert.IsTrue(result.accepted,result.reason);
            var house=w.Get(worker.orders.Peek().target);w.Tick(.1f);Assert.Greater(house.construction,0);Assert.Less(w.Players[0].resources.wood,wood);
            Assert.AreEqual(UnitPoseState.Build,Apply(house).state);Active(WorkerTool.Hammer);
            Order(OrderKind.Stop);house.construction=1;house.hp=w.MaxHp(house)-100;Order(OrderKind.Repair,house);
            wood=w.Players[0].resources.wood;float hp=house.hp;w.Tick(.1f);Assert.Greater(house.hp,hp);Assert.Less(w.Players[0].resources.wood,wood);
            Assert.AreEqual(UnitPoseState.Repair,Apply(house).state);Active(WorkerTool.Hammer);
            w.Players[0].resources=new Cost();hp=house.hp;w.Tick(.1f);Assert.AreEqual(hp,house.hp);Assert.AreEqual("Repair needs resources",worker.status);Apply(house);Active(WorkerTool.None);
            yield return null;
        }
        [UnityTest] public IEnumerator MoveUnfinishedFacilityAndReusedIdleRigClearOldWorkEquipment()
        {
            var tree=Resource("tree");Order(OrderKind.Gather,tree);w.Tick(.1f);Apply(tree);Active(WorkerTool.Axe);
            Order(OrderKind.Move,point:worker.position+Vector2.left*5);w.Tick(.1f);Assert.AreEqual(UnitPoseState.Move,Apply().state);Active(WorkerTool.None);
            Order(OrderKind.Stop);w.Remove(tree);
            var farm=Resource("farm");farm.construction=.1f;Order(OrderKind.Gather,farm);var funds=w.Players[0].resources;
            w.Tick(.1f);Assert.AreEqual(funds,w.Players[0].resources);Assert.IsFalse(worker.IsWorkingAt(w.Time));Apply(farm);Active(WorkerTool.None);
            w.Remove(farm);var mine=Resource("mine");Order(OrderKind.Gather,mine);w.Tick(.1f);Apply(mine);Active(WorkerTool.Pick);
            var idle=w.Spawn("villager",0,new Vector2(60,60));Apply(actor:idle);Active(WorkerTool.None);
            Apply(mine);Active(WorkerTool.Pick);w.Remove(worker);Assert.AreEqual(UnitPoseState.Death,Apply().state);Active(WorkerTool.None);
            yield return null;
        }
        [UnityTest] public IEnumerator PauseFreezesCurrentWorkPoseAndResumeContinuesEconomy()
        {
            var tree=Resource("tree");Order(OrderKind.Gather,tree);w.Tick(.1f);Apply(tree);Active(WorkerTool.Axe);
            var hand=rig.RightHand.position;var rotation=rig.WeaponPivot.rotation;float at=w.Time,wood=w.Players[0].resources.wood;
            w.Paused=true;w.Tick(2);Apply(tree);Assert.AreEqual(at,w.Time);Assert.AreEqual(wood,w.Players[0].resources.wood);Assert.AreEqual(hand,rig.RightHand.position);Assert.Less(Quaternion.Angle(rotation,rig.WeaponPivot.rotation),.001f);
            w.Paused=false;w.Tick(.2f);Apply(tree);Assert.Greater(w.Players[0].resources.wood,wood);Assert.Greater(Vector3.Distance(hand,rig.RightHand.position),.005f);
            yield return null;
        }
    }
}
