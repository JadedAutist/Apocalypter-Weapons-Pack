using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  [DataContract] public class Vec { [DataMember] public float x;[DataMember] public float y;[DataMember] public float z;public Vec(){}public Vec(Vector3 v){x=v.x;y=v.y;z=v.z;}public Vector3 Value(){return new Vector3(x,y,z);} }
  [DataContract] public class KeyData {
   [DataMember] public string name;[DataMember] public string easing;[DataMember] public float seconds;
   [DataMember] public Vec move,turn,weaponTurn,leftMove,leftTurn,rightMove,rightTurn;
   public KeyData(){}public KeyData(PoseKey k){name=k.name;easing=k.easing;seconds=k.seconds;move=new Vec(k.move);turn=new Vec(k.turn);weaponTurn=new Vec(k.weaponTurn);leftMove=new Vec(k.leftMove);leftTurn=new Vec(k.leftTurn);rightMove=new Vec(k.rightMove);rightTurn=new Vec(k.rightTurn);}
   static Vector3 V(Vec v){return v==null?Vector3.zero:v.Value();}public PoseKey Value(){return new PoseKey{name=name,easing=easing??"Smooth",seconds=seconds,move=V(move),turn=V(turn),weaponTurn=V(weaponTurn),leftMove=V(leftMove),leftTurn=V(leftTurn),rightMove=V(rightMove),rightTurn=V(rightTurn)};}
  }
  [DataContract] public class PresetData {
   [DataMember] public int version=7;[DataMember] public string weapon,preset,sourceClip,sourceWeapon,sourcePath;[DataMember] public bool lockHands=true;[DataMember] public float returnSeconds;[DataMember] public Vec swingTurn,weaponAlignment,pathStart,pathMiddle,pathEnd,pathStartTurn,pathMiddleTurn,pathEndTurn;[DataMember] public KeyData[] keys;
   public PresetData(){}public PresetData(Preset p){weapon=p.weapon;preset=p.preset;sourceClip=p.sourceClip;sourceWeapon=p.sourceWeapon;sourcePath=p.sourcePath;returnSeconds=p.returnSeconds;swingTurn=new Vec(p.swingTurn);weaponAlignment=new Vec(p.weaponAlignment);pathStart=new Vec(p.pathStart);pathMiddle=new Vec(p.pathMiddle);pathEnd=new Vec(p.pathEnd);pathStartTurn=new Vec(p.pathStartTurn);pathMiddleTurn=new Vec(p.pathMiddleTurn);pathEndTurn=new Vec(p.pathEndTurn);keys=p.keys.Select(k=>new KeyData(k)).ToArray();}
   public Preset Value(){if(keys==null||keys.Length!=3||keys.Any(k=>k==null))throw new InvalidDataException("Preset has no saved pose keys. Recreate or restore the motion in the editor and Save again.");return new Preset{weapon=weapon,preset=preset,sourceClip=sourceClip??"",sourceWeapon=sourceWeapon??"",sourcePath=sourcePath??"",returnSeconds=returnSeconds,swingTurn=swingTurn==null?Vector3.zero:swingTurn.Value(),weaponAlignment=weaponAlignment==null?Vector3.zero:weaponAlignment.Value(),pathStart=pathStart==null?Vector3.zero:pathStart.Value(),pathMiddle=pathMiddle==null?Vector3.zero:pathMiddle.Value(),pathEnd=pathEnd==null?Vector3.zero:pathEnd.Value(),pathStartTurn=pathStartTurn==null?Vector3.zero:pathStartTurn.Value(),pathMiddleTurn=pathMiddleTurn==null?Vector3.zero:pathMiddleTurn.Value(),pathEndTurn=pathEndTurn==null?Vector3.zero:pathEndTurn.Value(),keys=keys.Select(k=>k.Value()).ToArray(),lockHands=true};}
  }
  static Preset ReadPreset(string path){using(var stream=File.OpenRead(path)){var data=(PresetData)new DataContractJsonSerializer(typeof(PresetData)).ReadObject(stream);return data.Value();}}
  static void WritePreset(string path,Preset value){using(var stream=File.Create(path))new DataContractJsonSerializer(typeof(PresetData)).WriteObject(stream,new PresetData(value));}
 }
}
