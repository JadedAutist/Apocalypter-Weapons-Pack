using System.Diagnostics;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  int maintenanceJob;float nextMaintenance,nextMaintenanceWarning;
  void TickWeaponMaintenance(){
   if(Time.unscaledTime<nextMaintenance)return;nextMaintenance=Time.unscaledTime+.1f;
   int job=maintenanceJob;maintenanceJob=(maintenanceJob+1)%11;
   long started=Stopwatch.GetTimestamp();
   switch(job){
    case 0:InstallBodyHook();break;
    case 1:CreateTemplate();break;
    case 2:TickChainsawPersistence();break;
    case 3:TickChainsawCodex();break;
    case 4:TickChainsawWorldSpawns();break;
    case 5:CreateFlamethrower();break;
    case 6:TickFlamethrowerCodex();break;
    case 7:TickFlamethrowerFuelPersistence();break;
    case 8:TickFlamethrowerWorldSpawns();break;
    case 9:if(!gameAttacking)ScanMachetePhysics();break;
    case 10:TickWeaponMerchantStock();break;
   }
   double elapsed=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;
   if(elapsed>=6&&Time.unscaledTime>=nextMaintenanceWarning){nextMaintenanceWarning=Time.unscaledTime+10f;Logger.LogInfo("Weapons Pack maintenance job "+job+" took "+elapsed.ToString("0.0")+" ms.");}
  }
 }
}
