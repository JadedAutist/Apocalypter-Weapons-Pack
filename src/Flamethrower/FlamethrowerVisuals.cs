using UnityEngine;
using UnityEngine.Rendering;
namespace ChainsawPrototype {
 public partial class Plugin {
  ParticleSystem tunedFlameParticles;
  float tunedFlameOpacity=-1f,tunedFlameSaturation=-1f,tunedFlameTrail=-1f,tunedFlameSize=-1f,tunedFlameSmoke=-1f;

  internal static Color MutedFlameColor(Color orange,float saturation,float smokeBlend){
   float grey=orange.r*.2126f+orange.g*.7152f+orange.b*.0722f;
   var muted=Color.Lerp(new Color(grey,grey,grey,1f),orange,Mathf.Clamp01(saturation));
   return Color.Lerp(muted,new Color(.17f,.165f,.15f,1f),Mathf.Clamp01(smokeBlend));
  }
  internal static float FlameParticleWidth(float distance,float maximum,float size){
   // Scale first, then cap the largest starting diameter to the same .32*d
   // envelope used before the slider. The birth/growth curves start at zero.
   size=Mathf.Clamp(size,.5f,1.5f);
   return Mathf.Min(size,Mathf.Max(0f,distance)*.32f/maximum);
  }
  void CreateFlameSmokeBackdrop(Transform parent){
   if(flameSmokeMaterial==null)return;
   var go=new GameObject("Flamethrower.SmokeBackdrop");go.transform.SetParent(parent,false);
   flameTrailSmoke=go.AddComponent<ParticleSystem>();flameTrailSmoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=flameTrailSmoke.main;main.playOnAwake=false;main.loop=true;main.startSpeed=FlameSpeed;main.startLifetime=CurrentFlameRange/FlameSpeed;main.startSize=new ParticleSystem.MinMaxCurve(.38f,.62f);main.startRotation=new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);main.maxParticles=160;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=-.025f;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
   var emission=flameTrailSmoke.emission;emission.rateOverTime=0f;
   var shape=flameTrailSmoke.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=3.5f;shape.radius=.009f;
   var sizes=flameTrailSmoke.sizeOverLifetime;sizes.enabled=true;sizes.size=new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(new Keyframe(0,0,3f,3f),new Keyframe(.2f,.6f,1.25f,1.25f),new Keyframe(1,1.6f,1.25f,1.25f)));
   var colours=flameTrailSmoke.colorOverLifetime;colours.enabled=true;
   var renderer=flameTrailSmoke.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=flameSmokeMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.alignment=ParticleSystemRenderSpace.View;renderer.velocityScale=0f;renderer.cameraVelocityScale=0f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
   FlameSurfaceCollision(flameTrailSmoke);
   ConfigureFlameTrajectory(flameTrailSmoke);
  }
  void ApplyFlameVisualTuning(float distance){
   if(flameParticles==null)return;
   float opacity=FlameTuningValue(flameOpacity,.7f,.1f,1f),saturation=FlameTuningValue(flameSaturation,.45f,0f,1f),trail=FlameTuningValue(flameSmokeTrail,.7f,0f,1f),size=FlameTuningValue(flameParticleSize,1f,.5f,1.5f),smoke=FlameTuningValue(flameSmokeAmount,1.2f,0f,2f);
   if(tunedFlameParticles!=flameParticles||opacity!=tunedFlameOpacity||saturation!=tunedFlameSaturation||trail!=tunedFlameTrail||size!=tunedFlameSize||smoke!=tunedFlameSmoke){
    tunedFlameParticles=flameParticles;tunedFlameOpacity=opacity;tunedFlameSaturation=saturation;tunedFlameTrail=trail;tunedFlameSize=size;tunedFlameSmoke=smoke;
    var colours=flameParticles.colorOverLifetime;var gradient=new Gradient();
    gradient.SetKeys(new[]{new GradientColorKey(MutedFlameColor(new Color(1f,.7f,.25f),saturation,0f),0f),new GradientColorKey(MutedFlameColor(new Color(1f,.5f,.1f),saturation,trail*.05f),.28f),new GradientColorKey(MutedFlameColor(new Color(.95f,.34f,.08f),saturation,trail*.12f),.65f),new GradientColorKey(MutedFlameColor(new Color(.4f,.22f,.12f),saturation,trail*.55f),1f)},new[]{new GradientAlphaKey(.1f*opacity,0f),new GradientAlphaKey(.52f*opacity,.08f),new GradientAlphaKey(.48f*opacity,.7f),new GradientAlphaKey(.18f*opacity,.9f),new GradientAlphaKey(0f,1f)});colours.color=gradient;
    // Keep orange combustion through most of the jet. Separate dark smoke
    // supplies its backdrop rather than prematurely turning the fire gray.
    if(flameHotStream!=null){var hotColours=flameHotStream.colorOverLifetime;var hotGradient=new Gradient();hotGradient.SetKeys(new[]{new GradientColorKey(MutedFlameColor(new Color(1f,.96f,.78f),saturation,0f),0f),new GradientColorKey(MutedFlameColor(new Color(1f,.82f,.4f),saturation,0f),.35f),new GradientColorKey(MutedFlameColor(new Color(1f,.48f,.12f),saturation,0f),.75f),new GradientColorKey(MutedFlameColor(new Color(.5f,.25f,.1f),saturation,trail*.35f),1f)},new[]{new GradientAlphaKey(0f,0f),new GradientAlphaKey(.7f*opacity,.08f),new GradientAlphaKey(.6f*opacity,.65f),new GradientAlphaKey(.2f*opacity,.9f),new GradientAlphaKey(0f,1f)});hotColours.color=hotGradient;}
    if(flameCore!=null){var main=flameCore.main;main.startColor=new Color(1f,.78f,.5f,Mathf.Lerp(.38f,.85f,opacity));}
    if(flameContact!=null){var main=flameContact.main;var color=MutedFlameColor(new Color(.9f,.45f,.15f),saturation,trail*.15f);color.a=.4f*opacity;main.startColor=color;}
    if(flameSmoke!=null){
     var main=flameSmoke.main;main.startSize=new ParticleSystem.MinMaxCurve(.045f*size,.09f*size);
     var emission=flameSmoke.emission;emission.rateOverTime=44f*smoke;
     var smokeColours=flameSmoke.colorOverLifetime;var smokeGradient=new Gradient();smokeGradient.SetKeys(new[]{new GradientColorKey(new Color(.34f,.31f,.27f),0f),new GradientColorKey(new Color(.2f,.19f,.18f),1f)},new[]{new GradientAlphaKey(0f,0f),new GradientAlphaKey(.28f,.18f),new GradientAlphaKey(.16f,.6f),new GradientAlphaKey(0f,1f)});smokeColours.color=smokeGradient;
    }
    if(flameTrailSmoke!=null){
     var emission=flameTrailSmoke.emission;emission.rateOverTime=72f*smoke*trail;
     var smokeColours=flameTrailSmoke.colorOverLifetime;var smokeGradient=new Gradient();smokeGradient.SetKeys(new[]{new GradientColorKey(new Color(.29f,.27f,.23f),0f),new GradientColorKey(new Color(.19f,.18f,.16f),.55f),new GradientColorKey(new Color(.11f,.11f,.105f),1f)},new[]{new GradientAlphaKey(0f,0f),new GradientAlphaKey(.08f*trail,.18f),new GradientAlphaKey(.24f*trail,.65f),new GradientAlphaKey(0f,1f)});smokeColours.color=smokeGradient;
    }
   }
   var jet=flameParticles.main;float width=FlameParticleWidth(distance,.44f,size);jet.startSize=new ParticleSystem.MinMaxCurve(.28f*width,.44f*width);
   if(flameHotStream!=null){var main=flameHotStream.main;main.startLifetime=Mathf.Max(.001f,distance/FlameSpeed);float hotWidth=FlameParticleWidth(distance,.16f,size);main.startSize=new ParticleSystem.MinMaxCurve(.1f*hotWidth,.16f*hotWidth);}
   if(flameTrailSmoke!=null){var main=flameTrailSmoke.main;main.startLifetime=Mathf.Max(.001f,distance/FlameSpeed);float smokeWidth=FlameParticleWidth(distance,.62f,size);main.startSize=new ParticleSystem.MinMaxCurve(.38f*smokeWidth,.62f*smokeWidth);}
   if(flameContact!=null){var main=flameContact.main;float contactWidth=FlameParticleWidth(distance,.24f,size);main.startSize=new ParticleSystem.MinMaxCurve(.12f*contactWidth,.24f*contactWidth);}
  }
 }
}
