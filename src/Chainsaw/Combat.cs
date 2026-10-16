using System;
using System.Linq;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  ConfigEntry<float> sawDamage,sawReach,sawVolume;AudioClip engineClip,impactClip;AudioSource sawEngine,sawHit;bool swinging,hitThisSwing;float hitStart,hitEnd;Vector3 tipLocal,lastTip;Transform tipModel;readonly RaycastHit[] hitBuffer=new RaycastHit[48];float nextSoundSearch;bool loggedHitArea;
  bool engineUsingFallback;float nextEngineAudioWarning;
  void SetupCombat(){sawDamage=Config.Bind("Combat","Damage",50f,new ConfigDescription("Damage per swing, using native Bodypart damage events.",new AcceptableValueRange<float>(1,200)));sawReach=Config.Bind("Combat","Reach",1.8f,new ConfigDescription("Maximum melee contact distance in metres for both attacks.",new AcceptableValueRange<float>(.5f,3f)));sawVolume=FixedSetting("Audio","Engine volume",.3f);sawFuelCost=Config.Bind("Combat","Fuel per swing",.1f,new ConfigDescription("Litres used per LMB swing, including misses.",new AcceptableValueRange<float>(.01f,.5f)));sawAttackRate=Config.Bind("Combat","Attack rate",1f/1.2f,new ConfigDescription("LMB swings per second. Saved attack paths are retained; timing scales around their original duration.",new AcceptableValueRange<float>(.25f,3f)));}
  public static void BeginMeleeSwing(float windup,float contact){if(ReferenceEquals(instance,null)||!HasFuelForAttack||instance.swinging)return;instance.BeginSwingFuel();instance.swinging=true;instance.BeginChainsawSwingShake();instance.hitThisSwing=false;instance.hitStart=Mathf.Clamp(windup,.02f,5);instance.hitEnd=Mathf.Clamp(windup,.02f,5)+Mathf.Clamp(contact,.02f,5);instance.PrepareBladeTip();if(instance.preview!=null)instance.lastTip=instance.preview.transform.TransformPoint(instance.tipLocal);}
  public static void EndMeleeSwing(){if(!ReferenceEquals(instance,null))instance.swinging=false;}
  public static void UpdateMeleeSwing(float time){if(ReferenceEquals(instance,null)||!instance.enabledPreview||instance.preview==null)return;instance.MeterSwingFuel(time);instance.UpdateChainsawSwingShake(time);instance.SwingContact(time);}
  void PrepareBladeTip(){if(preview==null||tipModel==preview.transform)return;tipModel=preview.transform;Vector3 grip=tipModel.InverseTransformPoint(right.position);float far=-1;foreach(var vertex in heldMesh.vertices){float d=(vertex-grip).sqrMagnitude;if(d>far){far=d;tipLocal=vertex;}}}
  bool OwnCollider(Collider c){if(c==null)return true;for(var p=c.transform;p!=null;p=p.parent)if(p.name=="Player"||p.name=="PlayerCameraHolder"||p.name=="HandItemUse"||p.name.StartsWith("Chainsaw."))return true;return false;}
  RaycastHit? Nearest(Vector3 origin,Vector3 direction,float distance,float radius){int count=Physics.SphereCastNonAlloc(origin,radius,direction,hitBuffer,distance,MeleeHitArea.LayerMask,MeleeHitArea.Triggers);RaycastHit? best=null;for(int i=0;i<count;i++){var h=hitBuffer[i];if(OwnCollider(h.collider))continue;if(best==null||h.distance<best.Value.distance)best=h;}return best;}
  void SwingContact(float time){
   if(!swinging||hitThisSwing||time<hitStart||time>hitEnd||camera==null)return;
   // Native wrench casts from PlayerCamera, not the near clipping plane.
   Ray aim=new Ray(camera.position,camera.forward);
   float radius,nativeReach;RaycastHit? contact;
   if(MeleeHitArea.TryGetWrenchShape(out radius,out nativeReach)){
    if(!loggedHitArea){Logger.LogInfo("Chainsaw melee area: native wrench radius="+radius+" m; wrench reach="+nativeReach+" m; chainsaw reach="+sawReach.Value+" m; triggers="+MeleeHitArea.Triggers+"; mask="+MeleeHitArea.LayerMask);loggedHitArea=true;}
    contact=Nearest(aim.origin,aim.direction,sawReach.Value,radius);
   }else{
    // Scene initialization may precede the native wrench FSM. Retry until its exact radius is available.
    int count=Physics.RaycastNonAlloc(aim,hitBuffer,sawReach.Value,~0,QueryTriggerInteraction.Ignore);contact=null;
    for(int i=0;i<count;i++){var hit=hitBuffer[i];if(OwnCollider(hit.collider))continue;if(contact==null||hit.distance<contact.Value.distance)contact=hit;}
   }
   if(contact!=null){hitThisSwing=true;ProcessHit(contact.Value);}
  }
  void ProcessHit(RaycastHit hit){ProcessHit(hit,sawDamage.Value*SwingFuelDamageScale,true,3f*SwingFuelDamageScale,true);}
  void ProcessHit(RaycastHit hit,float damage,bool effects,float impulse,bool log){
   // The windup or cutting interval has paid for this contact, including any
   // final partial amount. Its funded damage remains valid on an empty tank.
   // Native Damage can destroy/detach the receiver immediately. Resolve its
   // surface identity before sending that event, while the collider is valid.
   Transform actor;PlayMakerFSM actorReceiver;var surfaceKind=effects&&TryFlameEnemy(hit.collider,out actor,out actorReceiver)?ChainsawSurface.Flesh:ChainsawSurface.Other;
   PlayMakerFSM receiver=null;for(var p=hit.collider.transform;p!=null;p=p.parent){receiver=p.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="Bodypart"&&f.FsmVariables.FindFsmFloat("Damage")!=null);if(receiver!=null)break;}
   bool damaged=receiver!=null;if(damaged){receiver.FsmVariables.FindFsmFloat("Damage").Value=-Mathf.Abs(damage);receiver.SendEvent("Damage");if(log)Logger.LogInfo("Chainsaw hit "+receiver.gameObject.name+" for "+damage);}
   if(effects)StandardMeleeHit(hit.point,hit.normal,surfaceKind);
   var body=hit.rigidbody;if(body!=null&&!body.isKinematic&&body.mass<100)body.AddForceAtPosition(camera.forward*impulse,hit.point,ForceMode.Impulse);
  }
  void UpdateSawPresentation(){
   UpdateChainsawHitShake();
   bool active=enabledPreview&&preview!=null&&Time.timeScale>0&&(HasFuelForAttack||swinging||cuttingHeld);
   if(!active){swinging=false;if(sawEngine!=null){sawEngine.volume=Mathf.MoveTowards(sawEngine.volume,0f,Time.unscaledDeltaTime*2f);if(sawEngine.volume<=.0001f&&sawEngine.isPlaying)sawEngine.Stop();}return;}
   PrepareSawSounds();
   if(sawEngine==null){var go=new GameObject("Chainsaw.EngineAudio");go.transform.SetParent(preview.transform,false);sawEngine=go.AddComponent<AudioSource>();sawEngine.loop=true;sawEngine.spatialBlend=0f;sawEngine.dopplerLevel=0f;sawEngine.priority=32;sawEngine.volume=0f;sawEngine.playOnAwake=false;var impact=new GameObject("Chainsaw.ImpactAudio");sawHit=impact.AddComponent<AudioSource>();sawHit.spatialBlend=.7f;sawHit.minDistance=1;sawHit.maxDistance=15;

   }
   // Held equipment takes priority over ordinary world engines in busy new worlds.
   sawEngine.enabled=true;sawEngine.mute=false;sawEngine.priority=32;
   if(sawEngine.clip!=engineClip){sawEngine.Stop();sawEngine.clip=engineClip;}
   sawEngine.volume=Mathf.MoveTowards(sawEngine.volume,sawVolume.Value,Time.unscaledDeltaTime*2f);sawEngine.pitch=Mathf.Lerp(sawEngine.pitch,swinging?1.55f:cuttingHeld?1.4f:.85f,Time.unscaledDeltaTime*12);if(engineClip!=null&&!sawEngine.isPlaying)sawEngine.Play();
   if(Time.unscaledTime>=nextEngineAudioWarning&&(sawEngine.isVirtual||AudioListener.pause||AudioListener.volume<=.00001f||engineClip==null)){
    nextEngineAudioWarning=Time.unscaledTime+10f;
    Logger.LogWarning("Chainsaw audio state: virtual="+sawEngine.isVirtual+", listenerPaused="+AudioListener.pause+", listenerVolume="+AudioListener.volume+", engineVolume="+sawEngine.volume+", clip="+(engineClip==null?"null":engineClip.name));
   }
  }
  void PrepareSawSounds(){
   if(engineClip!=null&&!engineUsingFallback)return;
   if(Time.unscaledTime<nextSoundSearch)return;nextSoundSearch=Time.unscaledTime+3f;
   var clips=Resources.FindObjectsOfTypeAll<AudioClip>();
   impactClip=clips.FirstOrDefault(c=>c.name=="rock_impact")??clips.FirstOrDefault(c=>c.name=="wrench1");
   var native=clips.FirstOrDefault(c=>c.name=="EngineRunning - MotorcycleSingleCylinder");
   var prepared=SmoothEngineLoop(native);engineUsingFallback=prepared==null;
   if(prepared!=null){engineClip=prepared;return;}
   // Retry native audio after startup rather than permanently caching a silent clip.
   if(engineClip==null){engineClip=SmoothEngineLoop(MakeEngine());Logger.LogWarning("Chainsaw uses its engine fallback while native engine audio is unavailable.");}
  }
  AudioClip SmoothEngineLoop(AudioClip original){
   if(original==null)return null;
   if(original.loadState!=AudioDataLoadState.Loaded){original.LoadAudioData();if(original.loadState!=AudioDataLoadState.Loaded)return null;}
   if(original.samples<2048)return null;
   int channels=original.channels,fade=Mathf.Min(original.frequency/25,original.samples/8);var source=new float[original.samples*channels];
   if(!original.GetData(source,0))return null;
   float peak=0f;foreach(float sample in source){if(!Finite(sample))return null;peak=Mathf.Max(peak,Mathf.Abs(sample));}
   if(peak<.00001f)return null;
   int count=original.samples-fade;var output=new float[count*channels];
   System.Array.Copy(source,fade*channels,output,0,(original.samples-2*fade)*channels);
   for(int frame=0;frame<fade;frame++){float mix=frame/(float)(fade-1);mix=mix*mix*(3-2*mix);for(int channel=0;channel<channels;channel++)output[(count-fade+frame)*channels+channel]=source[(original.samples-fade+frame)*channels+channel]*(1-mix)+source[frame*channels+channel]*mix;}
   var result=AudioClip.Create("Chainsaw.EngineSeamless",count,channels,original.frequency,false);if(!result.SetData(output,0)){Destroy(result);return null;}result.hideFlags=HideFlags.DontUnloadUnusedAsset;Logger.LogInfo("Chainsaw engine loop ready: source="+original.name+", peak="+peak.ToString("0.0000")+", 40 ms boundary crossfade.");return result;
  }
  AudioClip MakeEngine(){int rate=22050;var values=new float[rate];for(int i=0;i<rate;i++){float t=i/(float)rate;float pulse=(t*85)%1;values[i]=((pulse*2-1)*.26f+Mathf.Sin(t*2*Mathf.PI*170)*.12f+Mathf.Sin(t*2*Mathf.PI*340)*.06f)*( .8f+.2f*Mathf.Sin(t*2*Mathf.PI*17));}var clip=AudioClip.Create("Chainsaw.EngineFallback",rate,1,rate,false);clip.SetData(values,0);clip.hideFlags=HideFlags.DontUnloadUnusedAsset;return clip;}
  ParticleSystem MakeImpact(string label,Color color,float size){var go=new GameObject("Chainsaw."+label);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=.5f;main.startLifetime=.3f;main.startSpeed=1.1f;main.startSize=size;main.startColor=color;main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=.5f;var em=ps.emission;em.enabled=true;em.rateOverTime=0;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=35;shape.radius=.02f;var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=new Material(Shader.Find("Particles/Standard Unlit")??Shader.Find("Legacy Shaders/Particles/Alpha Blended"));renderer.sharedMaterial.mainTexture=ImpactTexture();return ps;}
  Texture2D particleTexture;
  Texture2D ImpactTexture(){if(particleTexture!=null)return particleTexture;particleTexture=new Texture2D(16,16,TextureFormat.RGBA32,false);var pixels=new Color[256];for(int y=0;y<16;y++)for(int x=0;x<16;x++){float d=new Vector2((x-7.5f)/7.5f,(y-7.5f)/7.5f).magnitude;pixels[y*16+x]=new Color(1,1,1,Mathf.Clamp01(1-d));}particleTexture.SetPixels(pixels);particleTexture.Apply();return particleTexture;}  void EmitImpact(ParticleSystem ps,Vector3 point,Vector3 normal,int amount){if(ps==null)return;ps.transform.position=point+normal*.01f;ps.transform.rotation=Quaternion.LookRotation(normal);ps.Emit(amount);}
 }
}
