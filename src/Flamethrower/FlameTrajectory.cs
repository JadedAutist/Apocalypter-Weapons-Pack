using UnityEngine;
namespace ChainsawPrototype {
 internal static class FlameTrajectory {
  internal const float Speed=11f,DropPerMetreSquared=.007f;
  internal const float NozzleRadius=.055f,RadiusSlope=.109f,Step=.25f;
  internal const float DownwardAcceleration=2f*DropPerMetreSquared*Speed*Speed;
  internal static Vector3 Point(Vector3 origin,Vector3 direction,float station){
   return origin+direction*station+new Vector3(0f,-DropPerMetreSquared*station*station,0f);
  }
  internal static Vector3 Tangent(Vector3 direction,float station){return direction+new Vector3(0f,-2f*DropPerMetreSquared*station,0f);}
  internal static float Radius(float station){return NozzleRadius+RadiusSlope*Mathf.Max(0f,station);}
 }
}
