using System;

namespace ChainsawPrototype {
 // Camera movement drives this weapon's motion. A stock weapon's animated
 // Parent transform is not a neutral reference: it can be lowering/raising
 // during an equip, or contain another weapon's placement offsets.
 internal sealed class WeaponHoldingMotion {
  internal float X,Y,Pitch,Yaw,Roll;
  // Fixed cadence: speed changes amplitude, never the bob frequency.
  float stepPhase;
  internal void Reset(){X=Y=Pitch=Yaw=Roll=stepPhase=0f;}
  static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
  static float Clamp(float value,float low,float high){return Math.Max(low,Math.Min(high,value));}
  internal void Sample(float pitchDelta,float yawDelta,float horizontalDistance,float deltaTime){
   if(!Finite(pitchDelta)||!Finite(yawDelta)||!Finite(horizontalDistance)||!Finite(deltaTime)||deltaTime<=0f||horizontalDistance<0f||horizontalDistance>1.5f){Reset();return;}
   float dt=Math.Min(deltaTime,.1f),blend=1f-(float)Math.Exp(-14f*dt);
   float angularScale=1f/(60f*deltaTime);pitchDelta*=angularScale;yawDelta*=angularScale;
   float walking=Clamp(horizontalDistance/deltaTime/4f,0f,1f);
   stepPhase=(stepPhase+dt*7.5f)%(2f*(float)Math.PI);
   float step=(float)Math.Sin(stepPhase),bounce=(float)Math.Cos(stepPhase*2f);
   float x=Clamp(-yawDelta*.001f,-.012f,.012f)+step*.003f*walking;
   float y=Clamp(pitchDelta*.0007f,-.01f,.01f)+bounce*.004f*walking;
   float pitch=Clamp(-pitchDelta*.65f,-4f,4f),yaw=Clamp(-yawDelta*.65f,-4f,4f);
   float roll=Clamp(yawDelta*.2f,-1.5f,1.5f)+step*.35f*walking;
   X+=(x-X)*blend;Y+=(y-Y)*blend;Pitch+=(pitch-Pitch)*blend;Yaw+=(yaw-Yaw)*blend;Roll+=(roll-Roll)*blend;
  }
 }
}
