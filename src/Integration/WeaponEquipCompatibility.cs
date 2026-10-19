using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  bool packEquipInstalled,packEquipUnavailable;float nextPackEquipCheck;
  FieldInfo equipPosed,equipPosition,equipRotation,equipEnabled;
  void InstallWeaponEquipCompatibility(){
   if(packEquipInstalled||packEquipUnavailable||harmony==null||Time.unscaledTime<nextPackEquipCheck)return;nextPackEquipCheck=Time.unscaledTime+3f;
   var type=OptionalWeaponTypes.Find("WildWasteland.Modules.Guns.WeaponTransition");var guns=OptionalWeaponTypes.Find("WildWasteland.Modules.Guns.GunsModule");if(type==null||guns==null)return;
   const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
   var pose=type.GetMethod("Pose",flags,null,Type.EmptyTypes,null);
   equipPosed=type.GetField("_posed",flags);equipPosition=type.GetField("_position",flags);equipRotation=type.GetField("_rotation",flags);equipEnabled=guns.GetField("SmoothWeaponSwitch",flags);
   if(pose==null||pose.ReturnType!=typeof(void)||equipPosed==null||equipPosed.FieldType!=typeof(Transform)||equipPosition==null||equipPosition.FieldType!=typeof(Vector3)||equipRotation==null||equipRotation.FieldType!=typeof(Quaternion)||equipEnabled==null||equipEnabled.FieldType!=typeof(ConfigEntry<bool>)){packEquipUnavailable=true;Logger.LogWarning("Optional WildWasteland equip animation layout is unsupported; pack holding remains standalone.");return;}
   try{harmony.Patch(pose,postfix:new HarmonyMethod(typeof(Plugin),nameof(AfterOptionalEquipPose)));packEquipInstalled=true;Logger.LogInfo("WildWasteland equip animation: independent pack holding poses registered.");}
   catch(Exception error){harmony.Unpatch(pose,HarmonyPatchType.Postfix,harmony.Id);packEquipUnavailable=true;Logger.LogWarning("Optional WildWasteland equip animation unavailable; pack holding remains standalone. "+error.GetBaseException().Message);}
  }
  bool TryPackEquipMotion(Transform view,out Quaternion turn,out Vector3 shift){
   turn=Quaternion.identity;shift=Vector3.zero;if(!packEquipInstalled||packEquipUnavailable||view==null)return false;
   try{
    var enabled=equipEnabled.GetValue(null) as ConfigEntry<bool>;if(enabled==null||!enabled.Value)return false;
    var posed=equipPosed.GetValue(null) as Transform;if(posed==null||!posed.gameObject.activeInHierarchy||!posed.IsChildOf(view)||posed.parent==null)return false;
    var position=(Vector3)equipPosition.GetValue(null);var rotation=(Quaternion)equipRotation.GetValue(null);if(!Finite(position)||!Finite(rotation))return false;
    var origin=posed.parent.TransformPoint(position);var basis=posed.parent.rotation*rotation;
    if(!Finite(posed.position)||!Finite(posed.rotation)||Vector3.Distance(origin,posed.position)>2f||Quaternion.Angle(basis,posed.rotation)>90f)return false;
    turn=posed.rotation*Quaternion.Inverse(basis);shift=posed.position-turn*origin;return Finite(turn)&&Finite(shift);
   }catch(Exception error){packEquipUnavailable=true;Logger.LogWarning("Optional WildWasteland equip pose could not be read; pack holding remains standalone. "+error.GetBaseException().Message);turn=Quaternion.identity;shift=Vector3.zero;return false;}
  }
  static void AfterOptionalEquipPose(){
   var owner=instance;if(ReferenceEquals(owner,null)||owner.packEquipUnavailable)return;
   // The postfix runs after WW's final pose regardless of plugin load order.
   // Rebuild from saved poses, never add an offset to a previously moved pose.
   try{
    Quaternion turn;Vector3 shift;
    if(owner.enabledPreview&&owner.camera!=null&&owner.CanPresentHeldWeapon(owner.camera)&&!owner.ActualThirdPersonActive()&&owner.TryPackEquipMotion(owner.camera,out turn,out shift))owner.ApplyStockPlacement();
    var item=owner.activeFlame;if(item!=null&&item.HoldingRig!=null&&owner.flamePlayer!=null&&owner.CanPresentHeldWeapon(owner.flamePlayer.transform)&&!owner.ActualThirdPersonActive()&&owner.TryPackEquipMotion(owner.flamePlayer.transform,out turn,out shift)){
     var controller=item.HoldingRig.GetComponent<FlamethrowerHoldingRunner>();if(controller!=null)owner.CompleteFlamethrowerPose(item,controller,false);
    }
   }catch(Exception error){if(owner.packEquipUnavailable)return;owner.packEquipUnavailable=true;owner.Logger.LogWarning("Optional equip presentation refresh unavailable; native switching remains unchanged. "+error.GetBaseException().Message);}
  }
 }
}
