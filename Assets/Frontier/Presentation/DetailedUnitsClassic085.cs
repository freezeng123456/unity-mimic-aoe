using UnityEngine;

namespace Frontier
{
    // Original silhouettes for the ordinary RTS camera. Authoring happens before
    // the existing joint-aware bake; source rig transforms and rule data stay intact.
    public static partial class DetailedUnits
    {
        static readonly Color Buff085=new Color(.67f,.48f,.25f),Wood085=new Color(.65f,.43f,.20f);
        static readonly Color Silver085=new Color(.68f,.72f,.71f),Bronze085=new Color(.65f,.43f,.20f);

        static void ClassicReadability085(Transform root,UnitRig rig,string id,Color team)
        {
            switch(id)
            {
                case "longbow":Archer085(root,rig,team);break;
                case "pike":Pikeman085(root,rig,team);break;
                case "carabineer":LightRider085(root,rig,team);break;
                case "cuirassier":HeavyRider085(root,rig,team);break;
                case "janissary":OttomanInfantry085(root,rig,team,true);break;
                case "abus":OttomanInfantry085(root,rig,team,false);break;
            }
        }

        static void Archer085(Transform root,UnitRig rig,Color team)
        {
            Tint085(root.Find("Hat brim"),Leather);Tint085(root.Find("Hat crown"),Leather);
            Replace(root.Find("Hat brim"),s=>s.Loft(new[]{-.022f,.022f},new[]{.29f,.29f},new[]{.25f,.25f},16,Color.white));
            Sleeves085(rig,Linen);
            var kit=new Surface();
            // A broad back mantle and a full-height quiver remain legible above the waist.
            kit.Plate(new[]{new Vector3(-.34f,1.28f,-.225f),new Vector3(.34f,1.28f,-.225f),new Vector3(.27f,.98f,-.265f),new Vector3(-.27f,.98f,-.265f)},.04f,Color.Lerp(team,Dark,.22f));
            kit.Ellipsoid(new Vector3(.245f,1.12f,-.35f),new Vector3(.115f,.30f,.105f),Leather,10,6);
            kit.Box(new Vector3(.245f,1.39f,-.35f),new Vector3(.235f,.09f,.22f),Buff085);
            for(int i=0;i<3;i++)
            {
                float x=.17f+i*.07f;
                kit.Beam(new Vector3(x,1.35f,-.35f),new Vector3(x+.025f,1.66f,-.36f),.022f,.022f,Wood085);
                kit.Box(new Vector3(x+.02f,1.60f,-.36f),new Vector3(.055f,.10f,.035f),Linen);
            }
            kit.Finish(root,"085 archer mantle and full quiver");
            Tint085(rig.Weapon.Find("Curved bow stave"),Wood085);
            Replace(rig.Weapon.Find("Curved bow stave"),s=>
            {
                // Exact original tips/curve and both animated string transforms survive.
                var p=new[]{new Vector3(0,-.88f,-.24f),new Vector3(0,-.65f,-.11f),new Vector3(0,-.36f,-.02f),Vector3.zero,new Vector3(0,.36f,-.02f),new Vector3(0,.65f,-.11f),new Vector3(0,.88f,-.24f)};
                for(int i=0;i<p.Length-1;i++)s.Beam(p[i],p[i+1],i==0||i==5?.055f:.085f,.060f,Color.white);
            });
            var grip=new Surface();grip.Band(Vector3.zero,.056f,.049f,.22f,Leather);grip.Finish(rig.Weapon,"085 longbow leather hand grip");
        }

