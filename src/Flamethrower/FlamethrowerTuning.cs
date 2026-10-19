using BepInEx.Configuration;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  ConfigEntry<float> flameOpacity,flameSaturation,flameSmokeTrail,flameParticleSize,flameSmokeAmount;
  ConfigEntry<float> flameLightStrength,flameLightReach,flameVolume;
  ConfigEntry<float> flameMuzzleGlowStrength;ConfigEntry<bool> flameSurfaceFire;
  void SetupFlamethrowerTuning(){
   flameMuzzleGlowStrength=FixedSetting("Flamethrower lighting","Muzzle glow strength",1f,new ConfigDescription("Muzzle metal heat glow: builds over 1.2 seconds of firing and cools over 2.5 seconds. 0 disables it.",new AcceptableValueRange<float>(0f,2f)));
   flameSurfaceFire=FixedSetting("Flamethrower appearance","Surface fire effects",true,"Small native animated flames on contacted surfaces. They stay lit during contact and fade completely within two seconds after contact stops. Visual only; no lingering damage.");
   flameOpacity=FixedSetting("Flamethrower appearance","Flame opacity",1f,new ConfigDescription("Opacity of the flame body and bright centre. Lower values make the stream more transparent.",new AcceptableValueRange<float>(.1f,1f)));
   flameSaturation=FixedSetting("Flamethrower appearance","Flame saturation",1f,new ConfigDescription("Orange saturation near the nozzle: 0 is gray, 1 is full orange. The stream darkens and grays as it travels.",new AcceptableValueRange<float>(0f,1f)));
   flameSmokeTrail=FixedSetting("Flamethrower appearance","Smoky tail strength",.7f,new ConfigDescription("Strength of the dark gray smoky backdrop behind the flames farther along the stream.",new AcceptableValueRange<float>(0f,1f)));
   flameParticleSize=FixedSetting("Flamethrower appearance","Flame size",1f,new ConfigDescription("Visual flame size multiplier. Contact-distance limits still prevent the stream growing back over the nozzle. Damage and range are unchanged.",new AcceptableValueRange<float>(.5f,1.5f)));
   flameSmokeAmount=FixedSetting("Flamethrower appearance","Smoke amount",1.2f,new ConfigDescription("Muzzle and stream smoke amount multiplier. 0 disables smoke; existing wisps finish fading.",new AcceptableValueRange<float>(0f,2f)));
   flameLightStrength=FixedSetting("Flamethrower lighting","Light strength",1f,new ConfigDescription("Brightness multiplier for the private can-light-style flame illumination. 0 disables it.",new AcceptableValueRange<float>(0f,2f)));
   flameLightReach=FixedSetting("Flamethrower lighting","Light reach",1f,new ConfigDescription("Illumination radius multiplier based on the native can light. Does not change flame damage range.",new AcceptableValueRange<float>(.5f,1.5f)));
   flameVolume=FixedSetting("Flamethrower audio","Attack volume",1f,new ConfigDescription("Portable gas stove activation click and active burner sound volume. 0 mutes this weapon.",new AcceptableValueRange<float>(0f,1f)));
  }
  static float FlameTuningValue(ConfigEntry<float> entry,float fallback,float min,float max){
   float value=entry==null?fallback:entry.Value;
   return Finite(value)?Mathf.Clamp(value,min,max):fallback;
  }
 }
}
