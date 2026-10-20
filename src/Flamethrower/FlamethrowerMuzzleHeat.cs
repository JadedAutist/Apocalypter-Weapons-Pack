using UnityEngine;
using UnityEngine.Rendering;
namespace ChainsawPrototype {
 public partial class Plugin {
  Mesh flameMuzzleHeatMesh;Material flameMuzzleHeatMaterial;MeshRenderer flameMuzzleHeatRenderer;
  Transform flameMuzzleHeatTarget;float flameMuzzleHeat;int flameMuzzleHeatFrame=-1;
  void UpdateFlamethrowerMuzzleHeat(bool firing,bool blocked){
   if(Time.timeScale<=0f||Cursor.visible){StopFlamethrowerMuzzleHeat();return;}
   if(flameMuzzleHeatFrame!=Time.frameCount){flameMuzzleHeatFrame=Time.frameCount;flameMuzzleHeat=FlameHeatEnvelope.Step(flameMuzzleHeat,firing&&!blocked,Time.unscaledDeltaTime);}
   float strength=FlameTuningValue(flameMuzzleGlowStrength,1f,0f,2f);
   if(flameMuzzleHeat<=.001f||strength<=.001f){if(flameMuzzleHeatRenderer!=null)flameMuzzleHeatRenderer.enabled=false;return;}
   if(flameMuzzleModel==null||flameMesh==null)return;
   if(flameMuzzleHeatMesh==null){
    var data=FlameMuzzleHeatGeometry.Build(flameMesh.vertices,flameMesh.triangles,flameMuzzleLocal);
    flameMuzzleHeatMesh=new Mesh{name="Flamethrower.MuzzleHeat.Mesh",hideFlags=HideFlags.DontUnloadUnusedAsset|HideFlags.DontSave};
    flameMuzzleHeatMesh.SetVertices(data.Points);flameMuzzleHeatMesh.SetColors(data.Colours);flameMuzzleHeatMesh.SetTriangles(data.Triangles,0);flameMuzzleHeatMesh.RecalculateBounds();
   }
   if(flameMuzzleHeatMaterial==null){
    flameMuzzleHeatMaterial=FlameAlphaMaterial(Texture2D.whiteTexture,"Flamethrower.MuzzleHeat.Material");
    flameMuzzleHeatMaterial.SetInt("_DstBlend",(int)BlendMode.One);flameMuzzleHeatMaterial.renderQueue=2998;
   }
   if(flameMuzzleHeatRenderer==null||flameMuzzleHeatTarget!=flameMuzzleModel){
    if(flameMuzzleHeatRenderer!=null)Destroy(flameMuzzleHeatRenderer.gameObject);
    var go=new GameObject("Flamethrower.MuzzleHeat"){hideFlags=HideFlags.DontSave};go.layer=flameMuzzleModel.gameObject.layer;go.transform.SetParent(flameMuzzleModel,false);
    go.AddComponent<MeshFilter>().sharedMesh=flameMuzzleHeatMesh;flameMuzzleHeatRenderer=go.AddComponent<MeshRenderer>();flameMuzzleHeatRenderer.sharedMaterial=flameMuzzleHeatMaterial;
    flameMuzzleHeatRenderer.shadowCastingMode=ShadowCastingMode.Off;flameMuzzleHeatRenderer.receiveShadows=false;flameMuzzleHeatTarget=flameMuzzleModel;
   }
   var hot=Color.Lerp(new Color(.8f,.035f,.004f),new Color(4f,1.05f,.08f),flameMuzzleHeat*flameMuzzleHeat);
   hot.r*=strength;hot.g*=strength;hot.b*=strength;hot.a=.7f*flameMuzzleHeat;
   if(flameMuzzleHeatMaterial.HasProperty("_Color"))flameMuzzleHeatMaterial.SetColor("_Color",hot);
   if(flameMuzzleHeatMaterial.HasProperty("_TintColor"))flameMuzzleHeatMaterial.SetColor("_TintColor",hot);
   flameMuzzleHeatRenderer.enabled=true;
  }
  void StopFlamethrowerMuzzleHeat(){flameMuzzleHeat=0f;flameMuzzleHeatFrame=-1;if(flameMuzzleHeatRenderer!=null)flameMuzzleHeatRenderer.enabled=false;}
  void CleanupFlamethrowerMuzzleHeat(){
   StopFlamethrowerMuzzleHeat();if(flameMuzzleHeatRenderer!=null)Destroy(flameMuzzleHeatRenderer.gameObject);flameMuzzleHeatRenderer=null;flameMuzzleHeatTarget=null;
   if(flameMuzzleHeatMesh!=null)Destroy(flameMuzzleHeatMesh);flameMuzzleHeatMesh=null;
   if(flameMuzzleHeatMaterial!=null)Destroy(flameMuzzleHeatMaterial);flameMuzzleHeatMaterial=null;
  }
 }
}

