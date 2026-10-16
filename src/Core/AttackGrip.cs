using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  class WristAnchor {public Transform bone,target,shoulder;public Vector3 point;public Quaternion rotation,offset;public float reach;}
  WristAnchor[] wristAnchors;
  Transform FindWrist(string side){var branch=attackRig.GetComponentsInChildren<Transform>(true);foreach(var t in branch)if(t.name==side)foreach(var b in t.GetComponentsInChildren<Transform>(true))if(b.name.StartsWith("hand1",StringComparison.OrdinalIgnoreCase))return b;return null;}
  void CaptureWristAnchors(){var anchors=new List<WristAnchor>();foreach(var side in new[]{"arm_left","arm_right"}){var bone=FindWrist(side);var target=side=="arm_left"?left:right;if(bone==null||target==null)continue;var shoulder=bone;float reach=0;for(int i=0;i<3&&shoulder.parent!=null;i++){reach+=Vector3.Distance(shoulder.position,shoulder.parent.position);shoulder=shoulder.parent;}anchors.Add(new WristAnchor{bone=bone,target=target,shoulder=shoulder,reach=reach,point=attackModel.InverseTransformPoint(bone.position),rotation=Quaternion.Inverse(attackModel.rotation)*bone.rotation,offset=Quaternion.Inverse(target.rotation)*bone.rotation});}wristAnchors=anchors.ToArray();}
  void RestoreAttackGrip(){
   if(wristAnchors==null)return;
   // Keep the grip captured at attack start; never infer it from last frame's animated wrist.
   foreach(var a in wristAnchors){
    a.target.SetPositionAndRotation(attackModel.TransformPoint(a.point),attackModel.rotation*a.rotation*Quaternion.Inverse(a.offset));
    a.bone.SetPositionAndRotation(attackModel.TransformPoint(a.point),attackModel.rotation*a.rotation);
   }
  }
  void EnforceWristAnchors(){
   if(wristAnchors==null)return;
   for(int iteration=0;iteration<8;iteration++)foreach(var a in wristAnchors){Vector3 grip=attackModel.TransformPoint(a.point),delta=grip-a.shoulder.position;float distance=delta.magnitude;if(distance>a.reach-.002f&&distance>.001f)attackModel.position-=delta/distance*(distance-a.reach+.002f);}
   foreach(var a in wristAnchors)a.target.SetPositionAndRotation(attackModel.TransformPoint(a.point),attackModel.rotation*a.rotation*Quaternion.Inverse(a.offset));
   SolveHeldIK();
   foreach(var a in wristAnchors)a.bone.SetPositionAndRotation(attackModel.TransformPoint(a.point),attackModel.rotation*a.rotation);
  }

 }
}
