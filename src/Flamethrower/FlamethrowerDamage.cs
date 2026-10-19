using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace ChainsawPrototype {
 public partial class Plugin {
  // Native enemy limbs share the root Health FSM. Vehicle parts instead damage
  // their Condition FSM and remain solid stream blockers.
  Collider[] flameEnemyColliders=new Collider[128];
  readonly Dictionary<Transform,FlameEnemyContact> flameEnemyContacts=new Dictionary<Transform,FlameEnemyContact>();
  struct FlameEnemyContact { public PlayMakerFSM Receiver;public Vector3 Point;public float Distance; }

  static bool QualifiedFlameTrigger(Collider surface){
   // Native heads and mounted crew have trigger hitboxes. A child proximity or
   // fire sensor must not borrow a Bodypart from an ancestor to become a hitbox.
   if(surface==null)return false;
   foreach(var fsm in surface.GetComponents<PlayMakerFSM>())
    if(fsm!=null&&fsm.Fsm!=null&&fsm.FsmName=="Bodypart"&&fsm.FsmVariables.FindFsmFloat("Damage")!=null)return true;
   return false;
  }

  static Vector3 ChooseFlameAimTarget(Vector3 muzzle,Vector3 viewForward,Vector3 worldTarget,Vector3? enemyPoint){
   // Preserve crosshair convergence on a nearby enemy despite an offset hip
   // muzzle. A target before the nozzle must never reverse the stream.
   return enemyPoint!=null&&Vector3.Dot(enemyPoint.Value-muzzle,viewForward)>.03f?enemyPoint.Value:worldTarget;
  }

  bool TryFlameEnemy(Collider surface,out Transform owner,out PlayMakerFSM receiver){
   owner=null;receiver=null;if(surface==null||OwnCollider(surface))return false;
   for(var node=surface.transform;node!=null;node=node.parent){
    foreach(var fsm in node.GetComponents<PlayMakerFSM>()){
     if(fsm==null||fsm.Fsm==null)continue;
     if(receiver==null&&fsm.FsmName=="Bodypart"&&fsm.FsmVariables.FindFsmFloat("Damage")!=null)receiver=fsm;
     if(fsm.FsmName=="Health"&&fsm.FsmVariables.FindFsmFloat("Health")!=null)owner=node;
    }
    if(owner!=null)return true;
   }
   return false;
  }

  PlayMakerFSM FlameDamageReceiver(Collider surface){
   if(surface==null)return null;
   for(var node=surface.transform;node!=null;node=node.parent)
    foreach(var fsm in node.GetComponents<PlayMakerFSM>())
     if(fsm!=null&&fsm.FsmName=="Bodypart"&&fsm.FsmVariables.FindFsmFloat("Damage")!=null)return fsm;
   return null;
  }
  void DamageFlameReceiver(PlayMakerFSM receiver){
   ApplyFlameDamage(receiver,Mathf.Abs(flamePaidDamage));
  }
  void EmitFlameContact(Vector3 point,Vector3 normal){
   if(flameContact==null)return;
   flameContact.transform.SetPositionAndRotation(point+normal*.025f,Quaternion.LookRotation(normal));flameContact.Emit(4);
  }

  bool TryFlameEnemyPoint(Collider collider,Vector3 origin,Vector3 direction,float distance,out Vector3 point,out float along,float maximumAlong=float.PositiveInfinity){
   point=Vector3.zero;along=maximumAlong;bool found=false;
   var bounds=collider.bounds;var extents=bounds.extents;
   float centre=Vector3.Dot(bounds.center-origin,direction);
   float extent=Mathf.Abs(direction.x)*extents.x+Mathf.Abs(direction.y)*extents.y+Mathf.Abs(direction.z)*extents.z;
   // An angled limb can enter the wider far end while its centre's closest
   // point remains outside. Refine both projected bounds ends as well as the
   // centre, and accept the nearest cone-contained point with clear cover LOS.
   for(int sample=0;sample<3;sample++){
    float station=Mathf.Clamp(centre+(sample-1)*extent,0f,distance);
    var candidate=collider.ClosestPoint(FlameTrajectory.Point(origin,direction,station));
    for(int j=0;j<4;j++){
     var centreline=FlameTrajectory.Point(origin,direction,station);var tangent=FlameTrajectory.Tangent(direction,station);
     station=Mathf.Clamp(station+Vector3.Dot(candidate-centreline,tangent)/tangent.sqrMagnitude,0f,distance);
     candidate=collider.ClosestPoint(FlameTrajectory.Point(origin,direction,station));
    }
    // Keep the nozzle/end planes even when closest-point refinement clamps.
    if(Vector3.Dot(candidate-origin,direction)<0f||Vector3.Dot(candidate-FlameTrajectory.Point(origin,direction,distance),FlameTrajectory.Tangent(direction,distance))>0f||station>=along)continue;
    float radius=FlameTrajectory.Radius(station);
    var offset=candidate-FlameTrajectory.Point(origin,direction,station);
    if(offset.sqrMagnitude>radius*radius)continue;
    float obstructionStation;
    // 1m chords have <=1.75mm sag error. A 2mm sweep conservatively covers it;
    // already accepted nearer limbs skip this work for their shared Health owner.
    var obstruction=FlameTrajectoryCast(origin,direction,station,.002f,out obstructionStation,offset,1f);
    if(obstruction!=null&&obstructionStation<station-.01f)continue;
    point=candidate;along=station;found=true;
   }
   return found;
  }

  void CollectFlameEnemyContacts(Vector3 origin,Vector3 direction,float distance){
   flameEnemyContacts.Clear();if(distance<=.001f)return;
   // The chord capsule includes the parabola's maximum sag relative to it.
   float broadRadius=FlameTrajectory.Radius(distance)+FlameTrajectory.DropPerMetreSquared*distance*distance*.25f;
   var end=FlameTrajectory.Point(origin,direction,distance);Collider[] candidates;int count;
   // Hordes can fill an unsorted nonalloc buffer. Grow and retry, then fall back
   // to the complete query rather than silently omitting enemies.
   while(true){
    count=Physics.OverlapCapsuleNonAlloc(origin,end,broadRadius,flameEnemyColliders,~(1<<2),QueryTriggerInteraction.Collide);
    if(count<flameEnemyColliders.Length){candidates=flameEnemyColliders;break;}
    if(flameEnemyColliders.Length>=1024){candidates=Physics.OverlapCapsule(origin,end,broadRadius,~(1<<2),QueryTriggerInteraction.Collide);count=candidates.Length;break;}
    flameEnemyColliders=new Collider[flameEnemyColliders.Length*2];
   }
   for(int i=0;i<count;i++){
    var collider=candidates[i];Transform owner;PlayMakerFSM receiver;
    if(collider.isTrigger&&!QualifiedFlameTrigger(collider))continue;
    if(!TryFlameEnemy(collider,out owner,out receiver)||receiver==null)continue;
    // The broad capsule gathers candidates only. Contact points must fit the
    // widening visible jet and have clear solid LOS from the actual muzzle.
    FlameEnemyContact old;
    float maximumAlong=flameEnemyContacts.TryGetValue(owner,out old)?old.Distance:float.PositiveInfinity;
    Vector3 point;float along;if(!TryFlameEnemyPoint(collider,origin,direction,distance,out point,out along,maximumAlong))continue;
    flameEnemyContacts[owner]=new FlameEnemyContact{Receiver=receiver,Point=point,Distance=along};
   }
   Array.Clear(flameEnemyColliders,0,flameEnemyColliders.Length);
  }
  void DamageFlameEnemies(Vector3 origin,Vector3 direction,float distance){
   // The per-frame ignite query already used these exact posed coordinates.
   // Reuse it for this paid damage tick, then release its actor references.
   if(!flameIgniteContactsReady||flameIgniteFrame!=Time.frameCount||flameIgniteOrigin!=origin||flameIgniteDirection!=direction||flameIgniteDistance!=distance)CollectFlameEnemyContacts(origin,direction,distance);
   flameIgniteContactsReady=false;
   // One native Bodypart event per shared health owner per paid fuel interval,
   // regardless of how many head, torso or limb colliders enter the flame.
   foreach(var contact in flameEnemyContacts.Values){DamageFlameReceiver(contact.Receiver);EmitFlameContact(contact.Point,-direction);}
   flameEnemyContacts.Clear();Array.Clear(flameEnemyColliders,0,flameEnemyColliders.Length);
  }
 }
}
