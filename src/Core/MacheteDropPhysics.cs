using System;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  float nextMacheteScan;
  void ScanMachetePhysics(){
   if(Time.unscaledTime<nextMacheteScan)return;nextMacheteScan=Time.unscaledTime+4f;
   foreach(var body in Resources.FindObjectsOfTypeAll<Rigidbody>()){
    if(!body.gameObject.scene.IsValid()||!body.name.StartsWith("machete",StringComparison.OrdinalIgnoreCase)||body.GetComponent<MacheteDropPhysics>()!=null)continue;
    var fsm=body.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="weaponType");var kind=fsm==null?null:fsm.FsmVariables.FindFsmString("weaponType");
    if(kind!=null&&kind.Value=="machete")body.gameObject.AddComponent<MacheteDropPhysics>();
   }
  }
 }
 sealed class MacheteDropPhysics:MonoBehaviour {
  Rigidbody body;float nextCheck;bool dropped;
  void Awake(){body=GetComponent<Rigidbody>();}
  void LateUpdate(){
   if(body==null||Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.1f;
   bool world=gameObject.activeInHierarchy;
   for(var p=transform.parent;p!=null;p=p.parent){string n=p.name.ToLowerInvariant();if(n=="handitemuse"||n=="player"||n=="playercamera"||n=="playercameraholder"||n=="weaponsarm"||n.Contains("inventory")||n.Contains("backpack"))world=false;}
   if(!world){dropped=false;return;}
   bool repair=!dropped||!body.detectCollisions||body.isKinematic;dropped=true;if(!repair)return;
   bool solid=false;foreach(var c in GetComponentsInChildren<Collider>(true)){
    if(c.isTrigger)continue;var mesh=c as MeshCollider;if(mesh!=null&&!mesh.convex){c.enabled=false;continue;}
    c.enabled=true;c.gameObject.layer=9;solid=true;
   }
   if(!solid){var mesh=GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m=>m.sharedMesh!=null);if(mesh!=null){var box=mesh.gameObject.AddComponent<BoxCollider>();box.center=mesh.sharedMesh.bounds.center;var size=mesh.sharedMesh.bounds.size;var scale=mesh.transform.lossyScale;box.size=new Vector3(Mathf.Max(size.x,.015f/Mathf.Max(Mathf.Abs(scale.x),.001f)),Mathf.Max(size.y,.015f/Mathf.Max(Mathf.Abs(scale.y),.001f)),Mathf.Max(size.z,.015f/Mathf.Max(Mathf.Abs(scale.z),.001f)));mesh.gameObject.layer=9;}}
   gameObject.layer=9;body.isKinematic=false;body.detectCollisions=true;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.WakeUp();
  }
 }
}
