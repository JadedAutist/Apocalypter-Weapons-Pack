using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace ChainsawPrototype {
 // Only the private flamethrower rig participates. The render callback is the
 // final camera-frame correction after native camera, sway and body updates.
 [DefaultExecutionOrder(32700)] public sealed class FlamethrowerHoldingRunner : MonoBehaviour {
  internal Plugin owner;
  internal FlamethrowerItem item;
  Transform view;
  LocalPose[] original;
  float nextError;
  struct LocalPose {
   internal Transform node;internal Vector3 position,scale;internal Quaternion rotation;
   internal LocalPose(Transform transform){node=transform;position=transform.localPosition;rotation=transform.localRotation;scale=transform.localScale;}
   internal void Restore(){if(node==null)return;node.localPosition=position;node.localRotation=rotation;node.localScale=scale;}
  }
  internal void Configure(Plugin plugin,FlamethrowerItem marker,Transform camera){
   owner=plugin;item=marker;view=camera;
   original=GetComponentsInChildren<Transform>(true).Select(t=>new LocalPose(t)).ToArray();
   Camera.onPreCull+=BeforeRender;
  }
  internal void RestoreBaseline(){if(original!=null)foreach(var pose in original)pose.Restore();}
  void LateUpdate(){CompletePose();}
  void BeforeRender(Camera render){if(render!=null&&render.transform==view)CompletePose();}
  void CompletePose(){
   if(ReferenceEquals(owner,null)||item==null)return;
   try{owner.CompleteFlamethrowerHolding(item,this);}
   catch(Exception error){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;owner.ReportFlamethrowerHoldingError(error);}}
  }
  void OnDestroy(){Camera.onPreCull-=BeforeRender;}
 }

 public partial class Plugin {
  ConfigFile flamePlacementConfig;
  StockPlacement flamePlacement;
  DateTime flamePlacementStamp=DateTime.MinValue;
  float nextFlamePlacementRead;
  string flamePlacementSource;
  Transform flamePlacementRig;
  KeyValuePair<Transform,StockBone>[] flamePlacementBones,flamePlacementModels;
  Vector3 flamePlacementPivot;
  bool flamePlacementReported;
  readonly WeaponHoldingMotion flameHoldingMotion=new WeaponHoldingMotion();
  Vector3 flamePreviousViewPosition,flameSwayPosition;
  Quaternion flamePreviousViewRotation=Quaternion.identity,flameSwayRotation=Quaternion.identity;
  bool flameSwayReady;
  int flameSwayFrame=-1;

  internal void ReportFlamethrowerHoldingError(Exception error){Logger.LogError("Flamethrower holding failure: "+error);}

  void ResetFlamePlacementBinding(){flamePlacementRig=null;flamePlacementBones=null;flamePlacementModels=null;flamePlacementReported=false;}
  void ResetFlamethrowerHolding(){ResetFlamePlacementBinding();flameHoldingMotion.Reset();flameSwayReady=false;flameSwayFrame=-1;flameSwayPosition=Vector3.zero;flameSwayRotation=Quaternion.identity;ResetFlamethrowerAim();}

  void ReadFlamethrowerHolding(){
   if(Time.unscaledTime<nextFlamePlacementRead)return;
   nextFlamePlacementRead=Time.unscaledTime+.25f;
   string path=Path.Combine(Paths.ConfigPath,"HandArmPlacement","flamethrower.cfg");
   DateTime stamp=File.Exists(path)?File.GetLastWriteTimeUtc(path):DateTime.MinValue;
   if(stamp==flamePlacementStamp)return;
   flamePlacementStamp=stamp;
   flamePlacement=null;flamePlacementConfig=null;ResetFlamePlacementBinding();
   if(stamp==DateTime.MinValue)return;
   // Read the committed runtime profile. Drafts and saved authoring presets
   // stay under the adjuster's control; this reader never writes its config.
   var config=new ConfigFile(path,false);config.SaveOnConfigSet=false;
   config.Bind("Holding motion","Sway strength",1f);
   string json=config.Bind("Imported arm pose","Data","").Value;
   if(string.IsNullOrEmpty(json)){flamePlacementConfig=config;return;}
   StockPlacement loaded;
   using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))loaded=(StockPlacement)new DataContractJsonSerializer(typeof(StockPlacement)).ReadObject(stream);
   if(loaded==null||loaded.bones==null||loaded.bones.Length==0||loaded.models==null||loaded.models.Length==0)throw new InvalidDataException("The committed flamethrower placement has no saved arms or weapon models.");
   foreach(var pose in loaded.bones.Concat(loaded.models)){
    if(pose==null||!Finite(pose.position)||!Finite(pose.rotation)||Quaternion.Dot(pose.rotation,pose.rotation)<.5f)throw new InvalidDataException("The committed flamethrower placement contains an invalid bone or model pose.");
    if(pose.scale!=null&&(!Finite(new Vector3(pose.scale.x,pose.scale.y,pose.scale.z))||pose.scale.x<=0||pose.scale.y<=0||pose.scale.z<=0))throw new InvalidDataException("The committed flamethrower placement contains an invalid model scale.");
   }
   foreach(var section in new[]{"Holding - whole pose position","Holding - whole pose rotation"})foreach(var axis in new[]{"X","Y","Z"})config.Bind(section,axis,0f);
   config.Bind("Holding - size","Scale",1f);
   flamePlacementSource=config.Bind("Imported arm pose","Source","").Value;
   flamePlacementConfig=config;flamePlacement=loaded;
  }

  static SkinnedMeshRenderer PrivateFlameArm(Transform root,string side){
   return root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s=>{
    for(var node=s.transform;node!=null&&node!=root;node=node.parent)if(node.name.Equals("arm_"+side,StringComparison.OrdinalIgnoreCase))return true;
    return false;
   });
  }
  void BindFlamethrowerHolding(FlamethrowerItem marker){
   if(flamePlacementRig==marker.HoldingRig&&flamePlacementBones!=null)return;
   var bones=new Dictionary<Transform,StockBone>();
   foreach(var pose in flamePlacement.bones){
    var skin=PrivateFlameArm(marker.HoldingRig,pose.side);if(skin==null)continue;
    var bone=skin.bones.FirstOrDefault(b=>b!=null&&b.name==pose.name);
    if(bone==null&&string.IsNullOrEmpty(pose.name)&&pose.index>=0&&pose.index<skin.bones.Length)bone=skin.bones[pose.index];
    // Never bind a borrowed stock or player skeleton outside the private rig.
    if(bone!=null&&bone.IsChildOf(marker.HoldingRig))bones[bone]=pose;
   }
   var models=marker.HoldingModel.GetComponentsInChildren<MeshRenderer>(true);
   flamePlacementModels=flamePlacement.models.Select(p=>new KeyValuePair<Transform,StockBone>(models.FirstOrDefault(m=>m.name==p.name)?.transform,p)).Where(p=>p.Key!=null).ToArray();
   flamePlacementBones=bones.OrderBy(p=>StockBoneDepth(p.Key)).ToArray();
   // The independent preview scales around the combined weapon centre, so
   // include both the barrel/body and the separate striker-box bounds.
   var weaponBounds=new Bounds();bool boundsReady=false;
   foreach(var pair in flamePlacementModels){
    var saved=pair.Value;var scale=saved.scale==null?Vector3.one:new Vector3(saved.scale.x,saved.scale.y,saved.scale.z);var bounds=pair.Key.GetComponent<MeshFilter>().sharedMesh.bounds;
    for(int i=0;i<8;i++){
     var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
     var point=saved.position+saved.rotation*Vector3.Scale(corner,scale);
     if(!boundsReady){weaponBounds=new Bounds(point,Vector3.zero);boundsReady=true;}else weaponBounds.Encapsulate(point);
    }
   }
   flamePlacementPivot=boundsReady?weaponBounds.center:Vector3.zero;
   flamePlacementRig=marker.HoldingRig;
   if(flamePlacementBones.Length!=flamePlacement.bones.Length||flamePlacementModels.Length!=flamePlacement.models.Length)Logger.LogWarning("Flamethrower placement bound "+flamePlacementBones.Length+" of "+flamePlacement.bones.Length+" bones and "+flamePlacementModels.Length+" of "+flamePlacement.models.Length+" models.");
  }
  Vector3 FlameHoldingAxes(string section){return new Vector3(flamePlacementConfig.Bind(section,"X",0f).Value,flamePlacementConfig.Bind(section,"Y",0f).Value,flamePlacementConfig.Bind(section,"Z",0f).Value);}
  static void FlameWorldScale(Transform node,Vector3 scale){var parent=node.parent==null?Vector3.one:node.parent.lossyScale;node.localScale=new Vector3(scale.x/Mathf.Max(.0001f,Mathf.Abs(parent.x)),scale.y/Mathf.Max(.0001f,Mathf.Abs(parent.y)),scale.z/Mathf.Max(.0001f,Mathf.Abs(parent.z)));}

  void SampleFlamethrowerSway(Transform view){
   // LateUpdate and the player's render callbacks reuse one filtered sample.
   // They must not advance the smoothing twice in the same rendered frame.
   if(flameSwayFrame==Time.frameCount)return;
   flameSwayFrame=Time.frameCount;
   bool usable=Time.timeScale>0f&&!Cursor.visible&&Cursor.lockState==CursorLockMode.Locked&&!ActualThirdPersonActive();
   if(!flameSwayReady||!usable){
    flamePreviousViewPosition=view.position;flamePreviousViewRotation=view.rotation;
    flameHoldingMotion.Reset();flameSwayPosition=Vector3.zero;flameSwayRotation=Quaternion.identity;flameSwayReady=usable;return;
   }
   var travel=view.position-flamePreviousViewPosition;
   var look=(Quaternion.Inverse(flamePreviousViewRotation)*view.rotation).eulerAngles;
   flamePreviousViewPosition=view.position;flamePreviousViewRotation=view.rotation;
   flameHoldingMotion.Sample(Mathf.DeltaAngle(0f,look.x),Mathf.DeltaAngle(0f,look.y),new Vector3(travel.x,0f,travel.z).magnitude,Time.deltaTime);
   flameSwayPosition=new Vector3(flameHoldingMotion.X,flameHoldingMotion.Y,0f);
   flameSwayRotation=Quaternion.Euler(flameHoldingMotion.Pitch,flameHoldingMotion.Yaw,flameHoldingMotion.Roll);
  }

  internal void CompleteFlamethrowerHolding(FlamethrowerItem marker,FlamethrowerHoldingRunner controller){
   if(marker!=activeFlame||flamePlayer==null||marker.HoldingRig==null||marker.HoldingModel==null||!marker.Equipped||!marker.gameObject.activeInHierarchy)return;
   if(!CanPresentHeldWeapon(flamePlayer.transform)){StopFlamethrower();return;}
   ReadFlamethrowerHolding();controller.RestoreBaseline();
   var view=flamePlayer.transform;
   SampleFlamethrowerAim();
   SampleFlamethrowerSway(view);
   float sway=Time.timeScale<=0f?0f:flamePlacementConfig==null?1f:flamePlacementConfig.Bind("Holding motion","Sway strength",1f).Value;
   sway=Finite(sway)?Mathf.Clamp01(sway):0f;
   var motion=view.rotation*Quaternion.Slerp(Quaternion.identity,flameSwayRotation,sway)*Quaternion.Inverse(view.rotation);var shift=view.TransformVector(flameSwayPosition*sway);
   Func<Vector3,Vector3> move=point=>view.position+motion*(point-view.position)+shift;
   // Move the renderer frames and the entire private skeleton together before
   // setting its saved camera-space bones, preserving their grip relationship.
   marker.HoldingRig.SetPositionAndRotation(move(marker.HoldingRig.position),motion*marker.HoldingRig.rotation);
   if(flamePlacement!=null){
    BindFlamethrowerHolding(marker);
    var offset=FlameHoldingAxes("Holding - whole pose position");var turn=Quaternion.Euler(FlameHoldingAxes("Holding - whole pose rotation"));float size=flamePlacementConfig.Bind("Holding - size","Scale",1f).Value;
    if(!Finite(offset)||!Finite(turn)||!Finite(new Vector3(size,size,size))||size<=0)throw new InvalidDataException("The committed flamethrower holding controls are invalid.");
    Func<StockBone,Vector3> point=pose=>move(view.TransformPoint(offset+turn*(flamePlacementPivot+(pose.position-flamePlacementPivot)*size)));
    Func<StockBone,Quaternion> rotation=pose=>motion*view.rotation*turn*pose.rotation;
    // Keep the common model frame aligned with its rendered leaves so the
    // muzzle and flame stream use the exact same transform as the barrel.
    if(flamePlacementModels.Length>0){var pose=flamePlacementModels[0].Value;marker.HoldingModel.SetPositionAndRotation(point(pose),rotation(pose));FlameWorldScale(marker.HoldingModel,(pose.scale==null?Vector3.one:new Vector3(pose.scale.x,pose.scale.y,pose.scale.z))*size);}
    foreach(var pair in flamePlacementBones)if(pair.Key!=null)pair.Key.SetPositionAndRotation(point(pair.Value),rotation(pair.Value));
    foreach(var pair in flamePlacementModels){var pose=pair.Value;var node=pair.Key;if(node==null)continue;node.SetPositionAndRotation(point(pose),rotation(pose));if(pose.scale!=null)FlameWorldScale(node,new Vector3(pose.scale.x,pose.scale.y,pose.scale.z)*size);}
    if(!flamePlacementReported){flamePlacementReported=true;Logger.LogInfo("Weapons Pack applied committed flamethrower holding: "+flamePlacementSource+"; "+flamePlacementBones.Length+" private bones; "+flamePlacementModels.Length+" models.");}
   }
   ApplyFlamethrowerAim(marker,view,motion,shift);
   ApplyFlamethrowerRecoil(marker,view);
   bool visible=!ActualThirdPersonActive();
   foreach(var arm in marker.HoldingRig.GetComponentsInChildren<SkinnedMeshRenderer>(true)){arm.enabled=visible;arm.updateWhenOffscreen=true;arm.localBounds=new Bounds(Vector3.zero,Vector3.one*6);}
   foreach(var model in marker.HoldingModel.GetComponentsInChildren<MeshRenderer>(true))model.enabled=visible;
   if(flameBodyArms!=null)flameBodyArms.enabled=false;
   SuppressFlameCarrier();
   LateFlameEffects();
  }
 }
}
