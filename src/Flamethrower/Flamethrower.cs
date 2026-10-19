using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 public sealed class FlamethrowerItem:MonoBehaviour {
  internal static readonly List<FlamethrowerItem> Active=new List<FlamethrowerItem>();
  void OnEnable(){if(!Active.Contains(this))Active.Add(this);}void OnDisable(){Active.Remove(this);}
  internal Transform HoldingRig,HoldingModel;
  internal bool PhysicsGrabbed {get;private set;}
  internal bool SpawnFuelHandled;
  Renderer sentinel;Renderer[] allRenderers;MeshRenderer[] models;Collider[] colliders;BoxCollider solid;Rigidbody body;bool wasHeld;ChainsawFuel itemFuel;float displayedDamage=float.NaN,displayedLitres=float.NaN,displayedRange=float.NaN;FlameFuelKind displayedKind;
  void Start(){allRenderers=GetComponentsInChildren<Renderer>(true);models=GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.transform.parent!=null&&r.transform.parent.name=="FlamethrowerModel").ToArray();sentinel=allRenderers.FirstOrDefault(r=>!models.Contains(r as MeshRenderer));colliders=GetComponentsInChildren<Collider>(true);solid=GetComponents<BoxCollider>().LastOrDefault(c=>!c.isTrigger);body=GetComponent<Rigidbody>();CustomWeaponInteraction.Ensure(gameObject);Plugin.InitializeSpawnFuel(gameObject);}
  internal bool Equipped {get{for(var p=transform.parent;p!=null;p=p.parent)if(p.name=="HandItemUse")return true;return false;}}
  void LateUpdate(){
   bool hidden=Equipped||(sentinel!=null&&!sentinel.gameObject.activeInHierarchy);
   foreach(var r in allRenderers)r.enabled=models!=null&&models.Contains(r as MeshRenderer)&&!hidden;
   bool carried=hidden;for(var p=transform.parent;p!=null;p=p.parent)if(p.name=="PlayerCamera")carried=true;
   PhysicsGrabbed=carried&&!hidden;
   if(carried){foreach(var c in colliders)c.enabled=false;if(body!=null)body.detectCollisions=false;}
   else if(wasHeld){foreach(var c in colliders)c.enabled=c.isTrigger||c==solid;if(body!=null){body.isKinematic=false;body.detectCollisions=true;body.WakeUp();}}
   if(itemFuel==null)itemFuel=GetComponentInChildren<ChainsawFuel>(true);float litres=itemFuel==null?-1f:Mathf.Round(itemFuel.Litres*100f)/100f,damage=Plugin.FlamethrowerDamageFor(gameObject),range=Plugin.FlamethrowerRangeFor(gameObject);var kind=Plugin.FlamethrowerKindFor(gameObject);
   if(displayedDamage!=damage||displayedLitres!=litres||displayedRange!=range||displayedKind!=kind){Plugin.UpdateFlamethrowerFuelLabel(gameObject);displayedDamage=damage;displayedLitres=litres;displayedRange=range;displayedKind=kind;}
   wasHeld=carried;
  }
  void OnDestroy(){if(HoldingRig!=null)Destroy(HoldingRig.gameObject);}
 }
 public partial class Plugin {
  GameObject flameTemplate;bool flameRegistered;Mesh flameMesh,flameBox;Material flameMaterial,flameBoxMaterial;FlamethrowerItem activeFlame;Transform flameCarrier;bool flameCarrierActive;ParticleSystem flameParticles;
  ConfigEntry<float> flameDamage,flameRange;Texture2D flameIcon;
  void SetupFlamethrower(){SetupFlamethrowerTuning();SetupFlameFuelBalance();SetupFlamethrowerIgnite();SetupFuelPuddles();SetupFlameEffects();}
  Material FlameMaterial(string textureName){
   var folder=Path.Combine(Path.GetDirectoryName(Info.Location),"Flamethrower");
   var image=new Texture2D(2,2);ImageConversion.LoadImage(image,File.ReadAllBytes(Path.Combine(folder,textureName+".png")));image.hideFlags=HideFlags.DontUnloadUnusedAsset;
   var material=new Material(Shader.Find("Standard")??Shader.Find("Legacy Shaders/Diffuse"));material.hideFlags=HideFlags.DontUnloadUnusedAsset;material.mainTexture=image;material.color=Color.white;material.SetFloat("_Glossiness",.08f);
   material.SetFloat("_Mode",0);material.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.One);material.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.Zero);material.SetInt("_ZWrite",1);
   material.DisableKeyword("_ALPHATEST_ON");material.DisableKeyword("_ALPHABLEND_ON");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.SetOverrideTag("RenderType","Opaque");material.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Geometry;return material;
  }
  Transform MakeFlameModel(Transform parent){
   var root=new GameObject("FlamethrowerModel").transform;root.SetParent(parent,false);
   var a=new GameObject("Flamethrower");a.transform.SetParent(root,false);a.AddComponent<MeshFilter>().sharedMesh=flameMesh;a.AddComponent<MeshRenderer>().sharedMaterial=flameMaterial;
   var b=new GameObject("StrikerBox");b.transform.SetParent(root,false);b.AddComponent<MeshFilter>().sharedMesh=flameBox;b.AddComponent<MeshRenderer>().sharedMaterial=flameBoxMaterial;return root;
  }
  void CreateFlamethrower(){
   if(flameTemplate==null){
    var source=Resources.FindObjectsOfTypeAll<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="weaponType"&&f.GetComponent<ChainsawItem>()==null&&f.GetComponent<FlamethrowerItem>()==null&&f.GetComponent<Rigidbody>()!=null&&f.FsmVariables.FindFsmString("weaponType")!=null&&f.FsmVariables.FindFsmString("weaponType").Value=="m16a1");
    if(source==null)return;
    var folder=Path.Combine(Path.GetDirectoryName(Info.Location),"Flamethrower");if(flameMesh==null){flameMesh=WeaponObj.Load(Path.Combine(folder,"Vlammewerper.obj"),true);flameBox=WeaponObj.Load(Path.Combine(folder,"Vlammewerperdoos.obj"),true);flameMaterial=FlameMaterial("metal_rusted_19");flameBoxMaterial=FlameMaterial("metal_rusted_16");flameIcon=new Texture2D(2,2);ImageConversion.LoadImage(flameIcon,File.ReadAllBytes(Path.Combine(folder,"icon.png")));flameIcon.hideFlags=HideFlags.DontUnloadUnusedAsset;}
    var stage=new GameObject("Flamethrower.Templates");stage.SetActive(false);DontDestroyOnLoad(stage);flameTemplate=Instantiate(source.gameObject,stage.transform,false);flameTemplate.name="Flamethrower";flameTemplate.AddComponent<FlamethrowerItem>();
    foreach(var f in flameTemplate.GetComponents<PlayMakerFSM>()){if(f.FsmName=="saveItemVar"){DestroyImmediate(f);continue;}if(f.FsmName=="ItemName"){foreach(var v in f.FsmVariables.StringVariables)if(v.Value!=null&&v.Value.IndexOf("m16",StringComparison.OrdinalIgnoreCase)>=0)v.Value="Flamethrower";foreach(var s in f.Fsm.States)foreach(var action in s.Actions)foreach(var field in action.GetType().GetFields()){var v=field.GetValue(action) as FsmString;if(v!=null&&v.Value!=null&&v.Value.IndexOf("m16",StringComparison.OrdinalIgnoreCase)>=0)v.Value="Flamethrower";}}
     foreach(var icon in f.FsmVariables.TextureVariables)if(icon.Value!=null)icon.Value=flameIcon;
    }
    foreach(var r in flameTemplate.GetComponentsInChildren<Renderer>(true))r.enabled=false;
    var model=MakeFlameModel(flameTemplate.transform);var scale=flameTemplate.transform.lossyScale;model.localScale=new Vector3(1/Mathf.Max(.0001f,Mathf.Abs(scale.x)),1/Mathf.Max(.0001f,Mathf.Abs(scale.y)),1/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
    foreach(var collider in flameTemplate.GetComponentsInChildren<Collider>(true))if(!collider.isTrigger)collider.enabled=false;
    var solid=flameTemplate.AddComponent<BoxCollider>();var bounds=flameMesh.bounds;bounds.Encapsulate(flameBox.bounds);solid.center=Vector3.Scale(bounds.center,model.localScale);solid.size=Vector3.Scale(bounds.size,model.localScale);
    if(!AddFuelTank(flameTemplate)){Destroy(stage);flameTemplate=null;return;}PrepareFlamethrowerCodex(flameTemplate);ResetOwnedWeaponSale(flameTemplate);flameTemplate.SetActive(true);
   }
   if(flameRegistered)return;var type=OptionalWeaponTypes.Find("ApocalypterInspector.ItemSpawner");if(type==null)return;
   var known=AccessTools.Field(type,"known").GetValue(null) as IDictionary;if(known==null)return;var row=Activator.CreateInstance(type.GetNestedType("ItemRow",BindingFlags.Public|BindingFlags.NonPublic),true);
   foreach(var pair in new Dictionary<string,object>{{"Name","Flamethrower"},{"Label","Flamethrower"},{"Category","Ranged Weapons"},{"Source",flameTemplate},{"IsAsset",true}})AccessTools.Field(row.GetType(),pair.Key).SetValue(row,pair.Value);known["flamethrower"]=row;AccessTools.Method(type,"Finish").Invoke(null,null);flameRegistered=true;Logger.LogInfo("Inspector item registered: Flamethrower");
  }
  SkinnedMeshRenderer flameBodyArms;bool flameBodyArmsEnabled;
  void UpdateFlameBody(object body){var skin=AccessTools.Field(body.GetType(),"_fpArms").GetValue(body) as SkinnedMeshRenderer;if(skin==null)return;if(flameBodyArms!=skin){flameBodyArms=skin;flameBodyArmsEnabled=skin.enabled;}skin.enabled=false;}
  Renderer[] flameCarrierRenderers;bool[] flameCarrierRendererFlags;PlayMakerFSM[] flameCarrierScripts;bool[] flameCarrierScriptFlags;
  GameObject flamePlayer;PlayMakerFSM flameHeldFsm;
  void SuppressFlameCarrier(){
   if(flameCarrier==null)return;
   if(flameCarrierRenderers==null){flameCarrierRenderers=flameCarrier.GetComponentsInChildren<Renderer>(true);flameCarrierRendererFlags=flameCarrierRenderers.Select(r=>r.enabled).ToArray();flameCarrierScripts=flameCarrier.GetComponentsInChildren<PlayMakerFSM>(true);flameCarrierScriptFlags=flameCarrierScripts.Select(s=>s.enabled).ToArray();}
   foreach(var renderer in flameCarrierRenderers)if(renderer!=null)renderer.enabled=false;
   foreach(var script in flameCarrierScripts)if(script!=null)script.enabled=false;
  }
  void StopFlamethrower(){
   StopFlameFiring();
   if(activeFlame!=null&&activeFlame.HoldingRig!=null)activeFlame.HoldingRig.gameObject.SetActive(false);
   ResetFlamethrowerHolding();
   if(flameParticles!=null)flameParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   if(flameBodyArms!=null)flameBodyArms.enabled=flameBodyArmsEnabled;flameBodyArms=null;
   if(flameCarrierRenderers!=null)for(int i=0;i<flameCarrierRenderers.Length;i++)if(flameCarrierRenderers[i]!=null)flameCarrierRenderers[i].enabled=flameCarrierRendererFlags[i];
   if(flameCarrierScripts!=null)for(int i=0;i<flameCarrierScripts.Length;i++)if(flameCarrierScripts[i]!=null)flameCarrierScripts[i].enabled=flameCarrierScriptFlags[i];
   flameCarrierRenderers=null;flameCarrierScripts=null;activeFlame=null;flameCarrier=null;
  }
  void TickFlamethrower(){
   if(enabledPreview){if(activeFlame!=null)StopFlamethrower();return;}
   if(flamePlayer==null){flamePlayer=GameObject.Find("PlayerCamera");flameHeldFsm=flamePlayer==null?null:flamePlayer.GetComponentsInChildren<PlayMakerFSM>(true).FirstOrDefault(f=>f.FsmName=="WeaponInHand");}
   if(flamePlayer!=null&&!CanPresentHeldWeapon(flamePlayer.transform)){if(activeFlame!=null)StopFlamethrower();return;}
   var selected=flameHeldFsm==null?null:flameHeldFsm.FsmVariables.FindFsmGameObject("Item")??flameHeldFsm.FsmVariables.FindFsmGameObject("item");
   var held=selected==null||selected.Value==null?null:selected.Value.GetComponent<FlamethrowerItem>();
   if(held!=null&&(!held.Equipped||!held.gameObject.activeInHierarchy))held=null;
   if(held!=activeFlame){StopFlamethrower();activeFlame=held;}
   if(activeFlame==null)return;
   var player=flamePlayer;if(player==null)return;
   if(flameCarrier==null){flameCarrier=player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="m16a1"&&t.parent!=null&&t.parent.name=="Parent");if(flameCarrier!=null)flameCarrierActive=flameCarrier.gameObject.activeSelf;}
   if(activeFlame.HoldingRig==null){
    var fists=flameCarrier;if(fists==null)return;
    var stage=new GameObject("Flamethrower.RigStaging");stage.SetActive(false);var clone=Instantiate(fists.gameObject,stage.transform,false);clone.name="Flamethrower.HeldRig";
    foreach(var t in clone.GetComponentsInChildren<Transform>(true))t.gameObject.tag="Untagged";foreach(var script in clone.GetComponentsInChildren<MonoBehaviour>(true))if(script.GetType().Name!="FastIKFabric")DestroyImmediate(script);else script.enabled=false;
    foreach(var animator in clone.GetComponentsInChildren<Animator>(true)){animator.enabled=false;if(animator.runtimeAnimatorController!=null){var idle=animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name.ToLowerInvariant().Contains("idle"));if(idle!=null)idle.SampleAnimation(animator.gameObject,0);}}
    foreach(var animation in clone.GetComponentsInChildren<Animation>(true))animation.enabled=false;
    foreach(var tween in clone.GetComponentsInChildren<Component>(true))if(tween!=null&&tween.GetType().Name=="iTween")DestroyImmediate(tween);
    foreach(var original in clone.GetComponentsInChildren<MeshRenderer>(true)){var filter=original.GetComponent<MeshFilter>();DestroyImmediate(original);if(filter!=null)DestroyImmediate(filter);}

    foreach(var collider in clone.GetComponentsInChildren<Collider>(true))DestroyImmediate(collider);
    clone.transform.SetParent(fists.parent,false);clone.transform.SetPositionAndRotation(fists.position,fists.rotation);clone.transform.localScale=fists.localScale;activeFlame.HoldingRig=clone.transform;
    var sourceIK=fists.GetComponentsInChildren<MonoBehaviour>(true).Where(s=>s.GetType().Name=="FastIKFabric").ToArray();var privateIK=clone.GetComponentsInChildren<MonoBehaviour>(true).Where(s=>s.GetType().Name=="FastIKFabric").ToArray();
    for(int i=0;i<privateIK.Length;i++){var type=privateIK[i].GetType();AccessTools.Method(type,"Init").Invoke(privateIK[i],null);if(i<sourceIK.Length)foreach(var name in new[]{"StartRotationBone","StartRotationTarget","StartDirectionSucc"}){var field=AccessTools.Field(type,name);if(field==null)continue;var value=field.GetValue(sourceIK[i]);if(value!=null)field.SetValue(privateIK[i],value is Array?((Array)value).Clone():value);}AccessTools.Method(type,"LateUpdate").Invoke(privateIK[i],null);}
    clone.transform.SetParent(player.transform,true);
    activeFlame.HoldingModel=MakeFlameModel(clone.transform);activeFlame.HoldingModel.SetPositionAndRotation(player.transform.TransformPoint(new Vector3(.12f,-.18f,.5f)),player.transform.rotation);FlameWorldScale(activeFlame.HoldingModel,Vector3.one);
    clone.AddComponent<FlamethrowerHoldingRunner>().Configure(this,activeFlame,player.transform);Destroy(stage);
   }
   activeFlame.HoldingRig.gameObject.SetActive(true);foreach(var arm in activeFlame.HoldingRig.GetComponentsInChildren<SkinnedMeshRenderer>(true))arm.enabled=!ActualThirdPersonActive();SuppressFlameCarrier();
   TickFlameFiring();
  }
  public static bool TryGetCustomHoldingRig(GameObject item,out Transform rig,out Transform model){var marker=item==null?null:item.GetComponent<FlamethrowerItem>();rig=marker==null?null:marker.HoldingRig;model=marker==null?null:marker.HoldingModel;return rig!=null&&model!=null;}
 }
}
