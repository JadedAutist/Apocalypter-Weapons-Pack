using BepInEx.Configuration;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  ConfigEntry<float>[] modelMove,modelTurn,leftMove,leftTurn,rightMove,rightTurn;
  ConfigEntry<float> poseScale;
  ConfigEntry<float>[] BindAxes(string section,string description,float range){var result=new ConfigEntry<float>[3];string[] axes={"X","Y","Z"};for(int i=0;i<3;i++)result[i]=placementConfig.Bind(section,axes[i],0f,new ConfigDescription(description,new AcceptableValueRange<float>(-range,range)));return result;}
  void SetupPoseControls(){
   swayStrength=placementConfig.Bind("Holding motion","Sway strength",1f,new ConfigDescription("Follow native weapon movement sway while keeping both hands on their grips.",new AcceptableValueRange<float>(0f,1f)));
   modelMove=BindAxes("Holding - whole pose position","Move model and both hands together in camera space. Metres: X right, Y up, Z forward.",.4f);
   modelTurn=BindAxes("Holding - whole pose rotation","Rotate model and both hands together. Degrees: X pitch, Y yaw, Z roll.",90f);
   poseScale=placementConfig.Bind("Holding - size","Scale",1f,new ConfigDescription("Scale model and spacing between hands together.",new AcceptableValueRange<float>(.5f,1.5f)));
   leftMove=BindAxes("Grip - left hand position","Fine tune left hand independently, in metres along camera axes.",.3f);
   leftTurn=BindAxes("Grip - left hand rotation","Fine tune left wrist independently in degrees.",180f);
   rightMove=BindAxes("Grip - right hand position","Fine tune right hand independently, in metres along camera axes.",.3f);
   rightTurn=BindAxes("Grip - right hand rotation","Fine tune right wrist independently in degrees.",180f);
  }
  Vector3 Values(ConfigEntry<float>[] axes){return new Vector3(PreviewValue(axes[0]),PreviewValue(axes[1]),PreviewValue(axes[2]));}
  void ApplyEditablePose(){ApplyM16Holding();}
 }
}
