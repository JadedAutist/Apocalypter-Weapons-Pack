using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ChainsawPrototype {
 public partial class Plugin {
  Graphic sawNativeCrosshair,sawReticle;
  float sawCrosshairRetry;

  void UpdateCrosshair(){SyncChainsawCrosshair(enabledPreview&&camera!=null&&CanPresentHeldWeapon(camera)&&Time.timeScale>0f&&Cursor.lockState==CursorLockMode.Locked&&!Cursor.visible);}
  void SyncChainsawCrosshair(bool visible){
   if(sawNativeCrosshair==null&&Time.unscaledTime>=sawCrosshairRetry){
    sawCrosshairRetry=Time.unscaledTime+2f;
    sawNativeCrosshair=Resources.FindObjectsOfTypeAll<Graphic>().FirstOrDefault(g=>g.name=="MouseCrosshair"&&g.gameObject.scene.IsValid());
   }
   if(sawNativeCrosshair==null){if(sawReticle!=null)sawReticle.gameObject.SetActive(false);return;}
   if(sawReticle!=null&&sawReticle.transform.parent!=sawNativeCrosshair.transform.parent){Destroy(sawReticle.gameObject);sawReticle=null;}
   if(visible&&sawReticle==null){
    var reticle=new GameObject("Chainsaw.NativeReticle",typeof(RectTransform),typeof(CanvasRenderer));
    reticle.transform.SetParent(sawNativeCrosshair.transform.parent,false);
    reticle.transform.SetSiblingIndex(sawNativeCrosshair.transform.GetSiblingIndex()+1);
    if(sawNativeCrosshair is RawImage)sawReticle=reticle.AddComponent<RawImage>();else sawReticle=reticle.AddComponent<Image>();
    sawReticle.raycastTarget=false;
   }
   if(sawReticle==null)return;
   // Share the game's HUD canvas/groups so HUD scale, hiding and native style
   // continue to apply. Native third-person crosshair movement stays authoritative.
   sawReticle.color=sawNativeCrosshair.color;sawReticle.material=sawNativeCrosshair.material;
   var sourceImage=sawNativeCrosshair as Image;var image=sawReticle as Image;
   if(sourceImage!=null&&image!=null){image.sprite=sourceImage.sprite;image.type=sourceImage.type;image.preserveAspect=sourceImage.preserveAspect;image.fillCenter=sourceImage.fillCenter;image.fillMethod=sourceImage.fillMethod;image.fillAmount=sourceImage.fillAmount;image.fillClockwise=sourceImage.fillClockwise;image.fillOrigin=sourceImage.fillOrigin;}
   var sourceRaw=sawNativeCrosshair as RawImage;var raw=sawReticle as RawImage;
   if(sourceRaw!=null&&raw!=null){raw.texture=sourceRaw.texture;raw.uvRect=sourceRaw.uvRect;}
   var sourceRect=sawNativeCrosshair.rectTransform;var rect=sawReticle.rectTransform;
   rect.anchorMin=sourceRect.anchorMin;rect.anchorMax=sourceRect.anchorMax;rect.pivot=sourceRect.pivot;rect.sizeDelta=sourceRect.sizeDelta;rect.anchoredPosition=sourceRect.anchoredPosition;rect.localScale=sourceRect.localScale;rect.localRotation=sourceRect.localRotation;
   sawReticle.gameObject.SetActive(visible);
  }
  void CleanupChainsawCrosshair(){if(sawReticle!=null)Destroy(sawReticle.gameObject);sawReticle=null;sawNativeCrosshair=null;}
 }
}

