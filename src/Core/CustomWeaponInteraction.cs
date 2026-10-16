using UnityEngine;

namespace ChainsawPrototype {
 // Native UseWeapon picks the hit collider's GameObject directly. Keep this
 // query collider on the item root, and separate it from dropped world solids.
 [DefaultExecutionOrder(31995)]
 public sealed class CustomWeaponInteraction : MonoBehaviour {
  [SerializeField] BoxCollider grabQuery;
  MeshFilter[] visibleMeshes;
  Rigidbody body;

  internal static void Ensure(GameObject item){
   CustomWeaponDrop.Ensure(item);
   if(item.GetComponent<CustomWeaponInteraction>()==null)item.AddComponent<CustomWeaponInteraction>();
  }
  void Awake(){
   if(grabQuery==null){grabQuery=gameObject.AddComponent<BoxCollider>();grabQuery.enabled=false;grabQuery.isTrigger=true;}
   var model=transform.Find("ChainsawModel")??transform.Find("FlamethrowerModel");
   visibleMeshes=model==null?new MeshFilter[0]:model.GetComponentsInChildren<MeshFilter>(true);
   body=GetComponent<Rigidbody>();
  }
  void Update(){ApplyGrabQuery();}
  // Run after ChainsawWorldCollision's FixedUpdate and the item visibility
  // pass. Only this trigger is restored; the item's solid hulls stay disabled.
  void FixedUpdate(){ApplyGrabQuery();}
  void LateUpdate(){ApplyGrabQuery();}
  void OnDisable(){if(grabQuery!=null)grabQuery.enabled=false;}

  internal static bool InPhysicalGrab(Transform item){
   bool inHand=false;
   for(var parent=item.parent;parent!=null;parent=parent.parent){
    if(parent.name=="HandItemUse")return false;
    if(parent.name=="Hand")inHand=true;
    if(parent.name=="PlayerCamera")return inHand;
   }
   return false;
  }
  void ApplyGrabQuery(){
   if(grabQuery==null)return;
   bool grabbed=gameObject.activeInHierarchy&&InPhysicalGrab(transform);
   grabQuery.isTrigger=true;grabQuery.enabled=grabbed;
   if(!grabbed)return;
   bool hasBounds=false;Bounds bounds=new Bounds();
   foreach(var filter in visibleMeshes){
    if(filter==null||filter.sharedMesh==null)continue;
    var meshBounds=filter.sharedMesh.bounds;
    var intoRoot=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
    for(int corner=0;corner<8;corner++){
     var offset=new Vector3((corner&1)==0?-meshBounds.extents.x:meshBounds.extents.x,(corner&2)==0?-meshBounds.extents.y:meshBounds.extents.y,(corner&4)==0?-meshBounds.extents.z:meshBounds.extents.z);
     var point=intoRoot.MultiplyPoint3x4(meshBounds.center+offset);
     if(!hasBounds){bounds=new Bounds(point,Vector3.zero);hasBounds=true;}else bounds.Encapsulate(point);
    }
   }
   if(hasBounds){grabQuery.center=bounds.center;grabQuery.size=Vector3.Max(bounds.size,Vector3.one*.008f);}
   if(body==null)body=GetComponent<Rigidbody>();
   // Keep detection available for native item queries while only the private
   // trigger is active. Native gravity, velocity and grab/throw state are kept.
   if(body!=null)body.detectCollisions=true;
  }
 }
}
