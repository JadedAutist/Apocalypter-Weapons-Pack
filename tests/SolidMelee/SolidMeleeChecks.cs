using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HutongGames.PlayMaker;
using UnityEngine;
[BepInPlugin("weaponspack.local.solidmelee","Local Solid Melee Checks","1.0.0")]
public sealed class SolidMeleeChecks:BaseUnityPlugin {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 List<GameObject> owned=new List<GameObject>();List<string> failures=new List<string>();int checks;object pack,blood;string host;
 UnityEngine.SceneManagement.Scene fixtureScene;
 float expectedMetalScale=.1f;
 object Call(object owner,string name,params object[] args){return owner.GetType().GetMethod(name,All).Invoke(owner,args);}
 object Field(object owner,string name){return owner.GetType().GetField(name,All).GetValue(owner);}
 void Set(object owner,string name,object value){owner.GetType().GetField(name,All).SetValue(owner,value);}
 void Check(bool ok,string label){checks++;Logger.LogInfo((ok?"PASS: ":"FAIL: ")+label);if(!ok)failures.Add(label);}
 GameObject Make(string name){if(!fixtureScene.IsValid())fixtureScene=UnityEngine.SceneManagement.SceneManager.CreateScene("SolidMeleeFixture");var go=new GameObject(name);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,fixtureScene);owned.Add(go);return go;}
 int Effects(bool flesh){int count=0;foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())if(go.name==(flesh?"Chainsaw.NativeBloodHit":"Chainsaw.NativeMeleeHit"))count++;return count;}
 int MetalEffects(){int count=0;foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())if(go.name=="Chainsaw.NativeMetalHit")count++;return count;}
 void SetIfPresent(string name,object value){var field=pack.GetType().GetField(name,All);if(field!=null)field.SetValue(pack,value);}
 void MetalCase(string label,string physics,string materialName,bool expectMetal,bool cutting=false,bool swing=false,bool flesh=false,bool noEffect=false,string vehicleTag=null,string parentName=null){
  var target=Make(label);target.transform.position=new Vector3(400+checks*3,1,2);var collider=target.AddComponent<BoxCollider>();
  if(physics!=null)collider.sharedMaterial=new PhysicMaterial(physics);
  if(materialName!=null){var renderer=target.AddComponent<MeshRenderer>();renderer.sharedMaterial=new Material(Shader.Find("Standard")){name=materialName};}
  if(vehicleTag!=null){var vehicle=Make(parentName??"NativeCar");vehicle.tag=vehicleTag;target.transform.SetParent(vehicle.transform,true);}
  PlayMakerFSM health=null;if(flesh){health=Metadata(target,"Health","Health");DamageReceiver(target,target);}
  var view=Make("MetalFixtureView");view.transform.position=target.transform.position-Vector3.forward;Set(pack,"camera",view.transform);Physics.SyncTransforms();RaycastHit hit;collider.Raycast(new Ray(view.transform.position,Vector3.forward),out hit,3f);
  int metal=MetalEffects(),surface=Effects(false),bloodCount=Effects(true);
  if(cutting)Call(pack,"CuttingContact",.16f,1f);else if(swing){Set(pack,"swinging",true);Set(pack,"hitThisSwing",false);Set(pack,"hitStart",0f);Set(pack,"hitEnd",1f);Set(pack,"swingFuelCost",.1f);Set(pack,"swingFuelSpent",.1f);Call(pack,"SwingContact",.1f);Call(pack,"SwingContact",.1f);}else Hit(hit,10f,true);
  Check(MetalEffects()-metal==(expectMetal?1:0)&&Effects(false)-surface==(!expectMetal&&!flesh&&!noEffect?1:0)&&Effects(true)-bloodCount==(flesh&&!noEffect?1:0),label+": exactly one correct metal/surface/blood effect");
  if(expectMetal)foreach(var effect in Resources.FindObjectsOfTypeAll<GameObject>())if(effect.name=="Chainsaw.NativeMetalHit"&&Vector3.Distance(effect.transform.position,hit.point)<.02f){
   Check(Vector3.Distance(effect.transform.localScale,Vector3.one*expectedMetalScale)<.0001f,label+": machete-sized root scale");
   bool hierarchy=true;foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>(true))hierarchy&=ps.main.scalingMode==ParticleSystemScalingMode.Hierarchy;
   Check(hierarchy,label+": particle size and velocity follow hierarchy scale");
  }
 }
 void Hit(RaycastHit hit,float amount,bool effects){pack.GetType().GetMethod("ProcessHit",All,null,new[]{typeof(RaycastHit),typeof(float),typeof(bool),typeof(float),typeof(bool)},null).Invoke(pack,new object[]{hit,amount,effects,0f,false});}
 void BloodPoll(){if(blood!=null)Call(blood,"Poll");}
 void ClearBlood(){if(blood!=null){((IList)Field(blood,"recent")).Clear();((IList)Field(blood,"gushes")).Clear();}}
 void OptionalBloodRouting(){
  if(blood==null)return;
  var bridge=pack.GetType().Assembly.GetType("ChainsawPrototype.OptionalBloodEffects");Check(bridge!=null,"optional BloodFX contact adapter exists without a hard dependency");if(bridge==null)return;
  Check((bool)bridge.GetMethod("Bind",All).Invoke(null,new object[]{blood,new Action<string>(s=>Logger.LogWarning(s))}),"real BloodFX signature validates and binds optional hooks");
  Check(!(bool)bridge.GetMethod("Bind",All).Invoke(null,new object[]{new object(),new Action<string>(s=>Logger.LogWarning(s))}),"unrelated plugin object cannot become a BloodFX dependency");
  var dormant=Make("DormantBloodTarget_Dead");dormant.transform.position=new Vector3(17000,1,0);dormant.AddComponent<Rigidbody>().isKinematic=true;dormant.AddComponent<BoxCollider>();var dormantView=Make("DormantBloodView");dormantView.transform.position=dormant.transform.position-Vector3.forward;Set(pack,"camera",dormantView.transform);Physics.SyncTransforms();int dormantNative=Effects(true);ClearBlood();Call(pack,"CuttingContact",.16f,1f);Check(Effects(true)==dormantNative+1&&((IList)Field(blood,"gushes")).Count==0,"enabled but non-running BloodFX preserves standalone feedback without needing a private lifecycle repair");
  Call(blood,"Update");
  var actor=Make("OptionalBloodTarget");actor.transform.position=new Vector3(16000,1,0);var healthFsm=Metadata(actor,"Health","Health");healthFsm.enabled=true;var hp=healthFsm.FsmVariables.FindFsmFloat("Health");hp.Value=1000f;var receiver=DamageReceiver(actor,actor);var box=actor.AddComponent<BoxCollider>();var view=Make("OptionalBloodView");view.transform.position=actor.transform.position-Vector3.forward;Set(pack,"camera",view.transform);Physics.SyncTransforms();
  Call(blood,"Scan");BloodPoll();bool exact=false;foreach(var tracked in (IList)Field(blood,"trackedList"))exact|=ReferenceEquals(tracked.GetType().GetField("F",All).GetValue(tracked),hp);Check(exact,"real BloodFX tracks the exact enabled native target Health variable");ClearBlood();Set(pack,"flamePaidDamage",20f);Call(pack,"DamageFlameReceiver",receiver);BloodPoll();
  Check(hp.Value==980f&&((IList)Field(blood,"gushes")).Count==0,"above-threshold native flame damage remains effective without BloodFX blood spray");
  // Unrelated damage in the same poll must survive subtraction of owned heat.
  ClearBlood();Set(pack,"flamePaidDamage",7f);Call(pack,"DamageFlameReceiver",receiver);receiver.FsmVariables.FindFsmFloat("Damage").Value=-30f;receiver.SendEvent("Damage");BloodPoll();int mixed=((IList)Field(blood,"gushes")).Count;Check(mixed==1,"unrelated solid damage still bleeds in a poll containing flame damage (gushes="+mixed+", HP="+hp.Value+")");
  ClearBlood();int native=Effects(true);Call(pack,"CuttingContact",.16f,1f);Check(((IList)Field(blood,"gushes")).Count==1&&Effects(true)==native,"RMB uses one real BloodFX gush instead of duplicate native blood");BloodPoll();Check(((IList)Field(blood,"gushes")).Count==1,"health polling cannot duplicate the explicit RMB contact");
  var dead=Make("Lizard_Dead(Clone)42");dead.transform.position=actor.transform.position;dead.AddComponent<Rigidbody>().isKinematic=true;dead.AddComponent<BoxCollider>();actor.SetActive(false);Physics.SyncTransforms();ClearBlood();Set(blood,"lastCorpseHit",Time.time-10f);Call(pack,"CuttingContact",.16f,1f);Check(((IList)Field(blood,"gushes")).Count==1,"RMB corpse contact receives the real optional blood effect");
  Set(blood,"lastCorpseHit",Time.time);ClearBlood();Call(pack,"CuttingContact",.16f,1f);Check(((IList)Field(blood,"gushes")).Count==0,"RMB corpse cutting respects BloodFX's native corpse cooldown");
  var corpseKeys=new[]{"countMin","countMax","countMul","immediateFraction","corpseMul","corpseIntensity","deathMul"};var corpseValues=new object[]{20,20,1f,0f,.25f,.3f,10f};var savedValues=new object[corpseKeys.Length];
  try{
   for(int i=0;i<corpseKeys.Length;i++){var entry=Field(blood,corpseKeys[i]);var property=entry.GetType().GetProperty("Value");savedValues[i]=property.GetValue(entry,null);property.SetValue(entry,corpseValues[i],null);}
   Set(blood,"lastCorpseHit",Time.time-10f);ClearBlood();Call(pack,"CuttingContact",.16f,1f);var gush=((IList)Field(blood,"gushes"))[0];
   Check(Mathf.Abs((float)Field(gush,"Intensity")-.3f)<.0001f&&Mathf.Abs((float)Field(gush,"Remaining")-5f)<.0001f&&!(bool)Field(gush,"Static"),"corpse contact preserves optional intensity/count settings without applying death multiplier");
   var multiplier=Field(blood,"corpseMul");multiplier.GetType().GetProperty("Value").SetValue(multiplier,0f,null);Set(blood,"lastCorpseHit",Time.time-10f);ClearBlood();Call(pack,"CuttingContact",.16f,1f);gush=((IList)Field(blood,"gushes"))[0];Check((float)Field(gush,"Remaining")==0f&&(float)Field(gush,"Rate")==0f,"zero corpse multiplier suppresses optional spray particles");
  }finally{for(int i=0;i<corpseKeys.Length;i++)if(savedValues[i]!=null){var entry=Field(blood,corpseKeys[i]);entry.GetType().GetProperty("Value").SetValue(entry,savedValues[i],null);}}
  var cfg=Field(blood,"cfgEnabled");cfg.GetType().GetProperty("Value").SetValue(cfg,false,null);ClearBlood();native=Effects(true);Call(pack,"CuttingContact",.16f,1f);Check(Effects(true)==native+1&&((IList)Field(blood,"gushes")).Count==0,"disabled BloodFX falls back to standalone corpse feedback");cfg.GetType().GetProperty("Value").SetValue(cfg,true,null);
  var singleton=pack.GetType().GetField("instance",All);var previous=singleton.GetValue(null);singleton.SetValue(null,pack);Set(pack,"enabledPreview",false);Set(pack,"activeFlame",null);Set(blood,"meleeAt",Time.time+10f);Set(blood,"meleePending",true);Call(blood,"HandleMelee");Check((bool)Field(blood,"meleePending"),"optional input hook preserves stock weapon detection");Set(pack,"enabledPreview",true);Call(blood,"HandleMelee");Check(!(bool)Field(blood,"meleePending"),"selected chainsaw uses exact contacts instead of duplicate LMB click guesses");Set(pack,"enabledPreview",false);var flame=Make("OwnFlameInput").AddComponent(pack.GetType().Assembly.GetType("ChainsawPrototype.FlamethrowerItem",true));Set(pack,"activeFlame",flame);Set(blood,"meleePending",true);Call(blood,"HandleMelee");Check(!(bool)Field(blood,"meleePending"),"selected flamethrower cannot trigger BloodFX solid strike detection");Set(pack,"activeFlame",null);singleton.SetValue(null,previous);
  bridge.GetMethod("Clear",All).Invoke(null,null);
 }
 void AttackRouteCase(string label,bool cutting,bool corpse){
  var root=Make(corpse?"Flexa_Dead(Clone)":"LivingAttackEnemy");root.transform.position=new Vector3(150+checks*2,1,1);
  var hp=Metadata(root,"Health","Health");hp.FsmVariables.FindFsmFloat("Health").Value=1000f;DamageReceiver(root,root);root.AddComponent<BoxCollider>();if(corpse){var body=root.AddComponent<Rigidbody>();body.isKinematic=true;}
  var view=Make("FixtureView");view.transform.position=root.transform.position-Vector3.forward;Set(pack,"camera",view.transform);Physics.SyncTransforms();int before=Effects(true);
  if(cutting)Call(pack,"CuttingContact",.16f,1f);else{Set(pack,"swinging",true);Set(pack,"hitThisSwing",false);Set(pack,"hitStart",0f);Set(pack,"hitEnd",1f);Set(pack,"swingFuelCost",.1f);Set(pack,"swingFuelSpent",.1f);Call(pack,"SwingContact",.1f);Call(pack,"SwingContact",.1f);}
  Check(Effects(true)-before==1,label+": one native blood impact");Check(hp.FsmVariables.FindFsmFloat("Health").Value==(corpse?1000f:cutting?980f:950f),label+": intended native damage once");
 }
 void SolidCase(string label,string rootName,float? health,bool rigid,bool trigger,bool mesh,bool flesh){
  var root=Make(rootName);root.transform.position=new Vector3(40+checks*2,1,3);if(rigid){var rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;}
  PlayMakerFSM hp=health.HasValue?Metadata(root,"Health","Health"):null;if(hp!=null)hp.FsmVariables.FindFsmFloat("Health").Value=health.Value;
  var part=Make(label+" limb");part.transform.SetParent(root.transform,false);Collider collider;
  if(mesh){var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);var shape=cube.GetComponent<MeshFilter>().sharedMesh;DestroyImmediate(cube);var c=part.AddComponent<MeshCollider>();c.sharedMesh=shape;c.convex=true;collider=c;}else collider=part.AddComponent<BoxCollider>();
  collider.isTrigger=trigger;var receiver=DamageReceiver(part,root);Physics.SyncTransforms();RaycastHit hit;Check(collider.Raycast(new Ray(part.transform.position-Vector3.forward*2,Vector3.forward),out hit,4f),label+": native collider contact");
  int oldBlood=Effects(true),oldSurface=Effects(false);Hit(hit,10f,true);
  Check(Effects(true)-oldBlood==(flesh?1:0)&&Effects(false)-oldSurface==(flesh?0:1),label+": one matching native effect");
  if(hp!=null)Check(hp.FsmVariables.FindFsmFloat("Health").Value==(health.Value>0f?health.Value-10f:health.Value),label+": living-only native damage once");
  if(!health.HasValue&&flesh)Check(receiver.FsmVariables.FindFsmFloat("Damage").Value==100f,label+": corpse never receives Damage event");
 }
 PlayMakerFSM Metadata(GameObject go,string name,string variable){
 bool active=go.activeSelf;go.SetActive(false);var f=go.AddComponent<PlayMakerFSM>();f.Reset();f.enabled=false;f.FsmName=name;var v=new FsmFloat();v.SetName(variable);v.Value=100;f.FsmVariables.FloatVariables=new[]{v};f.FsmVariables.Reinitialize();f.Fsm.Init(f);go.SetActive(active);return f;}
 PlayMakerFSM DamageReceiver(GameObject target,GameObject root){
 var f=Metadata(target,"Bodypart","Damage");var d=f.FsmVariables.FindFsmFloat("Damage");d.UseVariable=true;var fsm=new Fsm{Name="Bodypart",Variables=f.FsmVariables};var hit=FsmEvent.GetFsmEvent("Damage");
 fsm.States=new[]{new FsmState(fsm){Name="wait",Actions=new FsmStateAction[0]},new FsmState(fsm){Name="damage",Actions=new FsmStateAction[]{new HutongGames.PlayMaker.Actions.FsmFloatAdd{gameObject=new FsmOwnerDefault{OwnerOption=OwnerDefaultOption.SpecifyGameObject,GameObject=new FsmGameObject{Value=root}},fsmName=new FsmString{Value="Health"},variableName=new FsmString{Value="Health"},add=d}}}};
 fsm.GlobalTransitions=new[]{new FsmTransition{FsmEvent=hit,ToState="damage"}};fsm.Events=new[]{hit};fsm.StartState="wait";fsm.SaveActions();f.Fsm=fsm;fsm.Init(f);f.enabled=true;fsm.Start();return f;}
 void Awake(){host=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Info.Location),"../../.."));try{Run();}catch(Exception e){failures.Add(e.ToString());Logger.LogError(e);}File.WriteAllText(Path.Combine(host,"result.txt"),(failures.Count==0?"PASS: ":"FAIL: ")+checks+" solid melee checks; failures="+failures.Count+"\n"+string.Join("\n",failures.ToArray()));foreach(var go in owned)if(go!=null)DestroyImmediate(go);Application.Quit();}
 void Run(){
 var pa=Assembly.LoadFrom(Path.Combine(host,"lib/ChainsawPrototype.dll"));var pg=Make("InactivePack");pg.SetActive(false);pack=pg.AddComponent(pa.GetType("ChainsawPrototype.Plugin",true));
 if(File.Exists(Path.Combine(host,"lib/BloodFX.dll"))){var ba=Assembly.LoadFrom(Path.Combine(host,"lib/BloodFX.dll"));var bg=Make("InactiveBlood");bg.SetActive(false);blood=bg.AddComponent(ba.GetType("BloodFX.Plugin",true));Call(blood,"Awake");}
 var root=Make("BloodCheckEnemy");root.layer=10;root.transform.position=new Vector3(0,1,3);root.AddComponent<BoxCollider>();var hp=Metadata(root,"Health","Health");hp.enabled=true;
 var receiver=DamageReceiver(root,root);if(blood!=null){Call(blood,"Scan");BloodPoll();Check(((IList)Field(blood,"trackedList")).Count>0,"real BloodFX scans native Health variable");}
 int flameBloodBefore=Effects(true);Set(pack,"flamePaidDamage",7f);Call(pack,"DamageFlameReceiver",receiver);Check(hp.FsmVariables.FindFsmFloat("Health").Value==93f,"production flame lowers real native root Health once");Check(Effects(true)==flameBloodBefore,"flame native damage adds no pack blood spray");BloodPoll();if(blood!=null)Check(((IList)Field(blood,"gushes")).Count==1,"real BloodFX detects production flame root Health drop");
 ClearBlood();
 // Direct native event matches chainsaw damage dispatch; production ProcessHit exercised below with a real raycast.
 Physics.SyncTransforms();RaycastHit hit;Physics.Raycast(new Ray(new Vector3(0,1,0),Vector3.forward),out hit,5f);
 var method=pack.GetType().GetMethod("ProcessHit",All,null,new[]{typeof(RaycastHit),typeof(float),typeof(bool),typeof(float),typeof(bool)},null);method.Invoke(pack,new object[]{hit,50f,false,0f,false});
 Check(hp.FsmVariables.FindFsmFloat("Health").Value==43f,"production chainsaw lowers real native root Health once");BloodPoll();if(blood!=null)Check(((IList)Field(blood,"gushes")).Count==1,"real BloodFX detects production chainsaw root Health drop");
 ClearBlood();BloodPoll();if(blood!=null)Check(((IList)Field(blood,"gushes")).Count==0,"unchanged health produces no duplicate blood");
 // Replacement uses actual PlayMaker action fields, not a made-up BloodFX API.
 var wrench=Make("pipe_wrench");wrench.SetActive(false);var attack=wrench.AddComponent<PlayMakerFSM>();attack.Reset();attack.enabled=false;attack.FsmName="Attack";
 var surface=Make("CurrentSurfacePrefab");surface.SetActive(false);var first=Make("CurrentBloodPrefabA");first.SetActive(false);var replacement=Make("CurrentBloodPrefabB");replacement.SetActive(false);var third=Make("CurrentBloodPrefabC");third.SetActive(false);
 var current=new FsmGameObject{Value=first};var fsm=new Fsm{Name="Attack"};fsm.States=new[]{new FsmState(fsm){Name="getLayer",Actions=new FsmStateAction[]{new HutongGames.PlayMaker.Actions.SetFsmGameObject{variableName=new FsmString{Value="HitEffect"},setValue=new FsmGameObject{Value=surface}}}},new FsmState(fsm){Name="actorHitSound",Actions=new FsmStateAction[]{new HutongGames.PlayMaker.Actions.SetFsmGameObject{variableName=new FsmString{Value="HitEffect"},setValue=current}}}};fsm.SaveActions();attack.Fsm=fsm;fsm.Init(attack);
 var clip=AudioClip.Create("FixtureHit",64,1,8000,false);Set(pack,"nativeSurfaceHit",clip);Set(pack,"nativeActorHit",clip);
 var kind=pa.GetType("ChainsawPrototype.ChainsawSurface",true);var flesh=Enum.Parse(kind,"Flesh");Call(pack,"StandardMeleeHit",new Vector3(20,0,0),Vector3.up,flesh);Check((GameObject)Field(pack,"nativeActorEffect")==first,"first melee effect uses current native action");
 current.Value=replacement;Set(pack,"nativeHitRetry",0f);Call(pack,"StandardMeleeHit",new Vector3(21,0,0),Vector3.up,flesh);Check((GameObject)Field(pack,"nativeActorEffect")==replacement,"melee effect follows replacement of current action value");
 ((HutongGames.PlayMaker.Actions.SetFsmGameObject)fsm.States[1].Actions[0]).setValue=new FsmGameObject{Value=third};Set(pack,"nativeHitRetry",0f);Call(pack,"StandardMeleeHit",new Vector3(22,0,0),Vector3.up,flesh);Check((GameObject)Field(pack,"nativeActorEffect")==third,"melee effect follows replacement action field");
 fsm.States[1].Actions=new FsmStateAction[]{new HutongGames.PlayMaker.Actions.SetFsmGameObject{variableName=new FsmString{Value="HitEffect"},setValue=new FsmGameObject{Value=replacement}}};Call(pack,"StandardMeleeHit",new Vector3(23,0,0),Vector3.up,flesh);Check((GameObject)Field(pack,"nativeActorEffect")==replacement,"melee effect follows replacement action instance");
 ClearBlood();int nativeBefore=Effects(true);Hit(hit,10f,true);BloodPoll();Check(Effects(true)-nativeBefore==1,"solid hit emits exactly one pack native blood effect");if(blood!=null){Check(((IList)Field(blood,"gushes")).Count==1,"one solid hit yields one real BloodFX health response");BloodPoll();Check(((IList)Field(blood,"gushes")).Count==1,"pack never redispatches BloodFX after the native hit");}
 SolidCase("living body","LivingEnemy",100f,false,false,false,true);
 SolidCase("living specific trigger","LivingEnemy",100f,false,true,false,true);
 SolidCase("corpse physical mesh","Flexa_Dead(Clone)81",null,true,false,true,true);
 SolidCase("corpse physical box","Lizard_Dead(Clone)5",null,true,false,false,true);
 SolidCase("dead Health with residual Bodypart","Enemy",0f,false,false,false,true);
 SolidCase("foreign solid surface","MetalPanel",null,false,false,false,false);
 SolidCase("corpse-like name without physical body","Poster_Dead",null,false,false,false,false);
 Call(pack,"SetupCombat");Call(pack,"SetupCuttingAttack");AttackRouteCase("LMB living",false,false);AttackRouteCase("LMB corpse",false,true);AttackRouteCase("RMB living",true,false);AttackRouteCase("RMB corpse",true,true);
 int disabledBefore=Effects(true);((HutongGames.PlayMaker.Actions.SetFsmGameObject)fsm.States[1].Actions[0]).setValue.Value=null;Call(pack,"StandardMeleeHit",new Vector3(24,0,0),Vector3.up,flesh);Check(Effects(true)==disabledBefore,"cleared native blood choice stays disabled");
 DestroyImmediate(wrench);var nextWrench=Make("pipe_wrench");nextWrench.SetActive(false);var nextAttack=nextWrench.AddComponent<PlayMakerFSM>();nextAttack.Reset();nextAttack.enabled=false;
 var nextFsm=new Fsm{Name="Attack"};nextFsm.States=new[]{new FsmState(nextFsm){Name="getLayer",Actions=new FsmStateAction[]{new HutongGames.PlayMaker.Actions.SetFsmGameObject{variableName=new FsmString{Value="HitEffect"},setValue=new FsmGameObject{Value=surface}}}},new FsmState(nextFsm){Name="actorHitSound",Actions=new FsmStateAction[]{new HutongGames.PlayMaker.Actions.SetFsmGameObject{variableName=new FsmString{Value="HitEffect"},setValue=new FsmGameObject{Value=third}}}}};nextFsm.SaveActions();nextAttack.Fsm=nextFsm;nextFsm.Init(nextAttack);
 Set(pack,"nativeHitRetry",Time.unscaledTime+100f);Call(pack,"StandardMeleeHit",new Vector3(25,0,0),Vector3.up,flesh);Check((GameObject)Field(pack,"nativeActorEffect")==third,"destroyed native source resolves current replacement immediately");
 if(blood!=null){var cfg=Field(blood,"cfgEnabled");Logger.LogInfo("BloodFX master="+cfg.GetType().GetProperty("Value").GetValue(cfg,null));}
 var metalPrefab=Make("MetalImpact");metalPrefab.SetActive(false);var marker=Make("NativeMetalSparkMarker");marker.transform.SetParent(metalPrefab.transform,false);var particles=marker.AddComponent<ParticleSystem>();var main=particles.main;main.playOnAwake=false;main.scalingMode=ParticleSystemScalingMode.Local;SetIfPresent("nativeMetalImpact",metalPrefab);
 MetalCase("metal physics","Metal",null,true);MetalCase("metal render material",null,"metal_rusted_16",true);MetalCase("vehicle metal","VehicleMaterial",null,true);
 MetalCase("glass on vehicle","VehicleMaterial","glass",false);MetalCase("rubber on vehicle","VehicleMaterial","rubber_tire",false);MetalCase("wood surface","Wood","wood_planks",false);MetalCase("sand surface","Ground","sand",false);MetalCase("living flesh wins over metal","Metal","metal_armour",false,false,false,true);
 MetalCase("LMB metal impact","Metal",null,true,false,true);MetalCase("RMB metal impact","Metal",null,true,true);
 MetalCase("vehicle wheel collider","VehicleMaterial",null,false);MetalCase("collider_SeeTrough","VehicleMaterial",null,false);
 MetalCase("drivable body with unrecognized paint",null,"paint_23",true,false,false,false,false,"Vehicle");
 MetalCase("mounted car door",null,"paint_23",true,false,true,false,false,"vehPart");
 MetalCase("detached car door",null,"paint_23",true,true,false,false,false,"vehPartRemoved");
 MetalCase("vehicle glass with unknown physics",null,"glass",false,false,false,false,false,"Vehicle");
 MetalCase("vehicle rubber with unknown physics",null,"rubber",false,false,false,false,false,"Vehicle");
 MetalCase("vehicle upholstery with unknown physics",null,"leather",false,false,false,false,false,"Vehicle");
 MetalCase("collider_SeeTrough",null,"paint_23",false,false,false,false,false,"Vehicle");
 MetalCase("wheel rim",null,"paint_23",false,false,false,false,false,"Vehicle");
 MetalCase("nested wheel collider",null,"paint_23",false,false,false,false,false,"Vehicle","front_wheel");
 MetalCase("passenger flesh before car ancestor",null,"paint_23",false,false,false,true,false,"Vehicle");
 if(File.Exists(Path.Combine(host,"lib/WildWasteland.dll"))){
  var ww=Assembly.LoadFrom(Path.Combine(host,"lib/WildWasteland.dll"));var guns=ww.GetType("WildWasteland.Modules.Guns.GunsModule",true);
  var config=new BepInEx.Configuration.ConfigFile(Path.Combine(host,"fixture-metal.cfg"),false);
  config.SaveOnConfigSet=false;
  var scale=config.Bind("Tracers","MetalSparksScale",.25f);var enabled=config.Bind("Tracers","MetalSparks",true);
  scale.Value=.25f;enabled.Value=true;
  guns.GetField("MetalSparksScale",All).SetValue(null,scale);guns.GetField("MetalSparks",All).SetValue(null,enabled);SetIfPresent("nextMetalSettingsCheck",0f);
  expectedMetalScale=.25f;MetalCase("optional WW configured scale","Metal",null,true,false,false,false,false,"Vehicle");
  scale.Value=.15f;expectedMetalScale=.15f;MetalCase("optional WW live scale change","Metal",null,true,false,false,false,false,"Vehicle");
  enabled.Value=false;MetalCase("optional WW sparks disabled","Metal",null,false,false,false,false,false,"Vehicle");enabled.Value=true;
  scale.Value=float.NaN;expectedMetalScale=.1f;MetalCase("optional WW invalid scale fallback",null,null,true,false,false,false,false,"Vehicle");
 }
 Check(metalPrefab.transform.localScale==Vector3.one&&particles.main.scalingMode==ParticleSystemScalingMode.Local,"scaling never mutates the shared source");
 bool nativeAvailable=false;foreach(var candidate in Resources.FindObjectsOfTypeAll<GameObject>())if(candidate.name=="MetalImpact"&&!candidate.scene.IsValid()&&candidate.transform.parent==null)nativeAvailable=true;
 SetIfPresent("nativeMetalImpact",null);SetIfPresent("nativeMetalRetry",0f);MetalCase("native metal asset lookup","Metal",null,nativeAvailable);
 var sourceField=pack.GetType().GetField("nativeMetalImpact",All);var resolved=sourceField==null?null:sourceField.GetValue(pack) as GameObject;
 File.WriteAllText(Path.Combine(host,"native-metal-asset.txt"),"available="+nativeAvailable+"; selected="+(resolved==null?"none":resolved.name)+"; particles="+(resolved==null?0:resolved.GetComponentsInChildren<ParticleSystem>(true).Length)+"\n");
 Check(resolved!=metalPrefab&&(nativeAvailable?resolved!=null&&!resolved.scene.IsValid()&&resolved.transform.parent==null:resolved==null),"metal lookup never borrows scene decoration or child; actual native asset available="+nativeAvailable);
 SetIfPresent("nativeMetalImpact",null);SetIfPresent("nativeMetalRetry",Time.unscaledTime+100f);MetalCase("metal lookup pending fallback","Metal",null,false);
 ((HutongGames.PlayMaker.Actions.SetFsmGameObject)nextFsm.States[0].Actions[0]).setValue.Value=null;SetIfPresent("nativeMetalImpact",metalPrefab);MetalCase("shared surface effects disabled on metal","Metal",null,false,false,false,false,true);
 OptionalBloodRouting();
 }
}

