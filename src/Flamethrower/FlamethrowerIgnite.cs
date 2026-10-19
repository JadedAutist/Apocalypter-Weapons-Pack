using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChainsawPrototype {
 public partial class Plugin {
  ConfigEntry<float> flameIgniteDamage,flameIgniteInterval,flameIgniteDuration;
  ConfigEntry<int> flameIgniteStackCount;
  readonly Dictionary<int,FlamethrowerIgniteStatus> flameIgnitedEnemies=new Dictionary<int,FlamethrowerIgniteStatus>();
  GameObject flameIgniteRoot;Material flameIgniteMaterial;float nextIgniteMaterial;
  int flameIgniteFrame=-1;bool flameIgniteContactsReady;
  Vector3 flameIgniteOrigin,flameIgniteDirection;float flameIgniteDistance;
  void SetupFlamethrowerIgnite(){
   SetupFireSmoke();
   const string group="Flamethrower - Ignite";
   flameIgniteDamage=Config.Bind(group,"Damage per stack",10f,new ConfigDescription("Burn damage per second for each stack, delivered through the enemy's native root Bodypart.",new AcceptableValueRange<float>(.1f,200f)));
   flameIgniteStackCount=Config.Bind(group,"Maximum stacks",5,new ConfigDescription("Maximum burn stacks on one living enemy.",new AcceptableValueRange<int>(1,10)));
   flameIgniteInterval=Config.Bind(group,"Stack interval",1f,new ConfigDescription("Seconds of actual funded flame contact required for each additional stack.",new AcceptableValueRange<float>(.1f,5f)));
   flameIgniteDuration=Config.Bind(group,"Burn duration",10f,new ConfigDescription("Seconds the enemy keeps burning after its last funded flame contact.",new AcceptableValueRange<float>(.1f,60f)));
  }
  internal float IgniteDamage {get{return flameIgniteDamage==null?10f:Mathf.Clamp(flameIgniteDamage.Value,.1f,200f);}}
  internal int IgniteMaxStacks {get{return flameIgniteStackCount==null?5:Mathf.Clamp(flameIgniteStackCount.Value,1,10);}}
  internal float IgniteInterval {get{return flameIgniteInterval==null?1f:Mathf.Clamp(flameIgniteInterval.Value,.1f,5f);}}
  internal float IgniteDuration {get{return flameIgniteDuration==null?10f:Mathf.Clamp(flameIgniteDuration.Value,.1f,60f);}}
  internal void TouchFlameIgnite(Transform actor,float fundedSeconds,float now){
   if(actor==null||fundedSeconds<=0f||!Finite(fundedSeconds)||!actor.gameObject.activeInHierarchy||SolidMeleeContact.Corpse(actor))return;
   int id=actor.GetInstanceID();FlamethrowerIgniteStatus old;
   if(flameIgnitedEnemies.TryGetValue(id,out old)&&old!=null&&old.Touch(fundedSeconds,now))return;
   PlayMakerFSM rootReceiver=null;FsmFloat health=null;
   foreach(var fsm in actor.GetComponents<PlayMakerFSM>()){
    if(fsm==null||fsm.Fsm==null)continue;
    if(fsm.FsmName=="Health")health=fsm.FsmVariables.FindFsmFloat("Health");
    if(fsm.FsmName=="Bodypart"&&fsm.FsmVariables.FindFsmFloat("Damage")!=null)rootReceiver=fsm;
   }
   // Root Bodypart has the native body damage route. Never reuse a head/limb
   // receiver for fixed DPS, synthesize Health values or ignite a corpse.
   if(health==null||!(health.Value>0f)||rootReceiver==null)return;
   EnsureFlameIgniteRoot();
   var go=new GameObject("Flamethrower.EnemyIgnite"){hideFlags=HideFlags.DontSave};go.transform.SetParent(flameIgniteRoot.transform,false);
   var status=go.AddComponent<FlamethrowerIgniteStatus>();status.Configure(this,actor,id,rootReceiver,health,now);flameIgnitedEnemies[id]=status;status.Touch(fundedSeconds,now);
  }
  void IgniteFlameEnemies(Vector3 origin,Vector3 direction,float distance,float fundedSeconds){
   if(flameIgniteFrame==Time.frameCount||fundedSeconds<=0f)return;flameIgniteFrame=Time.frameCount;
   CollectFlameEnemyContacts(origin,direction,distance);
   flameIgniteOrigin=origin;flameIgniteDirection=direction;flameIgniteDistance=distance;flameIgniteContactsReady=true;
   foreach(var pair in flameEnemyContacts)TouchFlameIgnite(pair.Key,fundedSeconds,Time.time);
  }
  void ResetFlamethrowerIgniteContacts(){flameIgniteFrame=-1;flameIgniteContactsReady=false;flameEnemyContacts.Clear();}
  internal void ForgetFlameIgnite(int id,FlamethrowerIgniteStatus status){FlamethrowerIgniteStatus old;if(flameIgnitedEnemies.TryGetValue(id,out old)&&ReferenceEquals(old,status))flameIgnitedEnemies.Remove(id);}
  internal void BindFlameEndingCorpse(int id,GameObject corpse){FlamethrowerIgniteStatus status;if(corpse!=null&&flameIgnitedEnemies.TryGetValue(id,out status)&&status!=null)status.EndOnCorpse(corpse,Time.time);}
  void EnsureFlameIgniteRoot(){if(flameIgniteRoot==null){flameIgniteRoot=new GameObject("Flamethrower.IgnitedEnemies"){hideFlags=HideFlags.DontSave};flameIgniteRoot.AddComponent<FlameIgniteSceneRoot>().Owner=this;}}
  internal void FlameIgniteRootDestroyed(GameObject root){if(!ReferenceEquals(flameIgniteRoot,root))return;ClearFlameBurnDeaths();flameIgnitedEnemies.Clear();flameIgniteRoot=null;if(flameIgniteMaterial!=null)Destroy(flameIgniteMaterial);flameIgniteMaterial=null;}
  void CleanupFlamethrowerIgnite(){foreach(var status in flameIgnitedEnemies.Values)if(status!=null)status.Cancel();var root=flameIgniteRoot;if(root!=null)Destroy(root);FlameIgniteRootDestroyed(root);ResetFlamethrowerIgniteContacts();}
  bool warnedBodyFireCoverage;
  internal void BodyFireCoverageUnavailable(string reason){if(warnedBodyFireCoverage)return;warnedBodyFireCoverage=true;Logger.LogWarning(reason);}
  internal Material EnemyIgniteMaterial(){
   if(flameIgniteMaterial!=null)return flameIgniteMaterial;
   if(Time.unscaledTime<nextIgniteMaterial)return null;nextIgniteMaterial=Time.unscaledTime+1f;
   flameIgniteMaterial=AnimatedFireMaterial("Flamethrower.EnemyIgnite.Material");return flameIgniteMaterial;
  }
 }
 internal sealed class FlameIgniteSceneRoot:MonoBehaviour {
  internal Plugin Owner;
  void Update(){if(!ReferenceEquals(Owner,null))Owner.TickFlameBurnDeaths();}
  void OnDestroy(){if(!ReferenceEquals(Owner,null))Owner.FlameIgniteRootDestroyed(gameObject);}
 }
 [DefaultExecutionOrder(32020)] internal sealed class FlamethrowerIgniteStatus:MonoBehaviour {
  Plugin owner;Transform actor;int actorId;PlayMakerFSM receiver;FsmFloat health;
  float lastContact,lastStep,endingAt,endingSeconds=.65f;double contactSeconds;int stacks;bool ending,finished;
  double coveredStart=double.PositiveInfinity,coveredEnd=double.NegativeInfinity;
  Vector3 localCentre;ParticleSystem[] particles;ParticleSystemRenderer[] renderers;
  Transform[] fireAnchors;Vector3[] firePoints;
  Transform endingCorpse;bool followingCorpse;
  readonly ParticleSystem.Particle[] liveFire=new ParticleSystem.Particle[2];
  MaterialPropertyBlock properties;Material fireMaterial;
  FlameFireSmoke bodySmoke;
  internal int Stacks {get{return stacks;}}
  internal float DamagePerSecond {get{return ReferenceEquals(owner,null)?0f:owner.IgniteDamage*Mathf.Min(stacks,owner.IgniteMaxStacks);}}
  internal bool Ending {get{return ending;}}
  internal void Configure(Plugin plugin,Transform target,int id,PlayMakerFSM damageReceiver,FsmFloat actorHealth,float now){
   owner=plugin;actor=target;actorId=id;receiver=damageReceiver;health=actorHealth;lastContact=lastStep=now;
   localCentre=Vector3.up*.8f;var body=target.GetComponent<Collider>();if(body!=null)localCentre=target.InverseTransformPoint(body.bounds.center);
   Follow();CreateVisuals();
  }
  internal bool Touch(float seconds,float now){
   if(finished||seconds<=0f||actor==null||receiver==null||health==null||!(health.Value>0f))return false;
   Advance(now);if(finished||actor==null||receiver==null||!(health.Value>0f))return false;
   if(stacks==0||ending){stacks=1;contactSeconds=0f;ending=false;}
   lastContact=now;
   // Stream and puddles remain independent damage sources, but touching both
   // during the same time interval must not double the ignite stacking speed.
   double start=(double)now-seconds,end=now,credit;
   if(start>coveredEnd+.000001||end<coveredStart){credit=seconds;coveredStart=start;coveredEnd=end;}
   else{credit=Math.Max(0,coveredStart-start)+Math.Max(0,end-coveredEnd);coveredStart=Math.Min(coveredStart,start);coveredEnd=Math.Max(coveredEnd,end);}
   contactSeconds+=credit;
   float interval=owner.IgniteInterval;int extra=(int)Math.Floor((contactSeconds+.0000001)/interval);
   if(extra>0){stacks=Mathf.Min(owner.IgniteMaxStacks,stacks+extra);contactSeconds=Math.Max(0,contactSeconds-extra*(double)interval);}
   ApplyFade(1f);return true;
  }
  void Update(){Advance(Time.time);}
  void LateUpdate(){if(finished)return;if(followingCorpse&&endingCorpse==null){Finish();return;}if(!ending||followingCorpse)Follow();MorphBodyFire(Time.time);}
  internal void MorphBodyFire(float now){
   if(particles==null||float.IsNaN(now)||float.IsInfinity(now))return;
   float phase=(unchecked((uint)actorId)%1021u)*(Mathf.PI*2f/1021f);
   for(int i=0;i<particles.Length;i++)if(particles[i]!=null){
    float a=now*(1.9f+i*.17f)+phase+i*2.399963f,b=now*(1.35f+i*.11f)+phase+i*1.731f;
    var fire=particles[i];fire.transform.localScale=Vector3.one;int count=fire.GetParticles(liveFire);var main=fire.main;
    for(int j=0;j<count;j++){
     var particle=liveFire[j];float variation=(particle.randomSeed&255u)/255f;
     float width=Mathf.Lerp(main.startSizeX.constantMin,main.startSizeX.constantMax,variation);
     float height=Mathf.Lerp(main.startSizeY.constantMin,main.startSizeY.constantMax,((particle.randomSeed>>8)&255u)/255f);
     particle.startSize3D=new Vector3(width*(.75f+.35f*Mathf.Sin(a+variation)),height*(.85f+.3f*Mathf.Sin(b+variation)),1f);
     particle.rotation=7f*Mathf.Sin(a*.7f+variation);liveFire[j]=particle;
    }fire.SetParticles(liveFire,count);
   }
  }
  internal void Advance(float now){
   if(finished||now<=lastStep)return;float previous=lastStep;lastStep=now;
   if(!ending){
    if(actor==null||!actor.gameObject.activeInHierarchy||health==null||health.Value<=0f||receiver==null)BeginEnding(now);
    else{
     Follow();float deadline=lastContact+owner.IgniteDuration;float seconds=Mathf.Max(0f,Mathf.Min(now,deadline)-previous);
     if(seconds>0f&&stacks>0)owner.ApplyFlameDamage(receiver,DamagePerSecond*seconds,actor,health);
     if(health.Value<=0f)BeginEnding(now);else if(now>=deadline)BeginEnding(deadline);
    }
   }
   if(particles==null&&!ReferenceEquals(owner,null))CreateVisuals();
   if(ending){float fade=Mathf.Clamp01(1f-(now-endingAt)/endingSeconds);ApplyFade(fade);if(fade<=0f)Finish();}else ApplyFade(1f);
  }
  void Follow(){
   if(followingCorpse){if(endingCorpse==null)return;transform.SetPositionAndRotation(endingCorpse.position,Quaternion.identity);}
   else{if(actor==null)return;transform.SetPositionAndRotation(actor.TransformPoint(localCentre)+Vector3.up*.15f,Quaternion.identity);}
   if(particles!=null&&fireAnchors!=null)for(int i=0;i<particles.Length;i++)if(particles[i]!=null&&fireAnchors[i]!=null)particles[i].transform.position=fireAnchors[i].TransformPoint(firePoints[i]);
  }
  void BeginEnding(float now){if(ending)return;ending=true;endingAt=now;}
  internal void EndOnCorpse(GameObject corpse,float now){
   if(finished||followingCorpse||corpse==null)return;followingCorpse=true;endingCorpse=corpse.transform;ending=true;endingAt=now;endingSeconds=1.1f;
   var bones=new List<Transform>();foreach(var skin in corpse.GetComponentsInChildren<SkinnedMeshRenderer>(true))foreach(var bone in skin.bones)if(bone!=null&&bone.IsChildOf(endingCorpse)&&!bones.Contains(bone))bones.Add(bone);
   if(particles!=null)for(int i=0;i<particles.Length;i++){
    var old=fireAnchors[i];Transform target=null;if(old!=null)foreach(var bone in bones)if(bone.name==old.name){target=bone;break;}
    if(target==null){float distance=float.PositiveInfinity;foreach(var bone in bones){float candidate=(bone.position-particles[i].transform.position).sqrMagnitude;if(candidate<distance){distance=candidate;target=bone;}}if(target==null)target=endingCorpse;firePoints[i]=target.InverseTransformPoint(particles[i].transform.position);}
    fireAnchors[i]=target;
   }Follow();
  }
  void CreateVisuals(){
   if(ending||actor==null||!actor.gameObject.activeInHierarchy)return;
   var material=owner.EnemyIgniteMaterial();if(material==null)return;
   var coverage=FlameBodyCoverage.Build(actor,owner.BodyFireCoverageUnavailable);int count=coverage.Length;
   particles=new ParticleSystem[count];renderers=new ParticleSystemRenderer[count];fireAnchors=new Transform[count];firePoints=new Vector3[count];properties=new MaterialPropertyBlock();fireMaterial=material;
   for(int i=0;i<count;i++){
    var point=coverage[i];fireAnchors[i]=point.Anchor;firePoints[i]=point.LocalPoint;
    var go=new GameObject("Flamethrower.EnemyFireTongues");go.transform.SetParent(transform,false);go.transform.position=point.World;
    var fire=go.AddComponent<ParticleSystem>();particles[i]=fire;
    owner.ConfigureFireTongues(fire,material,unchecked((uint)actorId*397u+(uint)i*7919u+1u),point.Height*.75f,point.Height,1.2f,false,.8f+(i%6)*.09f);renderers[i]=fire.GetComponent<ParticleSystemRenderer>();
    var main=fire.main;main.startSizeX=new ParticleSystem.MinMaxCurve(point.Width*.75f,point.Width);
    var velocity=fire.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.y=.12f;
   }
  }
  void ApplyFade(float fade){
   transform.localScale=Vector3.one*Mathf.Sqrt(Mathf.Clamp01(fade));if(renderers==null)return;
   if(ending||finished){if(bodySmoke!=null)bodySmoke.End(Time.time);}else if(bodySmoke==null&&!ReferenceEquals(owner,null))bodySmoke=owner.CreateFireSmoke(transform,FireSmokeKind.Enemy);
   for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null){renderers[i].enabled=fade>0f;if(!ReferenceEquals(owner,null)&&fireMaterial!=null)owner.FadeFireTongues(particles[i],fireMaterial,properties,fade);var emission=particles[i].emission;emission.rateOverTime=fade>0f?1.2f:0f;}
  }
  void Finish(){if(finished)return;finished=true;if(bodySmoke!=null)bodySmoke.End(Time.time);if(!ReferenceEquals(owner,null))owner.ForgetFlameIgnite(actorId,this);Destroy(gameObject);}
  internal void Cancel(){finished=true;ApplyFade(0f);Destroy(gameObject);}
  void OnDestroy(){if(!ReferenceEquals(owner,null))owner.ForgetFlameIgnite(actorId,this);}
 }
}
