using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

// Native Unity/PlayMaker regression for qualified limb triggers. Optional mode
// invokes the installed WildWasteland Rider helpers without starting its plugin.
[BepInPlugin("weaponspack.local.flamepatrol","Local Flame Patrol Checks","1.0.0")]
public sealed class FlamePatrolChecks:BaseUnityPlugin {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 readonly List<GameObject> owned=new List<GameObject>();
 readonly List<string> failures=new List<string>();
 int checks;object packOwner;string host;bool withPatrol;Type rider;
 sealed class Actor { public GameObject Root;public CapsuleCollider Body;public PlayMakerFSM Health,Bodypart; }
 object Call(string name,params object[] args){try{return packOwner.GetType().GetMethod(name,All).Invoke(packOwner,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 void Check(bool ok,string label){checks++;if(ok)Logger.LogInfo("PASS: "+label);else{failures.Add(label);Logger.LogWarning("FAIL: "+label);}}
 GameObject Make(string name,Transform parent=null){var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);owned.Add(go);return go;}
 PlayMakerFSM Metadata(GameObject go,string name,string variable){
  bool active=go.activeSelf;go.SetActive(false);var f=go.AddComponent<PlayMakerFSM>();f.Reset();f.enabled=false;f.FsmName=name;
  var value=new FsmFloat{UseVariable=true,Value=100f};value.SetName(variable);f.FsmVariables.FloatVariables=new[]{value};f.FsmVariables.Reinitialize();
  f.Fsm.States=new[]{new FsmState(f.Fsm){Name="off",Actions=new FsmStateAction[0]}};f.Fsm.StartState="off";f.Fsm.Init(f);go.SetActive(active);return f;
 }
 PlayMakerFSM Receiver(GameObject part,GameObject actor,float multiplier){
  var receiver=Metadata(part,"Bodypart","Damage");var damage=receiver.FsmVariables.FindFsmFloat("Damage");damage.Value=0;
  var fsm=new Fsm{Name="Bodypart",Variables=receiver.FsmVariables};var hit=FsmEvent.GetFsmEvent("Damage");var done=FsmEvent.Finished;
  var actions=new List<FsmStateAction>();
  if(multiplier!=1f)actions.Add(new FloatMultiply{floatVariable=damage,multiplyBy=new FsmFloat{Value=multiplier}});
  actions.Add(new FsmFloatAdd{gameObject=new FsmOwnerDefault{OwnerOption=OwnerDefaultOption.SpecifyGameObject,GameObject=new FsmGameObject{Value=actor}},fsmName=new FsmString{Value="Health"},variableName=new FsmString{Value="Health"},add=damage});
  fsm.States=new[]{new FsmState(fsm){Name="off",Actions=new FsmStateAction[0]},new FsmState(fsm){Name="damage",Actions=actions.ToArray(),Transitions=new[]{new FsmTransition{FsmEvent=done,ToState="off"}}}};
  fsm.GlobalTransitions=new[]{new FsmTransition{FsmEvent=hit,ToState="damage"}};fsm.Events=new[]{hit,done};fsm.StartState="off";fsm.SaveActions();receiver.Fsm=fsm;fsm.Init(receiver);receiver.enabled=true;fsm.Start();return receiver;
 }
 Actor Human(string name,bool trigger){
  var go=Make(name);go.layer=10;go.transform.position=new Vector3(0,.167f,3);
  var health=Metadata(go,"Health","Health");var receiver=Receiver(go,go,1f);
  // Actual Flexa/Scrud/Sprokka root capsule dimensions and centre.
  var body=go.AddComponent<CapsuleCollider>();body.radius=.28f;body.height=1.5f;body.direction=1;body.center=new Vector3(0,-.23f,0);body.isTrigger=trigger;
  return new Actor{Root=go,Body=body,Health=health,Bodypart=receiver};
 }
 FsmFloat Hp(Actor actor){return actor.Health.FsmVariables.FindFsmFloat("Health");}
 void Reset(Actor actor){Hp(actor).Value=100f;actor.Bodypart.FsmVariables.FindFsmFloat("Damage").Value=0;}
 void Damage(Actor actor,float expected,string label){Reset(actor);Physics.SyncTransforms();Call("DamageFlameEnemies",Vector3.zero,Vector3.forward,4f);Check(Mathf.Abs(Hp(actor).Value-expected)<.001f,label+" (Health="+Hp(actor).Value+")");}
 void Aim(Collider expected,string label){Physics.SyncTransforms();var hit=(RaycastHit?)Call("FlameCast",Vector3.zero,Vector3.forward,4f,0f,false);Check(hit!=null&&hit.Value.collider==expected,label);}
 void Awake(){
  host=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Info.Location),"../../.."));withPatrol=Array.IndexOf(Environment.GetCommandLineArgs(),"--with-patrol")>=0;
  try{Run();}catch(Exception e){failures.Add(e.ToString());Logger.LogError(e);}
  File.WriteAllText(Path.Combine(host,"result.txt"),(failures.Count==0?"PASS: ":"FAIL: ")+checks+" native flame patrol checks; actual WildWasteland="+withPatrol+"; failures="+failures.Count+"\n"+string.Join("\n",failures.ToArray()));
  foreach(var go in owned)if(go!=null)DestroyImmediate(go);Application.Quit();
 }
 void Run(){
  var pack=Assembly.LoadFrom(Path.Combine(host,"lib/ChainsawPrototype.dll"));var inactive=Make("InactivePack");inactive.SetActive(false);packOwner=inactive.AddComponent(pack.GetType("ChainsawPrototype.Plugin",true));packOwner.GetType().GetField("flamePaidDamage",All).SetValue(packOwner,7f);
  var actor=Human("NativeHuman",false);
  actor.Bodypart.FsmVariables.FindFsmFloat("Damage").Value=-7f;actor.Bodypart.SendEvent("Damage");Check(Mathf.Abs(Hp(actor).Value-93f)<.001f,"fixture control executes native Bodypart global Damage / FsmFloatAdd into Health.Health");
  Damage(actor,93f,"ordinary nontrigger human torso takes damage");
  actor.Body.isTrigger=true;Aim(actor.Body,"qualified trigger torso participates in aim");Damage(actor,93f,"qualified trigger torso takes damage");
  Physics.SyncTransforms();Check(Call("FlameCast",Vector3.zero,Vector3.forward,4f,.002f,true)==null,"qualified trigger enemy does not become solid flame cover");
  // Only native head collider intersects the visible stream; root body hidden.
  actor.Body.enabled=false;var head=Make("mixamorig:Head",actor.Root.transform);head.layer=10;head.transform.position=new Vector3(0,-.153f,2.965f);var headPart=Receiver(head,actor.Root,2f);var headBox=head.AddComponent<CapsuleCollider>();headBox.radius=.14f;headBox.height=.32f;headBox.direction=1;headBox.center=new Vector3(0,.09f,.035f);headBox.isTrigger=true;
  Aim(headBox,"native head trigger alone participates in aim");Damage(actor,86f,"native head-only trigger damage retains native multiplier");head.SetActive(false);
  // An arbitrary child sensor may share actor ancestry, but owns no Bodypart.
  var sensor=Make("UnrelatedSensor",actor.Root.transform);sensor.transform.position=new Vector3(0,-.063f,3);sensor.AddComponent<SphereCollider>().isTrigger=true;
  Damage(actor,100f,"unrelated child trigger cannot deliver ancestor torso damage");Physics.SyncTransforms();Check(Call("FlameCast",Vector3.zero,Vector3.forward,4f,0f,false)==null,"unrelated trigger is ignored by aim");sensor.layer=2;Receiver(sensor,actor.Root,1f);Damage(actor,100f,"Ignore Raycast fire sensor stays excluded even with damage metadata");sensor.SetActive(false);
  actor.Body.enabled=true;var wall=Make("SolidVehicleBody");wall.transform.position=new Vector3(0,-.03f,1.5f);wall.AddComponent<BoxCollider>().size=new Vector3(2,2,.1f);Damage(actor,100f,"solid bodywork blocks qualified trigger damage");Aim(wall.GetComponent<Collider>(),"solid bodywork stays nearest aim hit");wall.SetActive(false);
  var car=Make("Vehicle");car.tag="Vehicle";var glass=Make("windshield_1_glass",car.transform);var pane=Make("collider",glass.transform);pane.layer=16;pane.transform.position=new Vector3(0,0,1.5f);pane.AddComponent<BoxCollider>().size=new Vector3(1.2f,1.2f,.025f);Damage(actor,93f,"qualified native pane permits triggered occupant damage");pane.SetActive(false);
  for(int i=0;i<3;i++){var limb=Make("NativeBodypart"+i,actor.Root.transform);limb.layer=10;limb.transform.position=new Vector3(.03f*i,-.063f,3+.03f*i);Receiver(limb,actor.Root,1f);limb.AddComponent<SphereCollider>().isTrigger=true;}
  Damage(actor,93f,"overlapping qualified limb triggers damage shared Health once");
  // Fill the nonalloc buffer with unrelated triggers, forcing complete retry.
  var crowd=Make("SensorCrowd");for(int i=0;i<1100;i++){var extra=Make("Sensor"+i,crowd.transform);extra.transform.position=new Vector3((i%9-4)*.01f,-.063f,1f+(i%90)*.02f);var c=extra.AddComponent<SphereCollider>();c.radius=.005f;c.isTrigger=true;}
  Damage(actor,93f,"trigger broadphase overflow retains qualified enemy");wall.SetActive(true);Damage(actor,100f,"trigger broadphase overflow retains solid cover");wall.SetActive(false);crowd.SetActive(false);actor.Root.SetActive(false);
  if(withPatrol)PatrolCases();
 }
 void PatrolCases(){
  var assembly=Assembly.LoadFrom(Path.Combine(host,"lib/WildWasteland.dll"));rider=assembly.GetType("WildWasteland.Modules.Patrols.Rider",true);Logger.LogInfo("Actual patrol assembly: "+assembly.FullName);
  var actor=Human("PatrolCrew",false);var rb=actor.Root.AddComponent<Rigidbody>();rb.useGravity=false;
  rider.GetMethod("NoContacts",All).Invoke(null,new object[]{actor.Root});Check(actor.Body.isTrigger&&rb.isKinematic,"actual WW Rider.NoContacts transforms crew capsule into trigger");Damage(actor,93f,"actual WW NoContacts crew receives native health damage");
  actor.Body.isTrigger=false;rb.isKinematic=false;rider.GetMethod("KeepNoContacts",All).Invoke(null,new object[]{actor.Root});Check(actor.Body.isTrigger&&rb.isKinematic,"actual WW Rider.KeepNoContacts enforces crew trigger");Damage(actor,93f,"actual WW KeepNoContacts crew receives native health damage");
 }
}
