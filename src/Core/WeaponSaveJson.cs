using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
namespace ChainsawPrototype {
 // Keep the existing public save schema while retaining nested record arrays.
 internal static class WeaponSaveJson {
  internal static string Serialize<T>(T value){using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(stream,value);return Encoding.UTF8.GetString(stream.ToArray());}}
  internal static T Deserialize<T>(string json){using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
 }
}
