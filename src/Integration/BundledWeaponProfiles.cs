using System.IO;
using BepInEx;
namespace ChainsawPrototype {
 public partial class Plugin {
  void InstallBundledWeaponProfiles(){
   string defaults=Path.Combine(Path.GetDirectoryName(Info.Location),"Defaults");
   foreach(string relative in new[]{"ApocalypterWeaponsPack/chainsaw-reference-holding.cfg"}){
    string source=Path.Combine(defaults,relative),target=Path.Combine(Paths.ConfigPath,relative);
    if(File.Exists(target))continue;
    if(!File.Exists(source))throw new FileNotFoundException("Weapons Pack is missing its bundled holding/attack profile.",source);
    Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(source,target,false);
   }
  }
 }
}
