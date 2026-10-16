using System.Linq;
using UnityEngine;
namespace ChainsawPrototype {
 public partial class Plugin {
  Material NativeFireplaceSurfaceMaterial(){
   // half_barrel_firepit/VFX_Fire_Floor_01_Smoke_Less/Fire Quad.
   // Copy only the visual material; the native fire object also owns sound,
   // a light and a damaging trigger, which must not accompany temporary fire.
   return Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m=>
    (m.name=="M_VFX_Fire_Quad_01"||m.name=="M_VFX_Fire_Quad_01 (Instance)")&&m.shader!=null&&m.shader.isSupported)??NativeFlameMaterial();
  }
 }
}
