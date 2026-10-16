using UnityEngine;namespace ChainsawPrototype {public partial class Plugin {Vector3 attackPivotCamera;
  static float Ease(float t,string easing){t=Mathf.Clamp01(t);if(easing=="Linear")return t;if(easing=="Ease in")return t*t;if(easing=="Ease out")return 1-(1-t)*(1-t);return t*t*(3-2*t);}
  static Vector3 RotationLerp(Vector3 a,Vector3 b,float t){return Quaternion.Slerp(Quaternion.Euler(a),Quaternion.Euler(b),t).eulerAngles;}
  static PoseKey Mix(PoseKey a,PoseKey b,float t){return new PoseKey{move=Vector3.Lerp(a.move,b.move,t),turn=RotationLerp(a.turn,b.turn,t),weaponTurn=RotationLerp(a.weaponTurn,b.weaponTurn,t),leftMove=Vector3.Lerp(a.leftMove,b.leftMove,t),leftTurn=RotationLerp(a.leftTurn,b.leftTurn,t),rightMove=Vector3.Lerp(a.rightMove,b.rightMove,t),rightTurn=RotationLerp(a.rightTurn,b.rightTurn,t)};}
  void ApplyAttackPose(Transform m,Transform l,Transform r,Transform camera){var p=forcedPose;var poseSettings=attackPreset;Quaternion q=camera.rotation*(Quaternion.Euler(poseSettings.swingTurn)*Quaternion.Euler(p.turn))*Quaternion.Inverse(camera.rotation);Vector3 pivot=camera.TransformPoint(attackPivotCamera);Vector3 move=camera.TransformVector(p.move+TravelOffset(poseSettings));Vector3 lp=l!=null?l.position:Vector3.zero,rp=r!=null?r.position:Vector3.zero;Quaternion lq=l!=null?l.rotation:Quaternion.identity,rq=r!=null?r.rotation:Quaternion.identity;m.SetPositionAndRotation(pivot+q*(m.position-pivot)+move,q*m.rotation);if(l!=null)l.SetPositionAndRotation(pivot+q*(lp-pivot)+move+camera.TransformVector(p.leftMove),q*lq*Quaternion.Euler(p.leftTurn));if(r!=null)r.SetPositionAndRotation(pivot+q*(rp-pivot)+move+camera.TransformVector(p.rightMove),q*rq*Quaternion.Euler(p.rightTurn));Vector3 grip=r!=null?r.position:camera.TransformPoint(attackPivotCamera);Quaternion weaponRotation=Quaternion.Euler(poseSettings.weaponAlignment)*Quaternion.Euler(p.weaponTurn);Quaternion frame=r!=null?r.rotation:camera.rotation;Quaternion only=(camera.rotation*TravelRotation(poseSettings)*Quaternion.Inverse(camera.rotation))*(frame*weaponRotation*Quaternion.Inverse(frame));m.SetPositionAndRotation(grip+only*(m.position-grip),only*m.rotation);}
  Vector3 TravelOffset(Preset data){

   float wind=Mathf.Clamp(data.keys[0].seconds,.02f,5f),cut=Mathf.Clamp(data.keys[1].seconds,.02f,5f),recover=Mathf.Clamp(data.keys[2].seconds,.02f,5f);
   float time=attackTime;
   if(time<=wind)return data.pathStart*Ease(time/wind,data.keys[0].easing);
   time-=wind;
   if(time<=cut){float t=Ease(time/cut,data.keys[1].easing);return Vector3.Lerp(data.pathStart,data.pathEnd,t)+data.pathMiddle*(4f*t*(1f-t));}
   time-=cut;
   if(time<=recover)return Vector3.Lerp(data.pathEnd,data.pathEnd*.4f,Ease(time/recover,data.keys[2].easing));
   time-=recover;return Vector3.Lerp(data.pathEnd*.4f,Vector3.zero,Ease(time/Mathf.Clamp(data.returnSeconds,.02f,5f),"Smooth"));
  }
  Quaternion TravelRotation(Preset data){

   float wind=Mathf.Clamp(data.keys[0].seconds,.02f,5f),cut=Mathf.Clamp(data.keys[1].seconds,.02f,5f),recover=Mathf.Clamp(data.keys[2].seconds,.02f,5f);
   float time=attackTime;
   Quaternion start=Quaternion.Euler(data.pathStartTurn),end=Quaternion.Euler(data.pathEndTurn);
   if(time<=wind)return Quaternion.Slerp(Quaternion.identity,start,Ease(time/wind,data.keys[0].easing));
   time-=wind;
   if(time<=cut){float t=Ease(time/cut,data.keys[1].easing);return Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(data.pathMiddleTurn),4f*t*(1f-t))*Quaternion.Slerp(start,end,t);}
   time-=cut;
   if(time<=recover)return Quaternion.Slerp(end,Quaternion.identity,.6f*Ease(time/recover,data.keys[2].easing));
   time-=recover;return Quaternion.Slerp(Quaternion.Slerp(end,Quaternion.identity,.6f),Quaternion.identity,Ease(time/Mathf.Clamp(data.returnSeconds,.02f,5f),"Smooth"));
  }

}}
