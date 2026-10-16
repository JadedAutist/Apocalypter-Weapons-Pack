using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  Transform presentationView,nativeWeaponContainer;PlayMakerFSM playerHealthFsm;FsmFloat playerHealth;
  bool CanPresentHeldWeapon(Transform view){
   if(view==null||!view.gameObject.activeInHierarchy)return false;
   if(presentationView!=view){
    presentationView=view;nativeWeaponContainer=view.Find("WeaponsArm");playerHealthFsm=null;playerHealth=null;
    var player=GameObject.Find("Player");
    if(player!=null)playerHealthFsm=player.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.Fsm!=null&&f.FsmName=="Health");
    if(playerHealthFsm!=null)playerHealth=playerHealthFsm.FsmVariables.FindFsmFloat("Health");
   }
   // These private rigs are parented to the camera, outside the native root
   // which the game's Health.playerDeath state deactivates.
   if(nativeWeaponContainer==null||!nativeWeaponContainer.gameObject.activeInHierarchy)return false;
   if(playerHealthFsm!=null&&(playerHealthFsm.ActiveStateName=="playerDeath"||playerHealthFsm.ActiveStateName=="backToMenu"))return false;
   return playerHealth==null||playerHealth.Value>0f;
  }
  bool SelectedPlayerCanPresent(){
   var view=heldFsm==null?null:heldFsm.GetComponentInParent<Camera>();return view==null||CanPresentHeldWeapon(view.transform);
  }
 }
}
