using System;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using Chain=ChainsawPrototype.Plugin;
namespace ChainsawPrototype {
 public partial class Plugin {
  readonly System.Collections.Generic.Dictionary<string,System.Tuple<DateTime,Preset>> installedCache=new System.Collections.Generic.Dictionary<string,System.Tuple<DateTime,Preset>>();
  Preset LoadInstalled(string path){DateTime stamp=File.GetLastWriteTimeUtc(path);System.Tuple<DateTime,Preset> entry;if(!installedCache.TryGetValue(path,out entry)||entry.Item1!=stamp){entry=System.Tuple.Create(stamp,ReadPreset(path));installedCache[path]=entry;}return entry.Item2;}
  bool gameAttacking,applyingAttack;float attackTime;int nextAttack;Preset attackPreset;PoseKey forcedPose;ConfigEntry<bool> attackEnabled;string directory;Transform attackModel,attackRig,attackView;
  void SetupOwnedMovesets(){
   directory=Path.Combine(BepInEx.Paths.ConfigPath,"WeaponMovesets");Directory.CreateDirectory(directory);
  }
  void CaptureAttackIdle(){RefreshAnimationRig();}
  static float AttackLength(Preset p){float total=Mathf.Clamp(p.returnSeconds,.02f,5f);foreach(var k in p.keys)total+=Mathf.Clamp(k.seconds,.02f,5f);return total;}
  static PoseKey AttackSample(Preset p,float time){var previous=new PoseKey();foreach(var key in p.keys){float seconds=Mathf.Clamp(key.seconds,.02f,5f);if(time<=seconds)return Mix(previous,key,Ease(time/seconds,key.easing));time-=seconds;previous=key;}return Mix(previous,new PoseKey(),Ease(time/Mathf.Clamp(p.returnSeconds,.02f,5f),"Smooth"));}
  internal void UpdateOwnedAttack(){
   if(attackEnabled==null)attackEnabled=FixedSetting("Chainsaw attacks","Left click attacks",true);
   Transform m,g,l,r,c;bool held=Chain.TryGetAnimationRig(out m,out g,out l,out r,out c);
   if(UpdateCuttingAttack())return;
   if(gameAttacking){if(!held||Time.timeScale<=0f||Cursor.lockState!=CursorLockMode.Locked){gameAttacking=false;Chain.EndMeleeSwing();forcedPose=null;return;}attackTime+=Time.deltaTime*(sawAttackRate.Value/(1f/1.2f));Chain.UpdateMeleeSwing(attackTime);if(attackTime>=AttackLength(attackPreset)){gameAttacking=false;Chain.EndMeleeSwing();forcedPose=null;Chain.RefreshAnimationRig();}else return;}
   if(!attackEnabled.Value||!held||!Chain.HasFuelForAttack||Time.timeScale<=0f||Cursor.lockState!=CursorLockMode.Locked||Mouse.current==null||!Mouse.current.leftButton.isPressed)return;
   attackModel=m;attackRig=g;left=l;right=r;attackView=c;Chain.RefreshAnimationRig();CaptureAttackIdle();CaptureWristAnchors();bool diagonal=(nextAttack++%2)==1;attackPreset=null;
   string path=Path.Combine(directory,"chainsaw--"+(diagonal?"upperleftdiagonalchop":"downwardswing")+".json");
   if(File.Exists(path)){try{var loaded=LoadInstalled(path);if(loaded.weapon=="chainsaw")attackPreset=loaded;}catch(Exception e){Logger.LogWarning("Active attack could not load: "+e.Message);}}
   if(attackPreset==null)return;
   Chain.BeginMeleeSwing(attackPreset.keys[0].seconds,attackPreset.keys[1].seconds);gameAttacking=true;attackTime=0;Chain.UpdateMeleeSwing(0f);Logger.LogInfo("Chainsaw left-click animation: "+attackPreset.preset);
  }
  internal void LateOwnedAttack(){
   if(!gameAttacking)return;try{
    applyingAttack=false;forcedPose=null;Chain.RefreshAnimationRig();RestoreAttackGrip();attackPivotCamera=attackView.InverseTransformPoint(right.position);
    forcedPose=AttackSample(attackPreset,attackTime);applyingAttack=true;Chain.ApplyAnimationBase();EnforceWristAnchors();
   }catch(Exception e){gameAttacking=false;ReportError(e);}finally{applyingAttack=false;forcedPose=null;}
  }
 }
}
