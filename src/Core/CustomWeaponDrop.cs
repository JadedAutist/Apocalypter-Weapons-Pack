using System.Collections.Generic;
using UnityEngine;

namespace ChainsawPrototype {
 // Runs after the custom world-collider repair and native inventory actions,
 // but before physics simulation. Native drop/throw velocity is never changed.
 [DefaultExecutionOrder(32001)]
 public sealed class CustomWeaponDrop : MonoBehaviour {
  struct IgnoredPair {internal Collider weapon,player;internal IgnoredPair(Collider w,Collider p){weapon=w;player=p;}}
  readonly List<IgnoredPair> ignored=new List<IgnoredPair>();
  readonly List<Collider> playerSolids=new List<Collider>();
  MeshFilter[] meshes;
  Transform player,view;
  bool observed,wasCarried,releasing,placePending;
  int clearSteps;
  float releasedAt;

  internal static void Ensure(GameObject item){if(item.GetComponent<CustomWeaponDrop>()==null)item.AddComponent<CustomWeaponDrop>();}
  void Awake(){
   var model=transform.Find("ChainsawModel")??transform.Find("FlamethrowerModel");
   meshes=model==null?new MeshFilter[0]:model.GetComponentsInChildren<MeshFilter>(true);
   ObserveCarry();
  }
  void OnEnable(){ObserveCarry();}
  void OnTransformParentChanged(){ObserveCarry();}
  void LateUpdate(){UpdateRelease(false);}
  void FixedUpdate(){UpdateRelease(true);}
  void OnDisable(){RestorePairs();releasing=false;placePending=false;}
  void OnDestroy(){RestorePairs();}

