using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChainsawPrototype {
 internal enum FireSmokeKind { Enemy,Ground,Corpse }
 public partial class Plugin {
  ConfigEntry<bool> fireSmokeMaster,fireSmokeEnemies,fireSmokeGround,fireSmokeCorpses;
  readonly HashSet<FlameFireSmoke> smokeEffects=new HashSet<FlameFireSmoke>();
  GameObject fireSmokeRoot;Material sharedFireSmoke;
  void SetupFireSmoke(){
   const string group="Flamethrower - Smoke";
   fireSmokeMaster=Config.Bind(group,"Enabled",true,"Enable all Weapons Pack fire smoke. Turning this off also removes active smoke to reduce rendering cost.");
   fireSmokeEnemies=Config.Bind(group,"Burning enemies",true,"Smoke from enemies while ignite is active.");
   fireSmokeGround=Config.Bind(group,"Ground fire",true,"Smoke from burning fuel patches and temporary surface fire.");
   fireSmokeCorpses=Config.Bind(group,"Burnt corpses",true,"Brief smoke after flame or ignite kills, following the corpse.");
  }
  internal bool FireSmokeEnabled(FireSmokeKind kind){return (fireSmokeMaster==null||fireSmokeMaster.Value)&&(kind==FireSmokeKind.Enemy?(fireSmokeEnemies==null||fireSmokeEnemies.Value):kind==FireSmokeKind.Ground?(fireSmokeGround==null||fireSmokeGround.Value):(fireSmokeCorpses==null||fireSmokeCorpses.Value));}
  internal FlameFireSmoke CreateFireSmoke(Transform source,FireSmokeKind kind,Renderer body=null){
   if(source==null||!FireSmokeEnabled(kind)||smokeEffects.Count>=(kind==FireSmokeKind.Corpse?32:24))return null;
   if(sharedFireSmoke==null){if(nativeSmokeMask==null)nativeSmokeMask=LoadNativeFlameTexture("Scorch");if(nativeSmokeMask==null)return null;sharedFireSmoke=FlameAlphaMaterial(nativeSmokeMask,"Flamethrower.FireSmoke");sharedFireSmoke.renderQueue=3001;}
   if(fireSmokeRoot==null){fireSmokeRoot=new GameObject("Flamethrower.FireSmokeRuntime"){hideFlags=HideFlags.DontSave};fireSmokeRoot.AddComponent<FireSmokeRoot>().Owner=this;}
   var go=new GameObject("Flamethrower."+kind+"Smoke"){hideFlags=HideFlags.DontSave};go.transform.SetParent(fireSmokeRoot.transform,false);
   var smoke=go.AddComponent<FlameFireSmoke>();smoke.Configure(this,source,kind,body,sharedFireSmoke,Time.time);smokeEffects.Add(smoke);return smoke;
  }
  void StartCorpseSmoke(GameObject corpse){
   if(!FireSmokeEnabled(FireSmokeKind.Corpse))return;Renderer body=null;float largest=-1;
   foreach(var renderer in corpse.GetComponentsInChildren<Renderer>(true)){if(!(renderer is SkinnedMeshRenderer)&&!(renderer is MeshRenderer))continue;float size=renderer.bounds.size.sqrMagnitude;if(size>largest){largest=size;body=renderer;}}
   CreateFireSmoke(corpse.transform,FireSmokeKind.Corpse,body);
  }
  internal void ForgetFireSmoke(FlameFireSmoke smoke){smokeEffects.Remove(smoke);}
  internal void FireSmokeRootGone(GameObject root){if(!ReferenceEquals(root,fireSmokeRoot))return;smokeEffects.Clear();fireSmokeRoot=null;if(sharedFireSmoke!=null)Destroy(sharedFireSmoke);sharedFireSmoke=null;}
  void CleanupFireSmoke(){var root=fireSmokeRoot;if(root!=null)Destroy(root);FireSmokeRootGone(root);}
  void UpdateStreamSmoke(bool emitting){
   bool enabled=fireSmokeMaster==null||fireSmokeMaster.Value;
   StreamSmokeState(flameSmoke,enabled,emitting);StreamSmokeState(flameTrailSmoke,enabled,emitting);
  }
  static void StreamSmokeState(ParticleSystem particles,bool enabled,bool emitting){if(particles==null)return;if(!enabled){if(particles.isPlaying||particles.particleCount>0)particles.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);}else if(emitting&&!particles.isEmitting)particles.Play(false);}
 }
 internal sealed class FireSmokeRoot:MonoBehaviour {internal Plugin Owner;void OnDestroy(){if(!ReferenceEquals(Owner,null))Owner.FireSmokeRootGone(gameObject);}}
 internal sealed class FlameFireSmoke:MonoBehaviour {
  Plugin owner;Transform source;Renderer body;FireSmokeKind kind;ParticleSystem particles;float born,endingAt=float.PositiveInfinity,nextCorpsePuff;bool finished;SmokePoint[] corpsePoints;int corpseCursor;
  internal void Configure(Plugin plugin,Transform target,FireSmokeKind context,Renderer bodyRenderer,Material material,float now){
   owner=plugin;source=target;kind=context;body=bodyRenderer;born=now;bool corpse=kind==FireSmokeKind.Corpse;
   particles=gameObject.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=particles.main;main.playOnAwake=false;main.loop=true;main.startLifetime=corpse?new ParticleSystem.MinMaxCurve(.65f,1f):new ParticleSystem.MinMaxCurve(.9f,1.5f);main.startSpeed=new ParticleSystem.MinMaxCurve(.06f,.14f);main.startSize=corpse?new ParticleSystem.MinMaxCurve(.28f,.48f):new ParticleSystem.MinMaxCurve(.12f,.28f);main.startRotation=new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);main.startColor=corpse?new Color(.48f,.46f,.43f,.85f):new Color(.3f,.29f,.27f,.45f);main.maxParticles=corpse?24:kind==FireSmokeKind.Ground?10:16;main.simulationSpace=ParticleSystemSimulationSpace.World;
   var emission=particles.emission;emission.rateOverTime=corpse?0f:kind==FireSmokeKind.Ground?3f:5f;
   var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=kind==FireSmokeKind.Ground?.2f:.09f;if(corpse)shape.enabled=false;
   var velocity=particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.y=.18f;
   var colours=particles.colorOverLifetime;colours.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.75f,.75f,.75f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(corpse?.8f:.55f,.15f),new GradientAlphaKey(corpse?.35f:.2f,.65f),new GradientAlphaKey(0,1)});colours.color=gradient;
   var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.5f),new Keyframe(1,1.8f)));
   var visual=particles.GetComponent<ParticleSystemRenderer>();visual.sharedMaterial=material;visual.renderMode=ParticleSystemRenderMode.Billboard;visual.shadowCastingMode=ShadowCastingMode.Off;visual.receiveShadows=false;
   Follow();particles.Play(true);if(corpse){CacheCorpsePoints();for(int i=0;i<corpsePoints.Length;i++)EmitCorpsePuff();nextCorpsePuff=now+.08f;}else particles.Emit(1);
  }
  // Cache a small, spatially spread set once. No mesh baking, per-frame
  // hierarchy scan or dependence on species-specific bone names is required.
  struct SmokePoint {
   internal Transform Anchor;internal Vector3 Local;internal Collider Surface;internal Renderer Visual;
   internal Vector3 Raw {get{return Anchor==null?Vector3.zero:Anchor.TransformPoint(Local);}}
   internal Vector3 Position {
    get{
     var point=Raw+Vector3.up*.08f;
     if(Surface!=null&&Surface.enabled&&Surface.gameObject.activeInHierarchy)return Outside(Surface.bounds,point);
     if(Visual!=null)return Outside(Visual.bounds,point);
     return point+Vector3.up*.04f;
    }
   }
  }
  static Vector3 Outside(Bounds bounds,Vector3 point){
   if(!bounds.Contains(point))return point;
   // Choose the nearest top/side face. Never push puffs below the corpse.
   float nearest=bounds.max.y-point.y;int face=0;
   float d=point.x-bounds.min.x;if(d<nearest){nearest=d;face=1;}d=bounds.max.x-point.x;if(d<nearest){nearest=d;face=2;}
   d=point.z-bounds.min.z;if(d<nearest){nearest=d;face=3;}d=bounds.max.z-point.z;if(d<nearest){face=4;}
   if(face==0)point.y=bounds.max.y+.035f;else if(face==1)point.x=bounds.min.x-.035f;else if(face==2)point.x=bounds.max.x+.035f;else if(face==3)point.z=bounds.min.z-.035f;else point.z=bounds.max.z+.035f;
   return point;
  }
  Collider FindBoneSurface(Transform bone){
   for(var node=bone;node!=null;node=node.parent){
    foreach(var collider in node.GetComponents<Collider>())if(!collider.isTrigger)return collider;
    if(node==source)break;
   }return null;
  }
  void CacheCorpsePoints(){
   var candidates=new List<SmokePoint>();var seen=new HashSet<Transform>();
   foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))foreach(var bone in skin.bones)
    if(bone!=null&&bone.IsChildOf(source)&&seen.Add(bone))candidates.Add(new SmokePoint{Anchor=bone,Surface=FindBoneSurface(bone),Visual=body});
   if(candidates.Count==0)foreach(var visual in source.GetComponentsInChildren<Renderer>(true)){
    if(!(visual is MeshRenderer)&&!(visual is SkinnedMeshRenderer))continue;
    var bounds=visual.bounds;Vector3 axis=bounds.size.x>=bounds.size.y&&bounds.size.x>=bounds.size.z?Vector3.right:bounds.size.y>=bounds.size.z?Vector3.up:Vector3.forward;
    float extent=Vector3.Dot(bounds.extents,axis);
    for(int i=0;i<5;i++){var point=bounds.center+axis*(extent*(-.8f+i*.4f));candidates.Add(new SmokePoint{Anchor=visual.transform,Local=visual.transform.InverseTransformPoint(point),Surface=visual.GetComponent<Collider>(),Visual=visual});}
   }
   if(candidates.Count==0)candidates.Add(new SmokePoint{Anchor=source});
   int count=Mathf.Min(8,candidates.Count);corpsePoints=new SmokePoint[count];var used=new bool[candidates.Count];Vector3 centre=body==null?source.position:body.bounds.center;
   for(int i=0;i<count;i++){
    int best=-1;float score=float.NegativeInfinity;
    for(int j=0;j<candidates.Count;j++)if(!used[j]){
     float distance=i==0?-(candidates[j].Raw-centre).sqrMagnitude:float.PositiveInfinity;
     if(i>0)for(int k=0;k<i;k++)distance=Mathf.Min(distance,(candidates[j].Raw-corpsePoints[k].Raw).sqrMagnitude);
     if(distance>score){score=distance;best=j;}
    }
    corpsePoints[i]=candidates[best];used[best]=true;
   }
  }
  void EmitCorpsePuff(){
   if(corpsePoints==null||corpsePoints.Length==0)return;var point=corpsePoints[corpseCursor++%corpsePoints.Length];if(point.Anchor==null)return;
   var emission=new ParticleSystem.EmitParams{position=point.Position,applyShapeToPosition=false};particles.Emit(emission,1);
  }
  internal void End(float now){if(float.IsPositiveInfinity(endingAt)){endingAt=now;particles.Stop(false,ParticleSystemStopBehavior.StopEmitting);}}
  void Update(){Advance(Time.time);}
  void LateUpdate(){if(!finished)Follow();}
  void Follow(){if(body!=null)transform.position=body.bounds.center+Vector3.up*.06f;else if(source!=null)transform.position=source.position+Vector3.up*.08f;}
  internal void Advance(float now){
   if(finished)return;if(ReferenceEquals(owner,null)||!owner.FireSmokeEnabled(kind)){Finish();return;}
   if(source==null)End(now);else if(kind==FireSmokeKind.Corpse&&now-born>=2f)End(born+2f);
   if(!float.IsPositiveInfinity(endingAt)){if(now-endingAt>=(kind==FireSmokeKind.Corpse?1.05f:1.6f))Finish();return;}
   if(kind==FireSmokeKind.Corpse){int catchup=8;while(now>=nextCorpsePuff&&catchup-->0){EmitCorpsePuff();nextCorpsePuff+=.08f;}if(nextCorpsePuff<now)nextCorpsePuff=now+.08f;}
  }
  void Finish(){if(finished)return;finished=true;particles.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);particles.GetComponent<ParticleSystemRenderer>().enabled=false;if(!ReferenceEquals(owner,null))owner.ForgetFireSmoke(this);Destroy(gameObject);}
  void OnDestroy(){if(!ReferenceEquals(owner,null))owner.ForgetFireSmoke(this);}
 }
}
