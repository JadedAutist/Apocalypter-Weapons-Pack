using System;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.UI;
namespace ChainsawPrototype {
 public partial class Plugin {
  AudioClip nativeSurfaceHit,nativeActorHit;int nativeActorLayer=-1;GameObject standardMeleeEffect,nativeActorEffect,crosshairCanvas;Image sawCrosshair;float nativeHitRetry;bool warnedNativeHit;
  void FindStandardMeleeEffect(){
   if((standardMeleeEffect!=null&&nativeActorEffect!=null&&nativeSurfaceHit!=null&&nativeActorHit!=null)||Time.unscaledTime<nativeHitRetry)return;nativeHitRetry=Time.unscaledTime+2;
   foreach(var attack in Resources.FindObjectsOfTypeAll<PlayMakerFSM>()){
    if(attack.FsmName!="Attack"||attack.gameObject.name!="pipe_wrench")continue;
    foreach(var state in attack.Fsm.States)foreach(var action in state.Actions){
     if(action.GetType().Name=="SetAudioClip"&&(state.Name=="actorHitSound"||state.Name=="getLayer")){var field=action.GetType().GetField("audioClip");var value=field==null?null:field.GetValue(action) as FsmObject;var clip=value==null?null:value.Value as AudioClip;if(state.Name=="actorHitSound")nativeActorHit=clip;else nativeSurfaceHit=clip;}
     if(state.Name=="getLayer"&&action.GetType().Name=="IntCompare"){var field=action.GetType().GetField("integer2");var value=field==null?null:field.GetValue(action) as FsmInt;if(value!=null)nativeActorLayer=value.Value;}
     // The wrench changes HitEffect's prefab before sending its impact event.
     // Reading CreateObject alone returns only the default ground-hit effect.
     if(action.GetType().Name=="SetFsmGameObject"&&(state.Name=="actorHitSound"||state.Name=="getLayer")){
      var nameField=action.GetType().GetField("variableName");var variable=nameField==null?null:nameField.GetValue(action) as FsmString;
      if(variable==null||variable.Value!="HitEffect")continue;
      var field=action.GetType().GetField("setValue");var value=field==null?null:field.GetValue(action) as FsmGameObject;
      if(value==null||value.Value==null)continue;
      if(state.Name=="actorHitSound")nativeActorEffect=value.Value;else standardMeleeEffect=value.Value;
     }
    }
   }
   foreach(var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>()){
    if(standardMeleeEffect!=null)break;
    if(fsm.FsmName!="HitEffect"||fsm.gameObject.name!="pipe_wrench")continue;
    foreach(var state in fsm.Fsm.States)foreach(var action in state.Actions){if(action.GetType().Name!="CreateObject")continue;var field=action.GetType().GetField("gameObject",BindingFlags.Public|BindingFlags.Instance);var obj=field==null?null:field.GetValue(action) as FsmGameObject;if(obj!=null&&obj.Value!=null)standardMeleeEffect=obj.Value;}
   }
   if(standardMeleeEffect==null||nativeActorEffect==null)foreach(var source in Resources.FindObjectsOfTypeAll<Transform>()){
    if(standardMeleeEffect==null&&source.name=="MeleeHit_Effect")standardMeleeEffect=source.gameObject;
    if(nativeActorEffect==null&&source.name=="BloodHit_Effect")nativeActorEffect=source.gameObject;
   }
  }
  void StandardMeleeHit(Vector3 point,Vector3 normal,ChainsawSurface kind){
   FindStandardMeleeEffect();bool actor=kind==ChainsawSurface.Flesh;
   var sound=actor?nativeActorHit:nativeSurfaceHit;if(sound==null)sound=nativeSurfaceHit??nativeActorHit??impactClip;
   if(sound!=null&&sawHit!=null){sawHit.transform.position=point;sawHit.pitch=UnityEngine.Random.Range(.9f,1.1f);sawHit.volume=1f;sawHit.PlayOneShot(sound,.9f);}
   var prefab=actor?nativeActorEffect:standardMeleeEffect;
   if(prefab!=null){var effect=Instantiate(prefab,point+normal*.005f,Quaternion.LookRotation(normal));effect.name=actor?"Chainsaw.NativeBloodHit":"Chainsaw.NativeMeleeHit";effect.SetActive(true);return;}
   if(!warnedNativeHit){Logger.LogWarning("Native melee "+(actor?"blood":"surface")+" impact prefab was unavailable in this scene.");warnedNativeHit=true;}
  }
  void UpdateCrosshair(){
   bool visible=enabledPreview&&Time.timeScale>0f&&Cursor.lockState==CursorLockMode.Locked;
   if(visible&&crosshairCanvas==null){
    var native=Resources.FindObjectsOfTypeAll<Graphic>().FirstOrDefault(i=>i.gameObject.name=="MouseCrosshair");
    crosshairCanvas=new GameObject("Chainsaw.Crosshair",typeof(RectTransform),typeof(Canvas));var canvas=crosshairCanvas.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
    var sourceImage=native as Image;var sourceRaw=native as RawImage;
    var reticle=new GameObject("Chainsaw.NativeReticle",typeof(RectTransform),typeof(CanvasRenderer));reticle.transform.SetParent(crosshairCanvas.transform,false);
    Graphic graphic;Vector2 size=new Vector2(4,4);
    if(sourceImage!=null&&sourceImage.sprite!=null){sawCrosshair=reticle.AddComponent<Image>();sawCrosshair.sprite=sourceImage.sprite;sawCrosshair.preserveAspect=true;graphic=sawCrosshair;size=sourceImage.rectTransform.rect.size;}
    else if(sourceRaw!=null&&sourceRaw.texture!=null){var raw=reticle.AddComponent<RawImage>();raw.texture=sourceRaw.texture;raw.uvRect=sourceRaw.uvRect;graphic=raw;size=sourceRaw.rectTransform.rect.size;}
    else {sawCrosshair=reticle.AddComponent<Image>();graphic=sawCrosshair;}
    // Native UI can be faded out while the rifle carrier is selected. Use an opaque reticle.
    graphic.color=Color.white;graphic.raycastTarget=false;
    var rect=reticle.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;
    rect.sizeDelta=new Vector2(Mathf.Clamp(size.x,3,48),Mathf.Clamp(size.y,3,48));
   }
   if(crosshairCanvas!=null)crosshairCanvas.SetActive(visible);
  }
 }
}
