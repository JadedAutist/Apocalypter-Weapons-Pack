using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ChainsawPrototype {
 public partial class Plugin {
  Graphic flameNativeCrosshair,flameCrosshair;
  float flameCrosshairRetry;

  internal void UpdateFlamethrowerCrosshair(){
   bool visible=activeFlame!=null&&activeFlame.Equipped&&activeFlame.gameObject.activeInHierarchy&&Time.timeScale>0f&&Cursor.lockState==CursorLockMode.Locked&&!Cursor.visible;
   if(flameNativeCrosshair==null&&Time.unscaledTime>=flameCrosshairRetry){
    flameCrosshairRetry=Time.unscaledTime+2f;
    flameNativeCrosshair=Resources.FindObjectsOfTypeAll<Graphic>().FirstOrDefault(g=>g.name=="MouseCrosshair"&&g.gameObject.scene.IsValid());
   }
   if(flameNativeCrosshair==null){if(flameCrosshair!=null)flameCrosshair.gameObject.SetActive(false);return;}
   if(flameCrosshair!=null&&flameCrosshair.transform.parent!=flameNativeCrosshair.transform.parent){Destroy(flameCrosshair.gameObject);flameCrosshair=null;}
   if(visible&&flameCrosshair==null){
    var reticle=new GameObject("Flamethrower.NativeReticle",typeof(RectTransform),typeof(CanvasRenderer));
    reticle.transform.SetParent(flameNativeCrosshair.transform.parent,false);
    reticle.transform.SetSiblingIndex(flameNativeCrosshair.transform.GetSiblingIndex()+1);
    if(flameNativeCrosshair is RawImage)flameCrosshair=reticle.AddComponent<RawImage>();else flameCrosshair=reticle.AddComponent<Image>();
    flameCrosshair.raycastTarget=false;
   }
   if(flameCrosshair==null)return;
   // Share the game's HUD canvas/groups so HUD scale, hiding and native style
   // continue to apply. The rifle carrier can hide its source without hiding us.
   flameCrosshair.color=flameNativeCrosshair.color;flameCrosshair.material=flameNativeCrosshair.material;
   var sourceImage=flameNativeCrosshair as Image;var image=flameCrosshair as Image;
   if(sourceImage!=null&&image!=null){image.sprite=sourceImage.sprite;image.type=sourceImage.type;image.preserveAspect=sourceImage.preserveAspect;image.fillCenter=sourceImage.fillCenter;image.fillMethod=sourceImage.fillMethod;image.fillAmount=sourceImage.fillAmount;image.fillClockwise=sourceImage.fillClockwise;image.fillOrigin=sourceImage.fillOrigin;}
   var sourceRaw=flameNativeCrosshair as RawImage;var raw=flameCrosshair as RawImage;
   if(sourceRaw!=null&&raw!=null){raw.texture=sourceRaw.texture;raw.uvRect=sourceRaw.uvRect;}
   var sourceRect=flameNativeCrosshair.rectTransform;var rect=flameCrosshair.rectTransform;
   rect.anchorMin=sourceRect.anchorMin;rect.anchorMax=sourceRect.anchorMax;rect.pivot=sourceRect.pivot;rect.sizeDelta=sourceRect.sizeDelta;rect.anchoredPosition=sourceRect.anchoredPosition;rect.localScale=sourceRect.localScale;rect.localRotation=sourceRect.localRotation;
   flameCrosshair.gameObject.SetActive(visible);
  }
  void CleanupFlamethrowerCrosshair(){if(flameCrosshair!=null)Destroy(flameCrosshair.gameObject);flameCrosshair=null;flameNativeCrosshair=null;}
 }
}

