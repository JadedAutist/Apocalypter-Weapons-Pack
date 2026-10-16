using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 internal sealed class FlameContainerHeat {
  internal const float IgnitionSeconds=2f,CoolingPerSecond=.5f;
  float heat,lastTouch;internal bool Triggered;
  internal float HeatAt(float now){return Mathf.Max(0f,heat-Mathf.Max(0f,now-lastTouch)*CoolingPerSecond);}
  internal bool Touch(float now,float fundedSeconds){
   if(Triggered||!FlamethrowerFuelPolicy.CanBurn(fundedSeconds))return false;
   // The current funded slice was flame contact, so cool only the gap before it.
   heat=HeatAt(now-fundedSeconds)+fundedSeconds;lastTouch=now;
   if(heat+.00001f<IgnitionSeconds)return false;Triggered=true;return true;
  }
 }
 public partial class Plugin {
  readonly Dictionary<PlayMakerFSM,FlameContainerHeat> flameContainerHeat=new Dictionary<PlayMakerFSM,FlameContainerHeat>();
  readonly List<PlayMakerFSM> coldFlameContainers=new List<PlayMakerFSM>();
  int flameHeatFrame=-1;float nextFlameHeatPrune;
  PlayMakerFSM FlameIgnitionReceiver(Collider surface){
   if(surface==null||surface.isTrigger||OwnCollider(surface))return null;
   for(var node=surface.transform;node!=null;node=node.parent){
    var fsms=node.GetComponents<PlayMakerFSM>();
    foreach(var fsm in fsms){
     if(fsm==null||!fsm.enabled||fsm.FsmName!="Explosion"||!fsm.gameObject.activeInHierarchy||!fsm.gameObject.scene.IsValid())continue;
     // Recognize the native can/barrel contract, never an arbitrary explosion
     // effect, vehicle, or a name containing "fuel". Preserve native eligibility.
     if(fsm.Fsm.GlobalTransitions==null||!fsm.Fsm.GlobalTransitions.Any(t=>t.EventName=="DamageFlammable"&&t.ToState=="explode"))continue;
     if(fsm.Fsm.ActiveStateName!="trigger")continue;
     var liquid=fsms.FirstOrDefault(f=>f.FsmName=="LiquidAmount");var amount=liquid==null?null:liquid.FsmVariables.FindFsmFloat("Liquid");
     if(amount!=null&&FlamethrowerFuelPolicy.CanBurn(amount.Value))return fsm;
    }
   }return null;
  }
  void HeatFlameContainer(Collider surface,float fundedSeconds){
   if(flameHeatFrame==Time.frameCount||!FlamethrowerFuelPolicy.CanBurn(fundedSeconds))return;
   flameHeatFrame=Time.frameCount;var receiver=FlameIgnitionReceiver(surface);if(receiver==null)return;
   FlameContainerHeat heat;
   if(!flameContainerHeat.TryGetValue(receiver,out heat)){
    if(flameContainerHeat.Count>=128)return;
    heat=new FlameContainerHeat();flameContainerHeat.Add(receiver,heat);
   }
   if(heat.Touch(Time.time,fundedSeconds))receiver.SendEvent("DamageFlammable");
  }
  void TickFlameIgnition(){
   if(Time.time<nextFlameHeatPrune)return;nextFlameHeatPrune=Time.time+1f;
   coldFlameContainers.Clear();
   foreach(var pair in flameContainerHeat){var fsm=pair.Key;
    if(fsm==null||!fsm.enabled||!fsm.gameObject.activeInHierarchy||!fsm.gameObject.scene.isLoaded||pair.Value.HeatAt(Time.time)<=0f)coldFlameContainers.Add(fsm);
   }
   foreach(var fsm in coldFlameContainers)flameContainerHeat.Remove(fsm);coldFlameContainers.Clear();
  }
  void ClearFlameIgnition(){flameContainerHeat.Clear();coldFlameContainers.Clear();flameHeatFrame=-1;nextFlameHeatPrune=0f;}
 }
}
