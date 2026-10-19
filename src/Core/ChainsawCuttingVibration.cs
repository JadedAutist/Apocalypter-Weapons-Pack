using System;
namespace ChainsawPrototype {
 internal struct CuttingVibrationPose {
  internal float X,Y,Z,Pitch,Yaw,Roll;
 }
 internal static class ChainsawCuttingVibration {
  internal static CuttingVibrationPose Sample(double seconds,float strength){
   if(double.IsNaN(seconds)||double.IsInfinity(seconds)||float.IsNaN(strength)||float.IsInfinity(strength)||strength<=0f)return default(CuttingVibrationPose);
   strength=Math.Min(strength,2f);
   // Integer frequencies share a continuous one-second wrap. No phase is
   // advanced by render callbacks, movement speed or attack contact cadence.
   double phase=(seconds-Math.Floor(seconds))*Math.PI*2;
   double a=Math.Sin(phase*19),b=Math.Sin(phase*23),c=Math.Cos(phase*17);
   return new CuttingVibrationPose{
    X=(float)a*.0008f*strength,Y=(float)b*.001f*strength,Z=(float)c*.0005f*strength,
    Pitch=(float)Math.Sin(phase*17)*.2f*strength,Yaw=(float)Math.Cos(phase*19)*.12f*strength,Roll=(float)b*.16f*strength
   };
  }
 }
}
