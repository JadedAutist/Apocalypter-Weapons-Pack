using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;
using ES3Internal;

namespace ChainsawPrototype {
 [Serializable] public sealed class ChainsawSavedFuel {
  public string reference, name;
  public float litres;
 }
 [Serializable] public sealed class ChainsawAreaRoll {
  public string key;
  public bool spawn;
  public int anchorIndex;
 }
 [Serializable] public sealed class ChainsawSaveData {
  public int version=1;
  public bool codexDiscovered;
  public ChainsawSavedFuel[] fuel=new ChainsawSavedFuel[0];
  public ChainsawAreaRoll[] areas=new ChainsawAreaRoll[0];
 }
 public partial class Plugin {
  // A permanent, mod-owned identity. Never reuse the donor rifle's ES3 prefab ID.
  const long ChainsawPrefabId=4708325016258697550L;
  const string ChainsawSaveKey="ApocalypterWeaponsPack.Chainsaw";
  ES3Prefab chainsawSavePrefab;
  PlayMakerArrayListProxy chainsawSaveItems,chainsawItemCatalog;
  readonly Dictionary<string,ChainsawAreaRoll> chainsawAreaRolls=new Dictionary<string,ChainsawAreaRoll>(StringComparer.Ordinal);
  readonly List<ChainsawSavedFuel> chainsawPendingFuel=new List<ChainsawSavedFuel>();
  float chainsawLoadingUntil;
  int chainsawLoadDepth;
  int chainsawMetadataSceneHandle=-1;
  bool chainsawPersistenceInstalled;
  internal bool ChainsawWorldLoading {get{return chainsawLoadDepth>0 || Time.unscaledTime<chainsawLoadingUntil;}}
  internal bool ChainsawPersistenceReady {get{
   var manager=ES3ReferenceMgrBase.Current;
   return !ChainsawWorldLoading && templateReady && chainsawSavePrefab!=null && manager!=null && manager.prefabs.Contains(chainsawSavePrefab) && chainsawSaveItems!=null && chainsawSaveItems.arrayList!=null && chainsawItemCatalog!=null;
  }}

  void SetupChainsawPersistence(){
   if(chainsawPersistenceInstalled)return;
   harmony.Patch(AccessTools.Method(typeof(ES3PlayMaker.Save),"Enter"),prefix:new HarmonyMethod(typeof(Plugin),nameof(BeforeNativeChainsawSave)));
   harmony.Patch(AccessTools.Method(typeof(ES3PlayMaker.Load),"Enter"),prefix:new HarmonyMethod(typeof(Plugin),nameof(BeforeNativeChainsawLoad)),finalizer:new HarmonyMethod(typeof(Plugin),nameof(AfterNativeChainsawLoad)));
   SceneManager.sceneLoaded+=OnChainsawPersistenceSceneLoaded;
   chainsawPersistenceInstalled=true;
  }
  void OnChainsawPersistenceSceneLoaded(Scene scene,LoadSceneMode mode){
   if(mode==LoadSceneMode.Additive)return;
   ClearWeaponMerchantRequests();
   chainsawSaveItems=null;chainsawItemCatalog=null;
   if(chainsawMetadataSceneHandle!=scene.handle){chainsawAreaRolls.Clear();chainsawPendingFuel.Clear();pendingFlameTanks.Clear();ResetFlameWorldSpawns();ResetWeaponMerchantStock();ResetChainsawCodexDiscovery();ResetFlamethrowerCodexDiscovery();chainsawMetadataSceneHandle=-1;}
   chainsawLoadDepth=0;chainsawLoadingUntil=Time.unscaledTime+3f;
  }

