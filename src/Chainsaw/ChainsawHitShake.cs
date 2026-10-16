using System;
using System.Reflection;
using BepInEx.Configuration;
using CameraMovementPro;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using NativeCameraMovement=CameraMovementPro.CameraMovementController;

namespace ChainsawPrototype {
 public partial class Plugin {
  NativeCameraMovement meleeShakeCamera;
  NativeCameraMovement.MovementPreset meleeShakePreset,cuttingShakePreset;
  string chainsawShakeLayer;
  bool chainsawShakeContinuous;
  bool chainsawSwingShakePlayed;
  ConfigEntry<float> chainsawLmbShakeStrength,chainsawRmbVibrationStrength;
  float meleeShakeIntensity=1f,meleeShakeSpeed=1f,nextMeleeShakeSearch,nextMeleeShakeWarning;

  void SetupChainsawHitShake(){
   chainsawLmbShakeStrength=FixedSetting("Chainsaw hit effects","LMB hit shake strength",1f,new ConfigDescription("Screen shake strength for fueled left-click swings, including air swings. Multiplier: 0 disables it, 1 uses normal native wrench strength, 2 doubles it.",new AcceptableValueRange<float>(0f,2f)));
   chainsawRmbVibrationStrength=FixedSetting("Chainsaw hit effects","RMB vibration strength",1f,new ConfigDescription("Screen vibration strength for fueled right-click cutting, including cutting air. Multiplier: 0 disables it, 1 uses the current small vibration (22% native wrench strength), 2 doubles it.",new AcceptableValueRange<float>(0f,2f)));
  }
  float ChainsawShakeMultiplier(bool continuous){
   var setting=continuous?chainsawRmbVibrationStrength:chainsawLmbShakeStrength;
   float value=setting==null?1f:setting.Value;
   return Finite(value)?Mathf.Clamp(value,0f,2f):1f;
  }

  void BeginChainsawSwingShake(){chainsawSwingShakePlayed=false;}
  void UpdateChainsawSwingShake(float time){
   // Fuel was paid when this swing began. Its normal contact phase supplies
   // one shake even when the blade hits air or that payment emptied the tank.
   if(!swinging||chainsawSwingShakePlayed||!Finite(time)||time<hitStart)return;
   chainsawSwingShakePlayed=true;
   PlayChainsawHitShake(false);
  }

