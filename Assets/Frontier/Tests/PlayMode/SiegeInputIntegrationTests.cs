using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Small native-input seam: the tests retain the shipped cannon definition and
    // drive RtsInput through queued InputSystem edges.
    public sealed class SiegeInputIntegrationTests : NativeInputFixture
    {
        private EntityState Cannon(Vector2 p) => SpawnHere("cannon", 0, p);
        private EntityState EnemyBuilding(Vector2 p) => SpawnHere("barracks", 1, p);

        private IEnumerator QueueSelect(EntityState cannon)
        {
            var p=ScreenPoint(cannon.position,1);AssertWorldPoint(p);yield return Click(p);yield return Ready();
            Assert.AreEqual(cannon.id,game.Controls.FirstSelected?.id,"Pointer must select the actual cannon");
        }
        private IEnumerator QueueAttack(EntityState cannon,EntityState target,MouseButton button=MouseButton.Left)
        {
            yield return QueueSelect(cannon);if(button==MouseButton.Left)yield return Press(Key.A);var p=ScreenPoint(target.position);AssertWorldPoint(p);yield return Click(p,button);
        }

        [UnityTest]
        public IEnumerator QueuedRightClickAssignsAnExplicitVisibleBuildingTarget()
        {
            var cannon=Cannon(site+Vector2.left*5);var building=EnemyBuilding(site+Vector2.right*8);
            yield return QueueAttack(cannon,building,MouseButton.Right);
            Assert.AreEqual(OrderKind.Attack,cannon.orders.Single().kind);
            Assert.AreEqual(building.id,cannon.orders.Single().target);
            Assert.IsTrue(game.World.Events.Any(e=>e.kind=="Command"&&e.entity==cannon.id&&e.target==building.id));
        }

        [UnityTest]
        public IEnumerator UnmodifiedCannonMovesDeploysThenDamagesBuilding()
        {
            var cannon=Cannon(site+Vector2.left*25);var building=EnemyBuilding(site+Vector2.right*8);
            // A nearby non-attacking scout supplies real player vision while the cannon approaches.
            SpawnHere("villager",0,building.position+Vector2.up*10);
            yield return QueueAttack(cannon,building);
            var hp=building.hp;float arrival=-1,deploy=-1,prepared=-1,fire=-1,impact=-1,damage=-1;float beforeFire=hp;var start=cannon.position;
            for(int i=0;i<240;i++)
            {
                game.Simulation.Step();
                if(arrival<0&&Vector2.Distance(cannon.position,start)>1)arrival=game.World.Time;
                if(deploy<0&&cannon.status=="Deploying cannon")deploy=game.World.Time;
                var a=game.World.Events.LastOrDefault(e=>e.kind=="AttackPrepared"&&e.entity==cannon.id&&e.target==building.id);if(prepared<0&&a!=null)prepared=a.time;
                var f=game.World.Events.LastOrDefault(e=>e.kind=="Fire"&&e.entity==cannon.id&&e.target==building.id);if(fire<0&&f!=null){fire=f.time;beforeFire=building.hp;}
                var im=game.World.Events.LastOrDefault(e=>e.kind=="Impact"&&e.entity==cannon.id&&e.target==building.id);if(impact<0&&im!=null)impact=im.time;
                var d=game.World.Events.LastOrDefault(e=>e.kind=="Damage"&&e.entity==cannon.id&&e.target==building.id);if(damage<0&&d!=null)damage=d.time;
                if(damage>=0)break;
            }
            Assert.GreaterOrEqual(arrival,0);Assert.GreaterOrEqual(deploy,0);Assert.GreaterOrEqual(prepared,0);Assert.GreaterOrEqual(fire,0);Assert.GreaterOrEqual(impact,0);Assert.GreaterOrEqual(damage,0);
            Assert.GreaterOrEqual(prepared-deploy,2f-0.0001f);Assert.Less(prepared,fire);Assert.Less(fire,impact);Assert.AreEqual(impact,damage,"Impact and damage resolve in the same simulation tick");Assert.AreEqual(hp,beforeFire,"HP must be unchanged before first Fire");Assert.Less(building.hp,hp);
        }

        [UnityTest]
        public IEnumerator ExplicitBuildingTargetIsRetainedNearVisibleDefender()
        {
            var cannon=Cannon(site+Vector2.left*5);var building=EnemyBuilding(site+Vector2.right*8);
            var defender=SpawnHere("pike",1,site+Vector2.right*6);yield return QueueAttack(cannon,building);
            Assert.AreEqual(building.id,cannon.orders.Single().target);
            for(int i=0;i<120;i++)game.Simulation.Step();
            Assert.IsTrue(game.World.Events.Any(e=>e.kind=="Fire"&&e.entity==cannon.id&&e.target==building.id));
            Assert.AreNotEqual(defender.id,cannon.orders.Count>0?cannon.orders.Peek().target:0,"Direct Attack must not be replaced by nearby defender retargeting");
        }

        [UnityTest]
        public IEnumerator QueuedAOnGroundCreatesAttackMoveWithoutFixedTarget()
        {
            var cannon=Cannon(site+Vector2.left*5);var select=ScreenPoint(cannon.position,1);AssertWorldPoint(select);yield return Click(select);yield return Press(Key.A);var point=ScreenPoint(site+Vector2.right*8);AssertWorldPoint(point);yield return Click(point);
            Assert.AreEqual(OrderKind.AttackMove,cannon.orders.Single().kind);
            Assert.AreEqual(0,cannon.orders.Single().target);
        }
    }
}
