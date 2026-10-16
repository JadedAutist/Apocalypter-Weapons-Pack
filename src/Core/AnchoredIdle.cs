using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  GameObject idleSource;AnimationClip wrenchIdle;Transform idleSampleRoot,idleWrist;float idleRetry;Vector3 idleWristBase;Quaternion idleWristRotation;Transform[] idleTransforms;Vector3[] idlePositions,idleScales;Quaternion[] idleRotations;
  void PrepareWrenchIdle(){
   if(idleSource!=null||camera==null||Time.unscaledTime<idleRetry)return;idleRetry=Time.unscaledTime+3;
   var source=camera.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="pipe_wrench"&&t.GetComponentsInChildren<Animator>(true).Length>0);if(source==null)source=nativeRigSource;if(source==null)return;
   var animator=source.GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>a.runtimeAnimatorController!=null&&a.runtimeAnimatorController.animationClips.Any(c=>c.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0));if(animator==null)return;
   wrenchIdle=animator.runtimeAnimatorController.animationClips.First(c=>c.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0);var path=new System.Collections.Generic.List<string>();for(var p=animator.transform;p!=null&&p!=source;p=p.parent)path.Insert(0,p.name);
   var staging=new GameObject("Chainsaw.IdleStaging");staging.SetActive(false);idleSource=Instantiate(source.gameObject,staging.transform,false);idleSource.name="Chainsaw.WrenchIdleSource";foreach(var node in idleSource.GetComponentsInChildren<Transform>(true))node.gameObject.tag="Untagged";
   foreach(var script in idleSource.GetComponentsInChildren<MonoBehaviour>(true))if(script.GetType().Name!="FastIKFabric")DestroyImmediate(script);
   foreach(var a in idleSource.GetComponentsInChildren<Animator>(true))a.enabled=false;foreach(var a in idleSource.GetComponentsInChildren<Animation>(true))a.enabled=false;foreach(var c in idleSource.GetComponentsInChildren<Collider>(true))c.enabled=false;foreach(var b in idleSource.GetComponentsInChildren<Rigidbody>(true)){b.isKinematic=true;b.detectCollisions=false;}foreach(var r in idleSource.GetComponentsInChildren<Renderer>(true))r.enabled=false;
   idleSource.SetActive(false);idleSource.transform.SetParent(source.parent,false);idleSource.transform.localPosition=source.localPosition;idleSource.transform.localRotation=source.localRotation;idleSource.transform.localScale=source.localScale;idleSource.transform.SetParent(camera,true);Destroy(staging);
   idleSampleRoot=path.Count==0?idleSource.transform:idleSource.transform.Find(string.Join("/",path));var arm=idleSource.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="arm_right");idleWrist=idleSource.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="IK Target R");if(idleWrist==null)idleWrist=idleSource.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="IK Target R");if(idleWrist==null||idleSampleRoot==null)return;
   idleTransforms=idleSource.GetComponentsInChildren<Transform>(true);idlePositions=idleTransforms.Select(t=>t.localPosition).ToArray();idleScales=idleTransforms.Select(t=>t.localScale).ToArray();idleRotations=idleTransforms.Select(t=>t.localRotation).ToArray();wrenchIdle.SampleAnimation(idleSampleRoot.gameObject,0);idleWristBase=camera.InverseTransformPoint(idleWrist.position);idleWristRotation=Quaternion.Inverse(camera.rotation)*idleWrist.rotation;Logger.LogInfo("Chainsaw anchored idle uses native "+wrenchIdle.name);
  }
  int idleSampleFrame=-1;Vector3 cachedIdleShift;Quaternion cachedIdleTurn=Quaternion.identity;
  void ApplyAnchoredIdle(){
   if(preview==null||left==null||right==null)return;
   var m=preview.transform;Vector3 lp=m.InverseTransformPoint(left.position),rp=m.InverseTransformPoint(right.position);Quaternion lq=Quaternion.Inverse(m.rotation)*left.rotation,rq=Quaternion.Inverse(m.rotation)*right.rotation;
   PrepareWrenchIdle();
   if(idleSource!=null&&wrenchIdle!=null&&idleWrist!=null&&idleTransforms!=null){
    if(idleSampleFrame!=Time.frameCount){
     for(int i=0;i<idleTransforms.Length;i++){idleTransforms[i].localPosition=idlePositions[i];idleTransforms[i].localRotation=idleRotations[i];idleTransforms[i].localScale=idleScales[i];}
     wrenchIdle.SampleAnimation(idleSampleRoot.gameObject,Time.time%Mathf.Max(.01f,wrenchIdle.length));cachedIdleShift=camera.InverseTransformPoint(idleWrist.position)-idleWristBase;Quaternion sampled=Quaternion.Inverse(camera.rotation)*idleWrist.rotation;cachedIdleTurn=sampled*Quaternion.Inverse(idleWristRotation);if(!Finite(cachedIdleShift)||!Finite(cachedIdleTurn)){cachedIdleShift=Vector3.zero;cachedIdleTurn=Quaternion.identity;}idleSampleFrame=Time.frameCount;
    }
    Vector3 shift=camera.TransformVector(cachedIdleShift);Quaternion turn=camera.rotation*cachedIdleTurn*Quaternion.Inverse(camera.rotation);Vector3 pivot=right.position;m.SetPositionAndRotation(pivot+turn*(m.position-pivot)+shift,turn*m.rotation);
   }else{
    // Preserve a gentle idle while native clip discovery is pending.
    float at=Time.time;float strength=PreviewValue(swayStrength);var shift=camera.TransformVector(new Vector3(.0015f*Mathf.Sin(at*1.7f),.0025f*Mathf.Sin(at*2.1f),0)*strength);var turn=camera.rotation*Quaternion.Euler(.25f*Mathf.Sin(at*1.4f)*strength,.2f*Mathf.Sin(at*1.7f)*strength,0)*Quaternion.Inverse(camera.rotation);var pivot=right.position;m.SetPositionAndRotation(pivot+turn*(m.position-pivot)+shift,turn*m.rotation);
   }
   left.SetPositionAndRotation(m.TransformPoint(lp),m.rotation*lq);right.SetPositionAndRotation(m.TransformPoint(rp),m.rotation*rq);
  }
 }
}
