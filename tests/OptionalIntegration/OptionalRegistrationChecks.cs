using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

// Exercises production registration retries before a scene starts. Ready inactive
// templates isolate optional integration from weapon creation and gameplay.
[BepInPlugin("weaponspack.local.registrationchecks","Local Optional Registration Checks","1.0.0")]
public sealed class OptionalRegistrationChecks:BaseUnityPlugin {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 sealed class Warnings:ILogListener {
  internal readonly List<string> Missing=new List<string>();
  public void LogEvent(object sender,LogEventArgs args){
   string message=Convert.ToString(args.Data);
   if(message.Contains("AccessTools.TypeByName: Could not find type named"))Missing.Add(message);
  }
  public void Dispose(){}
 }
 int checks;
 void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Logger.LogInfo("PASS: "+label);}
 string Argument(string name,bool required=true){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,name);if(at<0||at+1==args.Length){if(required)throw new Exception("Missing argument: "+name);return null;}return args[at+1];}
 void Awake(){
  string report=Argument("--registration-report");
  try{Run();File.WriteAllText(report,"PASS: "+checks+" optional registration checks.\n");}
  catch(Exception error){Logger.LogError(error);File.WriteAllText(report,"FAIL: "+error+"\n");}
  Application.Quit();
 }
 void Run(){
  var pack=Assembly.LoadFrom(Argument("--registration-pack"));
  var plugin=pack.GetType("ChainsawPrototype.Plugin",true);
  var helper=pack.GetType("ChainsawPrototype.OptionalWeaponTypes",true);
  var find=helper.GetMethod("Find",All);
  Func<string,Type> resolve=name=>(Type)find.Invoke(null,new object[]{name});
  const string inspector="ApocalypterInspector.ItemSpawner";
  string extraType=Argument("--registration-extra-type",false);
  Check(resolve(inspector)==null,"Inspector is absent before registration retries");
  var ownerObject=new GameObject("InactiveOptionalRegistrationFixture");ownerObject.SetActive(false);var owner=ownerObject.AddComponent(plugin);
  var template=new GameObject("ReadyChainsawFixture");template.SetActive(false);
  var flame=new GameObject("ReadyFlamethrowerFixture");flame.SetActive(false);
  plugin.GetField("template",All).SetValue(owner,template);
  plugin.GetField("templateReady",All).SetValue(owner,true);
  plugin.GetField("flameTemplate",All).SetValue(owner,flame);
  var retries=new List<MethodInfo>{plugin.GetMethod("CreateTemplate",All),plugin.GetMethod("CreateFlamethrower",All)};
  string extraMethods=Argument("--registration-extra-methods",false),extraFields=Argument("--registration-extra-template-fields",false);
  if(extraMethods!=null)foreach(var name in extraMethods.Split(',')){
   var method=plugin.GetMethod(name,All);if(method==null)throw new Exception("Missing extra retry method: "+name);retries.Add(method);
  }
  var extraTemplates=new Dictionary<FieldInfo,GameObject>();
  if(extraFields!=null)foreach(var name in extraFields.Split(',')){
   var field=plugin.GetField(name,All);if(field==null)throw new Exception("Missing extra template field: "+name);
   var item=new GameObject("ReadyExtraOptionalFixture");item.SetActive(false);field.SetValue(owner,item);extraTemplates.Add(field,item);
  }
  var warnings=new Warnings();BepInEx.Logging.Logger.Listeners.Add(warnings);
  try{
   for(int i=0;i<100;i++)foreach(var retry in retries)retry.Invoke(owner,null);
  }finally{BepInEx.Logging.Logger.Listeners.Remove(warnings);}
  Check(warnings.Missing.Count==0,"100 production registration retries emit no optional missing-type warnings (actual "+warnings.Missing.Count+")");
  Check(!(bool)plugin.GetField("registered",All).GetValue(owner),"missing Inspector leaves chainsaw registration pending");
  Check(!(bool)plugin.GetField("flameRegistered",All).GetValue(owner),"missing Inspector leaves flamethrower registration pending");
  var cache=(IDictionary)helper.GetField("cache",All).GetValue(null);var miss=cache[inspector];
  Check(miss!=null,"registration records an absent Inspector lookup");
  for(int i=0;i<10000;i++)foreach(var retry in retries)retry.Invoke(owner,null);
  Check(ReferenceEquals(miss,cache[inspector]),"10000 additional registration retries reuse the absent Inspector lookup");
  if(extraType!=null){
   var extraMiss=cache[extraType];Check(extraMiss!=null,"extra integration records an absent optional type lookup");
   for(int i=0;i<10000;i++)foreach(var retry in retries)retry.Invoke(owner,null);
   Check(ReferenceEquals(extraMiss,cache[extraType]),"10000 additional retries reuse the extra absent optional type lookup");
  }
  Check(resolve("UnityEngine.GameObject")==typeof(GameObject),"exact lookup still resolves a present engine type");
  Check(resolve("GameObject")==null,"short type names do not match unrelated optional integrations");
  // Loading the real DLL for reflection does not start the Inspector plugin.
  var assembly=Assembly.LoadFrom(Argument("--registration-inspector"));
  Check(resolve(inspector)==assembly.GetType(inspector,true),"a cached miss resolves the real Inspector type loaded later");
  Check(ReferenceEquals(template,plugin.GetField("template",All).GetValue(owner))&&ReferenceEquals(flame,plugin.GetField("flameTemplate",All).GetValue(owner)),"optional registration retries preserve the ready weapon templates");
  foreach(var pair in extraTemplates)Check(ReferenceEquals(pair.Value,pair.Key.GetValue(owner)),"optional retries preserve the extra ready template");
 }
}
