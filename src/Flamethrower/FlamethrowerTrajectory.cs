using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  // Segment the same parabola used by the particles; the worst chord error is
  // below .11 mm at a .25m step. The optional offset traces a cone edge to a limb.
  RaycastHit? FlameTrajectoryCast(Vector3 origin,Vector3 direction,float distance,float radius,out float station,Vector3 offset=default(Vector3),float step=FlameTrajectory.Step){
   station=Mathf.Max(0f,distance);if(distance<=.0001f)return null;
   int segments=Mathf.Max(1,Mathf.CeilToInt(distance/step));
   var start=origin;
   for(int i=1;i<=segments;i++){
    float next=distance*i/segments,previous=distance*(i-1)/segments;
    var end=FlameTrajectory.Point(origin,direction,next)+offset*(next/distance);
    var delta=end-start;float length=delta.magnitude;
    var hit=length>.0001f?FlameCast(start,delta/length,length,radius,true):null;
    if(hit!=null){station=previous+(next-previous)*Mathf.Clamp01(hit.Value.distance/length);return hit;}
    start=end;
   }
   return null;
  }
 }
}
