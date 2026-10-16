using System.Linq;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  bool m16HoldingReady;Vector3 m16LeftPoint,m16RightPoint;Quaternion m16LeftTurn,m16RightTurn,m16ModelTurn;float m16ModelScale;
  Vector3 chainsawPlacementBasePoint;Quaternion chainsawPlacementBaseRotation;
  public static bool GetChainsawPlacementMotion(out Quaternion turn,out Vector3 shift){turn=Quaternion.identity;shift=Vector3.zero;if(ReferenceEquals(instance,null)||!instance.enabledPreview||instance.preview==null)return false;if(instance.stockMotionReady){turn=instance.stockMotion;shift=instance.stockShift;return true;}turn=instance.preview.transform.rotation*Quaternion.Inverse(instance.chainsawPlacementBaseRotation);shift=instance.preview.transform.position-turn*instance.chainsawPlacementBasePoint;if(!Finite(turn)||!Finite(shift)){turn=Quaternion.identity;shift=Vector3.zero;}return true;}
  void PrepareM16Holding(){
   if(m16HoldingReady||weapon==null||camera==null||left==null||right==null)return;
   foreach(var animator in weapon.GetComponentsInChildren<Animator>(true)){
    if(animator.runtimeAnimatorController==null)continue;
    var idle=animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="m16a1_idle");
    if(idle!=null)idle.SampleAnimation(animator.gameObject,0);
   }
   SolveHeldIK();
   m16LeftPoint=camera.InverseTransformPoint(left.position);m16RightPoint=camera.InverseTransformPoint(right.position);
   m16LeftTurn=Quaternion.Inverse(camera.rotation)*left.rotation;m16RightTurn=Quaternion.Inverse(camera.rotation)*right.rotation;
   var authored=data.leftPosition-data.rightPosition;var native=m16LeftPoint-m16RightPoint;
   m16ModelTurn=Quaternion.FromToRotation(authored,native);
   m16ModelScale=authored.magnitude>.001f?Mathf.Clamp(native.magnitude/authored.magnitude,.75f,1.25f):1;
   m16HoldingReady=true;Logger.LogInfo("Chainsaw holding rebuilt from native M16 idle targets. L="+m16LeftPoint+" R="+m16RightPoint);
  }
  void ApplyM16Holding(){
   stockMotionReady=false;
   var pivot=data.rightPosition;var rotation=Quaternion.Euler(Values(modelTurn));var translation=Values(modelMove);float scale=PreviewValue(poseScale);
   preview.transform.localRotation=rotation;preview.transform.localScale=Vector3.one*scale;
   preview.transform.localPosition=pivot+translation-rotation*(pivot*scale);
   chainsawPlacementBasePoint=preview.transform.position;chainsawPlacementBaseRotation=preview.transform.rotation;
   left.SetPositionAndRotation(camera.TransformPoint(pivot+translation+rotation*((data.leftPosition-pivot)*scale)+Values(leftMove)),camera.rotation*rotation*data.leftRotation*Quaternion.Euler(Values(leftTurn)));
   right.SetPositionAndRotation(camera.TransformPoint(pivot+translation+Values(rightMove)),camera.rotation*rotation*data.rightRotation*Quaternion.Euler(Values(rightTurn)));
   ApplyAnchoredIdle();ApplyHeldMotion();
   if(applyingAttack&&forcedPose!=null)ApplyAttackPose(preview.transform,left,right,camera);
   NotifyPoseApplied();
  }
 }
}
