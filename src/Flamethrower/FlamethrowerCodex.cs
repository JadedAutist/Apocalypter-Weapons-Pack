using System;
using System.IO;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.UI;

namespace ChainsawPrototype {
 // The native Codex discovers weapon entries when the player equips them.
 // Parent changes also cover taking the flamethrower in the physical pickup hand.
 public sealed class FlamethrowerCodexDiscovery : MonoBehaviour {
  void OnEnable(){Plugin.CheckFlamethrowerCodexPickup(this);}
  void OnTransformParentChanged(){Plugin.CheckFlamethrowerCodexPickup(this);}
 }
 public sealed class FlamethrowerCodexTile : MonoBehaviour {
  internal Plugin owner;
  void OnEnable(){if(owner!=null)owner.RefreshFlamethrowerCodexTile();}
 }

 public partial class Plugin {
  const string FlamethrowerCodexSaveKey="ApocalypterWeaponsPack.FlamethrowerCodex";
  bool flamethrowerCodexDiscovered;
  GameObject flamethrowerCodexTile;
  RawImage flamethrowerCodexImage;
  Text flamethrowerCodexTitle;
  Texture flamethrowerCodexLocked;
  Texture2D flamethrowerCodexPhoto;
  PlayMakerFSM nativeFlamethrowerCodex;
  float nextFlamethrowerCodexImageAttempt;
  bool warnedFlamethrowerCodexPhoto;

  void SaveFlamethrowerCodexDiscovery(ES3Settings settings){try{ES3.Save<bool>(FlamethrowerCodexSaveKey,flamethrowerCodexDiscovered,settings);}catch(Exception error){Logger.LogWarning("Flamethrower Codex state could not be saved: "+error.Message);}}
  void LoadFlamethrowerCodexDiscovery(ES3Settings settings){bool found=false;try{found=ES3.KeyExists(FlamethrowerCodexSaveKey,settings)&&ES3.Load<bool>(FlamethrowerCodexSaveKey,settings);}catch(Exception error){Logger.LogWarning("Flamethrower Codex state could not be loaded: "+error.Message);}RestoreFlamethrowerCodexDiscovery(found);}
  bool FlamethrowerSelectedForCodex(){
   var player=flamePlayer==null?GameObject.Find("PlayerCamera"):flamePlayer;if(player==null)return false;
   var held=player.GetComponentsInChildren<PlayMakerFSM>(true).FirstOrDefault(f=>f.FsmName=="WeaponInHand");
   var item=held==null?null:held.FsmVariables.FindFsmGameObject("Item")??held.FsmVariables.FindFsmGameObject("item");
   return item!=null&&item.Value!=null&&item.Value.GetComponent<FlamethrowerItem>()!=null;
  }

  internal bool GetFlamethrowerCodexDiscovery(){return flamethrowerCodexDiscovered;}
  internal void RestoreFlamethrowerCodexDiscovery(bool found){
   flamethrowerCodexDiscovered=found;
   RefreshFlamethrowerCodexTile();
  }
  internal void ResetFlamethrowerCodexDiscovery(){RestoreFlamethrowerCodexDiscovery(false);}

