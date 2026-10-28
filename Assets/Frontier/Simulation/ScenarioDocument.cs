using System;
using UnityEngine;

namespace Frontier
{
    // Project-authored data only. No scripts, CLR types, asset paths or runtime
    // entity IDs enter the editor format. Stable IDs bind document references.
    [Serializable] public sealed class ScenarioDocument
    {
        public const int CurrentSchema=1;
        public int schema=CurrentSchema;
        public string id="",name="Untitled scenario",description="",rulesHash="";
        public int width=128,height=128,seed=314159,waterHeightCm=-40,victoryPlayer;
        public bool enableElimination=true;
        public int[] heightsCm=new int[129*129],surfaces=new int[128*128];
        public ScenarioRiver[] rivers=Array.Empty<ScenarioRiver>();
        public ScenarioBridge[] bridges=Array.Empty<ScenarioBridge>();
        public ScenarioStart[] starts=Array.Empty<ScenarioStart>();
        public ScenarioPlacement[] resources=Array.Empty<ScenarioPlacement>(),objects=Array.Empty<ScenarioPlacement>();
        public ScenarioGroup[] groups=Array.Empty<ScenarioGroup>();
        public Vector2[] tradeRoute=Array.Empty<Vector2>();
        public ScenarioTradeSocket[] tradeSockets=Array.Empty<ScenarioTradeSocket>();
        public ScenarioTreasure[] treasures=Array.Empty<ScenarioTreasure>();
        public ScenarioNeutralBinding[] neutralBindings=Array.Empty<ScenarioNeutralBinding>();
        public ScenarioRegion[] regions=Array.Empty<ScenarioRegion>();
        public ScenarioObjective[] objectives=Array.Empty<ScenarioObjective>();
        public ScenarioTrigger[] triggers=Array.Empty<ScenarioTrigger>();
    }
    [Serializable] public sealed class ScenarioRiver
    {
        public string id="";public Vector2[] points=Array.Empty<Vector2>();
        public float[] halfWidths=Array.Empty<float>();public bool smooth=true;
    }
    [Serializable] public sealed class ScenarioBridge
    {
        public string id="";public Vector2 center,direction=Vector2.right;
        public float halfLength=8,halfWidth=4.8f,rampLength=9,height=3.2f;
    }
    [Serializable] public sealed class ScenarioStart
    {
        public int player,team,age=1;public string civilization="coast";
        public Cost initialResources=new Cost(300,300,100);
        public Vector2 town,explorer;public Vector2[] workers=Array.Empty<Vector2>();
    }
    [Serializable] public sealed class ScenarioPlacement
    {
        public string id="",definition="";public Vector2 position;public int owner=-1;
        public float remaining=-1;public bool complete=true;
    }
    [Serializable] public sealed class ScenarioGroup
    {public string id="";public string[] objectIds=Array.Empty<string>();}
    [Serializable] public sealed class ScenarioTradeSocket
    {public string id="";public Vector2 position;public int waypoint;}
    [Serializable] public sealed class ScenarioTreasure
    {public string id="";public int definition;public Vector2 position;public Vector2[] guardPositions=Array.Empty<Vector2>();}
    [Serializable] public sealed class ScenarioNeutralBinding
    {public string definition="",socketId="";}
    [Serializable] public sealed class ScenarioRegion
    {public string id="";public Vector2 center,size=new Vector2(10,10);}
    [Serializable] public sealed class ScenarioObjective
    {
        public string id="",title="Objective",description="",kind="manual",objectId="",targetId="",regionId="";
        public int player;public bool required=true;public ResourceKind resource=ResourceKind.None;
        public float amount=1,holdSeconds=30,deadline;
        public string[] prerequisites=Array.Empty<string>();
    }
    [Serializable] public sealed class ScenarioTrigger
    {
        public string id="";public bool enabled=true,matchAny;public int maxFires=1;public float repeatSeconds=1;
        public ScenarioCondition[] conditions=Array.Empty<ScenarioCondition>();
        public ScenarioAction[] actions=Array.Empty<ScenarioAction>();
    }
    [Serializable] public sealed class ScenarioCondition
    {
        public string kind="time",objectId="",targetId="",regionId="",objectiveId="",triggerId="";
        public int player;public ResourceKind resource=ResourceKind.None;public float amount=1,time;
    }
    [Serializable] public sealed class ScenarioAction
    {
        public string kind="message",objectiveId="",regionId="",objectId="",triggerId="",definition="",targetId="",message="";
        public int player,count=1;public Vector2 point;public ResourceKind resource=ResourceKind.None;public float amount=1;
    }
    [Serializable] public sealed class ScenarioEnvelope
    {public int schema=1;public string encoding="frontier-scenario-json-v1",payload="",sha256="";}
    public sealed class ScenarioFileInfo
    {public string id,name,contentHash,path,reason;}
}
