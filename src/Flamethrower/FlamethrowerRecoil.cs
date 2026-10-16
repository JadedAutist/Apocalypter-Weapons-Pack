using UnityEngine;

namespace ChainsawPrototype {
 public partial class Plugin {
  const float FlameRecoilRise=.035f,FlameRecoilDuration=.27f;
  const float FlameRecoilDistance=.028f,FlameRecoilPitch=1.6f;
  bool flameRecoilWasFiring,flameRecoilActive;
  float flameRecoilStart,flameRecoilAmount,flameRecoilFrom;
  int flameRecoilFrame=-1;

  internal void ResetFlamethrowerRecoil(){
   flameRecoilWasFiring=flameRecoilActive=false;
   flameRecoilStart=flameRecoilAmount=flameRecoilFrom=0f;flameRecoilFrame=-1;
  }
  void SampleFlamethrowerRecoil(){
   // LateUpdate and camera pre-cull rebuild the same saved pose. Observe the
   // firing edge and advance the recoil envelope only once in a rendered frame.
   if(flameRecoilFrame==Time.frameCount)return;flameRecoilFrame=Time.frameCount;
   bool usable=activeFlame!=null&&activeFlame.Equipped&&activeFlame.gameObject.activeInHierarchy&&Time.timeScale>0f&&!Cursor.visible&&!ActualThirdPersonActive();
   if(!usable){flameRecoilWasFiring=flameRecoilActive=false;flameRecoilAmount=0f;return;}
   if(flameFiring&&!flameRecoilWasFiring){flameRecoilFrom=flameRecoilAmount;flameRecoilStart=Time.time;flameRecoilActive=true;}
   flameRecoilWasFiring=flameFiring;
   if(!flameRecoilActive){flameRecoilAmount=0f;return;}
   float age=Mathf.Max(0f,Time.time-flameRecoilStart);
   if(age>=FlameRecoilDuration){flameRecoilActive=false;flameRecoilAmount=0f;return;}
   float progress=age<FlameRecoilRise?age/FlameRecoilRise:(age-FlameRecoilRise)/(FlameRecoilDuration-FlameRecoilRise);
   float ease=progress*progress*(3f-2f*progress);
   flameRecoilAmount=age<FlameRecoilRise?Mathf.Lerp(flameRecoilFrom,1f,ease):1f-ease;
  }
  void ApplyFlamethrowerRecoil(FlamethrowerItem marker,Transform view){
   SampleFlamethrowerRecoil();if(flameRecoilAmount<=0f)return;
   if(marker.HoldingRig==null||marker.HoldingModel==null||!marker.HoldingModel.IsChildOf(marker.HoldingRig))return;
   // A single rigid move keeps both arms, grips and model together in hip and
   // aimed poses. Baseline restoration removes this offset on the next pass;
   // saved placement, native camera and stock weapon transforms are untouched.
   var turn=view.rotation*Quaternion.Euler(-FlameRecoilPitch*flameRecoilAmount,0f,0f)*Quaternion.Inverse(view.rotation);
   var pivot=view.TransformPoint(new Vector3(0f,-.08f,.24f));
   var shift=-view.forward*(FlameRecoilDistance*flameRecoilAmount);
   var rig=marker.HoldingRig;
   rig.SetPositionAndRotation(pivot+turn*(rig.position-pivot)+shift,turn*rig.rotation);
  }
 }
}