  void PrepareChainsawPersistence(GameObject source){
   if(source==null)return;
   var prefab=source.GetComponent<ES3Prefab>();
   if(prefab==null)prefab=source.AddComponent<ES3Prefab>();
   prefab.prefabId=ChainsawPrefabId;
   // Unity copies these references onto each instantiated hierarchy. The local IDs
   // must be identical every launch, including the custom mesh and fuel receiver.
   prefab.localRefs=new ES3RefIdDictionary();
   foreach(var node in source.GetComponentsInChildren<Transform>(true)){
    string path=ChainsawReferencePath(source.transform,node);
    prefab.localRefs[node.gameObject]=ChainsawLocalReferenceId(path+"/GameObject");
    var ordinals=new Dictionary<Type,int>();
    foreach(var component in node.GetComponents<Component>()){
     if(component==null)continue;
     int ordinal;ordinals.TryGetValue(component.GetType(),out ordinal);ordinals[component.GetType()]=ordinal+1;
     prefab.localRefs[component]=ChainsawLocalReferenceId(path+"/"+component.GetType().FullName+"["+ordinal+"]");
    }
    // Preserve native transforms/physics through ES3. FSMs and visual assets are
    // rebuilt from the registered prefab; fuel is restored from our slot key.
    var save=node.GetComponent<ES3GameObject>();if(save==null)save=node.gameObject.AddComponent<ES3GameObject>();
    save.components=new List<Component>{node};
    var body=node.GetComponent<Rigidbody>();if(body!=null)save.components.Add(body);
    var auto=node.GetComponent<ES3AutoSave>();
    if(auto!=null){auto.componentsToSave=new List<Component>(save.components);auto.saveChildren=false;auto.saveLayer=true;auto.saveTag=true;auto.saveName=true;auto.saveActive=true;}
   }
   chainsawSavePrefab=prefab;
   RegisterChainsawSaveTemplate();
   Logger.LogInfo("Chainsaw registered with its own native save/load prefab identity.");
  }
  static string ChainsawReferencePath(Transform root,Transform node){
   if(node==root)return "Chainsaw";
   int occurrence=0;for(int i=0;i<node.GetSiblingIndex();i++)if(node.parent.GetChild(i).name==node.name)occurrence++;
   return ChainsawReferencePath(root,node.parent)+"/"+node.name+"["+occurrence+"]";
  }
  static long ChainsawLocalReferenceId(string text){
   unchecked{ulong value=14695981039346656037UL;foreach(char c in text){value^=c;value*=1099511628211UL;}return (long)(value&0x7fffffffffffffffUL);}
  }
  void RegisterChainsawSaveTemplate(){
   if(chainsawSavePrefab==null || template==null)return;
   foreach(var manager in Resources.FindObjectsOfTypeAll<ES3ReferenceMgrBase>()){
    if(manager==null || !manager.gameObject.scene.IsValid())continue;
    manager.AddPrefab(chainsawSavePrefab);
   }
   FindChainsawSaveLists();
   if(chainsawItemCatalog!=null){
    if(chainsawItemCatalog.preFillGameObjectList!=null && !chainsawItemCatalog.preFillGameObjectList.Contains(template))chainsawItemCatalog.preFillGameObjectList.Add(template);
    if(chainsawItemCatalog.arrayList!=null && !chainsawItemCatalog.arrayList.Contains(template))chainsawItemCatalog.arrayList.Add(template);
   }
  }
  void FindChainsawSaveLists(){
   if(chainsawSaveItems!=null && chainsawItemCatalog!=null)return;
   foreach(var list in Resources.FindObjectsOfTypeAll<PlayMakerArrayListProxy>()){
    if(list==null || list.gameObject.name!="NewGO_ArrayList" || !list.gameObject.scene.IsValid() || !list.gameObject.scene.isLoaded)continue;
    if(list.referenceName=="ArrayList_Items")chainsawSaveItems=list;
    if(list.referenceName=="ItemList")chainsawItemCatalog=list;
   }
  }
  void TickChainsawPersistence(){
   if(template==null || !templateReady)return;
   RegisterChainsawSaveTemplate();
   foreach(var item in Resources.FindObjectsOfTypeAll<ChainsawItem>()){
    if(item==null || item.gameObject==template || !item.gameObject.scene.IsValid() || !item.gameObject.scene.isLoaded)continue;
    TrackPersistentChainsaw(item.gameObject);
   }
   RestorePendingChainsawFuel();
  }
  void TrackPersistentChainsaw(GameObject item){
   FindChainsawSaveLists();
   if(chainsawSaveItems==null || chainsawSaveItems.arrayList==null)return;
   var prefab=item.GetComponent<ES3Prefab>();
   if(prefab==null || prefab.prefabId!=ChainsawPrefabId)PrepareExistingChainsaw(item);
   if(!chainsawSaveItems.arrayList.Contains(item))chainsawSaveItems.arrayList.Add(item);
   if(item.name=="Chainsaw" || item.name=="Chainsaw(Clone)")item.name="Chainsaw(Clone)"+Guid.NewGuid().ToString("N");
  }
  void PrepareExistingChainsaw(GameObject item,long prefabId=ChainsawPrefabId){
   // Existing instances from a running pre-persistence build still receive the
   // custom ID and local map. Mapping by hierarchy path never touches the M16.
   var prefab=item.GetComponent<ES3Prefab>();if(prefab==null)prefab=item.AddComponent<ES3Prefab>();
   prefab.prefabId=prefabId;prefab.localRefs=new ES3RefIdDictionary();
   foreach(var node in item.GetComponentsInChildren<Transform>(true)){
    string path=ChainsawReferencePath(item.transform,node);
    prefab.localRefs[node.gameObject]=ChainsawLocalReferenceId(path+"/GameObject");
    var ordinals=new Dictionary<Type,int>();foreach(var component in node.GetComponents<Component>()){
     if(component==null)continue;int ordinal;ordinals.TryGetValue(component.GetType(),out ordinal);ordinals[component.GetType()]=ordinal+1;
     prefab.localRefs[component]=ChainsawLocalReferenceId(path+"/"+component.GetType().FullName+"["+ordinal+"]");
    }
    var save=node.GetComponent<ES3GameObject>();if(save==null)save=node.gameObject.AddComponent<ES3GameObject>();save.components=new List<Component>{node};
    var body=node.GetComponent<Rigidbody>();if(body!=null)save.components.Add(body);
    var auto=node.GetComponent<ES3AutoSave>();if(auto!=null){auto.componentsToSave=new List<Component>(save.components);auto.saveChildren=false;auto.saveLayer=true;auto.saveTag=true;auto.saveName=true;auto.saveActive=true;}
   }
  }
  internal GameObject SpawnPersistentChainsaw(Vector3 position,Quaternion rotation){
   if(template==null || !templateReady)return null;
   RegisterChainsawSaveTemplate();
   if(!ChainsawPersistenceReady)return null;
   var item=Instantiate(template,position,rotation);item.transform.SetParent(null,true);item.SetActive(true);
   TrackPersistentChainsaw(item);return item;
  }
  internal bool TryGetAreaRoll(string key,out bool spawn,out int anchorIndex){
   ChainsawAreaRoll roll;if(chainsawAreaRolls.TryGetValue(key,out roll)){spawn=roll.spawn;anchorIndex=roll.anchorIndex;return true;}
   spawn=false;anchorIndex=-1;return false;
  }
  internal void RecordAreaRoll(string key,bool spawn,int anchorIndex){chainsawAreaRolls[key]=new ChainsawAreaRoll{key=key,spawn=spawn,anchorIndex=anchorIndex};}

