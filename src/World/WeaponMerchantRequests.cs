using System;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 // Inject request data into the existing board generation. Native ItemList
 // creates/disposes the rows; SellItemsHere matches names, pays and removes
 // the delivered physical weapon through its normal E confirmation.
 public sealed class WeaponMerchantRequests:MonoBehaviour {
  internal readonly Queue<GameObject> Offers=new Queue<GameObject>();
  internal FsmGameObject Item;internal GameObject Saw,Flame;
  internal void Roll(){
   Offers.Clear();
   bool saw=UnityEngine.Random.value<.1f,flame=UnityEngine.Random.value<.1f;
   if(saw){SetOffer(Saw,UnityEngine.Random.Range(2000,3001));Offers.Enqueue(Saw);}
   if(flame){SetOffer(Flame,UnityEngine.Random.Range(2000,3001));Offers.Enqueue(Flame);}
  }
  internal static void SetOffer(GameObject request,int price){
   var value=request.GetComponents<PlayMakerFSM>().First(f=>f.FsmName=="Value");value.FsmVariables.FindFsmInt("SellValue").Value=price;
  }
  internal void Select(){if(Offers.Count>0)Item.Value=Offers.Dequeue();}
 }
 public sealed class RollWeaponMerchantRequests:FsmStateAction {
  internal WeaponMerchantRequests Requests;
  public override void OnEnter(){Requests.Roll();Finish();}
 }
 public sealed class SelectWeaponMerchantRequest:FsmStateAction {
  internal WeaponMerchantRequests Requests;
  public override void OnEnter(){Requests.Select();Finish();}
 }
 public partial class Plugin {
  GameObject merchantRequestTemplates;readonly List<WeaponMerchantRequests> merchantRequestBoards=new List<WeaponMerchantRequests>();
  static bool IsSlumtownRequestBoard(PlayMakerFSM fsm){
   return fsm!=null&&fsm.FsmName=="ItemList"&&fsm.name=="Sell_Items"&&fsm.gameObject.scene.IsValid()&&fsm.gameObject.scene.isLoaded&&fsm.transform.parent!=null&&NativePoiName(fsm.transform.parent.name,"Camp_15");
  }
  GameObject MakeMerchantRequest(string name,Transform parent){return MerchantRequestFactory.Create(name,parent);}
  void RememberWeaponMerchantRequests(PlayMakerFSM fsm){
   if(!IsSlumtownRequestBoard(fsm)||fsm.GetComponent<WeaponMerchantRequests>()!=null)return;
   var random=fsm.Fsm.States.FirstOrDefault(s=>s.Name=="random");var get=fsm.Fsm.States.FirstOrDefault(s=>s.Name=="getItem");var item=fsm.FsmVariables.FindFsmGameObject("Item");
   if(random==null||get==null||item==null)return;
   var selection=Array.FindIndex(get.Actions,a=>a.GetType().Name=="ArrayListGetRandomCurvedWeighted");if(selection<0)return;
   if(merchantRequestTemplates==null){merchantRequestTemplates=new GameObject("WeaponsPack.MerchantRequestData");merchantRequestTemplates.SetActive(false);DontDestroyOnLoad(merchantRequestTemplates);}
   var requests=fsm.gameObject.AddComponent<WeaponMerchantRequests>();requests.Item=item;
   requests.Saw=MakeMerchantRequest("Chainsaw",merchantRequestTemplates.transform);requests.Flame=MakeMerchantRequest("Flamethrower",merchantRequestTemplates.transform);
   var roll=new RollWeaponMerchantRequests{Requests=requests};roll.Init(random);
   random.Actions=random.Actions.Concat(new FsmStateAction[]{roll}).ToArray();
   var choose=new SelectWeaponMerchantRequest{Requests=requests};choose.Init(get);
   var actions=get.Actions.ToList();actions.Insert(selection+1,choose);get.Actions=actions.ToArray();
   merchantRequestBoards.Add(requests);
   // If this native board already completed its current list, the additions
   // participate in its next normal refresh rather than disturbing live rows.
  }
  void ClearWeaponMerchantRequests(){
   foreach(var board in merchantRequestBoards)if(board!=null){
    var fsm=board.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="ItemList");
    if(fsm!=null)foreach(var state in fsm.Fsm.States)state.Actions=state.Actions.Where(a=>!(a is RollWeaponMerchantRequests)&&!(a is SelectWeaponMerchantRequest)).ToArray();
    board.Offers.Clear();Destroy(board);
   }
   if(merchantRequestTemplates!=null)Destroy(merchantRequestTemplates);merchantRequestTemplates=null;
   merchantRequestBoards.Clear();
  }
 }
}
