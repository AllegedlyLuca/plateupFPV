using Kitchen;
using KitchenMods;
using Unity.Entities;
using Unity.Collections;

namespace FirstPersonView
{
    public class ManagePlayerFirstPersonView : GenericSystemBase, IModSystem
    {
        private EntityQuery playerQuery;

        protected override void Initialise()
        {
            base.Initialise();

            playerQuery = GetEntityQuery(new QueryHelper()
                .All(
                    typeof(CPlayer),
                    typeof(CPosition))
                .None(
                    typeof(CFirstPersonPlayer)));
        }

        protected override void OnUpdate()
        {
            using var players = playerQuery.ToEntityArray(Allocator.TempJob);

            for (int i = 0; i < players.Length; i++)
            {
                Set(players[i], new CFirstPersonPlayer()
                {
                    IsActive = false,
                    IsInitialised = false
                });
            }
        }
    }
}
