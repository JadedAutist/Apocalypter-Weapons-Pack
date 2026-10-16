using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace ChainsawPrototype {
 internal static class WeaponObj {
  internal sealed class Geometry {
   internal readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
   internal readonly List<Vector2> uv=new List<Vector2>();
   internal readonly List<int> triangles=new List<int>();
  }
  internal static Mesh Load(string path,bool twoSided=false){
   var data=Read(path,twoSided);
   var mesh=new Mesh{name=Path.GetFileNameWithoutExtension(path),hideFlags=HideFlags.DontUnloadUnusedAsset};
   if(data.vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
   mesh.SetVertices(data.vertices);mesh.SetUVs(0,data.uv);mesh.SetTriangles(data.triangles,0);mesh.SetNormals(data.normals);mesh.RecalculateBounds();return mesh;
  }
  // Keep parsing independent of native Mesh allocation so the installed source
  // assets can be verified before starting the game.
  internal static Geometry Read(string path,bool twoSided=false){
   var points=new List<Vector3>();var uvs=new List<Vector2>();var sourceNormals=new List<Vector3>();var data=new Geometry();
   var ci=CultureInfo.InvariantCulture;
   foreach(var line in File.ReadLines(path)){
    var content=line.Split('#')[0];var p=content.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);if(p.Length==0)continue;
    if(p[0]=="v")points.Add(new Vector3(-float.Parse(p[1],ci),float.Parse(p[2],ci),float.Parse(p[3],ci)));
    else if(p[0]=="vt")uvs.Add(new Vector2(float.Parse(p[1],ci),float.Parse(p[2],ci)));
    else if(p[0]=="vn")sourceNormals.Add(new Vector3(-float.Parse(p[1],ci),float.Parse(p[2],ci),float.Parse(p[3],ci)).normalized);
    else if(p[0]=="f"){
     int start=data.vertices.Count;
     for(int i=1;i<p.Length;i++){
      var q=p[i].Split('/');data.vertices.Add(points[Index(q[0],points.Count)]);
      data.uv.Add(q.Length>1&&q[1].Length>0?uvs[Index(q[1],uvs.Count)]:Vector2.zero);
      data.normals.Add(q.Length>2&&q[2].Length>0?sourceNormals[Index(q[2],sourceNormals.Count)]:Vector3.zero);
     }
     // Mirroring X changes handedness, so reverse the source winding once.
     for(int i=1;i<p.Length-2;i++){data.triangles.Add(start);data.triangles.Add(start+i+1);data.triangles.Add(start+i);}
    }
   }
   var generated=new Vector3[data.vertices.Count];
   for(int i=0;i<data.triangles.Count;i+=3){
    int a=data.triangles[i],b=data.triangles[i+1],c=data.triangles[i+2];
    var normal=Vector3.Cross(data.vertices[b]-data.vertices[a],data.vertices[c]-data.vertices[a]);
    generated[a]+=normal;generated[b]+=normal;generated[c]+=normal;
   }
   for(int i=0;i<data.normals.Count;i++)if(data.normals[i].sqrMagnitude<.000001f)data.normals[i]=generated[i].normalized;
   if(twoSided){
    // The supplied flamethrower has inward-facing islands and open sheets.
    // Separate backface vertices keep both sides correctly lit by an opaque
    // shader without cancelling normals or relying on shader-specific cull flags.
    int count=data.vertices.Count,triangleCount=data.triangles.Count;
    for(int i=0;i<count;i++){data.vertices.Add(data.vertices[i]);data.uv.Add(data.uv[i]);data.normals.Add(-data.normals[i]);}
    for(int i=0;i<triangleCount;i+=3){data.triangles.Add(data.triangles[i]+count);data.triangles.Add(data.triangles[i+2]+count);data.triangles.Add(data.triangles[i+1]+count);}
   }
   return data;
  }
  static int Index(string text,int count){int value=int.Parse(text,CultureInfo.InvariantCulture);if(value==0)throw new InvalidDataException("OBJ indices must not be zero.");int index=value<0?count+value:value-1;if(index<0||index>=count)throw new InvalidDataException("OBJ index outside source array.");return index;}
 }
}
