using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using ES3Internal;
using UnityEngine;
namespace ChainsawPrototype {
 [Serializable,DataContract] public sealed class BurntCorpseRecord {
  [DataMember] public string name;
  [DataMember] public long prefabId;
 }
 [Serializable,DataContract] public sealed class BurntCorpseSave {
  [DataMember] public int version=1;
  [DataMember] public BurntCorpseRecord[] corpses=new BurntCorpseRecord[0];
 }
 public partial class Plugin {
  const string BurntCorpseSaveKey="ApocalypterWeaponsPack.BurntCorpses";
  const int MaxBurntCorpseRecords=4096,MaxBurntCorpsePayload=2097152;
  readonly List<BurntCorpseRecord> pendingBurntCorpses=new List<BurntCorpseRecord>();
  bool warnedBurntCorpseMetadata;int pendingBurntCorpseAttempts;float pendingBurntCorpseDeadline;
  void SaveBurntCorpses(ES3Settings settings){
   var saved=new List<BurntCorpseRecord>();var pendingKeys=new HashSet<string>(StringComparer.Ordinal);
   foreach(var record in pendingBurntCorpses)pendingKeys.Add(CorpseIdentity(record));
   foreach(var pair in NativeCorpseIdentities()){
    var corpse=pair.Value;if(corpse==null||(corpse.GetComponent<FlameBurntCorpse>()==null&&!pendingKeys.Contains(pair.Key)))continue;
    var record=CorpseRecord(corpse);if(record!=null&&saved.Count<MaxBurntCorpseRecords)saved.Add(record);
   }
   ES3.Save<string>(BurntCorpseSaveKey,WeaponSaveJson.Serialize(new BurntCorpseSave{corpses=saved.ToArray()}),settings);
  }
  void LoadBurntCorpses(ES3Settings settings){
   ClearPendingBurntCorpses();if(!ES3.KeyExists(BurntCorpseSaveKey,settings))return;
   try{
    var json=ES3.Load<string>(BurntCorpseSaveKey,settings);
    if(string.IsNullOrEmpty(json)||json.Length>MaxBurntCorpsePayload)return;
    var saved=WeaponSaveJson.Deserialize<BurntCorpseSave>(json);
    if(saved==null||saved.version!=1||saved.corpses==null||saved.corpses.Length>MaxBurntCorpseRecords)return;
    var unique=new Dictionary<string,BurntCorpseRecord>(StringComparer.Ordinal);var duplicates=new HashSet<string>(StringComparer.Ordinal);
    foreach(var record in saved.corpses){if(!ValidCorpseRecord(record))continue;var key=CorpseIdentity(record);if(unique.ContainsKey(key))duplicates.Add(key);else unique.Add(key,record);}
    foreach(var pair in unique)if(!duplicates.Contains(pair.Key))pendingBurntCorpses.Add(pair.Value);
   }catch(Exception error){if(!warnedBurntCorpseMetadata){warnedBurntCorpseMetadata=true;Logger.LogWarning("Burnt corpse metadata could not be read; native saved items remain unchanged. "+error.Message);}}
  }
  void RestorePendingBurntCorpses(){AdvanceBurntCorpseRestore(Time.unscaledTime);}
  void AdvanceBurntCorpseRestore(float now){
   if(pendingBurntCorpses.Count==0||ChainsawWorldLoading)return;
   if(pendingBurntCorpseAttempts==0)pendingBurntCorpseDeadline=now+30f;
   if(pendingBurntCorpseAttempts>=12||now>=pendingBurntCorpseDeadline){ClearPendingBurntCorpses();return;}
   pendingBurntCorpseAttempts++;
   if(chainsawSaveItems==null||chainsawSaveItems.arrayList==null){if(pendingBurntCorpseAttempts>=12)ClearPendingBurntCorpses();return;}
   var native=NativeCorpseIdentities();
   for(int i=pendingBurntCorpses.Count-1;i>=0;i--){
    GameObject corpse;if(!native.TryGetValue(CorpseIdentity(pendingBurntCorpses[i]),out corpse)||corpse==null)continue;
    RestoreFlameCorpse(corpse);pendingBurntCorpses.RemoveAt(i);
   }
   if(pendingBurntCorpses.Count==0||pendingBurntCorpseAttempts>=12)ClearPendingBurntCorpses();
  }
  void ClearPendingBurntCorpses(){pendingBurntCorpses.Clear();pendingBurntCorpseAttempts=0;pendingBurntCorpseDeadline=0f;}
  Dictionary<string,GameObject> NativeCorpseIdentities(){
   var unique=new Dictionary<string,GameObject>(StringComparer.Ordinal);var duplicates=new HashSet<string>(StringComparer.Ordinal);
   if(chainsawSaveItems==null||chainsawSaveItems.arrayList==null)return unique;
   foreach(var entry in chainsawSaveItems.arrayList){
    var corpse=entry as GameObject;var record=CorpseRecord(corpse);if(record==null)continue;var key=CorpseIdentity(record);GameObject prior;
    if(unique.TryGetValue(key,out prior)){if(!ReferenceEquals(prior,corpse))duplicates.Add(key);}else unique.Add(key,corpse);
   }
   foreach(var key in duplicates)unique.Remove(key);return unique;
  }
  static BurntCorpseRecord CorpseRecord(GameObject corpse){
   if(corpse==null||!corpse.scene.IsValid()||!corpse.scene.isLoaded)return null;
   var prefab=corpse.GetComponent<ES3Prefab>();if(prefab==null)return null;
   var record=new BurntCorpseRecord{name=corpse.name,prefabId=prefab.prefabId};return ValidCorpseRecord(record)?record:null;
  }
  static bool ValidCorpseRecord(BurntCorpseRecord record){return record!=null&&record.prefabId>0&&!string.IsNullOrWhiteSpace(record.name)&&record.name.Length<=512;}
  static string CorpseIdentity(BurntCorpseRecord record){return record.prefabId.ToString(CultureInfo.InvariantCulture)+":"+record.name;}
 }
}


