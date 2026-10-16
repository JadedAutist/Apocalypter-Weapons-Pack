using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  GameObject weaponFeedbackRoot;
  // The pack's managed owner is called by PreviewRunner after its original
  // Unity host has gone away. Never use Plugin.transform for runtime objects.
  Transform WeaponFeedbackRoot(){
   if(weaponFeedbackRoot==null){weaponFeedbackRoot=new GameObject("WeaponsPack.FeedbackRuntime"){hideFlags=HideFlags.DontSave};DontDestroyOnLoad(weaponFeedbackRoot);}
   return weaponFeedbackRoot.transform;
  }
  void CleanupWeaponFeedbackRuntime(){if(weaponFeedbackRoot!=null)Destroy(weaponFeedbackRoot);weaponFeedbackRoot=null;}
 }
}
