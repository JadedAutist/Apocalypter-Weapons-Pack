using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 public sealed class FlamethrowerFuelTank:MonoBehaviour {
  public FlameFuelKind Kind=FlameFuelKind.Gasoline;
  ChainsawFuel fuel;PlayMakerFSM liquid;int transferFrame=-1;readonly HashSet<int> transferred=new HashSet<int>();
  public ChainsawFuel Fuel {get{if(fuel==null)fuel=GetComponent<ChainsawFuel>();return fuel;}}
  public float Litres {get{return Fuel==null?0f:Fuel.Litres;}}
  public bool CanAccept(FlameFuelKind incoming){return FlamethrowerFuelPolicy.CanAccept(Kind,Litres,incoming);}
  public void Restore(FlameFuelKind kind,float amount){
   Kind=FlamethrowerFuelPolicy.Valid(kind)?kind:FlameFuelKind.Gasoline;
   if(liquid==null)liquid=GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="LiquidAmount");
   var value=liquid==null?null:liquid.FsmVariables.FindFsmFloat("Liquid");if(value!=null)value.Value=Plugin.ValidFuelAmount(amount);
   var capacity=liquid==null?null:liquid.FsmVariables.FindFsmFloat("LiquidCapacity");if(capacity!=null)capacity.Value=FlamethrowerFuelPolicy.Capacity;
  }
  internal float Receive(GameObject source,FlameFuelKind incoming,float requested){
   if(source==null||source==gameObject||source.transform.IsChildOf(transform))return 0f;
   if(transferFrame!=Time.frameCount){transferred.Clear();transferFrame=Time.frameCount;}
   if(!transferred.Add(source.GetInstanceID()))return 0f;
   var sourceLiquid=source.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="LiquidAmount");
   var from=sourceLiquid==null?null:sourceLiquid.FsmVariables.FindFsmFloat("Liquid");if(from==null)return 0f;
   float before=Litres,amount=FlamethrowerFuelPolicy.Transfer(Kind,before,incoming,from.Value,requested);
   if(amount>0f){Restore(incoming,before+amount);from.Value=Mathf.Max(0f,from.Value-amount);}
   return amount;
  }
  void LateUpdate(){
   // Only the pack-owned tank's pouring filter follows its current fuel.
   var pour=GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="UseCan");if(pour==null)return;
   foreach(var state in pour.Fsm.States)foreach(var action in state.Actions){
    if(action.GetType().Name!="TriggerEvent")continue;
    var field=action.GetType().GetField("collideTag");var tag=field==null?null:field.GetValue(action) as FsmString;
    if(tag!=null)tag.Value=FlamethrowerFuelPolicy.Tag(Kind);
   }
  }
 }
 [DefaultExecutionOrder(32020)] public sealed class FlamethrowerFuelPort:MonoBehaviour {
  public FlamethrowerFuelTank Tank;public FlameFuelKind Kind;
  PlayMakerFSM proxy;BoxCollider inlet;
  internal void Sync(){
   if(Tank==null)return;if(proxy==null)proxy=GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="LiquidAmount");
   // The gasoline inlet shares the real LiquidAmount FSM. Its rejection must
   // never lower the real capacity: the native FloatClamp would erase oil or
   // diesel added through another inlet on the following frame.
   if(proxy!=null){var volume=proxy.FsmVariables.FindFsmFloat("Liquid");var cap=proxy.FsmVariables.FindFsmFloat("LiquidCapacity");if(volume!=null&&gameObject!=Tank.gameObject)volume.Value=Tank.Litres;if(cap!=null)cap.Value=gameObject==Tank.gameObject||Tank.CanAccept(Kind)?FlamethrowerFuelPolicy.Capacity:Tank.Litres;}
   if(inlet==null)inlet=GetComponent<BoxCollider>();bool carried=false;
   for(var node=Tank.transform.parent;node!=null;node=node.parent)if(node.name=="PlayerCamera"||node.name=="HandItemUse")carried=true;
   if(inlet!=null)inlet.enabled=!carried&&Tank.CanAccept(Kind)&&Tank.Litres<5f-.00001f;
  }
  void LateUpdate(){Sync();}
  internal void Transfer(GameObject source,PlayMakerFSM pour,float amount){
   if(Tank==null)return;FlameFuelKind actual;
   if(!Plugin.TryNativeFuelKind(source,out actual)||actual!=Kind)return;
   Tank.Receive(source,actual,amount);Sync();
   var sourceLiquid=source.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="LiquidAmount");
   var from=sourceLiquid==null?null:sourceLiquid.FsmVariables.FindFsmFloat("Liquid");
   if(pour!=null){var can=pour.FsmVariables.FindFsmFloat("CanLiquid");var current=pour.FsmVariables.FindFsmFloat("Liquid");var cap=pour.FsmVariables.FindFsmFloat("LiquidCapacity");if(can!=null&&from!=null)can.Value=from.Value;if(current!=null)current.Value=Tank.Litres;if(cap!=null)cap.Value=Tank.CanAccept(Kind)?5f:Tank.Litres;}
  }
 }
 public partial class Plugin {
  internal static float ValidFuelAmount(float value){return float.IsNaN(value)||float.IsInfinity(value)?0f:Mathf.Clamp(value,0f,5f);}
  internal static bool TryNativeFuelKind(GameObject source,out FlameFuelKind kind){
   kind=FlameFuelKind.Gasoline;if(source==null)return false;
   var own=source.GetComponent<FlamethrowerFuelTank>();if(own!=null){kind=own.Kind;return true;}
   var pour=source.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="UseCan");if(pour==null)return false;
   foreach(var state in pour.Fsm.States)foreach(var action in state.Actions){
    if(action.GetType().Name!="TriggerEvent")continue;
    var field=action.GetType().GetField("collideTag");var tag=field==null?null:field.GetValue(action) as FsmString;
    if(tag==null)continue;for(int i=0;i<3;i++)if(tag.Value==FlamethrowerFuelPolicy.Tag((FlameFuelKind)i)){kind=(FlameFuelKind)i;return true;}
   }return false;
  }
  void AddFlamethrowerFuelPorts(GameObject tank,GameObject nativeCan){
   var state=tank.AddComponent<FlamethrowerFuelTank>();
   var gas=tank.AddComponent<FlamethrowerFuelPort>();gas.Tank=state;gas.Kind=FlameFuelKind.Gasoline;
   var staging=new GameObject("Flamethrower.FuelPortStaging");staging.SetActive(false);
   try{for(int i=1;i<3;i++){
    var port=Instantiate(nativeCan,staging.transform,false);port.SetActive(false);port.name="Flamethrower.FuelPort."+(FlameFuelKind)i;
    for(int child=port.transform.childCount-1;child>=0;child--)DestroyImmediate(port.transform.GetChild(child).gameObject);
    foreach(var component in port.GetComponents<Component>())if(!(component is Transform)&&!(component is PlayMakerFSM&&((PlayMakerFSM)component).FsmName=="LiquidAmount"))DestroyImmediate(component);
    port.transform.SetParent(tank.transform,false);port.transform.localPosition=Vector3.zero;port.transform.localRotation=Quaternion.identity;port.transform.localScale=Vector3.one;port.tag=FlamethrowerFuelPolicy.Tag((FlameFuelKind)i);port.layer=2;
    var box=port.AddComponent<BoxCollider>();box.isTrigger=true;box.size=tank.GetComponent<BoxCollider>().size;
    var marker=port.AddComponent<FlamethrowerFuelPort>();marker.Tank=state;marker.Kind=(FlameFuelKind)i;port.SetActive(true);
   }}finally{Destroy(staging);}
  }
  void SetupFlamethrowerFuelPouring(){
   foreach(var type in new[]{typeof(HutongGames.PlayMaker.Actions.FloatAdd),typeof(HutongGames.PlayMaker.Actions.SetFsmFloat)})
    foreach(string method in new[]{"OnEnter","OnUpdate"})harmony.Patch(AccessTools.Method(type,method),prefix:new HarmonyMethod(typeof(Plugin),nameof(TransferIntoFlamethrower)));
  }
  static readonly HashSet<FsmStateAction> guardedFuelWrites=new HashSet<FsmStateAction>();
  static bool TransferIntoFlamethrower(FsmStateAction __instance,MethodBase __originalMethod){
   if(__instance.Fsm==null||__instance.Fsm.Name!="UseCan"||__instance.Fsm.ActiveStateName!="Fill"){guardedFuelWrites.Remove(__instance);return true;}
   var collided=__instance.Fsm.Variables.FindFsmGameObject("CollidedObj");var port=collided==null||collided.Value==null?null:collided.Value.GetComponent<FlamethrowerFuelPort>();
   if(port==null){
    // A can can move from our receiver to a normal item while Fill is active.
    // Re-enter the original action once to rebuild any native cached target.
    if(guardedFuelWrites.Remove(__instance)&&__originalMethod.Name=="OnUpdate"){__instance.OnEnter();return false;}
    return true;
   }
   guardedFuelWrites.Add(__instance);
   var debit=__instance.Fsm.ActiveState.Actions.OfType<HutongGames.PlayMaker.Actions.FloatAdd>().FirstOrDefault();
   if(debit!=null)port.Transfer(__instance.Owner,__instance.Fsm.Owner as PlayMakerFSM,Mathf.Abs(debit.add.Value)*(debit.perSecond?Time.deltaTime:1f));
   var everyFrame=__instance.GetType().GetField("everyFrame");if(everyFrame==null||!(bool)everyFrame.GetValue(__instance))__instance.Finish();
   return false;
  }
 }
}
