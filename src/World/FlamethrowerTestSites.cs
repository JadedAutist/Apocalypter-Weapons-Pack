using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChainsawPrototype {
 // Compatibility only: rebuild sites already recorded by development saves.
 // Release gameplay has no test-site creation controls or terrain plot search.
 [Serializable] public sealed class FlameSavedTestSite {public bool laboratory;public Vector3 position;public Quaternion rotation;}
 public sealed class FlameTestSite:MonoBehaviour {public bool Laboratory;}
 public partial class Plugin {
  readonly List<FlameTestSite> flameTestSiteCopies=new List<FlameTestSite>();
  readonly Dictionary<bool,GameObject> flameTestSiteSources=new Dictionary<bool,GameObject>();
  GameObject FindFlameTestSiteSource(bool lab){
   GameObject source;if(flameTestSiteSources.TryGetValue(lab,out source)&&source!=null)return source;
   string expected=lab?"Building_3":"Building_6";
   foreach(var root in Resources.FindObjectsOfTypeAll<Transform>()){
    if(root.name!=expected||root.gameObject.scene.IsValid())continue;
    if(root.Find(lab?"underground_lab_1/spawn_locations_item":"EnemySpawn_Wild_Boss")==null)continue;
    flameTestSiteSources[lab]=root.gameObject;return root.gameObject;
   }return null;
  }
  FlameSpawnSite CreateFlameTestSiteCopy(GameObject source,bool lab,Vector3 position,Quaternion rotation){
   var stage=new GameObject("WeaponsPack.TestSiteStaging");stage.SetActive(false);GameObject copy=null;
   try{
    copy=Instantiate(source,stage.transform,false);copy.SetActive(false);copy.name=(lab?"Building_3":"Building_6")+"(Clone).WeaponsPackTestSite";
    foreach(var node in copy.GetComponentsInChildren<Transform>(true)){
     string name=node.name;
     if(name.StartsWith("EnemySpawn",StringComparison.Ordinal)||name.StartsWith("ItemSpawn",StringComparison.Ordinal)||name.StartsWith("CarSpawn",StringComparison.Ordinal)||name.StartsWith("TrailerSpawn",StringComparison.Ordinal)||name.StartsWith("CrateSpawn",StringComparison.Ordinal)||name=="codex_trigger"||name=="mapIcon")node.gameObject.SetActive(false);
     foreach(var fsm in node.GetComponents<PlayMakerFSM>())if(fsm.FsmName!="BasementDoor"&&fsm.FsmName!="ItemName")fsm.enabled=false;
     // Only the owned metadata below saves test architecture. Native ES3/PoI
     // registration must not mistake a manual test copy for normal world loot.
     foreach(var component in node.GetComponents<Component>())if(component!=null&&component.GetType().Name.StartsWith("ES3",StringComparison.Ordinal))DestroyImmediate(component);
    }
    var marker=copy.AddComponent<FlameTestSite>();marker.Laboratory=lab;
    copy.transform.SetPositionAndRotation(position,rotation);copy.transform.SetParent(null,true);copy.SetActive(true);
    var site=DescribeFlameSpawnSite(copy.transform);if(site==null){Destroy(copy);return null;}
    flameTestSiteCopies.Add(marker);RememberFlameSite(copy.transform);
    Logger.LogInfo("Created owned "+(lab?"Laboratory":"Professor")+" test site at "+position+"; native enemy/ordinary-loot spawners disabled.");return site;
   }catch{if(copy!=null)Destroy(copy);throw;}finally{Destroy(stage);}
  }
  FlameSavedTestSite[] SavedFlameTestSites(){return flameTestSiteCopies.Where(m=>m!=null).Select(m=>new FlameSavedTestSite{laboratory=m.Laboratory,position=m.transform.position,rotation=m.transform.rotation}).ToArray();}
  void RestoreFlameTestSites(FlameSavedTestSite[] saved){
   if(saved==null)return;var restored=new HashSet<bool>();
   foreach(var entry in saved){
    if(entry==null||restored.Contains(entry.laboratory)||!Finite(entry.position.x)||!Finite(entry.position.y)||!Finite(entry.position.z)||entry.position.sqrMagnitude>1e14f||!Finite(Quaternion.Dot(entry.rotation,entry.rotation)))continue;
    var source=FindFlameTestSiteSource(entry.laboratory);if(source==null){Logger.LogWarning("Saved flamethrower test site could not restore: native PoI asset unavailable.");continue;}
    var rotation=Quaternion.Dot(entry.rotation,entry.rotation)>.5f?entry.rotation:source.transform.rotation;
    if(CreateFlameTestSiteCopy(source,entry.laboratory,entry.position,rotation)!=null)restored.Add(entry.laboratory);
   }
  }
  void ClearFlameTestSiteCopies(){
   foreach(var marker in flameTestSiteCopies)if(marker!=null){knownFlameSiteRoots.Remove(marker.transform);flameSpawnSites.RemoveAll(s=>s.Root==marker.transform);Destroy(marker.gameObject);}
   flameTestSiteCopies.Clear();
  }
 }
}
