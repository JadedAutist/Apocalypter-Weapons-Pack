using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  Texture2D chainsawIcon;Sprite chainsawSprite;bool bodyHook;GameObject thirdModel;object thirdOwner;GameObject suppressedProp;
  ConfigEntry<float>[] thirdMove,thirdTurn;
  void LoadIcon(){var path=Path.Combine(Path.GetDirectoryName(Info.Location),"Chainsaw-icon.png");chainsawIcon=new Texture2D(2,2);ImageConversion.LoadImage(chainsawIcon,File.ReadAllBytes(path));chainsawIcon.name="Chainsaw.InventoryIcon";chainsawIcon.hideFlags=HideFlags.DontUnloadUnusedAsset;chainsawSprite=Sprite.Create(chainsawIcon,new Rect(0,0,chainsawIcon.width,chainsawIcon.height),new Vector2(.5f,.5f));chainsawSprite.hideFlags=HideFlags.DontUnloadUnusedAsset;
   thirdMove=BindAxes("Third person - position","Adjust the third person prop relative to the right hand, in metres.",.4f);thirdTurn=BindAxes("Third person - rotation","Adjust the third person prop rotation in degrees.",180f);
  }
  void SetItemIcon(GameObject item){
   foreach(var fsm in item.GetComponents<PlayMakerFSM>()){
    foreach(var v in fsm.FsmVariables.TextureVariables)if(v.Value!=null || v.Name.ToLowerInvariant().Contains("icon"))v.Value=chainsawIcon;
    foreach(var v in fsm.FsmVariables.ObjectVariables)if(v.Value is Sprite)v.Value=chainsawSprite;else if(v.Value is Texture)v.Value=chainsawIcon;
    foreach(var state in fsm.Fsm.States)foreach(var action in state.Actions)foreach(var field in action.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance)){
     var value=field.GetValue(action);var texture=value as FsmTexture;if(texture!=null && texture.Value!=null)texture.Value=chainsawIcon;
     var obj=value as FsmObject;if(obj!=null && obj.Value is Sprite)obj.Value=chainsawSprite;else if(obj!=null && obj.Value is Texture)obj.Value=chainsawIcon;
    }
   }
  }
 }
}
