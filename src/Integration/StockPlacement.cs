using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
namespace ChainsawPrototype {
 public partial class Plugin {
  [DataContract] class StockScale {[DataMember]public float x;[DataMember]public float y;[DataMember]public float z;}
  [DataContract] class StockRotation {[DataMember]public float x;[DataMember]public float y;[DataMember]public float z;[DataMember]public float w;}
  [DataContract] class StockBone {
   [DataMember]public string side;[DataMember]public string name;[DataMember]public int index;[DataMember(EmitDefaultValue=false)]public StockScale scale;
   public Vector3 position;public Quaternion rotation;
   [DataMember(Name="position")]public StockScale SavedPosition {get{return new StockScale{x=position.x,y=position.y,z=position.z};}set{position=new Vector3(value.x,value.y,value.z);}}
   [DataMember(Name="rotation")]public StockRotation SavedRotation {get{return new StockRotation{x=rotation.x,y=rotation.y,z=rotation.z,w=rotation.w};}set{rotation=new Quaternion(value.x,value.y,value.z,value.w);}}
  }
  [DataContract] class StockPlacement {[DataMember]public StockBone[] bones;[DataMember]public StockBone[] models;}
  StockPlacement stockPlacement;Transform stockRig;KeyValuePair<Transform,StockBone>[] stockBones,stockModels;bool stockRead,stockMotionReady;Quaternion stockMotion;Vector3 stockShift;
  void ApplyStockPlacement(){
   if(!stockRead){stockRead=true;string path=Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location),"ChainsawPlacement.json");if(File.Exists(path)){using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(path))))stockPlacement=(StockPlacement)new DataContractJsonSerializer(typeof(StockPlacement)).ReadObject(stream);if(stockPlacement==null||stockPlacement.bones==null||stockPlacement.bones.Length==0)throw new InvalidDataException("Chainsaw stock placement has no bone data.");Logger.LogInfo("Chainsaw stock placement loaded by Weapons Pack: "+stockPlacement.bones.Length+" saved bones.");}}
   if(stockPlacement==null||weapon==null||preview==null)return;
   if(stockRig!=weapon||stockBones==null){var map=new Dictionary<Transform,StockBone>();var skins=weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    foreach(var pose in stockPlacement.bones){var skin=skins.FirstOrDefault(s=>{for(var t=s.transform;t!=null&&t!=weapon;t=t.parent)if(t.name.Equals("arm_"+pose.side,StringComparison.OrdinalIgnoreCase))return true;return false;});if(skin==null)continue;var bone=skin.bones.FirstOrDefault(b=>b!=null&&b.name==pose.name)??(pose.index>=0&&pose.index<skin.bones.Length?skin.bones[pose.index]:null);if(bone!=null)map[bone]=pose;}
    stockBones=map.OrderBy(p=>StockBoneDepth(p.Key)).ToArray();var models=preview.GetComponentsInChildren<MeshRenderer>(true);stockModels=(stockPlacement.models??new StockBone[0]).Select(p=>new KeyValuePair<Transform,StockBone>(models.FirstOrDefault(m=>m.name==p.name)?.transform,p)).Where(p=>p.Key!=null).ToArray();stockRig=weapon;Logger.LogInfo("Chainsaw stock placement bound: "+stockBones.Length+" bones and "+stockModels.Length+" models.");
   }
   Quaternion motion;Vector3 shift;GetChainsawPlacementMotion(out motion,out shift);stockMotion=motion;stockShift=shift;stockMotionReady=true;
   Quaternion equipTurn;Vector3 equipShift;if(!ActualThirdPersonActive()&&TryPackEquipMotion(camera,out equipTurn,out equipShift)){motion=equipTurn*motion;shift=equipTurn*shift+equipShift;}
   foreach(var pair in stockBones){var pose=pair.Value;pair.Key.SetPositionAndRotation(shift+motion*camera.TransformPoint(pose.position),motion*camera.rotation*pose.rotation);}
   foreach(var pair in stockModels){var pose=pair.Value;var node=pair.Key;node.SetPositionAndRotation(shift+motion*camera.TransformPoint(pose.position),motion*camera.rotation*pose.rotation);if(pose.scale!=null){var parent=node.parent==null?Vector3.one:node.parent.lossyScale;node.localScale=new Vector3(pose.scale.x/Mathf.Max(.0001f,parent.x),pose.scale.y/Mathf.Max(.0001f,parent.y),pose.scale.z/Mathf.Max(.0001f,parent.z));}}
  }
  static int StockBoneDepth(Transform t){int depth=0;while(t.parent!=null){depth++;t=t.parent;}return depth;}
  void CompleteHoldingPose(){ApplyStockPlacement();var placement=HoldingPoseCompleted;if(placement!=null)placement();}
 }
}
