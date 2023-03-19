﻿using Kitchen;
using ApplianceLib.Util;
using KitchenData;
using KitchenLib.Utils;
using System.Collections.Generic;
using UnityEngine;
using ApplianceLib.Api.References;
using static ApplianceLib.Api.References.ApplianceLibGDOs;
using ApplianceLib.Api.Prefab;
using KitchenLib.Customs;

namespace ApplianceLib.Appliances.MixingBowl
{
    public class MixingBowlProvider : CustomAppliance
    {
        public override string UniqueNameID => Ids.MixingBowlSource;
        public override GameObject Prefab => Prefabs.Find("MixingBowlProvider");
        public override PriceTier PriceTier => PriceTier.Medium;
        public override bool SellOnlyAsDuplicate => true;
        public override bool IsPurchasable => true;
        public override ShoppingTags ShoppingTags => ShoppingTags.Cooking | ShoppingTags.Misc;
        public override List<(Locale, ApplianceInfo)> InfoList => new()
        {
            (Locale.English, LocalisationUtils.CreateApplianceInfo("Mixing Bowl", "Provides a mixing bowl", new(), new()))
        };
        public override List<IApplianceProperty> Properties => new()
        {
            new CItemHolder(),
            KitchenPropertiesUtils.GetCItemProvider(Refs.MixingBowl.ID, 1, 1, false, false, true, false, false, true, false)
        };

        public override void SetupPrefab(GameObject prefab)
        {
            prefab.AttachCounter(CounterType.Drawers);

            prefab.ApplyMaterialToChild("HoldPoint/MixingBowl/Model", MaterialReferences.MixingBowl);

            var holdTransform = prefab.GetChild("HoldPoint").transform;
            var holdPoint = prefab.AddComponent<HoldPointContainer>();
            holdPoint.HoldPoint = holdTransform;

            var sourceView = prefab.AddComponent<LimitedItemSourceView>();
            sourceView.HeldItemPosition = holdTransform;
            ReflectionUtils.GetField<LimitedItemSourceView>("Items").SetValue(sourceView, new List<GameObject>()
            {
                GameObjectUtils.GetChildObject(prefab, "HoldPoint/MixingBowl")
            });
        }
    }
}
