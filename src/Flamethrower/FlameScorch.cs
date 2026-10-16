using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChainsawPrototype {
 public partial class Plugin {
  const int FlameScorchGrid=7,MaxFlameScorches=48;
  internal const float FlameScorchLifetimeSeconds=5f;
  readonly List<FlameScorchMark> flameScorches=new List<FlameScorchMark>(MaxFlameScorches);
  GameObject flameScorchRoot;
  Material flameScorchMaterial;
  Texture2D fallbackFlameScorchTexture;
  Texture nativeFlameScorchTexture;
  bool warnedFlameScorchShader;

  // A temporary overlay owns its mesh/material. Nothing is painted into native
  // terrain textures, vehicle materials, meshes, colliders, or game save data.
  internal void PlaceFlameScorch(RaycastHit hit,Texture nativeTexture){
   var surface=hit.collider;
   if(surface==null || surface.isTrigger || !surface.enabled || !surface.gameObject.activeInHierarchy || !ScorchableFlameSurface(surface))return;
   var normal=hit.normal;
   if(!Finite(normal.x)||!Finite(normal.y)||!Finite(normal.z)||normal.sqrMagnitude<.5f || !Finite(hit.point.x)||!Finite(hit.point.y)||!Finite(hit.point.z))return;
   normal.Normalize();
   float now=Time.unscaledTime;
   for(int i=flameScorches.Count-1;i>=0;i--){
    var old=flameScorches[i];
    if(old==null){flameScorches.RemoveAt(i);continue;}
    if(now-old.Born>=FlameScorchLifetimeSeconds){old.Expire();flameScorches.RemoveAt(i);continue;}
    // Repeated contact cannot extend a mark's original five-second deadline.
    if(old.Surface==surface && (surface.transform.TransformPoint(old.LocalImpact)-hit.point).sqrMagnitude<.045f)return;
   }
   if(!EnsureFlameScorchMaterial(nativeTexture))return;
   var mesh=ProjectFlameScorch(surface,hit.point,normal);
   if(mesh==null)return;
   if(flameScorches.Count>=MaxFlameScorches){flameScorches[0].Expire();flameScorches.RemoveAt(0);}
   var go=new GameObject("Flamethrower.TemporaryScorch");
   go.hideFlags=HideFlags.DontSave;
   // Keep overlays outside native item/vehicle hierarchies, including ES3's
   // saved children. Their local geometry still follows the hit collider pose.
   if(flameScorchRoot==null)flameScorchRoot=new GameObject("Flamethrower.TemporaryScorches"){hideFlags=HideFlags.DontSave};
   go.layer=0;go.transform.SetParent(flameScorchRoot.transform,false);
   go.AddComponent<MeshFilter>().sharedMesh=mesh;
   var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=flameScorchMaterial;
   renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
   renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
   var mark=go.AddComponent<FlameScorchMark>();
   mark.Initialize(surface,surface.transform.InverseTransformPoint(hit.point),mesh,renderer,now);
   flameScorches.Add(mark);
  }

  bool ScorchableFlameSurface(Collider surface){
   if(OwnCollider(surface))return false;
   for(var node=surface.transform;node!=null;node=node.parent){
    if(node.name.StartsWith("Flamethrower.",StringComparison.Ordinal) || node.GetComponent<FlamethrowerItem>()!=null || node.GetComponent<ChainsawItem>()!=null)return false;
    foreach(var behaviour in node.GetComponents<MonoBehaviour>()){
     if(behaviour==null)continue;
     var type=behaviour.GetType();
     if(type.Name=="NPCAI" || type.FullName=="Apocaplayer.Body" || type.FullName=="Apocaplayer.Runner")return false;
    }
   }
   return true;
  }

  Mesh ProjectFlameScorch(Collider surface,Vector3 impact,Vector3 normal){
   var axis=Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.right;
   var tangent=Vector3.Cross(axis,normal).normalized;
   var bitangent=Vector3.Cross(normal,tangent).normalized;
   // Rotate the soot without advancing Unity's random state used by world loot.
   float angle=Mathf.Repeat(Mathf.Sin(Vector3.Dot(impact,new Vector3(12.9898f,78.233f,37.719f)))*43758.5453f,360f)*Mathf.Deg2Rad;
   var rotated=tangent*Mathf.Cos(angle)+bitangent*Mathf.Sin(angle);
   bitangent=Vector3.Cross(normal,rotated).normalized;tangent=rotated;
   const float radius=.33f,projectionDepth=.24f,surfaceOffset=.004f;
   int vertexCount=FlameScorchGrid*FlameScorchGrid;
   var points=new Vector3[vertexCount];var uv=new Vector2[vertexCount];var colours=new Color[vertexCount];var supported=new bool[vertexCount];
   var worldPoints=new Vector3[vertexCount];var triangles=new List<int>((FlameScorchGrid-1)*(FlameScorchGrid-1)*6);
   for(int y=0;y<FlameScorchGrid;y++)for(int x=0;x<FlameScorchGrid;x++){
    int index=y*FlameScorchGrid+x;float u=x/(float)(FlameScorchGrid-1),v=y/(float)(FlameScorchGrid-1);
    float dx=u*2f-1f,dy=v*2f-1f,distance=Mathf.Sqrt(dx*dx+dy*dy);
    uv[index]=new Vector2(u,v);points[index]=surface.transform.InverseTransformPoint(impact);colours[index]=new Color(1f,1f,1f,Mathf.SmoothStep(0f,1f,Mathf.Clamp01((1f-distance)*4f)));
    if(distance>1.08f)continue;
    var candidate=impact+(tangent*dx+bitangent*dy)*radius;
    RaycastHit projected;
    // Cast against the actual surface only: no stamp jumps to neighbouring
    // objects or bridges unsupported areas at a terrain edge/car opening.
    if(!surface.Raycast(new Ray(candidate+normal*projectionDepth,-normal),out projected,projectionDepth*2f) || Vector3.Dot(projected.normal,normal)<.5f)continue;
    supported[index]=true;worldPoints[index]=projected.point;
    points[index]=surface.transform.InverseTransformPoint(projected.point+projected.normal*surfaceOffset);
   }
   float edgeLimit=(radius*2f/(FlameScorchGrid-1))*1.8f;
   for(int y=0;y<FlameScorchGrid-1;y++)for(int x=0;x<FlameScorchGrid-1;x++){
    int a=y*FlameScorchGrid+x,b=a+1,c=a+FlameScorchGrid,d=c+1;
    AppendScorchTriangle(triangles,a,b,c,supported,worldPoints,edgeLimit);
    AppendScorchTriangle(triangles,b,d,c,supported,worldPoints,edgeLimit);
   }
   if(triangles.Count<3)return null;
   var mesh=new Mesh{name="Flamethrower.TemporaryScorch.Mesh",hideFlags=HideFlags.DontSave};
   mesh.vertices=points;mesh.uv=uv;mesh.colors=colours;mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
   return mesh;
  }

  static void AppendScorchTriangle(List<int> triangles,int a,int b,int c,bool[] supported,Vector3[] points,float edgeLimit){
   if(!supported[a]||!supported[b]||!supported[c])return;
   float max=edgeLimit*edgeLimit;
   if((points[a]-points[b]).sqrMagnitude>max || (points[b]-points[c]).sqrMagnitude>max || (points[c]-points[a]).sqrMagnitude>max)return;
   triangles.Add(a);triangles.Add(b);triangles.Add(c);
  }

  bool EnsureFlameScorchMaterial(Texture nativeTexture){
   if(nativeTexture!=null)nativeFlameScorchTexture=nativeTexture;
   if(flameScorchMaterial==null){
    var shader=Shader.Find("Sprites/Default")??Shader.Find("Legacy Shaders/Particles/Alpha Blended")??Shader.Find("Particles/Standard Unlit");
    if(shader==null){if(!warnedFlameScorchShader){Logger.LogWarning("Flamethrower temporary soot disabled: no alpha-blended shader is loaded.");warnedFlameScorchShader=true;}return false;}
    flameScorchMaterial=new Material(shader){name="Flamethrower.TemporaryScorch.Material",hideFlags=HideFlags.DontUnloadUnusedAsset|HideFlags.DontSave};
    flameScorchMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);flameScorchMaterial.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
    flameScorchMaterial.SetInt("_ZWrite",0);flameScorchMaterial.SetInt("_Cull",(int)CullMode.Off);flameScorchMaterial.SetFloat("_Mode",2f);
    // Soft-particle depth fading would erase an overlay sitting on its surface.
    flameScorchMaterial.DisableKeyword("SOFTPARTICLES_ON");flameScorchMaterial.SetFloat("_SoftParticlesEnabled",0f);flameScorchMaterial.SetFloat("_InvFade",1000f);
    flameScorchMaterial.EnableKeyword("_ALPHABLEND_ON");flameScorchMaterial.DisableKeyword("_ALPHATEST_ON");flameScorchMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    flameScorchMaterial.renderQueue=3001;
   }
   if(nativeFlameScorchTexture!=null)flameScorchMaterial.mainTexture=nativeFlameScorchTexture;
   else{
    if(fallbackFlameScorchTexture==null){
     const int size=64;fallbackFlameScorchTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Flamethrower.TemporarySootFallback",hideFlags=HideFlags.DontUnloadUnusedAsset|HideFlags.DontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
     var pixels=new Color[size*size];
     for(int y=0;y<size;y++)for(int x=0;x<size;x++){
      float dx=(x-(size-1)*.5f)/((size-1)*.5f),dy=(y-(size-1)*.5f)/((size-1)*.5f),r=Mathf.Sqrt(dx*dx+dy*dy);
      float fleck=.83f+.17f*Mathf.Sin(x*2.19f+y*1.73f)*Mathf.Sin(y*3.17f-x*.61f);
      pixels[y*size+x]=new Color(1f,1f,1f,Mathf.Pow(Mathf.Clamp01(1f-r),.9f)*fleck);
     }
     fallbackFlameScorchTexture.SetPixels(pixels);fallbackFlameScorchTexture.Apply(false,true);
    }
    flameScorchMaterial.mainTexture=fallbackFlameScorchTexture;
   }
   return true;
  }

  internal void ClearFlameScorches(){
   ClearFlameSurfaceFires();
   foreach(var mark in flameScorches)if(mark!=null)mark.Expire();flameScorches.Clear();
   if(flameScorchRoot!=null)Destroy(flameScorchRoot);flameScorchRoot=null;
   if(flameScorchMaterial!=null)Destroy(flameScorchMaterial);flameScorchMaterial=null;
   if(fallbackFlameScorchTexture!=null)Destroy(fallbackFlameScorchTexture);fallbackFlameScorchTexture=null;
   // Native textures are borrowed, never destroyed or edited by this mod.
   nativeFlameScorchTexture=null;
  }
 }

 // A separate small lifetime component lets soot finish fading after unequip.
 internal sealed class FlameScorchMark:MonoBehaviour {
  internal Collider Surface{get;private set;}
  internal Vector3 LocalImpact{get;private set;}
  internal float Born{get;private set;}
  Mesh ownedMesh;MeshRenderer meshRenderer;MaterialPropertyBlock properties;float initialAlpha;bool expired;
  static readonly int Colour=Shader.PropertyToID("_Color"),Tint=Shader.PropertyToID("_TintColor");
  internal void Initialize(Collider surface,Vector3 localImpact,Mesh mesh,MeshRenderer renderer,float now){
   Surface=surface;LocalImpact=localImpact;ownedMesh=mesh;meshRenderer=renderer;Born=now;properties=new MaterialPropertyBlock();
   // The legacy particle shader doubles vertex/tint alpha; compensate to keep
   // opacity below saturation and preserve a smooth fade with either shader.
   initialAlpha=renderer.sharedMaterial.shader.name=="Legacy Shaders/Particles/Alpha Blended" ? .31f : .62f;
   FollowSurface();ApplyOpacity(1f);
  }
  void LateUpdate(){FollowSurface();}
  void FollowSurface(){
   if(expired||Surface==null||meshRenderer==null)return;
   meshRenderer.enabled=Surface.enabled&&Surface.gameObject.activeInHierarchy;
   var target=Surface.transform;transform.SetPositionAndRotation(target.position,target.rotation);transform.localScale=target.lossyScale;
  }
  void Update(){
   if(expired)return;
   float age=Time.unscaledTime-Born;
   if(Surface==null || age>=Plugin.FlameScorchLifetimeSeconds){Expire();return;}
   // Visible briefly, then progressively transparent; fully gone at five seconds.
   float fade=Mathf.Clamp01((age-.65f)/(Plugin.FlameScorchLifetimeSeconds-.65f));
   ApplyOpacity(1f-fade*fade*(3f-2f*fade));
  }
  void ApplyOpacity(float opacity){
   if(meshRenderer==null||properties==null)return;
   var black=new Color(0f,0f,0f,initialAlpha*opacity);properties.SetColor(Colour,black);properties.SetColor(Tint,black);meshRenderer.SetPropertyBlock(properties);
  }
  internal void Expire(){expired=true;if(meshRenderer!=null)meshRenderer.enabled=false;Destroy(gameObject);}
  void OnDestroy(){if(ownedMesh!=null)Destroy(ownedMesh);ownedMesh=null;}
 }
}
