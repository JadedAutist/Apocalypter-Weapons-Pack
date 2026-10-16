using System.Collections.Generic;
using UnityEngine;
namespace ChainsawPrototype {
 internal static class FlameMuzzleHeatGeometry {
  internal const float Depth=.018f,Offset=.00035f;
  internal sealed class Data {
   internal readonly List<Vector3> Points=new List<Vector3>();
   internal readonly List<Color> Colours=new List<Color>();
   internal readonly List<int> Triangles=new List<int>();
  }
  internal static Data Build(Vector3[] vertices,int[] triangles,Vector3 muzzle){
   var result=new Data();var polygon=new List<Vector3>(4);float cut=muzzle.z-Depth;
   // WeaponObj's two-sided mesh contains the reverse copies in its second half.
   // The glow material is double-sided, so retain the original triangles only.
   for(int i=0;i<triangles.Length/2;i+=3){
    polygon.Clear();
    for(int edge=0;edge<3;edge++){
     var a=vertices[triangles[i+edge]];var b=vertices[triangles[i+(edge+1)%3]];
     bool insideA=a.z>=cut,insideB=b.z>=cut;
     if(insideA)polygon.Add(a);
     if(insideA!=insideB)polygon.Add(a+(b-a)*((cut-a.z)/(b.z-a.z)));
    }
    int first=result.Points.Count;
    foreach(var point in polygon){
     var radial=new Vector3(point.x-muzzle.x,point.y-muzzle.y,0f).normalized;
     result.Points.Add(point+radial*Offset+new Vector3(0f,0f,Offset));
     float alpha=Mathf.Clamp01((point.z-cut)/Depth);alpha=alpha*alpha*(3f-2f*alpha);
     result.Colours.Add(new Color(1f,1f,1f,alpha));
    }
    for(int corner=1;corner<polygon.Count-1;corner++){result.Triangles.Add(first);result.Triangles.Add(first+corner);result.Triangles.Add(first+corner+1);}
   }
   return result;
  }
 }
}