        static void Pikeman085(Transform root,UnitRig rig,Color team)
        {
            // Low steel morion rather than the archer's felt cap.
            Tint085(root.Find("Hat brim"),Silver085);Tint085(root.Find("Hat crown"),Steel);
            Replace(root.Find("Hat brim"),s=>s.Loft(new[]{-.022f,.022f},new[]{.30f,.30f},new[]{.25f,.25f},16,Color.white));
            Replace(root.Find("Hat crown"),s=>s.Loft(new[]{-.075f,-.015f,.065f,.13f},new[]{.195f,.19f,.14f,.035f},new[]{.185f,.19f,.15f,.045f},16,Color.white));
            Hide085(root.Find("Hat team ribbon"));
            var armor=new Surface();
            armor.Loft(new[]{.83f,.96f,1.18f,1.31f},new[]{.235f,.285f,.337f,.285f},new[]{.24f,.285f,.29f,.225f},16,Steel);
            armor.Band(new Vector3(0,.86f,0),.25f,.255f,.085f,Leather);
            armor.Beam(new Vector3(0,1.78f,-.12f),new Vector3(0,1.96f,.06f),.065f,.09f,Silver085);
            armor.Finish(root,"085 pikeman morion and steel corselet");
            Sleeves085(rig,team);
            foreach(var shoulder in new[]{rig.LeftShoulder,rig.RightShoulder})
            {
                var plate=new Surface();plate.Ellipsoid(new Vector3(0,-.035f,0),new Vector3(.15f,.11f,.14f),Steel,10,5);plate.Finish(shoulder,"085 pikeman shoulder plate");
            }
            Tint085(rig.Weapon,Wood085);Tint085(rig.WeaponPivot.Find("Pike steel head"),Silver085);
            Replace(rig.WeaponPivot.Find("Pike steel head"),s=>s.Plate(new[]{new Vector3(-.10f,2.37f,-.03f),new Vector3(0,2.8f,-.03f),new Vector3(.10f,2.37f,-.03f)},.06f,Color.white));
            var socket=new Surface();socket.Beam(new Vector3(0,1.97f,0),new Vector3(0,2.40f,0),.085f,.085f,Steel);socket.Finish(rig.WeaponPivot,"085 pike steel socket");
        }

        static void LightRider085(Transform root,UnitRig rig,Color team)
        {
            float y=rig.RiderSeatHeight;
            Tint085(root.Find("Tailored coat"),Buff085);Tint085(root.Find("Hat brim"),Leather);Tint085(root.Find("Hat crown"),Leather);
            Replace(root.Find("Hat brim"),s=>s.Loft(new[]{-.022f,.022f},new[]{.365f,.365f},new[]{.30f,.30f},18,Color.white));
            Replace(root.Find("Hat crown"),s=>s.Loft(new[]{-.075f,.075f},new[]{.19f,.16f},new[]{.18f,.15f},16,Color.white));
            Sleeves085(rig,team);
            var jerkin=new Surface();
            jerkin.Loft(new[]{.91f+y,1.03f+y,1.22f+y,1.30f+y},new[]{.25f,.29f,.33f,.28f},new[]{.25f,.28f,.28f,.23f},16,Buff085);
            jerkin.Band(new Vector3(0,.91f+y,0),.268f,.27f,.10f,team);
            jerkin.Beam(new Vector3(-.20f,1.28f+y,-.292f),new Vector3(.20f,.97f+y,-.292f),.065f,.025f,Leather);
            jerkin.Finish(root,"085 Ruyter buff jerkin and team sash");
            SaddleCloth085(root,team,false);
            // A compact two-handed carbine: both hand/weapon pivots stay unchanged.
            var barrel=rig.Weapon.Find("Gun barrel");Tint085(barrel,Silver085);
            // Actual Ruyter already uses the original .78m short-carbine barrel.
            Tint085(rig.Weapon.Find("Wooden stock"),Buff085);
            Hide085(rig.Weapon.Find("Weapon fittings"));
            var gun=new Surface();gun.Box(new Vector3(0,.015f,-.33f),new Vector3(.17f,.22f,.19f),Leather);
            gun.Box(new Vector3(.102f,.08f,.065f),new Vector3(.065f,.16f,.21f),Brass);
            foreach(float z in new[]{.22f,.64f})gun.Box(new Vector3(0,.06f,z),new Vector3(.09f,.11f,.04f),Brass);
            gun.Box(new Vector3(0,.12f,.70f),new Vector3(.035f,.04f,.05f),Steel);
            gun.Finish(rig.Weapon,"085 Ruyter carbine breech and flared butt");
        }

