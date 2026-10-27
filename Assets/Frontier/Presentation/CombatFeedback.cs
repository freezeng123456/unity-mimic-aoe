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
            public int healer,patient;public bool fire;public readonly Transform[] smoke=new Transform[3];
        }
        private World world;private Transform parent;private int observer,eventCursor;
        private readonly List<Effect> active=new List<Effect>();
        private readonly Stack<Effect> pool=new Stack<Effect>();
        private readonly Dictionary<int,float> healingShownAt=new Dictionary<int,float>();
        private Material material,smokeMaterial;private Texture2D smokeTexture;
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
                Animate(effect,t);
            }
            if(world.Paused)return;
            for(;eventCursor<world.Events.Count;eventCursor++)
            {
                var ev=world.Events[eventCursor];float duration;Vector3 scale;Color color;
                if(ev.kind=="Fire"&&ev.message!="Melee")
                {
                    var source=world.Get(ev.entity);
                    if(source?.def.id=="longbow"||source?.def.id=="native_scout")continue;
                    bool heavy=source!=null&&((source.def.tags&Tags.Artillery)!=0||source.def.waterborne);
                    duration=.34f;scale=Vector3.one*(heavy?.82f:.48f);color=new Color(1,.72f,.22f);
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
                effect.fire=ev.kind=="Fire";effect.healer=ev.kind=="Heal"?ev.entity:0;effect.patient=ev.kind=="Heal"?ev.target:0;
                if(effect.patient!=0)healingShownAt[effect.patient]=ev.time;
                var entity=world.Get(ev.kind=="Impact"||ev.kind=="Heal"?ev.target:ev.entity);
                float height=world.SurfaceHeight(ev.position,entity?.def.waterborne??false);
                effect.origin=new Vector3(ev.position.x,height+(ev.kind=="Fire"?(entity?.def.id=="gunboat"?.83f:1.2f):.6f),ev.position.y);
                // The event stores the facing at fire time; never consult a hidden target.
                Vector3 facing=new Vector3(ev.facing.x,0,ev.facing.y);
                if(effect.fire&&facing.sqrMagnitude>.0001f)
                {
                    facing.Normalize();var muzzle=effect.origin+facing*.35f;
                    if(world.Fog.At(observer,new Vector2(muzzle.x,muzzle.z))==VisibilityState.Visible)effect.origin=muzzle;
                }
                effect.go.transform.SetParent(parent,false);effect.go.transform.position=effect.origin;
                effect.go.transform.rotation=facing.sqrMagnitude>.0001f?Quaternion.LookRotation(facing):Quaternion.identity;
                Animate(effect,Mathf.Clamp01((world.Time-ev.time)/duration));
                effect.tint.SetColor("_BaseColor",color);effect.renderer.SetPropertyBlock(effect.tint);
                effect.go.SetActive(true);active.Add(effect);
            }
        }
        // Muzzle flash expires early, leaving three small, rising powder-smoke
        // puffs. All pieces belong to the same bounded, visibility-gated effect.
        private static void Animate(Effect effect,float t)
        {
            effect.go.transform.localScale=effect.scale*(effect.fire?1-t*.3f:1-t);
            // The core is a separate child so reducing flash size does not shrink
            // the smoke. At 0.34 seconds lifetime it lasts under 0.055 seconds.
            effect.renderer.enabled=!effect.fire||t<.16f;
            effect.renderer.transform.localScale=effect.fire?Vector3.one*(.30f*Mathf.Clamp01(1-t/.16f)):Vector3.one;
            for(int i=0;i<effect.smoke.Length;i++)
            {
                var puff=effect.smoke[i];puff.gameObject.SetActive(effect.fire);
                if(!effect.fire)continue;
                float size=Mathf.Sin(Mathf.PI*Mathf.Clamp01((t+.08f)/(1f+i*.08f)))*(1.40f+i*.15f);
                puff.localScale=new Vector3(size,size*.8f,1);
                var camera=Camera.main;if(camera!=null)puff.rotation=Quaternion.LookRotation(camera.transform.forward,camera.transform.up);
                puff.localPosition=new Vector3((i-1)*.22f,.10f+t*(.32f+i*.12f),.16f+i*.12f+t*.22f);
            }
        }
        private Effect Create()
        {
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Frontier combat feedback",enableInstancing=true};}
            var go=new GameObject("Pooled combat feedback");
            var core=GameObject.CreatePrimitive(PrimitiveType.Sphere);core.name="Flash or impact core";core.transform.SetParent(go.transform,false);
            var collider=core.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=core.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            if(smokeMaterial==null)
            {
                smokeTexture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Soft powder smoke",wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color32[32*32];
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                {
                    float dx=(x-15.5f)/15.5f,dy=(y-15.5f)/15.5f;
                    // Keep the edge transparent while giving the cloud a visible soft
                    // interior; deterministic irregularity avoids a perfect disc.
                    float radial=Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dy*dy));
                    float grain=.88f+.12f*Mathf.PerlinNoise(x*.18f,y*.18f);
                    float alpha=Mathf.Pow(radial,1.25f)*.72f*grain;
                    pixels[y*32+x]=new Color(1,1,1,alpha);
                }
                smokeTexture.SetPixels32(pixels);smokeTexture.Apply(false,true);
                smokeMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Frontier soft powder smoke",renderQueue=3000};
                smokeMaterial.SetTexture("_BaseMap",smokeTexture);smokeMaterial.SetFloat("_Surface",1);
                smokeMaterial.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                smokeMaterial.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                smokeMaterial.SetFloat("_ZWrite",0);smokeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            var effect=new Effect{go=go,renderer=renderer};
            for(int i=0;i<effect.smoke.Length;i++)
            {
                var puff=GameObject.CreatePrimitive(PrimitiveType.Quad);puff.name="Powder smoke";
                puff.transform.SetParent(go.transform,false);
                var smokeCollider=puff.GetComponent<Collider>();smokeCollider.enabled=false;Destroy(smokeCollider);
                var smokeRenderer=puff.GetComponent<Renderer>();smokeRenderer.sharedMaterial=smokeMaterial;
                smokeRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                var tint=new MaterialPropertyBlock();float shade=.76f+i*.035f;
                tint.SetColor("_BaseColor",new Color(shade,shade*.98f,shade*.90f));smokeRenderer.SetPropertyBlock(tint);
                puff.SetActive(false);effect.smoke[i]=puff.transform;
            }
            return effect;
        }
        private void Recycle(Effect effect){effect.go.SetActive(false);pool.Push(effect);}
        public void Dispose()
        {
            foreach(var effect in active)if(effect.go!=null){effect.go.SetActive(false);Destroy(effect.go);}active.Clear();
            while(pool.Count>0){var effect=pool.Pop();if(effect.go!=null)Destroy(effect.go);}
            if(material!=null)Destroy(material);if(smokeMaterial!=null)Destroy(smokeMaterial);if(smokeTexture!=null)Destroy(smokeTexture);material=null;smokeMaterial=null;smokeTexture=null;world=null;eventCursor=0;healingShownAt.Clear();
        }
        private void OnDestroy(){Dispose();}
    }
}
