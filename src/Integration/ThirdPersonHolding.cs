using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChainsawPrototype {
 public partial class Plugin {
  const BindingFlags BodyFlags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
  FieldInfo playerBodyField,bodyRootField,bodyBonesField,bodyRightField,bodyLeftField,bodyPropField,bodyArmsField,bodyPropShownField,bodyPropShadowField,thirdPersonOn,thirdPersonHasView,thirdPersonPeek;
  MonoBehaviour playerBodyRunner;float nextPlayerBodyScan;bool bodyHookUnavailable,bodyTickHook;
  SkinnedMeshRenderer borrowedBodyArms;bool borrowedBodyArmsEnabled;
  object lastBody;bool samplingThirdPerson,playerThirdPerson;
  Transform thirdSource,thirdSourceRight,thirdSourceLeft;Vector3 thirdGripRight,thirdGripLeft,thirdUp,thirdScale;
  Renderer[] suppressedRenderers;bool[] suppressedEnabled;ShadowCastingMode[] suppressedShadows;
  object suppressedBody;bool suppressedShown,suppressedShadow;
  static Type BodyType(string name){return OptionalWeaponTypes.Find("WildWasteland.Modules.PlayerBody."+name)??OptionalWeaponTypes.Find("Apocaplayer."+(name=="BodyRunner"?"Runner":name));}
  void InstallBodyHook(){
   if(bodyHook||bodyHookUnavailable||harmony==null)return;var type=BodyType("Body");if(type==null)return;
   var method=type.GetMethod("LateFoot",BodyFlags);var runner=BodyType("BodyRunner");
   bodyRootField=type.GetField("Root",BodyFlags);bodyBonesField=type.GetField("Bones",BodyFlags);bodyRightField=type.GetField("RH",BodyFlags);bodyLeftField=type.GetField("LH",BodyFlags);bodyPropField=type.GetField("_prop",BodyFlags);bodyArmsField=type.GetField("_fpArms",BodyFlags);playerBodyField=runner==null?null:runner.GetField("_body",BodyFlags);
   if(method==null||method.ReturnType!=typeof(void)||bodyRootField==null||bodyRootField.FieldType!=typeof(GameObject)||bodyBonesField==null||!typeof(IDictionary).IsAssignableFrom(bodyBonesField.FieldType)||bodyRightField==null||bodyLeftField==null||bodyPropField==null||bodyPropField.FieldType!=typeof(GameObject)||bodyArmsField==null||playerBodyField==null||playerBodyField.FieldType!=type){DisableBodyCompatibility("unsupported body layout");return;}
   bodyPropShownField=type.GetField("_propShown",BodyFlags);bodyPropShadowField=type.GetField("_propShadow",BodyFlags);
   if(bodyPropShownField!=null&&bodyPropShownField.FieldType!=typeof(bool))bodyPropShownField=null;if(bodyPropShadowField!=null&&bodyPropShadowField.FieldType!=typeof(bool))bodyPropShadowField=null;
   // WW finalizes its first-person arms after LateFoot; run once after the
   // local runner instead. Standalone older Apocaplayer keeps its body hook.
   var late=runner.GetMethod("LateTick",BodyFlags,null,Type.EmptyTypes,null);bodyTickHook=playerBodyField.IsStatic&&late!=null&&late.IsStatic&&late.ReturnType==typeof(void);
   try{harmony.Patch(bodyTickHook?late:method,postfix:new HarmonyMethod(typeof(Plugin),bodyTickHook?nameof(AfterBodyRunnerPose):nameof(AfterBodyPose)));bodyHook=true;InstallThirdPersonWeaponActions();Logger.LogInfo("Optional player body: custom weapon third-person models connected.");}
   catch(Exception error){DisableBodyCompatibility(error.GetBaseException().Message);}
  }
  void DisableBodyCompatibility(string reason){HideThirdPerson();RestoreBodyArms();if(bodyHookUnavailable)return;bodyHookUnavailable=true;Logger.LogWarning("Optional third-person weapon presentation unavailable: "+reason);}
  static void AfterBodyPose(object __instance,object[] __args){
   var owner=instance;if(ReferenceEquals(owner,null)||owner.bodyHookUnavailable)return;
   try{if(owner.IsPlayerBody(__instance))owner.UpdateThirdPerson(__instance,__args);}
   catch(Exception error){owner.DisableBodyCompatibility(error.GetBaseException().Message);}
  }
  static void AfterBodyRunnerPose(){
   var owner=instance;if(ReferenceEquals(owner,null)||owner.bodyHookUnavailable)return;
   try{var body=owner.playerBodyField.GetValue(null);if(body==null){owner.HideThirdPerson();owner.RestoreBodyArms();return;}owner.UpdateThirdPerson(body,null);}
   catch(Exception error){owner.DisableBodyCompatibility(error.GetBaseException().Message);}
  }
  bool IsPlayerBody(object body){
   if(body==null||playerBodyField==null)return false;
   if(playerBodyField.IsStatic)return ReferenceEquals(playerBodyField.GetValue(null),body);
   if(playerBodyRunner==null){if(Time.unscaledTime<nextPlayerBodyScan)return false;nextPlayerBodyScan=Time.unscaledTime+1f;var type=playerBodyField.DeclaringType;playerBodyRunner=Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(m=>m.GetType()==type&&m.gameObject.scene.IsValid());}
   return playerBodyRunner!=null&&ReferenceEquals(playerBodyField.GetValue(playerBodyRunner),body);
  }
  bool ActualThirdPersonActive(){
   if(thirdPersonOn==null){var type=BodyType("ThirdPerson");if(type==null)return false;var on=type.GetField("On",BodyFlags);var view=type.GetField("HasView",BodyFlags);var peek=type.GetField("Peek",BodyFlags);if(on==null||!on.IsStatic||on.FieldType!=typeof(bool)||view!=null&&(!view.IsStatic||view.FieldType!=typeof(bool))||peek!=null&&(!peek.IsStatic||peek.FieldType!=typeof(bool)))return false;thirdPersonOn=on;thirdPersonHasView=view;thirdPersonPeek=peek;}
   return (bool)thirdPersonOn.GetValue(null)&&(thirdPersonPeek==null||!(bool)thirdPersonPeek.GetValue(null))&&(thirdPersonHasView==null||(bool)thirdPersonHasView.GetValue(null));
  }
  void RestoreBodyArms(bool nativeAlreadyPosed=false){if(borrowedBodyArms!=null&&!(nativeAlreadyPosed&&bodyTickHook))borrowedBodyArms.enabled=borrowedBodyArmsEnabled;borrowedBodyArms=null;}
  void RestoreThirdPersonProp(){
   bool changed=false,shown=false,shadow=false;
   if(suppressedBody!=null&&suppressedProp!=null&&bodyPropShownField!=null&&bodyPropShadowField!=null&&bodyPropField.GetValue(suppressedBody) as GameObject==suppressedProp){shown=(bool)bodyPropShownField.GetValue(suppressedBody);shadow=(bool)bodyPropShadowField.GetValue(suppressedBody);changed=shown!=suppressedShown||shadow!=suppressedShadow;}
   // Native ShowProp may already have applied a new camera mode and cached it.
   // Preserve that new state, rather than reinstating the old third-person one.
   if(suppressedRenderers!=null)for(int i=0;i<suppressedRenderers.Length;i++){var renderer=suppressedRenderers[i];if(renderer!=null){renderer.enabled=changed?shown||shadow:suppressedEnabled[i];renderer.shadowCastingMode=changed?(shown?ShadowCastingMode.On:ShadowCastingMode.ShadowsOnly):suppressedShadows[i];}}
   suppressedProp=null;suppressedBody=null;suppressedRenderers=null;suppressedEnabled=null;suppressedShadows=null;
  }
  void HideThirdPerson(){ClearThirdPersonAttackPose();if(thirdModel!=null)thirdModel.SetActive(false);RestoreThirdPersonProp();}
  void CleanupThirdPerson(){HideThirdPerson();RestoreBodyArms();if(thirdModel!=null)Destroy(thirdModel);thirdModel=null;thirdSource=null;thirdSourceRight=null;thirdSourceLeft=null;thirdOwner=null;lastBody=null;playerBodyRunner=null;}
  void SuppressThirdPersonProp(GameObject prop,object body){
   if(prop!=suppressedProp){RestoreThirdPersonProp();if(prop==null)return;suppressedProp=prop;suppressedBody=body;suppressedShown=bodyPropShownField!=null&&(bool)bodyPropShownField.GetValue(body);suppressedShadow=bodyPropShadowField!=null&&(bool)bodyPropShadowField.GetValue(body);suppressedRenderers=prop.GetComponentsInChildren<Renderer>(true);suppressedEnabled=suppressedRenderers.Select(r=>r.enabled).ToArray();suppressedShadows=suppressedRenderers.Select(r=>r.shadowCastingMode).ToArray();}
   if(suppressedRenderers!=null)foreach(var renderer in suppressedRenderers)if(renderer!=null){renderer.enabled=false;renderer.shadowCastingMode=ShadowCastingMode.Off;}
  }
  // A local development build can provide another owned holding rig. No runtime
  // dependency or development asset is added to the standalone public build.
  partial void FindLocalThirdPersonHolding(ref Transform model,ref Transform rig,ref Transform view);
  bool ThirdPersonSource(out Transform model,out Transform rig,out Transform view){
   model=null;rig=null;view=null;
   if(enabledPreview&&preview!=null&&weapon!=null){model=preview.transform;rig=weapon;view=camera;}
   else if(activeFlame!=null&&activeFlame.Equipped&&activeFlame.gameObject.activeInHierarchy){model=activeFlame.HoldingModel;rig=activeFlame.HoldingRig;view=flamePlayer==null?null:flamePlayer.transform;}
   else FindLocalThirdPersonHolding(ref model,ref rig,ref view);
   return model!=null&&rig!=null&&rig.gameObject.activeInHierarchy&&view!=null&&CanPresentHeldWeapon(view);
  }
  static Transform ThirdPersonGrip(Transform rig,string side){
   var skin=PrivateFlameArm(rig,side);if(skin==null)return null;
   var bones=skin.bones;return bones.Length>3&&bones[3]!=null&&bones[3].IsChildOf(rig)?bones[3]:null;
  }
  bool BuildThirdPersonModel(object body,GameObject root,Transform source,Transform rig,Transform view){
   // Use the committed posed wrist bones. IK targets can retain the carrier's
   // older position after the approved whole-rig placement has been applied.
   var rg=ThirdPersonGrip(rig,"right");var lg=ThirdPersonGrip(rig,"left");if(enabledPreview){if(rg==null)rg=right;if(lg==null)lg=left;}if(rg==null||lg==null)return false;
   var scale=source.lossyScale;var gripRight=source.InverseTransformPoint(rg.position);var gripLeft=source.InverseTransformPoint(lg.position);var up=source.InverseTransformDirection(view.up);
   if(!Finite(scale)||Mathf.Abs(scale.x)<.0001f||Mathf.Abs(scale.y)<.0001f||Mathf.Abs(scale.z)<.0001f||!Finite(gripRight)||!Finite(gripLeft)||Vector3.Scale(gripLeft-gripRight,scale).sqrMagnitude<.0001f)return false;
   if(thirdModel!=null){thirdModel.SetActive(false);Destroy(thirdModel);}thirdModel=new GameObject(source.name+".ThirdPersonModel");thirdModel.SetActive(false);thirdModel.transform.SetPositionAndRotation(source.position,source.rotation);thirdModel.transform.localScale=scale;
   foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true)){
    var renderer=filter.GetComponent<MeshRenderer>();if(filter.sharedMesh==null||renderer==null)continue;
    var visual=new GameObject(filter.name);visual.transform.SetParent(thirdModel.transform,false);visual.transform.localPosition=source.InverseTransformPoint(filter.transform.position);visual.transform.localRotation=Quaternion.Inverse(source.rotation)*filter.transform.rotation;var s=filter.transform.lossyScale;visual.transform.localScale=new Vector3(s.x/scale.x,s.y/scale.y,s.z/scale.z);visual.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;var copy=visual.AddComponent<MeshRenderer>();copy.sharedMaterials=renderer.sharedMaterials;copy.receiveShadows=renderer.receiveShadows;
   }
   if(thirdModel.transform.childCount==0){Destroy(thirdModel);thirdModel=null;return false;}
   thirdModel.transform.SetParent(root.transform,true);thirdSource=source;thirdSourceRight=rg;thirdSourceLeft=lg;thirdOwner=body;thirdGripRight=gripRight;thirdGripLeft=gripLeft;thirdUp=up;thirdScale=scale;return true;
  }
  void UpdateThirdPerson(object body,object[] args){
   lastBody=body;bool third=samplingThirdPerson||ActualThirdPersonActive();if(!samplingThirdPerson)playerThirdPerson=third;
   Transform source,rig,view;if(!ThirdPersonSource(out source,out rig,out view)){HideThirdPerson();RestoreBodyArms(true);return;}
   if(bodyRootField==null||!bodyRootField.DeclaringType.IsInstanceOfType(body)){HideThirdPerson();return;}
   if(!samplingThirdPerson){var skin=bodyArmsField.GetValue(body) as SkinnedMeshRenderer;if(skin!=null){if(skin!=borrowedBodyArms){RestoreBodyArms();borrowedBodyArms=skin;borrowedBodyArmsEnabled=skin.enabled;}skin.enabled=false;}}
   if(!third){HideThirdPerson();return;}
   var root=bodyRootField.GetValue(body) as GameObject;var bones=bodyBonesField.GetValue(body) as IDictionary;if(root==null||!root.activeInHierarchy||bones==null){HideThirdPerson();return;}
   var rh=bones[bodyRightField.GetValue(null)] as Transform;var lh=bones[bodyLeftField.GetValue(null)] as Transform;if(rh==null||lh==null||!Finite(rh.position)||!Finite(lh.position)||(lh.position-rh.position).sqrMagnitude<.0001f){HideThirdPerson();return;}
   if(enabledPreview)BeginThirdPersonBodyPose(body,root,rh,lh);
   if(thirdModel==null||thirdSource!=source||!ReferenceEquals(thirdOwner,body))if(!BuildThirdPersonModel(body,root,source,rig,view)){HideThirdPerson();return;}
   thirdModel.SetActive(true);
   if(enabledPreview)CaptureThirdPersonBasePose();
   else {var rotation=Quaternion.LookRotation(lh.position-rh.position,root.transform.up)*Quaternion.Inverse(Quaternion.LookRotation(Vector3.Scale(thirdGripLeft-thirdGripRight,thirdScale),thirdUp));thirdModel.transform.rotation=rotation;FlameWorldScale(thirdModel.transform,thirdScale);thirdModel.transform.position=rh.position-rotation*Vector3.Scale(thirdGripRight,thirdScale);}
   if(!samplingThirdPerson)SuppressThirdPersonProp(bodyPropField.GetValue(body) as GameObject,body);
  }
 }
}