        static void HeavyRider085(Transform root,UnitRig rig,Color team)
        {
            float y=rig.RiderSeatHeight;Sleeves085(rig,team);
            foreach(string name in new[]{"French steel breastplate","French steel backplate"})
            {
                Tint085(root.Find(name),Silver085);
                Replace(root.Find(name),s=>s.Loft(new[]{-.26f,-.16f,.04f,.19f,.26f},new[]{.195f,.245f,.315f,.315f,.27f},new[]{.060f,.10f,.125f,.10f,.065f},16,Color.white));
            }
            Tint085(root.Find("Steel cavalry helmet"),Silver085);
            var harness=new Surface();
            harness.Beam(new Vector3(-.29f,1.30f+y,-.27f),new Vector3(-.19f,.82f+y,-.29f),.055f,.025f,Brass);
            harness.Beam(new Vector3(.29f,1.30f+y,-.27f),new Vector3(.19f,.82f+y,-.29f),.055f,.025f,Brass);
            harness.Beam(new Vector3(0,2.14f+y,-.12f),new Vector3(0,1.78f+y,-.37f),.18f,.19f,Dark);
            harness.Beam(new Vector3(-.32f,1.11f,.55f),new Vector3(.32f,1.11f,.55f),.12f,.075f,Leather);
            harness.Ellipsoid(new Vector3(0,1.105f,.60f),new Vector3(.105f,.105f,.045f),Brass,10,5);
            harness.Finish(root,"085 cuirassier heavy harness and helmet crest");
            SaddleCloth085(root,team,true);
            Tint085(rig.Weapon.Find("Straight steel blade"),Silver085);
            Replace(rig.Weapon.Find("Straight steel blade"),s=>s.Plate(new[]{new Vector3(-.073f,-.59f,-.025f),new Vector3(-.063f,.43f,-.025f),new Vector3(0,.59f,-.025f),new Vector3(.063f,.43f,-.025f),new Vector3(.073f,-.59f,-.025f)},.05f,Color.white));
        }

