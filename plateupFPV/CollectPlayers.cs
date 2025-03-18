using Kitchen;
using KitchenMods;
using Unity.Entities;
using Unity.Collections;

namespace FirstPersonView
{
    public class CollectPlayers : GenericSystemBase, IModSystem
    {
        private EntityQuery PlayerQuery;

        protected override void Initialise()
        {
            base.Initialise();

            PlayerQuery = GetEntityQuery(new QueryHelper()
                .All(
                    typeof(CPlayer),
                    typeof(CPosition))
                .None(
                    typeof(CFirstPersonPlayer)));
        }

        protected override void OnUpdate()
        {
            using var players = PlayerQuery.ToEntityArray(Allocator.TempJob);

            for (int i = 0; i < players.Length; i++)
            {
                Set(players[i], new CFirstPersonPlayer()
                {
                    IsFirstPerson = false,
                    IsInitialised = false
                });
            }
        }
    }
}
