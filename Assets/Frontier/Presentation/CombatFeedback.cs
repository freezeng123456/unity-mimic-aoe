using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Visual-only consumers cannot write damage or obtain a hidden target position.
    public sealed class CombatFeedback : MonoBehaviour
    {
        private sealed class Effect
        {
            public GameObject go;public Renderer renderer;public readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
            public float start,duration;public Vector3 origin,scale;
            public int healer,patient;
        }
        private World world;private Transform parent;private int observer,eventCursor;
        private readonly List<Effect> active=new List<Effect>();
        private readonly Stack<Effect> pool=new Stack<Effect>();
        private readonly Dictionary<int,float> healingShownAt=new Dictionary<int,float>();
        private Material material;
        public const int Capacity=24;
        public int ActiveCount=>active.Count;
        public int PooledCount=>pool.Count;
        public void Bind(World value,int player,Transform effectParent)
        {
            Dispose();world=value;observer=player;parent=effectParent!=null?effectParent:transform;
            eventCursor=world?.Events.Count??0;
        }
        public static bool CanDisplay(World value,int player,SimEvent ev)
        {
            return value!=null&&ev!=null&&player>=0&&player<value.Players.Length
                &&(ev.visibilityMask&(1<<player))!=0&&value.Fog.At(player,ev.position)==VisibilityState.Visible;
        }
        public void Tick()
        {
            if(world==null)return;
            for(int i=active.Count-1;i>=0;i--)
            {
                var effect=active[i];float elapsed=world.Time-effect.start;
                if(elapsed>=effect.duration||world.Fog.At(observer,new Vector2(effect.origin.x,effect.origin.z))!=VisibilityState.Visible||
                    effect.patient!=0&&(!world.IsVisible(observer,world.Get(effect.patient))||!world.IsVisible(observer,world.Get(effect.healer))))
                {Recycle(effect);active.RemoveAt(i);continue;}
                float t=Mathf.Clamp01(elapsed/effect.duration);
                effect.go.transform.position=effect.origin+Vector3.up*(t*.4f);
                effect.go.transform.localScale=effect.scale*(1-t);
            }
            if(world.Paused)return;
            for(;eventCursor<world.Events.Count;eventCursor++)
            {
                var ev=world.Events[eventCursor];float duration;Vector3 scale;Color color;
                if(ev.kind=="Fire"&&ev.message!="Melee")
                {
                    var source=world.Get(ev.entity);
                    if(source?.def.id=="longbow"||source?.def.id=="native_scout")continue;
                    duration=.13f;scale=Vector3.one*.5f;color=new Color(1,.75f,.23f);
                }
                else if(ev.kind=="Impact")
                {duration=.28f;scale=new Vector3(.8f,.4f,.8f);color=new Color(.7f,.54f,.3f);}
                else if(ev.kind=="Death")
                {duration=.6f;scale=new Vector3(1,.8f,1);color=new Color(.46f,.38f,.28f);}
                else if(ev.kind=="Heal"&&ev.hasHealing&&ev.healing!=null&&ev.healing.actual>0)
                {
                    if(!world.IsVisible(observer,world.Get(ev.entity))||!world.IsVisible(observer,world.Get(ev.target)))continue;
                    if(healingShownAt.TryGetValue(ev.target,out float shown)&&ev.time-shown<.7f)continue;
                    duration=.36f;scale=new Vector3(.32f,.12f,.32f);color=new Color(.77f,.88f,.72f);
                }
                else continue;
                if(world.Time-ev.time>=duration||!CanDisplay(world,observer,ev)||active.Count>=Capacity)continue;
                var effect=pool.Count>0?pool.Pop():Create();effect.start=ev.time;effect.duration=duration;effect.scale=scale;
                effect.healer=ev.kind=="Heal"?ev.entity:0;effect.patient=ev.kind=="Heal"?ev.target:0;
                if(effect.patient!=0)healingShownAt[effect.patient]=ev.time;
                var entity=world.Get(ev.kind=="Impact"||ev.kind=="Heal"?ev.target:ev.entity);
                float height=world.SurfaceHeight(ev.position,entity?.def.waterborne??false);
                effect.origin=new Vector3(ev.position.x,height+(ev.kind=="Fire"?(entity?.def.id=="gunboat"?.83f:1.2f):.6f),ev.position.y);
                effect.go.transform.SetParent(parent,false);effect.go.transform.position=effect.origin;
                effect.go.transform.localScale=scale*Mathf.Clamp01(1-(world.Time-ev.time)/duration);
                effect.tint.SetColor("_BaseColor",color);effect.renderer.SetPropertyBlock(effect.tint);
                effect.go.SetActive(true);active.Add(effect);
            }
        }
        private Effect Create()
        {
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Frontier combat feedback",enableInstancing=true};}
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Pooled combat feedback";
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return new Effect{go=go,renderer=renderer};
        }
        private void Recycle(Effect effect){effect.go.SetActive(false);pool.Push(effect);}
        public void Dispose()
        {
            foreach(var effect in active)if(effect.go!=null){effect.go.SetActive(false);Destroy(effect.go);}active.Clear();
            while(pool.Count>0){var effect=pool.Pop();if(effect.go!=null)Destroy(effect.go);}
            if(material!=null)Destroy(material);material=null;world=null;eventCursor=0;healingShownAt.Clear();
        }
        private void OnDestroy(){Dispose();}
    }
}
