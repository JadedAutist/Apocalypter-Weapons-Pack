using HutongGames.PlayMaker;
using UnityEngine;

namespace ChainsawPrototype
{
    // Add this file to the Weapons Pack project. Plugin.MakeMerchantRequest delegates here.
    internal static class MerchantRequestFactory
    {
        internal static GameObject Create(string name, Transform parent)
        {
            var request = new GameObject("WeaponsPack.Request." + name);
            request.SetActive(false);
            request.transform.SetParent(parent, false);
            try
            {
                var itemName = AddMetadataFsm(request, "ItemName");
                var itemNameVariable = new FsmString { Value = name };
                itemNameVariable.SetName("ItemName");
                itemName.FsmVariables.StringVariables = new[] { itemNameVariable };
                itemName.FsmVariables.Reinitialize();

                var value = AddMetadataFsm(request, "Value");
                var sellValueVariable = new FsmInt { Value = 2000 };
                sellValueVariable.SetName("SellValue");
                value.FsmVariables.IntVariables = new[] { sellValueVariable };
                value.FsmVariables.Reinitialize();
                return request;
            }
            catch
            {
                // This object was created here and never published to a request board.
                // Remove it immediately so same-frame Resources scans cannot see an incomplete FSM.
                Object.DestroyImmediate(request);
                throw;
            }
        }

        private static PlayMakerFSM AddMetadataFsm(GameObject request, string name)
        {
            var component = request.AddComponent<PlayMakerFSM>();
            component.enabled = false;
            // The object is inactive, so Unity has not called Awake. In this PlayMaker build,
            // FsmName and FsmVariables dereference the internal FSM without creating it.
            component.Reset();
            component.FsmName = name;
            return component;
        }
    }
}
