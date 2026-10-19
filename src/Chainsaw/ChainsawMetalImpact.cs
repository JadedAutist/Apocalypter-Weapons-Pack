using System;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  GameObject nativeMetalImpact;float nativeMetalRetry;
  ConfigEntry<float> companionMetalScale;ConfigEntry<bool> companionMetalEnabled;float nextMetalSettingsCheck;
  float MetalImpactScale(){
   // Read optional public-mod settings in memory, never its files or methods.
   // Missing/changed companion versions retain standalone melee-sized sparks.
   if(Time.unscaledTime>=nextMetalSettingsCheck){
    nextMetalSettingsCheck=Time.unscaledTime+3f;
    var type=OptionalWeaponTypes.Find("WildWasteland.Modules.Guns.GunsModule");
    const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    companionMetalScale=type==null?null:type.GetField("MetalSparksScale",flags)?.GetValue(null) as ConfigEntry<float>;
    companionMetalEnabled=type==null?null:type.GetField("MetalSparks",flags)?.GetValue(null) as ConfigEntry<bool>;
   }
   if(companionMetalEnabled!=null&&!companionMetalEnabled.Value)return 0f;
   float scale=companionMetalScale==null?.1f:companionMetalScale.Value;
   return Finite(scale)?Mathf.Clamp(scale,.05f,4f):.1f;
  }
  static void ScaleMetalImpact(GameObject effect,float scale){
   foreach(var particles in effect.GetComponentsInChildren<ParticleSystem>(true)){var main=particles.main;main.scalingMode=ParticleSystemScalingMode.Hierarchy;}
   effect.transform.localScale*=scale;
  }
  static ChainsawSurface MeleeSurface(Collider surface,bool flesh){
   if(flesh)return ChainsawSurface.Flesh;if(surface==null)return ChainsawSurface.Other;
   var renderer=surface.GetComponent<Renderer>()??surface.GetComponentInParent<Renderer>();
   var material=renderer==null?null:renderer.sharedMaterial;var physics=surface.sharedMaterial;
   var kind=WeaponFeedbackPolicy.Surface(false,physics==null?null:physics.name,material==null?null:material.name,surface.name);
   if(kind==ChainsawSurface.Metal||kind==ChainsawSurface.Other){
    if(WeaponFeedbackPolicy.NonMetalMaterial(material==null?null:material.name))return ChainsawSurface.Other;
    bool vehicle=false;
    // A wheel/pane collider can inherit the vehicle physics material or renderer.
    for(var node=surface.transform;node!=null;node=node.parent){
     string name=node.name;
     if(name.IndexOf("wheel",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("tire",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("tyre",StringComparison.OrdinalIgnoreCase)>=0||name.StartsWith("windshield_1_glass",StringComparison.OrdinalIgnoreCase)||string.Equals(name,"collider_SeeTrough",StringComparison.OrdinalIgnoreCase))return ChainsawSurface.Other;
     // Native painted bodies/doors need not expose a named metal material.
     vehicle|=node.CompareTag("Vehicle")||node.CompareTag("vehPart")||node.CompareTag("vehPartRemoved");
    }
    if(vehicle)return ChainsawSurface.Metal;
   }
   return kind;
  }
  GameObject NativeMetalImpact(){
   if(nativeMetalImpact!=null)return nativeMetalImpact;
   if(!ReferenceEquals(nativeMetalImpact,null)){nativeMetalImpact=null;nativeMetalRetry=0f;}
   if(Time.unscaledTime<nativeMetalRetry)return null;nativeMetalRetry=Time.unscaledTime+2f;
   // Use the current shared game prefab, not a copied asset or a third-party API.
   // Scene objects and the identically named child particle are not templates.
   foreach(var source in Resources.FindObjectsOfTypeAll<GameObject>())
    if(source!=null&&source.name=="MetalImpact"&&!source.scene.IsValid()&&source.transform.parent==null){nativeMetalImpact=source;break;}
   return nativeMetalImpact;
  }
 }
}
