using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.UI;

namespace ChainsawPrototype {
 // The native Codex discovers weapon entries when the player equips them.
 // Parent changes also cover taking the chainsaw in the physical pickup hand.
 public sealed class ChainsawCodexDiscovery : MonoBehaviour {
  void OnEnable(){Plugin.CheckChainsawCodexPickup(this);}
  void OnTransformParentChanged(){Plugin.CheckChainsawCodexPickup(this);}
 }
 public sealed class ChainsawCodexTile : MonoBehaviour {
  internal Plugin owner;
  void OnEnable(){if(owner!=null)owner.RefreshChainsawCodexTile();}
 }

 public partial class Plugin {
  bool chainsawCodexDiscovered;
  GameObject chainsawCodexTile;
  RawImage chainsawCodexImage;
  Text chainsawCodexTitle;
  Texture chainsawCodexLocked;
  Texture2D chainsawCodexPhoto;
  PlayMakerFSM nativeChainsawCodex;
  float nextChainsawCodexImageAttempt;
  bool warnedChainsawCodexPhoto;

  void SetupChainsawCodexGuard(){
   harmony.Patch(AccessTools.Method(typeof(GetFsmBool),"OnEnter"),prefix:new HarmonyMethod(typeof(Plugin),nameof(GuardChainsawDonorDiscovery)));
  }
  static bool GuardChainsawDonorDiscovery(GetFsmBool __instance){
   if(ReferenceEquals(instance,null) || __instance.Fsm==null || __instance.Fsm.Name!="Codex" || __instance.Owner==null || __instance.Owner.name!="m16a1" || __instance.variableName==null || __instance.variableName.Value!="weapon_17")return true;
   if(__instance.Owner.GetComponentInParent<Camera>()==null || (instance.ReadHeldChainsaw()==null&&!instance.FlamethrowerSelectedForCodex()) || __instance.storeValue==null)return true;
   // Only this borrowed carrier sees "already found"; its global M16 entry is untouched.
   __instance.storeValue.Value=true;__instance.Finish();return false;
  }

  internal bool GetChainsawCodexDiscovery(){return chainsawCodexDiscovered;}
  internal void RestoreChainsawCodexDiscovery(bool found){
   chainsawCodexDiscovered=found;
   RefreshChainsawCodexTile();
  }
  internal void ResetChainsawCodexDiscovery(){RestoreChainsawCodexDiscovery(false);}

