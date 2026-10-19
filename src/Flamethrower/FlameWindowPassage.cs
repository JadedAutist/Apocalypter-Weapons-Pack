using System;
using UnityEngine;
namespace ChainsawPrototype {
 // Used only by the pack's flame queries. Other projectiles, world colliders
 // and the global layer matrix remain under the game/other mods' control.
 internal static class FlameWindowPassage {
  static readonly string[] DoorPanes={"door_car_1_L","door_car_1_R","door_truck_1_L","door_truck_1_R","door_truck_2_L","door_truck_2_R"};
  static bool NativePart(string name,string part){return name==part||name.StartsWith(part+"(",StringComparison.Ordinal);}
  internal static bool PassThrough(Collider surface){
   if(surface==null||surface.gameObject.layer!=16||!(surface is BoxCollider))return false;
   bool glass=false,vehicle=false,doorPane=false;
   for(var node=surface.transform;node!=null;node=node.parent){
    // The optional metal grille shares SeeTrough with glass panes.
    if(NativePart(node.name,"windshield_2_grid"))return false;
    if(NativePart(node.name,"windshield_1_glass"))glass=true;
    foreach(string part in DoorPanes)if(NativePart(node.name,part))doorPane=true;
    if(node==surface.transform&&string.Equals(node.name,"collider_SeeTrough",StringComparison.OrdinalIgnoreCase))glass=true;
    if(node.gameObject.CompareTag("Vehicle"))vehicle=true;
   }
   return (glass&&vehicle)||doorPane;
  }
 }
}
