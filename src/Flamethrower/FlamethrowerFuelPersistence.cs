using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;
using ES3Internal;
namespace ChainsawPrototype {
 [Serializable,DataContract] public sealed class FlamethrowerSavedFuel {[DataMember] public string reference,name;[DataMember] public float litres;[DataMember] public int kind;}
 [Serializable,DataContract] public sealed class FlamethrowerFuelSave {[DataMember] public int version=1;[DataMember] public FlamethrowerSavedFuel[] tanks=new FlamethrowerSavedFuel[0];}
 public partial class Plugin {
  const long FlamethrowerPrefabId=4708325016258697551L;
  const string FlamethrowerFuelSaveKey="ApocalypterWeaponsPack.FlamethrowerFuel";
  readonly List<FlamethrowerSavedFuel> pendingFlameTanks=new List<FlamethrowerSavedFuel>();
  void RegisterFlamethrowerSaveTemplate(){
   if(flameTemplate==null)return;var prefab=flameTemplate.GetComponent<ES3Prefab>();
   if(prefab==null||prefab.prefabId!=FlamethrowerPrefabId){PrepareExistingChainsaw(flameTemplate,FlamethrowerPrefabId);prefab=flameTemplate.GetComponent<ES3Prefab>();}
   foreach(var manager in Resources.FindObjectsOfTypeAll<ES3ReferenceMgrBase>())if(manager!=null&&manager.gameObject.scene.IsValid())manager.AddPrefab(prefab);
   FindChainsawSaveLists();if(chainsawItemCatalog!=null){if(chainsawItemCatalog.preFillGameObjectList!=null&&!chainsawItemCatalog.preFillGameObjectList.Contains(flameTemplate))chainsawItemCatalog.preFillGameObjectList.Add(flameTemplate);if(chainsawItemCatalog.arrayList!=null&&!chainsawItemCatalog.arrayList.Contains(flameTemplate))chainsawItemCatalog.arrayList.Add(flameTemplate);}
  }
  void TickFlamethrowerFuelPersistence(){
   RegisterFlamethrowerSaveTemplate();FindChainsawSaveLists();
   foreach(var marker in Resources.FindObjectsOfTypeAll<FlamethrowerItem>()){
    if(marker==null||marker.gameObject==flameTemplate||!marker.gameObject.scene.IsValid()||!marker.gameObject.scene.isLoaded)continue;
    TrackPersistentFlamethrower(marker.gameObject);
   }
   RestorePendingFlamethrowerFuel();
  }
  void TrackPersistentFlamethrower(GameObject item){
   var prefab=item.GetComponent<ES3Prefab>();if(prefab==null||prefab.prefabId!=FlamethrowerPrefabId)PrepareExistingChainsaw(item,FlamethrowerPrefabId);
   if(chainsawSaveItems!=null&&chainsawSaveItems.arrayList!=null&&!chainsawSaveItems.arrayList.Contains(item))chainsawSaveItems.arrayList.Add(item);
   if(item.name=="Flamethrower"||item.name=="Flamethrower(Clone)")item.name="Flamethrower(Clone)"+Guid.NewGuid().ToString("N");
  }
  void SaveFlamethrowerFuel(ES3Settings settings){
   TickFlamethrowerFuelPersistence();var saved=new List<FlamethrowerSavedFuel>();
   foreach(var marker in Resources.FindObjectsOfTypeAll<FlamethrowerItem>()){
    if(marker==null||marker.gameObject==flameTemplate||!marker.gameObject.scene.IsValid()||!marker.gameObject.scene.isLoaded)continue;
    var tank=marker.GetComponentInChildren<FlamethrowerFuelTank>(true);if(tank==null)continue;
    var manager=ES3ReferenceMgrBase.GetManagerFromScene(marker.gameObject.scene,true)??ES3ReferenceMgrBase.Current;
    saved.Add(new FlamethrowerSavedFuel{reference=manager==null?null:manager.Add(marker.gameObject).ToString(CultureInfo.InvariantCulture),name=marker.name,litres=tank.Litres,kind=(int)tank.Kind});
   }
   ES3.Save<string>(FlamethrowerFuelSaveKey,WeaponSaveJson.Serialize(new FlamethrowerFuelSave{tanks=saved.ToArray()}),settings);
  }
  void LoadFlamethrowerFuel(ES3Settings settings){
   pendingFlameTanks.Clear();if(!ES3.KeyExists(FlamethrowerFuelSaveKey,settings))return;
   var saved=WeaponSaveJson.Deserialize<FlamethrowerFuelSave>(ES3.Load<string>(FlamethrowerFuelSaveKey,settings));
   if(saved!=null&&saved.version==1&&saved.tanks!=null)pendingFlameTanks.AddRange(saved.tanks.Where(t=>t!=null));
  }
  void RestorePendingFlamethrowerFuel(){
   if(pendingFlameTanks.Count==0)return;var manager=ES3ReferenceMgrBase.Current;
   for(int i=pendingFlameTanks.Count-1;i>=0;i--){
    var saved=pendingFlameTanks[i];GameObject item=null;long id;
    if(manager!=null&&long.TryParse(saved.reference,NumberStyles.Integer,CultureInfo.InvariantCulture,out id))item=manager.Get(id) as GameObject;
    if(item==null||item==flameTemplate||item.GetComponent<FlamethrowerItem>()==null){
     var matches=Resources.FindObjectsOfTypeAll<FlamethrowerItem>().Where(f=>f!=null&&f.gameObject!=flameTemplate&&f.gameObject.scene.IsValid()&&f.name==saved.name).ToArray();
     item=matches.Length==1?matches[0].gameObject:null;
    }
    var tank=item==null?null:item.GetComponentInChildren<FlamethrowerFuelTank>(true);if(tank==null)continue;
    MarkRestoredWeaponFuel(item);tank.Restore((FlameFuelKind)saved.kind,saved.litres);UpdateFlamethrowerFuelLabel(item);pendingFlameTanks.RemoveAt(i);
   }
  }
 }
}
