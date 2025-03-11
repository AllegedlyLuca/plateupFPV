using Kitchen;
using KitchenMods;
using Unity.Collections;
using Unity.Entities;

namespace KitchenFirstPersonView
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
                    .Any(
                        typeof(CIndicator)
                        ,typeof(CPopup)
                        ,typeof(CCardPedestal)
                        ,typeof(CCardSetBubble)
                        ,typeof(CNewsCards)
                        ,typeof(CCardPedestal)
                        ,typeof(CProgressIndicator)
                        ,typeof(CCustomerIndicator)
                        ,typeof(CDecorationIndicator)
                        ,typeof(CTableSetIndicator)
                        //,typeof(CEventIndicator)
                        //typeof(CFranchiseKitchenIndicator)
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
