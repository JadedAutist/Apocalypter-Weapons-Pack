using System;
using System.Collections;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
namespace ChainsawPrototype {
 // Only the exact flame-owned corpse changes. Native spawning, counts and saves stay native.
 internal static class FlameCookedHarvest {
  const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
  static readonly FieldInfo names=typeof(ActionData).GetField("actionNames",Fields),enabled=typeof(ActionData).GetField("actionEnabled",Fields),starts=typeof(ActionData).GetField("actionStartIndex",Fields),paramNames=typeof(ActionData).GetField("paramName",Fields),paramTypes=typeof(ActionData).GetField("paramDataType",Fields),positions=typeof(ActionData).GetField("paramDataPos",Fields),objects=typeof(ActionData).GetField("fsmGameObjectParams",Fields);
  internal static int Apply(GameObject corpse,Action<string> warning){
   bool pending;int converted=ApplyReady(corpse,warning,out pending);if(pending)FlameCookedHarvestRetry.Queue(corpse,warning);return converted;
  }
  internal static int ApplyReady(GameObject corpse,Action<string> warning,out bool pending){
   pending=false;
   if(corpse==null||corpse.GetComponent<FlameBurntCorpse>()==null)return 0;
   int converted=0;bool warned=false;
   foreach(var component in corpse.GetComponents<PlayMakerFSM>()){
    var fsm=component.Fsm;if(fsm==null||fsm.Name!="Carcasse")continue;
    if(!fsm.Initialized){pending=true;continue;}
    if(!ReferenceEquals(fsm.Owner,component)||fsm.GameObject!=corpse)continue;
    if(fsm.States==null||fsm.States.Length==0){pending=true;continue;}
    foreach(var state in fsm.States){
     if(state==null)continue;
     if(state.Fsm==null){pending=true;continue;}if(!ReferenceEquals(state.Fsm,fsm))continue;
     if(!state.ActionsLoaded){pending=true;continue;}
     foreach(var action in state.Actions){
      var create=action as CreateObject;
      if(create==null||!create.Enabled||(create.State!=null&&(!ReferenceEquals(create.State,state)||!ReferenceEquals(create.Fsm,fsm)))||create.gameObject==null||create.gameObject.UseVariable)continue;
      var raw=create.gameObject.Value;if(raw==null||!RawMeat(raw.name))continue;
      try{
       var cooked=Cooked(raw);
       if(cooked==null){if(!warned&&warning!=null){warned=true;warning("Native cooked harvest mapping is missing or ambiguous; retaining this corpse's original loot.");}continue;}
       var selection=(FsmGameObject)create.gameObject.Clone();selection.Value=cooked;create.gameObject=selection;converted++;
      }catch(Exception error){if(!warned&&warning!=null){warned=true;warning("Native cooked harvest mapping unavailable: "+error.Message);}}
     }
    }
   }
   return converted;
  }
  static bool RawMeat(string name){return name=="meat_dog_raw"||name=="meat_insect_raw"||name=="meat_mutant_raw";}
  static GameObject Cooked(GameObject raw){
   GameObject result=null;int candidates=0;
   foreach(var component in raw.GetComponents<PlayMakerFSM>()){
    var fsm=component.Fsm;if(fsm==null||fsm.Name!="Cook"||fsm.States==null)continue;
    foreach(var state in fsm.States){
     if(state==null||state.Name!="spawn")continue;
     if(state.ActionsLoaded&&state.Fsm!=null){
      foreach(var action in state.Actions){var create=action as CreateObject;if(create==null||!create.Enabled)continue;if(!Constant(create.gameObject,raw,ref result))return null;candidates++;}
     }else{
      // An asset's FSM need not have awoken. Read its serialized constant without
      // initializing it, loading actions into it, or exposing a partial component.
      var data=state.ActionData;var ns=Read(names,data);var es=Read(enabled,data);var ss=Read(starts,data);var ps=Read(paramNames,data);var ts=Read(paramTypes,data);var ds=Read(positions,data);var os=Read(objects,data);
      if(ns==null||es==null||ss==null||ps==null||ts==null||ds==null||os==null||ns.Count!=es.Count||ns.Count!=ss.Count||ps.Count!=ts.Count||ps.Count!=ds.Count)return null;
      for(int i=0;i<ns.Count;i++){
       if((string)ns[i]!=typeof(CreateObject).FullName||!(bool)es[i])continue;
       int start=(int)ss[i],end=i+1<ss.Count?(int)ss[i+1]:ps.Count;if(start<0||end<start||end>ps.Count)return null;
       FsmGameObject selection=null;int fields=0;
       for(int j=start;j<end;j++)if((string)ps[j]=="gameObject"){
        int at=(int)ds[j];if(Convert.ToInt32(ts[j])!=19||at<0||at>=os.Count)return null;selection=os[at] as FsmGameObject;fields++;
       }
       if(fields!=1||!Constant(selection,raw,ref result))return null;candidates++;
      }
     }
    }
   }
   return candidates==1?result:null;
  }
  static IList Read(FieldInfo field,object owner){return field==null||owner==null?null:field.GetValue(owner) as IList;}
  static bool Constant(FsmGameObject selection,GameObject raw,ref GameObject result){
   if(selection==null||selection.UseVariable||selection.Value==null||selection.Value==raw)return false;
   var value=selection.Value;
   // A mod may change the cooked destination; its current meat choice is authoritative.
   if(!value.name.StartsWith("meat_",StringComparison.Ordinal)||value.name.IndexOf("_cooked",StringComparison.Ordinal)<0)return false;
   result=value;return true;
  }
 }
 // The native corpse may Awake after capture/load. This component owns only a
 // short readiness wait; it never initializes or resets the native FSM itself.
 internal sealed class FlameCookedHarvestRetry:MonoBehaviour {
  Action<string> warning;float nextAttempt,deadline;int attempts;bool finished;
  internal static void Queue(GameObject corpse,Action<string> warning){
   if(corpse==null||corpse.GetComponent<FlameCookedHarvestRetry>()!=null)return;
   var retry=corpse.AddComponent<FlameCookedHarvestRetry>();retry.warning=warning;retry.nextAttempt=Time.unscaledTime+.25f;retry.deadline=Time.unscaledTime+3f;
  }
  void Update(){Advance(Time.unscaledTime);}
  internal void Advance(float now){
   if(finished||now<nextAttempt)return;
   if(now>deadline||attempts>=12){Finish(true);return;}
   attempts++;nextAttempt=now+.25f;bool pending;FlameCookedHarvest.ApplyReady(gameObject,warning,out pending);
   if(!pending)Finish(false);else if(attempts>=12||now>=deadline)Finish(true);
  }
  void Finish(bool timedOut){
   if(finished)return;finished=true;enabled=false;
   if(timedOut&&warning!=null)warning("Native corpse harvest FSM was not ready within three seconds; retaining its original loot.");
   warning=null;Destroy(this);
  }
  void OnDestroy(){finished=true;warning=null;}
 }
}

