using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class EstuaryContent083Tests
    {
        static World Opening()=>new World(Catalog.Load(),42,"coast","coast",5,MapTopology.Estuary);
        static List<Vector2> Exact(GridNavigation nav,Vector2 from,Vector2 to,float radius)
        {
            Assert.IsTrue(nav.IsWalkable(from,radius));Assert.IsTrue(nav.IsWalkable(to,radius));
            var path=nav.ClearLine(from,to,radius)?new List<Vector2>{to}:nav.FindPath(from,to,radius);
            Assert.IsNotEmpty(path);Assert.Less(Vector2.Distance(path.Last(),to),.001f);
            var previous=from;foreach(var point in path){Assert.IsTrue(nav.IsWalkable(point,radius));Assert.IsTrue(nav.ClearLine(previous,point,radius));previous=point;}return path;
        }
        static float Historical(MapLayout layout,Vector2 point)
        {
            float d=layout.SignedBankDistance(point);
            return d<=0?Mathf.Lerp(layout.WaterHeight-.02f,-.9f,Mathf.SmoothStep(0,1,-d/4))
                :(layout.WaterHeight-.02f)*(1-Mathf.SmoothStep(0,1,d/3));
        }
        [Test] public void ActualBaseline20OpeningRemainsByteExactIncludingResourceIdentitiesAndNavigation()
        {
            var w=Opening();MapBaselineNeutral088.AssertExpandedRules(w.Rules);
            // Compare every historical opening field after normalizing only the rule identity.
            var snapshot=w.CaptureState();Assert.AreEqual(MapBaselineNeutral088.CurrentRulesHash,snapshot.rulesHash);
            snapshot.rulesHash=NeutralSaveMigration088.LegacyRulesHash;
            Assert.AreEqual("3f4f78e59cd69a4bd88b0eced04bc2b029e62a3dbdce3e733b495bf17bbfa0ab",SnapshotCodec.Hash(JsonUtility.ToJson(snapshot)),"Recorded EstuaryBaseline20 after rule-identity normalization, seed42 coast/coast agecap5");
            Assert.AreEqual(new Vector2(48,70),w.Get(103).position);Assert.AreEqual("mine",w.Get(103).def.id);Assert.AreEqual(new Vector2(82,93),w.Get(88).position);Assert.AreEqual("mine",w.Get(88).def.id);
            Assert.AreEqual(6,w.Entities.Count(e=>e.def.id=="mine"));Assert.AreEqual(62,w.Entities.Count(e=>e.def.id=="tree"));Assert.AreEqual(6,w.Entities.Count(e=>e.def.id=="fish"));Assert.AreEqual(0,w.Time);Assert.IsFalse(w.Layout.RequiresTransport);
            CollectionAssert.AreEqual(new[]{new Vector2(44,25),new Vector2(38,103)},w.Layout.DockSites[0]);CollectionAssert.AreEqual(new[]{new Vector2(79,103),new Vector2(62,19)},w.Layout.DockSites[1]);
        }
        [TestCase(0)] [TestCase(1)] public void BothIndependentMouthRequestsKeepActualLargestArtilleryUnloadAfterBothOriginalNorthernDocks(int owner)
        {
            // Explicit short geometry/command fixture: directly placed completed
            // docks/ship/cargo, not a paid economy or natural-match substitute.
            var original=Opening();var largest=original.Rules.entities.Where(d=>d.kind==EntityKind.Unit&&!d.waterborne&&(d.tags&Tags.Artillery)!=0).OrderByDescending(d=>d.radius).First();Assert.AreEqual("great_bombard",largest.id);Assert.AreEqual(1.05f,largest.radius,.00001f);
            for(int bank=0;bank<2;bank++)
            {
                var w=Opening();var request=EstuaryContent083.Landing(bank);
                CollectionAssert.AreEqual(new[]{EstuaryContent083.WestMouthLanding,EstuaryContent083.EastMouthLanding},w.Layout.LandingSites[owner]);
                Assert.Greater(w.Layout.DockSites.SelectMany(x=>x).Min(p=>Vector2.Distance(p,request)),11);
                w.Spawn("dock",0,w.Layout.DockSites[0][1]);w.Spawn("dock",1,w.Layout.DockSites[1][0]);
                var ship=w.Spawn("transport_ship",owner,w.Layout.FishPoints[0]);Assert.IsTrue(w.TryTransportShore(ship,request,largest.radius,out var land,out var berth));Assert.AreEqual(request,land);Exact(w.WaterNavigation,ship.position,berth,ship.def.radius);
                ship.position=ship.previousPosition=berth;var cargo=w.Spawn(largest.id,owner,land);cargo.garrisonedIn=ship.id;cargo.position=cargo.previousPosition=ship.position;ship.occupants.Add(cargo.id);w.Fog.Reveal(owner,land,25);
                var result=w.Issue(new GameCommand{player=owner,kind=CommandKind.Disembark,actors=new[]{ship.id},point=request});Assert.IsTrue(result.accepted,result.reason);while(cargo.Alive&&cargo.garrisonedIn!=0&&w.Time<3)w.Tick(.1f);
                Assert.IsTrue(cargo.Alive);Assert.AreEqual(0,cargo.garrisonedIn);Assert.AreEqual(request,cargo.position);Assert.AreEqual(1,w.Events.Count(e=>e.kind=="Disembarked"&&e.entity==cargo.id&&e.target==ship.id));
                foreach(int id in bank==0?new[]{103,109,119}:new[]{88,89,120})Exact(w.Navigation,cargo.position,w.Get(id).position,largest.radius);
            }
        }
        [Test] public void ContinuousDryTidalFlatKeepsHistoricalWetBedPadsAndBoundedRealSlopes()
        {
            var layout=Opening().Layout;int changed=0;float maximumDelta=0,maximumSlope=0;
            for(int z=0;z<=256;z++)for(int x=0;x<=256;x++)
            {
                var p=new Vector2(x*.5f,z*.5f);float d=layout.SignedBankDistance(p),old=Historical(layout,p),actual=layout.BankHeight(p);
                Assert.IsFalse(float.IsNaN(actual)||float.IsInfinity(actual));Assert.LessOrEqual(actual,.09001f);Assert.GreaterOrEqual(actual,-.90001f);
                float delta=Mathf.Abs(actual-old);maximumDelta=Mathf.Max(maximumDelta,delta);if(delta>.00001f)changed++;
                if(d<=0||d>=10||layout.EstuaryPreservationWeight083(p)==0||layout.EstuaryMouthWeight083(p)==0)Assert.AreEqual(old,actual,"Legacy wet/pad/outside-query exactness at "+p);
                if(x<256)maximumSlope=Mathf.Max(maximumSlope,Mathf.Abs(actual-layout.BankHeight(p+Vector2.right*.5f))/.5f);
                if(z<256)maximumSlope=Mathf.Max(maximumSlope,Mathf.Abs(actual-layout.BankHeight(p+Vector2.up*.5f))/.5f);
                var c=layout.BankLayers(p);Assert.That(c.r,Is.InRange(0,1));Assert.That(c.g,Is.InRange(0,1));Assert.That(c.b,Is.InRange(0,1));Assert.AreEqual(1,c.r+c.g+c.b,.00001f);
            }
            Assert.Greater(changed,50,"A real continuous north-mouth morphology must change more than one sampled point");Assert.Less(maximumDelta,.43f);Assert.LessOrEqual(maximumSlope,.4f);
            foreach(var bank in layout.DockSites)foreach(var dock in bank)
            {
                var direction=layout.WaterDirection(dock);var side=new Vector2(-direction.y,direction.x);
                foreach(var p in new[]{dock,dock+direction*5,dock+direction*6.4f,dock+direction*6.4f+side*1.25f,dock+direction*6.4f-side*1.25f})Assert.AreEqual(Historical(layout,p),layout.BankHeight(p),"Actual quay/exit pad remains historical");
            }
        }
        [Test] public void EstuaryScopeExcludesAllOtherMapsCustomScenariosAndLargerRosters()
        {
            Assert.IsTrue(EstuaryContent083.Applies(Opening().Layout));
            foreach(string id in MapTopology.All.Where(id=>id!=MapTopology.Estuary))Assert.IsFalse(EstuaryContent083.Applies(MapLayout.Create(id,42)));
            Assert.IsFalse(EstuaryContent083.Applies(MapLayout.Create(MapTopology.Estuary,42,4)));
            var rules=Catalog.Load();var document=ScenarioEditorSession.FromMap(rules,MapTopology.Estuary,42,2);Assert.IsFalse(EstuaryContent083.Applies(ScenarioCompiler.Compile(document,rules,2)));
            Assert.Throws<ArgumentOutOfRangeException>(()=>EstuaryContent083.Landing(-1));Assert.Throws<ArgumentOutOfRangeException>(()=>EstuaryContent083.Landing(2));
        }
    }
}