  // The current game's wrench calls this controller from its Animation FSM.
  // Read its parameters and clone its preset; never activate a stock weapon FSM.
  bool FindNativeMeleeShake(){
   if(meleeShakeCamera!=null&&meleeShakeCamera.isActiveAndEnabled&&meleeShakePreset!=null&&cuttingShakePreset!=null)return true;
   if(Time.unscaledTime<nextMeleeShakeSearch)return false;
   nextMeleeShakeSearch=Time.unscaledTime+2f;
   meleeShakeCamera=null;meleeShakePreset=null;cuttingShakePreset=null;chainsawShakeLayer=null;
   foreach(var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>()){
    if(fsm==null||fsm.FsmName!="Animation"||fsm.gameObject.name!="pipe_wrench"||!fsm.gameObject.scene.IsValid())continue;
    // Exclude physical items and independent editor preview copies.
    var view=fsm.GetComponentInParent<Camera>();
    if(view==null||camera==null||view.transform!=camera)continue;
    foreach(var state in fsm.Fsm.States)foreach(var action in state.Actions){
     var call=action as CallMethod;
     if(call==null||call.methodName==null||call.methodName.Value!="PlayMovement"||call.parameters==null||call.parameters.Length!=4)continue;
     string name=call.parameters[0].GetValue() as string;
     var target=call.behaviour==null?null:call.behaviour.Value as NativeCameraMovement;
     if(string.IsNullOrEmpty(name)||target==null||!target.isActiveAndEnabled)continue;
     var targetViewField=target.GetType().GetField("targetCamera",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
     var targetView=targetViewField==null?null:targetViewField.GetValue(target) as Camera;
     if(targetView!=null&&targetView.transform!=camera)continue;
     object strengthValue=call.parameters[1].GetValue(),speedValue=call.parameters[2].GetValue();
     if(!(strengthValue is float)||!(speedValue is float))continue;
     float strength=(float)strengthValue,speed=(float)speedValue;
     if(!Finite(strength)||!Finite(speed)||strength<0f||speed<=0f)continue;
     var custom=target.GetCustomPreset(name);
     var source=custom==null?PresetLibrary.GetPreset(name):custom.preset;
     if(source==null)continue;
     // Clone() deep-copies curves through native JsonUtility; edits stay private.
     var full=source.Clone();var small=MakeCuttingShakePreset(source);
     if(full==null||small==null)continue;
     meleeShakeCamera=target;meleeShakePreset=full;cuttingShakePreset=small;
     meleeShakeIntensity=strength;meleeShakeSpeed=speed;
     Logger.LogInfo("Chainsaw attack shake uses native wrench '"+name+"': intensity="+strength+", speed="+speed+"; right-click cutting vibration is 22% intensity with a 0.105 s maximum envelope, including air attacks.");
     return true;
    }
   }
   return false;
  }

  static NativeCameraMovement.MovementPreset MakeCuttingShakePreset(NativeCameraMovement.MovementPreset source){
   var result=source.Clone();if(result==null)return null;
   result.presetName="Chainsaw.ContactVibration";
   result.duration=.08f;result.fadeInTime=0f;result.fadeOutTime=.025f;
   result.positionFrequency=VibrationFrequency(result.positionFrequency);
   result.rotationFrequency=VibrationFrequency(result.rotationFrequency);
   result.enableZoom=false;result.zoomAmplitude=0f;
   return result;
  }
  static Vector3 VibrationFrequency(Vector3 native){
   return new Vector3(Mathf.Clamp(Mathf.Abs(native.x)*2f,90f,160f),Mathf.Clamp(Mathf.Abs(native.y)*2f,90f,160f),Mathf.Clamp(Mathf.Abs(native.z)*2f,90f,160f));
  }

  void PlayChainsawHitShake(bool continuous){
   if(!enabledPreview||camera==null||Time.timeScale<=0f||Cursor.lockState!=CursorLockMode.Locked)return;
   float multiplier=ChainsawShakeMultiplier(continuous);
   if(multiplier<=0f){CleanupChainsawHitShake();return;}
   try{
    if(!FindNativeMeleeShake())return;
    var previous=string.IsNullOrEmpty(chainsawShakeLayer)?null:meleeShakeCamera.GetLayer(chainsawShakeLayer);
    // Catch-up contacts and high cutting rates cannot stack camera impulses.
    // The native controller removes the short layer and restores camera offsets.
    if(continuous&&previous!=null)return;
    if(previous!=null)meleeShakeCamera.StopMovement(chainsawShakeLayer,0f);
    chainsawShakeLayer=meleeShakeCamera.PlayMovement(continuous?cuttingShakePreset:meleeShakePreset,meleeShakeIntensity*(continuous?.22f:1f)*multiplier,continuous?1f:meleeShakeSpeed,false);
    chainsawShakeContinuous=continuous;
   }catch(Exception error){ReportMeleeShakeFailure(error);}
  }

  void UpdateChainsawHitShake(){
   if(meleeShakeCamera==null){chainsawShakeLayer=null;return;}
   if(enabledPreview&&camera!=null&&Time.timeScale>0f&&Cursor.lockState==CursorLockMode.Locked&&meleeShakeCamera.isActiveAndEnabled&&ChainsawShakeMultiplier(chainsawShakeContinuous)>0f)return;
   CleanupChainsawHitShake();
  }
  void CleanupChainsawHitShake(){
   try{if(meleeShakeCamera!=null&&!string.IsNullOrEmpty(chainsawShakeLayer)&&meleeShakeCamera.GetLayer(chainsawShakeLayer)!=null)meleeShakeCamera.StopMovement(chainsawShakeLayer,0f);}
   catch(Exception error){ReportMeleeShakeFailure(error);}
   chainsawShakeLayer=null;
  }
  void ReportMeleeShakeFailure(Exception error){
   meleeShakeCamera=null;meleeShakePreset=null;cuttingShakePreset=null;chainsawShakeLayer=null;nextMeleeShakeSearch=Time.unscaledTime+2f;
   if(Time.unscaledTime<nextMeleeShakeWarning)return;
   nextMeleeShakeWarning=Time.unscaledTime+10f;
   Logger.LogWarning("Native chainsaw hit shake will retry: "+error.Message);
  }
 }
}
