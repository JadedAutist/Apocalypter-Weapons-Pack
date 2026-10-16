using System;
using System.Collections.Generic;
using System.Threading;

namespace ChainsawPrototype {
 // Optional integrations use exact names. Cache misses too: Harmony's fallback
 // enumerates every loaded type and logs a warning on every failed lookup.
 internal static class OptionalWeaponTypes {
  sealed class Entry { internal int Generation;internal Type Type; }
  static readonly Dictionary<string,Entry> cache=new Dictionary<string,Entry>(StringComparer.Ordinal);
  static int generation;
  static OptionalWeaponTypes(){AppDomain.CurrentDomain.AssemblyLoad+=(sender,args)=>Interlocked.Increment(ref generation);}
  internal static Type Find(string fullName){
   lock(cache){
    int current=Interlocked.CompareExchange(ref generation,0,0);Entry entry;
    if(cache.TryGetValue(fullName,out entry)&&(entry.Type!=null||entry.Generation==current))return entry.Type;
    Type found=null;
    foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){
     found=assembly.GetType(fullName,false,false);if(found!=null)break;
    }
    cache[fullName]=new Entry{Generation=current,Type=found};return found;
   }
  }
 }
}
