using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  bool thirdActionsInstalled,thirdActionsUnavailable;
  bool OwnsThirdPersonSaw(){return enabledPreview&&camera!=null&&CanPresentHeldWeapon(camera)&&ReadHeldChainsaw()!=null;}
  bool OwnsThirdPersonFlame(){return activeFlame!=null&&activeFlame.Equipped&&activeFlame.gameObject.activeInHierarchy&&flamePlayer!=null&&CanPresentHeldWeapon(flamePlayer.transform);}
  void InstallThirdPersonWeaponActions(){
   if(thirdActionsInstalled||thirdActionsUnavailable||harmony==null)return;
   var type=BodyType("Game");if(type==null)return;
   var drawn=type.GetProperty("DrawnWeapon",BodyFlags);var aim=type.GetProperty("AimDownSights",BodyFlags);
   var getDrawn=drawn==null?null:drawn.GetGetMethod(true);var getAim=aim==null?null:aim.GetGetMethod(true);
   var attack=type.GetMethod("Attack",BodyFlags,null,new[]{typeof(string),typeof(string).MakeByRefType(),typeof(float).MakeByRefType()},null);
   if(getDrawn==null||!getDrawn.IsStatic||drawn.PropertyType!=typeof(string)||getAim==null||!getAim.IsStatic||aim.PropertyType!=typeof(bool)||attack==null||!attack.IsStatic||attack.ReturnType!=typeof(bool)){thirdActionsUnavailable=true;Logger.LogWarning("Optional third-person weapon actions: unsupported body game layout.");return;}
   try{harmony.Patch(getDrawn,postfix:new HarmonyMethod(typeof(Plugin),nameof(AfterThirdPersonDrawnWeapon)));harmony.Patch(getAim,prefix:new HarmonyMethod(typeof(Plugin),nameof(BeforeThirdPersonAim)));harmony.Patch(attack,prefix:new HarmonyMethod(typeof(Plugin),nameof(BeforeThirdPersonNativeAttack)));thirdActionsInstalled=true;}
   catch(Exception error){harmony.Unpatch(getDrawn,HarmonyPatchType.Postfix,harmony.Id);harmony.Unpatch(getAim,HarmonyPatchType.Prefix,harmony.Id);harmony.Unpatch(attack,HarmonyPatchType.Prefix,harmony.Id);thirdActionsUnavailable=true;Logger.LogWarning("Optional third-person weapon actions unavailable: "+error.GetBaseException().Message);}
  }
  static void AfterThirdPersonDrawnWeapon(ref string __result){var owner=instance;if(!ReferenceEquals(owner,null)&&owner.OwnsThirdPersonSaw())__result="pipe_wrench";}
  static bool BeforeThirdPersonAim(ref bool __result){var owner=instance;if(ReferenceEquals(owner,null)||!owner.OwnsThirdPersonSaw()&&!owner.OwnsThirdPersonFlame())return true;__result=false;return false;}
  static bool BeforeThirdPersonNativeAttack(ref string __1,ref float __2,ref bool __result){var owner=instance;if(ReferenceEquals(owner,null)||!owner.OwnsThirdPersonSaw())return true;__1="on";__2=0f;__result=true;return false;}

  ThirdPersonArm thirdRightArm,thirdLeftArm;object thirdMotionBody;int thirdMotionFrame=-1;
  bool thirdAttackApplied;Transform thirdBodyRoot;
  Vector3 thirdPreviousRoot;float thirdRunBlend;int thirdTravelFrame=-1;
  void BeginThirdPersonBodyPose(object body,GameObject root,Transform rh,Transform lh){
   // LateTick and final holding/render refreshes can occur within one frame.
   // Always rebuild from the native pose, never from an earlier IK correction.
   if(thirdAttackApplied&&thirdMotionFrame==Time.frameCount&&ReferenceEquals(thirdMotionBody,body)){if(thirdRightArm!=null)thirdRightArm.Restore();if(thirdLeftArm!=null)thirdLeftArm.Restore();}
   if(!ReferenceEquals(thirdMotionBody,body)||thirdRightArm==null||thirdRightArm.Hand!=rh||thirdLeftArm==null||thirdLeftArm.Hand!=lh){thirdRightArm=new ThirdPersonArm(rh,root.transform);thirdLeftArm=new ThirdPersonArm(lh,root.transform);thirdMotionBody=body;}
   thirdRightArm.Capture();thirdLeftArm.Capture();thirdMotionFrame=Time.frameCount;thirdAttackApplied=false;
   if(thirdBodyRoot!=root.transform){thirdBodyRoot=root.transform;thirdPreviousRoot=thirdBodyRoot.position;thirdRunBlend=0;thirdTravelFrame=-1;}
   if(thirdTravelFrame!=Time.frameCount){thirdTravelFrame=Time.frameCount;var travel=Vector3.ProjectOnPlane(thirdBodyRoot.position-thirdPreviousRoot,thirdBodyRoot.up);thirdPreviousRoot=thirdBodyRoot.position;float dt=Time.deltaTime;float target=dt>0&&travel.sqrMagnitude<.75f*.75f?Mathf.InverseLerp(1.5f,4f,travel.magnitude/dt):0;thirdRunBlend=Mathf.MoveTowards(thirdRunBlend,target,Mathf.Max(0,dt)*4f);}

  }
  void CaptureThirdPersonBasePose(){RefreshThirdPersonAttackPose();}
  void ClearThirdPersonAttackPose(){
   if(thirdAttackApplied&&thirdMotionFrame==Time.frameCount){if(thirdRightArm!=null)thirdRightArm.Restore();if(thirdLeftArm!=null)thirdLeftArm.Restore();}
   thirdAttackApplied=false;thirdRunBlend=0;thirdTravelFrame=-1;
  }
  void RefreshThirdPersonAttackPose(){
   if(!enabledPreview||preview==null||camera==null||thirdModel==null||!thirdModel.activeSelf||thirdSource!=preview.transform||thirdMotionFrame!=Time.frameCount||!ActualThirdPersonActive())return;
   if(thirdBodyRoot==null||!thirdRightArm.Ready||!thirdLeftArm.Ready){thirdModel.SetActive(false);return;}
   var source=preview.transform;var scale=source.lossyScale;
   if(!Finite(scale)||Mathf.Abs(scale.x)<.0001f||Mathf.Abs(scale.y)<.0001f||Mathf.Abs(scale.z)<.0001f)return;
   thirdScale=scale;FlameWorldScale(thirdModel.transform,scale);
   if(thirdSourceRight!=null&&thirdSourceLeft!=null){var rg=source.InverseTransformPoint(thirdSourceRight.position);var lg=source.InverseTransformPoint(thirdSourceLeft.position);if(Finite(rg)&&Finite(lg)&&Vector3.Scale(lg-rg,scale).sqrMagnitude>.0001f){thirdGripRight=rg;thirdGripLeft=lg;}}
   float wind=.48f,contact=.22f,recovery=.18f,returning=.32f;bool diagonal=nextAttack>0&&nextAttack%2==0;
   if(attackPreset!=null){returning=Mathf.Clamp(attackPreset.returnSeconds,.02f,5f);diagonal=(attackPreset.preset??"").IndexOf("diagonal",StringComparison.OrdinalIgnoreCase)>=0;if(attackPreset.keys!=null&&attackPreset.keys.Length>=2){wind=Mathf.Clamp(attackPreset.keys[0].seconds,.02f,5f);contact=Mathf.Clamp(attackPreset.keys[1].seconds,.02f,5f);recovery=0;for(int i=2;i<attackPreset.keys.Length;i++)recovery+=Mathf.Clamp(attackPreset.keys[i].seconds,.02f,5f);}}
   var pose=ChainsawBodyPose.Sample(attackTime,wind,contact,recovery,returning,diagonal,gameAttacking,cuttingBlend,thirdRunBlend);
   var up=thirdBodyRoot.up;var forward=Vector3.ProjectOnPlane(thirdBodyRoot.forward,up);if(forward.sqrMagnitude<.0001f)return;
   float pitch=Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(Vector3.Dot(camera.forward,up),-1,1))*Mathf.Rad2Deg,-40,40);
   var frame=Quaternion.LookRotation(forward,up)*Quaternion.Euler(pitch,0,0);var centre=(thirdRightArm.Shoulder+thirdLeftArm.Shoulder)*.5f;
   float size=Mathf.Clamp((thirdRightArm.Length+thirdLeftArm.Length)/(2*.556f),.65f,1.7f);
   var neutral=Quaternion.identity;if(stockModels!=null)foreach(var entry in stockModels)if(entry.Key==source){neutral=entry.Value.rotation;break;}
   var turn=frame*Quaternion.Euler(pose.Angles)*neutral;if(thirdTurn!=null)turn*=Quaternion.Euler(Values(thirdTurn));
   var rightGoal=centre+frame*(pose.Right*size);if(thirdMove!=null)rightGoal+=frame*Values(thirdMove);
   if(!gameAttacking&&cuttingBlend>.0001f){var shake=ChainsawCuttingVibration.Sample(Time.timeAsDouble,cuttingHeld&&Time.timeScale>0?cuttingBlend*ChainsawShakeMultiplier(true):0);rightGoal+=frame*new Vector3(shake.X,shake.Y,shake.Z);turn=frame*Quaternion.Euler(shake.Pitch,shake.Yaw,shake.Roll)*Quaternion.Inverse(frame)*turn;}
   var rightOffset=turn*Vector3.Scale(thirdGripRight,scale);var leftOffset=turn*Vector3.Scale(thirdGripLeft,scale);Vector3 fitted;
   if(!ChainsawBodyPose.TryFit(rightGoal,thirdRightArm.Shoulder,thirdRightArm.Length*.94f,thirdLeftArm.Shoulder,thirdLeftArm.Length*.94f,leftOffset-rightOffset,out fitted)){thirdModel.SetActive(false);return;}
   var position=fitted-rightOffset;thirdModel.transform.SetPositionAndRotation(position,turn);
   var reachShift=fitted-rightGoal;thirdRightArm.SetBodyPose(centre+frame*(pose.RightElbow*size)+reachShift);thirdLeftArm.SetBodyPose(centre+frame*(pose.LeftElbow*size)+reachShift);
   thirdRightArm.Solve(fitted,Quaternion.identity);thirdLeftArm.Solve(position+leftOffset,Quaternion.identity);thirdAttackApplied=true;
  }

 }
 // Operates only on the local body arms while our own chainsaw is held.
 // Native animation continues to own the torso, legs and ordinary locomotion.
 internal sealed class ThirdPersonArm {
  internal readonly Transform Hand;readonly Transform upper,lower;readonly bool supported;bool valid;
  Quaternion upperRotation,lowerRotation,handRotation,handLocalRotation;Vector3 shoulderPoint,elbowPoint,handPoint,elbowHint;float upperLength,lowerLength;
  internal ThirdPersonArm(Transform hand,Transform root){Hand=hand;lower=hand==null?null:hand.parent;upper=lower==null?null:lower.parent;supported=upper!=null&&lower!=root&&upper!=root&&upper.IsChildOf(root);}
  internal void Capture(){valid=false;if(Hand==null||!supported||upper==null||lower==null)return;handPoint=Hand.position;handRotation=Hand.rotation;handLocalRotation=Hand.localRotation;shoulderPoint=upper.position;elbowPoint=lower.position;upperRotation=upper.rotation;lowerRotation=lower.rotation;upperLength=Vector3.Distance(shoulderPoint,elbowPoint);lowerLength=Vector3.Distance(elbowPoint,handPoint);valid=upperLength>.02f&&lowerLength>.02f&&upperLength<1.5f&&lowerLength<1.5f;elbowHint=elbowPoint;}
  internal bool Ready {get{return valid;}}
  internal Vector3 Shoulder {get{return shoulderPoint;}}
  internal float Length {get{return upperLength+lowerLength;}}
  internal void SetBodyPose(Vector3 elbow){elbowHint=elbow;}
  internal void Restore(){if(!valid||Hand==null||upper==null||lower==null)return;upper.rotation=upperRotation;lower.rotation=lowerRotation;Hand.rotation=handRotation;}
  internal void Solve(Vector3 goal,Quaternion unused){
   if(!valid||Hand==null||upper==null||lower==null)return;Restore();
   float a=upperLength,b=lowerLength;var to=goal-shoulderPoint;float length=to.magnitude;if(length<.0001f)return;var direction=to/length;float distance=Mathf.Clamp(length,Mathf.Abs(a-b)+.001f,a+b-.001f);
   var bend=Vector3.ProjectOnPlane(elbowHint-shoulderPoint,direction);if(bend.sqrMagnitude<.00001f)bend=Vector3.ProjectOnPlane(upper.up,direction);if(bend.sqrMagnitude<.00001f)bend=Vector3.ProjectOnPlane(upper.right,direction);bend.Normalize();
   float along=(a*a+distance*distance-b*b)/(2f*distance);var elbow=shoulderPoint+direction*along+bend*Mathf.Sqrt(Mathf.Max(0f,a*a-along*along));
   upper.rotation=Quaternion.FromToRotation(elbowPoint-shoulderPoint,elbow-shoulderPoint)*upperRotation;
   var target=shoulderPoint+direction*distance;lower.rotation=Quaternion.FromToRotation(Hand.position-lower.position,target-lower.position)*lower.rotation;
   // Flexa has no finger bones. Preserve the native wrist's local joint pose
   // while allowing it to follow the newly solved forearm, rather than freezing
   // a camera-space or wrench-space world rotation.
   Hand.localRotation=handLocalRotation;
  }
 }
}
