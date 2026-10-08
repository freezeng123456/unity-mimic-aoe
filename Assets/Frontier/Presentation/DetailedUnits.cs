using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier
{
    // Original model refinement. Geometry stays on the gameplay rig; each independently
    // animated or hidden group is baked separately, never across a joint/tool boundary.
    public static class DetailedUnits
    {
        static readonly Color Skin=new Color(.77f,.59f,.40f),Leather=new Color(.18f,.105f,.057f),Dark=new Color(.055f,.048f,.040f);
        static readonly Color Linen=new Color(.84f,.79f,.64f),Brass=new Color(.64f,.47f,.23f),Steel=new Color(.43f,.48f,.49f);
        static readonly Dictionary<Color,int> palette=new Dictionary<Color,int>();
        static Texture2D atlas;static Material material;

        // Merge authored industrial geometry without applying the ordinary
        // cannon silhouette to rocket rails or the larger bronze bombard.
        public static void BakeAuthored(Transform root,UnitRig rig)=>Bake(root,rig);

        public static void Refine(Transform root,UnitRig rig,EntityState e,Color team)
        {
            if(e.def.id=="envoy"||e.def.id=="native_scout")ScoutingFigure(root,rig,e,team);
            else if(e.def.id=="caravan")Freight(root,rig);
            else if(rig.Barrel!=null)Gun(root,rig,e.def.id);
            else Figure(root,rig,e,team);
            Bake(root,rig);
        }

        static void ScoutingFigure(Transform root,UnitRig rig,EntityState e,Color team)
        {
            // Do not inherit the infantry crossbelt, ammunition pouch or coat cut.
            bool envoy=e.def.id=="envoy";var torso=rig.ScoutBody;
            Replace(torso.Find("Head"),s=>s.Loft(new[]{-.17f,-.105f,.025f,.105f,.17f},new[]{.105f,.149f,.171f,.163f,.13f},new[]{.12f,.16f,.168f,.15f,.12f},16,Color.white));
            Replace(torso.Find(envoy?"Envoy narrow long coat":"Scout short tunic"),s=>s.Loft(new[]{-.29f,-.15f,.04f,.20f,.29f},new[]{.20f,.205f,.25f,.27f,.25f},new[]{.14f,.16f,.18f,.185f,.17f},16,Color.white));
            var detail=new Surface();
            detail.Ellipsoid(new Vector3(0,1.50f,.211f),new Vector3(.042f,.064f,.060f),Skin,10,6);
            foreach(float side in new[]{-1f,1f})
            {
                detail.Ellipsoid(new Vector3(side*.075f,1.565f,.181f),new Vector3(.016f,.014f,.010f),Dark,8,4);
                detail.Ellipsoid(new Vector3(side*.164f,1.54f,.008f),new Vector3(.034f,.059f,.033f),Skin,8,5);
                if(envoy)
                {
                    detail.Plate(new[]{new Vector3(side*.045f,1.36f,.191f),new Vector3(side*.21f,1.36f,.16f),new Vector3(side*.115f,1.13f,.205f),new Vector3(side*.037f,1.07f,.205f)},.024f,Linen);
                    detail.Beam(new Vector3(side*.095f,.91f,-.185f),new Vector3(side*.10f,.39f,-.20f),.012f,.017f,team);
                }
            }
            if(envoy)
            {
                for(int i=0;i<4;i++)detail.Ellipsoid(new Vector3(0,1.19f-i*.075f,.213f),new Vector3(.019f,.019f,.013f),Brass,8,4);
                detail.Box(new Vector3(.33f,.935f,.113f),new Vector3(.045f,.048f,.023f),Brass);
                detail.Beam(new Vector3(.255f,1.09f,.045f),new Vector3(.425f,1.09f,.045f),.009f,.009f,Leather);
            }
            else
            {
                detail.Beam(new Vector3(-.31f,.94f,-.283f),new Vector3(.265f,1.065f,-.283f),.025f,.018f,team);
                detail.Box(new Vector3(-.205f,.94f,.22f),new Vector3(.078f,.064f,.035f),Brass);
            }
            detail.Finish(torso,envoy?"Envoy folio and tailored facings":"Scout mantle edging and strap fastening");
            foreach(var shoulder in new[]{rig.LeftShoulder,rig.RightShoulder})
                Replace(shoulder.Find("Coat sleeve"),s=>s.Loft(new[]{-.18f,-.10f,.04f,.13f,.18f},new[]{.077f,.092f,.108f,.102f,.073f},new[]{.085f,.097f,.105f,.10f,.075f},14,Color.white));
            foreach(var hand in new[]{rig.LeftHand,rig.RightHand})
                Replace(hand.Find("Empty hand"),s=>s.Ellipsoid(Vector3.zero,new Vector3(.083f,.091f,.079f),Color.white,14,8));
            foreach(var elbow in new[]{rig.LeftElbow,rig.RightElbow})
            {
                var cuff=new Surface();cuff.Band(new Vector3(0,-.273f,0),.084f,.091f,.065f,envoy?Linen:team);cuff.Finish(elbow,"Scouting sleeve cuff");
            }
            if(!envoy)
            {
                var grip=new Surface();grip.Band(Vector3.zero,.046f,.042f,.16f,Leather);grip.Finish(rig.Weapon,"Scout bow leather grip");
            }
        }

        static void Figure(Transform root,UnitRig rig,EntityState e,Color team)
        {
            bool mounted=rig.LeftHorseLeg!=null,imported=e.def.id=="musketeer";
            bool kaftan=e.def.id=="janissary"||e.def.id=="abus"||e.def.id=="spahi";
            float y=mounted?rig.RiderSeatHeight:0;
            var body=new Surface();
            if(!imported)
            {
                // A brow, cheeks and jaw are part of the actual head silhouette.
                Replace(root.Find("Head"),s=>s.Loft(new[]{-.17f,-.105f,.025f,.105f,.17f},new[]{.105f,.149f,.171f,.163f,.13f},new[]{.12f,.16f,.168f,.15f,.12f},16,Color.white));
                Replace(root.Find("Tailored coat"),s=>s.Loft(new[]{-.275f,-.16f,.04f,.19f,.275f},new[]{.20f,.23f,.30f,.325f,.325f},new[]{.15f,.164f,.199f,.21f,.21f},16,Color.white));
                Replace(root.Find("Coat skirts"),s=>s.Loft(new[]{-.16f,-.05f,.09f,.16f},new[]{.31f,.286f,.23f,.20f},new[]{.20f,.21f,.17f,.15f},16,Color.white));
                bool broad=e.Worker||e.def.revolutionary||e.def.id=="explorer"||e.def.id=="skirmisher"||e.def.id=="longbow";
                Replace(root.Find("Hat brim"),s=>s.HatBrim(broad,e.Worker));
                Replace(root.Find("Steel cavalry helmet"),s=>s.Loft(new[]{-.14f,-.07f,.05f,.13f,.16f},new[]{.23f,.225f,.20f,.145f,.07f},new[]{.215f,.219f,.192f,.145f,.085f},18,Color.white));
                Replace(root.Find("Janissary tall felt cap"),s=>s.Loft(new[]{-.235f,-.12f,.12f,.235f},new[]{.195f,.20f,.18f,.17f},new[]{.18f,.19f,.16f,.14f},16,Color.white));
                Replace(root.Find("Ottoman wrapped turban"),s=>s.Ellipsoid(Vector3.zero,new Vector3(.30f,.143f,.27f),Color.white,18,10));
                body.Ellipsoid(new Vector3(0,1.47f+y,.207f),new Vector3(.045f,.072f,.065f),Skin,10,6);
                body.Ellipsoid(new Vector3(0,1.40f+y,.143f),new Vector3(.072f,.035f,.035f),Skin*.88f,10,5);
                foreach(float side in new[]{-1f,1f})
                {
                    body.Ellipsoid(new Vector3(side*.081f,1.548f+y,.163f),new Vector3(.042f,.017f,.018f),Linen,8,4);
                    body.Ellipsoid(new Vector3(side*.075f,1.548f+y,.179f),new Vector3(.016f,.016f,.009f),Dark,8,4);
                    body.Beam(new Vector3(side*.040f,1.578f+y,.162f),new Vector3(side*.116f,1.573f+y,.141f),.018f,.017f,Leather);
                    body.Ellipsoid(new Vector3(side*.165f,1.511f+y,.004f),new Vector3(.038f,.066f,.039f),Skin,8,5);
                    body.Ellipsoid(new Vector3(side*.181f,1.51f+y,.022f),new Vector3(.010f,.029f,.014f),Skin*.72f,8,4);
                    // European turned lapels and Ottoman upright kaftan facings have
                    // distinct cuts rather than sharing one national costume.
                    if(kaftan)
                    {
                        body.Plate(new[]{new Vector3(side*.047f,1.30f+y,.223f),new Vector3(side*.10f,1.29f+y,.222f),new Vector3(side*.10f,.956f+y,.225f),new Vector3(side*.047f,.956f+y,.225f)},.023f,Brass);
                        body.Beam(new Vector3(side*.112f,1.28f+y,.231f),new Vector3(side*.112f,.961f+y,.238f),.012f,.015f,Leather);
                    }
                    else
                    {
                        body.Plate(new[]{new Vector3(side*.065f,1.29f+y,.212f),new Vector3(side*.25f,1.31f+y,.17f),new Vector3(side*.17f,1.08f+y,.215f),new Vector3(side*.055f,1.00f+y,.216f)},.026f,e.Worker?Leather:Linen);
                        body.Beam(new Vector3(side*.24f,1.31f+y,.195f),new Vector3(side*.14f,1.08f+y,.235f),.014f,.013f,Brass);
                    }
                }
                body.Box(new Vector3(0,1.318f+y,.11f),new Vector3(.24f,.11f,.20f),Linen);
                body.Beam(new Vector3(-.115f,1.31f+y,.225f),new Vector3(.115f,1.31f+y,.225f),.026f,.026f,team);
                // Rear seam, split coat tails and two rows of buttons read at medium zoom.
                body.Beam(new Vector3(0,.94f+y,-.166f),new Vector3(0,1.27f+y,-.216f),.013f,.015f,Leather);
                foreach(float x in new[]{-.079f,.079f})for(int i=0;i<4;i++)body.Ellipsoid(new Vector3(x,.971f+y+i*.073f,.239f),new Vector3(.021f,.021f,.013f),Brass,8,4);
                body.Beam(new Vector3(0,.575f+y,.202f),new Vector3(0,.747f+y,.18f),.018f,.016f,Leather);
                body.Box(new Vector3(0,.879f+y,.231f),new Vector3(.14f,.10f,.028f),Brass);
                body.Box(new Vector3(0,.879f+y,.25f),new Vector3(.080f,.052f,.014f),Leather);
            }
            // Crossbelt wraps over the shoulder, not a flat badge floating on the chest.
            if(!e.Worker&&e.def.id!="spahi"&&e.def.id!="cuirassier")
            {
                body.Beam(new Vector3(-.24f,1.28f+y,.224f),new Vector3(.22f,.91f+y,.225f),.058f,.026f,Linen);
                body.Beam(new Vector3(-.24f,1.28f+y,-.212f),new Vector3(.22f,.91f+y,-.217f),.058f,.026f,Linen);
                body.Box(new Vector3(.34f,.89f+y,-.12f),new Vector3(.22f,.23f,.18f),Leather);
                body.Box(new Vector3(.34f,.94f+y,-.024f),new Vector3(.235f,.105f,.025f),Leather*.72f);
                body.Box(new Vector3(.34f,.922f+y,-.005f),new Vector3(.037f,.039f,.013f),Brass);
            }
            if(e.def.id=="hussar"||e.def.id=="outrider")for(int row=0;row<5;row++)
            {
                float height=.98f+y+row*.054f;
                body.Beam(new Vector3(-.19f,height,.234f),new Vector3(.19f,height,.234f),.013f,.021f,Brass);
            }
            if(e.def.id=="cuirassier")foreach(float side in new[]{-1f,1f})
            {
                for(int i=0;i<5;i++)body.Ellipsoid(new Vector3(side*(.23f-.015f*i),1.24f+y-i*.085f,.31f),new Vector3(.018f,.018f,.014f),Brass,8,4);
                body.Beam(new Vector3(side*.23f,1.285f+y,.31f),new Vector3(side*.15f,.85f+y,.29f),.013f,.018f,Brass);
            }
            body.Finish(root,"Tailoring and facial anatomy");
            if(!imported)
            {
                // Smooth sleeve/boot profiles use the existing joints and local
                // extents, preserving animation reach and the hand-grip contract.
                foreach(var shoulder in new[]{rig.LeftShoulder,rig.RightShoulder})
                    Replace(shoulder?.Find("Coat sleeve"),s=>s.Loft(new[]{-.18f,-.13f,-.02f,.10f,.17f,.18f},new[]{.076f,.085f,.10f,.123f,.112f,.075f},new[]{.083f,.09f,.098f,.11f,.106f,.072f},16,Color.white));
                foreach(var elbow in new[]{rig.LeftElbow,rig.RightElbow})
                    Replace(elbow?.Find("Forearm"),s=>s.Loft(new[]{-.17f,-.12f,.025f,.125f,.17f},new[]{.067f,.073f,.091f,.094f,.082f},new[]{.075f,.080f,.094f,.096f,.087f},16,Color.white));
                foreach(var hand in new[]{rig.LeftHand,rig.RightHand})
                    Replace(hand?.Find("Hand"),s=>s.Ellipsoid(Vector3.zero,new Vector3(.082f,.091f,.078f),Color.white,14,8));
                foreach(var leg in new[]{rig.LeftLeg,rig.RightLeg})
                    Replace(leg?.Find("Breeches"),s=>s.Loft(new[]{-.17f,-.10f,.02f,.12f,.17f},new[]{.088f,.099f,.12f,.126f,.112f},new[]{.10f,.114f,.129f,.124f,.112f},16,Color.white));
                foreach(var knee in new[]{rig.LeftKnee,rig.RightKnee})
                {
                    Replace(knee?.Find("Boot shaft"),s=>s.Loft(new[]{-.15f,-.09f,.015f,.11f,.15f},new[]{.087f,.086f,.099f,.112f,.107f},new[]{.097f,.096f,.108f,.114f,.108f},16,Color.white));
                    Replace(knee?.Find("Boot toe"),s=>s.Loft(new[]{-.07f,-.027f,.031f,.065f,.07f},new[]{.096f,.12f,.112f,.084f,.068f},new[]{.16f,.175f,.163f,.126f,.09f},16,Color.white));
                }
            }
            foreach(var elbow in new[]{rig.LeftElbow,rig.RightElbow})
            {
                if(elbow==null)continue;var detail=new Surface();
                detail.Band(new Vector3(0,-.271f,.008f),.089f,.095f,.095f,e.Worker?Leather:team);
                detail.Beam(new Vector3(-.081f,-.235f,.098f),new Vector3(.081f,-.235f,.098f),.014f,.014f,Linen);
                for(int i=0;i<2;i++)detail.Ellipsoid(new Vector3(.091f,-.248f-i*.043f,.048f),new Vector3(.014f,.014f,.015f),Brass,8,4);
                detail.Finish(elbow,"Turned cuff and cuff fasteners");
            }
            foreach(var knee in new[]{rig.LeftKnee,rig.RightKnee})
            {
                if(knee==null)continue;var detail=new Surface();
                detail.Band(new Vector3(0,-.318f,.057f),.116f,.158f,.037f,Dark);
                detail.Beam(new Vector3(0,-.026f,-.115f),new Vector3(0,-.26f,-.11f),.013f,.015f,Leather*.55f);
                detail.Box(new Vector3(0,-.041f,.12f),new Vector3(.207f,.061f,.028f),Leather*.66f);
                if(mounted){detail.Box(new Vector3(.105f,-.26f,-.07f),new Vector3(.05f,.052f,.07f),Steel);detail.Beam(new Vector3(.11f,-.26f,-.08f),new Vector3(.11f,-.26f,-.17f),.025f,.025f,Steel);}
                detail.Finish(knee,"Boot sole seam and spur");
            }
            if(mounted)Mount(root,rig,team);
            Weapons(rig,e.def.id);
        }

        static void Mount(Transform root,UnitRig rig,Color team)
        {
            Replace(root.Find("Horse torso"),s=>s.Ellipsoid(Vector3.zero,new Vector3(.39f,.31f,.71f),Color.white,16,10));
            Replace(root.Find("Horse neck"),s=>s.Loft(new[]{-.35f,-.23f,.04f,.23f,.35f},new[]{.23f,.24f,.19f,.145f,.12f},new[]{.25f,.27f,.22f,.18f,.16f},14,Color.white));
            Replace(rig.HorseHead.Find("Horse skull"),s=>s.Ellipsoid(new Vector3(0,0,.015f),new Vector3(.165f,.23f,.21f),Color.white,14,9));
            Replace(rig.HorseHead.Find("Horse muzzle"),s=>s.Ellipsoid(Vector3.zero,new Vector3(.148f,.133f,.229f),Color.white,14,8));
            var hide=new Color(.40f,.26f,.16f);var body=new Surface();
            foreach(float side in new[]{-1f,1f})
            {
                body.Ellipsoid(new Vector3(side*.27f,.93f,-.42f),new Vector3(.185f,.285f,.27f),hide,12,8);
                body.Ellipsoid(new Vector3(side*.255f,.91f,.42f),new Vector3(.165f,.28f,.23f),hide,12,8);
                body.Beam(new Vector3(side*.38f,1.10f,.23f),new Vector3(side*.31f,.83f,.33f),.064f,.034f,Leather);
                body.Beam(new Vector3(side*.31f,.83f,.33f),new Vector3(0,.727f,.34f),.064f,.034f,Leather);
                body.Box(new Vector3(side*.493f,1.29f,-.045f),new Vector3(.025f,.13f,.68f),Linen);
                body.Beam(new Vector3(side*.235f,1.47f,.13f),new Vector3(side*.52f,1.06f,.23f),.035f,.031f,Leather);
                body.Beam(new Vector3(side*.52f,1.06f,.23f),new Vector3(side*.52f,.90f,.23f),.025f,.022f,Steel);
                body.Beam(new Vector3(side*.52f,.90f,.23f),new Vector3(side*.52f,.90f,.38f),.024f,.024f,Steel);
                body.Beam(new Vector3(side*.52f,.90f,.38f),new Vector3(side*.52f,1.04f,.34f),.024f,.024f,Steel);
                body.Box(new Vector3(side*.413f,1.115f,-.39f),new Vector3(.16f,.33f,.32f),Leather);
                body.Box(new Vector3(side*.508f,1.17f,-.39f),new Vector3(.04f,.19f,.34f),Leather*.78f);
                body.Box(new Vector3(side*.535f,1.12f,-.39f),new Vector3(.025f,.045f,.040f),Brass);
            }
            body.Beam(new Vector3(-.25f,1.45f,-.29f),new Vector3(.25f,1.45f,-.29f),.13f,.10f,Leather);
            body.Beam(new Vector3(-.24f,1.46f,.22f),new Vector3(.24f,1.46f,.22f),.095f,.085f,Leather);
            body.Finish(root,"Horse shoulder haunch and saddle tack");
            var face=new Surface();
            foreach(float side in new[]{-1f,1f})
            {
                face.Ellipsoid(new Vector3(side*.155f,.047f,.124f),new Vector3(.018f,.033f,.040f),Dark,10,5);
                face.Ellipsoid(new Vector3(side*.127f,-.136f,.429f),new Vector3(.020f,.038f,.025f),Dark,8,5);
                face.Beam(new Vector3(side*.173f,.17f,-.01f),new Vector3(side*.159f,-.09f,.335f),.032f,.026f,Leather);
                face.Beam(new Vector3(side*.154f,-.09f,.335f),new Vector3(side*.157f,-.185f,.268f),.023f,.023f,Brass);
                face.Ellipsoid(new Vector3(side*.158f,-.17f,.30f),new Vector3(.028f,.039f,.029f),Brass,8,5);
            }
            face.Beam(new Vector3(-.158f,.167f,.001f),new Vector3(.158f,.167f,.001f),.042f,.030f,Leather);
            face.Beam(new Vector3(0,.19f,.135f),new Vector3(0,-.023f,.205f),.061f,.024f,Linen*.81f);
            face.Finish(rig.HorseHead,"Horse eyes nostrils and headstall");
            foreach(var leg in new[]{rig.LeftHorseLeg,rig.RightHorseLeg,rig.RearLeftHorseLeg,rig.RearRightHorseLeg})
            {
                var detail=new Surface();detail.Ellipsoid(new Vector3(0,-.40f,-.007f),new Vector3(.087f,.082f,.10f),hide*.91f,10,6);
                detail.Ellipsoid(new Vector3(0,-.686f,.007f),new Vector3(.09f,.092f,.10f),hide*.83f,10,6);
                detail.Box(new Vector3(0,-.855f,.031f),new Vector3(.177f,.025f,.203f),Steel*.48f);
                detail.Finish(leg,"Knee fetlock and horseshoe");
            }
        }

        static void Weapons(UnitRig rig,string id)
        {
            if(rig.Weapon==null)return;
            var detail=new Surface();
            if(rig.Weapon.Find("Gun barrel")!=null)
            {
                float length=id=="carabineer"?.78f:id=="convoy_gunner"?.62f:id=="dragoon"?1.12f:.96f;
                foreach(float z in new[]{.22f,length*.69f})detail.Box(new Vector3(0,.060f,z),new Vector3(.09f,.11f,.033f),Brass);
                detail.Beam(new Vector3(0,-.065f,-.19f),new Vector3(0,-.065f,length*.82f),.021f,.021f,Steel);
                detail.Beam(new Vector3(.080f,.07f,.04f),new Vector3(.105f,.174f,.035f),.026f,.022f,Steel);
                detail.Box(new Vector3(.107f,.179f,.060f),new Vector3(.037f,.026f,.077f),Steel);
                detail.Box(new Vector3(0,.126f,length*.85f),new Vector3(.028f,.032f,.035f),Steel);
                detail.Box(new Vector3(0,0,-.442f),new Vector3(.153f,.181f,.03f),Brass);
                detail.Beam(new Vector3(-.078f,-.085f,-.07f),new Vector3(-.11f,-.157f,.015f),.018f,.018f,Brass);
                detail.Beam(new Vector3(-.11f,-.157f,.015f),new Vector3(-.078f,-.085f,.118f),.018f,.018f,Brass);
            }
            if(id=="cuirassier"||id=="hussar"||id=="outrider"||id=="spahi")
            {
                detail.Beam(new Vector3(.13f,.012f,0),new Vector3(.16f,-.25f,0),.034f,.033f,Brass);
                detail.Beam(new Vector3(.16f,-.25f,0),new Vector3(0,-.29f,0),.034f,.032f,Brass);
                for(int i=0;i<4;i++)detail.Box(new Vector3(0,-.078f-i*.035f,.041f),new Vector3(.079f,.014f,.012f),Brass);
                detail.Ellipsoid(new Vector3(0,-.29f,0),new Vector3(.065f,.039f,.05f),Brass,8,5);
            }
            if(id=="abus")for(int i=0;i<3;i++)detail.Box(new Vector3(0,.248f,.05f+i*.30f),new Vector3(.075f,.025f,.065f),Brass);
            detail.Finish(rig.Weapon,"Weapon fittings");
        }

        static void Gun(Transform root,UnitRig rig,string id)
        {
            bool mortar=id=="mortar",counter=id=="culverin";
            float length=mortar?.82f:counter?3.3f:2.05f,diameter=mortar?.70f:counter?.29f:.40f;
            Replace(rig.Barrel,s=>s.HollowBarrel(length,diameter,16));
            var carriage=new Surface();
            float z=mortar?-.33f:-.08f;
            carriage.Beam(new Vector3(-.84f,mortar?.33f:.56f,z),new Vector3(.84f,mortar?.33f:.56f,z),.115f,.13f,Steel);
            foreach(float side in new[]{-1f,1f})
            {
                for(int i=0;i<4;i++)carriage.Box(new Vector3(side*(mortar?.52f:.575f),mortar?.42f:.67f,-.66f+i*.38f),new Vector3(.04f,.21f,.092f),Steel);
                carriage.Beam(new Vector3(side*.36f,.68f,-.56f),new Vector3(side*.36f,.97f,.29f),.026f,.30f,Steel*.75f);
                for(int i=0;i<3;i++)carriage.Ellipsoid(new Vector3(side*.468f,.64f+i*.105f,-.44f+i*.26f),new Vector3(.031f,.031f,.031f),Brass,8,5);
                if(!mortar)
                {
                    carriage.Beam(new Vector3(side*.45f,.51f,-.64f),new Vector3(side*.51f,.19f,counter?-1.76f:-1.30f),.13f,.20f,Leather);
                    carriage.Box(new Vector3(side*.52f,.19f,counter?-1.76f:-1.30f),new Vector3(.23f,.15f,.28f),Steel);
                }
            }
            carriage.Box(new Vector3(0,.43f,mortar?-.77f:-1.03f),new Vector3(.31f,.17f,.22f),Steel);
            carriage.Beam(new Vector3(.55f,.48f,-.73f),new Vector3(.55f,.48f,.82f),.044f,.044f,Leather);
            carriage.Ellipsoid(new Vector3(.55f,.48f,.84f),new Vector3(.067f,.066f,.091f),Linen*.70f,10,6);
            carriage.Finish(root,"Carriage axle ironwork and ramrod");
            var barrel=new Surface();
            barrel.Beam(new Vector3(-diameter*.77f,-length*.17f,0),new Vector3(diameter*.77f,-length*.17f,0),diameter*.30f,diameter*.30f,Steel);
            barrel.Ellipsoid(new Vector3(0,-length*.5f-.10f,0),new Vector3(diameter*.29f,.12f,diameter*.29f),Brass,12,7);
            barrel.Box(new Vector3(0,-length*.27f,-diameter*.55f),new Vector3(.039f,.080f,.075f),Brass);
            barrel.Finish(rig.Barrel,"Trunnions cascabel and vent");
            foreach(var wheel in new[]{rig.LeftWheel,rig.RightWheel})WheelDetails(wheel,mortar?.65f:1.12f,false);
        }

        static void Freight(Transform root,UnitRig rig)
        {
            var body=new Surface();
            for(int i=0;i<8;i++)body.Box(new Vector3(0,.779f,-.82f+i*.233f),new Vector3(1.15f,.026f,.023f),Leather*.50f);
            foreach(float side in new[]{-1f,1f})
            {
                for(int i=0;i<4;i++)
                {
                    body.Box(new Vector3(side*.637f,.98f,-.77f+i*.51f),new Vector3(.06f,.66f,.066f),Leather);
                    for(int j=0;j<2;j++)body.Ellipsoid(new Vector3(side*.674f,.84f+j*.23f,-.77f+i*.51f),new Vector3(.018f,.023f,.023f),Brass,8,4);
                }
                body.Beam(new Vector3(side*.646f,.73f,-.87f),new Vector3(side*.646f,.73f,.87f),.027f,.035f,Brass);
                for(int i=0;i<2;i++)body.Beam(new Vector3(-.82f,.40f,-.60f+i*1.2f),new Vector3(.82f,.40f,-.60f+i*1.2f),.09f,.11f,Steel);
            }
            body.Finish(root,"Wagon planking fasteners and axles");
            var cargo=new Surface();
            for(int i=0;i<3;i++)
            {
                float z=(i-1)*.51f;
                foreach(float x in new[]{-.33f,.33f})
                {
                    cargo.Box(new Vector3(x,1.213f,z),new Vector3(.045f,.021f,.462f),Brass);
                    cargo.Box(new Vector3(x,.97f,z+.232f),new Vector3(.045f,.46f,.022f),Brass);
                }
                for(int j=0;j<5;j++)cargo.Box(new Vector3(-.40f+j*.20f,1.210f,z),new Vector3(.014f,.016f,.42f),Leather*.55f);
                cargo.Box(new Vector3(0,.97f,z+.24f),new Vector3(.11f,.12f,.04f),Steel);
            }
            cargo.Finish(rig.Cargo,"Loaded chest boards straps and locks");
            foreach(var wheel in rig.CartWheels)WheelDetails(wheel,.76f,true);
        }

        static void WheelDetails(Transform wheel,float diameter,bool cart)
        {
            var s=new Surface();float radius=diameter*.5f;
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                s.Beam(p*radius*.19f,p*radius*.88f,.042f,.052f,new Color(.35f,.23f,.135f));
                s.Ellipsoid(p*radius*.935f+Vector3.up*.090f,new Vector3(.021f,.014f,.021f),Steel,8,4);
            }
            s.Ellipsoid(Vector3.up*.12f,new Vector3(.10f,.07f,.10f),Steel,12,6);
            s.Beam(new Vector3(0,.18f,-.083f),new Vector3(0,.18f,.083f),.028f,.025f,Brass);
            s.Finish(wheel,cart?"Freight wheel felloes and axle pin":"Artillery wheel felloes and axle pin");
        }

        static void Replace(Transform target,Action<Surface> author)
        {
            if(target==null)return;var s=new Surface();author(s);var mesh=s.Mesh(target.name+" refined surface");
            var life=target.GetComponent<ProceduralMeshLifetime>();if(life==null)life=target.gameObject.AddComponent<ProceduralMeshLifetime>();
            var old=life.OwnedMesh;life.OwnedMesh=mesh;target.GetComponent<MeshFilter>().sharedMesh=mesh;
            if(old!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(old);else UnityEngine.Object.DestroyImmediate(old);}
        }

        static Vector2 PaletteUV(Color color)
        {
            if(atlas==null)
            {
                palette.Clear();
                atlas=new Texture2D(1024,1,TextureFormat.RGBA32,false){name="Original unit cloth leather metal palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Original unit surface atlas",color=Color.white,mainTexture=atlas};
                material.SetFloat("_Smoothness",.19f);material.enableInstancing=true;
            }
            if(!palette.TryGetValue(color,out int index))
            {
                if(palette.Count>=1024)throw new InvalidOperationException("Original unit palette exhausted");
                index=palette.Count;palette.Add(color,index);atlas.SetPixel(index,0,color);atlas.Apply(false,false);
            }
            return new Vector2((index+.5f)/1024f,.5f);
        }

        static void Bake(Transform root,UnitRig rig)
        {
            var joints=new HashSet<Transform>{root,rig.ScoutBody,rig.LeftShoulder,rig.RightShoulder,rig.LeftElbow,rig.RightElbow,rig.LeftHand,rig.RightHand,rig.LeftLeg,rig.RightLeg,rig.LeftKnee,rig.RightKnee,rig.WeaponPivot,rig.Weapon,rig.MeleeWeapon,rig.Barrel,rig.Carriage,rig.LeftWheel,rig.RightWheel,rig.LeftHorseLeg,rig.RightHorseLeg,rig.RearLeftHorseLeg,rig.RearRightHorseLeg,rig.HorseHead,rig.HorseTail,rig.Shield,rig.Cargo,rig.RepairWeapon,rig.WorkerFirearm,rig.BowStringLower,rig.BowStringUpper};
            foreach(var wheel in rig.CartWheels)joints.Add(wheel);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(!t.gameObject.activeSelf||t.name=="Axe"||t.name=="Pick"||t.name=="Hammer"||t.name=="Basket")joints.Add(t);
            var groups=new Dictionary<Transform,List<MeshRenderer>>();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.enabled)continue;var joint=renderer.transform;
                while(joint!=root&&!joints.Contains(joint))joint=joint.parent;
                if(!groups.TryGetValue(joint,out var list)){list=new List<MeshRenderer>();groups.Add(joint,list);}list.Add(renderer);
            }
            foreach(var group in groups)
            {
                var vertices=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var triangles=new List<int>();
                foreach(var renderer in group.Value)
                {
                    var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                    var src=filter.sharedMesh;var matrix=group.Key.worldToLocalMatrix*filter.transform.localToWorldMatrix;var normalMatrix=matrix.inverse.transpose;
                    var input=src.vertices;var ns=src.normals;var sourceUV=src.uv;var mats=renderer.sharedMaterials;
                    for(int sub=0;sub<src.subMeshCount;sub++)
                    {
                        var mat=mats[Math.Min(sub,mats.Length-1)];bool own=mat==material;
                        var uv=own?Vector2.zero:PaletteUV(mat!=null?mat.color:Color.white);
                        foreach(int ix in src.GetTriangles(sub))
                        {
                            triangles.Add(vertices.Count);vertices.Add(matrix.MultiplyPoint3x4(input[ix]));normals.Add(ns.Length>ix?normalMatrix.MultiplyVector(ns[ix]).normalized:Vector3.up);
                            uvs.Add(own&&sourceUV.Length>ix?sourceUV[ix]:uv);
                        }
                    }
                    renderer.enabled=false;
                    // Detail authoring buffers are disposable once merged. Named source
                    // rig surfaces stay as inspectable geometry and mesh-lifetime owners.
                    if(renderer.sharedMaterial==material)
                    {
                        renderer.GetComponent<ProceduralMeshLifetime>()?.ReleaseOwnedMesh();
                        if(Application.isPlaying)UnityEngine.Object.Destroy(renderer.gameObject);
                        else UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                    }
                }
                var mesh=new Mesh{name="Rig surface / "+group.Key.name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
                var go=new GameObject("Batched rig surface");go.transform.SetParent(group.Key,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<ProceduralMeshLifetime>().OwnedMesh=mesh;
            }
        }

        sealed class Surface
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Vector3> normals=new List<Vector3>();readonly List<int> triangles=new List<int>();readonly List<Vector2> uvs=new List<Vector2>();
            void Tri(Vector3 a,Vector3 b,Vector3 c,Color color)
            {var normal=Vector3.Cross(b-a,c-a).normalized;Triangle(a,b,c,normal,normal,normal,color);}
            void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 na,Vector3 nb,Vector3 nc,Color color)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c});normals.AddRange(new[]{na,nb,nc});triangles.AddRange(new[]{n,n+1,n+2});var uv=PaletteUV(color);uvs.AddRange(new[]{uv,uv,uv});}
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){Tri(a,b,c,color);Tri(a,c,d,color);}
            public void Box(Vector3 p,Vector3 size,Color color)
            {
                Vector3[] v=new Vector3[8];for(int i=0;i<8;i++)v[i]=p+new Vector3((i&1)==0?-size.x:size.x,(i&2)==0?-size.y:size.y,(i&4)==0?-size.z:size.z)*.5f;
                Quad(v[0],v[2],v[3],v[1],color);Quad(v[4],v[5],v[7],v[6],color);Quad(v[0],v[4],v[6],v[2],color);Quad(v[1],v[3],v[7],v[5],color);Quad(v[0],v[1],v[5],v[4],color);Quad(v[2],v[6],v[7],v[3],color);
            }
            public void Beam(Vector3 a,Vector3 b,float width,float depth,Color color)
            {
                var axis=(b-a).normalized;var right=Vector3.Cross(axis,Mathf.Abs(axis.z)>.9f?Vector3.up:Vector3.forward).normalized*width*.5f;var front=Vector3.Cross(right.normalized,axis).normalized*depth*.5f;
                var v=new[]{a-right-front,a+right-front,a+right+front,a-right+front,b-right-front,b+right-front,b+right+front,b-right+front};
                Quad(v[0],v[4],v[5],v[1],color);Quad(v[1],v[5],v[6],v[2],color);Quad(v[2],v[6],v[7],v[3],color);Quad(v[3],v[7],v[4],v[0],color);Quad(v[0],v[1],v[2],v[3],color);Quad(v[4],v[7],v[6],v[5],color);
            }
            public void Ellipsoid(Vector3 p,Vector3 r,Color color,int sides,int rings)
            {
                Vector3 Point(int row,int col){float theta=Mathf.PI*row/rings,phi=2*Mathf.PI*col/sides;return p+Vector3.Scale(new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi),Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(phi)),r);}
                for(int row=0;row<rings;row++)for(int i=0;i<sides;i++)
                {
                    var a=Point(row,i);var b=Point(row,i+1);var c=Point(row+1,i+1);var d=Point(row+1,i);
                    Vector3 N(Vector3 v){v-=p;return new Vector3(v.x/(r.x*r.x),v.y/(r.y*r.y),v.z/(r.z*r.z)).normalized;}
                    if(row>0)Triangle(a,b,c,N(a),N(b),N(c),color);if(row<rings-1)Triangle(a,c,d,N(a),N(c),N(d),color);
                }
            }
            public void Loft(float[] heights,float[] rx,float[] rz,int sides,Color color)
            {
                Vector3 Point(int r,int i){float a=2*Mathf.PI*i/sides;return new Vector3(Mathf.Cos(a)*rx[r],heights[r],Mathf.Sin(a)*rz[r]);}
                Vector3 Normal(int row,int col)
                {
                    float a=2*Mathf.PI*col/sides,c=Mathf.Cos(a),z=Mathf.Sin(a);int lower=Mathf.Max(0,row-1),upper=Mathf.Min(heights.Length-1,row+1);
                    float dy=heights[upper]-heights[lower],dx=(rx[upper]-rx[lower])/dy,dz=(rz[upper]-rz[lower])/dy;
                    return new Vector3(c/rx[row],-c*c*dx/rx[row]-z*z*dz/rz[row],z/rz[row]).normalized;
                }
                for(int r=0;r<heights.Length-1;r++)for(int i=0;i<sides;i++)
                {
                    Triangle(Point(r,i),Point(r+1,i),Point(r+1,i+1),Normal(r,i),Normal(r+1,i),Normal(r+1,i+1),color);
                    Triangle(Point(r,i),Point(r+1,i+1),Point(r,i+1),Normal(r,i),Normal(r+1,i+1),Normal(r,i+1),color);
                }
                for(int i=0;i<sides;i++){Tri(new Vector3(0,heights[0],0),Point(0,i),Point(0,i+1),color);int r=heights.Length-1;Tri(new Vector3(0,heights[r],0),Point(r,i+1),Point(r,i),color);}
            }
            public void Plate(Vector3[] outline,float thickness,Color color)
            {
                // All callers supply convex lapel panels.
                for(int i=1;i<outline.Length-1;i++){Tri(outline[0],outline[i],outline[i+1],color);Tri(outline[0]+Vector3.forward*thickness,outline[i+1]+Vector3.forward*thickness,outline[i]+Vector3.forward*thickness,color);}
                for(int i=0;i<outline.Length;i++){int j=(i+1)%outline.Length;Quad(outline[i],outline[j],outline[j]+Vector3.forward*thickness,outline[i]+Vector3.forward*thickness,color);}
            }
            public void Band(Vector3 position,float radiusX,float radiusZ,float height,Color color)
            {
                int first=vertices.Count;Loft(new[]{-height*.5f,height*.5f},new[]{radiusX,radiusX},new[]{radiusZ,radiusZ},16,color);
                for(int i=first;i<vertices.Count;i++)vertices[i]+=position;
            }
            public void HatBrim(bool broad,bool worker)
            {
                const int sides=24;
                Vector3 Point(int i,bool outer,float lower)
                {
                    float a=i*Mathf.PI*2/sides;
                    float fold=worker?0:Mathf.Pow(Mathf.Max(0,Mathf.Cos(3*a)),2);
                    float radius=outer?(broad?.35f:.285f)*(worker?1:1-.12f*fold):.19f;
                    return new Vector3(Mathf.Cos(a)*radius,(outer?fold*.105f:0)+lower,Mathf.Sin(a)*radius*.87f);
                }
                for(int i=0;i<sides;i++)
                {
                    Quad(Point(i,false,.022f),Point(i+1,false,.022f),Point(i+1,true,.022f),Point(i,true,.022f),Color.white);
                    Quad(Point(i,true,-.022f),Point(i+1,true,-.022f),Point(i+1,false,-.022f),Point(i,false,-.022f),Color.white);
                    Quad(Point(i,true,.022f),Point(i+1,true,.022f),Point(i+1,true,-.022f),Point(i,true,-.022f),Color.white);
                }
            }
            public void HollowBarrel(float length,float diameter,int sides)
            {
                float[] ys={-.5f,-.40f,-.28f,-.22f,.27f,.43f,.48f,.5f};float[] rs={.59f,.59f,.53f,.49f,.44f,.47f,.57f,.57f};
                Vector3 P(float y,float r,int i){float a=i*2*Mathf.PI/sides;return new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);}
                for(int row=0;row<ys.Length-1;row++)for(int i=0;i<sides;i++)Quad(P(ys[row]*length,rs[row]*diameter,i),P(ys[row+1]*length,rs[row+1]*diameter,i),P(ys[row+1]*length,rs[row+1]*diameter,i+1),P(ys[row]*length,rs[row]*diameter,i+1),Color.white);
                for(int i=0;i<sides;i++)
                {
                    Quad(P(length*.5f,diameter*.57f,i),P(length*.5f,diameter*.33f,i),P(length*.5f,diameter*.33f,i+1),P(length*.5f,diameter*.57f,i+1),Color.white);
                    Quad(P(length*.5f,diameter*.33f,i),P(-length*.37f,diameter*.33f,i),P(-length*.37f,diameter*.33f,i+1),P(length*.5f,diameter*.33f,i+1),Color.white);
                    Tri(new Vector3(0,-length*.37f,0),P(-length*.37f,diameter*.33f,i+1),P(-length*.37f,diameter*.33f,i),Color.white);
                    Tri(new Vector3(0,-length*.5f,0),P(-length*.5f,diameter*.59f,i),P(-length*.5f,diameter*.59f,i+1),Color.white);
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uvs);mesh.SetNormals(normals);mesh.RecalculateBounds();return mesh;
            }
            public void Finish(Transform parent,string name)
            {
                if(vertices.Count==0)return;var go=new GameObject(name);go.transform.SetParent(parent,false);var mesh=Mesh(name);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<ProceduralMeshLifetime>().OwnedMesh=mesh;
            }
        }
    }
}
