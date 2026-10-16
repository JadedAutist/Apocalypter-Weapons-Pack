using System;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 // Shared baseline for custom melee weapons: copy the game's wrench cast dimensions.
 public static class MeleeHitArea {
  public static int LayerMask {get;private set;}=-1;public static QueryTriggerInteraction Triggers {get;private set;}=QueryTriggerInteraction.UseGlobal;
  static bool resolved;static float radius,reach,nextRetry;
  public static bool TryGetWrenchShape(out float hitRadius,out float hitReach){
   hitRadius=radius;hitReach=reach;if(resolved)return true;
   if(Time.unscaledTime<nextRetry)return false;nextRetry=Time.unscaledTime+2f;
   foreach(var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>()){
    if(fsm.FsmName!="Attack" || fsm.gameObject.name!="pipe_wrench")continue;
    foreach(var state in fsm.Fsm.States)foreach(var action in state.Actions){
     if(action.GetType().Name!="SphereCast2")continue;
     var radiusField=action.GetType().GetField("radius");var reachField=action.GetType().GetField("maxDistance");
     var r=radiusField==null?null:radiusField.GetValue(action) as FsmFloat;var d=reachField==null?null:reachField.GetValue(action) as FsmFloat;
     if(r==null || d==null || r.Value<=0f || d.Value<=0f || float.IsNaN(r.Value) || float.IsNaN(d.Value))continue;
     var layersField=action.GetType().GetField("layerMask");var invertField=action.GetType().GetField("invertMask");var triggerField=action.GetType().GetField("triggerInteraction");
     var layers=layersField==null?null:layersField.GetValue(action) as FsmInt[];var invert=invertField==null?null:invertField.GetValue(action) as FsmBool;
     if(layers!=null)LayerMask=ActionHelpers.LayerArrayToLayerMask(layers,invert!=null && invert.Value);
     if(triggerField!=null)Triggers=(QueryTriggerInteraction)triggerField.GetValue(action);
     radius=r.Value;reach=d.Value;resolved=true;hitRadius=radius;hitReach=reach;return true;
    }
   }
   return false;
  }
 }
}
