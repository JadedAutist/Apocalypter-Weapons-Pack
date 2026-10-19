using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using UnityEngine;

// Generic public contracts plus an optional, private numeric model fixture.
// No extracted meshes, model names, prefab IDs or native point data belong here.
public sealed partial class IgniteChecks {
 sealed class CoverageObservation {
  internal Vector3[] World;internal Transform[] Anchors;internal object[] Raw;internal float MeanWidth,MeanHeight,MinAxisCoverage,MaxSurfaceError;
 }
 #pragma warning disable 0649 // Optional fixture fields are populated by DataContractJsonSerializer.
 [DataContract] sealed class CoverageFixture { [DataMember] public CoverageActor[] actors; }
 [DataContract] sealed class CoverageActor {
  [DataMember] public CoverageRenderer[] renderers;[DataMember] public CoverageBone[] bones;[DataMember] public CoverageCollider[] colliders;
 }
 [DataContract] sealed class CoverageRenderer {
  [DataMember] public float[][] points;[DataMember] public bool active;[DataMember] public bool enabled;[DataMember] public int[] boneIds;[DataMember] public string type;[DataMember] public bool meshReadable;
 }
 [DataContract] sealed class CoverageBone { [DataMember] public int id;[DataMember] public float[] position; }
 [DataContract] sealed class CoverageCollider {
  [DataMember] public string type;[DataMember] public float[] center;[DataMember] public float worldRadius;[DataMember] public float[][] segmentEndpoints;
  [DataMember] public bool bodypart;[DataMember] public bool active;[DataMember] public bool enabled;
 }
 #pragma warning restore 0649
 [DataContract] sealed class CoverageMetric {
  [DataMember] public string fixtureCase;[DataMember] public int count;[DataMember] public float minAxisCoverage,maxSurfaceError,meanWidth,meanHeight;
 }
 [DataContract] sealed class CoverageMetricReport {
  [DataMember] public string scope="Synthetic reconstruction of private native sparse posed geometry; no rendered or gameplay verification.";
  [DataMember] public CoverageMetric[] cases;
 }
 static Vector3 CoverageVector(float[] values){return values!=null&&values.Length>=3?new Vector3(values[0],values[1],values[2]):Vector3.zero;}
 static bool CoverageFinite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
 static bool CoverageFinite(Vector3 value){return CoverageFinite(value.x)&&CoverageFinite(value.y)&&CoverageFinite(value.z);}
 static Bounds CoverageBounds(IList<Vector3> points){var value=new Bounds(points[0],Vector3.zero);for(int i=1;i<points.Count;i++)value.Encapsulate(points[i]);return value;}
 static Vector3[] CoverageBoxPoints(Vector3 size){
  var points=new List<Vector3>();
  // Several surface points per face make the bounded selection meaningful.
  for(int axis=0;axis<3;axis++)for(int side=-1;side<=1;side+=2)for(int a=0;a<5;a++)for(int b=0;b<5;b++){
   var p=Vector3.zero;p[axis]=side*size[axis]*.5f;p[(axis+1)%3]=(a/4f-.5f)*size[(axis+1)%3];p[(axis+2)%3]=(b/4f-.5f)*size[(axis+2)%3];points.Add(p);
  }return points.ToArray();
 }
 GameObject CoverageActorRoot(string label){var actor=Make(label);Metadata(actor,"Health","Health");Receiver(actor,actor);return actor;}
 SkinnedMeshRenderer CoverageSkin(GameObject actor,string label,Vector3[] points,Transform[] bones,List<Mesh> meshes,bool unreadable){
  var child=Make(label);child.transform.SetParent(actor.transform,false);var skin=child.AddComponent<SkinnedMeshRenderer>();var mesh=new Mesh();meshes.Add(mesh);mesh.vertices=points;
  var indices=new int[(points.Length/3)*3];for(int i=0;i<indices.Length;i++)indices[i]=i;mesh.triangles=indices;
  var weights=new BoneWeight[points.Length];var bindposes=new Matrix4x4[bones.Length];
  for(int i=0;i<bones.Length;i++)bindposes[i]=bones[i].worldToLocalMatrix*child.transform.localToWorldMatrix;
  for(int i=0;i<points.Length;i++){
   int nearest=0;float best=float.PositiveInfinity;Vector3 world=child.transform.TransformPoint(points[i]);
   for(int j=0;j<bones.Length;j++){float d=(bones[j].position-world).sqrMagnitude;if(d<best){best=d;nearest=j;}}
   weights[i]=new BoneWeight{boneIndex0=nearest,weight0=1f};
  }
  mesh.boneWeights=weights;mesh.bindposes=bindposes;mesh.RecalculateBounds();skin.sharedMesh=mesh;skin.bones=bones;skin.rootBone=bones[0];
  // Loose import/culling bounds must not become emitter origins.
  skin.localBounds=new Bounds(Vector3.zero,Vector3.one*30f);if(unreadable)mesh.UploadMeshData(true);return skin;
 }
 Transform[] CoverageBones(GameObject actor,Vector3 size){
  var bones=new Transform[7];for(int i=0;i<bones.Length;i++){var child=Make("SyntheticBone"+i);child.transform.SetParent(actor.transform,false);bones[i]=child.transform;}
  for(int axis=0;axis<3;axis++){var a=Vector3.zero;a[axis]=-size[axis]*.35f;bones[1+axis*2].localPosition=a;a[axis]*=-1;bones[2+axis*2].localPosition=a;}return bones;
 }
 CoverageObservation ObserveCoverage(MethodInfo build,GameObject actor,IList<Vector3> source,string label,bool requireSurface){
  var warnings=new List<string>();Array points=null;try{points=build.Invoke(null,new object[]{actor.transform,new Action<string>(warnings.Add)}) as Array;}catch(Exception e){Check(false,label+": helper builds without exception "+e.GetType().Name);return null;}
  Check(points!=null&&points.Length>=12&&points.Length<=20,label+": bounded 12–20 emitter budget");if(points==null||points.Length==0)return null;
  var result=new CoverageObservation{World=new Vector3[points.Length],Anchors=new Transform[points.Length],Raw=new object[points.Length],MinAxisCoverage=1f};bool finite=true,sized=true;float widths=0,heights=0;
  for(int i=0;i<points.Length;i++){
   object point=points.GetValue(i);var anchor=Field(point,"Anchor") as Transform;var local=(Vector3)Field(point,"LocalPoint");var world=(Vector3)Prop(point,"World");float width=(float)Field(point,"Width"),height=(float)Field(point,"Height");
   result.World[i]=world;result.Anchors[i]=anchor;result.Raw[i]=point;finite&=anchor!=null&&CoverageFinite(local)&&CoverageFinite(world);sized&=CoverageFinite(width)&&CoverageFinite(height)&&width>0&&height>0&&width<=2f&&height<=3f;widths+=width;heights+=height;
   if(requireSurface&&source.Count>0){float best=float.PositiveInfinity;foreach(var candidate in source)best=Mathf.Min(best,Vector3.Distance(candidate,world));result.MaxSurfaceError=Mathf.Max(result.MaxSurfaceError,best);}
  }
  result.MeanWidth=widths/points.Length;result.MeanHeight=heights/points.Length;Check(finite,label+": finite bone-local anchors and current world positions");Check(sized,label+": positive bounded flame dimensions");
  if(source.Count>0){var actual=CoverageBounds(result.World);var expected=CoverageBounds(source);float largest=Mathf.Max(expected.size.x,Mathf.Max(expected.size.y,expected.size.z));bool spans=true;
   for(int axis=0;axis<3;axis++)if(expected.size[axis]>=Mathf.Max(.1f,largest*.06f)){float ratio=actual.size[axis]/expected.size[axis];result.MinAxisCoverage=Mathf.Min(result.MinAxisCoverage,ratio);spans&=ratio>=.6f;}
   Check(spans,label+": practical coverage across each significant model axis");
  }
  if(requireSurface)Check(result.MaxSurfaceError<=.025f,label+": emitter origins lie on sampled model surfaces");return result;
 }
 void CoverageGenericCase(MethodInfo build,string label,Vector3 size,Vector3 translation,Vector3 scale,Quaternion rotation){
  var actor=CoverageActorRoot(label);var meshes=new List<Mesh>();var bones=CoverageBones(actor,size);var local=CoverageBoxPoints(size);CoverageSkin(actor,"SyntheticSurface",local,bones,meshes,true);
  actor.transform.position=translation;actor.transform.localScale=scale;actor.transform.rotation=rotation;var expected=new List<Vector3>();foreach(var v in local)expected.Add(actor.transform.TransformPoint(v));
  var result=ObserveCoverage(build,actor,expected,label,true);
  if(result!=null){int moving=-1;for(int i=0;i<result.Anchors.Length;i++)if(result.Anchors[i]!=null&&result.Anchors[i]!=actor.transform){moving=i;break;}Check(moving>=0,label+": sampled skin points retain a bone attachment");
   if(moving>=0){var anchor=result.Anchors[moving];Vector3 before=(Vector3)Prop(result.Raw[moving],"World");Vector3 delta=new Vector3(.17f,.09f,-.12f);anchor.position+=delta;
    Check(Vector3.Distance((Vector3)Prop(result.Raw[moving],"World"),before+delta)<.002f,label+": cached surface point follows its moving bone without a new snapshot");}
  }
  DestroyImmediate(actor);foreach(var mesh in meshes)DestroyImmediate(mesh);
 }
 void ModelCoverageChecks(){
  var type=assembly.GetType("ChainsawPrototype.FlameBodyCoverage");var build=type==null?null:type.GetMethod("Build",All);Check(build!=null,"model coverage owns adaptive body-surface selection");if(build==null)return;
  CoverageGenericCase(build,"TallBody",new Vector3(1.3f,5f,1.1f),Vector3.zero,Vector3.one,Quaternion.identity);
  CoverageGenericCase(build,"WideBody",new Vector3(5f,1.5f,1.2f),Vector3.zero,Vector3.one,Quaternion.identity);
  CoverageGenericCase(build,"LongBody",new Vector3(.6f,.9f,4.5f),Vector3.zero,Vector3.one,Quaternion.identity);
  CoverageGenericCase(build,"TranslatedRotatedScaledBody",new Vector3(1.2f,2.5f,.8f),new Vector3(120,-17,33),new Vector3(1.4f,.7f,1.8f),Quaternion.Euler(21,57,-13));
  CoverageFallbackChecks(build);CoverageSamplingOrder(build);CoverageEquipmentChecks(build);CoverageStaticAndCost(build);CoverageScaleChecks(build);CoverageBoneScaleCheck(build);CoverageHiddenGeometryCheck(build);CoveragePrivateModels(build);
 }
 void CoverageEquipmentChecks(MethodInfo build){
  var actor=CoverageActorRoot("BodyWithDetachedEquipment");var meshes=new List<Mesh>();var size=new Vector3(1f,3f,.8f);var surface=CoverageBoxPoints(size);var bones=CoverageBones(actor,size);CoverageSkin(actor,"BodySurface",surface,bones,meshes,true);
  foreach(var position in new[]{new Vector3(0,3f,0),new Vector3(4f,1f,0)}){
   var gear=Make("RigidAttachment");gear.transform.SetParent(actor.transform,false);gear.transform.localPosition=position;var mesh=new Mesh();meshes.Add(mesh);mesh.vertices=CoverageBoxPoints(new Vector3(2f,.5f,1f));mesh.triangles=new[]{0,1,2};mesh.RecalculateBounds();gear.AddComponent<MeshFilter>().sharedMesh=mesh;gear.AddComponent<MeshRenderer>();mesh.UploadMeshData(true);
  }
  ObserveCoverage(build,actor,surface,"BodyWithDetachedEquipment",true);DestroyImmediate(actor);foreach(var mesh in meshes)DestroyImmediate(mesh);
 }
 void CoverageFallbackChecks(MethodInfo build){
  var actor=CoverageActorRoot("ColliderOnlyFallback");var box=actor.AddComponent<BoxCollider>();box.size=new Vector3(.8f,2.8f,.7f);Physics.SyncTransforms();var source=CoverageBoxPoints(box.size);ObserveCoverage(build,actor,source,"ColliderOnlyFallback",false);DestroyImmediate(actor);
  actor=CoverageActorRoot("PartialSkeletonFallback");box=actor.AddComponent<BoxCollider>();box.size=new Vector3(.8f,.6f,.7f);box.center=Vector3.up*.9f;var foot=Make("SyntheticLowBone");foot.transform.SetParent(actor.transform,false);var shoulder=Make("SyntheticHighBone");shoulder.transform.SetParent(actor.transform,false);shoulder.transform.localPosition=Vector3.up*1.8f;var skin=actor.AddComponent<SkinnedMeshRenderer>();skin.bones=new[]{foot.transform,shoulder.transform};
  Physics.SyncTransforms();ObserveCoverage(build,actor,new[]{foot.transform.position,shoulder.transform.position},"PartialSkeletonFallback",false);DestroyImmediate(actor);
 }
 void CoverageSamplingOrder(MethodInfo build){
  var actor=CoverageActorRoot("AdversarialVertexOrder");var meshes=new List<Mesh>();var bones=CoverageBones(actor,Vector3.one*5f);var vertices=new Vector3[1000];int index=1;for(int axis=0;axis<3;axis++)for(int side=-1;side<=1;side+=2){if(index==5)index++;vertices[index][axis]=2.5f*side;index++;}CoverageSkin(actor,"OrderedSurface",vertices,bones,meshes,true);ObserveCoverage(build,actor,vertices,"AdversarialVertexOrder",true);DestroyImmediate(actor);foreach(var mesh in meshes)DestroyImmediate(mesh);
  actor=CoverageActorRoot("MixedRendererPriority");meshes.Clear();var expected=new List<Vector3>();for(int i=0;i<24;i++){var child=Make("SmallStaticPart"+i);child.transform.SetParent(actor.transform,false);var mesh=new Mesh();meshes.Add(mesh);var small=CoverageBoxPoints(Vector3.one*.2f);mesh.vertices=small;mesh.triangles=new[]{0,1,2};mesh.RecalculateBounds();child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>();foreach(var v in small)expected.Add(v);}var body=CoverageBoxPoints(new Vector3(1f,4f,1f));bones=CoverageBones(actor,new Vector3(1f,4f,1f));CoverageSkin(actor,"LateMainSkin",body,bones,meshes,true);expected.AddRange(body);ObserveCoverage(build,actor,expected,"MixedRendererPriority",true);DestroyImmediate(actor);foreach(var mesh in meshes)DestroyImmediate(mesh);
 }
 void CoverageStaticAndCost(MethodInfo build){
  var actor=CoverageActorRoot("StaticUnreadableBody");var meshes=new List<Mesh>();var surface=CoverageBoxPoints(new Vector3(1.1f,2f,.8f));var child=Make("StaticSurface");child.transform.SetParent(actor.transform,false);var mesh=new Mesh();meshes.Add(mesh);mesh.vertices=surface;mesh.triangles=new[]{0,1,2};mesh.RecalculateBounds();child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>();mesh.UploadMeshData(true);ObserveCoverage(build,actor,surface,"StaticUnreadableBody",true);DestroyImmediate(actor);DestroyImmediate(mesh);
  actor=CoverageActorRoot("DenseUnreadableSkin");meshes.Clear();var bones=CoverageBones(actor,new Vector3(1,2,.8f));var seed=CoverageBoxPoints(new Vector3(1,2,.8f));var dense=new Vector3[13230];for(int i=0;i<dense.Length;i++)dense[i]=seed[i%seed.Length];CoverageSkin(actor,"DenseSurface",dense,bones,meshes,true);var watch=System.Diagnostics.Stopwatch.StartNew();var points=build.Invoke(null,new object[]{actor.transform,null}) as Array;watch.Stop();Check(points!=null&&points.Length<=20,"dense unreadable skin still retains only bounded fire points");Timing("TIMING: one production coverage build for 13230-vertex unreadable skin="+watch.Elapsed.TotalMilliseconds.ToString("F3")+" ms; synthetic input, not whole-game FPS");DestroyImmediate(actor);foreach(var ownedMesh in meshes)DestroyImmediate(ownedMesh);
 }
 void CoverageScaleChecks(MethodInfo build){
  var actor=CoverageActorRoot("ScaledBody");var meshes=new List<Mesh>();var local=CoverageBoxPoints(new Vector3(1,2,.8f));var bones=CoverageBones(actor,new Vector3(1,2,.8f));CoverageSkin(actor,"ScaledSurface",local,bones,meshes,true);
  var expected=new List<Vector3>();foreach(var point in local)expected.Add(point);var small=ObserveCoverage(build,actor,expected,"BodyScaleOne",true);actor.transform.localScale=Vector3.one*2;expected.Clear();foreach(var point in local)expected.Add(actor.transform.TransformPoint(point));var large=ObserveCoverage(build,actor,expected,"BodyScaleTwo",true);
  Check(small!=null&&large!=null&&large.MeanWidth>=small.MeanWidth*1.12f&&large.MeanHeight>=small.MeanHeight*1.12f,"larger bodies grow flame width and height within the same emitter budget");DestroyImmediate(actor);foreach(var mesh in meshes)DestroyImmediate(mesh);
 }
 void CoverageBoneScaleCheck(MethodInfo build){
  var actor=CoverageActorRoot("BoneScaledUnreadableBody");var child=Make("SingleScaleBone");child.transform.SetParent(actor.transform,false);var meshes=new List<Mesh>();var local=CoverageBoxPoints(new Vector3(2,2.5f,1.6f));CoverageSkin(actor,"UnreadableScaledSurface",local,new[]{child.transform},meshes,true);child.transform.localScale=Vector3.one*1.4f;actor.transform.localScale=Vector3.one*2;actor.transform.position=new Vector3(-30,7,21);var expected=new List<Vector3>();foreach(var point in local)expected.Add(actor.transform.TransformPoint(point*1.4f));ObserveCoverage(build,actor,expected,"BoneScaledUnreadableBody",true);DestroyImmediate(actor);foreach(var mesh in meshes)DestroyImmediate(mesh);
 }
 void CoverageHiddenGeometryCheck(MethodInfo build){
  var actor=CoverageActorRoot("InactiveGeometryFilter");var meshes=new List<Mesh>();var size=new Vector3(1,2,.8f);var local=CoverageBoxPoints(size);var bones=CoverageBones(actor,size);CoverageSkin(actor,"VisibleSurface",local,bones,meshes,true);var oversized=CoverageBoxPoints(size*20);var disabled=CoverageSkin(actor,"DisabledSurface",oversized,bones,meshes,true);disabled.enabled=false;var inactive=CoverageSkin(actor,"InactiveSurface",oversized,bones,meshes,true);inactive.gameObject.SetActive(false);ObserveCoverage(build,actor,local,"InactiveGeometryFilter",true);DestroyImmediate(actor);foreach(var mesh in meshes)DestroyImmediate(mesh);
 }
 void CoveragePrivateModels(MethodInfo build){
  string path=Path.Combine(host,"lib/model-coverage.json");if(!File.Exists(path)){Timing("MODEL COVERAGE: optional private numeric fixture not supplied; native-model sampling not performed.");return;}
  CoverageFixture fixture;using(var stream=File.OpenRead(path))fixture=(CoverageFixture)new DataContractJsonSerializer(typeof(CoverageFixture)).ReadObject(stream);
  Check(fixture!=null&&fixture.actors!=null&&fixture.actors.Length==52,"private inventory includes all 52 native NPC Health actors");if(fixture==null||fixture.actors==null)return;var metrics=new List<CoverageMetric>();
  for(int index=0;index<fixture.actors.Length;index++){
   string label="ModelCase"+index.ToString("D3");var item=fixture.actors[index];var actor=CoverageActorRoot(label);var meshes=new List<Mesh>();var expected=new List<Vector3>();var boneMap=new Dictionary<int,Transform>();
   if(item.bones!=null)foreach(var b in item.bones)if(!boneMap.ContainsKey(b.id)){var child=Make("NumericBone"+boneMap.Count);child.transform.SetParent(actor.transform,false);child.transform.localPosition=CoverageVector(b.position);boneMap.Add(b.id,child.transform);}
   bool hasSkin=false;if(item.renderers!=null)foreach(var r in item.renderers)if(r.type!="MeshRenderer"&&r.active&&r.enabled&&r.points!=null&&r.points.Length>=3)hasSkin=true;
   int rendererIndex=0;if(item.renderers!=null)foreach(var r in item.renderers){if(!r.active||!r.enabled||r.points==null||r.points.Length<3)continue;var points=new Vector3[r.points.Length];for(int i=0;i<points.Length;i++){points[i]=CoverageVector(r.points[i]);if(!hasSkin||r.type!="MeshRenderer")expected.Add(points[i]);}
    if(r.type=="MeshRenderer"){var child=Make("NumericRigidSurface"+rendererIndex++);child.transform.SetParent(actor.transform,false);var mesh=new Mesh();meshes.Add(mesh);mesh.vertices=points;mesh.triangles=new[]{0,1,2};mesh.RecalculateBounds();child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>();if(!r.meshReadable)mesh.UploadMeshData(true);continue;}
    var selected=new List<Transform>();if(r.boneIds!=null)foreach(int id in r.boneIds){Transform bone;if(boneMap.TryGetValue(id,out bone))selected.Add(bone);}if(selected.Count==0){var child=Make("NumericRigidBone");child.transform.SetParent(actor.transform,false);selected.Add(child.transform);}CoverageSkin(actor,"NumericSurface"+rendererIndex++,points,selected.ToArray(),meshes,true);
   }
   if(item.colliders!=null)foreach(var c in item.colliders){if(!c.active||!c.enabled||!c.bodypart)continue;var child=Make("NumericBodypart");child.transform.SetParent(actor.transform,false);Receiver(child,actor);child.transform.localPosition=CoverageVector(c.center);
    if(c.type=="SphereCollider"){var sphere=child.AddComponent<SphereCollider>();sphere.radius=c.worldRadius;}
    else if(c.type=="CapsuleCollider"&&c.segmentEndpoints!=null&&c.segmentEndpoints.Length==2){var a=CoverageVector(c.segmentEndpoints[0]);var b=CoverageVector(c.segmentEndpoints[1]);child.transform.localPosition=(a+b)*.5f;child.transform.localRotation=(b-a).sqrMagnitude>.000001f?Quaternion.FromToRotation(Vector3.up,(b-a).normalized):Quaternion.identity;var capsule=child.AddComponent<CapsuleCollider>();capsule.radius=c.worldRadius;capsule.height=(b-a).magnitude+c.worldRadius*2;capsule.direction=1;}
   }
   Physics.SyncTransforms();bool meshCase=expected.Count>0;
   if(!meshCase){var colliders=actor.GetComponentsInChildren<Collider>();if(colliders.Length>0){var bounds=colliders[0].bounds;for(int i=1;i<colliders.Length;i++)bounds.Encapsulate(colliders[i].bounds);foreach(var v in CoverageBoxPoints(bounds.size))expected.Add(v+bounds.center);}}
   var result=ObserveCoverage(build,actor,expected,label,meshCase);
   if(result!=null){Touch(actor,.01f,0f);var status=Status(actor);var flames=status==null?new ParticleSystem[0]:((Component)status).GetComponentsInChildren<ParticleSystem>();bool integrated=flames.Length==result.World.Length;for(int i=0;i<flames.Length&&i<result.World.Length;i++)integrated&=Vector3.Distance(flames[i].transform.position,result.World[i])<.025f;Check(integrated,label+": production ignite uses the audited body points");}
   if(result!=null)metrics.Add(new CoverageMetric{fixtureCase=label,count=result.World.Length,minAxisCoverage=result.MinAxisCoverage,maxSurfaceError=result.MaxSurfaceError,meanWidth=result.MeanWidth,meanHeight=result.MeanHeight});var priorStatus=Status(actor);DestroyImmediate(actor);if(priorStatus!=null){Advance(priorStatus,2f);Advance(priorStatus,4f);}foreach(var mesh in meshes)DestroyImmediate(mesh);
  }
  using(var stream=File.Create(Path.Combine(host,"model-coverage-metrics.json")))new DataContractJsonSerializer(typeof(CoverageMetricReport)).WriteObject(stream,new CoverageMetricReport{cases=metrics.ToArray()});
  Check(metrics.Count==fixture.actors.Length,"private numeric model cases produce complete coverage metrics");
 }
}
