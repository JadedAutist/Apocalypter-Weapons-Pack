using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace ChainsawPrototype {
 public partial class Plugin {
  ConfigEntry<bool> chainsawBossLoot;
  const float ChainsawBossAreaSpawnChance=.25f;
  float nextChainsawWorldSpawnScan,nextChainsawWorldSpawnError;
  sealed class PendingChainsawBossRoll {public bool spawn;public int anchor;public GameObject item;}
  readonly Dictionary<string,PendingChainsawBossRoll> pendingChainsawBossRolls=new Dictionary<string,PendingChainsawBossRoll>();

  void SetupChainsawWorldSpawns(){
   chainsawBossLoot=FixedSetting("World spawns","Chainsaw boss-area loot",true);
  }

  // Native ItemSpawner keeps OnOff=1 after it has generated loot and saves that value.
  // Inspect its live FSM list once per second rather than scanning every object each frame.
  void TickChainsawWorldSpawns(){
   if(ChainsawWorldLoading){pendingChainsawBossRolls.Clear();return;}
   if(chainsawBossLoot==null || !chainsawBossLoot.Value || !ChainsawPersistenceReady || Time.unscaledTime<nextChainsawWorldSpawnScan)return;
   nextChainsawWorldSpawnScan=Time.unscaledTime+1f;
   var fsms=PlayMakerFSM.FsmList;
   for(int index=0;index<fsms.Count;index++){
    var bossGenerator=fsms[index];
    if(bossGenerator==null || bossGenerator.FsmName!="ItemSpawner" || !bossGenerator.gameObject.scene.IsValid() || !bossGenerator.gameObject.scene.isLoaded || !bossGenerator.gameObject.activeInHierarchy)continue;
    string bossFlag,lootGeneratorName,anchorGroupName;
    if(IsNamedBossGenerator(bossGenerator,"EnemySpawn_Scrapyard_Boss","Boss_Buzzgut")){
     bossFlag="Buzzgut";lootGeneratorName="ItemSpawn_V1_All";anchorGroupName="spawn_locations_item";
    }else if(IsNamedBossGenerator(bossGenerator,"EnemySpawn_Wild_Boss","Boss_Black_Juggernaut")){
     bossFlag="BlackJuggernaut";lootGeneratorName="ItemSpawn_Unique_BossRoom";anchorGroupName="spawn_locations_bossRoom_items";
    }else continue;
    try{TrySpawnChainsawBossLoot(bossGenerator.transform,bossFlag,lootGeneratorName,anchorGroupName);}
    catch(Exception e){if(Time.unscaledTime>=nextChainsawWorldSpawnError){nextChainsawWorldSpawnError=Time.unscaledTime+10f;Logger.LogError("Chainsaw boss-area loot could not finish: "+e);}}
   }
  }

  static bool IsNamedBossGenerator(PlayMakerFSM fsm,string objectName,string bossFlag){
   string name=fsm.gameObject.name;
   if(name!=objectName && !name.StartsWith(objectName+"(Clone)",StringComparison.Ordinal))return false;
   // Other wild boss spawners share the same object name. Match the native global boss flag.
   foreach(var state in fsm.Fsm.States)foreach(var action in state.Actions){
    var test=action as BoolTest;
    if(test!=null && test.boolVariable!=null && test.boolVariable.Name==bossFlag)return true;
   }
   return false;
  }

  void TrySpawnChainsawBossLoot(Transform bossGenerator,string boss,string lootGeneratorName,string anchorGroupName){
   Transform area=null,anchors=null,lootGenerator=null;
   for(var parent=bossGenerator.parent;parent!=null;parent=parent.parent){
    anchors=parent.Find(anchorGroupName);lootGenerator=parent.Find(lootGeneratorName);
    if(anchors!=null && lootGenerator!=null){area=parent;break;}
   }
   if(area==null || anchors.childCount==0 || !lootGenerator.gameObject.activeInHierarchy)return;
   PlayMakerFSM nativeLoot=null;
   foreach(var fsm in lootGenerator.GetComponents<PlayMakerFSM>())if(fsm.FsmName=="ItemSpawner"){nativeLoot=fsm;break;}
   var onOff=nativeLoot==null?null:nativeLoot.FsmVariables.FindFsmInt("OnOff");
   if(onOff==null || onOff.Value!=1)return;
   string key=ChainsawBossAreaKey(boss,area);
   bool savedSpawn;int savedAnchor;
   if(TryGetAreaRoll(key,out savedSpawn,out savedAnchor))return;

   // Pick a native item anchor only after normal loot generation has completed.
   PendingChainsawBossRoll roll;
   if(!pendingChainsawBossRolls.TryGetValue(key,out roll)){
    var available=new List<int>();
    for(int index=0;index<anchors.childCount;index++)if(!ChainsawLootAnchorOccupied(anchors.GetChild(index).position))available.Add(index);
    int chosen=available.Count>0?available[UnityEngine.Random.Range(0,available.Count)]:UnityEngine.Random.Range(0,anchors.childCount);
    roll=new PendingChainsawBossRoll{spawn=UnityEngine.Random.value<ChainsawBossAreaSpawnChance,anchor=chosen};
    pendingChainsawBossRolls.Add(key,roll);
   }
   if(!roll.spawn){RecordAreaRoll(key,false,roll.anchor);pendingChainsawBossRolls.Remove(key);Logger.LogInfo("Chainsaw loot roll: "+boss+" area has no chainsaw.");return;}
   var anchor=anchors.GetChild(Mathf.Clamp(roll.anchor,0,anchors.childCount-1));
   // The world model is centred on its rigidbody, so lift it clear of the supporting surface.
   float clearance=worldMesh==null?.35f:Mathf.Max(.08f,worldMesh.bounds.extents.magnitude+.03f);
   if(roll.item==null)roll.item=SpawnPersistentChainsaw(anchor.position+Vector3.up*clearance,Quaternion.Euler(0f,UnityEngine.Random.Range(0f,360f),0f));
   if(roll.item==null)return; // Retry the same roll if native readiness changed; do not reroll it.
   RecordAreaRoll(key,true,roll.anchor);pendingChainsawBossRolls.Remove(key);
   Logger.LogInfo("Rare chainsaw spawned in "+boss+" area at native item anchor "+anchor.name+" (25% chance).");
  }

  static string ChainsawBossAreaKey(string boss,Transform area){
   Vector3 p=area.position;
   // Coordinates are restored by native world saves; tenth-metre quantization avoids float noise.
   return boss+":"+Mathf.RoundToInt(p.x*10f).ToString(CultureInfo.InvariantCulture)+":"+Mathf.RoundToInt(p.y*10f).ToString(CultureInfo.InvariantCulture)+":"+Mathf.RoundToInt(p.z*10f).ToString(CultureInfo.InvariantCulture);
  }

  static bool ChainsawLootAnchorOccupied(Vector3 position){
   foreach(var collider in Physics.OverlapSphere(position,.45f,~0,QueryTriggerInteraction.Ignore)){
    if(collider==null || collider.attachedRigidbody==null)continue;
    var body=collider.attachedRigidbody;
    foreach(var fsm in body.GetComponents<PlayMakerFSM>())if(fsm.FsmName=="ItemName" || fsm.FsmName=="weaponType")return true;
   }
   return false;
  }
 }
}
