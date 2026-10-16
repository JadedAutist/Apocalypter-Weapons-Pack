using BepInEx.Configuration;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  ConfigEntry<float> swayStrength;Transform swayParent;Vector3 swayBasePosition,swayPosition;Quaternion swayBaseRotation=Quaternion.identity,swayRotation=Quaternion.identity;
  ConfigEntry<float> cameraTurnSway;Quaternion previousLook=Quaternion.identity;Vector3 lookTurn,lookShift;
  static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
  static bool Finite(Vector3 v){return Finite(v.x)&&Finite(v.y)&&Finite(v.z);}
  static bool Finite(Quaternion q){return Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w)&&Quaternion.Dot(q,q)>.00001f;}
  void ResetHeldMotion(){
   if(cameraTurnSway==null)cameraTurnSway=FixedSetting("Holding motion","Camera turn sway",1f);
   previousLook=camera==null?Quaternion.identity:camera.rotation;lookTurn=Vector3.zero;lookShift=Vector3.zero;
   swayParent=nativeRigSource==null?null:nativeRigSource.parent;swayPosition=Vector3.zero;swayRotation=Quaternion.identity;
   if(swayParent!=null&&camera!=null){swayBasePosition=camera.InverseTransformPoint(swayParent.position);swayBaseRotation=Quaternion.Inverse(camera.rotation)*swayParent.rotation;}
  }
  void UpdateHeldMotion(){
   if(camera==null||Time.timeScale<=0f)return;
   float dt=Mathf.Min(Time.deltaTime,.1f),blend=1f-Mathf.Exp(-14f*dt);
   if(!Finite(previousLook))previousLook=camera.rotation;
   var look=Quaternion.Inverse(previousLook)*camera.rotation;previousLook=camera.rotation;
   var angles=look.eulerAngles;float pitch=Mathf.DeltaAngle(0,angles.x),yaw=Mathf.DeltaAngle(0,angles.y);
   var desired=new Vector3(Mathf.Clamp(-pitch*.65f,-4,4),Mathf.Clamp(-yaw*.65f,-4,4),Mathf.Clamp(yaw*.2f,-1.5f,1.5f));
   if(Finite(desired)){lookTurn=Vector3.Lerp(lookTurn,desired,blend);lookShift=Vector3.Lerp(lookShift,new Vector3(Mathf.Clamp(-yaw*.001f,-.012f,.012f),Mathf.Clamp(pitch*.0007f,-.01f,.01f),0),blend);}
   if(swayParent==null)return;
   var rawShift=camera.InverseTransformPoint(swayParent.position)-swayBasePosition;var rotation=(Quaternion.Inverse(camera.rotation)*swayParent.rotation)*Quaternion.Inverse(swayBaseRotation);
   if(!Finite(rawShift)||!Finite(rotation)){swayPosition=Vector3.zero;swayRotation=Quaternion.identity;return;}
   float angle;Vector3 axis;rotation.ToAngleAxis(out angle,out axis);if(angle>180f)angle-=360f;
   rotation=Finite(axis)&&Finite(angle)&&axis.sqrMagnitude>.000001f&&Mathf.Abs(angle)>.00001f?Quaternion.AngleAxis(Mathf.Clamp(angle,-10f,10f),axis):Quaternion.identity;
   if(!Finite(swayPosition)||!Finite(swayRotation)){swayPosition=Vector3.zero;swayRotation=Quaternion.identity;}
   swayPosition=Vector3.Lerp(swayPosition,Vector3.ClampMagnitude(rawShift,.06f),blend);swayRotation=Quaternion.Slerp(swayRotation,rotation,blend);
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