  void PrepareChainsawCodex(GameObject source){
   if(source==null)return;
   // The chainsaw has its own discovery entry; never mark its donor M16 found.
   foreach(var fsm in source.GetComponents<PlayMakerFSM>())
    if(fsm.FsmName=="Codex")UnityEngine.Object.DestroyImmediate(fsm);
   if(source.GetComponent<ChainsawCodexDiscovery>()==null)source.AddComponent<ChainsawCodexDiscovery>();
  }
  internal static void CheckChainsawCodexPickup(ChainsawCodexDiscovery pickup){
   if(ReferenceEquals(instance,null) || pickup==null || pickup.gameObject==instance.template || instance.ChainsawWorldLoading)return;
   if(!pickup.gameObject.scene.IsValid() || !pickup.gameObject.scene.isLoaded)return;
   for(var parent=pickup.transform.parent;parent!=null;parent=parent.parent){
    if(parent.name!="HandItemUse" && parent.name!="PlayerCamera")continue;
    instance.DiscoverChainsawCodex();return;
   }
  }
  void DiscoverChainsawCodex(){
   if(chainsawCodexDiscovered)return;
   chainsawCodexDiscovered=true;
   RefreshChainsawCodexTile();
   FindNativeChainsawCodex();
   if(nativeChainsawCodex!=null)nativeChainsawCodex.SendEvent("Codex");
   Logger.LogInfo("Chainsaw discovered in the game Codex.");
  }
  void TickChainsawCodex(){
   if(template!=null)PrepareChainsawCodex(template);
   InstallChainsawCodexTile();
   if(!chainsawCodexDiscovered && !ChainsawWorldLoading){
    foreach(var item in ChainsawItem.Active){
     if(item==null || item.gameObject==template)continue;
     var discovery=item.GetComponent<ChainsawCodexDiscovery>();
     if(discovery==null){PrepareChainsawCodex(item.gameObject);discovery=item.GetComponent<ChainsawCodexDiscovery>();}
     CheckChainsawCodexPickup(discovery);
     if(chainsawCodexDiscovered)break;
    }
   }
   RefreshChainsawCodexTile();
  }
  void FindNativeChainsawCodex(){
   if(nativeChainsawCodex!=null)return;
   nativeChainsawCodex=Resources.FindObjectsOfTypeAll<PlayMakerFSM>().FirstOrDefault(f=>
    f!=null && f.FsmName=="Codex" && f.gameObject.name=="__GameManager__" && f.gameObject.scene.IsValid() && f.gameObject.scene.isLoaded);
  }
  void LoadChainsawCodexPhoto(){
   if(chainsawCodexPhoto!=null || Time.unscaledTime<nextChainsawCodexImageAttempt)return;
   nextChainsawCodexImageAttempt=Time.unscaledTime+10f;
   string path=Path.Combine(Path.GetDirectoryName(Info.Location),"ChainsawCodex.png");
   try{
    if(!File.Exists(path))throw new FileNotFoundException("The chainsaw Codex photograph is missing",path);
    var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
    if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(path))){UnityEngine.Object.Destroy(texture);throw new IOException("The chainsaw Codex photograph could not be decoded");}
    texture.name="Chainsaw.Codex.Discovered";texture.wrapMode=TextureWrapMode.Clamp;
    texture.hideFlags=HideFlags.DontUnloadUnusedAsset;chainsawCodexPhoto=texture;
   }catch(Exception error){if(!warnedChainsawCodexPhoto){warnedChainsawCodexPhoto=true;Logger.LogWarning(error.Message);}}
  }
  void InstallChainsawCodexTile(){
   if(chainsawCodexTile!=null)return;
   var nativeTile=Resources.FindObjectsOfTypeAll<PlayMakerFSM>().FirstOrDefault(f=>
    f!=null && f.FsmName=="CodexContent" && f.gameObject.name=="weapon 23" && f.gameObject.scene.IsValid() && f.gameObject.scene.isLoaded &&
    f.transform.parent!=null && f.transform.parent.parent!=null && f.transform.parent.parent.name=="Weapons 3");
   if(nativeTile==null)return;
   var originalImage=nativeTile.GetComponent<RawImage>();if(originalImage==null)return;
   chainsawCodexLocked=Resources.FindObjectsOfTypeAll<Texture2D>().FirstOrDefault(t=>t!=null && t.name=="codex_empty");
   if(chainsawCodexLocked==null && originalImage.texture!=null && originalImage.texture.name=="codex_empty")chainsawCodexLocked=originalImage.texture;
   if(chainsawCodexLocked==null)return;
   // Staging keeps the donor's discovery FSM from running on the cloned tile.
   var staging=new GameObject("Chainsaw.Codex.Staging");staging.SetActive(false);
   var clone=UnityEngine.Object.Instantiate(nativeTile.gameObject,staging.transform,false);
   clone.name="weapon chainsaw";
   foreach(var fsm in clone.GetComponentsInChildren<PlayMakerFSM>(true))UnityEngine.Object.DestroyImmediate(fsm);
   chainsawCodexTile=clone;chainsawCodexImage=clone.GetComponent<RawImage>();
   chainsawCodexTitle=clone.GetComponentsInChildren<Text>(true).FirstOrDefault(t=>t.gameObject.name=="text");
   if(chainsawCodexTitle!=null)chainsawCodexTitle.text="chainsaw";
   clone.AddComponent<ChainsawCodexTile>().owner=this;
   clone.transform.SetParent(nativeTile.transform.parent,false);clone.transform.SetAsLastSibling();
   // Native GridLayoutGroup places the extra entry in the next weapon slot.
   var layout=clone.transform.parent as RectTransform;
   if(layout!=null)LayoutRebuilder.MarkLayoutForRebuild(layout);
   clone.SetActive(true);UnityEngine.Object.Destroy(staging);
   RefreshChainsawCodexTile();
   Logger.LogInfo("Chainsaw Codex tile added to native Weapons 3 with the native undiscovered icon.");
  }
  internal void RefreshChainsawCodexTile(){
   if(chainsawCodexImage==null)return;
   LoadChainsawCodexPhoto();
   chainsawCodexImage.texture=chainsawCodexDiscovered && chainsawCodexPhoto!=null?chainsawCodexPhoto:chainsawCodexLocked;
   chainsawCodexImage.color=Color.white;
   if(chainsawCodexTitle!=null)chainsawCodexTitle.gameObject.SetActive(chainsawCodexDiscovered);
  }
 }
}
