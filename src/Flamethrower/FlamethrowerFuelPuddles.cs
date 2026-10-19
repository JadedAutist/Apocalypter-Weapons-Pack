using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChainsawPrototype {
 public partial class Plugin {
  const int MaxFuelPatches=64;
  const float FuelPatchRadius=.55f,FuelPatchBurnRate=.01f;
  readonly List<FuelPatch> fuelPatches=new List<FuelPatch>(MaxFuelPatches);
  readonly Dictionary<Transform,FuelGroundContact> fuelGroundContacts=new Dictionary<Transform,FuelGroundContact>();
  Collider[] fuelGroundColliders=new Collider[128];
  GameObject fuelPatchRoot;Material fuelWetMaterial,fuelFireMaterial;
  ConfigEntry<float> fuelWetLifetime,fuelMinimumBurn;
  float fuelPatchTick;
  sealed class FuelPatch {
   internal Collider Surface;internal Vector3 LocalCentre,LocalNormal;
   internal float Fuel,Born,LastStep,IgniteAt=float.PositiveInfinity,Damage,EndingAt=float.PositiveInfinity;
   internal float BurnEndsAt=float.PositiveInfinity;
   internal FlameFuelKind Kind;internal GameObject Visual;internal Mesh Mesh;internal MeshRenderer Wet;
   internal ParticleSystem Fire;internal FlameFireSmoke Smoke;internal MaterialPropertyBlock Properties=new MaterialPropertyBlock();
   internal Vector3 Point {get{return Surface.transform.TransformPoint(LocalCentre);}}
   internal Vector3 Normal {get{return Surface.transform.TransformDirection(LocalNormal).normalized;}}
  }
  struct FuelGroundContact {internal PlayMakerFSM Receiver;internal float Damage,Seconds;}
  void SetupFuelPuddles(){
   fuelWetLifetime=Config.Bind("Flamethrower - Fuel patches","Unlit lifetime",60f,new ConfigDescription("Seconds before unlit sprayed fuel evaporates. Fuel patches are temporary and are not saved.",new AcceptableValueRange<float>(5f,120f)));
   fuelMinimumBurn=Config.Bind("Flamethrower - Fuel patches","Minimum burn seconds",15f,new ConfigDescription("Minimum burning time for a funded fuel patch. More deposited fuel can extend it; adding fuel refreshes the minimum.",new AcceptableValueRange<float>(1f,60f)));
  }
  bool FuelPatchSurface(RaycastHit hit){
   var surface=hit.collider;
   if(surface==null||surface.isTrigger||!surface.enabled||!surface.gameObject.activeInHierarchy||surface.gameObject.layer==4||
    !Finite(hit.point)||!Finite(hit.normal)||hit.normal.sqrMagnitude<.5f||Vector3.Dot(hit.normal.normalized,Vector3.up)<.6f||!ScorchableFlameSurface(surface))return false;
   for(var node=surface.transform;node!=null;node=node.parent){
    if(SolidMeleeContact.Corpse(node))return false;
    foreach(var fsm in node.GetComponents<PlayMakerFSM>())if(fsm!=null&&fsm.Fsm!=null&&fsm.FsmName=="Health")return false;
   }return true;
  }
  FuelPatch DepositFuelPatch(RaycastHit hit,float litres,FlameFuelKind kind,float now){
   if(!Finite(litres)||litres<=0f||!Finite(now)||!FuelPatchSurface(hit))return null;
   foreach(var patch in fuelPatches)if(patch.Surface==hit.collider&&patch.Kind==kind&&float.IsPositiveInfinity(patch.EndingAt)&&
    (patch.Point-hit.point).sqrMagnitude<.09f&&Vector3.Dot(patch.Normal,hit.normal)>.9f){
    patch.Fuel=Mathf.Min(.3f,patch.Fuel+litres);patch.Born=now;
    if(!float.IsPositiveInfinity(patch.BurnEndsAt))patch.BurnEndsAt=Mathf.Max(patch.BurnEndsAt,now+Mathf.Max(FlameTuningValue(fuelMinimumBurn,15f,1f,60f),patch.Fuel/FuelPatchBurnRate));
    return patch;
   }
   // Refuse excess deposits rather than erase a burning hazard under an actor.
   if(fuelPatches.Count>=MaxFuelPatches)return null;
   var mesh=ProjectFlameSurfacePatch(hit.collider,hit.point,hit.normal.normalized,FuelPatchRadius);if(mesh==null)return null;
   var balance=flameFuelBalances[Mathf.Clamp((int)kind,0,2)];
   var next=new FuelPatch{Surface=hit.collider,LocalCentre=hit.collider.transform.InverseTransformPoint(hit.point),LocalNormal=hit.collider.transform.InverseTransformDirection(hit.normal.normalized),Fuel=Mathf.Min(.3f,litres),Born=now,LastStep=now,Kind=kind,Damage=FlameTuningValue(balance==null?null:balance.Damage,20f,1f,200f),Mesh=mesh};
   CreateFuelPatchVisual(next);fuelPatches.Add(next);
   // A fresh deposit touching an already burning pool catches immediately.
   foreach(var old in fuelPatches)if(!ReferenceEquals(old,next)&&old.IgniteAt<=now&&old.Fuel>0f&&FuelPatchesConnected(old,next)){next.IgniteAt=now;break;}
   return next;
  }
  void IgniteFuelAt(Vector3 point,float radius,float now){
   if(!Finite(point)||!Finite(radius)||radius<0f||!Finite(now))return;
   foreach(var patch in fuelPatches){
    if(patch.Surface==null||patch.Fuel<=0f)continue;
    var delta=point-patch.Point;
    if(Mathf.Abs(Vector3.Dot(delta,patch.Normal))>.25f||(delta-patch.Normal*Vector3.Dot(delta,patch.Normal)).sqrMagnitude>(FuelPatchRadius+radius)*(FuelPatchRadius+radius))continue;
    var start=patch.Point+patch.Normal*.08f;var end=point+patch.Normal*.08f;
    if(FuelClearLine(start,end))patch.IgniteAt=Mathf.Min(patch.IgniteAt,now);
   }
  }
  bool FuelClearLine(Vector3 start,Vector3 end){var delta=end-start;float length=delta.magnitude;return length<.01f||FlameCast(start,delta/length,length,.005f,true)==null;}
  bool FuelPatchesConnected(FuelPatch a,FuelPatch b){
   if(a.Surface==null||b.Surface==null||!a.Surface.enabled||!b.Surface.enabled)return false;
   var normal=a.Normal;var delta=b.Point-a.Point;
   if(Vector3.Dot(normal,b.Normal)<.9f||Mathf.Abs(Vector3.Dot(delta,normal))>.12f||delta.sqrMagnitude>4f*FuelPatchRadius*FuelPatchRadius)return false;
   if(!FuelClearLine(a.Point+normal*.08f,b.Point+b.Normal*.08f))return false;
   // Touching footprints on opposite ledges must not spread across empty air.
   for(int i=1;i<=3;i++){
    var point=Vector3.Lerp(a.Point,b.Point,i*.25f);RaycastHit support;
    bool supported=a.Surface.Raycast(new Ray(point+normal*.12f,-normal),out support,.24f)&&Vector3.Dot(support.normal,normal)>.8f;
    if(!supported)supported=b.Surface.Raycast(new Ray(point+normal*.12f,-normal),out support,.24f)&&Vector3.Dot(support.normal,normal)>.8f;
    if(!supported)return false;
   }return true;
  }
  void TickFuelPatches(float now){
   if(Time.timeScale<=0f||!Finite(now)||fuelPatches.Count==0)return;
   // Earliest-arrival propagation is independent of frame rate and list order.
   // The bounded graph is evaluated at most ten times a second.
   bool step=now-fuelPatchTick>=.0999f||now<fuelPatchTick;
   if(step||fuelPatchTick==0f){
    for(int pass=0;pass<fuelPatches.Count;pass++){
     bool changed=false;
     foreach(var a in fuelPatches)if(a.Surface!=null&&a.Fuel>0f&&a.IgniteAt<=now)
      foreach(var b in fuelPatches)if(b.Fuel>0f&&b.IgniteAt>a.IgniteAt+.15f&&FuelPatchesConnected(a,b)){b.IgniteAt=a.IgniteAt+.15f;changed=true;}
     if(!changed)break;
    }
   }
   fuelGroundContacts.Clear();
   for(int i=fuelPatches.Count-1;i>=0;i--){
    var patch=fuelPatches[i];
    if(patch.Surface==null||!patch.Surface.enabled||!patch.Surface.gameObject.activeInHierarchy||patch.Visual==null){DestroyFuelPatch(patch);fuelPatches.RemoveAt(i);continue;}
    float wetSeconds=FlameTuningValue(fuelWetLifetime,60f,5f,120f);
    if(float.IsPositiveInfinity(patch.IgniteAt)&&now-patch.Born>=wetSeconds){DestroyFuelPatch(patch);fuelPatches.RemoveAt(i);continue;}
    float seconds=0f;
    if(step&&patch.Fuel>0f&&patch.IgniteAt<=now){
     if(float.IsPositiveInfinity(patch.BurnEndsAt))patch.BurnEndsAt=patch.IgniteAt+Mathf.Max(FlameTuningValue(fuelMinimumBurn,15f,1f,60f),patch.Fuel/FuelPatchBurnRate);
     float previous=Mathf.Max(patch.LastStep,patch.IgniteAt),remaining=Mathf.Max(0f,patch.BurnEndsAt-previous);
     seconds=Mathf.Max(0f,Mathf.Min(now,patch.BurnEndsAt)-previous);
     patch.Fuel=now>=patch.BurnEndsAt?0f:patch.Fuel*(1f-Mathf.Clamp01(seconds/Mathf.Max(.00001f,remaining)));
     // Avoid catch-up bursts after a stalled frame. Fuel still burns with time.
     if(seconds>0f)CollectFuelGroundContacts(patch,Mathf.Min(seconds,.25f));
     if(patch.Fuel<=0f)patch.EndingAt=patch.BurnEndsAt;
    }
    if(step)patch.LastStep=now;
    float fade=float.IsPositiveInfinity(patch.EndingAt)?1f:Mathf.Clamp01(1f-(now-patch.EndingAt)/.65f);
    if(fade<=0f){DestroyFuelPatch(patch);fuelPatches.RemoveAt(i);continue;}
    UpdateFuelPatchVisual(patch,patch.IgniteAt<=now,fade);
   }
   if(step)fuelPatchTick=now;
   foreach(var pair in fuelGroundContacts){
    var actor=pair.Key;var contact=pair.Value;if(actor==null||contact.Receiver==null)continue;
    ApplyFlameDamage(contact.Receiver,contact.Damage);TouchFlameIgnite(actor,contact.Seconds,now);
   }fuelGroundContacts.Clear();
  }
  void CollectFuelGroundContacts(FuelPatch patch,float seconds){
   var point=patch.Point;var normal=patch.Normal;var start=point+normal*.08f;var end=point+normal*.55f;
   int count;Collider[] colliders;
   while(true){count=Physics.OverlapCapsuleNonAlloc(start,end,FuelPatchRadius,fuelGroundColliders,~(1<<2),QueryTriggerInteraction.Collide);
    if(count<fuelGroundColliders.Length){colliders=fuelGroundColliders;break;}
    if(fuelGroundColliders.Length>=1024){colliders=Physics.OverlapCapsule(start,end,FuelPatchRadius,~(1<<2),QueryTriggerInteraction.Collide);count=colliders.Length;break;}
    fuelGroundColliders=new Collider[fuelGroundColliders.Length*2];
   }
   for(int i=0;i<count;i++){
    var collider=colliders[i];Transform actor;PlayMakerFSM unused;
    if(collider.isTrigger&&!QualifiedFlameTrigger(collider)||!TryFlameEnemy(collider,out actor,out unused)||SolidMeleeContact.Corpse(actor))continue;
    PlayMakerFSM receiver=null;FsmFloat hp=null;
    foreach(var fsm in actor.GetComponents<PlayMakerFSM>())if(fsm!=null&&fsm.Fsm!=null){
     if(fsm.FsmName=="Bodypart"&&fsm.FsmVariables.FindFsmFloat("Damage")!=null)receiver=fsm;
     if(fsm.FsmName=="Health")hp=fsm.FsmVariables.FindFsmFloat("Health");
    }
    if(receiver==null||hp==null||hp.Value<=0f||!FuelClearLine(start,collider.ClosestPoint(end)))continue;
    FuelGroundContact old;fuelGroundContacts.TryGetValue(actor,out old);
    fuelGroundContacts[actor]=new FuelGroundContact{Receiver=receiver,Seconds=Mathf.Max(old.Seconds,seconds),Damage=Mathf.Max(old.Damage,patch.Damage*seconds)};
   }Array.Clear(fuelGroundColliders,0,fuelGroundColliders.Length);
  }
  void CreateFuelPatchVisual(FuelPatch patch){
   if(fuelPatchRoot==null)fuelPatchRoot=new GameObject("Flamethrower.FuelPatches"){hideFlags=HideFlags.DontSave};
   if(fuelWetMaterial==null){var shader=Shader.Find("Sprites/Default")??Shader.Find("Legacy Shaders/Particles/Alpha Blended");if(shader!=null)fuelWetMaterial=new Material(shader){name="Flamethrower.WetFuel",hideFlags=HideFlags.DontSave,renderQueue=3001};}
   patch.Visual=new GameObject("Flamethrower.DepositedFuel"){hideFlags=HideFlags.DontSave};patch.Visual.transform.SetParent(fuelPatchRoot.transform,false);
   patch.Visual.AddComponent<MeshFilter>().sharedMesh=patch.Mesh;patch.Wet=patch.Visual.AddComponent<MeshRenderer>();patch.Wet.sharedMaterial=fuelWetMaterial;patch.Wet.shadowCastingMode=ShadowCastingMode.Off;patch.Wet.receiveShadows=false;
   UpdateFuelPatchVisual(patch,false,1f);
  }
  void UpdateFuelPatchVisual(FuelPatch patch,bool burning,float fade){
   var target=patch.Surface.transform;patch.Visual.transform.SetPositionAndRotation(target.position,target.rotation);patch.Visual.transform.localScale=target.lossyScale;
   var wet=new Color(.12f,.08f,.025f,(burning?.18f:.42f)*fade);patch.Properties.SetColor("_Color",wet);patch.Properties.SetColor("_TintColor",wet);patch.Wet.SetPropertyBlock(patch.Properties);
   if(burning&&patch.Fire==null){
    if(fuelFireMaterial==null)fuelFireMaterial=AnimatedFireMaterial("Flamethrower.FuelFire");
    if(fuelFireMaterial!=null){
     var go=new GameObject("Flamethrower.FuelFireSheets"){hideFlags=HideFlags.DontSave};go.transform.SetParent(fuelPatchRoot.transform,false);patch.Fire=go.AddComponent<ParticleSystem>();patch.Fire.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
     uint seed=unchecked((uint)patch.Visual.GetInstanceID()*2654435761u);ConfigureFireTongues(patch.Fire,fuelFireMaterial,seed,.55f,.95f,3f,true,.85f+(seed%7)*.06f);
    }
   }
   if(patch.Fire!=null){patch.Fire.transform.SetPositionAndRotation(patch.Point+Vector3.up*.08f,Quaternion.identity);patch.Fire.transform.localScale=Vector3.one*Mathf.Sqrt(fade);FadeFireTongues(patch.Fire,fuelFireMaterial,patch.Properties,fade);}
   if(patch.Fire!=null&&patch.Fuel>0f&&burning){if(patch.Smoke==null)patch.Smoke=CreateFireSmoke(patch.Fire.transform,FireSmokeKind.Ground);}else if(patch.Smoke!=null)patch.Smoke.End(Time.time);
  }
  void DestroyFuelPatch(FuelPatch patch){if(patch.Smoke!=null)patch.Smoke.End(Time.time);if(patch.Fire!=null)Destroy(patch.Fire.gameObject);if(patch.Visual!=null)Destroy(patch.Visual);if(patch.Mesh!=null)Destroy(patch.Mesh);}
  void ClearFuelPatches(){foreach(var patch in fuelPatches)DestroyFuelPatch(patch);fuelPatches.Clear();fuelGroundContacts.Clear();fuelPatchTick=0f;if(fuelPatchRoot!=null)Destroy(fuelPatchRoot);fuelPatchRoot=null;if(fuelWetMaterial!=null)Destroy(fuelWetMaterial);fuelWetMaterial=null;if(fuelFireMaterial!=null)Destroy(fuelFireMaterial);fuelFireMaterial=null;Array.Clear(fuelGroundColliders,0,fuelGroundColliders.Length);}
 }
}
