using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 internal static class WeaponSpawnFuelPolicy {
  internal static FlameFuelKind Kind(bool flamethrower,float roll){return !flamethrower||roll<.70f?FlameFuelKind.Gasoline:roll<.95f?FlameFuelKind.Diesel:FlameFuelKind.EngineOil;}
  internal static float Litres(float fullnessRoll,float amountRoll){return fullnessRoll<.90f?.5f+Mathf.Clamp01(amountRoll)*2.5f:5f;}
  internal static Color CounterColor(float litres,Color normal){return litres>=0f&&litres<.5f?new Color(1f,.65f,.15f,normal.a):normal;}
 }
 public partial class Plugin {
  // Called once by each item's Start, after native restore has had its chance.
  // Loaded weapons and templates never get their fuel rerolled.
  internal static void InitializeSpawnFuel(GameObject item){
   if(ReferenceEquals(instance,null)||item==null||item==instance.template||item==instance.flameTemplate)return;
   var flameItem=item.GetComponent<FlamethrowerItem>();var sawItem=item.GetComponent<ChainsawItem>();bool flame=flameItem!=null;
   if(flame){if(flameItem.SpawnFuelHandled)return;flameItem.SpawnFuelHandled=true;}
   else{if(sawItem==null||sawItem.SpawnFuelHandled)return;sawItem.SpawnFuelHandled=true;}
   if(instance.ChainsawWorldLoading)return;
   float kindRoll=UnityEngine.Random.value,fullnessRoll=UnityEngine.Random.value,amountRoll=UnityEngine.Random.value;
   float litres=WeaponSpawnFuelPolicy.Litres(fullnessRoll,amountRoll);
   if(flame){var tank=item.GetComponentInChildren<FlamethrowerFuelTank>(true);if(tank!=null)tank.Restore(WeaponSpawnFuelPolicy.Kind(true,kindRoll),litres);}
   else{
    var fuel=item.GetComponentInChildren<ChainsawFuel>(true);var fsm=fuel==null?null:fuel.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="LiquidAmount");
    var amount=fsm==null?null:fsm.FsmVariables.FindFsmFloat("Liquid");if(amount!=null)amount.Value=litres;
   }
  }
  static void MarkRestoredWeaponFuel(GameObject item){
   var flame=item.GetComponent<FlamethrowerItem>();if(flame!=null)flame.SpawnFuelHandled=true;
   var saw=item.GetComponent<ChainsawItem>();if(saw!=null)saw.SpawnFuelHandled=true;
  }
  void MarkLoadedWeaponFuel(){
   // Inventory items may stay inactive long after loading, so their Start can
   // run minutes later. Mark them now, independently of the short load window.
   foreach(var marker in Resources.FindObjectsOfTypeAll<FlamethrowerItem>())if(marker!=null&&marker.gameObject!=flameTemplate&&marker.gameObject.scene.IsValid()&&marker.gameObject.scene.isLoaded)MarkRestoredWeaponFuel(marker.gameObject);
   foreach(var marker in Resources.FindObjectsOfTypeAll<ChainsawItem>())if(marker!=null&&marker.gameObject!=template&&marker.gameObject.scene.IsValid()&&marker.gameObject.scene.isLoaded)MarkRestoredWeaponFuel(marker.gameObject);
  }
 }
}
