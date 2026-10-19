using System;
using HutongGames.PlayMaker;
using UnityEngine;

namespace ChainsawPrototype {
 // Capture flesh/death identity while the native collider is still valid.
 // Corpse feedback is independent of whether another Damage event is allowed.
 internal struct SolidMeleeContact {
  internal PlayMakerFSM Receiver;
  internal Transform Actor;internal FsmFloat Health;
  internal bool Flesh,Dead;
  internal static bool Corpse(Transform node){
   string name=node.name;int clone=name.IndexOf("(Clone)",StringComparison.Ordinal);if(clone>=0)name=name.Substring(0,clone);
   return name.EndsWith("_Dead",StringComparison.OrdinalIgnoreCase)&&node.GetComponentInChildren<Rigidbody>(true)!=null;
  }
  internal static SolidMeleeContact Read(Collider surface){
   var result=new SolidMeleeContact();if(surface==null)return result;
   for(var node=surface.transform;node!=null;node=node.parent){
    foreach(var fsm in node.GetComponents<PlayMakerFSM>()){
     if(fsm==null||fsm.Fsm==null)continue;
     if(result.Receiver==null&&fsm.FsmName=="Bodypart"&&fsm.FsmVariables.FindFsmFloat("Damage")!=null)result.Receiver=fsm;
     if(fsm.FsmName=="Health"){
      var health=fsm.FsmVariables.FindFsmFloat("Health");
      if(health!=null){result.Flesh=true;result.Dead|=health.Value<=0f;result.Actor=node;result.Health=health;}
     }
    }
    // Native ragdolls use *_Dead roots and physical bodies. A suggestive
    // decoration name by itself must never turn a hard surface into flesh.
    if(Corpse(node)){result.Flesh=true;result.Dead=true;result.Actor=node;}
   }
   if(result.Dead)result.Receiver=null;
   return result;
  }
 }
}
