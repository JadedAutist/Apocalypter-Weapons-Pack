using UnityEngine;
using UnityEngine.Rendering;

namespace ChainsawPrototype {
 public partial class Plugin {
  bool fuelSpraying;float fuelSprayLitres;int fuelSprayFrame=-1,fuelSprayAppliedFrame=-1;
  ParticleSystem fuelSprayParticles;Material fuelSprayMaterial;Texture2D fuelDropTexture;
  float FundFuelSpray(ChainsawFuel fuel,float seconds,int frame){
   if(frame==fuelSprayFrame||fuel==null||!Finite(seconds)||seconds<=0f)return 0f;
   fuelSprayFrame=frame;
   var balance=activeFlame==null?flameFuelBalances[0]:FuelBalance(activeFlame.gameObject);
   float rate=FlameTuningValue(balance==null?null:balance.Rate,5f,1f,20f);
   return fuel.ConsumeContinuous(CurrentFlameCost*rate*seconds);
  }
  void TickFuelSpray(bool requested){
   if(!requested){fuelSpraying=false;fuelSprayLitres=0f;return;}
   if(fuelSprayFrame==Time.frameCount)return;
   var fuel=activeFlame.GetComponentInChildren<ChainsawFuel>(true);
   fuelSprayLitres=FundFuelSpray(fuel,Time.deltaTime,Time.frameCount);fuelSpraying=fuelSprayLitres>0f;
   if(fuel!=null)NoteEmptyFuelDebit(false,fuel,CurrentFlameCost);
  }
  void StopFuelSpray(){fuelSpraying=false;fuelSprayLitres=0f;fuelSprayFrame=fuelSprayAppliedFrame=-1;if(fuelSprayParticles!=null)fuelSprayParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
  void LateFuelSpray(Vector3 origin,Vector3 direction,float distance,RaycastHit? contact,bool blocked){
   bool spraying=fuelSpraying&&Time.timeScale>0f&&!Cursor.visible&&!ActualThirdPersonActive();
   if(spraying&&fuelSprayParticles==null)CreateFuelSpray();
   if(fuelSprayParticles!=null){
    fuelSprayParticles.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(direction));
    var main=fuelSprayParticles.main;main.startLifetime=Mathf.Max(.001f,distance/FlameSpeed);
    if(spraying&&!blocked){if(!fuelSprayParticles.isEmitting)fuelSprayParticles.Play(true);}
    else if(fuelSprayParticles.isEmitting)fuelSprayParticles.Stop(true,blocked?ParticleSystemStopBehavior.StopEmittingAndClear:ParticleSystemStopBehavior.StopEmitting);
   }
   if(!spraying||blocked||contact==null||fuelSprayFrame!=Time.frameCount||fuelSprayAppliedFrame==Time.frameCount)return;
   fuelSprayAppliedFrame=Time.frameCount;
   DepositFuelPatch(contact.Value,fuelSprayLitres,FuelKind(activeFlame.gameObject),Time.time);
  }
  void CreateFuelSpray(){
   const int size=16;fuelDropTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Flamethrower.FuelDrop",hideFlags=HideFlags.DontSave,wrapMode=TextureWrapMode.Clamp};var pixels=new Color[size*size];
   for(int y=0;y<size;y++)for(int x=0;x<size;x++){float r=(new Vector2(x-7.5f,y-7.5f)).magnitude/7.5f;pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01((1f-r)*3f));}fuelDropTexture.SetPixels(pixels);fuelDropTexture.Apply(false,true);
   fuelSprayMaterial=FlameAlphaMaterial(fuelDropTexture,"Flamethrower.ColdFuelSpray");
   var go=new GameObject("Flamethrower.ColdFuelSpray"){hideFlags=HideFlags.DontSave};fuelSprayParticles=go.AddComponent<ParticleSystem>();fuelSprayParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=fuelSprayParticles.main;main.playOnAwake=false;main.loop=true;main.startSpeed=FlameSpeed;main.startSize=new ParticleSystem.MinMaxCurve(.018f,.035f);main.startColor=new Color(.55f,.42f,.2f,.6f);main.maxParticles=256;main.simulationSpace=ParticleSystemSimulationSpace.World;
   var emission=fuelSprayParticles.emission;emission.rateOverTime=180f;var shape=fuelSprayParticles.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=2f;shape.radius=.009f;
   var colours=fuelSprayParticles.colorOverLifetime;colours.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(.5f,.8f),new GradientAlphaKey(0,1)});colours.color=gradient;
   ConfigureFlameTrajectory(fuelSprayParticles);FlameSurfaceCollision(fuelSprayParticles);
   var renderer=fuelSprayParticles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=fuelSprayMaterial;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=.6f;renderer.velocityScale=.015f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
  }
  void CleanupFuelSpray(){StopFuelSpray();if(fuelSprayParticles!=null)Destroy(fuelSprayParticles.gameObject);fuelSprayParticles=null;if(fuelSprayMaterial!=null)Destroy(fuelSprayMaterial);fuelSprayMaterial=null;if(fuelDropTexture!=null)Destroy(fuelDropTexture);fuelDropTexture=null;}
 }
}
