using System;
namespace ChainsawPrototype {
 internal static class WeaponSettingTabs {
  internal static readonly string[] Order={"Weapons","Fuel","Smoke"};
  internal static string Category(string section){
   if(string.Equals(section,"Flamethrower - Smoke",StringComparison.Ordinal))return "Smoke";
   if(section=="Flamethrower - Gasoline"||section=="Flamethrower - Diesel"||section=="Flamethrower - Engine oil"||section=="Flamethrower - Fuel patches")return "Fuel";
   return "Weapons";
  }
 }
}
