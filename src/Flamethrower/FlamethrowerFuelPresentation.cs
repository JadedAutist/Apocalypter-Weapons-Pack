using System;
using System.Globalization;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  internal static float ConfiguredFlamethrowerDamage {get{return ReferenceEquals(instance,null)||instance.flameDamage==null?20f:instance.flameDamage.Value;}}
  internal static float FlamethrowerDamageFor(GameObject item){return ReferenceEquals(instance,null)?20f:instance.FuelBalance(item).Damage.Value;}
  internal static float FlamethrowerRangeFor(GameObject item){return ReferenceEquals(instance,null)?3f:instance.FuelBalance(item).Range.Value;}
  internal static FlameFuelKind FlamethrowerKindFor(GameObject item){return ReferenceEquals(instance,null)?FlameFuelKind.Gasoline:instance.FuelKind(item);}
  internal static string FlamethrowerFuelDetails(GameObject item){
   string details="Damage - "+FlamethrowerDamageFor(item).ToString("0.##",CultureInfo.InvariantCulture)+" / sec · Range - "+FlamethrowerRangeFor(item).ToString("0.##",CultureInfo.InvariantCulture)+" m";
   var fuel=item==null?null:item.GetComponentInChildren<ChainsawFuel>(true);
   if(fuel!=null)details+="\n"+(fuel.Litres<=.00001f?"Tank empty":FlamethrowerFuelPolicy.Label(FlamethrowerKindFor(item)))+" - "+fuel.Litres.ToString("0.00",CultureInfo.InvariantCulture)+" / 5 L";
   return details;
  }
  internal static void UpdateFlamethrowerFuelLabel(GameObject item){
   string details=FlamethrowerFuelDetails(item);
   foreach(var fsm in item.GetComponents<PlayMakerFSM>()){
    if(fsm.FsmName!="ItemName")continue;
    foreach(var value in fsm.FsmVariables.StringVariables)if(IsFlamethrowerDamageLabel(value))value.Value=details;
    foreach(var state in fsm.Fsm.States)foreach(var action in state.Actions)foreach(var field in action.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance)){
     var value=field.GetValue(action) as FsmString;if(IsFlamethrowerDamageLabel(value))value.Value=details;
    }
   }
  }
  static bool IsFlamethrowerDamageLabel(FsmString value){return value!=null&&value.Value!=null&&value.Value.TrimStart().StartsWith("Damage -",StringComparison.OrdinalIgnoreCase);}
 }
}
