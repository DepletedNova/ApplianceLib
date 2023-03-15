﻿using ApplianceLib.Api.FlexibleContainer;
using Kitchen;
using KitchenData;
using KitchenMods;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;

namespace ApplianceLib.Customs.FlexibleContainer
{
    public class ApplyProcessesToFlexible : GameSystemBase, IModSystem
    {
        private EntityQuery query;
        protected override void Initialise()
        {
            base.Initialise();
            query = GetEntityQuery(new QueryHelper().All(typeof(CTakesDuration), typeof(CFlexibleContainer), typeof(CAppliesProcessToFlexible)));
        }

        protected override void OnUpdate()
        {
            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (!Require<CTakesDuration>(entity, out var duration) ||
                    !Require<CAppliesProcessToFlexible>(entity, out var process) ||
                    !Require<CFlexibleContainer>(entity, out var container) ||
                    !Require<CAppliance>(entity, out var applianceComp))
                    continue;

                if (!(duration.Remaining <= 0f && duration.Active) ||
                    !GameData.Main.TryGet<Appliance>(applianceComp.ID, out var appliance))
                    continue;

                List<(int item, ItemList components)> convertedItems = new();
                for (int i = 0; i < container.Items.Count; i++)
                {
                    var itemID = container.Items.GetItem(i);
                    var componentIDs = container.Items[i];

                    if (!GameData.Main.TryGet<Item>(itemID, out var item))
                        continue;

                    var itemProcess = item.DerivedProcesses.Find(IP => appliance.Processes.Any(AP => AP.Process.ID == IP.Process.ID));
                    if (itemProcess.Equals(default(Item.ItemProcess)))
                    {
                        convertedItems.Add((itemID, componentIDs));
                        continue;
                    }

                    convertedItems.Add((itemProcess.Result.ID, new ItemList(itemProcess.Result.ID)));
                }

                container.Items = new();
                foreach (var itemSet in convertedItems)
                    container.Items.Add(itemSet.item, itemSet.components);

                Set(entity, container);
            }

        }
    }
}
