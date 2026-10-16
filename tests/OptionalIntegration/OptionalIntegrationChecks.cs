using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

// Runs before scene startup in a disposable Unity host. Companion DLLs are
// loaded for their real types, without starting their plugins or opening saves.
[BepInPlugin("weaponspack.local.optionalchecks","Local Optional Integration Checks","1.0.0")]
public sealed class OptionalIntegrationChecks:BaseUnityPlugin {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 int checks;string root,variant,timing;
 void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Logger.LogInfo("PASS: "+label);}
 void Awake(){
  root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Info.Location),"../../.."));
  var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--optional-variant");variant=args[at+1];
  try{Run();File.WriteAllText(Path.Combine(root,"result-"+variant+".txt"),"PASS: "+checks+" optional integration checks for "+variant+".\n"+timing+"\n");}
  catch(Exception e){Logger.LogError(e);File.WriteAllText(Path.Combine(root,"result-"+variant+".txt"),"FAIL: "+e);}
  Application.Quit();
 }
 void Run(){
  var pack=Assembly.LoadFrom(Path.Combine(root,"lib",variant,"ChainsawPrototype.dll"));
  var plugin=pack.GetType("ChainsawPrototype.Plugin");var go=new GameObject("InactivePack");go.SetActive(false);var owner=go.AddComponent(plugin);
  var third=(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),owner,plugin.GetMethod("ActualThirdPersonActive",All));
  Check(!third(),"absent player-body mod keeps first-person mode");
  int iterations=variant=="baseline"?100:10000;var timer=Stopwatch.StartNew();
  for(int i=0;i<iterations;i++)if(third())throw new Exception("Absent third person became active");
  timer.Stop();timing="PERFORMANCE: "+iterations+" absent-mod view checks in "+timer.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms";Logger.LogInfo(timing);
  if(variant=="baseline")return; // Intentional slow/log-spam negative control.
  var helper=pack.GetType("ChainsawPrototype.OptionalWeaponTypes");var find=helper.GetMethod("Find",All);
  Func<string,Type> resolve=name=>(Type)find.Invoke(null,new object[]{name});
  var names=new[]{"Apocaplayer.ThirdPerson","Apocaplayer.Body","Apocaplayer.Runner","WildWasteland.Modules.CleanHUD.Assets"};
  foreach(var name in names)Check(resolve(name)==null,"optional type absent: "+name);
  var cache=(IDictionary)helper.GetField("cache",All).GetValue(null);var miss=cache[names[0]];
  for(int i=0;i<10000;i++)third();
  Check(ReferenceEquals(miss,cache[names[0]]),"10000 further view checks reuse the cached miss");
  Check(resolve("UnityEngine.GameObject")==typeof(GameObject),"exact type resolution finds a real engine type");
  var bodyAssembly=Assembly.LoadFrom(Path.Combine(root,"lib","Apocaplayer.dll"));
  foreach(var name in new[]{names[0],names[1],names[2]})Check(resolve(name)==bodyAssembly.GetType(name,true),"previous miss resolves after real companion assembly loads: "+name);
  var view=resolve(names[0]);var on=view.GetField("On",All);var hasView=view.GetField("HasView",All);
  Check(on!=null&&hasView!=null,"real companion third-person flags are available");
  var oldOn=on.GetValue(null);var oldView=hasView.GetValue(null);
  try{
   on.SetValue(null,false);hasView.SetValue(null,true);Check(!third(),"disabled third person remains first person");
   on.SetValue(null,true);hasView.SetValue(null,false);Check(!third(),"missing third-person view remains first person");
   hasView.SetValue(null,true);Check(third(),"valid third-person view is still detected");
   on.SetValue(null,false);Check(!third(),"live mode toggle is still detected");
  }finally{on.SetValue(null,oldOn);hasView.SetValue(null,oldView);}
  var wild=Assembly.LoadFrom(Path.Combine(root,"lib","WildWasteland.dll"));
  Check(resolve(names[3])==wild.GetType(names[3],true),"real WildWasteland HUD type resolves after a cached miss");
 }
}
