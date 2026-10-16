using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChainsawPrototype {
 // Keep every physical collider on the weapon root: native pickup actions inspect
 // the hit GameObject for weaponType rather than searching its parents.
 [DefaultExecutionOrder(31990)]
 public sealed class ChainsawWorldCollision : MonoBehaviour {
  [SerializeField] Transform visual;
  [SerializeField] Collider[] inheritedSolids;
  [SerializeField] MeshCollider[] shapes;
  // A flat primitive inside the casing supplies sweep CCD; the surrounding
  // convex hulls retain the detailed blade, body and handle contact surfaces.
  [SerializeField] BoxCollider motorCore;
  Mesh[] ownedMeshes;
  Vector3[][] sourcePoints;
  Vector3[][] scaledPoints;
  Matrix4x4 lastVisualMatrix;
  Rigidbody body;
  bool initialized,matrixReady,wasCarried=true;

  internal void Configure(Transform model){
   visual=model;
   Initialize();
   ApplyWorldState(false);
  }

  void Awake(){Initialize();}
  void OnEnable(){ApplyWorldState(InPlayerHands());}
  void FixedUpdate(){ApplyWorldState(InPlayerHands());}

  bool InPlayerHands(){
   for(var parent=transform.parent;parent!=null;parent=parent.parent)
    if(parent.name=="HandItemUse" || parent.name=="PlayerCamera")return true;
   return false;
  }

  void Initialize(){
   if(initialized)return;
   if(visual==null)visual=transform.Find("ChainsawModel");
   var filter=visual==null?null:visual.GetComponent<MeshFilter>();
   if(filter==null || filter.sharedMesh==null)return;
   var model=filter.sharedMesh;
   if(model.vertexCount!=ChainsawCollisionGeometry.SourceVertexCount)
    throw new InvalidOperationException("Chainsaw collider topology does not match ChainsawIdle.json. Re-export Assets/export_world_collision.py for the new model.");
   if(inheritedSolids==null){
    var originals=new List<Collider>();
    foreach(var collider in GetComponentsInChildren<Collider>(true)){
     if(collider.isTrigger)continue;
     bool owned=collider==motorCore;
     if(shapes!=null)foreach(var shape in shapes)if(shape==collider){owned=true;break;}
     if(!owned)originals.Add(collider);
    }
    inheritedSolids=originals.ToArray();
   }
   var vertices=model.vertices;
   int count=ChainsawCollisionGeometry.Vertices.Length;
   if(shapes==null || shapes.Length!=count)shapes=new MeshCollider[count];
   ownedMeshes=new Mesh[count];sourcePoints=new Vector3[count][];scaledPoints=new Vector3[count][];
   PhysicMaterial material=null;
   foreach(var original in inheritedSolids)if(original!=null){material=original.sharedMaterial;if(material!=null)break;}
   for(int part=0;part<count;part++){
    var indices=ChainsawCollisionGeometry.Vertices[part];
    sourcePoints[part]=new Vector3[indices.Length];scaledPoints[part]=new Vector3[indices.Length];
    for(int vertex=0;vertex<indices.Length;vertex++)sourcePoints[part][vertex]=vertices[indices[vertex]];
    string name=part==ChainsawCollisionGeometry.BodyHull?"TankBody":part==ChainsawCollisionGeometry.BladeHull?"Blade":"Detail."+part;
    var mesh=new Mesh();mesh.name="Chainsaw.WorldCollision."+name;mesh.hideFlags=HideFlags.DontUnloadUnusedAsset;
    mesh.vertices=sourcePoints[part];mesh.triangles=ChainsawCollisionGeometry.Triangles[part];mesh.RecalculateBounds();
    ownedMeshes[part]=mesh;
    if(shapes[part]==null)shapes[part]=gameObject.AddComponent<MeshCollider>();
    var collider=shapes[part];collider.enabled=false;collider.isTrigger=false;collider.convex=true;
    collider.cookingOptions=MeshColliderCookingOptions.CookForFasterSimulation|MeshColliderCookingOptions.EnableMeshCleaning|MeshColliderCookingOptions.WeldColocatedVertices;
    collider.sharedMaterial=material;collider.contactOffset=.002f;collider.sharedMesh=mesh;
   }
   if(motorCore==null)motorCore=gameObject.AddComponent<BoxCollider>();
   motorCore.enabled=false;motorCore.isTrigger=false;motorCore.sharedMaterial=material;motorCore.contactOffset=.002f;
   body=GetComponent<Rigidbody>();
   initialized=true;
   SuppressInheritedSolids();
  }

  void SuppressInheritedSolids(){
   if(inheritedSolids==null)return;
   foreach(var collider in inheritedSolids)if(collider!=null && collider.enabled)collider.enabled=false;
  }

  internal void ApplyWorldState(bool carried){
   Initialize();
   if(!initialized)return;
   // Native LockPhysics can remove the pickup's Rigidbody and create a new
   // one later. Never keep using only the component captured at initialization.
   bool newBody=false;
   if(body==null){body=GetComponent<Rigidbody>();newBody=body!=null;}
   SuppressInheritedSolids();
   bool changed=SyncVisualShape();
   for(int part=0;part<shapes.Length;part++){
    var shape=shapes[part];if(shape==null)continue;
    // Native pickup/save actions can overwrite collider state independently of
    // the Rigidbody. Restore the actual solid and mesh, not just its enabled flag.
    if(shape.isTrigger){shape.isTrigger=false;changed=true;}
    if(!shape.convex){shape.convex=true;changed=true;}
    if(shape.sharedMesh!=ownedMeshes[part]){shape.sharedMesh=ownedMeshes[part];changed=true;}
    shape.enabled=!carried && matrixReady;
   }
   if(motorCore!=null){motorCore.isTrigger=false;motorCore.enabled=!carried && matrixReady;}
   if(body!=null){
    bool collide=!carried && matrixReady;
    bool repaired=body.detectCollisions!=collide;
    body.detectCollisions=collide;
    if(carried && body.collisionDetectionMode!=CollisionDetectionMode.Discrete)body.collisionDetectionMode=CollisionDetectionMode.Discrete;
    if(!carried && !WeaponMerchantSale.IsForSale(gameObject)){
     // A copied/loaded or newly added world body can retain the held state.
     // Repair before the physics step, including spawns that were never held.
     repaired|=body.isKinematic || !body.useGravity;
     if(body.isKinematic)body.isKinematic=false;
     if(!body.useGravity)body.useGravity=true;
     if(gameObject.layer!=9){gameObject.layer=9;repaired=true;}
     // Unity's sweep CCD requires a primitive collider. Keep the casing box
     // active so a fast drop/throw cannot rely solely on speculative mesh contacts.
     if(body.collisionDetectionMode!=CollisionDetectionMode.ContinuousDynamic){body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;repaired=true;}
     if(wasCarried || changed || newBody || repaired){
      body.ResetCenterOfMass();body.ResetInertiaTensor();body.WakeUp();
     }
    }
   }
   wasCarried=carried;
  }

  bool SyncVisualShape(){
   // The native M16 carrier can have a non-unit inherited scale. The renderer
   // compensates that scale; transform its points into root space for colliders.
   // Compose local transforms instead of subtracting distant world positions:
   // world matrix cancellation otherwise produces jitter and needless recooking.
   var matrix=Matrix4x4.identity;
   for(var node=visual;node!=null && node!=transform;node=node.parent)
    matrix=Matrix4x4.TRS(node.localPosition,node.localRotation,node.localScale)*matrix;
   bool same=matrixReady;
   for(int i=0;i<16;i++){
    float value=matrix[i];
    if(float.IsNaN(value) || float.IsInfinity(value)){matrixReady=false;return false;}
    if(!matrixReady || Mathf.Abs(value-lastVisualMatrix[i])>.00001f)same=false;
   }
   for(int part=0;part<ownedMeshes.Length;part++)if(ownedMeshes[part]==null){
    var mesh=new Mesh();mesh.name="Chainsaw.WorldCollision.Rebuilt."+part;mesh.hideFlags=HideFlags.DontUnloadUnusedAsset;
    mesh.vertices=sourcePoints[part];mesh.triangles=ChainsawCollisionGeometry.Triangles[part];ownedMeshes[part]=mesh;same=false;
   }
   if(same)return false;
   for(int part=0;part<shapes.Length;part++){
    var points=scaledPoints[part];
    for(int vertex=0;vertex<points.Length;vertex++)points[vertex]=matrix.MultiplyPoint3x4(sourcePoints[part][vertex]);
    var collider=shapes[part];collider.enabled=false;collider.sharedMesh=null;
    ownedMeshes[part].vertices=points;ownedMeshes[part].RecalculateBounds();
    collider.sharedMesh=ownedMeshes[part];
   }
   FitMotorCore();
   lastVisualMatrix=matrix;matrixReady=true;
   return true;
  }

  void FitMotorCore(){
   var points=scaledPoints[ChainsawCollisionGeometry.BodyHull];
   Vector3 center=Vector3.zero,min=points[0],max=points[0];
   foreach(var point in points){center+=point;min=Vector3.Min(min,point);max=Vector3.Max(max,point);}
   center/=points.Length;
   var half=Vector3.Min(center-min,max-center);
   // Shrink an axis-aligned box against every casing hull plane. All eight
   // corners then stay inside the casing rather than protruding or rolling.
   float fit=1f;var triangles=ChainsawCollisionGeometry.Triangles[ChainsawCollisionGeometry.BodyHull];
   for(int i=0;i<triangles.Length;i+=3){
    var a=points[triangles[i]];var normal=Vector3.Cross(points[triangles[i+1]]-a,points[triangles[i+2]]-a);
    float clearance=Mathf.Abs(Vector3.Dot(normal,center-a));
    float extent=Mathf.Abs(normal.x)*half.x+Mathf.Abs(normal.y)*half.y+Mathf.Abs(normal.z)*half.z;
    if(extent>1e-12f)fit=Mathf.Min(fit,clearance/extent);
   }
   motorCore.center=center;motorCore.size=half*(2f*fit*.98f);
  }

  void OnDestroy(){
   if(ownedMeshes==null)return;
   foreach(var mesh in ownedMeshes)if(mesh!=null)Destroy(mesh);
  }
 }
}
