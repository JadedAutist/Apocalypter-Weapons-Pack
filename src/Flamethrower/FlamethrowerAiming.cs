using System;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace ChainsawPrototype {
 public partial class Plugin {
  // Centres of the authored sight apertures, after WeaponObj mirrors OBJ X.
  // The front aperture sits slightly lower than the rear aperture.
  static readonly Vector3 FlameRearSight=new Vector3(.0121155f,.082309f,.090751f);
  static readonly Vector3 FlameFrontSight=new Vector3(.0121155f,.0781365f,.270142f);
  const float FlameAimDefaultIn=.25f,FlameAimDefaultOut=.22f;
  PlayMakerFSM flameNativeAimHold,flameNativeAimToggle;
  Transform flameAimCarrier;
  float flameAimProgress,flameAimBlend,flameAimStart,flameAimInTime=.18f,flameAimOutTime=.18f,flameAimDepth=-.036f;
  string flameAimInEase="easeInOutSine",flameAimOutEase="easeInOutSine";
  bool flameAimRequested,flameAimBound,flameAimToggleLatched,flameAimToggleMode;
  int flameAimFrame=-1;

  void ResetFlamethrowerAim(){
   flameAimProgress=flameAimBlend=flameAimStart=0f;flameAimRequested=false;flameAimFrame=-1;
   flameAimToggleLatched=flameAimToggleMode=false;
   flameNativeAimHold=flameNativeAimToggle=null;flameAimCarrier=null;flameAimBound=false;
  }
  static iTweenMoveTo FlameNativeAimMove(PlayMakerFSM fsm,string state){
   if(fsm==null)return null;
   return fsm.Fsm.States.Where(s=>s.Name==state).SelectMany(s=>s.Actions).OfType<iTweenMoveTo>().FirstOrDefault();
  }
  static float FlameNativeAimDuration(iTweenMoveTo move,float distance){
   if(move==null)return .18f;
   // Match iTweenMoveTo's native speed-over-time precedence. Only read the
   // existing action; never enter its state or move the stock rifle.
   float duration=move.speed!=null&&!move.speed.IsNone&&move.speed.Value>0f?distance/move.speed.Value:
    move.time!=null&&!move.time.IsNone?move.time.Value:1f;
   return Finite(duration)&&duration>0f?Mathf.Clamp(duration,.02f,1f):.18f;
  }
  void BindFlamethrowerAim(){
   if(flamePlayer==null)return;
   if(flameNativeAimHold==null||flameNativeAimToggle==null){
    var scripts=flamePlayer.GetComponents<PlayMakerFSM>();
    flameNativeAimHold=scripts.FirstOrDefault(f=>f.FsmName=="AimDownSIghts_Hold");
    flameNativeAimToggle=scripts.FirstOrDefault(f=>f.FsmName=="AimDownSIghts_Toggle");
   }
   if(flameAimBound&&flameAimCarrier==flameCarrier)return;
   var native=flameCarrier==null?null:flameCarrier.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="AimDownSights");
   var enter=FlameNativeAimMove(native,"on anim");var leave=FlameNativeAimMove(native,"off anim");
   if(enter==null||leave==null)return;
   var delta=enter.vectorPosition.Value-leave.vectorPosition.Value;
   if(!Finite(delta))return;
   flameAimDepth=Mathf.Clamp(delta.z,-.12f,.12f);
   flameAimInTime=FlameNativeAimDuration(enter,delta.magnitude);
   // Retain native alignment/easing metadata; transition duration is now the
   // player's live per-weapon setting rather than the rifle's very short move.
   flameAimOutTime=FlameNativeAimDuration(leave,delta.magnitude);
   flameAimInEase=enter.easeType.ToString();flameAimOutEase=leave.easeType.ToString();flameAimCarrier=flameCarrier;flameAimBound=true;
   Logger.LogInfo("Flamethrower ADS reads native Aim Down Sights hold/toggle controls and easing (native timing "+flameAimInTime.ToString("F3")+" / "+flameAimOutTime.ToString("F3")+" s); live private timing defaults to 0.25 / 0.22 s. Native camera zoom remains independent.");
  }
  float FlameAimTransitionDuration(bool entering){
   return entering?FlameTuningValue(flameAimInDuration,FlameAimDefaultIn,.05f,.8f):FlameTuningValue(flameAimOutDuration,FlameAimDefaultOut,.05f,.8f);
  }
  static float FlameAimEase(float t,string ease){
   t=Mathf.Clamp01(t);
   switch(ease){
    case "linear":return t;
    case "easeInQuad":return t*t;
    case "easeOutQuad":return t*(2f-t);
    case "easeInOutQuad":return t<.5f?2f*t*t:1f-2f*(1f-t)*(1f-t);
    case "easeInCubic":return t*t*t;
    case "easeOutCubic":return 1f-(1f-t)*(1f-t)*(1f-t);
    case "easeInOutCubic":return t<.5f?4f*t*t*t:1f-4f*(1f-t)*(1f-t)*(1f-t);
    case "easeInSine":return 1f-Mathf.Cos(t*Mathf.PI*.5f);
    case "easeOutSine":return Mathf.Sin(t*Mathf.PI*.5f);
    case "easeInOutSine":return .5f-.5f*Mathf.Cos(Mathf.PI*t);
    case "easeInExpo":return t<=0f?0f:Mathf.Pow(2f,10f*(t-1f));
    case "easeOutExpo":return t>=1f?1f:1f-Mathf.Pow(2f,-10f*t);
    case "easeInOutExpo":return t<=0f?0f:t>=1f?1f:t<.5f?.5f*Mathf.Pow(2f,20f*t-10f):1f-.5f*Mathf.Pow(2f,-20f*t+10f);
    default:return t*t*(3f-2f*t);
   }
  }
  void SampleFlamethrowerAim(){
   // LateUpdate and pre-cull reconstruct the same pose. Advance its input and
   // transition once per frame so extra render cameras cannot speed up ADS.
   if(flameAimFrame==Time.frameCount)return;flameAimFrame=Time.frameCount;
   BindFlamethrowerAim();
   bool usable=activeFlame!=null&&activeFlame.Equipped&&activeFlame.gameObject.activeInHierarchy&&Time.timeScale>0f&&!Cursor.visible&&!ActualThirdPersonActive();
   if(!usable){flameAimProgress=flameAimBlend=flameAimStart=0f;flameAimRequested=flameAimToggleLatched=false;return;}
   // Use the same remappable input as the native GetButton/GetButtonDown
   // actions and observe the enabled FSM for the game's Hold vs Toggle mode.
   // The private weapon's latch cannot become stuck in a donor reload state.
   bool toggle=flameNativeAimToggle!=null&&flameNativeAimToggle.enabled;
   if(toggle!=flameAimToggleMode){flameAimToggleMode=toggle;flameAimToggleLatched=false;}
   bool aim;
   if(toggle){if(InsaneSystems.InputManager.InputController.GetKeyActionIsDown("Aim Down Sights"))flameAimToggleLatched=!flameAimToggleLatched;aim=flameAimToggleLatched;}
   else aim=InsaneSystems.InputManager.InputController.GetKeyActionIsActive("Aim Down Sights");
   if(aim!=flameAimRequested){flameAimRequested=aim;flameAimStart=flameAimBlend;flameAimProgress=0f;}
   float duration=FlameAimTransitionDuration(aim);
   flameAimProgress=Mathf.Min(1f,flameAimProgress+Mathf.Min(Time.deltaTime,.1f)/duration);
   // On direction changes the transition starts from the current position.
   // A state-specific start value keeps native easing from causing a jump.
   flameAimBlend=Mathf.Lerp(flameAimStart,aim?1f:0f,FlameAimEase(flameAimProgress,aim?flameAimInEase:flameAimOutEase));
  }
  void ApplyFlamethrowerAim(FlamethrowerItem marker,Transform view,Quaternion swayRotation,Vector3 swayShift){
   if(flameAimBlend<=0f)return;
   var barrel=marker.HoldingModel.Find("Flamethrower");if(barrel==null||!barrel.IsChildOf(marker.HoldingRig))return;
   var rear=barrel.TransformPoint(FlameRearSight);var front=barrel.TransformPoint(FlameFrontSight);
   var undoSway=Quaternion.Inverse(swayRotation);
   var rearLocal=view.InverseTransformPoint(view.position+undoSway*(rear-view.position-swayShift));
   var direction=view.InverseTransformDirection(undoSway*(front-rear));
   var up=view.InverseTransformDirection(undoSway*barrel.up);
   if(!Finite(rearLocal)||!Finite(direction)||direction.sqrMagnitude<.000001f||!Finite(up))return;
   var aimTurn=Quaternion.Slerp(Quaternion.identity,Quaternion.Inverse(Quaternion.LookRotation(direction,up)),flameAimBlend);
   float depth=Mathf.Clamp(rearLocal.z+flameAimDepth,.24f,.65f);
   var aimedRear=Vector3.Lerp(rearLocal,new Vector3(0f,0f,depth),flameAimBlend);
   var worldTurn=swayRotation*view.rotation*aimTurn*Quaternion.Inverse(view.rotation)*undoSway;
   var worldRear=view.position+swayRotation*(view.TransformPoint(aimedRear)-view.position)+swayShift;
   // One rigid transform moves the entire private model, skeleton and both
   // grips together. The saved hip pose and authored scale stay intact.
   var rig=marker.HoldingRig;
   rig.SetPositionAndRotation(worldRear+worldTurn*(rig.position-rear),worldTurn*rig.rotation);
  }
 }
}
