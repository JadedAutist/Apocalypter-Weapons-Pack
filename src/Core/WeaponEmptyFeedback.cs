using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ChainsawPrototype {
 public partial class Plugin {
  readonly EmptyTankAttempt emptyLeft=new EmptyTankAttempt(),emptyRight=new EmptyTankAttempt();
  UnityEngine.Object emptyWeapon;AudioSource emptyTankAudio;AudioClip emptySawStarter,emptyFlameClick;float emptyNextCue,emptyNextLoad;
  UnityEngine.Object emptyStallWeapon;bool loggedSawEmpty,loggedFlameEmpty;
  void NoteEmptyFuelDebit(bool chainsaw,ChainsawFuel fuel,float cost){
   if(fuel!=null&&!FlamethrowerFuelPolicy.CanBurn(fuel.Litres))emptyStallWeapon=chainsaw?(UnityEngine.Object)lastHeldChain:activeFlame;
  }
  void TickEmptyTankFeedback(){
   UnityEngine.Object selected=enabledPreview?(UnityEngine.Object)lastHeldChain:activeFlame;
   if(selected==null){ResetEmptyTankFeedback();return;}
   bool controllable=Time.timeScale>0f&&!Cursor.visible&&Cursor.lockState==CursorLockMode.Locked&&!ActualThirdPersonActive();
   if(!controllable){ResetEmptyTankFeedback();return;}
   if(emptyWeapon!=selected){ResetEmptyTankFeedback(selected);emptyWeapon=selected;}
   bool leftHeld=Mouse.current!=null&&Mouse.current.leftButton.isPressed;
   bool rightHeld=Mouse.current!=null&&Mouse.current.rightButton.isPressed;
   bool cue=false;
   if(enabledPreview){
    bool left=emptyLeft.Tick(leftHeld&&attackEnabled!=null&&attackEnabled.Value,heldFuel==null||!FlamethrowerFuelPolicy.CanBurn(heldFuel.Litres),gameAttacking||swinging||cuttingHeld||cuttingBlend>.0001f);
    bool right=emptyRight.Tick(rightHeld&&cuttingEnabled!=null&&cuttingEnabled.Value,heldFuel==null||!FlamethrowerFuelPolicy.CanBurn(heldFuel.Litres),gameAttacking||cuttingHeld);
    cue=left||right;
   }else{
    var fuel=activeFlame.GetComponentInChildren<ChainsawFuel>(true);
    cue=emptyLeft.Tick(leftHeld,fuel==null||!FlamethrowerFuelPolicy.CanBurn(fuel.Litres),flameFiring);
    emptyRight.Tick(false,false,false);
   }
   bool paidActive=enabledPreview?(gameAttacking||swinging||cuttingHeld):flameFiring;
   if(emptyStallWeapon==selected&&!paidActive){
    cue=true;emptyStallWeapon=null;if(leftHeld)emptyLeft.Latch();if(rightHeld)emptyRight.Latch();
   }
   if(cue&&Time.unscaledTime>=emptyNextCue){emptyNextCue=Time.unscaledTime+.4f;PlayEmptyTankCue(enabledPreview);}
  }
  void ResetEmptyTankFeedback(UnityEngine.Object keepStallFor=null){emptyWeapon=null;if(emptyStallWeapon!=keepStallFor)emptyStallWeapon=null;emptyLeft.Tick(false,false,false);emptyRight.Tick(false,false,false);if(emptyTankAudio!=null)emptyTankAudio.Stop();}
  void PlayEmptyTankCue(bool chainsaw){
   float volume=chainsaw?(sawVolume==null?.3f:Mathf.Clamp01(sawVolume.Value)):FlamethrowerAttackVolume();
   if(volume<=0f)return;
   if((emptySawStarter==null||emptyFlameClick==null)&&Time.unscaledTime>=emptyNextLoad){
    emptyNextLoad=Time.unscaledTime+3f;
    if(emptySawStarter==null)emptySawStarter=LoadWeaponFeedbackPcm("EmptySawStarter");
    if(emptyFlameClick==null)emptyFlameClick=LoadWeaponFeedbackPcm("EmptyFlameSputter");
   }
   var clip=chainsaw?emptySawStarter:emptyFlameClick;if(clip==null)return;
   if(emptyTankAudio==null){var go=new GameObject("WeaponsPack.EmptyTankAudio"){hideFlags=HideFlags.DontSave};go.transform.SetParent(WeaponFeedbackRoot(),false);emptyTankAudio=go.AddComponent<AudioSource>();ConfigureFlameAudioSource(emptyTankAudio,false);}
   emptyTankAudio.priority=0;emptyTankAudio.pitch=chainsaw?.72f:1f;emptyTankAudio.volume=Mathf.Clamp01(volume*(chainsaw?1.5f:1f));emptyTankAudio.clip=clip;emptyTankAudio.Play();
   if(chainsaw?!loggedSawEmpty:!loggedFlameEmpty){if(chainsaw)loggedSawEmpty=true;else loggedFlameEmpty=true;Logger.LogInfo((chainsaw?"Chainsaw":"Flamethrower")+" empty-tank cue: "+clip.name+", playing="+emptyTankAudio.isPlaying+", volume="+emptyTankAudio.volume+", virtual="+emptyTankAudio.isVirtual);}
  }
  AudioClip LoadWeaponFeedbackPcm(string name){
   string path=Path.Combine(Path.GetDirectoryName(Info.Location),"WeaponFeedback",name+".wav");if(!File.Exists(path))return null;
   try{int channels,frequency;var samples=ReadNativeFirePcm(path,out channels,out frequency);return MakeFlameAttackClip(samples,channels,frequency,"WeaponsPack.Feedback."+name,false);}
   catch(Exception error){Logger.LogWarning("Weapon feedback sound unavailable: "+name+"; "+error.Message);return null;}
  }
  void CleanupEmptyTankFeedback(){ResetEmptyTankFeedback();if(emptyTankAudio!=null)Destroy(emptyTankAudio.gameObject);if(emptySawStarter!=null)Destroy(emptySawStarter);if(emptyFlameClick!=null)Destroy(emptyFlameClick);emptyTankAudio=null;emptySawStarter=emptyFlameClick=null;}
 }
}
