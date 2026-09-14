using System;
using System.Collections.Generic;
using UnityEngine;
namespace Frontier
{
    // Original FBX surfaces are attached to gameplay pivots with canonical local axes.
    // Importer axis-conversion transforms never become animation joint axes.
    public static class AuthoredInfantry
    {
        static GameObject source;
        public static void Attach(Transform root,UnitRig rig,Color team)
        {
            if(source==null)source=Resources.Load<GameObject>("Models/FrontierInfantry");
            if(source==null)throw new InvalidOperationException("Original infantry model is missing from Resources/Models");
            var imported=UnityEngine.Object.Instantiate(source,root);imported.name="Authored infantry import";
            var joints=new Dictionary<string,Transform>();
            Transform Joint(string name,Vector3 point,Transform parent)
            {
                var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=root.TransformPoint(point);joints[name]=go.transform;return go.transform;
            }
            rig.LeftShoulder=Joint("Left shoulder",new Vector3(-.3f,1.25f,.05f),root);rig.RightShoulder=Joint("Right shoulder",new Vector3(.3f,1.25f,.05f),root);
            rig.LeftElbow=Joint("Left elbow",new Vector3(-.3f,.89f,.05f),rig.LeftShoulder);rig.RightElbow=Joint("Right elbow",new Vector3(.3f,.89f,.05f),rig.RightShoulder);
            rig.LeftHand=Joint("Left hand",new Vector3(-.3f,.55f,.05f),rig.LeftElbow);rig.RightHand=Joint("Right hand",new Vector3(.3f,.55f,.05f),rig.RightElbow);
            rig.LeftLeg=Joint("Left leg",new Vector3(-.17f,.65f,0),root);rig.RightLeg=Joint("Right leg",new Vector3(.17f,.65f,0),root);
            foreach(var renderer in imported.GetComponentsInChildren<MeshRenderer>())
            {
                var target=root;for(var parent=renderer.transform.parent;parent!=null&&parent!=root;parent=parent.parent)
                    if(joints.TryGetValue(parent.name,out target))break;else target=root;
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)materials[i]=ProceduralArt.Mat(ColorFor(materials[i]?.name??renderer.name,team));
                renderer.sharedMaterials=materials;renderer.transform.SetParent(target,true);
            }
            UnityEngine.Object.Destroy(imported);
        }
        static Color ColorFor(string name,Color team)
        {
            if(name.Contains("Team uniform"))return team;
            if(name.Contains("Coat facings"))return new Color(.55f,.055f,.035f);
            if(name.Contains("Warm linen"))return new Color(.77f,.71f,.53f);
            if(name.Contains("Boot leather"))return new Color(.07f,.045f,.035f);
            if(name.Contains("Felt"))return new Color(.035f,.044f,.05f);
            if(name.Contains("Skin"))return new Color(.62f,.38f,.23f);
            if(name.Contains("Hair"))return new Color(.10f,.057f,.025f);
            if(name.Contains("Hand"))return new Color(.64f,.43f,.28f);
            if(name.Contains("Brass"))return new Color(.68f,.42f,.11f);
            if(name.Contains("Eyes"))return new Color(.028f,.02f,.013f);
            throw new InvalidOperationException("Unmapped original infantry material: "+name);
        }
    }
}
