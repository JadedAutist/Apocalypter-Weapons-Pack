using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace ChainsawPrototype {
 public partial class Plugin {
  Transform tooltipView;Text grabName,grabDetails;bool wroteGrabTooltip;string lastGrabName,lastGrabDetails;float tooltipRetry;
  bool StockWeaponEquipped(){
   if(heldFsm==null)return false;var value=heldFsm.FsmVariables.FindFsmGameObject("Item")??heldFsm.FsmVariables.FindFsmGameObject("item");
   if(value==null || value.Value==null || !value.Value.activeInHierarchy)return false;
   for(var p=value.Value.transform.parent;p!=null;p=p.parent)if(p.name=="HandItemUse")return true;return false;
  }
  internal void UpdateGrabTooltip(){
   var saw=ChainsawItem.Active.FirstOrDefault(i=>i!=null&&i.PhysicsGrabbed&&i.gameObject.activeInHierarchy);
   var flame=saw==null?FlamethrowerItem.Active.FirstOrDefault(i=>i!=null&&i.PhysicsGrabbed&&i.gameObject.activeInHierarchy):null;
   var item=saw!=null?saw.gameObject:flame==null?null:flame.gameObject;
   if(item==null && Time.timeScale>0f){
    if(tooltipView==null){var viewpoint=GameObject.Find("PlayerCamera");if(viewpoint!=null)tooltipView=viewpoint.transform;}if(tooltipView!=null){RaycastHit hit;if(Physics.Raycast(tooltipView.position,tooltipView.forward,out hit,3f,~0,QueryTriggerInteraction.Ignore)){
     // Keep the existing chainsaw tooltip eligibility; a grounded flame tank
     // must also refresh while the player holds a different weapon/refills it.
     if(!enabledPreview&&!StockWeaponEquipped())saw=hit.collider.GetComponentInParent<ChainsawItem>();
     flame=saw==null?hit.collider.GetComponentInParent<FlamethrowerItem>():null;if(flame!=null&&flame.Equipped)flame=null;
     item=saw!=null?saw.gameObject:flame==null?null:flame.gameObject;
    }}
   }
   if(item!=null){
    if((grabName==null||grabDetails==null)&&Time.unscaledTime>=tooltipRetry){tooltipRetry=Time.unscaledTime+2f;var texts=Resources.FindObjectsOfTypeAll<Text>();grabName=texts.FirstOrDefault(t=>t.name=="ItemName"&&t.gameObject.scene.IsValid());grabDetails=texts.FirstOrDefault(t=>t.name=="ItemName_4"&&t.gameObject.scene.IsValid());}
    var fuel=item.GetComponentInChildren<ChainsawFuel>(true);lastGrabName=flame==null?"CHAINSAW":"FLAMETHROWER";
    lastGrabDetails=flame!=null?FlamethrowerFuelDetails(item):"Damage - "+sawDamage.Value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+(fuel==null?"":"\n"+fuel.Litres.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" / "+ChainsawFuel.Capacity.ToString("0",System.Globalization.CultureInfo.InvariantCulture)+" L");
    if(grabName!=null){grabName.enabled=true;grabName.text=lastGrabName;}if(grabDetails!=null){grabDetails.enabled=true;grabDetails.text=lastGrabDetails;}wroteGrabTooltip=true;
   }else if(wroteGrabTooltip){if(grabName!=null&&grabName.text==lastGrabName)grabName.text="";if(grabDetails!=null&&grabDetails.text==lastGrabDetails)grabDetails.text="";wroteGrabTooltip=false;}
  }
 }
}
