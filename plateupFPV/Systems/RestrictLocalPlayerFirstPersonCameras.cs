using Kitchen;
using KitchenMods;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;

namespace KitchenFirstPersonView.Systems
{
    public class RestrictLocalPlayerFirstPersonCameras : GenericSystemBase, IModSystem
    {
        EntityQuery Query;
        List<int> inputSourceIDs;
        protected override void Initialise()
        {
            Query = GetEntityQuery(new QueryHelper()
                .All(
                    typeof(CFirstPersonPlayer)));

            inputSourceIDs = new List<int>();
        }
        protected override void OnUpdate()
        {
            using var Entities = Query.ToEntityArray(Allocator.Temp);
            using var FirstPersonPlayerComponentsArray = Query.ToComponentDataArray<CFirstPersonPlayer>(Allocator.Temp);
            using var PlayerComponents = Query.ToComponentDataArray<CPlayer>(Allocator.Temp);

            //Mod.LogWarning("These are players that share InputSource with at least one other player!");
            foreach (var playerItem in PlayerComponents
                .Select((item, i) => new {
                    Index = i,
                    Player = item
                })
                .GroupBy(group => group.Player.InputSource)
                .Where(group => group.Count() > 1)
                .SelectMany(group => group))
            {
                int index = playerItem.Index;
                Entity ent = Entities[index];
                CFirstPersonPlayer FirstPersonPlayerComponent = FirstPersonPlayerComponentsArray[index];

                FirstPersonPlayerComponent.IsActive = false;
                Set(ent, FirstPersonPlayerComponent);
            }

            /*foreach (var component in playerComponents)
            {
                if (!inputSourceIDs.Contains(component.InputSource))
                {
                    inputSourceIDs.Add(component.InputSource);
                }
            }

            IEnumerable<int> duplicates = inputSourceIDs.GroupBy(x => x)
                                            .SelectMany(g => g.Skip(1));

            for (int i = 0; i < ents.Length; i++)
            {
                var ent = ents[i];
                var firstPersonPlayerComponent = firstPersonPlayerComponents[i];
                var playerComponent = playerComponents[i];

                if (duplicates.Contains(InputSourceIdentifier.Identifier) && playerComponent.InputSource == InputSourceIdentifier.Identifier)
                {
                    firstPersonPlayerComponent.IsActive = false;
                    Set(ent, firstPersonPlayerComponent);
                }
            }*/
        }
    }
}
