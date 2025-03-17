using Kitchen;
using KitchenMods;
using Unity.Collections;
using Unity.Entities;

namespace FirstPersonView
{
    public struct CFirstPersonIndicator : IModComponent
    {
    }

    public class SetLookAtData : GenericSystemBase, IModSystem
    {
        private EntityQuery indicators;

        protected override void Initialise()
        {
            base.Initialise();
            indicators = GetEntityQuery(new QueryHelper()
                    .All(
                        typeof(CIndicator),
                        typeof(CPosition)
                    )
                    .Any(
                        typeof(CIndicator),
                        typeof(CProgressIndicator),
                        typeof(CCustomerIndicator),
                        typeof(CDecorationIndicator),
                        typeof(CTableSetIndicator)
                    )
                    .None(
                        typeof(CFirstPersonIndicator)
                    ));
        }
        
        protected override void OnUpdate()
        {
            var indicators = this.indicators.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < indicators.Length; i++)
            {
                Set(indicators[i], new CFirstPersonIndicator()
                {
                });
            }
            indicators.Dispose();
        }
    }
}
