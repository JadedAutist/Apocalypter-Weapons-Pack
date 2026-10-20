using UnityEngine;
namespace ChainsawPrototype {
 // Presentation only: body-space keyframes share the real attack's clock but
 // never start attacks, consume fuel or publish contacts.
 internal struct ChainsawBodyPose {
  internal Vector3 Right,Angles,RightElbow,LeftElbow;
  static ChainsawBodyPose Pose(Vector3 right,Vector3 angles,Vector3 re,Vector3 le){return new ChainsawBodyPose{Right=right,Angles=angles,RightElbow=re,LeftElbow=le};}
  static readonly ChainsawBodyPose Idle=Pose(new Vector3(.14f,-.29f,.24f),Vector3.zero,new Vector3(.38f,-.31f,.01f),new Vector3(-.38f,-.25f,.08f));
  static readonly ChainsawBodyPose Carry=Pose(new Vector3(.17f,-.33f,.18f),new Vector3(7,-8,3),new Vector3(.37f,-.35f,-.02f),new Vector3(-.36f,-.3f,.03f));
  static readonly ChainsawBodyPose DownWind=Pose(new Vector3(.1f,-.03f,.2f),new Vector3(-62,-7,4),new Vector3(.35f,-.06f,.02f),new Vector3(-.34f,.02f,.08f));
  static readonly ChainsawBodyPose DownHit=Pose(new Vector3(.15f,-.32f,.18f),new Vector3(30,3,-4),new Vector3(.39f,-.3f,.01f),new Vector3(-.35f,-.28f,.12f));
  static readonly ChainsawBodyPose DiagonalWind=Pose(new Vector3(-.1f,-.06f,.18f),new Vector3(-45,-35,25),new Vector3(.3f,-.08f,.04f),new Vector3(-.39f,-.02f,.06f));
  static readonly ChainsawBodyPose DiagonalHit=Pose(new Vector3(.19f,-.29f,.16f),new Vector3(25,28,-20),new Vector3(.4f,-.28f,.02f),new Vector3(-.3f,-.29f,.12f));
  static ChainsawBodyPose Mix(ChainsawBodyPose a,ChainsawBodyPose b,float t){t=Mathf.Clamp01(t);return Pose(Vector3.Lerp(a.Right,b.Right,t),Quaternion.Slerp(Quaternion.Euler(a.Angles),Quaternion.Euler(b.Angles),t).eulerAngles,Vector3.Lerp(a.RightElbow,b.RightElbow,t),Vector3.Lerp(a.LeftElbow,b.LeftElbow,t));}
  static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
  internal static ChainsawBodyPose Sample(float time,float wind,float contact,float recovery,float returning,bool diagonal,bool attacking,float cutting,float running){
   var idle=Mix(Idle,Carry,running);
   if(attacking){
    var start=diagonal?DiagonalWind:DownWind;var hit=diagonal?DiagonalHit:DownHit;
    time=Mathf.Max(0,time);wind=Mathf.Max(.02f,wind);contact=Mathf.Max(.02f,contact);recovery=Mathf.Max(.02f,recovery);returning=Mathf.Max(.02f,returning);
    if(time<=wind)return Mix(idle,start,Smooth(time/wind));time-=wind;
    if(time<=contact){float t=Mathf.Clamp01(time/contact);return Mix(start,hit,t*t);}time-=contact;
    var settle=Mix(idle,hit,.3f);if(time<=recovery)return Mix(hit,settle,Smooth(time/recovery));time-=recovery;
    return Mix(settle,idle,Smooth(time/returning));
   }
   float weight=Smooth(cutting);idle.Right+=new Vector3(0,.015f,.06f)*weight;idle.Angles+=new Vector3(5,0,0)*weight;return idle;
  }
  // A right grip must lie inside both arm-reach spheres. The left sphere's
  // centre is shifted by the immutable handle offset. Project analytically
  // onto their intersection instead of repeatedly pushing the weapon around.
  static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
  static bool Finite(Vector3 v){return Finite(v.x)&&Finite(v.y)&&Finite(v.z);}
  internal static bool TryFit(Vector3 desired,Vector3 rs,float rr,Vector3 ls,float lr,Vector3 delta,out Vector3 fitted){
   fitted=desired;if(!Finite(desired)||!Finite(rs)||!Finite(ls)||!Finite(delta)||!Finite(rr)||!Finite(lr)||rr<=0||lr<=0)return false;var lc=ls-delta;var between=lc-rs;float distance=between.magnitude;
   if(distance>rr+lr+.00001f)return false;
   if((desired-rs).sqrMagnitude<=rr*rr&&(desired-lc).sqrMagnitude<=lr*lr)return true;
   if(distance<.00001f){fitted=rs+Vector3.ClampMagnitude(desired-rs,Mathf.Min(rr,lr));return true;}
   if(distance+Mathf.Min(rr,lr)<=Mathf.Max(rr,lr)){var centre=rr<=lr?rs:lc;fitted=centre+Vector3.ClampMagnitude(desired-centre,Mathf.Min(rr,lr));return true;}
   var pr=rs+Vector3.ClampMagnitude(desired-rs,rr);if((pr-lc).sqrMagnitude<=lr*lr){fitted=pr;return true;}
   var pl=lc+Vector3.ClampMagnitude(desired-lc,lr);if((pl-rs).sqrMagnitude<=rr*rr){fitted=pl;return true;}
   var axis=between/distance;float along=(rr*rr-lr*lr+distance*distance)/(2*distance);var centreCircle=rs+axis*along;float radius=Mathf.Sqrt(Mathf.Max(0,rr*rr-along*along));var radial=Vector3.ProjectOnPlane(desired-centreCircle,axis);
   if(radial.sqrMagnitude<.00000001f)radial=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right);
   fitted=centreCircle+radial.normalized*radius;return true;
  }
 }
}
