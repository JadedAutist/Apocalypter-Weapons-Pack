using System;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.UI;
namespace ChainsawPrototype {
 public sealed class ChainsawFuel : MonoBehaviour {
  public const float Capacity=10f,PerSwing=.10f;
  PlayMakerFSM tank,pour;FsmFloat liquid,capacity;BoxCollider inlet;Collider nozzle;bool wasGrabbed;
  void Start(){Plugin.ConfigureFuelPour(gameObject);}
  public float Litres {get{EnsureTank();return liquid==null?0f:Mathf.Clamp(liquid.Value,0f,Capacity);}}
  public bool Ready {get{return FlamethrowerFuelPolicy.CanBurn(Litres);}}
  void EnsureTank(){if(tank==null)tank=GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="LiquidAmount");if(pour==null)pour=GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="UseCan");if(nozzle==null){var t=transform.Find("FuelTrigger");if(t!=null)nozzle=t.GetComponent<Collider>();}if(tank!=null && liquid==null){liquid=tank.FsmVariables.FindFsmFloat("Liquid");capacity=tank.FsmVariables.FindFsmFloat("LiquidCapacity");}if(capacity!=null&&capacity.Value!=Capacity)capacity.Value=Capacity;if(inlet==null)inlet=GetComponent<BoxCollider>();}
  public void Consume(){EnsureTank();if(liquid!=null)liquid.Value=Mathf.Max(0f,Mathf.Round((liquid.Value-PerSwing)*10000f)/10000f);}
  public int ConsumeAttacks(int requested,float cost){
   EnsureTank();if(liquid==null||requested<=0||cost<=0f||float.IsNaN(cost)||float.IsInfinity(cost))return 0;
   float available=Mathf.Clamp(liquid.Value,0f,Capacity);
   int paid=Mathf.Min(requested,Mathf.FloorToInt((available+.00001f)/cost));
   if(paid>0)liquid.Value=Mathf.Max(0f,Mathf.Round((available-paid*cost)*10000f)/10000f);
   return paid;
  }
  public float ConsumeContinuous(float requested){
   EnsureTank();if(liquid==null||requested<=0f||float.IsNaN(requested)||float.IsInfinity(requested)||!FlamethrowerFuelPolicy.CanBurn(liquid.Value))return 0f;
   float available=Mathf.Clamp(liquid.Value,0f,Capacity),spent=Mathf.Min(available,requested);
   // Preserve sub-centilitre changes and consume the final partial frame.
   // Per-frame rounding would make fuel use depend on the frame rate.
   liquid.Value=Mathf.Max(0f,available-spent);return spent;
  }
  void LateUpdate(){
   EnsureTank();if(liquid!=null)liquid.Value=Mathf.Clamp(liquid.Value,0f,Capacity);
   bool equipped=false,carried=false;for(var p=transform.parent;p!=null;p=p.parent){if(p.name=="HandItemUse")equipped=true;if(p.name=="PlayerCamera")carried=true;}
   var item=transform.parent==null?null:transform.parent.GetComponent<ChainsawItem>();var flame=transform.parent==null?null:transform.parent.GetComponent<FlamethrowerItem>();bool grabbed=carried&&!equipped&&((item!=null&&item.PhysicsGrabbed)||(flame!=null&&flame.PhysicsGrabbed));
   if(inlet!=null)inlet.enabled=!carried&&!equipped;
   if(nozzle!=null)nozzle.enabled=grabbed&&Litres>0f;
   if(pour!=null&&grabbed!=wasGrabbed)pour.SendEvent(grabbed?"InHand":"OutofHand");wasGrabbed=grabbed;
  }

 }
 public partial class Plugin {
  ChainsawItem fuelOwner;ChainsawFuel heldFuel;Text fuelLabel;Color fuelNormalColor;bool fuelHudShowing;Text[] nativeFuelTexts;bool[] nativeFuelTextEnabled;float displayedFuel=-1f,nextHudRetry;
  public static bool HasFuelForAttack {get{return !ReferenceEquals(instance,null)&&instance.enabledPreview&&instance.heldFuel!=null&&FlamethrowerFuelPolicy.CanBurn(instance.heldFuel.Litres);}}
  bool AddFuelTank(GameObject item){
   var source=Resources.FindObjectsOfTypeAll<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="LiquidAmount" && f.gameObject.name=="Gasoline_Can");if(source==null)return false;
   var tank=Instantiate(source.gameObject,item.transform,false);tank.name="Chainsaw.FuelTank";tank.SetActive(false);
   foreach(var fsm in tank.GetComponents<PlayMakerFSM>())if(fsm.FsmName!="LiquidAmount" && fsm.FsmName!="UseCan")DestroyImmediate(fsm);
   foreach(var child in tank.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==tank.transform && t.name!="FuelTrigger").ToArray())DestroyImmediate(child.gameObject);
   foreach(var component in tank.GetComponents<Component>())if(!(component is Transform) && !(component is PlayMakerFSM) && !(component is AudioSource))DestroyImmediate(component);
   tank.tag="Gasoline";tank.layer=2; // Native Fuel and FuelTrigger receivers use Ignore Raycast.
   tank.transform.localPosition=Vector3.zero;tank.transform.localRotation=Quaternion.identity;tank.transform.localScale=Vector3.one;
   var liquidFsm=tank.GetComponents<PlayMakerFSM>().First(f=>f.FsmName=="LiquidAmount");liquidFsm.FsmVariables.FindFsmFloat("Liquid").Value=ChainsawFuel.Capacity;liquidFsm.FsmVariables.FindFsmFloat("LiquidCapacity").Value=ChainsawFuel.Capacity;
   var trigger=tank.AddComponent<BoxCollider>();trigger.isTrigger=true;var scale=item.transform.lossyScale;trigger.size=new Vector3(.25f/Mathf.Max(Mathf.Abs(scale.x),.00001f),.25f/Mathf.Max(Mathf.Abs(scale.y),.00001f),.25f/Mathf.Max(Mathf.Abs(scale.z),.00001f));var nozzle=tank.transform.Find("FuelTrigger");if(nozzle!=null){
    nozzle.localPosition=Vector3.zero;nozzle.localRotation=Quaternion.identity;nozzle.localScale=new Vector3(1f/Mathf.Max(Mathf.Abs(scale.x),.00001f),1f/Mathf.Max(Mathf.Abs(scale.y),.00001f),1f/Mathf.Max(Mathf.Abs(scale.z),.00001f));nozzle.gameObject.layer=2;
    foreach(var c in nozzle.GetComponents<Collider>())DestroyImmediate(c);var outlet=nozzle.gameObject.AddComponent<SphereCollider>();outlet.isTrigger=true;outlet.radius=.12f;outlet.enabled=false;
   }
   tank.AddComponent<ChainsawFuel>();InstallConservedPour(tank);if(item.GetComponent<FlamethrowerItem>()!=null)AddFlamethrowerFuelPorts(tank,source.gameObject);tank.SetActive(true);
   Logger.LogInfo("Weapons Pack native fuel receiver ready: "+ChainsawFuel.Capacity+" L capacity.");return true;
  }
  void BindFuel(ChainsawItem item){if(item==fuelOwner)return;fuelOwner=item;heldFuel=item==null?null:item.GetComponentInChildren<ChainsawFuel>(true);displayedFuel=heldFuel==null?-1f:heldFuel.Litres;}
  internal void UpdateFuelHud(){
   // Keep the attack fuel binding chainsaw-only; the display can serve either custom weapon.
   var hudFuel=enabledPreview?heldFuel:EquippedFlamethrowerFuel();bool show=hudFuel!=null;
   if(show&&fuelLabel==null&&Time.unscaledTime>=nextHudRetry){
    nextHudRetry=Time.unscaledTime+2f;
    nativeFuelTexts=Resources.FindObjectsOfTypeAll<Text>().Where(t=>t.gameObject.scene.IsValid() && (t.name=="AmmoInWeaponText" || t.name=="AmmoText" || t.name=="AmmoLable")).ToArray();
    var label=nativeFuelTexts.FirstOrDefault(t=>t.name=="AmmoLable" && t.gameObject.activeInHierarchy)??nativeFuelTexts.FirstOrDefault(t=>t.name=="AmmoLable");
    if(label!=null){
     var canvas=label.GetComponentInParent<Canvas>();nativeFuelTexts=nativeFuelTexts.Concat(Resources.FindObjectsOfTypeAll<Text>().Where(t=>t.gameObject.scene.IsValid()&&t.text!=null&&t.text.Trim()=="/"&&t.GetComponentInParent<Canvas>()==canvas && Mathf.Abs(t.transform.position.y-label.transform.position.y)<Mathf.Min(10f,label.rectTransform.rect.height*.3f))).Distinct().ToArray();
     nativeFuelTextEnabled=nativeFuelTexts.Select(t=>t.enabled).ToArray();fuelLabel=CloneFuelText(label,"Counter");fuelNormalColor=fuelLabel.color;
     var rect=fuelLabel.rectTransform;float oldWidth=rect.rect.width;rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,180f);
     rect.anchoredPosition+=new Vector2((180f-oldWidth)*(rect.pivot.x-1f),0f);
     fuelLabel.alignment=TextAnchor.MiddleRight;fuelLabel.horizontalOverflow=HorizontalWrapMode.Overflow;fuelLabel.verticalOverflow=VerticalWrapMode.Overflow;
     fuelLabel.fontSize=Mathf.Min(label.fontSize,24);fuelLabel.resizeTextForBestFit=false;
    }
   }
   if(fuelLabel==null)return;
   if(show){
    if(!fuelHudShowing)nativeFuelTextEnabled=nativeFuelTexts.Select(t=>t!=null && t.enabled).ToArray();
    displayedFuel=hudFuel.Litres;
    fuelLabel.text=displayedFuel.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" FUEL";
    fuelLabel.color=WeaponSpawnFuelPolicy.CounterColor(displayedFuel,fuelNormalColor);
    foreach(var text in nativeFuelTexts)if(text!=null)text.enabled=false;
   }else if(fuelHudShowing){for(int i=0;i<nativeFuelTexts.Length;i++)if(nativeFuelTexts[i]!=null)nativeFuelTexts[i].enabled=nativeFuelTextEnabled[i];}
   fuelLabel.gameObject.SetActive(show);fuelHudShowing=show;
  }
  ChainsawFuel EquippedFlamethrowerFuel(){
   var hand=flameHeldFsm??heldFsm;
   if(hand!=null){
    var selected=hand.FsmVariables.FindFsmGameObject("Item")??hand.FsmVariables.FindFsmGameObject("item");
    var marker=selected==null||selected.Value==null?null:selected.Value.GetComponent<FlamethrowerItem>();
    return marker==null||!marker.Equipped||!marker.gameObject.activeInHierarchy?null:marker.GetComponentInChildren<ChainsawFuel>(true);
   }
   var item=FlamethrowerItem.Active.FirstOrDefault(i=>i!=null&&i.Equipped&&i.gameObject.activeInHierarchy);
   return item==null?null:item.GetComponentInChildren<ChainsawFuel>(true);
  }
  Text CloneFuelText(Text original,string suffix){var copy=Instantiate(original,original.transform.parent,false);copy.name="Chainsaw.Fuel"+suffix;foreach(var child in copy.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==copy.transform).ToArray())DestroyImmediate(child.gameObject);foreach(var script in copy.GetComponents<MonoBehaviour>())if(script!=copy)DestroyImmediate(script);copy.enabled=true;copy.raycastTarget=false;return copy;}
 }
}
