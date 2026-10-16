// Headless adapters model inactive Unity objects and PlayMaker's null-dereferencing metadata.
// The factory under test is the same source compiled into the patched DLL.
using System;
using System.Collections.Generic;
using System.Linq;
using ChainsawPrototype;
using HutongGames.PlayMaker;
using UnityEngine;

namespace UnityEngine
{
    public class Object
    {
        public static readonly List<GameObject> Alive = new List<GameObject>();
        public static void DestroyImmediate(Object obj) { Alive.Remove((GameObject)obj); }
    }
    public class Transform { public Transform parent; public void SetParent(Transform value, bool world) { parent = value; } }
    public class GameObject : Object
    {
        public string name; public bool activeSelf = true;
        public Transform transform = new Transform();
        public readonly List<PlayMakerFSM> Components = new List<PlayMakerFSM>();
        public static int FailOnAddNumber;
        public GameObject(string name) { this.name = name; Alive.Add(this); }
        public void SetActive(bool value) { activeSelf = value; }
        public T AddComponent<T>() where T : PlayMakerFSM, new()
        {
            if (FailOnAddNumber > 0 && Components.Count + 1 == FailOnAddNumber) throw new InvalidOperationException("Injected creation failure");
            var result = new T(); Components.Add(result);
            // Awake initializes an active component; adding to an inactive object defers Awake.
            if (activeSelf) result.Reset();
            return result;
        }
    }
}
namespace HutongGames.PlayMaker
{
    public class FsmString { public string Name {get;private set;} public string Value; public void SetName(string name){Name=name;} }
    public class FsmInt { public string Name {get;private set;} public int Value; public void SetName(string name){Name=name;} }
    public class FsmVariables { public FsmString[] StringVariables; public FsmInt[] IntVariables; public void Reinitialize(){} }
    public class Fsm { public string Name; public FsmVariables Variables = new FsmVariables(); }
}
public class PlayMakerFSM
{
    public bool enabled = true; public Fsm Fsm; public int ResetCalls;
    public string FsmName { get { return Fsm.Name; } set { Fsm.Name = value; } }
    public FsmVariables FsmVariables { get { return Fsm.Variables; } }
    public void Reset() { ResetCalls++; if (Fsm == null) Fsm = new Fsm(); }
}
internal static class FactoryTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static void Main()
    {
        try { RunChecks(); }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error); Environment.ExitCode = 1; }
    }
    private static void RunChecks()
    {
        var parent = new GameObject("WeaponsPack.MerchantRequestData"); parent.SetActive(false);
        var baseline = new GameObject("unpatched"); baseline.SetActive(false);
        bool reproduced = false;
        try { baseline.AddComponent<PlayMakerFSM>().FsmName = "ItemName"; }
        catch (NullReferenceException) { reproduced = true; }
        finally { UnityEngine.Object.DestroyImmediate(baseline); }
        Check(reproduced, "Unpatched inactive creation must reproduce the null-FSM exception");
        foreach (var name in new[] { "Chainsaw", "Flamethrower" })
        {
            var request = MerchantRequestFactory.Create(name, parent.transform);
            Check(!request.activeSelf && request.transform.parent == parent.transform, "Request stays inactive under its metadata parent");
            Check(request.Components.Count == 2 && request.Components.All(f => !f.enabled && f.Fsm != null && f.ResetCalls == 1), "Both internal FSMs are initialized without enabling behavior");
            Check(request.Components[0].FsmName == "ItemName" && request.Components[0].FsmVariables.StringVariables.Single().Value == name, "Weapon name metadata retained");
            Check(request.Components[1].FsmName == "Value" && request.Components[1].FsmVariables.IntVariables.Single().Value == 2000, "Sale value metadata retained");
        }
        int before = UnityEngine.Object.Alive.Count;
        GameObject.FailOnAddNumber = 2;
        bool propagated = false;
        try { MerchantRequestFactory.Create("failure-test", parent.transform); }
        catch (InvalidOperationException) { propagated = true; }
        Check(propagated, "Construction failure propagates to the caller");
        Check(UnityEngine.Object.Alive.Count == before, "No partially constructed request remains visible to scans");
        Console.WriteLine("PASS: " + checks + " factory checks, including unpatched negative control and failure cleanup.");
    }
}
