using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HutongGames.PlayMaker;
using UnityEngine;

// Exercises production flame queries/damage in an isolated native Unity host.
[BepInPlugin("weaponspack.local.flamewindows","Local Flame Window Checks","1.0.0")]
public sealed class FlameWindowChecks:BaseUnityPlugin {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 readonly List<GameObject> owned=new List<GameObject>();
 readonly List<string> failures=new List<string>();
 int checks;object packOwner;string host;
 object Call(string name,params object[] args){return packOwner.GetType().GetMethod(name,All).Invoke(packOwner,args);}
 void Check(bool ok,string label){checks++;if(!ok){failures.Add(label);Logger.LogWarning("FAIL: "+label);}else Logger.LogInfo("PASS: "+label);}
 GameObject Make(string name,Transform parent=null){var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);owned.Add(go);return go;}
 PlayMakerFSM Fsm(GameObject go,string name,string variable){
  bool active=go.activeSelf;go.SetActive(false);var f=go.AddComponent<PlayMakerFSM>();f.Reset();f.enabled=false;f.FsmName=name;
  var value=new FsmFloat();value.SetName(variable);value.Value=100;f.FsmVariables.FloatVariables=new[]{value};f.FsmVariables.Reinitialize();f.Fsm.Init(f);go.SetActive(active);return f;
 }
 void Awake(){
  host=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Info.Location),"../../.."));
  try{Run();}catch(Exception error){failures.Add(error.ToString());Logger.LogError(error);}
  File.WriteAllText(Path.Combine(host,"result.txt"),(failures.Count==0?"PASS: ":"FAIL: ")+checks+" native flame window checks; failures="+failures.Count+"\n"+string.Join("\n",failures.ToArray()));
  foreach(var go in owned)if(go!=null)DestroyImmediate(go);
  Application.Quit();
 }
 void Run(){
  var pack=Assembly.LoadFrom(Path.Combine(host,"lib/ChainsawPrototype.dll"));
  var inactive=Make("InactivePack");inactive.SetActive(false);packOwner=inactive.AddComponent(pack.GetType("ChainsawPrototype.Plugin",true));
  // Real Bodypart/Health variables, events and collider geometry; no damage mock.
  var enemy=Make("Enemy");enemy.transform.position=new Vector3(0,-.063f,3);enemy.layer=10;
  Fsm(enemy,"Health","Health");var receiver=Fsm(enemy,"Bodypart","Damage");
  enemy.AddComponent<BoxCollider>().size=new Vector3(.5f,.5f,.25f);
  var car=Make("Car");car.tag="Vehicle";
  var assembly=Make("windshield_1_glass(Clone)",car.transform);
  var pane=Make("collider",assembly.transform);pane.layer=16;pane.transform.position=new Vector3(0,0,1.5f);var box=pane.AddComponent<BoxCollider>();box.size=new Vector3(1.2f,1.2f,.04f);
  Physics.SyncTransforms();
  WindowCase("mounted front pane",pane,receiver,true);
  assembly.name="side";pane.name="collider_SeeTrough";WindowCase("mounted side pane",pane,receiver,true);
  assembly.name="door_car_1_L(Clone)";pane.name="collider";car.tag="Untagged";WindowCase("detached car door pane",pane,receiver,true);
  assembly.name="door_truck_2_R";WindowCase("truck door pane",pane,receiver,true);
  car.tag="Vehicle";assembly.name="windshield_2_grid";WindowCase("wire grid",pane,receiver,false);
  assembly.name="metal_frame";WindowCase("vehicle metal",pane,receiver,false);
  assembly.name="windshield_1_glass";pane.layer=0;WindowCase("wrong layer remains solid",pane,receiver,false);
  pane.layer=16;car.tag="Untagged";WindowCase("unqualified glass remains solid",pane,receiver,false);
  car.tag="Vehicle";assembly.name="windshield_2_grid";var glass=Make("windshield_1_glass",assembly.transform);pane.transform.SetParent(glass.transform,true);WindowCase("grid ancestor wins over glass name",pane,receiver,false);
  pane.transform.SetParent(assembly.transform,true);assembly.name="windshield_1_glass";
  // A window exception must never skip a nearer wall/body panel behind it.
  var wall=Make("Car body");wall.transform.position=new Vector3(0,0,2.2f);wall.AddComponent<BoxCollider>().size=new Vector3(1,1,.1f);Physics.SyncTransforms();WindowCase("bodywork behind pane",pane,receiver,false);wall.SetActive(false);
  // Moving/rotated vehicle coordinates are queried at their current pose.
  car.transform.SetPositionAndRotation(new Vector3(2,0,0),Quaternion.Euler(0,20,0));Physics.SyncTransforms();
  var moved=(RaycastHit?)Call("FlameCast",Vector3.zero,Vector3.forward,4f,0f,false);Check(moved!=null&&moved.Value.collider.gameObject==enemy,"moving pane cannot leave stale blocking cache");
  car.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);Physics.SyncTransforms();
  // More unsorted hits than the fixed query buffer must retain nearest cover.
  for(int i=0;i<80;i++){var extra=Make("collider_SeeTrough",car.transform);extra.layer=16;extra.transform.position=new Vector3(0,0,1+i*.01f);extra.AddComponent<BoxCollider>().size=new Vector3(1,1,.002f);}
  wall.SetActive(true);Physics.SyncTransforms();WindowCase("overflow retains solid cover",pane,receiver,false);
 }
 void WindowCase(string label,GameObject pane,PlayMakerFSM receiver,bool passes){
  Physics.SyncTransforms();
  var ray=(RaycastHit?)Call("FlameCast",Vector3.zero,Vector3.forward,4f,0f,false);
  Check(ray!=null&&(ray.Value.collider.gameObject.name=="Enemy")==passes,label+": crosshair query");
  var sphere=(RaycastHit?)Call("FlameCast",Vector3.zero,Vector3.forward,4f,.055f,false);
  Check(sphere!=null&&(sphere.Value.collider.gameObject.name=="Enemy")==passes,label+": stream query");
  object[] path={Vector3.zero,Vector3.forward,4f,.055f,0f,Vector3.zero,.25f};var obstruction=(RaycastHit?)Call("FlameTrajectoryCast",path);
  Check((obstruction==null)==passes,label+": curved cover query");
  float distance=obstruction==null?4f:(float)path[4];
  receiver.FsmVariables.FindFsmFloat("Damage").Value=0;packOwner.GetType().GetField("flamePaidDamage",All).SetValue(packOwner,7f);
  Call("DamageFlameEnemies",Vector3.zero,Vector3.forward,distance);
  Check((receiver.FsmVariables.FindFsmFloat("Damage").Value== -7f)==passes,label+": native enemy damage delivery");
 }
}
