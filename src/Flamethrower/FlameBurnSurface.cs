using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChainsawPrototype {
 // Runtime-derived maps, never extracted game textures in the distribution.
 // Corpse references own the cache lifetime, including after the pack host dies.
 internal sealed class BurnSkinTexture {
  static readonly Dictionary<string,BurnSkinTexture> cache=new Dictionary<string,BurnSkinTexture>(StringComparer.OrdinalIgnoreCase);
  internal Texture2D Texture;string path;int users;
  internal static BurnSkinTexture Acquire(string path,Action<string> warning){
   BurnSkinTexture item;if(cache.TryGetValue(path,out item)){item.users++;return item;}
   Texture2D texture=null;
   try{
    if(!File.Exists(path))return null;
    var file=new FileInfo(path);if(file.Length>8*1024*1024)throw new InvalidOperationException("Burnt skin asset is too large");
    texture=new Texture2D(2,2,TextureFormat.RGBA32,true,false){name="Flamethrower.BurntSkin",hideFlags=HideFlags.DontSave,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
    if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(path)))throw new InvalidOperationException("Burnt skin PNG could not be decoded");
    if(texture.width>2048||texture.height>2048)throw new InvalidOperationException("Burnt skin dimensions exceed the supported limit");
    item=new BurnSkinTexture{Texture=texture,path=path,users=1};cache.Add(path,item);return item;
   }catch(Exception error){if(texture!=null)UnityEngine.Object.Destroy(texture);if(warning!=null)warning(error.GetBaseException().Message);return null;}
  }
  internal void Release(){if(--users>0)return;cache.Remove(path);if(Texture!=null)UnityEngine.Object.Destroy(Texture);Texture=null;}
 }
 internal sealed class BurnSurfaceMaps {
  static readonly Dictionary<Texture2D,BurnSurfaceMaps> cache=new Dictionary<Texture2D,BurnSurfaceMaps>();
  internal Texture2D Normal,Finish;
  Texture2D source;int users;bool authored;
  internal static BurnSurfaceMaps Acquire(Texture2D texture,Action<string> warning){return AcquireCore(texture,warning,false);}
  internal static BurnSurfaceMaps AcquireSkin(Texture2D texture,Action<string> warning){return AcquireCore(texture,warning,true);}
  static BurnSurfaceMaps AcquireCore(Texture2D texture,Action<string> warning,bool authored){
   BurnSurfaceMaps maps;if(cache.TryGetValue(texture,out maps)){maps.users++;return maps;}
   maps=new BurnSurfaceMaps{source=texture,users=1,authored=authored};cache.Add(texture,maps);
   try{maps.Build(texture);}catch(Exception error){maps.Dispose();if(warning!=null)warning(error.GetBaseException().Message);}
   return maps;
  }
  internal void Release(){if(--users>0)return;cache.Remove(source);Dispose();source=null;}
  void Dispose(){if(Normal!=null)UnityEngine.Object.Destroy(Normal);if(Finish!=null)UnityEngine.Object.Destroy(Finish);Normal=Finish=null;}
  void Build(Texture2D texture){
   int width=Mathf.Min(256,texture.width),height=Mathf.Min(256,texture.height);if(width<2||height<2)return;
   Color32[] input;int inputWidth,inputHeight;
   if(texture.isReadable&&texture.width<=2048&&texture.height<=2048){input=texture.GetPixels32();inputWidth=texture.width;inputHeight=texture.height;}
   else{input=ReadGpu(texture,width,height);inputWidth=width;inputHeight=height;}
   var heights=new float[width*height];var finish=new Color32[heights.Length];var normal=new Color32[heights.Length];
   for(int y=0;y<height;y++)for(int x=0;x<width;x++){
    var pixel=input[(y*inputHeight/height)*inputWidth+x*inputWidth/width];float brightness=(pixel.r*.2126f+pixel.g*.7152f+pixel.b*.0722f)/255f;
    float crack=authored?Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.02f,.18f,(pixel.r-Mathf.Max(pixel.g,pixel.b))/255f)):Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.12f,.65f,brightness));int index=y*width+x;
    // Keep the approved albedo. Brighter char ridges get subtle relief, while
    // red gaps sit below the plates and have a restrained, varied sheen.
    float light=Mathf.InverseLerp(.02f,.30f,brightness);
    heights[index]=authored?Mathf.Lerp(.70f+.22f*light,.12f+.08f*light,crack):1f-crack;
    float smoothness=authored?Mathf.Lerp(Mathf.Lerp(.065f,.02f,light),.18f+.09f*light,crack):Mathf.Lerp(.025f,.3f,crack);
    finish[index]=new Color32(0,0,0,(byte)Mathf.RoundToInt(smoothness*255f));
   }
   if(authored)heights=SoftenHeight(heights,width,height);
   for(int y=0;y<height;y++)for(int x=0;x<width;x++){
    // Match the original texture's wrapping at the atlas boundary. The visible
    // crop stays within the cold left half, away from glowing coals on the right.
    float dx=heights[y*width+(x+width-1)%width]-heights[y*width+(x+1)%width];
    float dy=heights[((y+height-1)%height)*width+x]-heights[((y+1)%height)*width+x];
    float strength=authored?.9f:.45f;var direction=new Vector3(dx*strength,dy*strength,1f).normalized;
    normal[y*width+x]=new Color(direction.x*.5f+.5f,direction.y*.5f+.5f,direction.z*.5f+.5f,1f);
   }
   Normal=Map(width,height,"Flamethrower.BurnCrackNormal",normal);
   Finish=Map(width,height,"Flamethrower.BurnCrackFinish",finish);
  }
  static float[] SoftenHeight(float[] height,int width,int rows){
   var result=new float[height.Length];
   for(int y=0;y<rows;y++)for(int x=0;x<width;x++){
    float total=0f;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)total+=height[((y+dy+rows)%rows)*width+(x+dx+width)%width]*(dx==0?2f:1f)*(dy==0?2f:1f);
    result[y*width+x]=total/16f;
   }return result;
  }
  static Texture2D Map(int width,int height,string name,Color32[] pixels){
   var texture=new Texture2D(width,height,TextureFormat.RGBA32,true,true){name=name,hideFlags=HideFlags.DontSave,filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Repeat};
   try{texture.SetPixels32(pixels);texture.Apply(true,false);return texture;}catch{UnityEngine.Object.Destroy(texture);throw;}
  }
  static Color32[] ReadGpu(Texture2D source,int width,int height){
   if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new InvalidOperationException("No graphics device for optional surface maps");
   var previous=RenderTexture.active;bool previousSrgb=GL.sRGBWrite;RenderTexture temporary=null;Texture2D readable=null;
   try{
    // Read albedo in the same perceptual domain as GetPixels32. A linear target
    // crushed real mid-grey cracks while our black/white test stayed unchanged.
    temporary=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
    GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;Graphics.Blit(source,temporary);RenderTexture.active=temporary;
    readable=new Texture2D(width,height,TextureFormat.RGBA32,false,false);
    readable.ReadPixels(new Rect(0,0,width,height),0,0,false);return readable.GetPixels32();
   }finally{RenderTexture.active=previous;GL.sRGBWrite=previousSrgb;if(temporary!=null)RenderTexture.ReleaseTemporary(temporary);if(readable!=null)UnityEngine.Object.Destroy(readable);}
  }
  internal void Apply(Material material){
   Vector2 scale=authored?Vector2.one:new Vector2(.48f,.98f),offset=authored?Vector2.zero:new Vector2(.01f,.01f);
   if(Normal!=null&&material.HasProperty("_BumpMap")){
    material.SetTexture("_BumpMap",Normal);material.SetTextureScale("_BumpMap",scale);material.SetTextureOffset("_BumpMap",offset);
    if(material.HasProperty("_BumpScale"))material.SetFloat("_BumpScale",authored?.8f:.65f);material.EnableKeyword("_NORMALMAP");
   }
   if(Finish!=null&&material.HasProperty("_MetallicGlossMap")){
    material.SetTexture("_MetallicGlossMap",Finish);material.SetTextureScale("_MetallicGlossMap",scale);material.SetTextureOffset("_MetallicGlossMap",offset);
    if(material.HasProperty("_GlossMapScale"))material.SetFloat("_GlossMapScale",1f);material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");material.EnableKeyword("_METALLICGLOSSMAP");
   }
  }
 }
}
