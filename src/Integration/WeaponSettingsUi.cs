using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  void InstallWeaponSettingsUi(){PackSettingsUiBridge.Install(Config,message=>Logger.LogWarning(message));}
 }
 internal static class WeaponSettingSteps {
  internal static float Snap(ConfigEntryBase entry,float value){
   if(float.IsNaN(value)||float.IsInfinity(value))value=(float)entry.DefaultValue;
   // Decimal conversion removes float representation noise at ties (.35f),
   // while preserving the intended one-decimal, away-from-zero rounding.
   double rounded=Math.Abs((double)value)<7.9e28?(double)Math.Round((decimal)value,1,MidpointRounding.AwayFromZero):Math.Round((double)value,1,MidpointRounding.AwayFromZero);
   var range=entry.Description.AcceptableValues as AcceptableValueRange<float>;
   if(range!=null){
    double min=Math.Ceiling(Math.Round((double)range.MinValue*10,5))/10;
    double max=Math.Floor(Math.Round((double)range.MaxValue*10,5))/10;
    rounded=Math.Max(min,Math.Min(max,rounded));
   }
   return (float)rounded;
  }
 }
 // Presentation state only: config keys and native catalog entries stay intact.
 internal sealed class WeaponSettingsGroups {
  readonly HashSet<string> collapsed=new HashSet<string>(),seen=new HashSet<string>();
  readonly List<ConfigEntryBase> empty=new List<ConfigEntryBase>();
  internal void Begin(){seen.Clear();}
  internal bool HasVisibleGroups {get{return seen.Count>0;}}
  internal static string Group(string section){
   if(section=="Combat"||section=="Chainsaw cutting")return "Chainsaw";
   if(section.StartsWith("Flamethrower - ",StringComparison.Ordinal))return "Flamethrower";
   return null;
  }
  internal List<ConfigEntryBase> Select(List<ConfigEntryBase> entries,string search,Func<string,bool,bool> drawHeader){
   if(entries.Count==0)return entries;string group=Group(entries[0].Definition.Section);if(group==null)return entries;
   bool searching=!string.IsNullOrWhiteSpace(search),open=searching||!collapsed.Contains(group);
   if(seen.Add(group)&&drawHeader(group,open)&&!searching){if(open)collapsed.Add(group);else collapsed.Remove(group);open=!open;}
   return open?entries:empty;
  }
 }
 internal static class PackSettingsUiBridge {
  const string PatchId="apocalypter.weaponspack.settings";
  static ConfigFile ownConfig;static bool attempted;
  static FieldInfo modConfig,settingSearch,sectionStyle,white;
  static MethodInfo scale,outline;
  static readonly WeaponSettingsGroups groups=new WeaponSettingsGroups();
  internal static bool Install(ConfigFile config,Action<string> report){
   if(attempted)return ownConfig!=null;
   Type ui=null;foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){ui=assembly.GetType("Apocasetter.SettingsUI",false);if(ui!=null)break;}if(ui==null)return false;
   attempted=true;var patch=new Harmony(PatchId);
   try{
    var mod=AccessTools.TypeByName("Apocasetter.ModEntry");var skin=AccessTools.TypeByName("Apocasetter.GameSkin");
    modConfig=AccessTools.Field(mod,"Config");settingSearch=AccessTools.Field(ui,"_settingSearch");
    sectionStyle=AccessTools.Field(skin,"Section");white=AccessTools.Field(skin,"White");scale=AccessTools.Method(ui,"U",new[]{typeof(float)});
    outline=AccessTools.Method(skin,"Out",new[]{typeof(Rect),typeof(string),typeof(GUIStyle),typeof(Color),typeof(float)});
    var draw=AccessTools.Method(ui,"DrawEntry");var settings=AccessTools.Method(ui,"DrawSettings");var normalize=AccessTools.Method(ui,"Normalize",new[]{typeof(ConfigEntryBase),typeof(string)});
    if(modConfig==null||settingSearch==null||sectionStyle==null||white==null||scale==null||outline==null||draw==null||settings==null||normalize==null)throw new MissingMemberException("Apocasetter settings API changed");
    ownConfig=config;
    patch.Patch(draw,transpiler:new HarmonyMethod(typeof(PackSettingsUiBridge),nameof(SliderTranspiler)));
    patch.Patch(normalize,postfix:new HarmonyMethod(typeof(PackSettingsUiBridge),nameof(Normalize)));
    patch.Patch(settings,prefix:new HarmonyMethod(typeof(PackSettingsUiBridge),nameof(BeginGroups)),transpiler:new HarmonyMethod(typeof(PackSettingsUiBridge),nameof(GroupsTranspiler)));
    return true;
   }catch(Exception error){patch.UnpatchSelf();ownConfig=null;report("Weapons Pack settings enhancements unavailable; keeping Apocasetter's original renderer. "+error.Message);return false;}
  }
  internal static bool OwnFloat(ConfigEntryBase entry){return entry!=null&&ReferenceEquals(entry.ConfigFile,ownConfig)&&entry.SettingType==typeof(float);}
  internal static float SliderValue(ConfigEntryBase entry,float value){return OwnFloat(entry)?WeaponSettingSteps.Snap(entry,value):value;}
  static float Slider(Rect rect,float value,float min,float max,GUIStyle track,GUIStyle knob,ConfigEntryBase entry){
   return SliderValue(entry,GUI.HorizontalSlider(rect,value,min,max,track,knob));
  }
  static void Normalize(ConfigEntryBase __0,ref string __result){
   float number;if(OwnFloat(__0)&&float.TryParse(__result,NumberStyles.Float,CultureInfo.InvariantCulture,out number))__result=WeaponSettingSteps.Snap(__0,number).ToString("0.#",CultureInfo.InvariantCulture);
  }
  static bool OwnMod(object mod){return mod!=null&&ownConfig!=null&&ReferenceEquals(modConfig.GetValue(mod),ownConfig);}
  static string EmptyMessage(string native,object mod){return OwnMod(mod)&&groups.HasVisibleGroups?"Expand a weapon to view its settings.":native;}
  static void BeginGroups(object __0){if(OwnMod(__0))groups.Begin();}
  static List<ConfigEntryBase> FilterGroups(List<ConfigEntryBase> entries,object ui,object mod,float width){
   if(!OwnMod(mod))return entries;
   string search=settingSearch.GetValue(ui) as string;
   return groups.Select(entries,search,(title,open)=>DrawHeader(ui,width,title,open));
  }
  static bool DrawHeader(object ui,float width,string title,bool open){
   float spacing=(float)scale.Invoke(ui,new object[]{12f}),height=(float)scale.Invoke(ui,new object[]{30f});
   GUILayout.Space(spacing);var rect=GUILayoutUtility.GetRect(width,height,GUILayout.Width(width),GUILayout.Height(height));
   var style=(GUIStyle)sectionStyle.GetValue(null);var color=(Color)white.GetValue(null);
   outline.Invoke(null,new object[]{rect,(open?"▼ ":"▶ ")+title.ToUpperInvariant(),style,color,1f});
   return GUI.Button(rect,new GUIContent("",open?"Collapse "+title:"Expand "+title),GUIStyle.none);
  }
  static IEnumerable<CodeInstruction> SliderTranspiler(IEnumerable<CodeInstruction> source){
   var code=source.ToList();var slider=AccessTools.Method(typeof(GUI),"HorizontalSlider",new[]{typeof(Rect),typeof(float),typeof(float),typeof(float),typeof(GUIStyle),typeof(GUIStyle)});
   if(code.Count(c=>c.Calls(slider))!=1)throw new InvalidOperationException("Expected one native numeric slider");
   foreach(var instruction in code){
    if(instruction.Calls(slider)){
     var entry=new CodeInstruction(OpCodes.Ldarg_2);entry.labels.AddRange(instruction.labels);instruction.labels.Clear();entry.blocks.AddRange(instruction.blocks);instruction.blocks.Clear();yield return entry;
     yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(PackSettingsUiBridge),nameof(Slider)));
    }else yield return instruction;
   }
  }
  static IEnumerable<CodeInstruction> GroupsTranspiler(IEnumerable<CodeInstruction> source){
   var code=source.ToList();
   Func<CodeInstruction,bool> match=c=>{var method=c.operand as MethodInfo;return method!=null&&method.DeclaringType==typeof(Enumerable)&&method.Name=="ToList"&&method.IsGenericMethod&&method.GetGenericArguments()[0]==typeof(ConfigEntryBase);};
   if(code.Count(match)!=1||code.Count(c=>c.opcode==OpCodes.Ldstr&&(string)c.operand=="No setting matches.")!=1)throw new InvalidOperationException("Expected native filtered settings list and empty-state message");
   foreach(var instruction in code){yield return instruction;if(match(instruction)){
    yield return new CodeInstruction(OpCodes.Ldarg_0);yield return new CodeInstruction(OpCodes.Ldarg_1);yield return new CodeInstruction(OpCodes.Ldarg_3);
    yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(PackSettingsUiBridge),nameof(FilterGroups)));
   }else if(instruction.opcode==OpCodes.Ldstr&&(string)instruction.operand=="No setting matches."){
    yield return new CodeInstruction(OpCodes.Ldarg_1);yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(PackSettingsUiBridge),nameof(EmptyMessage)));
   }}
  }
 }
}

