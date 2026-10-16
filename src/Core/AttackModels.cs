using System;using UnityEngine;namespace ChainsawPrototype {
 [Serializable] public class PoseKey {public string name;public float seconds=.25f;public string easing="Smooth";public Vector3 move,turn,weaponTurn,leftMove,leftTurn,rightMove,rightTurn;}
 [Serializable] public class Preset {public int version=4;public bool lockHands=true;public string sourceClip="",sourceWeapon="",sourcePath="";public Vector3 swingTurn,weaponAlignment,pathStart,pathMiddle,pathEnd,pathStartTurn,pathMiddleTurn,pathEndTurn;public string weapon,preset="Default";public PoseKey[] keys={new PoseKey{name="Wind-up"},new PoseKey{name="Contact",seconds=.2f},new PoseKey{name="Recovery",seconds=.35f}};public float returnSeconds=.25f;}

}
