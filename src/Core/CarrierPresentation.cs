using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  Transform suppressedCarrier;
  readonly Dictionary<Renderer,bool> carrierRenderStates=new Dictionary<Renderer,bool>();
  readonly Dictionary<PlayMakerFSM,bool> carrierFsmStates=new Dictionary<PlayMakerFSM,bool>();
  void SuppressNativeCarrierPresentation(){
   if(nativeView==null)return;
   if(suppressedCarrier!=nativeView){
    RestoreNativeCarrierPresentation();suppressedCarrier=nativeView;
    foreach(var renderer in nativeView.GetComponentsInChildren<Renderer>(true))carrierRenderStates[renderer]=renderer.enabled;
    foreach(var fsm in nativeView.GetComponentsInChildren<PlayMakerFSM>(true))carrierFsmStates[fsm]=fsm.enabled;
   }
   foreach(var pair in carrierRenderStates)if(pair.Key!=null)pair.Key.enabled=false;
   foreach(var pair in carrierFsmStates)if(pair.Key!=null&&pair.Key.enabled)pair.Key.enabled=false;
  }
  void RestoreNativeCarrierPresentation(){
   foreach(var pair in carrierRenderStates)if(pair.Key!=null)pair.Key.enabled=pair.Value;
   foreach(var pair in carrierFsmStates)if(pair.Key!=null)pair.Key.enabled=pair.Value;
   carrierRenderStates.Clear();carrierFsmStates.Clear();suppressedCarrier=null;
  }
 }
}
