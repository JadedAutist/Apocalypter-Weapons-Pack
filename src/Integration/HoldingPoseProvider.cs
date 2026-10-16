using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  [Serializable] class HoldingBone {public string side;public int index;public Vector3 position;public Quaternion rotation;}
  [Serializable] class HoldingBones {public HoldingBone[] bones;}
  public static string GetHoldingArmPose(){
   if(ReferenceEquals(instance,null))return null;var owner=instance;owner.FindWeapon();if(owner.weapon==null)return null;bool active=owner.weapon.gameObject.activeSelf;var poses=new List<HoldingBone>();
   try{owner.weapon.gameObject.SetActive(true);owner.CalibrateClonedIK();owner.ApplyTargets();owner.SolveHeldIK();
    foreach(string side in new[]{"left","right"}){var branch=owner.weapon.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="arm_"+side);if(branch==null)continue;var skin=branch.GetComponentInChildren<SkinnedMeshRenderer>(true);if(skin==null)continue;for(int i=0;i<skin.bones.Length;i++){var bone=skin.bones[i];if(bone!=null)poses.Add(new HoldingBone{side=side,index=i,position=owner.camera.InverseTransformPoint(bone.position),rotation=Quaternion.Inverse(owner.camera.rotation)*bone.rotation});}}
    return poses.Count==0?null:JsonUtility.ToJson(new HoldingBones{bones=poses.ToArray()});
   }finally{owner.weapon.gameObject.SetActive(active);}
  }
 }
}
