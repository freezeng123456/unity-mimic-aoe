using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Public-order and grid regression fixtures. This is not a normal paid-game
    // acceptance scenario: fixture actors are spawned explicitly for isolation.
    public sealed class FormationBoundary082Tests
    {
        [TestCase(4,64,1.8f)][TestCase(124,64,1.8f)]
        [TestCase(64,4,1.8f)][TestCase(64,124,1.8f)]
        [TestCase(4,4,1.8f)][TestCase(124,124,1.8f)]
        [TestCase(4,64,3.3f)][TestCase(124,64,3.3f)]
        [TestCase(64,4,3.3f)][TestCase(64,124,3.3f)]
        [TestCase(4,4,3.3f)][TestCase(124,124,3.3f)]
        public void OneLegalEdgeSlotPreservesRequestedCenter(float x,float y,float spacing)
        {
            var navigation=new GridNavigation();var requested=new Vector2(x,y);
            Assert.IsTrue(navigation.IsWalkable(requested,spacing*.38f),"Fixture endpoint must be body-clear before issuing any formation query");
            var slots=navigation.FormationSlots(requested,1,spacing);
            Assert.AreEqual(1,slots.Count);Assert.AreEqual(requested,slots[0],"A legal edge target must not be replaced by the old 8..119 inset");
        }

        [TestCase(-1000,64)][TestCase(1000,64)]
        [TestCase(64,-1000)][TestCase(64,1000)]
        public void OutsideRequestStillProducesBodyClearSlots(float x,float y)
        {
            var navigation=new GridNavigation();const float spacing=3.3f;
            var slots=navigation.FormationSlots(new Vector2(x,y),12,spacing);
            Assert.AreEqual(12,slots.Count);
            foreach(var slot in slots)Assert.IsTrue(navigation.IsWalkable(slot,spacing*.38f),"Clamping and formation expansion must retain full body clearance");
            AssertSpacing(slots.ToArray(),spacing);
        }

        [Test]public void BlockedEdgeCenterIsSkippedAndEveryAlternativeRetainsClearance()
        {
            var navigation=new GridNavigation();var requested=new Vector2(64,124);const float spacing=3.3f;
            navigation.SetBlocked(requested,0,true);
            var slots=navigation.FormationSlots(requested,16,spacing);
            Assert.AreEqual(16,slots.Count);Assert.IsFalse(slots.Contains(requested));
            foreach(var slot in slots)Assert.IsTrue(navigation.IsWalkable(slot,spacing*.38f),"Removing the arbitrary inset must not remove the obstacle/body predicate");
            AssertSpacing(slots.ToArray(),spacing);
        }

        [Test]public void EdgeGroupUsesItsRequestedCenterAndExpandsOnlyIntoLegalSlots()
        {
            var navigation=new GridNavigation();var requested=new Vector2(64,124);const float spacing=3.3f;
            var slots=navigation.FormationSlots(requested,24,spacing);
            Assert.AreEqual(24,slots.Count);Assert.AreEqual(requested,slots[0]);
            foreach(var slot in slots)Assert.IsTrue(navigation.IsWalkable(slot,spacing*.38f));
            AssertSpacing(slots.ToArray(),spacing);
        }

        [TestCase(false,false,OrderKind.Move)][TestCase(false,true,OrderKind.Move)]
        [TestCase(true,false,OrderKind.Move)][TestCase(true,true,OrderKind.Move)]
        [TestCase(false,false,OrderKind.AttackMove)][TestCase(false,true,OrderKind.AttackMove)]
        [TestCase(true,false,OrderKind.AttackMove)][TestCase(true,true,OrderKind.AttackMove)]
        public void PublicLandAndWaterOrdersKeepLegalEdgeClicksAndReachThem(bool waterborne,bool north,OrderKind kind)
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.River);
            // The authored straight river supplies independently fixed, legal
            // north/south land and water endpoints, with no mask or fog edits.
            var requested=new Vector2(waterborne?64:40,north?124:4);
            var start=new Vector2(requested.x,north?112:16);
            var actor=world.Spawn(waterborne?"gunboat":"cannon",0,start);
            var navigation=world.NavigationFor(actor);float spacing=Mathf.Max(1.8f,actor.def.radius*4+.3f);
            Assert.IsTrue(navigation.IsWalkable(start,actor.def.radius));
            Assert.IsTrue(navigation.IsWalkable(requested,spacing*.38f));
            Assert.AreEqual(waterborne,world.IsWater(requested));
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{actor.id},order=kind,point=requested});
            Assert.IsTrue(result.accepted,result.reason);Assert.AreEqual(1,actor.orders.Count);
            Assert.AreEqual(requested,actor.orders.Peek().point,"Check the public command against the raw clicked point, not another FormationSlots call");
            for(int tick=0;tick<200&&actor.orders.Count>0;tick++)
            {
                var previous=actor.position;world.Tick(.1f);
                Assert.IsTrue(actor.Alive);Assert.IsTrue(navigation.IsWalkable(actor.position,actor.def.radius));
                Assert.IsTrue(navigation.ClearLine(previous,actor.position,actor.def.radius));
                Assert.LessOrEqual(Vector2.Distance(previous,actor.position),world.Speed(actor)*.1f+.0001f);
                Assert.AreEqual(waterborne,world.IsWater(actor.position));
            }
            Assert.IsEmpty(actor.orders,"The ordinary issued order must finish within twenty simulation seconds");
            // Move completes at its endpoint; the existing AttackMove contract
            // dequeues inside squared distance 1.5 (MovementCombat), before an
            // idle scan. Both still measure against the raw clicked target.
            float tolerance=kind==OrderKind.Move?.051f:Mathf.Sqrt(1.5f);
            Assert.Less(Vector2.Distance(actor.position,requested),tolerance,"Arrival must obey this order's existing contract at the originally requested edge target");
        }

        static void AssertSpacing(Vector2[] slots,float spacing)
        {
            for(int i=0;i<slots.Length;i++)for(int j=i+1;j<slots.Length;j++)
                Assert.GreaterOrEqual(Vector2.Distance(slots[i],slots[j]),spacing-.0001f);
        }
    }
}
