using BepInEx.Configuration;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  internal sealed class FlameFuelBalance {
   internal ConfigEntry<float> Damage,Range,Cost,Rate;
  }
  readonly FlameFuelBalance[] flameFuelBalances=new FlameFuelBalance[3];
  ConfigEntry<float> sawFuelCost,sawAttackRate;
  void SetupFlameFuelBalance(){
   float[] damage={20f,30f,45f},range={5f,7f,10f};
   for(int i=0;i<3;i++){
    string section="Flamethrower - "+FlamethrowerFuelPolicy.Label((FlameFuelKind)i);
    flameFuelBalances[i]=new FlameFuelBalance{
     Damage=Config.Bind(section,"Damage per second",damage[i],new ConfigDescription("Damage per second for every enemy in the stream.",new AcceptableValueRange<float>(1f,200f))),
     Range=Config.Bind(section,"Range",range[i],new ConfigDescription("Maximum contact range in metres for this fuel.",new AcceptableValueRange<float>(1f,10f))),
     Cost=Config.Bind(section,"Fuel per attack",.1f,new ConfigDescription("Fuel cost per attack interval, including misses. Drains continuously at cost times attack rate in litres/second; partial fuel is usable.",new AcceptableValueRange<float>(.1f,.5f))),
     Rate=Config.Bind(section,"Attack rate",5f,new ConfigDescription("Damage contacts per second. Damage per second stays independent; continuous fuel use is cost times rate.",new AcceptableValueRange<float>(1f,20f)))};
   }
   // Legacy private aliases keep template initialization independent of a tank.
   flameDamage=flameFuelBalances[0].Damage;flameRange=flameFuelBalances[0].Range;
  }
  FlameFuelKind FuelKind(GameObject item){var tank=item==null?null:item.GetComponentInChildren<FlamethrowerFuelTank>(true);return tank==null?FlameFuelKind.Gasoline:tank.Kind;}
  FlameFuelBalance FuelBalance(GameObject item){return flameFuelBalances[(int)FuelKind(item)];}
  float CurrentFlameRange {get{return activeFlame==null?flameRange.Value:FuelBalance(activeFlame.gameObject).Range.Value;}}
  float CurrentFlameCost {get{return activeFlame==null?.1f:FuelBalance(activeFlame.gameObject).Cost.Value;}}
  internal static float CurrentChainsawFuelCost {get{return ReferenceEquals(instance,null)||instance.sawFuelCost==null?.1f:instance.sawFuelCost.Value;}}
 }
}
