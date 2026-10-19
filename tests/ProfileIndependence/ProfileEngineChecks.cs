using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using HutongGames.PlayMaker;
using UnityEngine;

// Loads the real public readers in an isolated host. The shared tool profiles
// deliberately disagree with the frozen release profiles, then are removed.
[BepInPlugin("weaponspack.local.profilechecks","Local Profile Independence Checks","1.0.0")]
public sealed class ProfileEngineChecks:BaseUnityPlugin {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 string root;int checks,failures;
 static object Get(object target,string name){return (target as Type??target.GetType()).GetField(name,All).GetValue(target is Type?null:target);}
 static void Set(object target,string name,object value){(target as Type??target.GetType()).GetField(name,All).SetValue(target is Type?null:target,value);}
 static object Call(object target,string name,params object[] args){try{return (target as Type??target.GetType()).GetMethod(name,All).Invoke(target is Type?null:target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 void Check(bool ok,string message){checks++;if(ok)Logger.LogInfo("PASS: "+message);else{failures++;Logger.LogWarning("FAIL: "+message);}}
 static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
 [DataContract] sealed class Attack { [DataMember] public string weapon=null,preset=null;[DataMember] public Key[] keys=null; }
 [DataContract] sealed class Key { [DataMember] public float seconds=0f; }
 static Attack ReadAttack(string json){using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))return (Attack)new DataContractJsonSerializer(typeof(Attack)).ReadObject(stream);}
 void Awake(){
  root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Info.Location),"../../.."));
  try{Run();File.WriteAllText(Path.Combine(root,"result.txt"),(failures==0?"PASS: ":"FAIL: ")+checks+" profile independence checks; "+failures+" failures.\n");}
  catch(Exception error){Logger.LogError(error);File.WriteAllText(Path.Combine(root,"result.txt"),"FAIL: "+error);}
  Application.Quit();
 }
 void Run(){
  if(!Path.GetFullPath(Paths.ConfigPath).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Profile fixture config path must stay inside its isolated host.");
  foreach(var boot in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())if(boot.gameObject.scene.IsValid()&&boot.Fsm==null)DestroyImmediate(boot);
  var dll=Path.Combine(root,"lib","candidate","ChainsawPrototype.dll");var pack=Assembly.LoadFrom(dll);var type=pack.GetType("ChainsawPrototype.Plugin");
  var inactive=new GameObject("InactivePack");inactive.SetActive(false);var owner=inactive.AddComponent(type);
  // Chainloader normally supplies this location; the candidate is deliberately
  // loaded as a library instead of starting its complete gameplay plugin.
  var info=((BaseUnityPlugin)owner).Info;info.GetType().GetProperty("Location",All).SetValue(info,dll,null);Set(type,"instance",owner);
  var bundled=Path.Combine(Path.GetDirectoryName(dll),"Defaults");
  string[] relative={"HandArmPlacement/flamethrower.cfg","WeaponMovesets/chainsaw--downwardswing.json","WeaponMovesets/chainsaw--upperleftdiagonalchop.json"};
  string[] hashes={"02C1350C17CB2EC3C1036C9432526259803DAE9A895A309827C338BD7E0D5494","F4E345198883AC89056F7AC7B85ACA6AD07AE10E841039BA59CE0EBEC37B996C","D2AAA9BAC84037F6500B01BE8AF94917CBB28B5E078ADBFC9A3BED76010A391E"};
  var toolPaths=relative.Select(p=>Path.Combine(Paths.ConfigPath,p)).ToArray();
  for(int i=0;i<relative.Length;i++){
   var source=Path.Combine(bundled,relative[i]);Check(Hash(source)==hashes[i],"frozen approved release profile: "+relative[i]);
   Directory.CreateDirectory(Path.GetDirectoryName(toolPaths[i]));
   var draft=File.ReadAllText(source).Replace("flamethrower--Flamethrower.cfg","tool-draft.cfg").Replace("0.15587528","2.15587528").Replace("Downward swing","Tool downward draft").Replace("Upper left diagonal chop","Tool diagonal draft").Replace("\"seconds\":0.48","\"seconds\":3.48");
   File.WriteAllText(toolPaths[i],draft);
  }
  var toolBytes=toolPaths.Select(File.ReadAllBytes).ToArray();
  var own=Path.Combine(Paths.ConfigPath,"ApocalypterWeaponsPack","chainsaw-reference-holding.cfg");Directory.CreateDirectory(Path.GetDirectoryName(own));File.WriteAllText(own,"[Holding motion]\nSway strength = 0.37\n");var ownBytes=File.ReadAllBytes(own);
  Call(owner,"InstallBundledWeaponProfiles");Check(File.ReadAllBytes(own).SequenceEqual(ownBytes),"existing pack-owned chainsaw settings remain untouched");
  for(int i=0;i<toolPaths.Length;i++)Check(File.ReadAllBytes(toolPaths[i]).SequenceEqual(toolBytes[i]),"existing shared tool profile is not modified: "+relative[i]);
  CheckReaders(owner,type,"different shared tool drafts");
  foreach(var path in toolPaths)File.Delete(path);
  Call(owner,"InstallBundledWeaponProfiles");
  foreach(var path in toolPaths)Check(!File.Exists(path),"runtime does not create a shared tool profile: "+Path.GetFileName(path));
  CheckReaders(owner,type,"shared tool profiles absent");
  File.Delete(own);Call(owner,"InstallBundledWeaponProfiles");
  Check(Hash(own)=="3EED8205B64CF05E04ABE657EE59491835AEFE0217AF296720E522BAF6F98DC0","missing pack-owned chainsaw config still installs its approved default");
  for(int i=0;i<relative.Length;i++)Check(Hash(Path.Combine(bundled,relative[i]))==hashes[i],"runtime leaves approved bundled bytes unchanged: "+relative[i]);
  Check(!type.GetCustomAttributes(typeof(BepInDependency),false).Cast<BepInDependency>().Any(d=>d.DependencyGUID=="apocalypter.handarmplacement"),"released plugin has no private Adjuster dependency declaration");
 }
 void CheckReaders(object owner,Type type,string scenario){
  Set(owner,"nextFlamePlacementRead",0f);Set(owner,"flamePlacementStamp",DateTime.MinValue);Call(owner,"ReadFlamethrowerHolding");
  Check((string)Get(owner,"flamePlacementSource")=="flamethrower--Flamethrower.cfg","flamethrower uses approved profile source with "+scenario);
  var placement=Get(owner,"flamePlacement");var models=placement==null?null:(Array)Get(placement,"models");
  Check(models!=null&&models.Length==2&&Math.Abs(((Vector3)Get(models.GetValue(0),"position")).x-.15587528f)<.0000001f,"flamethrower uses approved model placement with "+scenario);
  Call(owner,"SetupOwnedMovesets");var animations=(string[])Call(type,"GetActiveAttackAnimations");
  Check(animations.Length==2,"both owned attacks load with "+scenario);
  if(animations.Length==2){var down=ReadAttack(animations[0]);var diagonal=ReadAttack(animations[1]);
   Check(down.weapon=="chainsaw"&&down.preset=="Downward swing"&&down.keys.Length==3&&Math.Abs(down.keys[0].seconds-.48f)<.000001f,"downward attack ignores tool draft with "+scenario);
   Check(diagonal.weapon=="chainsaw"&&diagonal.preset=="Upper left diagonal chop"&&diagonal.keys.Length==3&&Math.Abs(diagonal.keys[0].seconds-.48f)<.000001f,"diagonal attack ignores tool draft with "+scenario);
  }
 }
}
