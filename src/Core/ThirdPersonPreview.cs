using System;
using HarmonyLib;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  Light previewKey,previewFill;Camera thirdPreviewCamera;RenderTexture thirdPreviewTexture;GameObject previewBody;SkinnedMeshRenderer previewBodyRenderer;string thirdPreviewError;
  void DrawThirdPersonPreview(){
   GUILayout.Label("Third person holding preview");Rect rect=GUILayoutUtility.GetRect(10f,220f,GUILayout.ExpandWidth(true));GUI.Box(rect,GUIContent.none);
   if(!enabledPreview||lastBody==null){GUI.Label(new Rect(rect.x+8,rect.y+8,rect.width-16,60),"Equip a supported weapon in game to preview the third person grip.");return;}
   if(Event.current.type==EventType.Repaint){try{RenderThirdPersonPreview();thirdPreviewError=null;}catch(Exception e){thirdPreviewError=e.GetBaseException().Message;}}
   if(thirdPreviewTexture!=null)GUI.DrawTexture(rect,thirdPreviewTexture,ScaleMode.ScaleToFit,false);
   if(thirdPreviewError!=null)GUI.Label(new Rect(rect.x+8,rect.y+8,rect.width-16,50),"Preview unavailable: "+thirdPreviewError);
  }
  float nextThirdRender;
  void RenderThirdPersonPreview(){
   if(Time.realtimeSinceStartup<nextThirdRender)return;nextThirdRender=Time.realtimeSinceStartup+.08f;
   var t=lastBody.GetType();var root=AccessTools.Field(t,"Root").GetValue(lastBody) as GameObject;if(root==null)return;
   var source=AccessTools.Field(t,"_shadow").GetValue(lastBody) as SkinnedMeshRenderer;if(source==null)source=AccessTools.Field(t,"_smr").GetValue(lastBody) as SkinnedMeshRenderer;if(source==null||source.sharedMesh==null)return;
   if(previewBody==null){previewBody=new GameObject("Chainsaw.SettingsBody");previewBody.layer=31;previewBodyRenderer=previewBody.AddComponent<SkinnedMeshRenderer>();previewBody.SetActive(false);}
   previewBodyRenderer.sharedMesh=source.sharedMesh;previewBodyRenderer.sharedMaterials=source.sharedMaterials;previewBodyRenderer.bones=source.bones;previewBodyRenderer.rootBone=source.rootBone;previewBodyRenderer.localBounds=source.localBounds;previewBodyRenderer.updateWhenOffscreen=true;
   previewBody.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);previewBody.transform.localScale=source.transform.lossyScale;
   if(thirdPreviewTexture==null){thirdPreviewTexture=new RenderTexture(768,432,24);thirdPreviewTexture.Create();}
   if(thirdPreviewCamera==null){var go=new GameObject("Chainsaw.ThirdPreviewCamera");thirdPreviewCamera=go.AddComponent<Camera>();thirdPreviewCamera.enabled=false;thirdPreviewCamera.cullingMask=1<<31;thirdPreviewCamera.clearFlags=CameraClearFlags.SolidColor;thirdPreviewCamera.backgroundColor=new Color(.09f,.09f,.1f,1);thirdPreviewCamera.nearClipPlane=.02f;thirdPreviewCamera.farClipPlane=20f;thirdPreviewCamera.orthographic=true;thirdPreviewCamera.orthographicSize=1.05f;}
   Vector3 center=source.bounds.center;if(source.bounds.size.sqrMagnitude<.1f)center=root.transform.position+Vector3.up;
   thirdPreviewCamera.transform.position=center+root.transform.forward*2.6f+root.transform.right*1.7f+Vector3.up*.25f;thirdPreviewCamera.transform.LookAt(center,Vector3.up);thirdPreviewCamera.aspect=16f/9f;thirdPreviewCamera.targetTexture=thirdPreviewTexture;
   if(previewKey==null){var key=new GameObject("Chainsaw.PreviewKeyLight");previewKey=key.AddComponent<Light>();previewKey.type=LightType.Directional;previewKey.cullingMask=1<<31;previewKey.intensity=1.4f;previewKey.shadows=LightShadows.None;previewKey.enabled=false;var fill=new GameObject("Chainsaw.PreviewFillLight");previewFill=fill.AddComponent<Light>();previewFill.type=LightType.Directional;previewFill.cullingMask=1<<31;previewFill.intensity=.85f;previewFill.shadows=LightShadows.None;previewFill.enabled=false;}
   previewKey.transform.rotation=Quaternion.LookRotation(center-thirdPreviewCamera.transform.position,Vector3.up);previewFill.transform.rotation=Quaternion.LookRotation(-root.transform.right+Vector3.down*.3f,Vector3.up);
   bool wasActive=thirdModel!=null&&thirdModel.activeSelf;Transform[] layerNodes=null;int[] oldLayers=null;
   samplingPending=true;samplingThirdPerson=true;
   try{UpdateThirdPerson(lastBody,new object[]{"ThirdPerson"});if(thirdModel==null)return;layerNodes=thirdModel.GetComponentsInChildren<Transform>(true);oldLayers=new int[layerNodes.Length];for(int i=0;i<layerNodes.Length;i++){oldLayers[i]=layerNodes[i].gameObject.layer;layerNodes[i].gameObject.layer=31;}previewBody.SetActive(true);previewKey.enabled=true;previewFill.enabled=true;thirdPreviewCamera.Render();}
   finally{if(previewKey!=null)previewKey.enabled=false;if(previewFill!=null)previewFill.enabled=false;samplingPending=false;samplingThirdPerson=false;previewBody.SetActive(false);if(layerNodes!=null)for(int i=0;i<layerNodes.Length;i++)if(layerNodes[i]!=null)layerNodes[i].gameObject.layer=oldLayers[i];if(thirdModel!=null)thirdModel.SetActive(wasActive);}
  }
 }
}
