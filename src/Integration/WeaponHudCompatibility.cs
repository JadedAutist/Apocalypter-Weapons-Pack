using System;
using HarmonyLib;
using UnityEngine;

namespace ChainsawPrototype {
 public partial class Plugin {
  bool packHudInstalled;float nextPackHudCheck;
  void InstallPackHudCompatibility(){
   if(packHudInstalled||Time.unscaledTime<nextPackHudCheck)return;nextPackHudCheck=Time.unscaledTime+3f;
   var assets=OptionalWeaponTypes.Find("WildWasteland.Modules.CleanHUD.Assets");if(assets==null)return;
   var icon=AccessTools.Method(assets,"WeaponIcon",new[]{typeof(GameObject)});
   if(icon==null||icon.ReturnType!=typeof(Texture2D))return;
   try{
    harmony.Patch(icon,prefix:new HarmonyMethod(typeof(Plugin),nameof(PackHudIcon)));
    packHudInstalled=true;Logger.LogInfo("WildWasteland CleanHUD: pack inventory images registered.");
   }catch(Exception error){harmony.Unpatch(icon,HarmonyPatchType.Prefix,harmony.Id);packHudInstalled=true;Logger.LogWarning("Optional WildWasteland icon bridge unavailable: "+error.Message);}
  }
  static bool PackHudIcon(GameObject __0,ref Texture2D __result){
   if(__0==null||ReferenceEquals(instance,null))return true;
   // CleanHUD renders meshes rather than reading imageUI. Only our item markers
   // opt into the existing inventory image; names and other mods' items are untouched.
   Texture2D icon=null;
   if(__0.GetComponentInParent<ChainsawItem>(true)!=null)icon=instance.chainsawIcon;
   else if(__0.GetComponentInParent<FlamethrowerItem>(true)!=null)icon=instance.flameIcon;
   if(icon==null)return true;__result=icon;return false;
  }
 }
}
