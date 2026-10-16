using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  Texture2D chainsawIcon;Sprite chainsawSprite;bool bodyHook;GameObject thirdModel;object thirdOwner;GameObject suppressedProp;
  ConfigEntry<float>[] thirdMove,thirdTurn;
  void LoadIcon(){var path=Path.Combine(Path.GetDirectoryName(Info.Location),"Chainsaw-icon.png");chainsawIcon=new Texture2D(2,2);ImageConversion.LoadImage(chainsawIcon,File.ReadAllBytes(path));chainsawIcon.name="Chainsaw.InventoryIcon";chainsawIcon.hideFlags=HideFlags.DontUnloadUnusedAsset;chainsawSprite=Sprite.Create(chainsawIcon,new Rect(0,0,chainsawIcon.width,chainsawIcon.height),new Vector2(.5f,.5f));chainsawSprite.hideFlags=HideFlags.DontUnloadUnusedAsset;
   thirdMove=BindAxes("Third person - position","Adjust the third person prop relative to the right hand, in metres.",.4f);thirdTurn=BindAxes("Third person - rotation","Adjust the third person prop rotation in degrees.",180f);
  }
  void SetItemIcon(GameObject item){
   foreach(var fsm in item.GetComponents<PlayMakerFSM>()){
    foreach(var v in fsm.FsmVariables.TextureVariables)if(v.Value!=null || v.Name.ToLowerInvariant().Contains("icon"))v.Value=chainsawIcon;
    foreach(var v in fsm.FsmVariables.ObjectVariables)if(v.Value is Sprite)v.Value=chainsawSprite;else if(v.Value is Texture)v.Value=chainsawIcon;
    foreach(var state in fsm.Fsm.States)foreach(var action in state.Actions)foreach(var field in action.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance)){
     var value=field.GetValue(action);var texture=value as FsmTexture;if(texture!=null && texture.Value!=null)texture.Value=chainsawIcon;
     var obj=value as FsmObject;if(obj!=null && obj.Value is Sprite)obj.Value=chainsawSprite;else if(obj!=null && obj.Value is Texture)obj.Value=chainsawIcon;
    }
   }
  }
  void InstallBodyHook(){if(bodyHook)return;var type=AccessTools.TypeByName("Apocaplayer.Body");if(type==null)return;var method=AccessTools.Method(type,"LateFoot");if(method==null)return;harmony.Patch(method,postfix:new HarmonyMethod(typeof(Plugin),nameof(AfterBodyPose)));bodyHook=true;Logger.LogInfo("Third person Chainsaw prop hook installed.");}
  static void AfterBodyPose(object __instance,object[] __args){if(ReferenceEquals(instance,null)||!instance.IsPlayerBody(__instance))return;if(instance.activeFlame!=null)instance.UpdateFlameBody(__instance);if(!instance.enabledPreview&&!instance.samplingThirdPerson){instance.lastBody=__instance;return;}instance.UpdateThirdPerson(__instance,__args);}
  void HideThirdPerson(){if(thirdModel!=null)thirdModel.SetActive(false);if(suppressedProp!=null)suppressedProp.SetActive(true);suppressedProp=null;}
  MonoBehaviour playerBodyRunner;System.Reflection.FieldInfo playerBodyField;float nextPlayerBodyScan;
  bool IsPlayerBody(object body){
   if(playerBodyRunner==null){
    if(Time.unscaledTime<nextPlayerBodyScan)return false;nextPlayerBodyScan=Time.unscaledTime+1f;
    var runnerType=AccessTools.TypeByName("Apocaplayer.Runner");if(runnerType==null)return false;
    playerBodyRunner=Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(m=>m.GetType()==runnerType&&m.gameObject.scene.IsValid());playerBodyField=AccessTools.Field(runnerType,"_body");
   }
   return playerBodyRunner!=null && playerBodyField!=null && ReferenceEquals(playerBodyField.GetValue(playerBodyRunner),body);
  }
  SkinnedMeshRenderer borrowedBodyArms;bool borrowedBodyArmsEnabled;
  void RestoreBodyArms(){if(borrowedBodyArms!=null)borrowedBodyArms.enabled=borrowedBodyArmsEnabled;borrowedBodyArms=null;}
  System.Reflection.FieldInfo thirdPersonOn,thirdPersonHasView;bool resolvedViewFields;
  bool ActualThirdPersonActive(){
   if(!resolvedViewFields){var type=AccessTools.TypeByName("Apocaplayer.ThirdPerson");if(type!=null){thirdPersonOn=AccessTools.Field(type,"On");thirdPersonHasView=AccessTools.Field(type,"HasView");resolvedViewFields=true;}}
   return thirdPersonOn!=null && (bool)thirdPersonOn.GetValue(null) && (thirdPersonHasView==null || (bool)thirdPersonHasView.GetValue(null));
  }
  object lastBody;bool samplingThirdPerson,playerThirdPerson;
  void UpdateThirdPerson(object body,object[] args){
   lastBody=body;
   if(enabledPreview && !samplingThirdPerson){var bodyArms=AccessTools.Field(body.GetType(),"_fpArms").GetValue(body) as SkinnedMeshRenderer;if(bodyArms!=null){if(bodyArms!=borrowedBodyArms){RestoreBodyArms();borrowedBodyArms=bodyArms;borrowedBodyArmsEnabled=bodyArms.enabled;}bodyArms.enabled=false;}}
   bool third=samplingThirdPerson || ActualThirdPersonActive();if(!samplingThirdPerson)playerThirdPerson=third;
   if(enabledPreview && !samplingThirdPerson){if(preview!=null)preview.GetComponent<MeshRenderer>().enabled=!third;if(weapon!=null)foreach(var arm in weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true))arm.enabled=!third;}
   if(!enabledPreview || !third){HideThirdPerson();return;}
   var t=body.GetType();var bones=AccessTools.Field(t,"Bones").GetValue(body) as IDictionary;var root=AccessTools.Field(t,"Root").GetValue(body) as GameObject;if(bones==null || root==null)return;
   var rhName=AccessTools.Field(t,"RH").GetValue(null) as string;var lhName=AccessTools.Field(t,"LH").GetValue(null) as string;
   var rh=bones[rhName] as Transform;var lh=bones[lhName] as Transform;if(rh==null||lh==null)return;
   if(thirdModel==null || !ReferenceEquals(thirdOwner,body)){if(thirdModel!=null)Destroy(thirdModel);thirdModel=new GameObject("Chainsaw.ThirdPersonModel");thirdModel.AddComponent<MeshFilter>().sharedMesh=heldMesh;thirdModel.AddComponent<MeshRenderer>().sharedMaterial=chainsawMaterial;thirdOwner=body;}
   var prop=AccessTools.Field(t,"_prop").GetValue(body) as GameObject;if(!samplingThirdPerson && prop!=null && prop!=thirdModel){suppressedProp=prop;prop.SetActive(false);}
   Vector3 source=data.leftPosition-data.rightPosition,destination=lh.position-rh.position;
   if(source.sqrMagnitude<.0001f||destination.sqrMagnitude<.0001f)return;
   Quaternion alignment=Quaternion.LookRotation(destination,root.transform.up)*Quaternion.Inverse(Quaternion.LookRotation(source,Vector3.up));
   Quaternion rotation=alignment*Quaternion.Euler(Values(thirdTurn));float scale=PreviewValue(poseScale);
   thirdModel.transform.rotation=rotation;thirdModel.transform.localScale=Vector3.one*scale;
   thirdModel.transform.position=rh.position+rh.TransformVector(Values(thirdMove))-rotation*(data.rightPosition*scale);thirdModel.SetActive(true);
  }
 }
}
