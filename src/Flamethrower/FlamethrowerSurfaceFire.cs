using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
namespace ChainsawPrototype {
 public partial class Plugin {
  Material flameSurfaceFireMaterial;
  readonly List<FlameSurfaceFire> flameSurfaceFires=new List<FlameSurfaceFire>(24);
  GameObject flameSurfaceFireRoot;bool loggedSurfaceFire;float nextSurfaceFireBirth;
  internal bool FlameSurfaceFireEnabled {get{return flameSurfaceFire==null||flameSurfaceFire.Value;}}
  void PlaceFlameSurfaceFire(RaycastHit hit){
   if(!FlameSurfaceFireEnabled)return;
   var surface=hit.collider;
   if(surface==null||surface.isTrigger||!surface.enabled||!surface.gameObject.activeInHierarchy||!ScorchableFlameSurface(surface))return;
   if(!Finite(hit.point.x)||!Finite(hit.point.y)||!Finite(hit.point.z)||!Finite(hit.normal.x)||!Finite(hit.normal.y)||!Finite(hit.normal.z)||hit.normal.sqrMagnitude<.5f)return;
   float now=Time.unscaledTime;
   for(int i=flameSurfaceFires.Count-1;i>=0;i--){
    var old=flameSurfaceFires[i];
    if(old==null||old.Expired){flameSurfaceFires.RemoveAt(i);continue;}
    if(old.Surface==surface&&(surface.transform.TransformPoint(old.LocalImpact)-hit.point).sqrMagnitude<.045f){old.Touch(now);return;}
   }
   if(now<nextSurfaceFireBirth)return;
   if(flameSurfaceFireMaterial==null){
    var native=NativeFireplaceSurfaceMaterial();if(native==null)return;
    flameSurfaceFireMaterial=new Material(native){name="Flamethrower.TemporarySurfaceFire.Material",hideFlags=HideFlags.DontUnloadUnusedAsset|HideFlags.DontSave,renderQueue=3002};
    flameSurfaceFireMaterial.DisableKeyword("SOFTPARTICLES_ON");
   }
   if(flameSurfaceFires.Count>=24){flameSurfaceFires[0].Expire();flameSurfaceFires.RemoveAt(0);}
   if(flameSurfaceFireRoot==null)flameSurfaceFireRoot=new GameObject("Flamethrower.TemporarySurfaceFires"){hideFlags=HideFlags.DontSave};
   var go=new GameObject("Flamethrower.TemporarySurfaceFire"){hideFlags=HideFlags.DontSave};go.transform.SetParent(flameSurfaceFireRoot.transform,false);
   var fire=go.AddComponent<ParticleSystem>();fire.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   // Native Fire Quad is a stationary, upright procedural flame sheet: one
   // one-second particle per loop, VerticalBillboard with a .25 Y pivot.
   // Scale its native 1.6m sheet to .62m to fit a temporary scorch patch.
   var main=fire.main;main.playOnAwake=false;main.loop=true;main.duration=1f;main.useUnscaledTime=true;main.startLifetime=1f;main.startSpeed=0f;main.startSize=.62f;main.startRotation=0f;main.startColor=Color.white;main.maxParticles=2;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
   var emission=fire.emission;emission.rateOverTime=1f;
   var shape=fire.shape;shape.enabled=false;
   var renderer=fire.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=flameSurfaceFireMaterial;renderer.renderMode=ParticleSystemRenderMode.VerticalBillboard;renderer.pivot=new Vector3(0f,.25f,0f);renderer.alignment=ParticleSystemRenderSpace.View;renderer.velocityScale=0f;renderer.cameraVelocityScale=0f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
   var patch=go.AddComponent<FlameSurfaceFire>();patch.Configure(this,surface,surface.transform.InverseTransformPoint(hit.point),surface.transform.InverseTransformDirection(hit.normal.normalized),now,fire,renderer);
   flameSurfaceFires.Add(patch);nextSurfaceFireBirth=now+.1f;fire.Play(true);fire.Emit(1);
   if(!loggedSurfaceFire){loggedSurfaceFire=true;Logger.LogInfo("Flamethrower surface fire created on "+surface.name+" using "+flameSurfaceFireMaterial.shader.name+"; independent of soot projection, fades 2 s after last contact.");}
  }
  void ClearFlameSurfaceFires(){foreach(var fire in flameSurfaceFires)if(fire!=null)fire.Expire();flameSurfaceFires.Clear();nextSurfaceFireBirth=0f;if(flameSurfaceFireRoot!=null)Destroy(flameSurfaceFireRoot);flameSurfaceFireRoot=null;}
  void CleanupFlamethrowerSurfaceFire(){ClearFlameSurfaceFires();if(flameSurfaceFireMaterial!=null)Destroy(flameSurfaceFireMaterial);flameSurfaceFireMaterial=null;}
 }
 [DefaultExecutionOrder(32010)] internal sealed class FlameSurfaceFire:MonoBehaviour {
  Plugin owner;Collider surface;Vector3 localImpact,localNormal;float born,lastContact;
  internal Collider Surface{get{return surface;}}
  internal Vector3 LocalImpact{get{return localImpact;}}
  internal bool Expired{get{return expired;}}
  ParticleSystem particles;ParticleSystemRenderer renderer;MaterialPropertyBlock properties;Color tint;bool expired;float opacityBoost,noisesBoost,emissive;Vector3 initialScale;
  static readonly int Colour=Shader.PropertyToID("_Color"),Tint=Shader.PropertyToID("_TintColor");
  static readonly int Opacity=Shader.PropertyToID("_Opacity_Boost"),Noises=Shader.PropertyToID("_NoisesOpacityBoost");
  static readonly int Emissive=Shader.PropertyToID("_EmissiveIntensity");
  internal void Configure(Plugin plugin,Collider target,Vector3 point,Vector3 normal,float now,ParticleSystem effect,ParticleSystemRenderer visual){
   owner=plugin;surface=target;localImpact=point;localNormal=normal;born=lastContact=now;particles=effect;renderer=visual;properties=new MaterialPropertyBlock();initialScale=transform.localScale;
   var material=renderer.sharedMaterial;tint=material.HasProperty("_TintColor")?material.GetColor("_TintColor"):Color.white;
   opacityBoost=material.HasProperty("_Opacity_Boost")?material.GetFloat("_Opacity_Boost"):1f;noisesBoost=material.HasProperty("_NoisesOpacityBoost")?material.GetFloat("_NoisesOpacityBoost"):1f;emissive=material.HasProperty("_EmissiveIntensity")?material.GetFloat("_EmissiveIntensity"):1f;FollowSurface();ApplyFade(0f);
  }
  void FollowSurface(){
   if(surface==null)return;var normal=surface.transform.TransformDirection(localNormal).normalized;
   transform.SetPositionAndRotation(surface.transform.TransformPoint(localImpact)+normal*.06f,Quaternion.LookRotation(normal));
  }
  internal void Touch(float now){if(!expired)lastContact=now;}
  void LateUpdate(){if(!expired)FollowSurface();}
  void Update(){
   if(expired)return;float age=Time.unscaledTime-born,idle=Time.unscaledTime-lastContact;
   if(surface==null||object.ReferenceEquals(owner,null)||!owner.FlameSurfaceFireEnabled||idle>=FlameHeatEnvelope.SurfaceFireSeconds){Expire();return;}
   // Contact refreshes fire, never the separate five-second soot deadline.
   ApplyFade(Mathf.Clamp01(age/.08f)*FlameHeatEnvelope.SurfaceFire(Mathf.Max(.08f,idle)));
  }
  void ApplyFade(float fade){
   bool visible=surface!=null&&surface.enabled&&surface.gameObject.activeInHierarchy&&fade>.0001f;
   renderer.enabled=visible;var emission=particles.emission;emission.rateOverTime=visible?1f:0f;
   // Shrink the already-live sheet too. Changing startSize affects only new
   // particles and cannot hide the native shader's abrupt final-frame cutoff.
   float size=Mathf.Sqrt(Mathf.Clamp01(fade));transform.localScale=initialScale*size;
   var main=particles.main;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
   var colour=tint;colour.a*=fade;properties.SetColor(Tint,colour);properties.SetColor(Colour,colour);
   // This shader uses procedural opacity, rather than the old atlas tint.
   properties.SetFloat(Opacity,opacityBoost*fade);properties.SetFloat(Noises,noisesBoost*fade);properties.SetFloat(Emissive,emissive*fade);renderer.SetPropertyBlock(properties);
  }
  internal void Expire(){if(expired)return;expired=true;if(renderer!=null)renderer.enabled=false;if(particles!=null)particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);Destroy(gameObject);}
 }
}
