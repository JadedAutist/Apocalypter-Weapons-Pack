using UnityEngine;
namespace ChainsawPrototype {
 internal static class FlameHeatEnvelope {
  internal const float HeatUpSeconds=1.2f,CoolDownSeconds=2.5f,SurfaceFireSeconds=2f;
  internal static float Step(float heat,bool firing,float delta){return Mathf.MoveTowards(heat,firing?1f:0f,Mathf.Clamp(delta,0f,.1f)/(firing?HeatUpSeconds:CoolDownSeconds));}
  internal static float SurfaceFire(float age){
   if(age<0f||age>=SurfaceFireSeconds)return 0f;
   float fade=Mathf.Clamp01((SurfaceFireSeconds-age)/(SurfaceFireSeconds-.65f));
   return Mathf.Clamp01(age/.08f)*fade*fade*(3f-2f*fade);
  }
 }
}
