using UnityEngine;
using UnityEngine.Rendering;

namespace ChainsawPrototype {
 public partial class Plugin {
  Material AnimatedFireMaterial(string name){
   var native=NativeFireplaceSurfaceMaterial();if(native==null)return null;
   var material=new Material(native){name=name,hideFlags=HideFlags.DontSave,renderQueue=3002};
   // Keep the earlier fireplace shader and its original mask/noise textures.
   // Shape and motion variation belong to our renderers, never shared assets.
   material.DisableKeyword("SOFTPARTICLES_ON");return material;
  }
  internal void ConfigureFireTongues(ParticleSystem fire,Material material,uint seed,float small,float big,float rate,bool ground,float animationSpeed){
   fire.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);fire.useAutoRandomSeed=false;fire.randomSeed=seed==0?1u:seed;
   var main=fire.main;main.playOnAwake=false;main.loop=true;main.duration=1f;main.startLifetime=new ParticleSystem.MinMaxCurve(.9f,1.25f);main.startSpeed=0f;main.startSize3D=true;main.startSizeX=new ParticleSystem.MinMaxCurve(small*.7f,big*.9f);main.startSizeY=new ParticleSystem.MinMaxCurve(small,big);main.startSizeZ=1f;main.startRotation=new ParticleSystem.MinMaxCurve(-.1f,.1f);main.startColor=Color.white;main.maxParticles=ground?6:2;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.simulationSpeed=animationSpeed;
   var emission=fire.emission;emission.rateOverTime=rate;
   var shape=fire.shape;shape.enabled=ground;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.3f;if(ground)shape.rotation=new Vector3(90,0,0);
   var velocity=fire.velocityOverLifetime;velocity.enabled=false;
   var colours=fire.colorOverLifetime;colours.enabled=false;
   var size=fire.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.8f),new Keyframe(.35f,1),new Keyframe(1,.85f)));
   var sheet=fire.textureSheetAnimation;sheet.enabled=false;
   var visual=fire.GetComponent<ParticleSystemRenderer>();visual.sharedMaterial=material;visual.renderMode=ParticleSystemRenderMode.VerticalBillboard;visual.pivot=new Vector3(0,.25f,0);visual.shadowCastingMode=ShadowCastingMode.Off;visual.receiveShadows=false;
   FadeFireTongues(fire,material,new MaterialPropertyBlock(),1f);
   // Each emitter starts partway through a different seeded lifecycle without
   // changing Unity's shared random sequence used for loot and other mods.
   fire.Simulate((seed%31)*.017f,true,false,true);fire.Play(true);fire.Emit(1);
  }
  internal void FadeFireTongues(ParticleSystem fire,Material material,MaterialPropertyBlock properties,float fade){
   properties.Clear();uint seed=fire.randomSeed;
   float motion=.8f+.4f*(seed&255u)/255f;
   if(material.HasProperty("_Global_Speed"))properties.SetFloat("_Global_Speed",material.GetFloat("_Global_Speed")*motion);
   FireVectorVariation(material,properties,"_Noise_01_Scale",.8f+.4f*((seed>>4)&255u)/255f,1.12f-.24f*((seed>>12)&255u)/255f);
   FireVectorVariation(material,properties,"_Noise_02_Scale",1.15f-.3f*((seed>>8)&255u)/255f,.85f+.3f*((seed>>16)&255u)/255f);
   FireVectorVariation(material,properties,"_Noise_01_Speed",motion,motion);
   FireVectorVariation(material,properties,"_Noise_02_Speed",1.9f-motion,1.9f-motion);
   FadeFireFloat(material,properties,"_Opacity_Boost",fade);FadeFireFloat(material,properties,"_NoisesOpacityBoost",fade);FadeFireFloat(material,properties,"_EmissiveIntensity",fade);
   if(material.HasProperty("_TintColor")){var tint=material.GetColor("_TintColor");tint.a*=fade;properties.SetColor("_TintColor",tint);}
   if(material.HasProperty("_Color")){var colour=material.GetColor("_Color");colour.a*=fade;properties.SetColor("_Color",colour);}
   fire.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(properties);
  }
  static void FireVectorVariation(Material material,MaterialPropertyBlock properties,string name,float x,float y){if(!material.HasProperty(name))return;var value=material.GetVector(name);value.x*=x;value.y*=y;properties.SetVector(name,value);}
  static void FadeFireFloat(Material material,MaterialPropertyBlock properties,string name,float fade){if(material.HasProperty(name))properties.SetFloat(name,material.GetFloat(name)*fade);}
 }
}