  void PrepareFlamethrowerCodex(GameObject source){
   if(source==null)return;
   // The flamethrower has its own discovery entry; never mark its donor M16 found.
   foreach(var fsm in source.GetComponents<PlayMakerFSM>())
    if(fsm.FsmName=="Codex")UnityEngine.Object.DestroyImmediate(fsm);
   if(source.GetComponent<FlamethrowerCodexDiscovery>()==null)source.AddComponent<FlamethrowerCodexDiscovery>();
  }
  internal static void CheckFlamethrowerCodexPickup(FlamethrowerCodexDiscovery pickup){
   if(ReferenceEquals(instance,null) || pickup==null || pickup.gameObject==instance.flameTemplate || instance.ChainsawWorldLoading)return;
   if(!pickup.gameObject.scene.IsValid() || !pickup.gameObject.scene.isLoaded)return;
   for(var parent=pickup.transform.parent;parent!=null;parent=parent.parent){
    if(parent.name!="HandItemUse" && parent.name!="PlayerCamera")continue;
    instance.DiscoverFlamethrowerCodex();return;
   }
  }
  void DiscoverFlamethrowerCodex(){
   if(flamethrowerCodexDiscovered)return;
   flamethrowerCodexDiscovered=true;
   RefreshFlamethrowerCodexTile();
   FindNativeFlamethrowerCodex();
   if(nativeFlamethrowerCodex!=null)nativeFlamethrowerCodex.SendEvent("Codex");
   Logger.LogInfo("Flamethrower discovered in the game Codex.");
  }
  void TickFlamethrowerCodex(){
   if(flameTemplate!=null)PrepareFlamethrowerCodex(flameTemplate);
   InstallFlamethrowerCodexTile();
   if(!flamethrowerCodexDiscovered && !ChainsawWorldLoading){
    foreach(var item in FlamethrowerItem.Active){
     if(item==null || item.gameObject==flameTemplate)continue;
     var discovery=item.GetComponent<FlamethrowerCodexDiscovery>();
     if(discovery==null){PrepareFlamethrowerCodex(item.gameObject);discovery=item.GetComponent<FlamethrowerCodexDiscovery>();}
     CheckFlamethrowerCodexPickup(discovery);
     if(flamethrowerCodexDiscovered)break;
    }
   }
   RefreshFlamethrowerCodexTile();
  }
  void FindNativeFlamethrowerCodex(){
   if(nativeFlamethrowerCodex!=null)return;
   nativeFlamethrowerCodex=Resources.FindObjectsOfTypeAll<PlayMakerFSM>().FirstOrDefault(f=>
    f!=null && f.FsmName=="Codex" && f.gameObject.name=="__GameManager__" && f.gameObject.scene.IsValid() && f.gameObject.scene.isLoaded);
  }
  void LoadFlamethrowerCodexPhoto(){
   if(flamethrowerCodexPhoto!=null || Time.unscaledTime<nextFlamethrowerCodexImageAttempt)return;
   nextFlamethrowerCodexImageAttempt=Time.unscaledTime+10f;
   string path=Path.Combine(Path.GetDirectoryName(Info.Location),"Flamethrower/Codex.png");
   try{
    if(!File.Exists(path))throw new FileNotFoundException("The flamethrower Codex photograph is missing",path);
    var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
    if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(path))){UnityEngine.Object.Destroy(texture);throw new IOException("The flamethrower Codex photograph could not be decoded");}
    texture.name="Flamethrower.Codex.Discovered";texture.wrapMode=TextureWrapMode.Clamp;
    texture.hideFlags=HideFlags.DontUnloadUnusedAsset;flamethrowerCodexPhoto=texture;
   }catch(Exception error){if(!warnedFlamethrowerCodexPhoto){warnedFlamethrowerCodexPhoto=true;Logger.LogWarning(error.Message);}}
  }
  void InstallFlamethrowerCodexTile(){
   if(flamethrowerCodexTile!=null)return;
   var nativeTile=Resources.FindObjectsOfTypeAll<PlayMakerFSM>().FirstOrDefault(f=>
    f!=null && f.FsmName=="CodexContent" && f.gameObject.name=="weapon 23" && f.gameObject.scene.IsValid() && f.gameObject.scene.isLoaded &&
    f.transform.parent!=null && f.transform.parent.parent!=null && f.transform.parent.parent.name=="Weapons 3");
   if(nativeTile==null)return;
   var originalImage=nativeTile.GetComponent<RawImage>();if(originalImage==null)return;
   flamethrowerCodexLocked=Resources.FindObjectsOfTypeAll<Texture2D>().FirstOrDefault(t=>t!=null && t.name=="codex_empty");
   if(flamethrowerCodexLocked==null && originalImage.texture!=null && originalImage.texture.name=="codex_empty")flamethrowerCodexLocked=originalImage.texture;
   if(flamethrowerCodexLocked==null)return;
   // Staging keeps the donor's discovery FSM from running on the cloned tile.
   var staging=new GameObject("Flamethrower.Codex.Staging");staging.SetActive(false);
   var clone=UnityEngine.Object.Instantiate(nativeTile.gameObject,staging.transform,false);
   clone.name="weapon flamethrower";
   foreach(var fsm in clone.GetComponentsInChildren<PlayMakerFSM>(true))UnityEngine.Object.DestroyImmediate(fsm);
   flamethrowerCodexTile=clone;flamethrowerCodexImage=clone.GetComponent<RawImage>();
   flamethrowerCodexTitle=clone.GetComponentsInChildren<Text>(true).FirstOrDefault(t=>t.gameObject.name=="text");
   if(flamethrowerCodexTitle!=null)flamethrowerCodexTitle.text="flamethrower";
   clone.AddComponent<FlamethrowerCodexTile>().owner=this;
   clone.transform.SetParent(nativeTile.transform.parent,false);clone.transform.SetAsLastSibling();
   // Native GridLayoutGroup places the extra entry in the next weapon slot.
   var layout=clone.transform.parent as RectTransform;
   if(layout!=null)LayoutRebuilder.MarkLayoutForRebuild(layout);
   clone.SetActive(true);UnityEngine.Object.Destroy(staging);
   RefreshFlamethrowerCodexTile();
   Logger.LogInfo("Flamethrower Codex tile added to native Weapons 3 with the native undiscovered icon.");
  }
  internal void RefreshFlamethrowerCodexTile(){
   if(flamethrowerCodexImage==null)return;
   LoadFlamethrowerCodexPhoto();
   flamethrowerCodexImage.texture=flamethrowerCodexDiscovered && flamethrowerCodexPhoto!=null?flamethrowerCodexPhoto:flamethrowerCodexLocked;
   flamethrowerCodexImage.color=Color.white;
   if(flamethrowerCodexTitle!=null)flamethrowerCodexTitle.gameObject.SetActive(flamethrowerCodexDiscovered);
  }
 }
}