  static bool NativeChainsawArrayAction(FsmStateAction action,FsmString key){return key!=null && key.Value=="newGOArray_Items" && action.Fsm!=null && action.Fsm.Name=="Save_NewGO_ArrayList";}
  static void BeforeNativeChainsawSave(ES3PlayMaker.Save __instance){
   if(ReferenceEquals(instance,null) || !NativeChainsawArrayAction(__instance,__instance.key))return;
   try{instance.SaveNativeChainsawData(__instance.GetSettings());}catch(Exception error){instance.Logger.LogError("Chainsaw save failed: "+error);}
  }
  static void BeforeNativeChainsawLoad(ES3PlayMaker.Load __instance,out bool __state){
   __state=!ReferenceEquals(instance,null) && NativeChainsawArrayAction(__instance,__instance.key);if(!__state)return;
   instance.chainsawLoadDepth++;instance.chainsawLoadingUntil=Time.unscaledTime+3f;
   instance.chainsawMetadataSceneHandle=__instance.Owner.scene.handle;
   // Loading the slot can begin before our normal one-second setup scan.
   // Finish the custom prefab synchronously before ES3 reads an item reference.
   instance.CreateTemplate();instance.RegisterChainsawSaveTemplate();instance.CreateFlamethrower();instance.RegisterFlamethrowerSaveTemplate();
   if(instance.template==null || !instance.templateReady || instance.chainsawSavePrefab==null)instance.Logger.LogError("Chainsaw template is not ready before native item loading; saved chainsaws require the native rifle and gasoline assets to be available.");
   try{instance.LoadNativeChainsawData(__instance.GetSettings());}catch(Exception error){instance.Logger.LogError("Chainsaw slot data could not be loaded: "+error);}
  }
  static Exception AfterNativeChainsawLoad(Exception __exception,bool __state){
   if(!__state || ReferenceEquals(instance,null))return __exception;
   instance.chainsawLoadDepth=Mathf.Max(0,instance.chainsawLoadDepth-1);instance.chainsawLoadingUntil=Time.unscaledTime+3f;
   instance.MarkLoadedWeaponFuel();
   if(__exception==null)instance.RestoreWeaponMerchantStock();
   if(__exception==null){instance.RestorePendingChainsawFuel();instance.RestorePendingFlamethrowerFuel();}return __exception;
  }
  void SaveNativeChainsawData(ES3Settings settings){
   TickChainsawPersistence();
   var saved=new List<ChainsawSavedFuel>();
   if(chainsawSaveItems!=null && chainsawSaveItems.arrayList!=null)foreach(var entry in chainsawSaveItems.arrayList){
    var item=entry as GameObject;if(item==null || item==template || item.GetComponent<ChainsawItem>()==null)continue;
    var fuel=item.GetComponentInChildren<ChainsawFuel>(true);if(fuel==null)continue;
    var manager=ES3ReferenceMgrBase.GetManagerFromScene(item.scene,true)??ES3ReferenceMgrBase.Current;
    saved.Add(new ChainsawSavedFuel{reference=manager==null?null:manager.Add(item).ToString(CultureInfo.InvariantCulture),name=item.name,litres=fuel.Litres});
   }
   var data=new ChainsawSaveData{codexDiscovered=GetChainsawCodexDiscovery(),fuel=saved.ToArray(),areas=chainsawAreaRolls.Values.OrderBy(r=>r.key,StringComparer.Ordinal).ToArray()};
   ES3.Save<string>(ChainsawSaveKey,JsonUtility.ToJson(data),settings);
   SaveFlamethrowerCodexDiscovery(settings);
   SaveFlamethrowerFuel(settings);
   SaveFlameWorldSpawns(settings);
   SaveWeaponMerchantStock(settings);
   Logger.LogInfo("Saved "+saved.Count+" chainsaw fuel records and "+data.areas.Length+" area spawn rolls with "+settings.path+".");
  }
  void LoadNativeChainsawData(ES3Settings settings){
   chainsawAreaRolls.Clear();chainsawPendingFuel.Clear();ResetChainsawCodexDiscovery();
   ResetFlamethrowerCodexDiscovery();LoadFlamethrowerCodexDiscovery(settings);
   LoadFlamethrowerFuel(settings);
   LoadFlameWorldSpawns(settings);
   LoadWeaponMerchantStock(settings);
   if(!ES3.KeyExists(ChainsawSaveKey,settings))return;
   var data=JsonUtility.FromJson<ChainsawSaveData>(ES3.Load<string>(ChainsawSaveKey,settings));
   if(data==null || data.version!=1)return;
   RestoreChainsawCodexDiscovery(data.codexDiscovered);
   if(data.areas!=null)foreach(var roll in data.areas)if(roll!=null && !string.IsNullOrEmpty(roll.key))chainsawAreaRolls[roll.key]=roll;
   if(data.fuel!=null)chainsawPendingFuel.AddRange(data.fuel.Where(f=>f!=null));
  }
  void RestorePendingChainsawFuel(){
   if(chainsawPendingFuel.Count==0)return;var manager=ES3ReferenceMgrBase.Current;
   ChainsawItem[] candidates=null;
   for(int i=chainsawPendingFuel.Count-1;i>=0;i--){
    var saved=chainsawPendingFuel[i];GameObject item=null;long id;
    if(manager!=null && long.TryParse(saved.reference,NumberStyles.Integer,CultureInfo.InvariantCulture,out id))item=manager.Get(id,false) as GameObject;
    if(item==null && !string.IsNullOrEmpty(saved.name)){
     if(candidates==null)candidates=Resources.FindObjectsOfTypeAll<ChainsawItem>();
     var marker=candidates.FirstOrDefault(c=>c!=null && c.gameObject!=template && c.gameObject.scene.IsValid() && c.name==saved.name);if(marker!=null)item=marker.gameObject;
    }
    if(item==null || item.GetComponent<ChainsawItem>()==null)continue;
    var fuel=item.GetComponentInChildren<ChainsawFuel>(true);if(fuel==null)continue;
    var tank=fuel.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="LiquidAmount");var liquid=tank==null?null:tank.FsmVariables.FindFsmFloat("Liquid");
    if(liquid==null)continue;liquid.Value=float.IsNaN(saved.litres)||float.IsInfinity(saved.litres)?0f:Mathf.Clamp(saved.litres,0f,ChainsawFuel.Capacity);
    MarkRestoredWeaponFuel(item);UpdateDamageLabel(item);chainsawPendingFuel.RemoveAt(i);
   }
  }
 }
}
