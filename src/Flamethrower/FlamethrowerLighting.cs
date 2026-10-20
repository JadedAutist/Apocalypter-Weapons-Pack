using UnityEngine;
using System;
namespace ChainsawPrototype {
 public partial class Plugin {
  Light flameLight;
  readonly Light[] flamePathLights=new Light[3];
  float flameLightingLevel;
  int flameLightingFrame=-1;
  bool flameCanLightStyleReady;
  float nextFlameCanLightSearch;
  Color flameCanLightColor=new Color(1f,.992025f,.806604f);
  float flameCanLightRange=3f,flameCanLightTemperature=6570f,flameCanLightBias=.05f,flameCanLightNormalBias=.4f,flameCanLightNearPlane=.1f;
  int flameCanLightMask=98261;
  bool flameCanLightUseTemperature=true;

  static Light CreateFlameLight(Transform parent,string name){
   var go=new GameObject(name);go.transform.SetParent(parent,false);
   var light=go.AddComponent<Light>();light.type=LightType.Point;
   light.intensity=0f;light.enabled=false;
   // Real surface lighting along the jet, rather than emission on its sprites.
   // ForcePixel keeps these short-lived lights from becoming weak vertex lights
   // under the game's forward-renderer light selection. Global quality stays intact.
   light.renderMode=LightRenderMode.ForcePixel;light.shadows=LightShadows.None;
   return light;
  }
  static bool NativeCanLight(Light light){
   if(light==null||light.type!=LightType.Point)return false;
   for(var node=light.transform;node!=null;node=node.parent){string name=node.name;if(name.Equals("tin_can_light",StringComparison.OrdinalIgnoreCase)||name.StartsWith("tin_can_light(Clone)",StringComparison.OrdinalIgnoreCase)||name.StartsWith("tin_can_light (",StringComparison.OrdinalIgnoreCase))return true;}
   return false;
  }
  void ReadNativeCanLightStyle(){
   if(flameCanLightStyleReady||Time.unscaledTime<nextFlameCanLightSearch)return;
   nextFlameCanLightSearch=Time.unscaledTime+3f;
   foreach(var light in Resources.FindObjectsOfTypeAll<Light>()){
    if(!NativeCanLight(light))continue;
    // Read the light only. Its native activation/FSM, prefab and shared
    // materials stay untouched; inspected authored values cover unloaded scenes.
    flameCanLightColor=light.color;flameCanLightRange=Mathf.Clamp(light.range,1f,5f);flameCanLightMask=light.cullingMask;
    flameCanLightUseTemperature=light.useColorTemperature;flameCanLightTemperature=light.colorTemperature;
    flameCanLightBias=light.shadowBias;flameCanLightNormalBias=light.shadowNormalBias;flameCanLightNearPlane=light.shadowNearPlane;
    flameCanLightStyleReady=true;break;
   }
   ApplyCanLightStyle(flameLight,true);
   foreach(var light in flamePathLights)ApplyCanLightStyle(light,false);
  }
  void ApplyCanLightStyle(Light light,bool shadows){
   if(light==null)return;
   light.color=flameCanLightColor;light.cullingMask=flameCanLightMask;light.bounceIntensity=0f;
   light.useColorTemperature=flameCanLightUseTemperature;light.colorTemperature=flameCanLightTemperature;
   // One canlight-style shadow source is bounded at one point-light cubemap.
   // The three softer path fills retain their inexpensive unshadowed rendering.
   light.shadows=shadows?LightShadows.Hard:LightShadows.None;
   light.shadowBias=flameCanLightBias;light.shadowNormalBias=flameCanLightNormalBias;light.shadowNearPlane=flameCanLightNearPlane;
  }
  void CreateFlamethrowerLighting(Transform parent){
   flameLight=CreateFlameLight(parent,"Flamethrower.MuzzleLight");
   for(int i=0;i<flamePathLights.Length;i++)flamePathLights[i]=CreateFlameLight(parent,"Flamethrower.PathLight."+i);
   ApplyCanLightStyle(flameLight,true);foreach(var light in flamePathLights)ApplyCanLightStyle(light,false);ReadNativeCanLightStyle();
   flameLightingLevel=0f;flameLightingFrame=-1;
  }
  void UpdateFlamethrowerLighting(Vector3 origin,Vector3 direction,float distance,bool firing,bool blocked){
   if(flameLight==null)return;
   if(blocked||Time.timeScale<=0f||Cursor.visible){StopFlamethrowerLighting();return;}
   ReadNativeCanLightStyle();
   if(flameLightingFrame!=Time.frameCount){
    flameLightingFrame=Time.frameCount;
    flameLightingLevel=Mathf.MoveTowards(flameLightingLevel,firing?1f:0f,Mathf.Max(0f,Time.unscaledDeltaTime)/(firing?.035f:.10f));
   }
   float strength=FlameTuningValue(flameLightStrength,1f,0f,2f),reach=FlameTuningValue(flameLightReach,1f,.5f,1.5f);
   bool visible=flameLightingLevel>.001f&&strength>.001f;
   // Native LightFlicker: Perlin noise at speed 4, multiplied by 2, then
   // clamped to .7-1.5 before setting the canlight's intensity every frame.
   float flicker=Mathf.Clamp(Mathf.PerlinNoise(Time.time*4f,3f)*2f,.7f,1.5f);
   distance=Mathf.Max(0f,distance);
   flameLight.transform.position=FlameTrajectory.Point(origin,direction,Mathf.Min(.06f,distance*.1f));
   flameLight.range=flameCanLightRange*reach;flameLight.intensity=flicker*flameLightingLevel*strength;flameLight.enabled=visible;
   for(int i=0;i<flamePathLights.Length;i++){
    var light=flamePathLights[i];if(light==null)continue;
    // All centres stay on the unobstructed stream. Overlapping radii light the
    // ground and nearby scenery, including the surface at its end.
    float station=distance*(.22f+.32f*i);
    light.transform.position=FlameTrajectory.Point(origin,direction,station);
    light.range=flameCanLightRange*reach;
    // Short jets cluster the three lights; reduce their combined energy so a
    // close wall does not receive three full-length flame lights at one point.
    light.intensity=.55f*flameLightingLevel*flicker*strength*Mathf.Clamp(distance/2.5f,.12f,1f);
    light.enabled=visible&&distance>.12f;
   }
  }
  void StopFlamethrowerLighting(){
   flameLightingLevel=0f;flameLightingFrame=-1;
   if(flameLight!=null){flameLight.intensity=0f;flameLight.enabled=false;}
   foreach(var light in flamePathLights)if(light!=null){light.intensity=0f;light.enabled=false;}
  }
 }
}

