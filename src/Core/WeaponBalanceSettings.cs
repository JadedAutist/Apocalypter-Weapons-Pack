using System;
using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
namespace ChainsawPrototype {
 public partial class Plugin {
  ConfigFile fixedWeaponValues;
  ConfigEntry<T> FixedSetting<T>(string section,string key,T value,ConfigDescription description=null){
   if(fixedWeaponValues==null)fixedWeaponValues=new ConfigFile(Path.Combine(Paths.ConfigPath,"ApocalypterWeaponsPack","runtime-values.internal"),false){SaveOnConfigSet=false};
   return fixedWeaponValues.Bind(section,key,value,description??new ConfigDescription("Fixed weapon presentation value."));
  }
  ConfigEntry<T> FixedSetting<T>(string section,string key,T value,string description){return FixedSetting(section,key,value,new ConfigDescription(description));}
  void TrimWeaponSettings(){
   // Old unbound settings otherwise reappear as BepInEx orphaned config keys.
   var property=AccessTools.Property(typeof(ConfigFile),"OrphanedEntries");
   var orphaned=property==null?null:property.GetValue(Config,null) as IDictionary;if(orphaned!=null)orphaned.Clear();
   Config.Save();
  }
 }
}
