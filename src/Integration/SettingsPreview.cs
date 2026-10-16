using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  bool settingsHooks,samplingPending;FieldInfo settingsInstance,pendingField;
  Camera settingsCamera;RenderTexture settingsTexture;float nextPreviewRender;
  void InstallSettingsPreview(){
   if(settingsHooks)return;Type ui=AccessTools.TypeByName("Apocasetter.SettingsUI");if(ui==null)return;
   // Settings belong to Apocasetter's native, scaled and tab-aware renderer.
   // Read pending values only for the separately requested placement previews.
   settingsInstance=AccessTools.Field(ui,"Instance");pendingField=AccessTools.Field(ui,"_pending");
   settingsHooks=true;Logger.LogInfo("Weapons Pack uses the native Apocasetter settings renderer.");
  }
  public static void DrawPlacementPreviews(float width){if(ReferenceEquals(instance,null))return;instance.InstallSettingsPreview();float half=(width-16f)*.5f;GUILayout.BeginHorizontal();GUILayout.BeginVertical(GUILayout.Width(half));instance.DrawChainsawPreview();GUILayout.EndVertical();GUILayout.Space(16f);GUILayout.BeginVertical(GUILayout.Width(half));instance.DrawThirdPersonPreview();GUILayout.EndVertical();GUILayout.EndHorizontal();}
  float PreviewValue(ConfigEntry<float> entry){
   if(samplingPending && settingsInstance!=null && pendingField!=null){var ui=settingsInstance.GetValue(null);var pending=ui==null?null:pendingField.GetValue(ui) as IDictionary;if(pending!=null && pending.Contains(entry)){
    string value=Convert.ToString(pending[entry]);float parsed;if(float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed)||float.TryParse(value,out parsed))return parsed;
   }}return entry.Value;
  }
  void DrawChainsawPreview(){
   GUILayout.Label("Weapon holding preview");
   Rect rect=GUILayoutUtility.GetRect(10f,220f,GUILayout.ExpandWidth(true));GUI.Box(rect,GUIContent.none);
   if(!enabledPreview||camera==null||weapon==null){GUI.Label(new Rect(rect.x+10,rect.y+10,rect.width-20,60),"Equip a supported weapon in game, then open these settings.\nThe preview shows the model and both hands.");return;}
   if(Event.current.type==EventType.Repaint && Time.realtimeSinceStartup>=nextPreviewRender){
    nextPreviewRender=Time.realtimeSinceStartup+.05f;
    if(settingsTexture==null){settingsTexture=new RenderTexture(768,432,24);settingsTexture.name="Chainsaw.SettingsPreview";settingsTexture.Create();}
    if(settingsCamera==null){var go=new GameObject("Chainsaw.SettingsCamera");go.transform.SetParent(camera,false);settingsCamera=go.AddComponent<Camera>();settingsCamera.enabled=false;}
    settingsCamera.CopyFrom(camera.GetComponent<Camera>());settingsCamera.enabled=false;settingsCamera.targetTexture=settingsTexture;settingsCamera.aspect=16f/9f;settingsCamera.rect=new Rect(0,0,1,1);
    settingsCamera.transform.SetPositionAndRotation(camera.position,camera.rotation);
    samplingPending=true;
    try{ApplyTargets();foreach(var ik in weapon.GetComponentsInChildren<MonoBehaviour>(false))if(ik.GetType().Name=="FastIKFabric")AccessTools.Method(ik.GetType(),"LateUpdate").Invoke(ik,null);settingsCamera.Render();}
    finally{samplingPending=false;ApplyTargets();}
   }
   if(settingsTexture!=null)GUI.DrawTexture(rect,settingsTexture,ScaleMode.ScaleToFit,false);
  }
 }
}
