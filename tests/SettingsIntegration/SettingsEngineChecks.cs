using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

// Runs only in the disposable integration host. No personal config/save is opened.
[BepInPlugin("weaponspack.local.settingschecks","Local Settings Checks","1.0.0")]
public sealed class SettingsEngineChecks:BaseUnityPlugin {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 string root,variant;int checks;bool finished;Assembly pack,apocasetter;
 Type uiType;object owner,ui,foreignMod;ConfigFile ownConfig,foreignConfig;
 ConfigEntry<float>[] entries;ConfigEntry<float> foreign;Dictionary<ConfigEntryBase,string> pending;
 static object Get(object target,string field){return (target as Type??target.GetType()).GetField(field,All).GetValue(target is Type?null:target);}
 static void Set(object target,string field,object value){(target as Type??target.GetType()).GetField(field,All).SetValue(target is Type?null:target,value);}
 static object Call(object target,string name,params object[] args){try{return (target as Type??target.GetType()).GetMethod(name,All).Invoke(target is Type?null:target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Logger.LogInfo("PASS: "+message);}
 void Finish(Exception error){if(finished)return;finished=true;File.WriteAllText(Path.Combine(root,"result-"+variant+".txt"),(error==null?"PASS: "+checks+" native Apocasetter checks; rendered menu/physical clicking NOT VERIFIED.":"FAIL: "+error)+"\n");if(error!=null)Logger.LogError(error);Application.Quit();}
 void Awake(){
  root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Info.Location),"../../.."));var args=Environment.GetCommandLineArgs();variant=args[Array.IndexOf(args,"--settings-variant")+1];
  try{Prepare();CheckGroups();CheckCompactTabs();Finish(null);}catch(Exception e){Finish(e);}
 }
 void Prepare(){
  // The checker exits before game startup and touches only inert fixture objects.
  foreach(var boot in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())if(boot.gameObject.scene.IsValid()&&boot.Fsm==null)DestroyImmediate(boot);
  pack=Assembly.LoadFrom(Path.Combine(root,"lib",variant,"ChainsawPrototype.dll"));
  var inactive=new GameObject("InactivePackFixture");inactive.SetActive(false);owner=inactive.AddComponent(pack.GetType("ChainsawPrototype.Plugin"));
  ownConfig=((BaseUnityPlugin)owner).Config;ownConfig.Clear();ownConfig.SaveOnConfigSet=false;ownConfig.Bind("General","Apocasetter",true);
  Call(owner,"SetupCombat");Call(owner,"SetupCuttingAttack");Call(owner,"SetupFlameFuelBalance");var ignite=owner.GetType().GetMethod("SetupFlamethrowerIgnite",All);if(ignite!=null)ignite.Invoke(owner,null);Call(owner,"TrimWeaponSettings");
  entries=ownConfig.Select(p=>p.Value).OfType<ConfigEntry<float>>().ToArray();Check(entries.Length==(ignite==null?19:22),"all released weapon balance settings retained");
  Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetType("Apocasetter.SettingsUI",false)!=null),"optional settings mod initially absent");
  Set(owner,"maintenanceJob",0);Set(owner,"nextMaintenance",-1f);Call(owner,"TickWeaponMaintenance");
  apocasetter=Assembly.LoadFrom(Path.Combine(root,"lib","Apocasetter.dll"));uiType=apocasetter.GetType("Apocasetter.SettingsUI",true);
  Set(apocasetter.GetType("Apocasetter.Plugin"),"Log",Logger);
  var installedVersion=(string)apocasetter.GetType("Apocasetter.Plugin").GetField("VERSION",All).GetRawConstantValue();Check(!string.IsNullOrEmpty(installedVersion)&&uiType.GetMethod("DrawSectionTabs",All)!=null&&uiType.GetMethod("DrawSettings",All)!=null,"actual installed Apocasetter "+installedVersion+" native settings API loaded");
  Set(owner,"maintenanceJob",0);Set(owner,"nextMaintenance",-1f);Call(owner,"TickWeaponMaintenance");
  var patch=Harmony.GetPatchInfo(uiType.GetMethod("DrawSettings",All));
  Check(patch!=null&&patch.Owners.Contains("apocalypter.weaponspack.settings"),"production maintenance installs native weapon foldouts");
  Set(owner,"maintenanceJob",0);Set(owner,"nextMaintenance",-1f);Call(owner,"TickWeaponMaintenance");
  Check(Harmony.GetPatchInfo(uiType.GetMethod("DrawSettings",All)).Transpilers.Count(p=>p.owner=="apocalypter.weaponspack.settings")==1,"repeated maintenance does not duplicate hooks");
  foreach(string name in new[]{"DrawPane","DrawAbout","DrawNotes","DrawFiles"}){var info=Harmony.GetPatchInfo(uiType.GetMethod(name,All));Check(info==null||!info.Owners.Contains("apocalypter.weaponspack.settings"),"native "+name+" untouched");}
  foreignConfig=new ConfigFile(Path.Combine(root,"foreign-settings-"+variant+".cfg"),false){SaveOnConfigSet=false};foreign=foreignConfig.Bind("Combat","Damage",1.2f,new ConfigDescription("Foreign numeric setting",new AcceptableValueRange<float>(0f,200f)));
  var uiGo=new GameObject("InactiveNativeSettings");uiGo.SetActive(false);ui=uiGo.AddComponent(uiType);Set(ui,"_sc",1f);Set(ui,"_keyUsers",new Dictionary<string,List<string>>());
  foreignMod=MakeMod(foreignConfig,"foreign.mod",new List<ConfigEntryBase>{foreign});
  pending=(Dictionary<ConfigEntryBase,string>)Get(ui,"_pending");
  CheckDataPaths();
 }
 object MakeMod(ConfigFile config,string guid,List<ConfigEntryBase> list){var mod=Activator.CreateInstance(apocasetter.GetType("Apocasetter.ModEntry"),true);Set(mod,"Config",config);Set(mod,"Guid",guid);Set(mod,"Name",guid);Set(mod,"Version","1.0.0");Set(mod,"OptedIn",true);Set(mod,"Entries",list);return mod;}
 void CheckDataPaths(){
  var normalize=uiType.GetMethod("Normalize",All);var slider=pack.GetType("ChainsawPrototype.PackSettingsUiBridge");int samples=0;bool valid=true;
  foreach(var entry in entries){var range=(AcceptableValueRange<float>)entry.Description.AcceptableValues;for(int i=0;i<=997;i++){float raw=range.MinValue+(range.MaxValue-range.MinValue)*i/997f;float value=(float)Call(slider,"SliderValue",entry,raw);valid&=Math.Abs(value*10-Math.Round(value*10))<.0003&&value>=range.MinValue&&value<=range.MaxValue;samples++;}}
  Check(valid,"all "+samples+" owned slider samples use tenths inside their ranges");
  Check((float)Call(slider,"SliderValue",foreign,1.234567f)==1.234567f,"foreign slider precision preserved");
  Check((string)normalize.Invoke(null,new object[]{foreign,"1,234567"})=="1.234567","foreign normalization preserved");
  foreach(var entry in entries)pending[entry]=((float)entry.DefaultValue+.12345f).ToString("R",CultureInfo.InvariantCulture);
  pending[foreign]="1.234567";Call(ui,"ApplyPending");
  Check(pending.Count==0&&((Dictionary<ConfigEntryBase,string>)Get(ui,"_errors")).Count==0&&entries.All(e=>Math.Abs(e.Value*10-Math.Round(e.Value*10))<.0003)&&foreign.Value==1.234567f,"native pending/apply saves owned tenths and foreign precision");
  var fuel=entries.First(e=>e.Definition.Key.StartsWith("Fuel",StringComparison.Ordinal));Call(ui,"SetSerialized",fuel,"0.234567");Check(fuel.Value==.2f,"native typed fuel edit snaps to a tenth");
  foreach(var entry in entries)Call(ui,"SetNow",entry,entry.DefaultValue);
  var count=ownConfig.Select(p=>p.Value).FirstOrDefault(e=>e.SettingType==typeof(int));if(count!=null){pending[count]="7";Call(ui,"ApplyPending");Check((int)count.BoxedValue==7,"native integer edit remains integral");Call(ui,"SetNow",count,count.DefaultValue);Check((int)count.BoxedValue==5,"native integer reset preserves stack default");}
  Check(entries.All(e=>e.Value==(float)e.DefaultValue),"native reset preserves every released default");
  Check(entries.Single(e=>e.Definition.Section=="Flamethrower - Gasoline"&&e.Definition.Key=="Range").Value==5f&&entries.Single(e=>e.Definition.Section=="Flamethrower - Diesel"&&e.Definition.Key=="Range").Value==7f&&entries.Single(e=>e.Definition.Section=="Flamethrower - Engine oil"&&e.Definition.Key=="Range").Value==10f,"range reset remains 5/7/10 m");
  ownConfig.Save();var reload=new ConfigFile(ownConfig.ConfigFilePath,false);Check(reload.Bind("Combat","Fuel per swing",9f).Value==.1f&&reload.Bind("Combat","Attack rate",9f).Value==.8f,"native saved config reload retains original keys and defaults");
  pending[entries[0]]="4.3";
 }
 void CheckCompactTabs(){
  var bridge=pack.GetType("ChainsawPrototype.PackSettingsUiBridge");
  Check(bridge.GetMethod("PrepareTabs",All)!=null,"owned settings provide three compact category tabs");
  ownConfig.Bind("Flamethrower - Smoke","Enemy smoke",true);
  var original=ownConfig.Select(p=>p.Value).Where(e=>e.Definition.Key!="Apocasetter").ToList();var mod=MakeMod(ownConfig,"apocalypter.weaponspack",original);
  Call(bridge,"PrepareTabs",ui,mod);
  var tabs=(List<string>)Get(ui,"_sections");var sections=(List<KeyValuePair<string,List<ConfigEntryBase>>>)Get(ui,"_sectionEntries");
  Check(tabs.SequenceEqual(new[]{"Weapons","Fuel","Smoke"}),"only Weapons Fuel and Smoke appear in the native tab strip");
  Check(sections.SelectMany(p=>p.Value).Count()==original.Count&&original.All(e=>sections.Any(p=>p.Key==e.Definition.Section&&p.Value.Contains(e))),"compact tabs retain all entries and their original per-fuel section labels");
  Check(!(bool)Call(bridge,"SectionDiffers","Combat","Weapons",mod)&&!(bool)Call(bridge,"SectionDiffers","Chainsaw cutting","Weapons",mod)&&!(bool)Call(bridge,"SectionDiffers","Flamethrower - Ignite","Weapons",mod),"Weapons selects attacks and ignite together");
  Check(!(bool)Call(bridge,"SectionDiffers","Flamethrower - Gasoline","Fuel",mod)&&!(bool)Call(bridge,"SectionDiffers","Flamethrower - Diesel","Fuel",mod)&&!(bool)Call(bridge,"SectionDiffers","Flamethrower - Smoke","Smoke",mod),"fuel types and smoke select their short tabs");
  Set(ui,"_section","Smoke");Call(bridge,"PrepareTabs",ui,mod);Check((string)Get(ui,"_section")=="Smoke","repeated rendering preserves the selected compact tab");
  var before=Get(ui,"_sections");Call(bridge,"PrepareTabs",ui,foreignMod);Check(ReferenceEquals(before,Get(ui,"_sections"))&&(bool)Call(bridge,"SectionDiffers","Combat","Weapons",foreignMod),"foreign mods keep their original native section matching and cache");
  Check(pending.Count==1&&pending[entries[0]]=="4.3","tab navigation preserves pending native setting edits");
  var info=Harmony.GetPatchInfo(uiType.GetMethod("DrawSectionTabs",All));Check(info!=null&&info.Owners.Contains("apocalypter.weaponspack.settings"),"compact tabs are connected to the actual native tab renderer");
 }
 void CheckGroups(){
  var type=pack.GetType("ChainsawPrototype.WeaponSettingsGroups");var groups=Activator.CreateInstance(type,true);int headerCount=0;
  var saw=entries.Where(e=>e.Definition.Section=="Combat").Cast<ConfigEntryBase>().ToList();var cut=entries.Where(e=>e.Definition.Section=="Chainsaw cutting").Cast<ConfigEntryBase>().ToList();
  var flame=ownConfig.Select(p=>p.Value).Where(e=>e.Definition.Section.StartsWith("Flamethrower - ",StringComparison.Ordinal)).GroupBy(e=>e.Definition.Section).Select(g=>g.ToList()).ToArray();
  Func<List<ConfigEntryBase>,string,Func<string,bool,bool>,List<ConfigEntryBase>> select=(list,search,header)=>(List<ConfigEntryBase>)Call(groups,"Select",list,search,header);
  Func<string,bool,bool> clickAll=(name,open)=>{headerCount++;return true;};Call(groups,"Begin");
  bool collapsed=select(saw,"",clickAll).Count==0&&select(cut,"",clickAll).Count==0;
  foreach(var list in flame)collapsed&=select(list,"",clickAll).Count==0;
  Check(collapsed&&headerCount==2,"two foldout selections collapse every weapon section including fuel and ignition");
  Call(groups,"Begin");Check(ReferenceEquals(select(saw,"damage",(name,open)=>true),saw),"search temporarily expands a collapsed weapon");
  Call(groups,"Begin");Check(select(saw,"",(name,open)=>false).Count==0,"clearing search restores collapse state");
  Call(groups,"Begin");Check(ReferenceEquals(select(saw,"",(name,open)=>true),saw)&&ReferenceEquals(select(cut,"",(name,open)=>false),cut),"header click expands every chainsaw section");
  Call(groups,"Begin");bool expanded=true;foreach(var list in flame)expanded&=ReferenceEquals(select(list,"",(name,open)=>true),list);Check(expanded,"header click expands every flamethrower section");
  var bridge=pack.GetType("ChainsawPrototype.PackSettingsUiBridge");var other=new List<ConfigEntryBase>{foreign};Check(ReferenceEquals(Call(bridge,"FilterGroups",other,ui,foreignMod,800f),other),"foreign Combat section returns its exact native panel list");
  Check(pending.Count==1&&pending[entries[0]]=="4.3","collapse/search/expand preserves unsaved native edits");
  Check(ownConfig.ContainsKey(new ConfigDefinition("Combat","Damage"))&&ownConfig.ContainsKey(new ConfigDefinition("Chainsaw cutting","Damage per hit")),"presentation preserves released config keys");
 }
}

