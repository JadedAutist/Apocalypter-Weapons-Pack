using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
namespace ChainsawPrototype {
 [Serializable] public sealed class WeaponMerchantRecord {
  public string key,itemName;public bool flame,spawn,created,forSale;public int price;
 }
 [Serializable] public sealed class WeaponMerchantSave {public int version=1;public WeaponMerchantRecord[] stock=new WeaponMerchantRecord[0];}
 // The native Buyable FSM owns payment, insufficient-funds checks and release.
 // This helper only holds the private weapon on its display while ForBuyInt=1.
 [DefaultExecutionOrder(32010)] public sealed class WeaponMerchantSale:MonoBehaviour {
  internal WeaponMerchantRecord Record;PlayMakerFSM sale;Rigidbody body;int layer;
  internal static bool IsForSale(GameObject item){var marker=item.GetComponent<WeaponMerchantSale>();return marker!=null&&marker.ForSale;}
  internal bool ForSale {get{var v=sale==null?null:sale.FsmVariables.FindFsmInt("ForBuyInt");return v!=null&&v.Value==1;}}
  internal void Bind(WeaponMerchantRecord record,PlayMakerFSM native,int saleLayer){Record=record;sale=native;layer=saleLayer;Hold();}
  void FixedUpdate(){Hold();}void LateUpdate(){Hold();}
  void Hold(){
   if(Record==null)return;Record.forSale=ForSale;if(!Record.forSale)return;
   if(body==null)body=GetComponent<Rigidbody>();
   if(body!=null){body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;body.detectCollisions=true;}
   gameObject.layer=layer;
  }
 }
 public partial class Plugin {
  const string WeaponMerchantSaveKey="ApocalypterWeaponsPack.SlumtownStock";
  readonly List<PlayMakerFSM> weaponMerchants=new List<PlayMakerFSM>();
  readonly Dictionary<string,WeaponMerchantRecord> weaponMerchantRecords=new Dictionary<string,WeaponMerchantRecord>(StringComparer.Ordinal);
  readonly Dictionary<string,WeaponMerchantRecord> pendingMerchantRestore=new Dictionary<string,WeaponMerchantRecord>(StringComparer.Ordinal);
  float nextMerchantError,merchantRestoreUntil;
  void RememberWeaponMerchant(PlayMakerFSM fsm){
   if(fsm==null||fsm.name!="MerchantItemsSpawn_V1"||!fsm.gameObject.scene.IsValid()||!fsm.gameObject.scene.isLoaded)return;
   var root=fsm.transform.parent;
   // Camp 15 is the native Codex entry labelled Slumtown. Its two general
   // racks share the weapons vendor; mechanic/organic vendors are separate.
   if(root==null||!NativePoiName(root.name,"Camp_15")||root.Find("spawnList")==null||root.Find("MerchantItemsSpawn_V1 (1)")==null)return;
   if(!weaponMerchants.Contains(fsm))weaponMerchants.Add(fsm);
  }
  static string MerchantAreaKey(Transform root){var p=root.position;return "Slumtown:"+Mathf.RoundToInt(p.x*10f).ToString(CultureInfo.InvariantCulture)+":"+Mathf.RoundToInt(p.y*10f).ToString(CultureInfo.InvariantCulture)+":"+Mathf.RoundToInt(p.z*10f).ToString(CultureInfo.InvariantCulture);}
  void TickWeaponMerchantStock(){
   if(ChainsawWorldLoading||!ChainsawPersistenceReady||flameTemplate==null)return;
   RestoreWeaponMerchantStock();
   for(int i=weaponMerchants.Count-1;i>=0;i--){
    var spawner=weaponMerchants[i];if(spawner==null||!spawner.gameObject.scene.isLoaded){weaponMerchants.RemoveAt(i);continue;}
    if(!spawner.gameObject.activeInHierarchy)continue;
    var done=spawner.FsmVariables.FindFsmInt("OnOff");if(done==null||done.Value!=1)continue;
    for(int kind=0;kind<2;kind++)try{TryStockWeapon(spawner.transform.parent,kind==1);}catch(Exception error){if(Time.unscaledTime>=nextMerchantError){nextMerchantError=Time.unscaledTime+10f;Logger.LogError("Weapons Pack merchant stock could not finish: "+error);}}
   }
  }
  void TryStockWeapon(Transform root,bool flame){
   string key=MerchantAreaKey(root)+(flame?":Flamethrower":":Chainsaw");WeaponMerchantRecord record;
   if(!weaponMerchantRecords.TryGetValue(key,out record)){
    record=new WeaponMerchantRecord{key=key,flame=flame,spawn=UnityEngine.Random.value<.1f,price=UnityEngine.Random.Range(3000,4001)};
    weaponMerchantRecords.Add(key,record);
   }
   if(!record.spawn||record.created)return;
   Vector3 position;Quaternion rotation;MerchantDisplayPosition(root,flame,out position,out rotation);
   var item=flame?SpawnPersistentFlamethrower(position,rotation):SpawnPersistentChainsaw(position,rotation);if(item==null)return;
   InitializeSpawnFuel(item);record.itemName=item.name;record.created=true;record.forSale=true;
   ApplyWeaponMerchantSale(item,record);
   Logger.LogInfo((flame?"Flamethrower":"Chainsaw")+" stocked at Slumtown for "+record.price+" nuts (10% chance).");
  }
  static void MerchantDisplayPosition(Transform root,bool flame,out Vector3 position,out Quaternion rotation){
   var rack=root.Find(flame?"spawnList (1)":"spawnList");
   for(int i=rack.childCount-1;i>=0;i--){var anchor=rack.GetChild(i);if(ChainsawLootAnchorOccupied(anchor.position))continue;position=anchor.position+Vector3.up*.16f;rotation=anchor.rotation;return;}
   // Keep both additions beside their native rack if its normal slots are full.
   position=rack.TransformPoint(new Vector3(.95f,.16f,0f));rotation=rack.rotation;
  }
  static PlayMakerFSM WeaponFsm(GameObject item,string name){return item.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName==name);}
  static void ResetOwnedWeaponSale(GameObject item){
   var sale=WeaponFsm(item,"ForBuy");var flag=sale==null?null:sale.FsmVariables.FindFsmInt("ForBuyInt");if(flag!=null)flag.Value=0;
   var buy=WeaponFsm(item,"Buyable");if(buy!=null)buy.enabled=false;
   var label=WeaponFsm(item,"ItemName");if(label!=null)label.enabled=true;
  }
  static void ApplyWeaponMerchantSale(GameObject item,WeaponMerchantRecord record){
   var value=WeaponFsm(item,"Value");var price=value==null?null:value.FsmVariables.FindFsmInt("BuyValue");
   var sale=WeaponFsm(item,"ForBuy");var flag=sale==null?null:sale.FsmVariables.FindFsmInt("ForBuyInt");var buy=WeaponFsm(item,"Buyable");
   if(price==null||flag==null||buy==null)throw new InvalidOperationException("The custom weapon has no native purchase contract.");
   price.Value=Mathf.Clamp(record.price,3000,4000);flag.Value=record.forSale?1:0;
   // Native Value can initialize after Instantiate or re-enter on Activate.
   // Replace only its BuyValue conversion; native resale calculations remain.
   foreach(var state in value.Fsm.States){
    var actions=state.Actions.ToArray();
    for(int i=0;i<actions.Length;i++){
     if(actions[i].GetType().Name!="ConvertFloatToInt"&&actions[i].GetType().Name!="SetIntValue")continue;
     var field=actions[i].GetType().GetField("intVariable");var target=field==null?null:field.GetValue(actions[i]) as FsmInt;
     if(target!=null&&(ReferenceEquals(target,price)||target.Name=="BuyValue")){actions[i]=new SetIntValue{intVariable=price,intValue=new FsmInt{Value=price.Value},everyFrame=false};actions[i].Init(state);}
    }
    state.Actions=actions;
   }
   var label=WeaponFsm(item,"ItemName");if(label!=null)label.enabled=!record.forSale;
   int saleLayer=item.layer;
   foreach(var state in sale.Fsm.States)if(state.Name=="forBuy")foreach(var action in state.Actions)if(action.GetType().Name=="SetLayer"){
    var field=action.GetType().GetField("layer");var layer=field==null?null:field.GetValue(action);if(layer is int)saleLayer=(int)layer;else if(layer is FsmInt)saleLayer=((FsmInt)layer).Value;
   }
   if(record.forSale){sale.enabled=true;sale.SendEvent("ForBuy");}
   buy.enabled=record.forSale;
   var marker=item.GetComponent<WeaponMerchantSale>()??item.AddComponent<WeaponMerchantSale>();marker.Bind(record,sale,saleLayer);
  }
  void RestoreWeaponMerchantStock(){
   if(pendingMerchantRestore.Count==0)return;
   if(Time.unscaledTime>merchantRestoreUntil){pendingMerchantRestore.Clear();return;}
   if(chainsawSaveItems==null||chainsawSaveItems.arrayList==null)return;
   foreach(var entry in chainsawSaveItems.arrayList){
    var item=entry as GameObject;WeaponMerchantRecord record;
    if(item==null||!pendingMerchantRestore.TryGetValue(item.name,out record))continue;
    if((record.flame&&item.GetComponent<FlamethrowerItem>()!=null)||(!record.flame&&item.GetComponent<ChainsawItem>()!=null)){
     ApplyWeaponMerchantSale(item,record);pendingMerchantRestore.Remove(item.name);
     if(pendingMerchantRestore.Count==0)break;
    }
   }
  }
  void SaveWeaponMerchantStock(ES3Settings settings){
   if(chainsawSaveItems!=null&&chainsawSaveItems.arrayList!=null)foreach(var entry in chainsawSaveItems.arrayList){var item=entry as GameObject;var marker=item==null?null:item.GetComponent<WeaponMerchantSale>();if(marker!=null&&marker.Record!=null)marker.Record.forSale=marker.ForSale;}
   ES3.Save<string>(WeaponMerchantSaveKey,JsonUtility.ToJson(new WeaponMerchantSave{stock=weaponMerchantRecords.Values.OrderBy(r=>r.key,StringComparer.Ordinal).ToArray()}),settings);
  }
  void LoadWeaponMerchantStock(ES3Settings settings){
   weaponMerchantRecords.Clear();pendingMerchantRestore.Clear();merchantRestoreUntil=Time.unscaledTime+15f;if(!ES3.KeyExists(WeaponMerchantSaveKey,settings))return;
   var saved=JsonUtility.FromJson<WeaponMerchantSave>(ES3.Load<string>(WeaponMerchantSaveKey,settings));if(saved==null||saved.version!=1||saved.stock==null)return;
   foreach(var record in saved.stock)if(record!=null&&!string.IsNullOrEmpty(record.key)){
    weaponMerchantRecords[record.key]=record;
    if(record.created&&!string.IsNullOrEmpty(record.itemName))pendingMerchantRestore[record.itemName]=record;
   }
  }
  void ResetWeaponMerchantStock(){weaponMerchants.Clear();weaponMerchantRecords.Clear();pendingMerchantRestore.Clear();}
 }
}
