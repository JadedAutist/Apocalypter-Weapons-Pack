using System;
using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
namespace ChainsawPrototype {
 public partial class Plugin {
  ConfigFile fixedWeaponValues;
  bool roundingWeaponSetting;
  ConfigEntry<T> FixedSetting<T>(string section,string key,T value,ConfigDescription description=null){
   if(fixedWeaponValues==null)fixedWeaponValues=new ConfigFile(Path.Combine(Paths.ConfigPath,"ApocalypterWeaponsPack","runtime-values.internal"),false){SaveOnConfigSet=false};
   return fixedWeaponValues.Bind(section,key,value,description??new ConfigDescription("Fixed weapon presentation value."));
  }
  ConfigEntry<T> FixedSetting<T>(string section,string key,T value,string description){return FixedSetting(section,key,value,new ConfigDescription(description));}
  void TrimWeaponSettings(){
   Config.SettingChanged-=RoundChangedWeaponSetting;
   foreach(var setting in Config)RoundWeaponSetting(setting.Value as ConfigEntry<float>);
   Config.SettingChanged+=RoundChangedWeaponSetting;
   // Old unbound settings otherwise reappear as BepInEx orphaned config keys.
   var property=AccessTools.Property(typeof(ConfigFile),"OrphanedEntries");
   var orphaned=property==null?null:property.GetValue(Config,null) as IDictionary;if(orphaned!=null)orphaned.Clear();
   Config.Save();
  }
  void RoundChangedWeaponSetting(object sender,SettingChangedEventArgs args){RoundWeaponSetting(args.ChangedSetting as ConfigEntry<float>);}
  void RoundWeaponSetting(ConfigEntry<float> setting){
   if(setting==null||roundingWeaponSetting)return;
   // Player-facing balance settings only; authored placement/animation configs
   // and private presentation values keep their original precision.
   float current=setting.Value;
   float rounded=float.IsNaN(current)||float.IsInfinity(current)?(float)setting.DefaultValue:(float)Math.Round((decimal)current,1,MidpointRounding.AwayFromZero);
   if(current.Equals(rounded))return;
   roundingWeaponSetting=true;
   try{setting.Value=rounded;}finally{roundingWeaponSetting=false;}
  }
 }
}
