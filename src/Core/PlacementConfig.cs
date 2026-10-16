using System.IO;
using BepInEx;
using BepInEx.Configuration;
namespace ChainsawPrototype {
 public partial class Plugin {
  ConfigFile placementConfig;
  public static void ReloadPlacementSettings(){if(ReferenceEquals(instance,null))return;instance.placementConfig.Reload();RefreshAnimationRig();}
  void SetupPlacementConfig(){
   var folder=Path.Combine(Paths.ConfigPath,"ApocalypterWeaponsPack");Directory.CreateDirectory(folder);
   placementConfig=new ConfigFile(Path.Combine(folder,"chainsaw-reference-holding.cfg"),true);
  }
 }
}
