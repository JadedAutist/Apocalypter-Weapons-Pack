using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChainsawPrototype {
 public partial class Plugin {
  ConfigEntry<bool> cuttingEnabled;
  ConfigEntry<float> cuttingLean,cuttingBlendTime,cuttingDamagePerHit,cuttingAttackRate,cuttingFuelPerAttack,cuttingTilt;
  bool cuttingHeld;
  float cuttingBlend,cuttingContactTime,cuttingFuelCredit;

  void SetupCuttingAttack(){
   cuttingEnabled=FixedSetting("Chainsaw cutting","Right click cutting",true);
   cuttingLean=FixedSetting("Chainsaw cutting","Forward lean",.10f);
   cuttingBlendTime=FixedSetting("Chainsaw cutting","Lean transition",.18f);
   cuttingDamagePerHit=Config.Bind("Chainsaw cutting","Damage per hit",20f,new ConfigDescription("Damage for each right-click sawing contact. Independent of left-click swing damage.",new AcceptableValueRange<float>(.1f,100f)));
   cuttingAttackRate=Config.Bind("Chainsaw cutting","Attack rate",6f,new ConfigDescription("Right-click sawing attacks per second. Each attack pays Fuel per attack and deals Damage per hit on contact.",new AcceptableValueRange<float>(.5f,20f)));
   cuttingFuelPerAttack=Config.Bind("Chainsaw cutting","Fuel per attack",.10f,new ConfigDescription("Litres used for each right-click sawing attack, including misses. Fuel use per second is this value multiplied by Attack rate.",new AcceptableValueRange<float>(.1f,.5f)));
   cuttingTilt=FixedSetting("Chainsaw cutting","Blade forward tilt",8f);
  }

  // Return true while this mode owns the attack input, including its short recovery.
  bool UpdateCuttingAttack(){
   bool canControl=enabledPreview&&camera!=null&&preview!=null&&Time.timeScale>0f&&Cursor.lockState==CursorLockMode.Locked&&Mouse.current!=null;
   if(!canControl){ResetCuttingAttack();return false;}
   float fuelCost=cuttingFuelPerAttack.Value;
   bool requested=cuttingEnabled.Value&&!gameAttacking&&Mouse.current.rightButton.isPressed&&heldFuel!=null&&FlamethrowerFuelPolicy.CanBurn(heldFuel.Litres);
   float elapsed=Finite(Time.deltaTime)?Mathf.Max(0f,Time.deltaTime):0f;
   cuttingHeld=requested;
   cuttingBlend=Mathf.MoveTowards(cuttingBlend,requested?1f:0f,elapsed/Mathf.Max(.05f,cuttingBlendTime.Value));
   if(requested){
    float rate=Mathf.Max(.5f,cuttingAttackRate.Value),spent=heldFuel.ConsumeContinuous(fuelCost*rate*elapsed);
    cuttingFuelCredit+=spent/fuelCost;cuttingContactTime+=spent/(fuelCost*rate);
    if(spent>0f)NoteEmptyFuelDebit(true,heldFuel,fuelCost);
    if((cuttingBlend>=.5f&&cuttingContactTime+.000001f>=1f/rate)||!FlamethrowerFuelPolicy.CanBurn(heldFuel.Litres)){
     FlushCuttingFuel();
    }
   }else FlushCuttingFuel();
   return requested||cuttingBlend>.0001f;
  }

  void FlushCuttingFuel(){
   if(cuttingFuelCredit>0f){PlayChainsawHitShake(true);CuttingContact(cuttingFuelCredit/Mathf.Max(.5f,cuttingAttackRate.Value),cuttingFuelCredit);}
   cuttingFuelCredit=0f;cuttingContactTime=0f;
  }
  void CuttingContact(float duration,float attacks=1f){
   float radius,reach;
   bool native=MeleeHitArea.TryGetWrenchShape(out radius,out reach);
   var contact=Nearest(camera.position,camera.forward,sawReach.Value,native?radius:.025f);
   if(contact==null)return;
   ProcessHit(contact.Value,cuttingDamagePerHit.Value*attacks,true,.6f*duration,false);
  }

  void ApplyCuttingPose(){
   if(cuttingBlend<=.0001f||camera==null||preview==null||left==null||right==null)return;
   float weight=cuttingBlend*cuttingBlend*(3f-2f*cuttingBlend);
   var turn=camera.rotation*Quaternion.Euler(cuttingTilt.Value*weight,0,0)*Quaternion.Inverse(camera.rotation);
   var shift=camera.TransformVector(new Vector3(0,.012f*weight,cuttingLean.Value*weight));
   var pivot=right.position;var model=preview.transform;
   model.SetPositionAndRotation(pivot+turn*(model.position-pivot)+shift,turn*model.rotation);
   left.SetPositionAndRotation(pivot+turn*(left.position-pivot)+shift,turn*left.rotation);
   right.SetPositionAndRotation(pivot+turn*(right.position-pivot)+shift,turn*right.rotation);
  }

  void ResetCuttingAttack(){cuttingHeld=false;cuttingBlend=0f;cuttingContactTime=cuttingFuelCredit=0f;}
 }
}