        static void OttomanInfantry085(Transform root,UnitRig rig,Color team,bool janissary)
        {
            Sleeves085(rig,team);
            var coat=new Surface();
            foreach(float side in new[]{-1f,1f})
            {
                coat.Plate(new[]{new Vector3(side*.08f,1.30f,.27f),new Vector3(side*.245f,1.28f,.27f),new Vector3(side*.21f,.64f,.245f),new Vector3(side*.07f,.64f,.245f)},.035f,team);
                coat.Plate(new[]{new Vector3(side*.10f,1.28f,-.25f),new Vector3(side*.29f,1.25f,-.25f),new Vector3(side*.22f,.64f,-.245f),new Vector3(side*.07f,.64f,-.245f)},.035f,team);
            }
            coat.Band(new Vector3(0,.88f,0),.30f,.285f,.13f,team);
            coat.Finish(root,janissary?"085 Janissary team kaftan panels":"085 Abus team kaftan panels");
            if(janissary)
            {
                Replace(root.Find("Janissary tall felt cap"),s=>s.Loft(new[]{-.235f,-.10f,.08f,.235f},new[]{.18f,.21f,.235f,.225f},new[]{.17f,.19f,.145f,.105f},16,Color.white));
                // Keep the characteristic high felt cap and long rear flap, not a turban.
                Replace(root.Find("Janissary hanging cap cloth"),s=>s.Plate(new[]{new Vector3(-.40f,-.5f,-.5f),new Vector3(.40f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f)},1f,Color.white));
                Tint085(rig.Weapon.Find("Gun barrel"),Silver085);
                // Scimitar stays inside its existing independently hidden melee group.
                Tint085(rig.MeleeWeapon.Find("Curved scimitar blade"),Silver085);
            }
            else
            {
                Replace(root.Find("Ottoman wrapped turban"),s=>s.Ellipsoid(Vector3.zero,new Vector3(.33f,.15f,.28f),Color.white,16,8));
                var tube=rig.Weapon.Find("Heavy hand cannon barrel");Tint085(tube,Bronze085);
                Replace(tube,s=>s.HollowBarrel(1.07f,.27f,16));
                Tint085(rig.Weapon.Find("Reinforced hand cannon muzzle"),Bronze085);
                Replace(rig.Weapon.Find("Reinforced hand cannon muzzle"),s=>s.HollowBarrel(.15f,.33f,16));
                // The bronze helper also colours its recessed end cap bronze;
                // add a genuinely recessed soot-dark throat inside the existing bore.
                var bore=new Surface();Vector3 P(int i,float radius,float z){float a=i*Mathf.PI/8;return new Vector3(Mathf.Cos(a)*radius,.105f+Mathf.Sin(a)*radius,z);}
                var end=new Vector3[16];
                for(int i=0;i<16;i++)
                {
                    bore.Plate(new[]{P(i,.108f,.947f),P(i+1,.108f,.947f),P(i+1,.105f,.918f),P(i,.105f,.918f)},.001f,Dark);
                    end[i]=P(i,.105f,.917f);
                }
                bore.Plate(end,.001f,Dark);bore.Finish(rig.Weapon,"085 Abus recessed dark bore");
                Tint085(rig.Weapon.Find("Hand cannon stock"),Leather);
                var gun=new Surface();gun.Box(new Vector3(0,-.035f,-.31f),new Vector3(.25f,.23f,.43f),Leather);
                gun.Box(new Vector3(0,-.045f,.29f),new Vector3(.22f,.09f,.56f),Buff085);
                gun.Finish(rig.Weapon,"085 Abus broad shoulder stock and barrel bed");
            }
        }

        static void SaddleCloth085(Transform root,Color team,bool heavy)
        {
            // Paired cloth panels stay on the static saddle, away from animated legs.
            foreach(float side in new[]{-1f,1f})
            {
                var panel=new Surface();
                Vector3[] edge=heavy?new[]{new Vector3(-.43f,1.31f,0),new Vector3(.43f,1.31f,0),new Vector3(.40f,1.01f,0),new Vector3(-.28f,.91f,0),new Vector3(-.43f,1.02f,0)}:new[]{new Vector3(-.31f,1.31f,0),new Vector3(.32f,1.31f,0),new Vector3(.23f,1.13f,0),new Vector3(-.16f,1.06f,0)};
                panel.Plate(edge,.035f,team);
                for(int i=0;i<edge.Length;i++)panel.Beam(edge[i]+Vector3.forward*.04f,edge[(i+1)%edge.Length]+Vector3.forward*.04f,heavy?.040f:.025f,.025f,heavy?Linen:Buff085);
                string name=side<0?"085 left saddle cloth":"085 right saddle cloth";panel.Finish(root,name);
                var t=root.Find(name);t.localPosition=new Vector3(side*.4775f-.0175f,0,-.03f);t.localRotation=Quaternion.Euler(0,90,0);
            }
            Tint085(root.Find("Team saddle blanket"),team);
        }

        static void Sleeves085(UnitRig rig,Color color)
        {
            foreach(var shoulder in new[]{rig.LeftShoulder,rig.RightShoulder})Tint085(shoulder.Find("Coat sleeve"),color);
            foreach(var elbow in new[]{rig.LeftElbow,rig.RightElbow})Tint085(elbow.Find("Forearm"),color);
        }
        static void Tint085(Transform part,Color color)
        {
            // Cached colour materials are assigned, never mutated; atlas-owned authoring
            // buffers alone are disposable, so named source nodes survive the bake.
            if(part!=null)part.GetComponent<MeshRenderer>().sharedMaterial=ProceduralArt.Mat(color);
        }
        static void Hide085(Transform part){if(part!=null)part.GetComponent<MeshRenderer>().enabled=false;}
    }
}
