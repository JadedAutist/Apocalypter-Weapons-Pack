using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace ChainsawPrototype {
 public partial class Plugin {
  readonly Dictionary<int,FlameDeathWatch> flameDeathWatches=new Dictionary<int,FlameDeathWatch>();
  readonly List<int> expiredFlameDeaths=new List<int>();
  Texture flameBurnTexture;float nextBurnTexture;bool warnedBurnCapture,warnedBurnSurface,warnedCookedHarvest,burnSkinUnavailable;
  internal void ApplyFlameDamage(PlayMakerFSM receiver,float amount,Transform knownActor=null,FsmFloat knownHealth=null){
   if(receiver==null||receiver.Fsm==null||!Finite(amount)||amount<=0f)return;
   var damage=receiver.FsmVariables.FindFsmFloat("Damage");if(damage==null)return;
   Transform actor=knownActor;FsmFloat health=knownHealth;PlayMakerFSM healthFsm=null;
   for(var node=receiver.transform;node!=null&&health==null;node=node.parent)
    foreach(var fsm in node.GetComponents<PlayMakerFSM>())if(fsm!=null&&fsm.Fsm!=null&&fsm.FsmName=="Health"){
     var value=fsm.FsmVariables.FindFsmFloat("Health");if(value!=null){actor=node;health=value;healthFsm=fsm;break;}
    }
   FlameDeathWatch watch=null;
   if(actor!=null&&health.Value>0f){
    EnsureFlameIgniteRoot();int id=actor.GetInstanceID();
    if(!flameDeathWatches.TryGetValue(id,out watch)){
     if(healthFsm==null)foreach(var fsm in actor.GetComponents<PlayMakerFSM>())if(fsm!=null&&fsm.Fsm!=null&&fsm.FsmName=="Health"){healthFsm=fsm;break;}
     if(healthFsm!=null){watch=new FlameDeathWatch(this,actor,healthFsm,health);flameDeathWatches.Add(id,watch);}
    }
    if(watch!=null)watch.BeforeDamage();
   }
   float before=health==null?0f:health.Value;
   try{damage.Value=-amount;receiver.SendEvent("Damage");}finally{if(health!=null)OptionalBloodEffects.NoteDamage(health,before-health.Value);if(watch!=null)watch.AfterDamage();}
  }
  internal void TickFlameBurnDeaths(){
   expiredFlameDeaths.Clear();
   foreach(var pair in flameDeathWatches)if(pair.Value.Expired(Time.time)){pair.Value.Forget();expiredFlameDeaths.Add(pair.Key);}
   foreach(int id in expiredFlameDeaths)flameDeathWatches.Remove(id);
  }
  void ClearFlameBurnDeaths(){foreach(var watch in flameDeathWatches.Values)watch.Forget();flameDeathWatches.Clear();expiredFlameDeaths.Clear();flameBurnTexture=null;burnSkinUnavailable=false;}
  internal void BurnCaptureUnavailable(string reason){if(warnedBurnCapture)return;warnedBurnCapture=true;Logger.LogWarning("Flame corpse appearance capture unavailable: "+reason+". Native damage is unchanged.");}
  internal void ScorchFlameCorpse(GameObject corpse){ApplyFlameCorpse(corpse,true);}
  internal void RestoreFlameCorpse(GameObject corpse){ApplyFlameCorpse(corpse,false);}
  void ApplyFlameCorpse(GameObject corpse,bool newDeath){
   if(corpse==null||corpse.GetComponent<FlameBurntCorpse>()!=null)return;
   try{
    var appearance=corpse.AddComponent<FlameBurntCorpse>();appearance.Warning=BurnSurfaceUnavailable;
    try{FlameCookedHarvest.Apply(corpse,CookedHarvestUnavailable);}catch(Exception error){CookedHarvestUnavailable(error.GetBaseException().Message);}
    if(!burnSkinUnavailable){
     string path=Path.Combine(Path.GetDirectoryName(Info.Location),"Flamethrower","BurntSkin.png");
     if(appearance.ApplySkinFile(path)){if(newDeath)StartCorpseSmoke(corpse);return;}
     burnSkinUnavailable=true;BurnSurfaceUnavailable("Burnt skin asset could not be loaded; using the native fallback.");
    }
    if(flameBurnTexture==null&&Time.unscaledTime>=nextBurnTexture){nextBurnTexture=Time.unscaledTime+2f;
     foreach(var texture in Resources.FindObjectsOfTypeAll<Texture2D>())if(texture!=null&&texture.name=="coal"){flameBurnTexture=texture;break;}
    }
    appearance.Apply(flameBurnTexture);
    if(newDeath)StartCorpseSmoke(corpse);
   }catch(Exception error){BurnCaptureUnavailable(error.Message);}
  }
  void CookedHarvestUnavailable(string reason){if(warnedCookedHarvest)return;warnedCookedHarvest=true;Logger.LogWarning("Flame corpse cooked harvest unavailable; native harvesting retained. "+reason);}
  void BurnSurfaceUnavailable(string reason){if(warnedBurnSurface)return;warnedBurnSurface=true;Logger.LogWarning("Flame corpse surface detail unavailable; retaining charred colour. "+reason);}
 }
 // Watch exact Health death actions, never the shared Item output (loot replaces
 // it), nearby corpses, a global death event or an enemy merely burning earlier.
 internal sealed class FlameDeathWatch {
  readonly Plugin owner;readonly Transform actor;readonly FsmFloat health;readonly PlayMakerFSM healthFsm;
  readonly int actorId;
  readonly List<CreateObject> actions=new List<CreateObject>();
  readonly List<FloatCompare> deathComparisons=new List<FloatCompare>();
  GameObject created;int depth;bool lethal,applied;float expires;
  internal FlameDeathWatch(Plugin pack,Transform target,PlayMakerFSM fsm,FsmFloat hp){
   owner=pack;actor=target;actorId=target.GetInstanceID();health=hp;healthFsm=fsm;
   foreach(var state in fsm.Fsm.States){if(state==null||state.Actions==null)continue;foreach(var action in state.Actions){
    var create=action as CreateObject;var prefab=create==null||create.gameObject==null?null:create.gameObject.Value;
    if(create==null||!create.Enabled||prefab==null||!CorpseName(prefab.name)||prefab.GetComponentInChildren<Rigidbody>(true)==null)continue;
    actions.Add(create);
   }}
   if(actions.Count>0&&FlameCorpseCapture.Ensure(owner))foreach(var action in actions)FlameCorpseCapture.Watch(action,this);
  }
  static bool CorpseName(string name){int clone=name.IndexOf("(Clone)",StringComparison.Ordinal);if(clone>=0)name=name.Substring(0,clone);return name.EndsWith("_Dead",StringComparison.OrdinalIgnoreCase);}
  internal void BeforeDamage(){
   if(depth++!=0)return;deathComparisons.Clear();
   // Read the native living state's actual death predicate. Several stock
   // NPCs die below 1 HP, rather than at zero. Only accept a local transition
   // that creates one of this actor's qualified physical corpses.
   var state=healthFsm==null?null:healthFsm.Fsm.ActiveState;
   if(state==null||state.Actions==null||state.Transitions==null)return;
   foreach(var action in state.Actions){
    var compare=action as FloatCompare;
    if(compare==null||!compare.Enabled||compare.float1==null||compare.float2==null||compare.tolerance==null||
     !compare.float1.UseVariable||!ReferenceEquals(healthFsm.FsmVariables.FindFsmFloat(compare.float1.Name),health)||
     compare.float2.UseVariable||compare.tolerance.UseVariable||compare.tolerance.Value!=0f||
     float.IsNaN(compare.float2.Value)||float.IsInfinity(compare.float2.Value))continue;
    if(DeathEvent(state,compare.lessThan)||DeathEvent(state,compare.equal))deathComparisons.Add(compare);
   }
  }
  bool DeathEvent(FsmState state,FsmEvent evt){
   if(evt==null)return false;
   foreach(var transition in state.Transitions){
    if(transition==null||transition.FsmEvent==null||transition.FsmEvent.Name!=evt.Name)continue;
    foreach(var target in healthFsm.Fsm.States)if(target!=null&&target.Name==transition.ToState&&target.Actions!=null)
     foreach(var action in target.Actions)if(actions.Contains(action as CreateObject))return true;
    return false;
   }return false;
  }
  bool NativeDeathReached(){
   if(health.Value<=0f)return true;
   foreach(var compare in deathComparisons){
    var state=compare.State;
    if(state!=null&&((health.Value<compare.float2.Value&&DeathEvent(state,compare.lessThan))||
     (health.Value==compare.float2.Value&&DeathEvent(state,compare.equal))))return true;
   }return false;
  }
  internal void AfterDamage(){
   depth=Math.Max(0,depth-1);
   if(NativeDeathReached()){lethal=true;expires=Time.time+2f;
    if(created!=null){owner.BindFlameEndingCorpse(actorId,created);owner.ScorchFlameCorpse(created);applied=true;}
    else if(actions.Count==0&&actor!=null){owner.ScorchFlameCorpse(actor.gameObject);applied=true;}
   }else if(depth==0)created=null;
  }
  internal void Created(GameObject corpse){
   if(corpse==null||applied||(!lethal&&depth==0))return;
   created=corpse;if(lethal||(depth>0&&NativeDeathReached())){owner.BindFlameEndingCorpse(actorId,corpse);owner.ScorchFlameCorpse(corpse);applied=true;}
  }
  internal bool Expired(float now){return applied||(lethal?now>expires:actor==null||health.Value<=0f);}
  internal void Forget(){foreach(var action in actions)FlameCorpseCapture.Forget(action,this);created=null;}
 }
 internal static class FlameCorpseCapture {
  static readonly Dictionary<CreateObject,FlameDeathWatch> watched=new Dictionary<CreateObject,FlameDeathWatch>();
  static bool patched,unavailable;
  internal static bool Ensure(Plugin owner){
   if(patched)return true;if(unavailable)return false;
   try{new Harmony("apocalypter.weaponspack.flamecorpse").Patch(AccessTools.Method(typeof(CreateObject),"OnEnter"),transpiler:new HarmonyMethod(typeof(FlameCorpseCapture),nameof(Transpile)));patched=true;}
   catch(Exception error){unavailable=true;owner.BurnCaptureUnavailable(error.Message);}return patched;
  }
  internal static void Watch(CreateObject action,FlameDeathWatch watch){watched[action]=watch;}
  internal static void Forget(CreateObject action,FlameDeathWatch watch){FlameDeathWatch old;if(watched.TryGetValue(action,out old)&&ReferenceEquals(old,watch))watched.Remove(action);}
  static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions){
   int count=0;foreach(var instruction in instructions){yield return instruction;var method=instruction.operand as MethodInfo;
    if(instruction.opcode==OpCodes.Call&&method!=null&&method.Name=="Instantiate"&&method.DeclaringType==typeof(UnityEngine.Object)){
     yield return new CodeInstruction(OpCodes.Dup);yield return new CodeInstruction(OpCodes.Ldarg_0);yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(FlameCorpseCapture),nameof(Created)));count++;
    }
   }if(count!=1)throw new InvalidOperationException("Expected one native CreateObject Instantiate call.");
  }
  static void Created(GameObject corpse,CreateObject action){FlameDeathWatch watch;if(corpse!=null&&watched.TryGetValue(action,out watch))watch.Created(corpse);}
 }
 [DefaultExecutionOrder(32000)]
 internal sealed class FlameBurntCorpse:MonoBehaviour {
  readonly List<Material> owned=new List<Material>();
  readonly Dictionary<Material,Material> replacements=new Dictionary<Material,Material>();
  readonly List<Material> materialBuffer=new List<Material>(8);
  internal Action<string> Warning;BurnSurfaceMaps maps;BurnSkinTexture skin;bool applied,attempted,authored,warnedRefresh;
  Renderer[] renderers;Texture charcoal;
  internal bool ApplySkinFile(string path){
   if(applied)return authored;
   skin=BurnSkinTexture.Acquire(path,Warning);if(skin==null)return false;
   authored=true;Apply(skin.Texture);return true;
  }
  internal void Apply(Texture texture){
   if(applied)return;applied=true;charcoal=texture;RefreshAppearance();
  }
  void OnTransformChildrenChanged(){renderers=null;}
  void LateUpdate(){if(!applied)return;try{RefreshAppearance();}catch(Exception error){if(!warnedRefresh&&Warning!=null){warnedRefresh=true;Warning("Custom corpse appearance refresh unavailable: "+error.GetBaseException().Message);}}}
  // Other mods may dress a corpse after CreateObject returns. Observe only
  // this corpse's cached renderers; preserve their current mesh and shader.
  internal void RefreshAppearance(){
   if(renderers==null)renderers=GetComponentsInChildren<Renderer>(true);
   foreach(var renderer in renderers){
    if(renderer==null)continue;
    if(!(renderer is MeshRenderer)&&!(renderer is SkinnedMeshRenderer))continue;
    // Reuse the buffer for steady-state observation. Only a genuine foreign
    // material swap allocates a new owned clone/assignment array.
    renderer.GetSharedMaterials(materialBuffer);var materials=materialBuffer;bool changed=false;
    for(int i=0;i<materials.Count;i++){
     var source=materials[i];if(source==null||owned.Contains(source))continue;
     Material existing;if(replacements.TryGetValue(source,out existing)&&existing!=null){materials[i]=existing;changed=true;continue;}
     // A foreign mod replacing materials continuously cannot grow our cache forever.
     if(owned.Count>=128){if(!warnedRefresh&&Warning!=null){warnedRefresh=true;Warning("Corpse material changes exceeded the owned burn-material budget; retaining additional custom materials.");}continue;}
     var material=new Material(source){name="Flamethrower.BurntCorpse",hideFlags=HideFlags.DontSave};owned.Add(material);replacements[source]=material;materials[i]=material;changed=true;
     if(charcoal!=null){
      // The native coal atlas's left half is cold charcoal; its right half
      // contains glowing coals and unrelated dirt. Do not wrap into that area.
      foreach(string property in new[]{"_MainTex","_BaseMap"})if(material.HasProperty(property)){material.SetTexture(property,charcoal);material.SetTextureScale(property,authored?Vector2.one:new Vector2(.48f,.98f));material.SetTextureOffset(property,authored?Vector2.zero:new Vector2(.01f,.01f));}
      // Multiply only our corpse material: pale atlas cracks become raw red
      // flesh while near-black charcoal stays black. The source stays intact.
      foreach(string property in new[]{"_Color","_BaseColor"})if(material.HasProperty(property))material.SetColor(property,new Color(authored?1f:.62f,authored?1f:.035f,authored?1f:.025f,source.HasProperty(property)?source.GetColor(property).a:1f));
     }else if(material.HasProperty("_Color")){var colour=material.color;material.color=new Color(.12f,.10f,.09f,colour.a);}
     if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",0f);
     if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0f);
     if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",Color.black);material.DisableKeyword("_EMISSION");
     if(charcoal is Texture2D texture&&(material.HasProperty("_BumpMap")||material.HasProperty("_MetallicGlossMap"))){if(!attempted){attempted=true;maps=authored?BurnSurfaceMaps.AcquireSkin(texture,Warning):BurnSurfaceMaps.Acquire(texture,Warning);}if(maps!=null)maps.Apply(material);}
    }
    if(changed)renderer.sharedMaterials=materials.ToArray();
   }
  }
  void OnDestroy(){foreach(var material in owned)if(material!=null)Destroy(material);owned.Clear();replacements.Clear();materialBuffer.Clear();renderers=null;charcoal=null;if(maps!=null)maps.Release();maps=null;if(skin!=null)skin.Release();skin=null;Warning=null;}
 }
}
