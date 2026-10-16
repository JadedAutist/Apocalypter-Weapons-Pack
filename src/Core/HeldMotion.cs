using BepInEx.Configuration;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  ConfigEntry<float> swayStrength;Vector3 swayPosition;Quaternion swayRotation=Quaternion.identity;
  ConfigEntry<float> cameraTurnSway;Quaternion previousLook=Quaternion.identity;Vector3 lookTurn,lookShift;
  readonly WeaponHoldingMotion chainsawHoldingMotion=new WeaponHoldingMotion();Vector3 previousHeldPosition;int heldMotionFrame=-1;
  static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
  static bool Finite(Vector3 v){return Finite(v.x)&&Finite(v.y)&&Finite(v.z);}
  static bool Finite(Quaternion q){return Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w)&&Quaternion.Dot(q,q)>.00001f;}
  void ResetHeldMotion(){
   if(cameraTurnSway==null)cameraTurnSway=FixedSetting("Holding motion","Camera turn sway",1f);
   previousLook=camera==null?Quaternion.identity:camera.rotation;lookTurn=Vector3.zero;lookShift=Vector3.zero;
   previousHeldPosition=camera==null?Vector3.zero:camera.position;chainsawHoldingMotion.Reset();heldMotionFrame=-1;swayPosition=Vector3.zero;swayRotation=Quaternion.identity;
  }
  void UpdateHeldMotion(){
   if(camera==null||heldMotionFrame==Time.frameCount)return;heldMotionFrame=Time.frameCount;
   if(Time.timeScale<=0f){previousLook=camera.rotation;previousHeldPosition=camera.position;return;}
   if(!Finite(previousLook))previousLook=camera.rotation;
   var look=Quaternion.Inverse(previousLook)*camera.rotation;previousLook=camera.rotation;
   var angles=look.eulerAngles;float pitch=Mathf.DeltaAngle(0,angles.x),yaw=Mathf.DeltaAngle(0,angles.y);
   var travel=camera.position-previousHeldPosition;previousHeldPosition=camera.position;
   float turnStrength=cameraTurnSway==null?1f:cameraTurnSway.Value;
   chainsawHoldingMotion.Sample(pitch*turnStrength,yaw*turnStrength,new Vector3(travel.x,0,travel.z).magnitude,Time.deltaTime);
   swayPosition=new Vector3(chainsawHoldingMotion.X,chainsawHoldingMotion.Y,0);
   swayRotation=Quaternion.Euler(chainsawHoldingMotion.Pitch,chainsawHoldingMotion.Yaw,chainsawHoldingMotion.Roll);
   lookTurn=lookShift=Vector3.zero;
  }
  void ApplyHeldMotion(){
   if(preview==null||camera==null)return;float strength=PreviewValue(swayStrength);
   float lookStrength=cameraTurnSway==null?1f:cameraTurnSway.Value;
   if(!Finite(swayRotation))swayRotation=Quaternion.identity;if(!Finite(swayPosition))swayPosition=Vector3.zero;
   Quaternion turn=camera.rotation*(Quaternion.Slerp(Quaternion.identity,swayRotation,strength)*Quaternion.Euler(lookTurn*lookStrength*strength))*Quaternion.Inverse(camera.rotation);
   Vector3 pivot=camera.position,shift=camera.TransformVector((swayPosition+lookShift*lookStrength)*strength);
   var m=preview.transform;m.SetPositionAndRotation(pivot+turn*(m.position-pivot)+shift,turn*m.rotation);
   left.SetPositionAndRotation(pivot+turn*(left.position-pivot)+shift,turn*left.rotation);
   right.SetPositionAndRotation(pivot+turn*(right.position-pivot)+shift,turn*right.rotation);
  }
 }
}
