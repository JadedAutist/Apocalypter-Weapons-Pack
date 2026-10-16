using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HutongGames.PlayMaker;
using UnityEngine;

[BepInPlugin("weaponspack.local.holdingchecks","Local Holding Checks","1.0.0")]
public sealed class HoldingEngineChecks:BaseUnityPlugin {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 string root,variant;int checks;
 static object Get(object o,string n){return o.GetType().GetField(n,All).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,All).SetValue(o,v);}
 static object Call(object o,string n,params object[] args){try{return o.GetType().GetMethod(n,All).Invoke(o,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Logger.LogInfo("PASS: "+text);}
 void Awake(){
  root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Info.Location),"../../.."));var args=Environment.GetCommandLineArgs();variant=args[Array.IndexOf(args,"--holding-variant")+1];
  try{Run();File.WriteAllText(Path.Combine(root,"result-"+variant+".txt"),"PASS: "+checks+" native holding checks for "+variant+".\n");}
  catch(Exception e){Logger.LogError(e);File.WriteAllText(Path.Combine(root,"result-"+variant+".txt"),"FAIL: "+e);}
  Application.Quit();
 }
 void Run(){
  foreach(var boot in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())if(boot.gameObject.scene.IsValid()&&boot.Fsm==null)DestroyImmediate(boot);
  var pack=Assembly.LoadFrom(Path.Combine(root,"lib",variant,"ChainsawPrototype.dll"));var type=pack.GetType("ChainsawPrototype.Plugin");
  var inactive=new GameObject("InactivePack");inactive.SetActive(false);var owner=inactive.AddComponent(type);
  var player=new GameObject("Player");player.SetActive(false);var health=player.AddComponent<PlayMakerFSM>();health.Reset();health.enabled=false;health.FsmName="Health";
  var hp=new FsmFloat{Value=100f};hp.SetName("Health");health.FsmVariables.FloatVariables=new[]{hp};health.FsmVariables.Reinitialize();
  health.Fsm.States=new[]{new FsmState(health.Fsm){Name="playerHealth",Actions=new FsmStateAction[0]},new FsmState(health.Fsm){Name="playerDeath",Actions=new FsmStateAction[0]}};
  health.Fsm.Init(health);player.SetActive(true);
  var view=new GameObject("PlayerCamera");var camera=view.AddComponent<Camera>();camera.enabled=false;
  var native=new GameObject("WeaponsArm");native.transform.SetParent(view.transform,false);
  var rig=new GameObject("Chainsaw.HeldRig");rig.transform.SetParent(view.transform,false);var skin=rig.AddComponent<SkinnedMeshRenderer>();
  var model=new GameObject("Chainsaw.HeldModel");model.transform.SetParent(view.transform,false);model.AddComponent<MeshRenderer>();
  Set(owner,"camera",view.transform);Set(owner,"weapon",rig.transform);Set(owner,"preview",model);Set(owner,"enabledPreview",true);
  Call(owner,"ProtectChainsawRender",camera);Check(model.activeSelf&&skin.enabled,"healthy chainsaw can render");
  native.SetActive(false);Call(owner,"ProtectChainsawRender",camera);
  if(variant=="baseline"){Check(model.activeSelf&&rig.activeSelf,"negative control: old render callback ignores hidden native root");return;}
  Check(!model.activeSelf&&!rig.activeSelf&&!(bool)Get(owner,"enabledPreview"),"native weapon hide immediately stops private rig in pre-render");
  Call(owner,"ProtectChainsawRender",camera);Call(owner,"LatePose");Check(!model.activeSelf&&!rig.activeSelf,"later render and pose callbacks cannot resurrect hidden chainsaw");
  native.SetActive(true);Check((bool)Call(owner,"CanPresentHeldWeapon",view.transform),"native visibility restored for a healthy player");
  Set(owner,"enabledPreview",true);model.SetActive(true);rig.SetActive(true);hp.Value=0;Call(owner,"LatePose");
  Check(!model.activeSelf&&!rig.activeSelf&&!(bool)Get(owner,"enabledPreview"),"zero health stops held pose even before native root is hidden");
  hp.Value=100;health.Fsm.SetState("playerDeath");Check(!(bool)Call(owner,"CanPresentHeldWeapon",view.transform),"native death state wins even if another mod restored health");
  health.Fsm.SetState("playerHealth");Check((bool)Call(owner,"CanPresentHeldWeapon",view.transform),"return to healthy state permits presentation");
  float scale=Time.timeScale;Time.timeScale=0;Check((bool)Call(owner,"CanPresentHeldWeapon",view.transform),"ordinary pause is not mistaken for death");Time.timeScale=scale;
  Set(owner,"cameraTurnSway",null);Call(owner,"ResetHeldMotion");view.transform.position=Vector3.right*.1f;Call(owner,"UpdateHeldMotion");
  var sampled=Get(owner,"swayPosition");var position=Get(owner,"previousHeldPosition");view.transform.position=Vector3.right*.2f;Call(owner,"UpdateHeldMotion");
  Check(sampled.Equals(Get(owner,"swayPosition"))&&position.Equals(Get(owner,"previousHeldPosition")),"chainsaw motion sampled once despite repeated same-frame calls");
  var hand=new GameObject("HandItemUse");hand.transform.SetParent(view.transform,false);var itemGo=new GameObject("Flamethrower");itemGo.transform.SetParent(hand.transform,false);var marker=itemGo.AddComponent(pack.GetType("ChainsawPrototype.FlamethrowerItem"));
  var flameRig=new GameObject("Flamethrower.HeldRig");flameRig.transform.SetParent(view.transform,false);var flameModel=new GameObject("Flamethrower.HeldModel");flameModel.transform.SetParent(flameRig.transform,false);
  Set(marker,"HoldingRig",flameRig.transform);Set(marker,"HoldingModel",flameModel.transform);Set(owner,"activeFlame",marker);Set(owner,"flamePlayer",view);Set(owner,"flameFiring",true);Set(owner,"flameDamagePending",true);
  hp.Value=0;Call(owner,"CompleteFlamethrowerHolding",marker,null);
  Check(!flameRig.activeSelf&&Get(owner,"activeFlame")==null&&!(bool)Get(owner,"flameFiring")&&!(bool)Get(owner,"flameDamagePending"),"flamethrower holding callback hides rig and cancels firing/damage on death");
  hp.Value=100;DestroyImmediate(view);var next=new GameObject("PlayerCamera");new GameObject("WeaponsArm").transform.SetParent(next.transform,false);
  Check((bool)Call(owner,"CanPresentHeldWeapon",next.transform),"replacement camera binds fresh presentation state after reload");
 }
}
