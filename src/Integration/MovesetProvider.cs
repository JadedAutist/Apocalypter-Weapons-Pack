using System.IO;
using System.Text;
using System.Runtime.Serialization.Json;
namespace ChainsawPrototype {
 public partial class Plugin {
  public static string[] GetActiveAttackAnimations(){
   if(ReferenceEquals(instance,null))return new string[0];var result=new string[2];
   for(int i=0;i<2;i++){
    Preset motion;string path=Path.Combine(instance.directory,"chainsaw--"+(i==1?"upperleftdiagonalchop":"downwardswing")+".json");
    try{motion=instance.LoadInstalled(path);if(motion.weapon!="chainsaw")return new string[0];}catch{return new string[0];}
    var exported=new PresetData(motion);exported.sourceClip="";exported.sourceWeapon="";exported.sourcePath="";
    using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(PresetData)).WriteObject(stream,exported);result[i]=Encoding.UTF8.GetString(stream.ToArray());}
   }return result;
  }
  public static string GetActiveIdleClip(){if(ReferenceEquals(instance,null))return "";instance.PrepareWrenchIdle();return instance.wrenchIdle==null?"":instance.wrenchIdle.name;}
  public static float[] GetSolvedAttackMotion(string json,float time){
   if(ReferenceEquals(instance,null))return null;var owner=instance;owner.FindWeapon();if(owner.preview==null||owner.weapon==null||owner.right==null)return null;
   bool equipped=owner.enabledPreview,rigActive=owner.weapon.gameObject.activeSelf;var savedPivot=owner.attackPivotCamera;
   try{
    owner.weapon.gameObject.SetActive(true);owner.CalibrateClonedIK();owner.enabledPreview=true;RefreshAnimationRig();
    var initialRotation=owner.preview.transform.rotation;var initialPosition=owner.preview.transform.position;var origin=owner.camera.InverseTransformPoint(initialPosition);
    SampleAttackPreview(json,time);
    var rotation=UnityEngine.Quaternion.Inverse(owner.camera.rotation)*owner.right.rotation;
    var grip=owner.camera.InverseTransformPoint(owner.right.position);
    return new[]{grip.x,grip.y,grip.z,rotation.x,rotation.y,rotation.z,rotation.w,0f,0f,0f};
   }finally{owner.enabledPreview=equipped;owner.attackPivotCamera=savedPivot;RefreshAnimationRig();owner.weapon.gameObject.SetActive(rigActive);}
  }
  string cachedPreviewJson;Preset cachedPreviewMotion;
  public static void SampleAttackPreview(string json,float time){
   if(ReferenceEquals(instance,null)||!instance.enabledPreview)return;var owner=instance;
   if(owner.cachedPreviewMotion==null||owner.cachedPreviewJson!=json){using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))owner.cachedPreviewMotion=((PresetData)new DataContractJsonSerializer(typeof(PresetData)).ReadObject(stream)).Value();owner.cachedPreviewJson=json;}
   Preset motion=owner.cachedPreviewMotion;
   var savedPreset=owner.attackPreset;var savedWrists=owner.wristAnchors;var savedModel=owner.attackModel;var savedRig=owner.attackRig;var savedView=owner.attackView;var savedPose=owner.forcedPose;bool savedApplying=owner.applyingAttack;float savedTime=owner.attackTime;
   try{
    owner.applyingAttack=false;owner.forcedPose=null;RefreshAnimationRig();
    owner.attackModel=owner.preview.transform;owner.attackRig=owner.weapon;owner.attackView=owner.camera;owner.CaptureWristAnchors();owner.RestoreAttackGrip();owner.attackPivotCamera=owner.camera.InverseTransformPoint(owner.right.position);
    owner.attackPreset=motion;owner.attackTime=time;owner.forcedPose=AttackSample(motion,time);owner.applyingAttack=true;owner.ApplyTargets();owner.EnforceWristAnchors();owner.CompleteHoldingPose();
   }finally{owner.attackPreset=savedPreset;owner.wristAnchors=savedWrists;owner.attackModel=savedModel;owner.attackRig=savedRig;owner.attackView=savedView;owner.forcedPose=savedPose;owner.applyingAttack=savedApplying;owner.attackTime=savedTime;}
  }
 }
}
