using System;
using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  Transform stockParent;MonoBehaviour[] stockIK;System.Reflection.MethodInfo stockSolve;float nextStockScan;bool loggedStockTiming;
  internal void UpdateStockWeaponTiming(){
   if(enabledPreview||Time.timeScale<=0f||Cursor.lockState!=CursorLockMode.Locked)return;
   if(stockParent==null || stockIK==null){
    if(Time.unscaledTime<nextStockScan)return;nextStockScan=Time.unscaledTime+2f;
    var sway=Resources.FindObjectsOfTypeAll<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="Sway"&&f.gameObject.name=="Parent"&&f.GetComponentInParent<Camera>()!=null);
    if(sway==null)return;stockParent=sway.transform;stockIK=stockParent.GetComponentsInChildren<MonoBehaviour>(true).Where(m=>m.GetType().Name=="FastIKFabric"&&!m.transform.GetComponentsInParent<Transform>(true).Any(t=>t.name.StartsWith("Chainsaw.")||t.name.StartsWith("AnimationEditor."))).ToArray();
    if(stockIK.Length>0)stockSolve=AccessTools.Method(stockIK[0].GetType(),"LateUpdate");
    if(!loggedStockTiming){Logger.LogInfo("Shared stock-weapon final IK: "+stockIK.Length+" solvers across native weapon rigs.");loggedStockTiming=true;}
   }
   foreach(var ik in stockIK)if(ik!=null&&ik.enabled&&ik.gameObject.activeInHierarchy)stockSolve.Invoke(ik,null);
  }
 }
}
