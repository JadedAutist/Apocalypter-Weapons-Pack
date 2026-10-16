using System;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 public sealed class ConservedFuelTransfer : FsmStateAction {
  public float rate;public bool perSecond;int updatedFrame=-1;
  public override void OnEnter(){Transfer();}
  public override void OnUpdate(){Transfer();}
  void Transfer(){
   if(updatedFrame==Time.frameCount)return;updatedFrame=Time.frameCount;
   var source=Owner.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="LiquidAmount");
   var collided=Fsm.Variables.FindFsmGameObject("CollidedObj");if(source==null||collided==null||collided.Value==null||collided.Value==Owner)return;
   var port=collided.Value.GetComponent<FlamethrowerFuelPort>();if(port!=null){port.Transfer(Owner,Fsm.Owner as PlayMakerFSM,rate*(perSecond?Time.deltaTime:1f));return;}
   var target=collided.Value.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="LiquidAmount");if(target==null)return;
   var from=source.FsmVariables.FindFsmFloat("Liquid");var to=target.FsmVariables.FindFsmFloat("Liquid");var capacity=target.FsmVariables.FindFsmFloat("LiquidCapacity");
   var own=Owner.GetComponent<FlamethrowerFuelTank>();string fuelTag=own==null?"Gasoline":FlamethrowerFuelPolicy.Tag(own.Kind);
   if(from==null||to==null||capacity==null||!collided.Value.CompareTag(fuelTag))return;
   float room=Mathf.Max(0f,capacity.Value-to.Value);float amount=Mathf.Min(Mathf.Max(0f,from.Value),Mathf.Min(room,rate*(perSecond?Time.deltaTime:1f)));
   if(amount>0f){to.Value+=amount;from.Value-=amount;}
   var canLiquid=Fsm.Variables.FindFsmFloat("CanLiquid");var targetLiquid=Fsm.Variables.FindFsmFloat("Liquid");var targetCapacity=Fsm.Variables.FindFsmFloat("LiquidCapacity");
   if(canLiquid!=null)canLiquid.Value=from.Value;if(targetLiquid!=null)targetLiquid.Value=to.Value;if(targetCapacity!=null)targetCapacity.Value=capacity.Value;
  }
 }
 public partial class Plugin {
  internal static void ConfigureFuelPour(GameObject tank){if(!ReferenceEquals(instance,null))instance.InstallConservedPour(tank);}
  void InstallConservedPour(GameObject tank){
   var pour=tank.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="UseCan");if(pour==null)throw new InvalidOperationException("Native chainsaw pouring FSM missing.");
   var fill=pour.Fsm.States.First(s=>s.Name=="Fill");if(fill.Actions.Any(a=>a is ConservedFuelTransfer))return;var decrement=fill.Actions.First(a=>a.GetType().Name=="FloatAdd");var rate=(FsmFloat)decrement.GetType().GetField("add").GetValue(decrement);bool perSecond=(bool)decrement.GetType().GetField("perSecond").GetValue(decrement);
   var replacement=new ConservedFuelTransfer{rate=Mathf.Abs(rate.Value),perSecond=perSecond};var actions=new List<FsmStateAction>();bool inserted=false;
   foreach(var action in fill.Actions){if(action.GetType().Name=="FloatAdd" || action.GetType().Name=="SetFsmFloat"){if(!inserted){actions.Add(replacement);inserted=true;}continue;}actions.Add(action);}
   fill.Actions=actions.ToArray();Logger.LogInfo("Chainsaw pouring conserves fuel and stops at receiving capacity; native rate="+replacement.rate+", perSecond="+perSecond);
  }
 }
}
