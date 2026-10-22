using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class BridgeTraffic082RecognitionTests
    {
        const string RecordedWorldSha256="b3900b7ca4706552014954782edd50f6ef20c7d02a2d12268f973933ca366436";
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] sealed class Check {public string name;public bool passed;}
        [Serializable] sealed class Receipt
        {
            public string label,error,sourceSnapshotSha256=RecordedWorldSha256;
            public string scope="Query-only geometric regression; no gameplay movement, endpoint arrival or240s acceptance";
            public bool passed,worldUnchanged;public Vector2[] path;public Check[] checks;
        }
        static World River()
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,"river");
            foreach(var entity in world.Entities.ToArray())world.Remove(entity);return world;
        }
        static string State(World world)=>JsonUtility.ToJson(world.CaptureState());
        static void Prepare(World world,EntityState actor,Vector2 goal,List<Vector2> path)
        {
            var method=typeof(World).GetMethod("PrepareBridgeTraffic",Flags);Assert.NotNull(method);
            method.Invoke(world,new object[]{actor,goal,path});
        }
        static bool Line(World world,EntityState actor,Vector2 start,Vector2 end)
        {
            var method=typeof(World).GetMethod("MovementLineFree",Flags);Assert.NotNull(method);
            return (bool)method.Invoke(world,new object[]{actor,start,end});
        }
        static void Observe(World world,string label,List<Vector2> path,Action<Action<string,bool>> query)
        {
            string before=State(world);var checks=new List<Check>();var receipt=new Receipt{label=label,path=path.ToArray()};
            try
            {
                query((name,condition)=>{checks.Add(new Check{name=name,passed=condition});Assert.IsTrue(condition,name);});
                receipt.worldUnchanged=before==State(world);Assert.IsTrue(receipt.worldUnchanged,"Query cannot change serialized World");receipt.passed=true;
            }
            catch(Exception e){receipt.error=e.ToString();throw;}
            finally
            {
                receipt.worldUnchanged=before==State(world);receipt.checks=checks.ToArray();
                string output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");
                if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","BridgeTraffic082");
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"bridge-recognition-"+label+".json"),JsonUtility.ToJson(receipt,true));
            }
        }
        [Test] public void ActualEastBank321PathKeepsEveryOriginalClearSegmentAndFirstStep()
        {
            var world=River();var path=new List<Vector2>{new Vector2(75.80056762695312f,39.98927307128906f),new Vector2(80,45),new Vector2(77.9000015258789f,50.79999923706055f)};
            var actor=world.Spawn("hussar",0,path[0]);Vector2 goal=path[path.Count-1];
            Observe(world,"actual-eastbank321",path,check=>{
                check("actual original hussar radius",Mathf.Abs(actor.def.radius-.65f)<.00001f);
                check("actual origin outside deck",!world.Layout.Bridges[0].Contains(actor.position));
                check("all actual path legs remain on the dry east bank",path.All(p=>p.x>MapTopology.RiverMaxX+actor.def.radius));
                Prepare(world,actor,goal,path);
                for(int i=1;i<path.Count;i++)
                {
                    check("actual Navigation.ClearLine leg"+i,world.Navigation.ClearLine(path[i-1],path[i],actor.def.radius));
                    check("same-bank original leg is not assigned a bridge lane"+i,Line(world,actor,path[i-1],path[i]));
                }
                Vector2 firstStep=Vector2.MoveTowards(actor.position,path[1],world.Speed(actor)*.1f);
                check("ordinary first0.6m physical navigation step",world.Navigation.ClearLine(actor.position,firstStep,actor.def.radius));
                check("ordinary first step remains available",Line(world,actor,actor.position,firstStep));
                var cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));var mirror=cold.Get(actor.id);Prepare(cold,mirror,goal,path);
                check("cold query preserves the same-bank first step",Line(cold,mirror,mirror.position,firstStep));
            });
        }
        [Test] public void AnActorBornOnTheDeckKeepsItsOutwardTrafficDirection()
        {
            var world=River();var actor=world.Spawn("hussar",0,new Vector2(64,40));Vector2 goal=new Vector2(84,40);
            var path=world.Navigation.FindPath(actor.position,goal,actor.def.radius);
            Observe(world,"born-on-deck",path,check=>{
                check("actual deck origin",world.Layout.Bridges[0].Contains(actor.position));check("static outward route exists",path.Count>=2);
                Prepare(world,actor,goal,path);Vector2 right=actor.position+new Vector2(0,-.6f),left=actor.position+new Vector2(0,.6f);
                check("both directions are physical navigation-clear",world.Navigation.ClearLine(actor.position,right,actor.def.radius)&&world.Navigation.ClearLine(actor.position,left,actor.def.radius));
                check("rightward lane correction remains available",Line(world,actor,actor.position,right));
                check("opposing-half correction is still rejected",!Line(world,actor,actor.position,left));
            });
        }
        [Test] public void CompleteSavedPathKeepsPastAndFutureBridgeDirectionsSeparately()
        {
            var world=River();var actor=world.Spawn("hussar",0,new Vector2(76,38));
            // A query fixture concatenates real static routes through two distinct
            // bridges. It models the full saved path without moving/injecting one.
            var anchors=new[]{new Vector2(44,40),new Vector2(84,40),new Vector2(84,88),new Vector2(44,88),new Vector2(30,100)};
            var path=new List<Vector2>();
            for(int i=1;i<anchors.Length;i++)
            {
                var leg=world.Navigation.FindPath(anchors[i-1],anchors[i],actor.def.radius);Assert.GreaterOrEqual(leg.Count,2);
                if(path.Count>0)leg.RemoveAt(0);path.AddRange(leg);
            }
            Observe(world,"full-saved-two-bridges",path,check=>{
                Prepare(world,actor,anchors[anchors.Length-1],path);
                check("actor has already left the first deck",!world.Layout.Bridges[0].Contains(actor.position));
                Vector2 firstRight=new Vector2(76.6f,38),firstWrong=new Vector2(76,40);
                check("first bridge eastbound right remains available",Line(world,actor,actor.position,firstRight));
                check("past median crossing still rejects wrong half before ramp exit",!Line(world,actor,actor.position,firstWrong));
                Vector2 northRight=new Vector2(64,89),northRightEnd=new Vector2(63.4f,89),northWrong=new Vector2(64,87),northWrongEnd=new Vector2(63.4f,87);
                check("future bridge physical probes are both clear",world.Navigation.ClearLine(northRight,northRightEnd,actor.def.radius)&&world.Navigation.ClearLine(northWrong,northWrongEnd,actor.def.radius));
                check("future north bridge westbound right stays available",Line(world,actor,northRight,northRightEnd));
                check("future north bridge opposite half remains rejected",!Line(world,actor,northWrong,northWrongEnd));
                var cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));var mirror=cold.Get(actor.id);Prepare(cold,mirror,anchors[anchors.Length-1],path);
                check("both saved bridge directions survive a cold query",Line(cold,mirror,mirror.position,firstRight)&&!Line(cold,mirror,mirror.position,firstWrong)&&Line(cold,mirror,northRight,northRightEnd)&&!Line(cold,mirror,northWrong,northWrongEnd));
            });
        }
    }
}
