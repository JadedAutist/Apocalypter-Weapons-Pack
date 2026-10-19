using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

[BepInPlugin("weaponspack.local.capacitychecks","Local Fuel Capacity Checks","1.0.0")]
public sealed class FuelCapacityEngineChecks:BaseUnityPlugin {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 string root,variant;int checks;Assembly pack;Type plugin,fuelType,tankType,portType,kindType;object owner;
 UnityEngine.SceneManagement.Scene fixtureScene;
 Tuple<GameObject,PlayMakerFSM,Component,Component> extraSaw,extraFlame;
 static object Get(object target,string name){return (target as Type??target.GetType()).GetField(name,All).GetValue(target is Type?null:target);}
 static void Set(object target,string name,object value){(target as Type??target.GetType()).GetField(name,All).SetValue(target is Type?null:target,value);}
 static object Call(object target,string name,params object[] args){try{return (target as Type??target.GetType()).GetMethod(name,All).Invoke(target is Type?null:target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 static float Litres(object fuel){return (float)fuel.GetType().GetProperty("Litres",All).GetValue(fuel,null);}
 static bool Near(float a,float b){return Mathf.Abs(a-b)<.00001f;}
 object Kind(int value){return Enum.ToObject(kindType,value);}
 void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Logger.LogInfo("PASS: "+label);}
 void Awake(){
  root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Info.Location),"../../.."));var args=Environment.GetCommandLineArgs();variant=args[Array.IndexOf(args,"--capacity-variant")+1];
  try{Run();File.WriteAllText(Path.Combine(root,"result-"+variant+".txt"),"PASS: "+checks+" native tank/pour/clamp/save checks for "+variant+". Full physical can collision workflow NOT VERIFIED.\n");}
  catch(Exception e){Logger.LogError(e);File.WriteAllText(Path.Combine(root,"result-"+variant+".txt"),"FAIL: "+e+"\n");}
  Application.Quit();
 }
 FsmFloat Number(string name,float value){var number=new FsmFloat{Value=value};number.SetName(name);return number;}
 PlayMakerFSM Liquid(GameObject go,float amount,float capacity){var fsm=go.AddComponent<PlayMakerFSM>();fsm.Reset();fsm.enabled=false;fsm.FsmName="LiquidAmount";fsm.FsmVariables.FloatVariables=new[]{Number("Liquid",amount),Number("LiquidCapacity",capacity)};fsm.FsmVariables.Reinitialize();fsm.Fsm.States=new[]{new FsmState(fsm.Fsm){Name="Idle",Actions=new FsmStateAction[0]}};fsm.Fsm.Init(fsm);return fsm;}
 Tuple<GameObject,PlayMakerFSM,Component,Component> Tank(bool flame,float amount){var item=new GameObject((flame?"CapacityFlame":"CapacityChainsaw")+Guid.NewGuid().ToString("N"));item.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item,fixtureScene);item.AddComponent(pack.GetType(flame?"ChainsawPrototype.FlamethrowerItem":"ChainsawPrototype.ChainsawItem"));var go=new GameObject("OwnedFuelTank");go.SetActive(false);go.transform.SetParent(item.transform,false);var native=Liquid(go,amount,5f);go.AddComponent<BoxCollider>().isTrigger=true;var fuel=go.AddComponent(fuelType);var tank=flame?go.AddComponent(tankType):null;return Tuple.Create(item,native,fuel,tank);}
 PlayMakerFSM Can(int kind,float amount,Component port,float requested){
  var go=new GameObject("NativeCan."+kind+"."+checks);go.SetActive(false);Liquid(go,amount,10f);var fsm=go.AddComponent<PlayMakerFSM>();fsm.Reset();fsm.enabled=false;fsm.FsmName="UseCan";
  var hit=new FsmGameObject{Value=port.gameObject};hit.SetName("CollidedObj");fsm.FsmVariables.GameObjectVariables=new[]{hit};fsm.FsmVariables.FloatVariables=new[]{Number("CanLiquid",amount),Number("Liquid",Litres(Get(port,"Tank"))),Number("LiquidCapacity",10f)};fsm.FsmVariables.Reinitialize();
  var tag=new TriggerEvent{collideTag=new FsmString{Value=kind==1?"Diesel":kind==2?"MotorOil":"Gasoline"}};
  var debit=new FloatAdd{add=new FsmFloat{Value=-requested},perSecond=false,everyFrame=true};var write=new SetFsmFloat{everyFrame=true};
  fsm.Fsm.States=new[]{new FsmState(fsm.Fsm){Name="Idle",Actions=new FsmStateAction[0]},new FsmState(fsm.Fsm){Name="CheckTrigger",Actions=new FsmStateAction[0]},new FsmState(fsm.Fsm){Name="Fill",Actions=new FsmStateAction[0]}};fsm.Fsm.Init(fsm);fsm.Fsm.SetState("Fill");
  fsm.Fsm.GetState("CheckTrigger").Actions=new FsmStateAction[]{tag};fsm.Fsm.GetState("Fill").Actions=new FsmStateAction[]{debit,write};foreach(var state in fsm.Fsm.States)foreach(var action in state.Actions)action.Init(state);
  if(fsm.Fsm.ActiveStateName!="Fill")throw new Exception("Native fill fixture was not initialized.");
  // Add actions after PlayMaker's Init load, then invoke initialized native callbacks.
  foreach(var action in fsm.Fsm.ActiveState.Actions){Call(action,"OnEnter");Call(action,"OnUpdate");}return fsm;
 }
 Component Port(Component tank,PlayMakerFSM native,int kind){var go=kind==0?tank.gameObject:new GameObject("OwnedPort."+kind);if(kind!=0){go.SetActive(false);go.transform.SetParent(tank.transform,false);Liquid(go,0f,5f);go.AddComponent<BoxCollider>().isTrigger=true;}var port=go.AddComponent(portType);Set(port,"Tank",tank);Set(port,"Kind",Kind(kind));Call(port,"Sync");return port;}
 void Clamp(PlayMakerFSM native){var state=new FsmState(native.Fsm){Name="NativeClamp"};var clamp=new FloatClamp{floatVariable=native.FsmVariables.FindFsmFloat("Liquid"),minValue=new FsmFloat{Value=0f},maxValue=native.FsmVariables.FindFsmFloat("LiquidCapacity"),everyFrame=false};clamp.Init(state);clamp.OnEnter();}
 void Run(){
  foreach(var boot in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())if(boot.gameObject.scene.IsValid()&&boot.Fsm==null)DestroyImmediate(boot);
  pack=Assembly.LoadFrom(Path.Combine(root,"lib",variant,"ChainsawPrototype.dll"));plugin=pack.GetType("ChainsawPrototype.Plugin");fuelType=pack.GetType("ChainsawPrototype.ChainsawFuel");tankType=pack.GetType("ChainsawPrototype.FlamethrowerFuelTank");portType=pack.GetType("ChainsawPrototype.FlamethrowerFuelPort");kindType=pack.GetType("ChainsawPrototype.FlameFuelKind");
  fixtureScene=UnityEngine.SceneManagement.SceneManager.CreateScene("FuelCapacityFixture");
  var inactive=new GameObject("InactiveCapacityPlugin");inactive.SetActive(false);owner=inactive.AddComponent(plugin);Set(plugin,"instance",owner);Call(owner,"SetupCombat");Call(owner,"SetupFlameFuelBalance");Set(owner,"harmony",new Harmony("weaponspack.local.capacitychecks.pour"));Call(owner,"SetupFlamethrowerFuelPouring");
  if(Environment.GetCommandLineArgs().Contains("--capacity-save-only")){var savedSaw=Tank(false,5f);var savedFlame=Tank(true,5f);foreach(float amount in new[]{5f,9.8f})SaveRoundTrip(savedSaw,savedFlame,amount);CheckLegacyPayloads(savedSaw,savedFlame);return;}
  var saw=Tank(false,8f);Check(Near(Litres(saw.Item3),8f),"shared tank reads 8 L above former 5 L limit");
  Check(Near(saw.Item2.FsmVariables.FindFsmFloat("LiquidCapacity").Value,10f),"owned legacy receiver capacity expands without changing contents");Clamp(saw.Item2);Check(Near(Litres(saw.Item3),8f),"native FloatClamp retains fuel above 5 L");
  saw.Item2.FsmVariables.FindFsmFloat("Liquid").Value=12f;Call(saw.Item3,"LateUpdate");Check(Near(Litres(saw.Item3),10f),"shared tank safely clamps overfilled contents to 10 L");
  saw.Item2.FsmVariables.FindFsmFloat("Liquid").Value=.000001f;float spent=(float)Call(saw.Item3,"ConsumeContinuous",.1f);Check(spent==.000001f&&Litres(saw.Item3)==0f,"tiny final fraction is fully consumed without rounding");
  var flame=Tank(true,0f);var ports=Enumerable.Range(0,3).Select(i=>Port(flame.Item4,flame.Item2,i)).ToArray();
  for(int kind=0;kind<3;kind++){
   Call(flame.Item4,"Restore",Kind(kind),5f);foreach(var port in ports)Call(port,"Sync");Check(ports[kind].GetComponent<BoxCollider>().enabled,"matching inlet remains open at old full amount: "+kind);
   var can=Can(kind,8f,ports[kind],6f);foreach(var port in ports)Call(port,"Sync");Clamp(flame.Item2);
   Check(Near(Litres(flame.Item4),10f)&&Near(can.GetComponents<PlayMakerFSM>().Single(f=>f.FsmName=="LiquidAmount").FsmVariables.FindFsmFloat("Liquid").Value,3f),"actual native can Fill callbacks conserve fuel from 5 to 10 L: "+kind+"; tank="+Litres(flame.Item4)+"; can="+can.GetComponents<PlayMakerFSM>().Single(f=>f.FsmName=="LiquidAmount").FsmVariables.FindFsmFloat("Liquid").Value+"; actions="+string.Join(",",can.Fsm.ActiveState.Actions.Select(a=>a.GetType().Name).ToArray()));
   Check(Near(can.FsmVariables.FindFsmFloat("LiquidCapacity").Value,10f)&&!ports[kind].GetComponent<BoxCollider>().enabled,"pour cache and full inlet use 10 L: "+kind);
   Call(flame.Item4,"Restore",Kind(kind),9.8f);var near=Can(kind,1f,ports[kind],1f);Check(Near(Litres(flame.Item4),10f)&&Near(near.GetComponents<PlayMakerFSM>().Single(f=>f.FsmName=="LiquidAmount").FsmVariables.FindFsmFloat("Liquid").Value,.8f),"near-full pour transfers only remaining 0.2 L: "+kind);
   Call(flame.Item4,"Restore",Kind(kind),5f);var wrong=Can((kind+1)%3,1f,ports[(kind+1)%3],1f);Check(Near(Litres(flame.Item4),5f)&&Near(wrong.GetComponents<PlayMakerFSM>().Single(f=>f.FsmName=="LiquidAmount").FsmVariables.FindFsmFloat("Liquid").Value,1f),"wrong fuel leaves tank and source unchanged: "+kind);
  }
  foreach(float amount in new[]{5f,9.8f})SaveRoundTrip(saw,flame,amount);
  CheckLegacyPayloads(saw,flame);
  CheckLegacyWorldSchema();
  var policy=pack.GetType("ChainsawPrototype.WeaponSpawnFuelPolicy");Check(Near((float)Call(policy,"Litres",.95f,.5f),5f)&&Near((float)Call(policy,"Litres",.5f,1f),3f),"new spawn amounts retain existing 0.5-3 or 5 L policy");
  Call(flame.Item4,"Restore",Kind(0),9.8f);Check(((string)Call(plugin,"FlamethrowerFuelDetails",flame.Item1)).Contains("9.80 / 10 L"),"flamethrower inventory details show actual 10 L capacity");
 }
 void SaveRoundTrip(Tuple<GameObject,PlayMakerFSM,Component,Component> saw,Tuple<GameObject,PlayMakerFSM,Component,Component> flame,float amount){
  if(extraSaw==null){extraSaw=Tank(false,0f);extraFlame=Tank(true,0f);}float second=amount==5f?2.35f:7.25f;
  saw.Item2.FsmVariables.FindFsmFloat("Liquid").Value=amount;Call(flame.Item4,"Restore",Kind(2),amount);
  extraSaw.Item2.FsmVariables.FindFsmFloat("Liquid").Value=second;Call(extraFlame.Item4,"Restore",Kind(1),second);
  var areas=(IDictionary)Get(owner,"chainsawAreaRolls");areas.Clear();var roll=Activator.CreateInstance(pack.GetType("ChainsawPrototype.ChainsawAreaRoll"));Set(roll,"key","capacity-area");Set(roll,"spawn",false);Set(roll,"anchorIndex",2);areas["capacity-area"]=roll;Set(owner,"chainsawCodexDiscovered",true);
  var flameAreas=(IDictionary)Get(owner,"flameWorldSpawnRecords");flameAreas.Clear();var flameRoll=Activator.CreateInstance(pack.GetType("ChainsawPrototype.FlameWorldSpawnRecord"));Set(flameRoll,"key","capacity-flame-area");Set(flameRoll,"anchor","anchor-2");Set(flameRoll,"itemName","");Set(flameRoll,"pending",false);flameAreas["capacity-flame-area"]=flameRoll;
  var stock=(IDictionary)Get(owner,"weaponMerchantRecords");stock.Clear();var sale=Activator.CreateInstance(pack.GetType("ChainsawPrototype.WeaponMerchantRecord"));Set(sale,"key","capacity-shop");Set(sale,"itemName","");Set(sale,"flame",true);Set(sale,"spawn",false);Set(sale,"created",false);Set(sale,"forSale",false);Set(sale,"price",3456);stock["capacity-shop"]=sale;
  var listGo=new GameObject("NativeCapacitySaveList");listGo.SetActive(false);var items=listGo.AddComponent<PlayMakerArrayListProxy>();Call(items,"Awake");foreach(var item in new[]{saw.Item1,flame.Item1,extraSaw.Item1,extraFlame.Item1})items.arrayList.Add(item);Set(owner,"chainsawSaveItems",items);Set(owner,"chainsawItemCatalog",items);
  string path=Path.Combine(root,"fuel-"+variant+"-"+amount.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".es3");var settings=new ES3Settings(path,ES3.Location.File);settings.encryptionType=ES3.EncryptionType.None;settings.compressionType=ES3.CompressionType.None;
  ES3.Save<int>("Sentinel",42,settings);Call(owner,"SaveNativeChainsawData",settings);
  string sawJson=ES3.Load<string>("ApocalypterWeaponsPack.Chainsaw",settings),flameJson=ES3.Load<string>("ApocalypterWeaponsPack.FlamethrowerFuel",settings);
  saw.Item2.FsmVariables.FindFsmFloat("Liquid").Value=0f;extraSaw.Item2.FsmVariables.FindFsmFloat("Liquid").Value=0f;Call(flame.Item4,"Restore",Kind(0),0f);Call(extraFlame.Item4,"Restore",Kind(0),0f);
  Call(owner,"LoadNativeChainsawData",settings);Call(owner,"RestorePendingChainsawFuel");Call(owner,"RestorePendingFlamethrowerFuel");
  Check(Near(Litres(saw.Item3),amount)&&Near(Litres(flame.Item4),amount)&&(int)Get(flame.Item4,"Kind")==2&&ES3.Load<int>("Sentinel",settings)==42,"actual native save/load preserves both tanks and fuel kind at "+amount+" L; saw="+Litres(saw.Item3)+"; flame="+Litres(flame.Item4)+"; kind="+Get(flame.Item4,"Kind"));
  Check(Near(Litres(extraSaw.Item3),second)&&Near(Litres(extraFlame.Item4),second)&&(int)Get(extraFlame.Item4,"Kind")==1,"multiple fuel records retain their independent amount and kind");
  Check(sawJson.Contains("\"version\":1")&&sawJson.Contains("\"fuel\"")&&sawJson.Contains("\"areas\"")&&flameJson.Contains("\"tanks\"")&&areas.Contains("capacity-area")&&(int)Get(areas["capacity-area"],"anchorIndex")==2&&!(bool)Get(areas["capacity-area"],"spawn")&&(bool)Get(owner,"chainsawCodexDiscovered"),"same version/member names preserve area rolls and Codex discovery");
  Check(flameAreas.Contains("capacity-flame-area")&&!(bool)Get(flameAreas["capacity-flame-area"],"pending")&&(string)Get(flameAreas["capacity-flame-area"],"anchor")=="anchor-2","flamethrower failed spawn roll survives actual save/load without reroll");
  Check(stock.Contains("capacity-shop")&&!(bool)Get(stock["capacity-shop"],"spawn")&&(int)Get(stock["capacity-shop"],"price")==3456,"merchant stock decision and price survive actual save/load");
 }
 void CheckLegacyPayloads(Tuple<GameObject,PlayMakerFSM,Component,Component> saw,Tuple<GameObject,PlayMakerFSM,Component,Component> flame){
  var settings=new ES3Settings(Path.Combine(root,"legacy-"+variant+".es3"),ES3.Location.File);settings.encryptionType=ES3.EncryptionType.None;settings.compressionType=ES3.CompressionType.None;
  string sawKey="ApocalypterWeaponsPack.Chainsaw",flameKey="ApocalypterWeaponsPack.FlamethrowerFuel";
  ES3.Save<string>(sawKey,"{\"version\":1,\"codexDiscovered\":true,\"fuel\":[{\"name\":\""+saw.Item1.name+"\",\"litres\":5.0}],\"areas\":[{\"key\":\"legacy-area\",\"spawn\":false,\"anchorIndex\":3}]}",settings);
  ES3.Save<string>(flameKey,"{\"version\":1,\"tanks\":[{\"name\":\""+flame.Item1.name+"\",\"litres\":9.8,\"kind\":1}]}",settings);
  Call(owner,"LoadNativeChainsawData",settings);Call(owner,"RestorePendingChainsawFuel");Call(owner,"RestorePendingFlamethrowerFuel");Check(Near(Litres(saw.Item3),5f)&&Near(Litres(flame.Item4),9.8f)&&(int)Get(flame.Item4,"Kind")==1&&((IDictionary)Get(owner,"chainsawAreaRolls")).Contains("legacy-area"),"valid legacy JSON restores amounts, fuel kind and area records");
  foreach(string payload in new[]{"{\"version\":1}","{\"version\":1,\"fuel\":[],\"areas\":[]}","{\"version\":2,\"fuel\":[{\"name\":\""+saw.Item1.name+"\",\"litres\":1.0}]}"}){
   string flamePayload=payload.Replace("\"fuel\"","\"tanks\"");ES3.Save<string>(sawKey,payload,settings);ES3.Save<string>(flameKey,flamePayload,settings);Call(owner,"LoadNativeChainsawData",settings);Call(owner,"RestorePendingChainsawFuel");Call(owner,"RestorePendingFlamethrowerFuel");
   Check(Near(Litres(saw.Item3),5f)&&Near(Litres(flame.Item4),9.8f)&&((IList)Get(owner,"chainsawPendingFuel")).Count==0&&((IList)Get(owner,"pendingFlameTanks")).Count==0,"missing/empty arrays or unsupported version do not invent or replace fuel: "+payload);
   Check(ES3.Load<string>(sawKey,settings)==payload&&ES3.Load<string>(flameKey,settings)==flamePayload,"loading leaves stored legacy payload unchanged");
  }
 }
 void CheckLegacyWorldSchema(){
  var json=pack.GetType("ChainsawPrototype.WeaponSaveJson");var read=json.GetMethod("Deserialize",All);var write=json.GetMethod("Serialize",All);
  var type=pack.GetType("ChainsawPrototype.FlameWorldSpawnSave");
  const string legacy="{\"version\":1,\"areas\":[{\"key\":\"legacy-world\",\"anchor\":\"anchor-4\",\"itemName\":\"saved-weapon\",\"pending\":true}],\"testSites\":[{\"laboratory\":true,\"position\":{\"x\":1.25,\"y\":2.5,\"z\":-3.75},\"rotation\":{\"x\":0,\"y\":0.6,\"z\":0,\"w\":0.8}}]}";
  var data=read.MakeGenericMethod(type).Invoke(null,new object[]{legacy});var areas=(Array)Get(data,"areas");var sites=(Array)Get(data,"testSites");var site=sites.GetValue(0);
  Check(areas.Length==1&&(bool)Get(areas.GetValue(0),"pending")&&(string)Get(areas.GetValue(0),"itemName")=="saved-weapon"&&sites.Length==1&&(bool)Get(site,"laboratory")&&(Vector3)Get(site,"position")==new Vector3(1.25f,2.5f,-3.75f)&&(Quaternion)Get(site,"rotation")==new Quaternion(0f,.6f,0f,.8f),"legacy flame metadata retains pending rolls and exact saved site transforms");
  var encoded=(string)write.MakeGenericMethod(type).Invoke(null,new[]{data});var copy=read.MakeGenericMethod(type).Invoke(null,new object[]{encoded});
  var copied=((Array)Get(copy,"testSites")).GetValue(0);Check((Vector3)Get(copied,"position")==new Vector3(1.25f,2.5f,-3.75f)&&(Quaternion)Get(copied,"rotation")==new Quaternion(0f,.6f,0f,.8f),"flame metadata JSON roundtrip preserves native Vector3 and Quaternion fields");
  var merchant=pack.GetType("ChainsawPrototype.WeaponMerchantSave");var shop=read.MakeGenericMethod(merchant).Invoke(null,new object[]{"{\"version\":1,\"stock\":[{\"key\":\"legacy-shop\",\"itemName\":\"saved-item\",\"flame\":true,\"spawn\":true,\"created\":true,\"forSale\":true,\"price\":3678}]}"});
  var record=((Array)Get(shop,"stock")).GetValue(0);Check((string)Get(record,"itemName")=="saved-item"&&(bool)Get(record,"flame")&&(bool)Get(record,"spawn")&&(bool)Get(record,"created")&&(bool)Get(record,"forSale")&&(int)Get(record,"price")==3678,"valid legacy merchant JSON retains every stock field");
 }
}
