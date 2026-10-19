using System;
namespace ChainsawPrototype {
 internal enum ChainsawSurface { Other, Metal, Wood, Ground, Flesh }
 internal static class WeaponFeedbackPolicy {
  internal static ChainsawSurface Surface(bool flesh,string physics,string material,string objectName){
   if(flesh)return ChainsawSurface.Flesh;
   string visual=(material??"").ToLowerInvariant();
   // Wheels, windows and upholstery can share a vehicle physics material.
   if(NonMetalMaterial(visual))return ChainsawSurface.Other;
   if(Has(visual,"wood","timber","plank"))return ChainsawSurface.Wood;
   if(Has(visual,"metal","rusted","rusty","rustang","rustchief","rustliner","rustcargo"))return ChainsawSurface.Metal;
   if(Has(visual,"sand","soil","ground","stone","concrete","rock","asphalt"))return ChainsawSurface.Ground;
   if(Has((physics??"").ToLowerInvariant(),"vehiclematerial","metal"))return ChainsawSurface.Metal;
   // Only use names when there is no better material identity.
   if(visual.Length==0){
    string name=(objectName??"").ToLowerInvariant();
    if(Has(name,"metal","steel","barrel","oiltank"))return ChainsawSurface.Metal;
    if(Has(name,"wood","plank","timber"))return ChainsawSurface.Wood;
    if(Has(name,"terrain","ground","sand","stone","concrete"))return ChainsawSurface.Ground;
   }
   return ChainsawSurface.Other;
  }
  internal static bool NonMetalMaterial(string material){return Has((material??"").ToLowerInvariant(),"glass","rubber","tire","tyre","leather","plastic");}
  static bool Has(string text,params string[] words){foreach(string word in words)if(text.IndexOf(word,StringComparison.Ordinal)>=0)return true;return false;}
 }
 // An unsuccessful hold produces one cue, even if the weapon runs dry after
 // several successful attacks. A paid swing/interval is never an empty attempt.
 internal sealed class EmptyTankAttempt {
  bool cued;
  internal void Latch(){cued=true;}
  internal bool Tick(bool requested,bool insufficient,bool paidAttackActive){
   if(!requested){cued=false;return false;}
   if(cued||!insufficient||paidAttackActive)return false;
   cued=true;return true;
  }
 }
}
