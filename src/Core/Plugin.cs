using System;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace ChainsawPrototype {
 [Serializable] public class Pose { public Vector3 position; public Quaternion rotation; }
 [Serializable] public class PreviewData { public Vector3 leftPosition,rightPosition,bladeDirection,bladeNormal;public Quaternion leftRotation,rightRotation; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
 [BepInPlugin("apocalypter.weaponspack", "Apocalypter Weapons Pack", "1.6.4")]
 public partial class Plugin : BaseUnityPlugin {
  static Plugin instance; Harmony harmony; PreviewData data; GameObject preview; Transform weapon,camera,left,right;
  Mesh heldMesh;Material chainsawMaterial;bool enabledPreview;Transform nativeRigSource;bool calibratedIK;ChainsawItem lastHeldChain;float lastHeldSeen;Transform nativeView;bool nativeWasActive;PlayMakerFSM heldFsm;
  void Awake() {
   // Apocasetter treats this as metadata and excludes it from player settings.
   Config.Bind("General","Apocasetter",true,"Show this mod in the Apocasetter Mods menu.");
   instance=this;InstallBundledWeaponProfiles();SetupPlacementConfig();SetupPoseControls();SetupCombat();SetupChainsawHitShake();SetupCuttingAttack();SetupOwnedMovesets();SetupFlamethrower();SetupChainsawWorldSpawns();TrimWeaponSettings();Camera.onPreRender+=ProtectChainsawRender;Camera.onPreCull+=ProtectChainsawRender;
   try {
    LoadIcon();string folder=Path.GetDirectoryName(Info.Location);
    data=JsonUtility.FromJson<PreviewData>(File.ReadAllText(Path.Combine(folder,"ChainsawIdle.json")));
    if(Quaternion.Dot(data.rightRotation,data.rightRotation)<.5f || Quaternion.Dot(data.leftRotation,data.leftRotation)<.5f)throw new Exception("ChainsawIdle.json contains invalid hand rotations");
    Vector3 grip=data.rightPosition;Vector3 axis=data.bladeDirection.normalized;Vector3 bladeUp=Vector3.Cross(data.bladeNormal,axis).normalized;if(bladeUp.y<0)bladeUp=-bladeUp;Quaternion tilt=Quaternion.LookRotation(new Vector3(-.35f,.08f,1f).normalized,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(axis,bladeUp));for(int v=0;v<data.vertices.Length;v++)data.vertices[v]=grip+tilt*(data.vertices[v]-grip);data.leftPosition=grip+tilt*(data.leftPosition-grip);data.leftRotation=tilt*data.leftRotation;data.rightRotation=tilt*data.rightRotation;Vector3 min=data.vertices[0],max=min;
    foreach(var point in data.vertices){min=Vector3.Min(min,point);max=Vector3.Max(max,point);}
    var size=max-min;float modelScale=Mathf.Min(1f,.65f/Mathf.Max(size.x,Mathf.Max(size.y,size.z)));
    float nearest=float.MaxValue;for(int v=0;v<data.vertices.Length;v++){data.vertices[v]=grip+(data.vertices[v]-grip)*modelScale;nearest=Mathf.Min(nearest,data.vertices[v].z);}
    Vector3 offset=new Vector3(0,0,Mathf.Max(0,.12f-nearest));for(int v=0;v<data.vertices.Length;v++)data.vertices[v]+=offset;
    data.leftPosition=grip+(data.leftPosition-grip)*modelScale+offset;data.rightPosition+=offset;
    Logger.LogInfo("Chainsaw frame conversion: scale="+modelScale+", camera offset="+offset+", left="+data.leftPosition+", right="+data.rightPosition);
    for(int i=0;i<data.triangles.Length;i+=3){int swap=data.triangles[i+1];data.triangles[i+1]=data.triangles[i+2];data.triangles[i+2]=swap;}
    var mesh=new Mesh();mesh.name="Chainsaw.SavedIdle.Mesh";mesh.vertices=data.vertices;mesh.uv=data.uv;mesh.triangles=data.triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
    heldMesh=mesh;heldMesh.hideFlags=HideFlags.DontUnloadUnusedAsset;
    var material=new Material(Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse"));
    var texture=new Texture2D(2,2);ImageConversion.LoadImage(texture,File.ReadAllBytes(Path.Combine(folder,"ChainSaw_Clean.png")));material.mainTexture=texture;material.SetFloat("_Glossiness",.12f);
    chainsawMaterial=material;chainsawMaterial.hideFlags=HideFlags.DontUnloadUnusedAsset;texture.hideFlags=HideFlags.DontUnloadUnusedAsset;
    harmony=new Harmony("apocalypter.weaponspack");SetupFlamethrowerFuelPouring();
    SetupChainsawPersistence();
    SetupChainsawCodexGuard();
    Type ik=AccessTools.AllTypes().FirstOrDefault(t=>t.Name=="FastIKFabric");var method=ik==null?null:AccessTools.Method(ik,"LateUpdate");
    if(method==null)throw new Exception("Native FastIKFabric.LateUpdate was not found");

    EnsureRunner();UnityEngine.SceneManagement.SceneManager.sceneLoaded+=(scene,mode)=>EnsureRunner();
    Logger.LogInfo("Chainsaw loaded from separate ChainsawIdle.json. Spawn a NEW Chainsaw in Inspector.");
   }catch(Exception e){Logger.LogError(e);enabled=false;}
  }
  static bool IsActuallyCarried(ChainsawItem item){if(item==null || !item.gameObject.activeInHierarchy)return false;for(var p=item.transform.parent;p!=null;p=p.parent)if(p.name=="HandItemUse")return true;return false;}
  ChainsawItem ReadHeldChainsaw(){
   if(heldFsm==null){var c=GameObject.Find("PlayerCamera");if(c!=null)heldFsm=c.GetComponentsInChildren<PlayMakerFSM>(true).FirstOrDefault(f=>f.FsmName=="WeaponInHand");}
   if(heldFsm!=null){var selected=heldFsm.FsmVariables.FindFsmGameObject("Item")??heldFsm.FsmVariables.FindFsmGameObject("item");
    if(selected!=null&&selected.Value!=null){var marker=selected.Value.GetComponent<ChainsawItem>();if(marker==null)return null;
     // Native carrier visibility may deactivate the item without changing the selected inventory slot.
     for(var parent=marker.transform.parent;parent!=null;parent=parent.parent)if(parent.name=="HandItemUse")return marker;
     return null;
    }
    return null;
   }
   foreach(var physical in ChainsawItem.Active)if(physical!=null&&physical.gameObject!=template&&IsActuallyCarried(physical))return physical;
   return null;
  }
  ChainsawItem FindHeldChainsaw(){
   var found=ReadHeldChainsaw();if(!SelectedPlayerCanPresent()){lastHeldChain=null;return null;}if(found!=null){lastHeldChain=found;lastHeldSeen=Time.unscaledTime;return found;}
   bool anotherWeapon=false;if(heldFsm!=null){var current=heldFsm.FsmVariables.FindFsmGameObject("Item")??heldFsm.FsmVariables.FindFsmGameObject("item");anotherWeapon=current!=null&&current.Value!=null&&current.Value.GetComponent<ChainsawItem>()==null;}
   if(!anotherWeapon && lastHeldChain!=null && Time.unscaledTime-lastHeldSeen<.15f){for(var parent=lastHeldChain.transform.parent;parent!=null;parent=parent.parent)if(parent.name=="HandItemUse")return lastHeldChain;}
   lastHeldChain=null;return null;
  }
  void FindWeapon(){
   if(weapon!=null && camera!=null)return;
   weapon=null;left=null;right=null;camera=null;
   foreach(var t in Resources.FindObjectsOfTypeAll<Transform>()){
    if(t.name!="m16a1" || !t.gameObject.scene.IsValid())continue;
    var c=t.GetComponentInParent<Camera>();if(c==null)continue;
    var targets=t.GetComponentsInChildren<Transform>(true);if(!targets.Any(x=>x.name=="IK Target L"))continue;
    camera=c.transform;nativeRigSource=t;calibratedIK=false;cachedIK=null;m16HoldingReady=false;
    // A distinct clone owns its skeleton, targets and saved idle. Native rifle data stays intact.
    var staging=new GameObject("Chainsaw.RigStaging");staging.SetActive(false);
    var clone=UnityEngine.Object.Instantiate(t.gameObject,staging.transform,false);clone.name="Chainsaw.HeldRig";foreach(var node in clone.GetComponentsInChildren<Transform>(true))node.gameObject.tag="Untagged";
    foreach(var script in clone.GetComponentsInChildren<MonoBehaviour>(true))if(script.GetType().Name!="FastIKFabric")UnityEngine.Object.DestroyImmediate(script);else script.enabled=false;
    foreach(var tween in clone.GetComponentsInChildren<Component>(true))if(tween!=null && tween.GetType().Name=="iTween")UnityEngine.Object.DestroyImmediate(tween);foreach(var animator in clone.GetComponentsInChildren<Animator>(true))animator.enabled=false;
    foreach(var animation in clone.GetComponentsInChildren<Animation>(true))animation.enabled=false;
    foreach(var collider in clone.GetComponentsInChildren<Collider>(true))collider.enabled=false;foreach(var body in clone.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;}
    foreach(var r in clone.GetComponentsInChildren<Renderer>(true)){var skin=r as SkinnedMeshRenderer;if(skin==null)r.enabled=false;else skin.localBounds=new Bounds(skin.localBounds.center,Vector3.one*4f);}
    clone.transform.SetParent(t.parent,false);clone.transform.localPosition=t.localPosition;clone.transform.localRotation=t.localRotation;clone.transform.localScale=t.localScale;
    // Keep the private arm rig in the same camera frame as the held model, outside native rifle sway.
    clone.transform.SetParent(camera,true);
    clone.SetActive(false);Destroy(staging);weapon=clone.transform;
    foreach(var target in weapon.GetComponentsInChildren<Transform>(true)){if(target.name=="IK Target L")left=target;if(target.name=="IK Target R")right=target;}
    EnsureHeldModel();var arm=clone.GetComponentInChildren<SkinnedMeshRenderer>(true);preview.layer=arm!=null?arm.gameObject.layer:0;
    Logger.LogInfo("Created independent Chainsaw.HeldRig with cloned native arms and dedicated IK targets.");break;
   }
  }
  void EnsureHeldModel(){if(preview!=null)return;preview=new GameObject("Chainsaw.HeldModel");preview.AddComponent<MeshFilter>().sharedMesh=heldMesh;preview.AddComponent<MeshRenderer>().sharedMaterial=chainsawMaterial;preview.SetActive(false);if(camera!=null)preview.transform.SetParent(camera,false);Logger.LogInfo("Recreated gameplay chainsaw mesh: "+heldMesh.vertexCount+" vertices.");}
  void StartPreview(){
   if(weapon==null || left==null || right==null)return;
   EnsureHeldModel();preview.transform.SetParent(camera,false);preview.transform.localPosition=Vector3.zero;preview.transform.localRotation=Quaternion.identity;preview.transform.localScale=Vector3.one;
   weapon.gameObject.SetActive(true);CalibrateClonedIK();ResetHeldMotion();ApplyTargets();SuppressCarrier();enabledPreview=true;preview.SetActive(true);Logger.LogInfo("Chainsaw independent saved idle applied on equip.");
  }
  void SuppressCarrier(){
   if(camera==null)return;
   if(nativeView==null)foreach(var t in camera.GetComponentsInChildren<Transform>(false)){
    if(t==weapon || t.IsChildOf(weapon))continue;
    if(t.parent!=null && t.parent.name=="Parent" && t.GetComponentsInChildren<Transform>(true).Any(x=>x.name=="IK Target L")){nativeView=t;nativeWasActive=t.gameObject.activeSelf;break;}
   }
   if(nativeView!=null)SuppressNativeCarrierPresentation();
  }
  void ApplyTargets(){if(left==null||right==null||camera==null)return;ApplyEditablePose();}
  void StopPreview(){ResetCuttingAttack();CleanupChainsawHitShake();gameAttacking=false;applyingAttack=false;forcedPose=null;swinging=false;hitThisSwing=false;BindFuel(null);if(sawEngine!=null)sawEngine.Stop();RestoreNativeCarrierPresentation();var selected=heldFsm==null?null:(heldFsm.FsmVariables.FindFsmGameObject("Item")??heldFsm.FsmVariables.FindFsmGameObject("item"));Logger.LogInfo("Held preview stopping: selected="+(selected==null||selected.Value==null?"null":selected.Value.name)+"; last="+(lastHeldChain==null?"null":lastHeldChain.name)+"; fuelOwnerParent="+(fuelOwner==null||fuelOwner.transform.parent==null?"null":fuelOwner.transform.parent.name));RestoreBodyArms();HideThirdPerson();Logger.LogInfo("Chainsaw dropped/unequipped: disabling held model and arms.");enabledPreview=false;if(preview!=null)preview.SetActive(false);if(weapon!=null)weapon.gameObject.SetActive(false);if(nativeView!=null && heldFsm!=null){var current=heldFsm.FsmVariables.FindFsmGameObject("Item")??heldFsm.FsmVariables.FindFsmGameObject("item");if(current!=null && current.Value!=null && current.Value.GetComponent<ChainsawItem>()==null){var type=current.Value.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="weaponType");var value=type==null?null:type.FsmVariables.FindFsmString("weaponType");if(value!=null && string.Equals(value.Value,nativeView.name,StringComparison.OrdinalIgnoreCase))nativeView.gameObject.SetActive(nativeWasActive);}}nativeView=null;}
 }
}
