using System;
namespace ChainsawPrototype {
 public enum FlameFuelKind {Gasoline=0,Diesel=1,EngineOil=2}
 internal static class FlamethrowerFuelPolicy {
  internal const float EmptyTolerance=.00001f,Capacity=ChainsawFuel.Capacity;
  internal static bool Valid(FlameFuelKind kind){return kind>=FlameFuelKind.Gasoline&&kind<=FlameFuelKind.EngineOil;}
  internal static string Label(FlameFuelKind kind){return kind==FlameFuelKind.Diesel?"Diesel":kind==FlameFuelKind.EngineOil?"Engine oil":"Gasoline";}
  internal static string Tag(FlameFuelKind kind){return kind==FlameFuelKind.Diesel?"Diesel":kind==FlameFuelKind.EngineOil?"MotorOil":"Gasoline";}
  internal static bool CanAccept(FlameFuelKind current,float litres,FlameFuelKind incoming){return Valid(incoming)&&(litres<=EmptyTolerance||current==incoming);}
  internal static float Transfer(FlameFuelKind current,float stored,FlameFuelKind incoming,float available,float requested){
   if(!CanAccept(current,stored,incoming)||!Finite(stored)||!Finite(available)||!Finite(requested)||available<=0f||requested<=0f)return 0f;
   return Math.Min(available,Math.Min(Math.Max(0f,Capacity-Math.Max(0f,stored)),requested));
  }
  internal static bool CanPay(float litres,float cost){return Finite(litres)&&Finite(cost)&&cost>0f&&litres+EmptyTolerance>=cost;}
  internal static bool CanBurn(float litres){return Finite(litres)&&litres>0f;}
  static bool Finite(float n){return !float.IsNaN(n)&&!float.IsInfinity(n);}
 }
}
