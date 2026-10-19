using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

// Uses Unity's native objects, the production maintenance entry point and the
// supplied real WildWasteland DLL. Companion plugins and player saves never run.
[BepInPlugin("weaponspack.local.hudiconchecks","Local HUD Icon Checks","1.0.0")]
public sealed class HudIconChecks:BaseUnityPlugin {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 sealed class Warnings:ILogListener {
  internal readonly List<string> Missing=new List<string>();
  public void LogEvent(object sender,LogEventArgs args){string message=Convert.ToString(args.Data);if(message.Contains("AccessTools.TypeByName: Could not find type named"))Missing.Add(message);}
  public void Dispose(){}
 }
 int checks;Type plugin;object owner;
 void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Logger.LogInfo("PASS: "+label);}
 string Argument(string name){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,name);if(at<0||at+1==args.Length)throw new Exception("Missing argument: "+name);return args[at+1];}
 void Awake(){
  string report=Argument("--hud-report");
  try{Run();File.WriteAllText(report,"PASS: "+checks+" native HUD icon checks.\n");}
  catch(Exception error){Logger.LogError(error);File.WriteAllText(report,"FAIL: "+error+"\n");}
  Application.Quit();
 }
 void Maintenance(){
  plugin.GetField("maintenanceJob",All).SetValue(owner,0);plugin.GetField("nextMaintenance",All).SetValue(owner,0f);
  var next=plugin.GetField("nextPackHudCheck",All);if(next!=null)next.SetValue(owner,0f);
  plugin.GetMethod("TickWeaponMaintenance",All).Invoke(owner,null);
 }
 Texture2D LoadIcon(string name){var icon=new Texture2D(2,2);Check(ImageConversion.LoadImage(icon,File.ReadAllBytes(Path.Combine(Argument("--hud-assets"),name))),"pack PNG decodes: "+name);return icon;}
 GameObject Item(Assembly pack,string marker,string label,string key,Texture2D icon){
  var item=new GameObject(label);item.SetActive(false);item.AddComponent(pack.GetType("ChainsawPrototype."+marker,true));
  // The local development branch already has a separate identity component.
  var identity=pack.GetType("ChainsawPrototype.PackWeaponIdentity");if(identity!=null){var value=item.AddComponent(identity);identity.GetField("Key").SetValue(value,key);identity.GetField("Icon").SetValue(value,icon);}
  return item;
 }
 void Run(){
  var pack=Assembly.LoadFrom(Argument("--hud-pack"));plugin=pack.GetType("ChainsawPrototype.Plugin",true);
  var inactive=new GameObject("InactivePackHudFixture");inactive.SetActive(false);owner=inactive.AddComponent(plugin);
  plugin.GetField("instance",All).SetValue(null,owner);plugin.GetField("harmony",All).SetValue(owner,new Harmony("weaponspack.local.hudiconchecks.bridge"));
  var sawIcon=LoadIcon("Chainsaw-icon.png");var flameIcon=LoadIcon("Flamethrower/icon.png");
  plugin.GetField("chainsawIcon",All).SetValue(owner,sawIcon);plugin.GetField("flameIcon",All).SetValue(owner,flameIcon);
  Check(AppDomain.CurrentDomain.GetAssemblies().Length>0,"native host is running");
  var warnings=new Warnings();BepInEx.Logging.Logger.Listeners.Add(warnings);
  try{for(int i=0;i<100;i++)Maintenance();}finally{BepInEx.Logging.Logger.Listeners.Remove(warnings);}
  Check(warnings.Missing.Count==0,"100 maintenance retries with WildWasteland absent stay quiet");
  var wild=Assembly.LoadFrom(Argument("--hud-wild"));
  Logger.LogInfo("REAL COMPANION: "+wild.FullName);
  var assets=wild.GetType("WildWasteland.Modules.CleanHUD.Assets",true);
  var weaponIcon=assets.GetMethod("WeaponIcon",All);var keyOf=assets.GetMethod("KeyOf",All);
  Func<GameObject,Texture2D> icon=item=>(Texture2D)weaponIcon.Invoke(null,new object[]{item});
  var rendered=(IDictionary)assets.GetField("Rendered",All).GetValue(null);var embedded=(IDictionary)assets.GetField("Embedded",All).GetValue(null);
  var queue=assets.GetField("IconQueue",All).GetValue(null);var queueCount=queue.GetType().GetProperty("Count");
  var saw=Item(pack,"ChainsawItem","Chainsaw(Clone)(Clone)","chainsaw",sawIcon);
  var flame=Item(pack,"FlamethrowerItem","Flamethrower(Clone)","flamethrower",flameIcon);
  // The real unpatched lookup ignores the pack's PNGs and queues mesh rendering.
  Check(icon(saw)!=sawIcon,"negative control: unbridged real CleanHUD does not read the pack inventory image");
  queue.GetType().GetMethod("Clear").Invoke(queue,null);
  Maintenance();
  Check(icon(saw)==sawIcon,"real CleanHUD WeaponIcon returns the chainsaw inventory image after production maintenance");
  Check(icon(flame)==flameIcon,"real CleanHUD WeaponIcon returns the flamethrower inventory image");
  Check((int)queueCount.GetValue(queue,null)==0,"owned weapons bypass CleanHUD mesh rendering");
  var donor=new Texture2D(3,3);donor.name="WrongDonorSilhouette";
  string sawKey=(string)keyOf.Invoke(null,new object[]{saw}),flameKey=(string)keyOf.Invoke(null,new object[]{flame});
  rendered[sawKey]=donor;embedded[flameKey]=donor;
  Check(icon(saw)==sawIcon,"chainsaw image overrides an already cached rendered silhouette");
  Check(icon(flame)==flameIcon,"flamethrower image overrides a same-key embedded texture");
  var child=new GameObject("PickupVisual");child.transform.SetParent(saw.transform,false);
  Check(icon(child)==sawIcon,"inactive owned pickup children retain the pack image");
  var clone=UnityEngine.Object.Instantiate(saw);Check(icon(clone)==sawIcon,"cloned saved weapon marker retains the pack image");
  var foreign=new GameObject("Chainsaw");rendered["Chainsaw"]=donor;
  Check(icon(foreign)==donor,"foreign item with the pack display name keeps CleanHUD's own cached image");
  var stock=new GameObject("m16a1");embedded["m16a1"]=donor;
  Check(icon(stock)==donor,"stock weapon embedded image remains unchanged");
  Check(icon(null)==null,"null item retains CleanHUD's native null result");
  var unknown=new GameObject("UnrelatedHudFixture");Check(icon(unknown)==null,"unrelated uncached item keeps CleanHUD's native pending result");
  Check((int)queueCount.GetValue(queue,null)==1,"only the unrelated uncached item enters the native render queue");
  var patches=Harmony.GetPatchInfo(weaponIcon);int owned=0;
  foreach(var prefix in patches.Prefixes)if(prefix.owner=="weaponspack.local.hudiconchecks.bridge")owned++;
  for(int i=0;i<100;i++)Maintenance();
  int repeated=0;foreach(var prefix in Harmony.GetPatchInfo(weaponIcon).Prefixes)if(prefix.owner=="weaponspack.local.hudiconchecks.bridge")repeated++;
  Check(owned==1&&repeated==1,"repeated maintenance installs exactly one owned icon prefix");
  // The pack's persistent runner retains its managed owner after the game's
  // original Unity plugin component is destroyed. The HUD must do the same.
  UnityEngine.Object.DestroyImmediate((Component)owner);
  Check((UnityEngine.Object)owner==null&&!ReferenceEquals(owner,null),"fixture models destroyed native host with retained managed owner");
  Check(icon(saw)==sawIcon&&icon(flame)==flameIcon,"owned icons survive native plugin host teardown");
 }
}
