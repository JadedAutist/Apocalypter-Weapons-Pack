using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace ChainsawPrototype {
 public partial class Plugin {
  internal static bool OwnsBloodInput {get{return !ReferenceEquals(instance,null)&&(instance.activeFlame!=null||instance.enabledPreview);}}
  void InstallOptionalBloodEffects(){OptionalBloodEffects.Discover(message=>Logger.LogWarning(message));}
 }
 // Optional, signature-checked adapter for the public BloodFX 0.2 contract.
 // No assembly reference, file load, replacement DLL or config edit is needed.
 internal static class OptionalBloodEffects {
  const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  const string PatchId="apocalypter.weaponspack.optionalblood";
  static object blood;static Type contract;static bool failed,warned;
  static FieldInfo enabled,npcs,trackedFloat,trackedMax,minFraction,meleePending;
  static MethodInfo fireHit,hitCorpse;static Action<string> report;
  static float lastUpdate=float.NegativeInfinity;
  static readonly Dictionary<FsmFloat,float> ownedDrops=new Dictionary<FsmFloat,float>();
  internal static void Discover(Action<string> warning){
   if(failed)return;
   foreach(var info in Chainloader.PluginInfos.Values){
    object plugin=info.Instance;
    if(!ReferenceEquals(plugin,null)&&plugin.GetType().FullName=="BloodFX.Plugin"){Bind(plugin,warning);return;}
   }
  }
  internal static bool Bind(object plugin,Action<string> warning){
   if(plugin==null||plugin.GetType().FullName!="BloodFX.Plugin"||failed)return false;
   if(ReferenceEquals(blood,plugin))return true;
   report=warning;
   try{
    var type=plugin.GetType();
    if(contract!=null&&contract!=type)throw new InvalidOperationException("multiple BloodFX type identities");
    if(contract==null){
     var tracked=type.GetNestedType("Tracked",BindingFlags.NonPublic);
     enabled=type.GetField("cfgEnabled",All);npcs=type.GetField("npcBleed",All);minFraction=type.GetField("minDropFraction",All);meleePending=type.GetField("meleePending",All);
     trackedFloat=tracked==null?null:tracked.GetField("F",All);trackedMax=tracked==null?null:tracked.GetField("MaxSeen",All);
     fireHit=type.GetMethod("FireHit",All,null,new[]{typeof(Transform),typeof(Vector3),typeof(bool),typeof(bool),typeof(float),typeof(bool),typeof(Vector3),typeof(float)},null);
     hitCorpse=type.GetMethod("HitCorpse",All,null,new[]{typeof(Transform),typeof(RaycastHit),typeof(Vector3)},null);
     var onHit=tracked==null?null:type.GetMethod("OnHit",All,null,new[]{tracked,typeof(float),typeof(bool),typeof(Camera)},null);
     var poll=type.GetMethod("Poll",All,null,Type.EmptyTypes,null);var melee=type.GetMethod("HandleMelee",All,null,Type.EmptyTypes,null);var update=type.GetMethod("Update",All,null,Type.EmptyTypes,null);
     if(enabled==null||enabled.FieldType!=typeof(ConfigEntry<bool>)||npcs==null||npcs.FieldType!=typeof(ConfigEntry<bool>)||minFraction==null||minFraction.FieldType!=typeof(ConfigEntry<float>)||
      trackedFloat==null||trackedFloat.FieldType!=typeof(FsmFloat)||trackedMax==null||trackedMax.FieldType!=typeof(float)||meleePending==null||meleePending.FieldType!=typeof(bool)||fireHit==null||hitCorpse==null||onHit==null||poll==null||melee==null||update==null)throw new MissingMemberException("BloodFX effect contract changed");
     var patch=new Harmony(PatchId);
     try{
      patch.Patch(onHit,prefix:new HarmonyMethod(typeof(OptionalBloodEffects),nameof(BeforeHealthHit)));
      patch.Patch(poll,postfix:new HarmonyMethod(typeof(OptionalBloodEffects),nameof(AfterPoll)));
      patch.Patch(melee,prefix:new HarmonyMethod(typeof(OptionalBloodEffects),nameof(BeforeMeleeInput)));
      patch.Patch(update,postfix:new HarmonyMethod(typeof(OptionalBloodEffects),nameof(AfterUpdate)));
     }catch{patch.UnpatchSelf();throw;}
     contract=type;
    }blood=plugin;lastUpdate=float.NegativeInfinity;Clear();return true;
   }catch(Exception error){Fail(error);return false;}
  }
  static bool Active {get{return !failed&&!ReferenceEquals(blood,null)&&enabled.GetValue(blood) is ConfigEntry<bool> entry&&entry.Value;}}
  static bool Running {get{var component=blood as Behaviour;return Active&&((component!=null&&component.isActiveAndEnabled)||Time.realtimeSinceStartup-lastUpdate<=.5f);}}
  internal static void NoteDamage(FsmFloat health,float decrease){
   if(health==null||!(decrease>0f)||float.IsNaN(decrease)||float.IsInfinity(decrease)||!Active)return;
   float old;ownedDrops.TryGetValue(health,out old);ownedDrops[health]=old+decrease;
  }
  internal static bool SolidHit(Transform actor,RaycastHit hit,bool dead){
   if(actor==null||!Running)return false;
   try{
    // Respect the optional mod's explicit NPC blood switch too.
    if(!((ConfigEntry<bool>)npcs.GetValue(blood)).Value)return true;
    // Reuse native corpse policy: cooldown, intensity, multiplier and pools.
    if(dead)hitCorpse.Invoke(blood,new object[]{actor,hit,hit.normal});
    else fireHit.Invoke(blood,new object[]{actor,hit.point,false,false,1f,false,hit.normal,1f});return true;
   }catch(Exception error){Fail(error);return false;}
  }
  static bool BeforeHealthHit(object __instance,object __0,ref float __1){
   if(!ReferenceEquals(__instance,blood)||failed)return true;
   try{
    var health=trackedFloat.GetValue(__0) as FsmFloat;float owned;
    if(health==null||!ownedDrops.TryGetValue(health,out owned))return true;
    __1=Mathf.Max(0f,__1-owned);ownedDrops.Remove(health);
    float maximum=Mathf.Max(.0001f,(float)trackedMax.GetValue(__0));
    return __1>.00001f&&__1/maximum>=((ConfigEntry<float>)minFraction.GetValue(blood)).Value;
   }catch(Exception error){Fail(error);return true;}
  }
  static void AfterPoll(object __instance){if(ReferenceEquals(__instance,blood))Clear();}
  static void AfterUpdate(object __instance){if(ReferenceEquals(__instance,blood))lastUpdate=Time.realtimeSinceStartup;}
  static bool BeforeMeleeInput(object __instance){
   if(!ReferenceEquals(__instance,blood)||failed||!Plugin.OwnsBloodInput)return true;
   // Native click guesses are replaced only while our own weapon is selected.
   // Exact paid solid contacts below work for LMB and continuous RMB alike.
   meleePending.SetValue(__instance,false);return false;
  }
  internal static void Clear(){ownedDrops.Clear();}
  static void Fail(Exception error){failed=true;Clear();if(!warned){warned=true;if(report!=null)report("Optional BloodFX integration unavailable; keeping standalone impacts. "+error.GetBaseException().Message);}}
 }
}
