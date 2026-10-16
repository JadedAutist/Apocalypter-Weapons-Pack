using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChainsawPrototype {
 public sealed class ChainsawItem : MonoBehaviour {
  internal static readonly List<ChainsawItem> Active=new List<ChainsawItem>();
  void OnEnable(){if(!Active.Contains(this))Active.Add(this);}
  void OnDisable(){Active.Remove(this);}
  internal bool PhysicsGrabbed {get;private set;}
  internal bool SpawnFuelHandled;
  Renderer[] renderers;Collider[] colliders;ChainsawWorldCollision collision;Rigidbody body;bool wasCarried;float displayedDamage=float.NaN,displayedLitres=float.NaN;ChainsawFuel itemFuel;Transform visual;GameObject nativeVisual;PlayMakerFSM visibility;
  void Start(){renderers=GetComponentsInChildren<Renderer>(true);colliders=GetComponentsInChildren<Collider>(true);collision=GetComponent<ChainsawWorldCollision>();body=GetComponent<Rigidbody>();visual=transform.Find("ChainsawModel");visibility=GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="Visibility");
   if(visibility!=null)foreach(var state in visibility.Fsm.States)foreach(var action in state.Actions){
    if(action.GetType().Name!="ActivateGameObjects")continue;
    var field=action.GetType().GetField("gameObjects");var objects=field==null?null:field.GetValue(action) as FsmGameObject[];
    if(objects==null)continue;
    foreach(var entry in objects){var go=entry.Value;if(go!=null && go!=gameObject && go.transform.IsChildOf(transform) && go.GetComponentsInChildren<Renderer>(true).Any(r=>r.gameObject.name!="ChainsawModel")){nativeVisual=go;break;}}
    if(nativeVisual!=null)break;
   }
   if(nativeVisual==null){var native=renderers.FirstOrDefault(r=>r.gameObject!=gameObject && r.gameObject.name!="ChainsawModel");if(native!=null)nativeVisual=native.gameObject;}
   CustomWeaponInteraction.Ensure(gameObject);Plugin.InitializeSpawnFuel(gameObject);
  }
  void LateUpdate(){
   // Native Visibility controls inventory storage independently of camera parenting.
   bool equipped=false,carried=false;for(var p=transform.parent;p!=null;p=p.parent){if(p.name=="HandItemUse")equipped=true;if(p.name=="HandItemUse" || p.name=="PlayerCamera")carried=true;}
   bool hidden=equipped || (carried && nativeVisual!=null && !nativeVisual.activeInHierarchy);PhysicsGrabbed=carried&&!hidden;carried|=hidden;
   if(body==null)body=GetComponent<Rigidbody>();
   if(renderers!=null)foreach(var r in renderers)if(r!=null)r.enabled=r.gameObject.name=="ChainsawModel" && !hidden;
   if(visual!=null && hidden)visual.gameObject.SetActive(false);
   if(carried){if(colliders!=null)foreach(var c in colliders)if(c!=null)c.enabled=false;if(body!=null){body.detectCollisions=false;}}
   else if(wasCarried){if(colliders!=null)foreach(var c in colliders)if(c!=null)c.enabled=c.isTrigger;if(body!=null){body.isKinematic=false;body.detectCollisions=true;body.WakeUp();}}
   if(visual!=null && !hidden){visual.gameObject.SetActive(true);visual.gameObject.layer=gameObject.layer;
    visual.localPosition=Vector3.zero;
    var scale=transform.lossyScale;visual.localScale=new Vector3(1f/Mathf.Max(Mathf.Abs(scale.x),.00001f),1f/Mathf.Max(Mathf.Abs(scale.y),.00001f),1f/Mathf.Max(Mathf.Abs(scale.z),.00001f));}
   if(collision!=null)collision.ApplyWorldState(carried);
   if(itemFuel==null)itemFuel=GetComponentInChildren<ChainsawFuel>(true);float litres=itemFuel==null?-1f:Mathf.Round(itemFuel.Litres*100f)/100f;float damage=Plugin.ConfiguredDamage;if(displayedDamage!=damage || displayedLitres!=litres){Plugin.UpdateDamageLabel(gameObject);displayedDamage=damage;displayedLitres=litres;}wasCarried=carried;
  }
 }
 [DefaultExecutionOrder(32000)] public sealed class PreviewRunner : MonoBehaviour { internal Plugin owner;float nextError;void LateUpdate(){if(ReferenceEquals(owner,null))return;try{owner.LatePose();owner.UpdateFuelHud();owner.UpdateGrabTooltip();}catch(Exception e){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10f;owner.ReportError(e);}}}void Update(){if(ReferenceEquals(owner,null))return;try{owner.Tick();}catch(Exception e){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10f;owner.ReportError(e);}}} }
 public partial class Plugin {
  internal void ReportError(Exception e){Logger.LogError("Chainsaw runtime failure: "+e);}
  bool templateReady;
  GameObject template;Mesh worldMesh;bool registered;PreviewRunner runner;
  void EnsureRunner(){if(runner!=null)return;var go=new GameObject("ChainsawPrototype.Runtime");UnityEngine.Object.DontDestroyOnLoad(go);runner=go.AddComponent<PreviewRunner>();runner.owner=this;}
  internal void Tick(){
   TickWeaponMaintenance();TickFlameSiteDiscovery();
   TickFlameIgnition();var held=FindHeldChainsaw();BindFuel(held);
   if(held!=null && !enabledPreview){FindWeapon();if(weapon!=null && left!=null && right!=null)StartPreview();}
   if(enabledPreview && (held==null || weapon==null || camera==null || left==null || right==null))StopPreview();
   if(enabledPreview)SuppressCarrier();UpdateOwnedAttack();UpdateSawPresentation();UpdateCrosshair();if(enabledPreview)FindStandardMeleeEffect();UpdateFuelHud();TickFlamethrower();UpdateFlamethrowerCrosshair();TickEmptyTankFeedback();
  }
  public static event Action HoldingPoseCompleted;
  float nextPoseTiming;
  internal void LatePose(){if(!enabledPreview || weapon==null || camera==null)return;long poseStarted=System.Diagnostics.Stopwatch.GetTimestamp();playerThirdPerson=ActualThirdPersonActive();preview.SetActive(true);preview.GetComponent<MeshRenderer>().enabled=!playerThirdPerson;weapon.gameObject.SetActive(true);foreach(var arm in HeldArmRenderers())arm.enabled=!playerThirdPerson;UpdateHeldMotion();solvedFinalPose=false;if(!gameAttacking)ApplyTargets();LateOwnedAttack();ApplyCuttingPose();var handler=FinalCameraPose;if(handler!=null)handler();if(!solvedFinalPose)SolveHeldIK();UpdateFuelHud();CompleteHoldingPose();double poseMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-poseStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;if(poseMilliseconds>8&&Time.unscaledTime>=nextPoseTiming){nextPoseTiming=Time.unscaledTime+5;Logger.LogInfo("Slow chainsaw pose update: "+poseMilliseconds.ToString("0.0")+" ms; attacking="+gameAttacking);}}
  void CreateTemplate(){
   if(template!=null && !templateReady){UnityEngine.Object.Destroy(template.transform.parent.gameObject);template=null;}
   if(template==null && GameObject.Find("Player")!=null){
    foreach(var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>()){
     // Read a stock donor, then modify only our private clone below.
     if(fsm.FsmName!="weaponType" || fsm.GetComponent<ChainsawItem>()!=null || fsm.GetComponent<FlamethrowerItem>()!=null)continue;
     var type=fsm.FsmVariables.FindFsmString("weaponType");
     if(type==null || !string.Equals(type.Value,"m16a1",StringComparison.OrdinalIgnoreCase) || fsm.GetComponent<Rigidbody>()==null || fsm.GetComponent<Collider>()==null)continue;
     bool inHand=false;for(var p=fsm.transform.parent;p!=null;p=p.parent)if(p.name=="HandItemUse")inHand=true;if(inHand)continue;
     var staging=new GameObject("ChainsawPrototype.Templates");staging.SetActive(false);UnityEngine.Object.DontDestroyOnLoad(staging);
     template=UnityEngine.Object.Instantiate(fsm.gameObject,staging.transform,false);template.name="Chainsaw";template.AddComponent<ChainsawItem>();foreach(var inheritedSave in template.GetComponents<PlayMakerFSM>())if(inheritedSave.FsmName=="saveItemVar")DestroyImmediate(inheritedSave);
     foreach(var label in template.GetComponents<PlayMakerFSM>())if(label.FsmName=="ItemName"){
      foreach(var v in label.FsmVariables.StringVariables)if(!string.IsNullOrEmpty(v.Value) && v.Value.IndexOf("m16",StringComparison.OrdinalIgnoreCase)>=0)v.Value="Chainsaw";
      foreach(var state in label.Fsm.States)foreach(var action in state.Actions)foreach(var field in action.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance)){
       var v=field.GetValue(action) as FsmString;if(v!=null && !string.IsNullOrEmpty(v.Value) && v.Value.IndexOf("m16",StringComparison.OrdinalIgnoreCase)>=0)v.Value="Chainsaw";
      }
     }
     foreach(var r in template.GetComponentsInChildren<Renderer>(true))r.enabled=false;
     if(worldMesh==null){worldMesh=UnityEngine.Object.Instantiate(heldMesh);worldMesh.hideFlags=HideFlags.DontUnloadUnusedAsset;var vertices=worldMesh.vertices;var center=worldMesh.bounds.center;for(int i=0;i<vertices.Length;i++)vertices[i]-=center;worldMesh.vertices=vertices;worldMesh.RecalculateBounds();}
     var visual=new GameObject("ChainsawModel");visual.transform.SetParent(template.transform,false);var inherited=template.transform.lossyScale;visual.transform.localScale=new Vector3(1f/Mathf.Max(Mathf.Abs(inherited.x),.00001f),1f/Mathf.Max(Mathf.Abs(inherited.y),.00001f),1f/Mathf.Max(Mathf.Abs(inherited.z),.00001f));visual.AddComponent<MeshFilter>().sharedMesh=worldMesh;visual.AddComponent<MeshRenderer>().sharedMaterial=chainsawMaterial;
     // Compound shapes follow the blade, body and handles while retaining root pickup colliders.
     foreach(var c in template.GetComponentsInChildren<Collider>(true))if(!c.isTrigger)c.enabled=false;
     template.AddComponent<ChainsawWorldCollision>().Configure(visual.transform);
     foreach(var f in template.GetComponents<PlayMakerFSM>()){var damage=f.FsmVariables.FindFsmFloat("Damage");if(damage!=null)damage.Value=-sawDamage.Value;}
     if(!AddFuelTank(template)){Logger.LogWarning("Waiting for native gasoline can before publishing chainsaw.");return;}UpdateDamageLabel(template);SetItemIcon(template);PrepareChainsawCodex(template);PrepareChainsawPersistence(template);ResetOwnedWeaponSale(template);template.SetActive(true);templateReady=true;Logger.LogInfo("Chainsaw template created with native pickup/equip and saved Blender idle.");break;
    }
   }
   if(template==null || !templateReady || registered)return;
   Type inspector=AccessTools.TypeByName("ApocalypterInspector.ItemSpawner");if(inspector==null)return;
   harmony.Patch(AccessTools.Method(inspector,"Finish"),prefix:new HarmonyMethod(typeof(Plugin),nameof(RegisterChainsaw)));
   RegisterChainsaw();AccessTools.Method(inspector,"Finish").Invoke(null,null);registered=true;Logger.LogInfo("Inspector item registered: Chainsaw");
  }
  internal static float ConfiguredDamage {get{return ReferenceEquals(instance,null)?50f:instance.sawDamage.Value;}}
  internal static void UpdateDamageLabel(GameObject item){
   if(ReferenceEquals(instance,null))return;
   string text="Damage - "+instance.sawDamage.Value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
   var fuel=item.GetComponentInChildren<ChainsawFuel>(true);if(fuel!=null)text+="\n"+fuel.Litres.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" / 5 L";
   foreach(var fsm in item.GetComponents<PlayMakerFSM>()){
    if(fsm.FsmName!="ItemName")continue;
    foreach(var v in fsm.FsmVariables.StringVariables)if(v.Value!=null && v.Value.TrimStart().StartsWith("Damage -",StringComparison.OrdinalIgnoreCase))v.Value=text;
    foreach(var state in fsm.Fsm.States)foreach(var action in state.Actions)foreach(var field in action.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance)){
     var v=field.GetValue(action) as FsmString;if(v!=null && v.Value!=null && v.Value.TrimStart().StartsWith("Damage -",StringComparison.OrdinalIgnoreCase))v.Value=text;
    }
   }
  }
  static void RegisterChainsaw(){
   if(ReferenceEquals(instance,null)||instance.template==null)return;
   Type type=AccessTools.TypeByName("ApocalypterInspector.ItemSpawner");var known=AccessTools.Field(type,"known").GetValue(null) as IDictionary;if(known==null)return;
   var row=known.Contains("chainsaw")?known["chainsaw"]:Activator.CreateInstance(type.GetNestedType("ItemRow",BindingFlags.Public|BindingFlags.NonPublic),true);
   foreach(var pair in new Dictionary<string,object>{{"Name","Chainsaw"},{"Label","Chainsaw"},{"Category","Melee Weapons"},{"Source",instance.template},{"IsAsset",true}})AccessTools.Field(row.GetType(),pair.Key).SetValue(row,pair.Value);
   known["chainsaw"]=row;
  }
 }
}
