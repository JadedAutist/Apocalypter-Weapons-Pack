using System;
using ChainsawPrototype;
class MotionTests {
 static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
 static void Main(){
  foreach(int fps in new[]{30,60,144}){
   var walking=new WeaponHoldingMotion();var running=new WeaponHoldingMotion();
   int walkCrossings=0,runCrossings=0;float lastWalk=0,lastRun=0;
   for(int i=0;i<fps*12;i++){
    walking.Sample(0,0,4f/fps,1f/fps);running.Sample(0,0,9f/fps,1f/fps);
    Check(Math.Abs(walking.X-running.X)<.000001f&&Math.Abs(walking.Y-running.Y)<.000001f&&Math.Abs(walking.Roll-running.Roll)<.000001f,"running keeps walking cadence and bounded amplitude at "+fps+" fps");
    if(i>fps){if(lastWalk<=0&&walking.X>0)walkCrossings++;if(lastRun<=0&&running.X>0)runCrossings++;}
    lastWalk=walking.X;lastRun=running.X;
   }
   Check(walkCrossings==runCrossings&&walkCrossings>=12&&walkCrossings<=14,"smooth fixed cadence across twelve seconds at "+fps+" fps");
   for(int i=0;i<fps*3;i++)running.Sample(0,0,0,1f/fps);
   Check(Math.Abs(running.X)<.000001f&&Math.Abs(running.Y)<.000001f&&Math.Abs(running.Roll)<.000001f,"stopping returns to neutral at "+fps+" fps");
   running.Reset();Check(running.X==0&&running.Y==0&&running.Pitch==0&&running.Yaw==0&&running.Roll==0,"re-equip resets motion");
  }
  var rejected=new WeaponHoldingMotion();rejected.Sample(float.NaN,0,.1f,.016f);Check(rejected.X==0,"invalid look input rejected");
  rejected.Sample(0,0,50,.016f);Check(rejected.X==0&&rejected.Y==0,"teleport rejected");
  Console.WriteLine("PASS: "+checks+" motion checks; walking/running cadence at 30/60/144 fps, neutral recovery, resets and invalid input.");
 }
}
