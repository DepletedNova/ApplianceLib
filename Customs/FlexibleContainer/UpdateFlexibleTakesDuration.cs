using ApplianceLib.Api;
using Kitchen;
using KitchenData;
using KitchenLib.Utils;
using KitchenMods;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;

namespace ApplianceLib.Customs
{
    [UpdateInGroup(typeof(DurationLocks))]
    internal class UpdateFlexibleTakesDuration : GameSystemBase, IModSystem
    {
        private EntityQuery query;
        protected override void Initialise()
        {
            base.Initialise();
            query = GetEntityQuery(new QueryHelper().All(
                typeof(CAppliance),
                typeof(CTakesDuration),
                typeof(CFlexibleContainer),
                typeof(CAppliesProcessToFlexible)
            ));
        }

        protected override void OnUpdate()
        {
            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (!Require<CTakesDuration>(entity, out var duration)
                    || !Require<CAppliesProcessToFlexible>(entity, out var processModifiers)
                    || !Require<CFlexibleContainer>(entity, out var container)
                    || !Require<CAppliance>(entity, out var cAppliance)
                    || !GameData.Main.TryGet<Appliance>(cAppliance.ID, out var appliance)
                    || processModifiers.ProcessType == FlexibleProcessType.UseTakesDurationTime)
                {
                    continue;
                }

                // Gather process speed modifiers
                Dictionary<int, float> processSpeedModifiers = new();
                Dictionary<int, float> processBadSpeedModifiers = new();
                if (Has<CAffectedBy.Marker>(entity))
                {
                    foreach (CAffectedBy cAffected in GetBuffer<CAffectedBy>(entity))
                    {
                        if (Require(cAffected, out CAppliesEffect cApplies) && cApplies.IsActive && Require(cAffected, out CApplianceSpeedModifier cSpeedMod))
                        {
                            var index = cSpeedMod.AffectsAllProcesses ? 1 : cSpeedMod.Process;

                            if (!processSpeedModifiers.ContainsKey(index))
                                processSpeedModifiers[index] = 1f;
                            processSpeedModifiers[index] *= 1f + cSpeedMod.Speed;

                            if (!processBadSpeedModifiers.ContainsKey(index))
                                processBadSpeedModifiers[index] = 1f;
                            processBadSpeedModifiers[index] *= 1f + cSpeedMod.BadSpeed;
                        }
                    }
                }

                // Displayed Duration updating beginnings
                var hasVisibleDuration = Require(entity, out CDisplayDuration cDisplayed);
                int collectedBad = 0;
                Dictionary<int, int> collectedProcesses = new();

                var ignoresBad = Has<CNoBadProcesses>(entity);

                var oldTotal = duration.Total;
                List<float> totalProcessTime = new();
                foreach (var itemID in container.Items.GetItems())
                {
                    if (!GameData.Main.TryGet<Item>(itemID, out var item))
                        continue;

                    var itemProcess = item.DerivedProcesses.Find(ip => appliance.Processes.Exists(ap => ip.Process.ID == ap.Process.ID));
                    if (itemProcess.Equals(default(Item.ItemProcess)))
                        continue;

                    // Ignore bad processes if need be
                    if (itemProcess.IsBad && ignoresBad)
                        continue;

                    var applianceProcess = appliance.Processes.Find(ap => ap.Process.ID == itemProcess.Process.ID);

                    var modifier = itemProcess.IsBad ? (processBadSpeedModifiers.ContainsKey(1) ? processBadSpeedModifiers[1] : 1f) : (processSpeedModifiers.ContainsKey(1) ? processSpeedModifiers[1] : 1f);
                    if (itemProcess.IsBad && processBadSpeedModifiers.ContainsKey(applianceProcess.Process.ID)) modifier *= processBadSpeedModifiers[applianceProcess.Process.ID];
                    else if (!itemProcess.IsBad && processBadSpeedModifiers.ContainsKey(applianceProcess.Process.ID)) modifier *= processSpeedModifiers[applianceProcess.Process.ID];

                    totalProcessTime.Add(itemProcess.Duration * processModifiers.ProcessTimeMultiplier / applianceProcess.Speed / modifier);

                    // Gather the total amount of processes for updating the visible duration
                    if (hasVisibleDuration)
                    {
                        if (itemProcess.IsBad) collectedBad++;
                        if (!collectedProcesses.ContainsKey(applianceProcess.Process.ID)) collectedProcesses.Add(applianceProcess.Process.ID, 1);
                        collectedProcesses[applianceProcess.Process.ID]++;
                    }
                }

                var preferredTotal = processModifiers.ProcessType switch
                {
                    FlexibleProcessType.Additive => totalProcessTime.Sum(),
                    FlexibleProcessType.Average => totalProcessTime.Count == 0 ? 0 : totalProcessTime.Average(),
                    _ => oldTotal,
                };
                duration.IsLocked = duration.IsLocked || (container.Maximum > 0 && container.Items.Count < processModifiers.MinimumItems) || totalProcessTime.Count == 0;
                var newTotal = Math.Max(processModifiers.MinimumProcessTime, preferredTotal);
                if (!duration.IsLocked && newTotal != oldTotal)
                {
                    duration.Total = newTotal;
                    duration.Remaining += newTotal - oldTotal;
                }
                Set(entity, duration);

                // Update displayed duration based on majority
                if (hasVisibleDuration && !duration.IsLocked && !collectedProcesses.IsNullOrEmpty())
                {
                    cDisplayed.IsBad = collectedBad >= totalProcessTime.Count / 2f;

                    var found = false;
                    foreach (var processDictPair in collectedProcesses)
                    {
                        if (processDictPair.Value >= totalProcessTime.Count / 2f)
                        {
                            cDisplayed.Process = processDictPair.Key;
                            found = true;
                            break;
                        }
                    }
                    if (!found) cDisplayed.Process = collectedProcesses.First().Key;

                    Set(entity, cDisplayed);
                }
            }
        }
    }
}
