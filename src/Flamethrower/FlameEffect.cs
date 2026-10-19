using System;
using System.IO;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace ChainsawPrototype {
 public partial class Plugin {
  const float FlameSpeed=FlameTrajectory.Speed;
  bool flameFiring,flameDamagePending,flameMuzzleReady,loggedNativeFlame;
  float flameNextDamage,flamePaidDamage,flameDamageAccumulator,flameFundedSeconds;
  int flameFuelFrame=-1;
  Vector3 flameMuzzleLocal;
  Transform flameMuzzleModel;
  ParticleSystem flameCore,flameHotStream,flameContact,flameSmoke,flameTrailSmoke;
  Material flameJetMaterial,flamePilotMaterial,flameSmokeMaterial;Texture2D nativeFlameAtlas,nativeScorchAtlas,nativeFirePuff,nativeSmokeMask;
  readonly RaycastHit[] flameHitBuffer=new RaycastHit[48];

  void SetupFlameEffects(){SceneManager.sceneLoaded+=OnFlameSceneLoaded;}
  void OnFlameSceneLoaded(Scene scene,LoadSceneMode mode){if(mode!=LoadSceneMode.Additive){OptionalBloodEffects.Clear();StopFlameFiring();CleanupFlamethrowerIgnite();ClearFuelPatches();CleanupFireSmoke();ClearFlameScorches();ClearFlameIgnition();ResetEmptyTankFeedback();guardedFuelWrites.Clear();}}

  void TickFlameFiring(){
   bool controllable=Time.timeScale>0f&&!Cursor.visible&&!ActualThirdPersonActive();
   bool requested=controllable&&Input.GetMouseButton(0);
   // LMB wins if both buttons are down: exactly one mode funds the tank.
   TickFuelSpray(controllable&&!requested&&Input.GetMouseButton(1));
   if(!requested){
    if(controllable)QueueFlameDamage();else{flameDamagePending=false;flameDamageAccumulator=0f;}
    flameFiring=false;flameNextDamage=flameFundedSeconds=0f;return;
   }
   if(flameFuelFrame==Time.frameCount)return;flameFuelFrame=Time.frameCount;flameFundedSeconds=0f;
   var fuel=activeFlame.GetComponentInChildren<ChainsawFuel>(true);var balance=FuelBalance(activeFlame.gameObject);
   float rate=FlameTuningValue(balance.Rate,5f,1f,20f),litresPerSecond=CurrentFlameCost*rate;
   float spent=fuel==null?0f:fuel.ConsumeContinuous(litresPerSecond*Time.deltaTime);
   if(spent<=0f){QueueFlameDamage();flameFiring=false;return;}
   // Damage uses only funded time. The attack rate still controls contact
   // cadence, while the tank drains smoothly even between those contacts.
   bool starting=!flameFiring;flameFiring=true;
   flameFundedSeconds=spent/litresPerSecond;
   flameDamageAccumulator+=FlameTuningValue(balance.Damage,20f,1f,200f)*flameFundedSeconds;
   if(starting)flameNextDamage=Time.time;
   if(Time.time>=flameNextDamage||!FlamethrowerFuelPolicy.CanBurn(fuel.Litres)){
    QueueFlameDamage();flameNextDamage=Time.time+1f/rate;
   }
   NoteEmptyFuelDebit(false,fuel,CurrentFlameCost);
  }
  void QueueFlameDamage(){
   if(flameDamageAccumulator<=0f)return;
   flamePaidDamage=(flameDamagePending?flamePaidDamage:0f)+flameDamageAccumulator;
   flameDamageAccumulator=0f;flameDamagePending=true;
  }
  void StopFlameFiring(){
   StopFuelSpray();
   ResetFlamethrowerIgniteContacts();
   StopFlamethrowerMuzzleHeat();
   ResetFlamethrowerRecoil();
   flameFiring=false;flameDamagePending=false;flameNextDamage=flamePaidDamage=flameDamageAccumulator=flameFundedSeconds=0f;flameFuelFrame=-1;flameMuzzleModel=null;
   if(flameParticles!=null)flameParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   if(flameContact!=null)flameContact.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   StopFlamethrowerAudio();
   StopFlamethrowerLighting();
  }
  Texture2D LoadNativeFlameTexture(string name){
   string path=Path.Combine(Path.GetDirectoryName(Info.Location),"Flamethrower","NativeEffects",name+".png");
   if(!File.Exists(path))return null;
   var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);ImageConversion.LoadImage(texture,File.ReadAllBytes(path));texture.name="Flamethrower.Native."+name;
   texture.hideFlags=HideFlags.DontUnloadUnusedAsset;texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;return texture;
  }
  Material NativeFlameMaterial(){
   if(flameJetMaterial!=null)return flameJetMaterial;
   var native=Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m=>(m.name=="M_VFX_Fire_02"||m.name=="M_VFX_Fire_02 (Instance)")&&m.shader!=null&&m.shader.isSupported);
   if(native!=null)flameJetMaterial=new Material(native);
   else{
    native=Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m=>(m.name=="ParticleFirecloud"||m.name=="ParticleFirecloud (Instance)")&&m.shader!=null&&m.shader.isSupported);
    if(native!=null)flameJetMaterial=new Material(native);
    else{nativeFirePuff=LoadNativeFlameTexture("FirePuff");if(nativeFirePuff==null)return null;flameJetMaterial=FlameAlphaMaterial(nativeFirePuff,"Flamethrower.NativeFirecloud");}
   }
   flameJetMaterial.name="Flamethrower.BroadNativeFlame";flameJetMaterial.hideFlags=HideFlags.DontUnloadUnusedAsset;flameJetMaterial.renderQueue=3000;
   // Daylight needs a luminous body as well as transparent turbulent edges.
   // Preserve the native moving mask; smoke is supplied by its separate layer.
   if(flameJetMaterial.HasProperty("_OpacityBoost"))flameJetMaterial.SetFloat("_OpacityBoost",2.4f);
   if(flameJetMaterial.HasProperty("_EmissionIntensity"))flameJetMaterial.SetFloat("_EmissionIntensity",6f);
   if(!loggedNativeFlame){Logger.LogInfo("Flamethrower broad stream uses "+(native==null?"native ParticleFirecloud texture":native.name)+" with dense, non-stretched billboards.");loggedNativeFlame=true;}
   nativeScorchAtlas=LoadNativeFlameTexture("Scorch");return flameJetMaterial;
  }
  Material FlameAlphaMaterial(Texture texture,string name){
   var shader=Shader.Find("Particles/Standard Unlit")??Shader.Find("Legacy Shaders/Particles/Alpha Blended");
   var material=new Material(shader){name=name,hideFlags=HideFlags.DontUnloadUnusedAsset};material.mainTexture=texture;
   material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_ZWrite",0);material.SetInt("_Cull",(int)CullMode.Off);
   if(material.HasProperty("_Mode"))material.SetFloat("_Mode",2f);
   foreach(string property in new[]{"_LightingEnabled","_EmissionEnabled","_SoftParticlesEnabled","_DistortionEnabled","_FlipbookMode"})if(material.HasProperty(property))material.SetFloat(property,0f);
   if(material.HasProperty("_Color"))material.SetColor("_Color",Color.white);if(material.HasProperty("_TintColor"))material.SetColor("_TintColor",Color.white);
   material.EnableKeyword("_ALPHABLEND_ON");material.DisableKeyword("_ALPHATEST_ON");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.DisableKeyword("SOFTPARTICLES_ON");material.renderQueue=3000;return material;
  }
  Material NativeFlamePilotMaterial(){
   if(flamePilotMaterial!=null)return flamePilotMaterial;
   var native=Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m=>m.name=="Flame1"||m.name=="Flame1 (Instance)");
   if(native!=null)flamePilotMaterial=new Material(native);
   else{
    nativeFlameAtlas=LoadNativeFlameTexture("Flame1");
    if(nativeFlameAtlas==null)return null;
    var shader=Shader.Find("Legacy Shaders/Particles/Anim Alpha Blended")??Shader.Find("Legacy Shaders/Particles/Alpha Blended")??Shader.Find("Particles/Standard Unlit");
    flamePilotMaterial=new Material(shader);flamePilotMaterial.mainTexture=nativeFlameAtlas;
    flamePilotMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);flamePilotMaterial.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);flamePilotMaterial.SetInt("_ZWrite",0);
    flamePilotMaterial.SetColor("_TintColor",new Color(.860294f,.669869f,.575638f,1f));flamePilotMaterial.SetFloat("_InvFade",3f);
    if(shader.name=="Particles/Standard Unlit"){flamePilotMaterial.SetFloat("_Mode",2f);flamePilotMaterial.EnableKeyword("_ALPHABLEND_ON");}
   }
   flamePilotMaterial.name="Flamethrower.NozzlePilot";flamePilotMaterial.hideFlags=HideFlags.DontUnloadUnusedAsset;flamePilotMaterial.DisableKeyword("SOFTPARTICLES_ON");flamePilotMaterial.renderQueue=3000;return flamePilotMaterial;
  }
  void NativeFlameAnimation(ParticleSystem effect){
   var sheet=effect.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=2;sheet.numTilesY=4;sheet.animation=ParticleSystemAnimationType.WholeSheet;
   sheet.startFrame=0f;sheet.cycleCount=1;sheet.timeMode=ParticleSystemAnimationTimeMode.Lifetime;sheet.uvChannelMask=(UVChannelFlags)(-1);
   // Exact lifetime frame curve from the native Flame ParticleSystem.
   sheet.frameOverTime=new ParticleSystem.MinMaxCurve(.9999f,new AnimationCurve(new Keyframe(0f,0f,2f,2f),new Keyframe(.160312f,.464415f,.824675f,.824675f),new Keyframe(1f,.983812f,.418221f,.418221f)));
  }
  static void NativeFlameVertexStreams(ParticleSystemRenderer renderer){renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV,ParticleSystemVertexStream.UV2});}
  static void FlameSurfaceCollision(ParticleSystem effect){
   var collision=effect.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;collision.mode=ParticleSystemCollisionMode.Collision3D;collision.quality=ParticleSystemCollisionQuality.High;
   // Native layer 10 contains enemy colliders only (all 176 inspected solids
   // have a shared Health owner). Pass them visually as well as for damage.
   int mask=Physics.DefaultRaycastLayers&~(1<<10),playerLayer=LayerMask.NameToLayer("Player");if(playerLayer>=0)mask&=~(1<<playerLayer);
   collision.collidesWith=mask;collision.enableDynamicColliders=true;collision.maxCollisionShapes=128;collision.radiusScale=.12f;collision.lifetimeLoss=1f;collision.bounce=0f;collision.dampen=1f;collision.sendCollisionMessages=false;
  }
  static void ConfigureFlameTrajectory(ParticleSystem effect){
   var main=effect.main;main.gravityModifier=0f;
   // Explicit world force makes drop independent of a scene's Physics.gravity.
   var force=effect.forceOverLifetime;force.enabled=true;force.space=ParticleSystemSimulationSpace.World;
   force.x=0f;force.y=-FlameTrajectory.DownwardAcceleration;force.z=0f;force.randomized=false;
  }
  ParticleSystem CreateFlameStream(){
   var material=NativeFlameMaterial();var pilotMaterial=NativeFlamePilotMaterial();if(material==null||pilotMaterial==null)return null;
   var go=new GameObject("Flamethrower.FlameStream");var effect=go.AddComponent<ParticleSystem>();effect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=effect.main;main.playOnAwake=false;main.loop=true;main.duration=1;main.startLifetime=CurrentFlameRange/FlameSpeed;main.startSpeed=FlameSpeed;main.startSize=new ParticleSystem.MinMaxCurve(.28f,.44f);main.startRotation=new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);main.maxParticles=384;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=-.025f;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
   var emission=effect.emission;emission.rateOverTime=360;
   var shape=effect.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=3.5f;shape.radius=.009f;
   // Live visual settings supply a translucent orange body and a darker tail.
   // The original procedural mask, motion, emission density and native pilot stay.
   var colours=effect.colorOverLifetime;colours.enabled=true;
   // Grow only after leaving the lip. A velocity-stretched newborn billboard
   // used to draw a long tail back inside the barrel despite a correct emitter.
   var sizes=effect.sizeOverLifetime;sizes.enabled=true;sizes.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,0,3.4375f,3.4375f),new Keyframe(.16f,.55f,1.488095f,1.488095f),new Keyframe(1,1.8f,1.488095f,1.488095f)));
   var noise=effect.noise;noise.enabled=true;noise.strength=new ParticleSystem.MinMaxCurve(.06f,AnimationCurve.Linear(0,0,1,1));noise.frequency=3;noise.scrollSpeed=2;noise.quality=ParticleSystemNoiseQuality.Low;
   FlameSurfaceCollision(effect);
   ConfigureFlameTrajectory(effect);
   var renderer=effect.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.alignment=ParticleSystemRenderSpace.View;renderer.velocityScale=0f;renderer.cameraVelocityScale=0f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
   var coreObject=new GameObject("Flamethrower.FlameCore");coreObject.transform.SetParent(go.transform,false);flameCore=coreObject.AddComponent<ParticleSystem>();flameCore.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   // The small animated pilot follows the nozzle exactly, including while
   // turning/aiming. The main jet keeps its world-space motion farther forward.
   coreObject.transform.localPosition=new Vector3(0,0,.008f);
   var core=flameCore.main;core.playOnAwake=false;core.loop=true;core.startLifetime=.055f;core.startSpeed=0f;core.startSize=new ParticleSystem.MinMaxCurve(.012f,.016f);core.startColor=new Color(1f,.9f,.7f,.9f);core.maxParticles=12;core.simulationSpace=ParticleSystemSimulationSpace.Local;
   var ce=flameCore.emission;ce.rateOverTime=130;var cs=flameCore.shape;cs.shapeType=ParticleSystemShapeType.Cone;cs.angle=0f;cs.radius=.001f;NativeFlameAnimation(flameCore);
   var cr=flameCore.GetComponent<ParticleSystemRenderer>();cr.sharedMaterial=pilotMaterial;cr.renderMode=ParticleSystemRenderMode.Billboard;cr.velocityScale=0f;cr.cameraVelocityScale=0f;cr.shadowCastingMode=ShadowCastingMode.Off;
   NativeFlameVertexStreams(cr);
   FlameSurfaceCollision(flameCore);
   // A narrow animated native-flame centre remains legible against sunlit sand.
   // Parent playback/obstruction/scene cleanup owns it just like the nozzle pilot.
   var hotObject=new GameObject("Flamethrower.HotStream");hotObject.transform.SetParent(go.transform,false);
   flameHotStream=hotObject.AddComponent<ParticleSystem>();flameHotStream.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var hot=flameHotStream.main;hot.playOnAwake=false;hot.loop=true;hot.startLifetime=CurrentFlameRange/FlameSpeed;hot.startSpeed=FlameSpeed;hot.startSize=new ParticleSystem.MinMaxCurve(.1f,.16f);hot.startRotation=new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);hot.maxParticles=128;hot.simulationSpace=ParticleSystemSimulationSpace.World;hot.gravityModifier=-.025f;hot.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
   var hotEmission=flameHotStream.emission;hotEmission.rateOverTime=120f;
   var hotShape=flameHotStream.shape;hotShape.shapeType=ParticleSystemShapeType.Cone;hotShape.angle=3.5f;hotShape.radius=.004f;
   var hotSizes=flameHotStream.sizeOverLifetime;hotSizes.enabled=true;hotSizes.size=sizes.size;
   var hotColours=flameHotStream.colorOverLifetime;hotColours.enabled=true;NativeFlameAnimation(flameHotStream);
   var hotRenderer=flameHotStream.GetComponent<ParticleSystemRenderer>();hotRenderer.sharedMaterial=pilotMaterial;hotRenderer.renderMode=ParticleSystemRenderMode.Billboard;hotRenderer.alignment=ParticleSystemRenderSpace.View;hotRenderer.velocityScale=0f;hotRenderer.cameraVelocityScale=0f;hotRenderer.shadowCastingMode=ShadowCastingMode.Off;hotRenderer.receiveShadows=false;
   NativeFlameVertexStreams(hotRenderer);FlameSurfaceCollision(flameHotStream);ConfigureFlameTrajectory(flameHotStream);
   var contactObject=new GameObject("Flamethrower.SurfaceFlame");flameContact=contactObject.AddComponent<ParticleSystem>();flameContact.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var contact=flameContact.main;contact.playOnAwake=false;contact.loop=false;contact.startLifetime=new ParticleSystem.MinMaxCurve(.12f,.22f);contact.startSpeed=new ParticleSystem.MinMaxCurve(.15f,.5f);contact.startSize=new ParticleSystem.MinMaxCurve(.12f,.24f);contact.maxParticles=32;contact.simulationSpace=ParticleSystemSimulationSpace.World;contact.startColor=new Color(1,.65f,.3f,.8f);
   var contactEmission=flameContact.emission;contactEmission.rateOverTime=0;var contactShape=flameContact.shape;contactShape.shapeType=ParticleSystemShapeType.Hemisphere;contactShape.radius=.035f;
   var contactRenderer=flameContact.GetComponent<ParticleSystemRenderer>();contactRenderer.sharedMaterial=material;contactRenderer.renderMode=ParticleSystemRenderMode.Billboard;contactRenderer.shadowCastingMode=ShadowCastingMode.Off;
   CreateMuzzleSmoke(go.transform);
   CreateFlameSmokeBackdrop(go.transform);
   CreateFlamethrowerAudio(go.transform);
   CreateFlamethrowerLighting(go.transform);
   return effect;
  }
  void CreateMuzzleSmoke(Transform parent){
   if(nativeSmokeMask==null)nativeSmokeMask=LoadNativeFlameTexture("Scorch");if(nativeSmokeMask==null)return;
   if(flameSmokeMaterial==null){flameSmokeMaterial=FlameAlphaMaterial(nativeSmokeMask,"Flamethrower.MuzzleSmoke");flameSmokeMaterial.renderQueue=2999;}
   var go=new GameObject("Flamethrower.MuzzleSmoke");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,0,.012f);
   flameSmoke=go.AddComponent<ParticleSystem>();flameSmoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=flameSmoke.main;main.playOnAwake=false;main.loop=true;main.startLifetime=new ParticleSystem.MinMaxCurve(.45f,.75f);main.startSpeed=new ParticleSystem.MinMaxCurve(.8f,1.4f);main.startSize=new ParticleSystem.MinMaxCurve(.045f,.09f);main.startRotation=new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);main.maxParticles=80;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=-.12f;
   var emission=flameSmoke.emission;emission.rateOverTime=44;var shape=flameSmoke.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12f;shape.radius=.004f;
   var velocity=flameSmoke.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.y=new ParticleSystem.MinMaxCurve(.2f);
   var colours=flameSmoke.colorOverLifetime;colours.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.34f,.31f,.27f),0),new GradientColorKey(new Color(.20f,.19f,.18f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.28f,.18f),new GradientAlphaKey(.16f,.6f),new GradientAlphaKey(0,1)});colours.color=gradient;
   var size=flameSmoke.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,0),new Keyframe(.2f,.8f),new Keyframe(1,2.7f)));
   var renderer=flameSmoke.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=flameSmokeMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
   FlameSurfaceCollision(flameSmoke);
  }
  RaycastHit? FlameCast(Vector3 origin,Vector3 direction,float distance,float radius,bool enemiesPass=false){
   // Native enemy solids use layer 10. Exclude them before physics queries so
   // horde LOS does not repeatedly inspect every intercepted bodypart. The
   // Health-owner check below also handles enemies moved to another layer.
   int mask=~(1<<2);if(enemiesPass)mask&=~(1<<10);
   int count=radius>0f?Physics.SphereCastNonAlloc(origin,radius,direction,flameHitBuffer,distance,mask,QueryTriggerInteraction.Collide):Physics.RaycastNonAlloc(origin,direction,flameHitBuffer,distance,mask,QueryTriggerInteraction.Collide);
   var hits=flameHitBuffer;
   // A full unsorted buffer can omit the nearest wall in a crowded horde.
   if(count==hits.Length){hits=radius>0f?Physics.SphereCastAll(origin,radius,direction,distance,mask,QueryTriggerInteraction.Collide):Physics.RaycastAll(origin,direction,distance,mask,QueryTriggerInteraction.Collide);count=hits.Length;}
   RaycastHit? nearest=null;
   for(int i=0;i<count;i++){
    var hit=hits[i];if(OwnCollider(hit.collider)||FlameWindowPassage.PassThrough(hit.collider))continue;
    Transform owner;PlayMakerFSM receiver;bool enemy=false;
    if(hit.collider.isTrigger){
     if(!QualifiedFlameTrigger(hit.collider))continue;
     enemy=TryFlameEnemy(hit.collider,out owner,out receiver)&&receiver!=null;
     if(!enemy)continue;
    }
    if(enemiesPass&&(enemy||TryFlameEnemy(hit.collider,out owner,out receiver)))continue;
    if(nearest==null||hit.distance<nearest.Value.distance)nearest=hit;
   }
   return nearest;
  }
  void PrepareFlameMuzzle(){
   if(flameMuzzleReady)return;var points=flameMesh.vertices;float tip=flameMesh.bounds.max.z;Vector3 total=Vector3.zero;int count=0;
   foreach(var point in points)if(point.z>=tip-.004f){total+=point;count++;}
   flameMuzzleLocal=count>0?total/count:new Vector3(0,0,tip);flameMuzzleReady=true;
  }
  // Called after the final saved holding pose, including the player's pre-cull
  // pass. Damage has a paid-interval latch, so multiple cameras cannot double it.
  internal void LateFlameEffects(){
   if(activeFlame==null||activeFlame.HoldingModel==null||flamePlayer==null)return;
   if(flameParticles==null)flameParticles=CreateFlameStream();if(flameParticles==null)return;
   if(flameMuzzleModel==null||!flameMuzzleModel.IsChildOf(activeFlame.HoldingModel)){flameMuzzleModel=activeFlame.HoldingModel.Find("Flamethrower");if(flameMuzzleModel==null)flameMuzzleModel=activeFlame.HoldingModel;}
   PrepareFlameMuzzle();var view=flamePlayer.transform;var origin=flameMuzzleModel.TransformPoint(flameMuzzleLocal);
   var aimed=FlameCast(view.position,view.forward,CurrentFlameRange+.75f,0f,true);var worldTarget=aimed==null?view.position+view.forward*(CurrentFlameRange+.75f):aimed.Value.point;
   var crosshairContact=FlameCast(view.position,view.forward,CurrentFlameRange+.75f,0f);Vector3? enemyPoint=null;Transform enemyOwner;PlayMakerFSM enemyReceiver;
   if(crosshairContact!=null&&TryFlameEnemy(crosshairContact.Value.collider,out enemyOwner,out enemyReceiver))enemyPoint=crosshairContact.Value.point;
   var target=ChooseFlameAimTarget(origin,view.forward,worldTarget,enemyPoint);
   var toMuzzle=origin-view.position;float muzzleDistance=toMuzzle.magnitude;
   // A barrel poking through a close wall must not shoot backward at the player
   // or start a cast inside the wall and hit something on its far side.
   var obstruction=muzzleDistance>.01f?FlameCast(view.position,toMuzzle/muzzleDistance,muzzleDistance+.03f,0f,true):null;
   bool blocked=obstruction!=null||(aimed!=null&&Vector3.Dot(worldTarget-origin,view.forward)<=.03f);
   var direction=blocked?view.forward:(target-origin).normalized;if(direction.sqrMagnitude<.5f)direction=view.forward;
   float distance=.02f;
   var contact=blocked?(obstruction??aimed):FlameTrajectoryCast(origin,direction,CurrentFlameRange,FlameTrajectory.NozzleRadius,out distance);
   LateFuelSpray(origin,direction,distance,contact,blocked);
   // Keep the stream independent of inherited arm/model scale. Its origin comes
   // from the actual posed barrel renderer, not the provisional holding root.
   flameParticles.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(direction,view.up));
   var main=flameParticles.main;main.startLifetime=Mathf.Max(.001f,distance/FlameSpeed);
   // Size controls are applied before the existing contact-distance envelope;
   // larger settings cannot grow a short stream backward over the nozzle.
   ApplyFlameVisualTuning(distance);
   var noise=flameParticles.noise;var turbulence=noise.strength;turbulence.curveMultiplier=Mathf.Min(.06f,distance*.04f);noise.strength=turbulence;
   bool firing=flameFiring&&Time.timeScale>0f&&!ActualThirdPersonActive();
   UpdateFlamethrowerMuzzleHeat(firing,blocked);
   UpdateFlamethrowerLighting(origin,direction,distance,firing,blocked);
   if(firing&&!blocked){if(!flameParticles.isEmitting)flameParticles.Play(true);}else if(flameParticles.isEmitting)flameParticles.Stop(true,blocked?ParticleSystemStopBehavior.StopEmittingAndClear:ParticleSystemStopBehavior.StopEmitting);
   UpdateStreamSmoke(firing&&!blocked);
   UpdateFlamethrowerAudio(firing);
   // Visual fire follows continuous paid contact even at a low attack-tick
   // setting. Its own creation budget prevents render callbacks making trails.
   if(firing&&!blocked&&contact!=null){PlaceFlameSurfaceFire(contact.Value);IgniteFuelAt(contact.Value.point,.15f,Time.time);}
   if(firing&&!blocked&&contact!=null&&flameFuelFrame==Time.frameCount)HeatFlameContainer(contact.Value.collider,flameFundedSeconds);
   // Observe real, funded contact once per rendered frame. Damage intervals
   // alone cannot establish where the stream pointed between their endpoints.
   if(firing&&!blocked&&flameFuelFrame==Time.frameCount)IgniteFlameEnemies(origin,direction,distance,flameFundedSeconds);
   if(!flameDamagePending||Time.timeScale<=0f||ActualThirdPersonActive())return;flameDamagePending=false;
   if(!blocked)DamageFlameEnemies(origin,direction,distance);
   if(contact==null)return;var hit=contact.Value;DamageFlameReceiver(FlameDamageReceiver(hit.collider));
   EmitFlameContact(hit.point,hit.normal);
   PlaceFlameScorch(hit,nativeScorchAtlas);
  }
  void CleanupFlameEffects(){
   CleanupFuelSpray();ClearFuelPatches();CleanupFireSmoke();
   CleanupFlamethrowerMuzzleHeat();CleanupFlamethrowerSurfaceFire();ClearFlameIgnition();
   StopFlameFiring();ClearFlameScorches();if(flameParticles!=null)Destroy(flameParticles.gameObject);if(flameContact!=null)Destroy(flameContact.gameObject);
   if(flameJetMaterial!=null)Destroy(flameJetMaterial);if(flamePilotMaterial!=null)Destroy(flamePilotMaterial);if(flameSmokeMaterial!=null)Destroy(flameSmokeMaterial);
   if(nativeFlameAtlas!=null)Destroy(nativeFlameAtlas);if(nativeScorchAtlas!=null)Destroy(nativeScorchAtlas);if(nativeFirePuff!=null)Destroy(nativeFirePuff);if(nativeSmokeMask!=null)Destroy(nativeSmokeMask);
   CleanupFlamethrowerAudio();
  }
  void OnDestroy(){SceneManager.sceneLoaded-=OnFlameSceneLoaded;ClearWeaponMerchantRequests();CleanupFlameEffects();CleanupChainsawHitShake();CleanupFlamethrowerCrosshair();CleanupEmptyTankFeedback();CleanupWeaponFeedbackRuntime();}
 }
}
