using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using ES3Internal;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
namespace ChainsawPrototype {
 [Serializable,DataContract] public sealed class FlameWorldSpawnRecord {[DataMember] public string key,anchor,itemName;[DataMember] public bool pending;}
 [Serializable,DataContract] public sealed class FlameWorldSpawnSave {[DataMember] public int version=1;[DataMember] public FlameWorldSpawnRecord[] areas=new FlameWorldSpawnRecord[0];[DataMember] public FlameSavedTestSite[] testSites=new FlameSavedTestSite[0];}
 internal sealed class FlameSpawnSite {internal Transform Root,Room,Anchors,Reference;internal bool Laboratory;internal string Key;}
 public partial class Plugin {
  static readonly float FlamethrowerWorldSpawnChance=.25f;
  const string FlameWorldSpawnSaveKey="ApocalypterWeaponsPack.FlamethrowerWorldLoot";
  readonly Dictionary<string,FlameWorldSpawnRecord> flameWorldSpawnRecords=new Dictionary<string,FlameWorldSpawnRecord>(StringComparer.Ordinal);
  readonly List<FlameSpawnSite> flameSpawnSites=new List<FlameSpawnSite>();
  readonly HashSet<Transform> knownFlameSiteRoots=new HashSet<Transform>();int flameSiteCursor;
  float nextFlameSpawnScan,nextFlameSpawnError;
  static bool NativePoiName(string name,string expected){return name==expected||name.StartsWith(expected+"(Clone)",StringComparison.Ordinal);}
  static string FlameWorldSiteKey(bool lab,Vector3 position){return (lab?"Laboratory:":"Professor:")+Mathf.RoundToInt(position.x*10f).ToString(CultureInfo.InvariantCulture)+":"+Mathf.RoundToInt(position.y*10f).ToString(CultureInfo.InvariantCulture)+":"+Mathf.RoundToInt(position.z*10f).ToString(CultureInfo.InvariantCulture);}
  static FlameSpawnSite DescribeFlameSpawnSite(Transform root){
   if(root==null||!root.gameObject.scene.IsValid()||!root.gameObject.scene.isLoaded)return null;
   bool lab=NativePoiName(root.name,"Building_3"),professor=NativePoiName(root.name,"Building_6");if(!lab&&!professor)return null;
   Transform room=lab?root.Find("underground_lab_1"):root;
   Transform reference=lab?root.Find("to_surface/spawn"):root.Find("EnemySpawn_Wild_Boss");
   if(room==null||reference==null)return null;
   if(professor){
    bool flag=false;foreach(var fsm in reference.GetComponents<PlayMakerFSM>())foreach(var state in fsm.Fsm.States)foreach(var action in state.Actions){var test=action as BoolTest;if(test!=null&&test.boolVariable!=null&&test.boolVariable.Name=="Boss_Collage_Teacher")flag=true;}
    if(!flag)return null;
   }
   var anchors=room.Find("spawn_locations_item");if(anchors==null||anchors.childCount==0||room.Find("ItemSpawn_V1_All")==null)return null;
   return new FlameSpawnSite{Root=root,Room=room,Anchors=anchors,Reference=reference,Laboratory=lab,Key=FlameWorldSiteKey(lab,root.position)};
  }
  void RememberFlameSite(Transform root){
   if(root==null||knownFlameSiteRoots.Contains(root))return;
   var site=DescribeFlameSpawnSite(root);if(site==null)return;
   knownFlameSiteRoots.Add(root);flameSpawnSites.Add(site);
  }
  void TickFlameSiteDiscovery(){
   var fsms=PlayMakerFSM.FsmList;int count=fsms.Count;if(count==0){flameSiteCursor=0;return;}
   long started=System.Diagnostics.Stopwatch.GetTimestamp();
   for(int visited=0;visited<64&&visited<count;visited++){
    if(flameSiteCursor>=fsms.Count)flameSiteCursor=0;if(fsms.Count==0)break;
    var fsm=fsms[flameSiteCursor++];
    if(fsm!=null&&fsm.FsmName=="ItemList")RememberWeaponMerchantRequests(fsm);
    if(fsm!=null&&fsm.FsmName=="ItemSpawner"){RememberWeaponMerchant(fsm);}
    if(fsm!=null&&fsm.FsmName=="ItemSpawner")for(var node=fsm.transform;node!=null;node=node.parent){
     if(knownFlameSiteRoots.Contains(node))break;
     if(NativePoiName(node.name,"Building_3")||NativePoiName(node.name,"Building_6")){RememberFlameSite(node);break;}
    }
    if((visited&7)==7&&(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency>=.5)break;
   }
  }
  static bool FlameSiteLootReady(FlameSpawnSite site){
   if(!site.Root.gameObject.activeInHierarchy||!site.Room.gameObject.activeInHierarchy)return false;
   if(site.Root.GetComponent<FlameTestSite>()!=null)return true;
   var source=site.Room.Find("ItemSpawn_V1_All");if(source==null||!source.gameObject.activeInHierarchy)return false;
   var fsm=source.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="ItemSpawner");
   var done=fsm==null?null:fsm.FsmVariables.FindFsmInt("OnOff");return done!=null&&done.Value==1;
  }
  static Transform ChooseFlameLootAnchor(FlameSpawnSite site,bool requireFree){
   Transform best=null;float score=float.PositiveInfinity;
   for(int i=0;i<site.Anchors.childCount;i++){
    var anchor=site.Anchors.GetChild(i);if(requireFree&&ChainsawLootAnchorOccupied(anchor.position))continue;
    var delta=anchor.position-site.Reference.position;float candidate=delta.x*delta.x+delta.z*delta.z+delta.y*delta.y*4f;
    if(candidate<score){score=candidate;best=anchor;}
   }return best;
  }
  void TickFlamethrowerWorldSpawns(){
   if(Time.unscaledTime<nextFlameSpawnScan)return;nextFlameSpawnScan=Time.unscaledTime+1f;
   if(ChainsawWorldLoading||!ChainsawPersistenceReady||flameTemplate==null)return;
   for(int i=flameSpawnSites.Count-1;i>=0;i--)if(flameSpawnSites[i].Root==null||!flameSpawnSites[i].Root.gameObject.scene.isLoaded){knownFlameSiteRoots.Remove(flameSpawnSites[i].Root);flameSpawnSites.RemoveAt(i);}
   foreach(var site in flameSpawnSites){
    site.Key=FlameWorldSiteKey(site.Laboratory,site.Root.position);
    FlameWorldSpawnRecord record;flameWorldSpawnRecords.TryGetValue(site.Key,out record);
    if((record!=null&&!record.pending)||!FlameSiteLootReady(site))continue;
    try{
     var anchor=ChooseFlameLootAnchor(site,true);if(anchor==null)continue;
     // Persist the decision before creation. A temporary prefab/readiness
     // failure retries the successful roll; failed rolls never reroll.
     if(record==null){record=new FlameWorldSpawnRecord{key=site.Key,anchor=anchor.name,itemName="",pending=UnityEngine.Random.value<FlamethrowerWorldSpawnChance};flameWorldSpawnRecords.Add(site.Key,record);}
     if(!record.pending)continue;
     float clearance=flameMesh==null?.2f:Mathf.Max(.12f,flameMesh.bounds.extents.y+.04f);
     var item=SpawnPersistentFlamethrower(anchor.position+Vector3.up*clearance,Quaternion.Euler(0f,site.Root.eulerAngles.y,0f));if(item==null)continue;
     record.anchor=anchor.name;record.itemName=item.name;record.pending=false;
     Logger.LogInfo("Flamethrower spawned at "+(site.Laboratory?"Laboratory underground":"Professor PoI")+" / "+anchor.name+" (25% chance).");
    }catch(Exception error){if(Time.unscaledTime>=nextFlameSpawnError){nextFlameSpawnError=Time.unscaledTime+10f;Logger.LogError("Flamethrower PoI loot could not finish: "+error);}}
   }
  }
  GameObject SpawnPersistentFlamethrower(Vector3 position,Quaternion rotation){
   if(flameTemplate==null||!ChainsawPersistenceReady)return null;RegisterFlamethrowerSaveTemplate();
   var prefab=flameTemplate.GetComponent<ES3Prefab>();var manager=ES3ReferenceMgrBase.Current;
   if(prefab==null||manager==null||!manager.prefabs.Contains(prefab))return null;
   var item=Instantiate(flameTemplate,position,rotation);item.transform.SetParent(null,true);item.SetActive(true);
   TrackPersistentFlamethrower(item);InitializeSpawnFuel(item);UpdateFlamethrowerFuelLabel(item);return item;
  }
  void SaveFlameWorldSpawns(ES3Settings settings){ES3.Save<string>(FlameWorldSpawnSaveKey,WeaponSaveJson.Serialize(new FlameWorldSpawnSave{areas=flameWorldSpawnRecords.Values.OrderBy(r=>r.key,StringComparer.Ordinal).ToArray(),testSites=SavedFlameTestSites()}),settings);}
  void LoadFlameWorldSpawns(ES3Settings settings){
   flameWorldSpawnRecords.Clear();ClearFlameTestSiteCopies();if(!ES3.KeyExists(FlameWorldSpawnSaveKey,settings))return;
   var saved=WeaponSaveJson.Deserialize<FlameWorldSpawnSave>(ES3.Load<string>(FlameWorldSpawnSaveKey,settings));
   if(saved==null||saved.version!=1||saved.areas==null)return;
   foreach(var record in saved.areas)if(record!=null&&!string.IsNullOrEmpty(record.key))flameWorldSpawnRecords[record.key]=record;
   RestoreFlameTestSites(saved.testSites);
  }
  void ResetFlameWorldSpawns(){flameWorldSpawnRecords.Clear();ClearFlameTestSiteCopies();flameSpawnSites.Clear();knownFlameSiteRoots.Clear();flameSiteCursor=0;nextFlameSpawnScan=0f;}
 }
}