  internal static bool InPlayerCarry(Transform item){
   for(var p=item.parent;p!=null;p=p.parent)
    if(p.name=="PlayerCamera"||p.name=="HandItemUse"||p.name=="Slot 1"||p.name=="Slot 2"||p.name=="Slot 3")return true;
   return false;
  }
  void ObserveCarry(){
   bool carried=InPlayerCarry(transform);
   if(observed&&wasCarried&&!carried&&gameObject.scene.IsValid()){
    releasing=true;placePending=true;clearSteps=0;releasedAt=Time.unscaledTime;
    RefreshPlayer();IgnorePlayerPairs();
   }
   if(carried){RestorePairs();releasing=false;placePending=false;}
   observed=true;wasCarried=carried;
  }
  void RefreshPlayer(){
   if(player==null){var go=GameObject.Find("Player");player=go==null?null:go.transform;}
   if(view==null){var go=GameObject.Find("PlayerCamera");view=go==null?null:go.transform;}
   playerSolids.Clear();
   if(player==null)return;
   foreach(var c in player.GetComponentsInChildren<Collider>(true))
    if(c!=null&&c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&!c.transform.IsChildOf(transform)&&(view==null||!c.transform.IsChildOf(view)))playerSolids.Add(c);
  }
  bool TryWeaponBounds(out Bounds bounds){
   bounds=new Bounds();bool found=false;
   foreach(var mesh in meshes){
    if(mesh==null||mesh.sharedMesh==null)continue;
    var b=mesh.sharedMesh.bounds;var matrix=mesh.transform.localToWorldMatrix;
    for(int i=0;i<8;i++){
     var point=matrix.MultiplyPoint3x4(b.center+new Vector3((i&1)==0?-b.extents.x:b.extents.x,(i&2)==0?-b.extents.y:b.extents.y,(i&4)==0?-b.extents.z:b.extents.z));
     if(found)bounds.Encapsulate(point);else{bounds=new Bounds(point,Vector3.zero);found=true;}
    }
   }
   return found;
  }
  bool PlayerBounds(out Bounds bounds){
   bounds=new Bounds();bool found=false;
   foreach(var c in playerSolids){if(c==null||!c.enabled)continue;if(found)bounds.Encapsulate(c.bounds);else{bounds=c.bounds;found=true;}}
   return found;
  }
  static float Support(Vector3 extents,Vector3 direction){return extents.x*Mathf.Abs(direction.x)+extents.y*Mathf.Abs(direction.y)+extents.z*Mathf.Abs(direction.z);}
  internal static Vector3 ClearanceOffset(Bounds item,Bounds character,Vector3 direction){
   direction.y=0;direction=direction.normalized;
   float distance=Support(character.extents,direction)+Support(item.extents,direction)+.12f-Vector3.Dot(item.center-character.center,direction);
   return direction*Mathf.Max(0,distance);
  }
  bool WorldSolid(Collider c){
   return c!=null&&c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&!c.transform.IsChildOf(transform)&&!playerSolids.Contains(c);
  }
  bool VolumeClear(Vector3 center,Vector3 extents){
   foreach(var c in Physics.OverlapBox(center,extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))if(WorldSolid(c))return false;
   return true;
  }
  bool RouteClear(Bounds item,Vector3 offset){
   float distance=offset.magnitude;if(distance<.001f)return false;
   var extents=Vector3.Max(item.extents,Vector3.one*.005f)+Vector3.one*.015f;
   // A shape cast catches thin barriers; start/route volumes also reject an
   // initially intersecting obstacle, which Unity casts alone do not report.
   if(!VolumeClear(item.center,extents))return false;
   foreach(var hit in Physics.BoxCastAll(item.center,extents,offset/distance,Quaternion.identity,distance,~0,QueryTriggerInteraction.Ignore))if(WorldSolid(hit.collider))return false;
   int steps=Mathf.CeilToInt(distance/.12f);
   for(int step=1;step<=steps;step++)if(!VolumeClear(item.center+offset*((float)step/steps),extents))return false;
   return true;
  }
  void PlaceClearOfPlayer(){
   Bounds item,character;if(!TryWeaponBounds(out item)||!PlayerBounds(out character))return;
   var padded=character;padded.Expand(.24f);if(!padded.Intersects(item))return;
   var forward=view==null?Vector3.forward:view.forward;forward.y=0;
   if(forward.sqrMagnitude<.001f)forward=player==null?Vector3.forward:player.forward;
   forward.y=0;forward=forward.normalized;
   var right=new Vector3(forward.z,0,-forward.x);
   var directions=new[]{forward,(forward+right).normalized,(forward-right).normalized,right,-right,(-forward+right).normalized,(-forward-right).normalized,-forward};
   foreach(var direction in directions){
    var offset=ClearanceOffset(item,character,direction);
    if(offset.sqrMagnitude>9f||!RouteClear(item,offset))continue;
    // Translation only: retain rotation, scale, native velocity and angular
    // velocity. Near walls, keep the native drop point and ignore the player
    // until the item falls clear instead of teleporting through the obstacle.
    var destination=transform.position+offset;var body=GetComponent<Rigidbody>();
    transform.position=destination;if(body!=null)body.position=destination;
    Physics.SyncTransforms();break;
   }
  }
  void IgnorePlayerPairs(){
   if(playerSolids.Count==0)return;
   foreach(var weapon in GetComponentsInChildren<Collider>(true)){
    if(weapon==null||!weapon.enabled||weapon.isTrigger||!weapon.gameObject.activeInHierarchy)continue;
    foreach(var character in playerSolids){
     if(character==null)continue;bool owned=false;
     foreach(var pair in ignored)if(pair.weapon==weapon&&pair.player==character){owned=true;break;}
     if(!owned&&Physics.GetIgnoreCollision(weapon,character))continue;
     if(!owned)ignored.Add(new IgnoredPair(weapon,character));
     // Disabling a native pickup collider may discard IgnoreCollision state.
     Physics.IgnoreCollision(weapon,character,true);
    }
   }
  }
  void RestorePairs(){
   foreach(var pair in ignored)if(pair.weapon!=null&&pair.player!=null)Physics.IgnoreCollision(pair.weapon,pair.player,false);
   ignored.Clear();
  }
  void UpdateRelease(bool physicsStep){
   ObserveCarry();if(!releasing||!gameObject.activeInHierarchy)return;
   RefreshPlayer();
   if(placePending&&playerSolids.Count>0){PlaceClearOfPlayer();placePending=false;}
   IgnorePlayerPairs();
   if(!physicsStep)return;
   Bounds item,character;
   if(!TryWeaponBounds(out item)||!PlayerBounds(out character))return;
   character.Expand(.08f);clearSteps=character.Intersects(item)?0:clearSteps+1;
   if(clearSteps>=2&&Time.unscaledTime-releasedAt>=.12f){RestorePairs();releasing=false;}
  }
 }
}
