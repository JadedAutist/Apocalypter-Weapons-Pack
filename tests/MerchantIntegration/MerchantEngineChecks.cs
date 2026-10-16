using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using UnityEngine;

// Loads the release DLL as inactive components in a separate Unity process.
// Only owned fixture objects and a save file under this host are modified.
[BepInPlugin("weaponspack.local.merchantchecks","Local Merchant Checks","1.0.0")]
public sealed class MerchantEngineChecks:BaseUnityPlugin {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 int checks;string root,variant;
 static object Call(object target,string method,params object[] args){try{return (target as Type??target.GetType()).GetMethod(method,All).Invoke(target is Type?null:target,args);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException??e).Throw();throw;}}
 static void Set(object target,string field,object value){(target as Type??target.GetType()).GetField(field,All).SetValue(target is Type?null:target,value);}
 static object Get(object target,string field){return (target as Type??target.GetType()).GetField(field,All).GetValue(target is Type?null:target);}
 void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Logger.LogInfo("PASS: "+message);}
 void Awake(){
  root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Info.Location),"../../.."));
  var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--merchant-variant");variant=at>=0?args[at+1]:"fixed";
  try{Run();File.WriteAllText(Path.Combine(root,"result-"+variant+".txt"),"PASS: "+checks+" Unity/PlayMaker/Apocapocket checks for "+variant+".\n");}
  catch(Exception e){Logger.LogError(e);File.WriteAllText(Path.Combine(root,"result-"+variant+".txt"),"FAIL: "+e);}
  Application.Quit();
 }
 PlayMakerFSM Fsm(string name){
  var go=new GameObject(name);go.SetActive(false);var f=go.AddComponent<PlayMakerFSM>();f.Reset();f.enabled=false;f.FsmName=name;f.Fsm.Init(f);return f;
 }
 void Run(){
  // Chainloader Awake precedes the main-menu scene's Awake. Remove only its
  // not-yet-initialized scene components from this isolated fixture process.
  foreach(var boot in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())if(boot.gameObject.scene.IsValid()&&boot.Fsm==null)UnityEngine.Object.DestroyImmediate(boot);
  var pack=Assembly.LoadFrom(Path.Combine(root,"lib",variant,"ChainsawPrototype.dll"));var plugin=pack.GetType("ChainsawPrototype.Plugin");
  var go=new GameObject("InactivePack");go.SetActive(false);var owner=go.AddComponent(plugin);
  var parent=new GameObject("WeaponsPack.MerchantRequestData");parent.SetActive(false);
  bool creationFailed=false;GameObject chainsaw=null,flame=null;
  try{chainsaw=(GameObject)Call(owner,"MakeMerchantRequest","Chainsaw",parent.transform);flame=(GameObject)Call(owner,"MakeMerchantRequest","Flamethrower",parent.transform);}
  catch(NullReferenceException){creationFailed=true;}
  Check(creationFailed==(variant=="baseline"),"original failure reproduced or fixed creation succeeds");
  if(!creationFailed){
   foreach(var item in new[]{chainsaw,flame}){
    var fsms=item.GetComponents<PlayMakerFSM>();Check(!item.activeSelf&&fsms.Length==2&&fsms.All(f=>!f.enabled&&f.Fsm!=null),"inactive initialized metadata: "+item.name);
    var label=fsms.Single(f=>f.FsmName=="ItemName").FsmVariables.FindFsmString("ItemName");
    var value=fsms.Single(f=>f.FsmName=="Value").FsmVariables.FindFsmInt("SellValue");
    if(variant=="handoff"){Check(label==null&&value==null,"handoff Name setter issue reproduced");continue;}
    Check(label!=null&&label.Value==(item==chainsaw?"Chainsaw":"Flamethrower"),"native name lookup");
    Check(value!=null&&value.Value==2000,"native sale value lookup");
    Call(pack.GetType("ChainsawPrototype.WeaponMerchantRequests"),"SetOffer",item,2750);Check(value.Value==2750,"merchant offer update uses named value");
   }
  }
  // Exercise Apocapocket's actual BeginSave resource scan. Native player
  // references are represented by initialized, inert fixture FSMs/slots.
  var pocket=Assembly.LoadFrom(Path.Combine(root,"lib","Apocapocket.dll"));
  var pocketPlugin=pocket.GetType("Apocapocket.Plugin");Set(pocketPlugin,"Log",Logger);
  Set(pocketPlugin,"Enabled",Config.Bind("Fixture","Enabled",true));Set(pocketPlugin,"RequireBackpack",Config.Bind("Fixture","RequireBackpack",false));Set(pocketPlugin,"Verbose",Config.Bind("Fixture","Verbose",true));
  var runnerGo=new GameObject("InactivePocket");runnerGo.SetActive(false);var runner=runnerGo.AddComponent(pocket.GetType("Apocapocket.Runner"));Call(runner,"Awake");Set(runner,"Selected",-1);
  var refs=Activator.CreateInstance(pocket.GetType("Apocapocket.Refs"),true);
  var grab=Fsm("Grab");var weapons=Fsm("WeaponInHand");Set(refs,"Grab",grab);Set(refs,"Weapons",weapons);Set(refs,"Hand",grab.transform);Set(refs,"HandItemUse",weapons.transform);
  var slots=Enumerable.Range(1,6).Select(i=>new GameObject("Slot "+i).transform).ToArray();Set(refs,"Slots",slots);Set(runner,"Refs",refs);
  Set(pocket.GetType("Apocapocket.ExtraSlots"),"Staging",new GameObject("PocketStaging").transform);
  Check((bool)runner.GetType().GetProperty("Ready",All).GetValue(runner,null),"native pocket readiness with fixture references");
  string file=Path.Combine(root,"merchant-save-"+variant+".es3");ES3Settings.defaultSettings.path=file;
  ES3Settings.defaultSettings.encryptionType=ES3.EncryptionType.None;ES3Settings.defaultSettings.compressionType=ES3.CompressionType.None;
  var disk=new ES3Settings(file,ES3.Location.File);ES3.Save<int>("FixtureSentinel",42,disk);ES3.CacheFile(disk);
  var persistence=Get(runner,"Save");bool saveFailed=false;
  try{Call(persistence,"BeginSave",runner);}catch(NullReferenceException e){saveFailed=true;Logger.LogInfo("Save probe exception: "+e);}
  Check(saveFailed==(variant=="baseline"),"actual Apocapocket BeginSave scan failure reproduced or prevented");
  if(!saveFailed){
   Check((bool)Get(persistence,"Normalised"),"pocket save normalization entered");
   Call(persistence,"EndSave",runner);Check(!(bool)Get(persistence,"Normalised"),"pocket save normalization ended");
   ES3.StoreCachedFile(new ES3Settings(file,ES3.Location.Cache));
   var json=(string)Get(persistence,"_json");Check((bool)Call(pocket.GetType("Apocapocket.Persistence"),"WriteMapping",file,json),"actual pocket mapping write verifies");
   Check(ES3.Load<int>("FixtureSentinel",disk)==42&&ES3.Load<string>("Apocapocket.Slots",disk)==json,"pocket mapping and prior save key coexist");
  }
 }
}
