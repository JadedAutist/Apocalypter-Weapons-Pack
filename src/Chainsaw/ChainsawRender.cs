using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  Transform renderRig;SkinnedMeshRenderer[] renderArms;
  SkinnedMeshRenderer[] HeldArmRenderers(){if(renderRig!=weapon||renderArms==null){renderRig=weapon;renderArms=weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true);}return renderArms;}
  void ProtectChainsawRender(Camera rendering){
   if(!enabledPreview||camera==null||preview==null||weapon==null||rendering.targetTexture!=null||rendering!=camera.GetComponent<Camera>())return;
   if(!CanPresentHeldWeapon(camera)){StopPreview();return;}
   bool visible=!ActualThirdPersonActive();
   preview.SetActive(true);var model=preview.GetComponent<MeshRenderer>();model.enabled=visible;model.allowOcclusionWhenDynamic=false;preview.layer=0;
   weapon.gameObject.SetActive(true);
   foreach(var skin in HeldArmRenderers()){skin.gameObject.layer=0;skin.enabled=visible;skin.updateWhenOffscreen=true;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*6);}
  }
  float nextVisibilityLog;
  void TraceChainsawVisibility(Camera rendering){
   if(!enabledPreview||camera==null||preview==null||rendering.targetTexture!=null||rendering!=camera.GetComponent<Camera>()||Time.unscaledTime<nextVisibilityLog)return;nextVisibilityLog=Time.unscaledTime+3;
   var model=preview.GetComponent<MeshRenderer>();var point=rendering.transform.InverseTransformPoint(model.bounds.center);bool inFrustum=GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(rendering),model.bounds);var selected=heldFsm==null?null:(heldFsm.FsmVariables.FindFsmGameObject("Item")??heldFsm.FsmVariables.FindFsmGameObject("item"));string item=selected==null||selected.Value==null?"null":selected.Value.name;
   Logger.LogInfo("Chainsaw visibility: selected="+item+"; modelActive="+preview.activeInHierarchy+"; renderer="+model.enabled+"; visible="+model.isVisible+"; frustum="+inFrustum+"; third="+ActualThirdPersonActive()+"; attacking="+gameAttacking+"; local="+preview.transform.localPosition.ToString("F3")+"; scale="+preview.transform.lossyScale.ToString("F3")+"; boundsCamera="+point.ToString("F3")+"; motionShift="+stockShift.ToString("F3")+"; near="+rendering.nearClipPlane+"; mask="+rendering.cullingMask+"; camera="+rendering.name);
  }
  void OnApplicationQuit(){Camera.onPreRender-=ProtectChainsawRender;Camera.onPreCull-=ProtectChainsawRender;Camera.onPostRender-=TraceChainsawVisibility;}
 }
}
