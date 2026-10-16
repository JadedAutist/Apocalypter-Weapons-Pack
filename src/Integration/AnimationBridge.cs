using System;
using HarmonyLib;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  bool solvedFinalPose;MonoBehaviour[] cachedIK;System.Reflection.MethodInfo ikLateUpdate;
  void CalibrateClonedIK(){
   if(calibratedIK || nativeRigSource==null || weapon==null)return;
   var originals=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(nativeRigSource.GetComponentsInChildren<MonoBehaviour>(true),ik=>ik.GetType().Name=="FastIKFabric"));
   cachedIK=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(weapon.GetComponentsInChildren<MonoBehaviour>(true),ik=>ik.GetType().Name=="FastIKFabric"));
   if(originals.Length!=cachedIK.Length)throw new InvalidOperationException("Cloned/native arm IK count mismatch.");
   for(int i=0;i<cachedIK.Length;i++){
    var type=cachedIK[i].GetType();AccessTools.Method(type,"Init").Invoke(cachedIK[i],null);
    foreach(var name in new[]{"StartRotationBone","StartRotationTarget","StartDirectionSucc"}){
     var field=AccessTools.Field(type,name);var value=field.GetValue(originals[i]);if(value==null)continue;if(value is Quaternion && Quaternion.Dot((Quaternion)value,(Quaternion)value)<.5f)continue;var array=value as Array;field.SetValue(cachedIK[i],array==null?value:array.Clone());
    }
    var bones=AccessTools.Field(type,"Bones").GetValue(cachedIK[i]) as Transform[];
    if(bones!=null)foreach(var bone in bones)if(bone!=null&&!bone.IsChildOf(weapon))throw new InvalidOperationException("Chainsaw IK referenced an external/native bone: "+bone.name);
   }
   if(cachedIK.Length>0)ikLateUpdate=AccessTools.Method(cachedIK[0].GetType(),"LateUpdate");calibratedIK=true;Logger.LogInfo("Chainsaw IK calibrated from native reference rotations with independent bone arrays.");
  }
  void SolveHeldIK(){
   if(weapon==null)return;solvedFinalPose=true;
   if(cachedIK==null || cachedIK.Length==0 || cachedIK[0]==null){cachedIK=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(weapon.GetComponentsInChildren<MonoBehaviour>(true),ik=>ik.GetType().Name=="FastIKFabric"));if(cachedIK.Length>0)ikLateUpdate=AccessTools.Method(cachedIK[0].GetType(),"LateUpdate");}
   foreach(var ik in cachedIK)if(ik!=null && ik.gameObject.activeInHierarchy)ikLateUpdate.Invoke(ik,null);
  }
  public static void SolveAnimationTargets(){if(!ReferenceEquals(instance,null))instance.SolveHeldIK();}
  public static event Action<Transform,Transform,Transform,Transform> PoseApplied;
  public static event Action FinalCameraPose;
  public static bool TryGetAnimationRig(out Transform model,out Transform rig,out Transform l,out Transform r,out Transform view){model=null;rig=null;l=null;r=null;view=null;if(ReferenceEquals(instance,null)||!instance.enabledPreview||instance.preview==null)return false;model=instance.preview.transform;rig=instance.weapon;l=instance.left;r=instance.right;view=instance.camera;return model!=null&&rig!=null&&l!=null&&r!=null&&view!=null;}
  public static void ApplyAnimationBase(){if(!ReferenceEquals(instance,null)&&instance.enabledPreview)instance.ApplyTargets();}
  public static void RefreshAnimationRig(){if(ReferenceEquals(instance,null)||!instance.enabledPreview)return;instance.ApplyTargets();instance.SolveHeldIK();instance.CompleteHoldingPose();}
  void NotifyPoseApplied(){var handler=PoseApplied;if(handler!=null)handler(preview.transform,left,right,camera);}
 }
}
