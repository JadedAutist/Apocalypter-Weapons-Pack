using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ChainsawPrototype {
 internal struct FlameBodyPoint {
  internal Transform Anchor;internal Vector3 LocalPoint;internal float Width,Height;
  internal Vector3 World {get{return Anchor==null?Vector3.zero:Anchor.TransformPoint(LocalPoint);}}
 }
 // Capture geometry once at ignition, then keep only bounded bone-local points.
 // Never read an unreadable shared mesh or bake a mesh during visual updates.
 internal static class FlameBodyCoverage {
  const int MaxCandidates=2048,MaxVertices=100000,MaxRenderers=32,SamplesPerRenderer=192;
  internal static FlameBodyPoint[] Build(Transform actor,Action<string> warning){
   if(actor==null)return new FlameBodyPoint[0];var candidates=new List<FlameBodyPoint>();var vertices=new List<Vector3>();Mesh snapshot=null;bool warned=false,hasSkinSurface=false;int processed=0,totalVertices=0;
   var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);var allBones=new List<Transform>();var renderers=new List<Renderer>();
   // Prioritize animated body geometry before static weapons and accessories.
   foreach(var skin in skins)if(skin!=null&&skin.enabled&&skin.gameObject.activeInHierarchy)renderers.Add(skin);
   foreach(var visual in actor.GetComponentsInChildren<MeshRenderer>(true))if(visual!=null&&visual.enabled&&visual.gameObject.activeInHierarchy)renderers.Add(visual);
   try{
    foreach(var renderer in renderers){
     if(processed>=MaxRenderers||candidates.Count>=MaxCandidates)break;
     if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
     var skin=renderer as SkinnedMeshRenderer;var visual=renderer as MeshRenderer;if(skin==null&&visual==null)continue;
     // A valid animated body already supplies the burning surface. Rigid
     // trophies, helmets and held gear are not extra body coverage; unreadable
     // attachment bounds in particular contain empty corners around the mesh.
     if(skin==null&&hasSkinSurface)continue;
     var bones=skin==null?null:skin.bones;
     if(bones!=null)foreach(var bone in bones)if(allBones.Count<MaxCandidates&&bone!=null&&bone.IsChildOf(actor)&&!allBones.Contains(bone))allBones.Add(bone);
     Mesh mesh=skin==null?null:skin.sharedMesh;if(visual!=null){var filter=visual.GetComponent<MeshFilter>();mesh=filter==null?null:filter.sharedMesh;}
     if(mesh==null||mesh.vertexCount==0||mesh.vertexCount>MaxVertices||totalVertices+mesh.vertexCount>MaxVertices)continue;processed++;totalVertices+=mesh.vertexCount;
     try{
      vertices.Clear();
      if(skin!=null){
       if(bones==null||bones.Length==0)continue;
       if(snapshot==null)snapshot=new Mesh{name="Flamethrower.BodySnapshot",hideFlags=HideFlags.DontSave};else snapshot.Clear();
       // On this Unity target true compensates renderer scale; TransformPoint
       // below applies it once. The native scale regression covers this contract.
       skin.BakeMesh(snapshot,true);snapshot.GetVertices(vertices);
      }else if(mesh.isReadable)mesh.GetVertices(vertices);
      else{AddBox(candidates,renderer.transform,mesh.bounds.center,mesh.bounds.size);continue;}
      if(vertices.Count==0)continue;
      // Preserve the complete posed silhouette even when vertex order hides
      // a hand, wing or tail between the bounded uniform sample indices.
      var extremes=new int[6];var scores=new float[6];for(int i=0;i<6;i++){extremes[i]=-1;scores[i]=float.NegativeInfinity;}
      for(int i=0;i<vertices.Count;i++){
       var world=renderer.transform.TransformPoint(vertices[i]);vertices[i]=world;if(!Finite(world))continue;
       for(int axis=0;axis<3;axis++)for(int side=0;side<2;side++){int slot=axis*2+side;float score=world[axis]*(side==0?-1f:1f);if(score>scores[slot]){scores[slot]=score;extremes[slot]=i;}}
      }
      int before=candidates.Count,count=Mathf.Min(SamplesPerRenderer,vertices.Count);
      for(int i=0;i<count+6;i++){
       int index=i<6?extremes[i]:(count==1?0:(i-6)*(vertices.Count-1)/(count-1));if(index<0)continue;var world=vertices[index];if(!Finite(world))continue;
       var anchor=renderer.transform;
       if(bones!=null){float nearest=float.PositiveInfinity;foreach(var bone in bones){if(bone==null||!bone.IsChildOf(actor))continue;float distance=(bone.position-world).sqrMagnitude;if(distance<nearest){nearest=distance;anchor=bone;}}}
       Add(candidates,anchor,world);
      }
      if(skin!=null&&candidates.Count>before)hasSkinSurface=true;
     }catch(Exception error){if(!warned&&warning!=null){warned=true;warning("Body fire snapshot unavailable; using bounded body fallback. "+error.GetBaseException().Message);}}
    }
   }finally{if(snapshot!=null)UnityEngine.Object.Destroy(snapshot);}
   if(candidates.Count==0){
    foreach(var collider in actor.GetComponentsInChildren<Collider>(true)){
     if(collider==null||!collider.enabled||!collider.gameObject.activeInHierarchy||(collider.transform!=actor&&!DamagePart(collider.transform)))continue;
     AddCollider(candidates,collider);
    }
    foreach(var bone in allBones)Add(candidates,bone,bone.position);
    // Sparse or unreadable non-skinned fallback points still follow the closest
    // animated bone instead of remaining on a rigid actor root.
    if(allBones.Count>0)for(int i=0;i<candidates.Count;i++)if(candidates[i].Anchor==actor){
     var point=candidates[i];var world=point.World;Transform anchor=null;float nearest=float.PositiveInfinity;
     foreach(var bone in allBones){float distance=(bone.position-world).sqrMagnitude;if(distance<nearest){nearest=distance;anchor=bone;}}
     if(anchor!=null){point.Anchor=anchor;point.LocalPoint=anchor.InverseTransformPoint(world);candidates[i]=point;}
    }
   }
   if(candidates.Count==0)AddBox(candidates,actor,Vector3.up*.5f,new Vector3(.4f,1f,.4f));
   var worldPoints=new Vector3[candidates.Count];for(int i=0;i<candidates.Count;i++)worldPoints[i]=candidates[i].World;
   var bounds=new Bounds(worldPoints[0],Vector3.zero);foreach(var point in worldPoints)bounds.Encapsulate(point);
   float area=2f*(bounds.size.x*bounds.size.y+bounds.size.y*bounds.size.z+bounds.size.z*bounds.size.x);
   int target=Mathf.Clamp(Mathf.CeilToInt(12f+Mathf.Max(0f,Mathf.Sqrt(area)-2.2f)*2f),12,20);
   // Degenerate foreign geometry still has a finite, bounded fallback.
   var selected=new List<int>(target);var used=new bool[candidates.Count];
   for(int axis=0;axis<3;axis++)for(int side=-1;side<=1;side+=2){int best=-1;float score=float.NegativeInfinity;for(int i=0;i<candidates.Count;i++){float value=worldPoints[i][axis]*side;if(!used[i]&&value>score){score=value;best=i;}}if(best>=0){used[best]=true;selected.Add(best);}}
   while(selected.Count<target){
    int best=-1;float score=float.NegativeInfinity;
    for(int i=0;i<candidates.Count;i++)if(!used[i]){float nearest=float.PositiveInfinity;foreach(int chosen in selected)nearest=Mathf.Min(nearest,(worldPoints[i]-worldPoints[chosen]).sqrMagnitude);if(nearest>score){score=nearest;best=i;}}
    if(best<0){selected.Add(selected.Count%candidates.Count);continue;}
    used[best]=true;selected.Add(best);
   }
   float height=Mathf.Clamp(.85f*Mathf.Sqrt(Mathf.Max(.01f,area)/target),Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z))>=1.1f?.5f:.22f,1.6f);
   var result=new FlameBodyPoint[selected.Count];for(int i=0;i<result.Length;i++)result[i]=candidates[selected[i]];for(int i=0;i<result.Length;i++){float variation=.9f+(i%3)*.1f;result[i].Height=height*variation;result[i].Width=height*.72f*variation;}return result;
  }
  static bool Finite(Vector3 value){return !float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&!float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);}
  static void Add(List<FlameBodyPoint> points,Transform anchor,Vector3 world){if(points.Count>=MaxCandidates||anchor==null||!Finite(world))return;points.Add(new FlameBodyPoint{Anchor=anchor,LocalPoint=anchor.InverseTransformPoint(world)});}
  static bool DamagePart(Transform node){foreach(var fsm in node.GetComponents<PlayMakerFSM>())if(fsm.Fsm!=null&&fsm.FsmName=="Bodypart"&&fsm.FsmVariables.FindFsmFloat("Damage")!=null)return true;return false;}
  static void AddBox(List<FlameBodyPoint> points,Transform anchor,Vector3 centre,Vector3 size){
   for(int axis=0;axis<3;axis++)for(int side=-1;side<=1;side+=2)for(int a=-1;a<=1;a++)for(int b=-1;b<=1;b++){
    var p=centre;p[axis]+=side*size[axis]*.5f;p[(axis+1)%3]+=a*size[(axis+1)%3]*.5f;p[(axis+2)%3]+=b*size[(axis+2)%3]*.5f;Add(points,anchor,anchor.TransformPoint(p));
   }
  }
  static void AddCollider(List<FlameBodyPoint> points,Collider collider){
   var box=collider as BoxCollider;if(box!=null){AddBox(points,box.transform,box.center,box.size);return;}
   var capsule=collider as CapsuleCollider;var sphere=collider as SphereCollider;
   if(capsule==null&&sphere==null){var bounds=collider.bounds;for(int axis=0;axis<3;axis++)for(int side=-1;side<=1;side+=2){var p=bounds.center;p[axis]+=bounds.extents[axis]*side;Add(points,collider.transform,p);}return;}
   int direction=capsule==null?1:capsule.direction;float radius=capsule==null?sphere.radius:capsule.radius;float stem=capsule==null?0f:Mathf.Max(0f,capsule.height*.5f-radius);Vector3 centre=capsule==null?sphere.center:capsule.center;
   for(int level=-1;level<=1;level++)for(int i=0;i<8;i++){
    float angle=i*Mathf.PI*.25f;var p=centre;p[direction]+=level*stem;p[(direction+1)%3]+=Mathf.Cos(angle)*radius;p[(direction+2)%3]+=Mathf.Sin(angle)*radius;Add(points,collider.transform,collider.transform.TransformPoint(p));
   }
   for(int side=-1;side<=1;side+=2){var p=centre;p[direction]+=side*(stem+radius);Add(points,collider.transform,collider.transform.TransformPoint(p));}
  }
 }
}
