using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  float swingFuelCost,swingFuelBudget,swingFuelSpent;
  void BeginSwingFuel(){
   swingFuelCost=CurrentChainsawFuelCost;swingFuelSpent=0f;
   swingFuelBudget=heldFuel==null?0f:Mathf.Min(heldFuel.Litres,swingFuelCost);
  }
  void MeterSwingFuel(float motionTime){
   if(!swinging||heldFuel==null||swingFuelCost<=0f)return;
   // Drain during the saved windup. The hit is funded by contact time, so
   // cancelling cannot deliver damage before its fuel has been paid.
   float target=Mathf.Min(swingFuelBudget,swingFuelCost*Mathf.Clamp01(motionTime/hitStart));
   float spent=heldFuel.ConsumeContinuous(Mathf.Max(0f,target-swingFuelSpent));
   swingFuelSpent+=spent;if(spent>0f)NoteEmptyFuelDebit(true,heldFuel,swingFuelCost);
  }
  float SwingFuelDamageScale {get{return swingFuelCost>0f?Mathf.Clamp01(swingFuelSpent/swingFuelCost):0f;}}
 }
}
