using Kitchen;
using KitchenMods;
using MessagePack;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KitchenFirstPersonView 
{
    public class IndicatorLookAtPlayerView : UpdatableObjectView<IndicatorLookAtPlayerView.IndicatorViewData>
    {
        public class MyViewSystemBase : IncrementalViewSystemBase<IndicatorViewData>, IModSystem
        {
            private EntityQuery EntityQuery;
            private EntityQuery PlayerQuery;

            protected override void Initialise()
            {
                base.Initialise();
                EntityQuery = GetEntityQuery(new QueryHelper().All(typeof(CFirstPersonIndicator), typeof(CLinkedView)));
                PlayerQuery = GetEntityQuery(new QueryHelper().All(typeof(CPlayer), typeof(CPosition)));
            }

            protected override void OnUpdate()
            {
                if (EntityQuery.IsEmpty) return;

                using NativeArray<CLinkedView> NativeArray = EntityQuery.ToComponentDataArray<CLinkedView>(Allocator.Temp);
                using var Components = PlayerQuery.ToComponentDataArray<CPosition>(Allocator.Temp);

                for (int i = 0; i < NativeArray.Length; i++)
                {
                    SendUpdate(NativeArray[i], new IndicatorViewData { PlayerPosition = Components[0].Position });
                }
            }
        }

        [MessagePackObject]
        public struct IndicatorViewData : ISpecificViewData, IViewData, IViewResponseData, IViewData.ICheckForChanges<IndicatorViewData>
        {
            [Key(0)] public Vector3 PlayerPosition;
            [Key(1)] public Quaternion OriginalRotation;
            [Key(2)] public bool HasOriginalRotation;

            public IUpdatableObject GetRelevantSubview(IObjectView view)
            {
                return view.GameObject.GetComponent<IndicatorLookAtPlayerView>() != null ? view.GameObject.GetComponent<IndicatorLookAtPlayerView>() : view.GameObject.AddComponent<IndicatorLookAtPlayerView>();
            }

            public bool IsChangedFrom(IndicatorViewData check)
            {
                return PlayerPosition.x != check.PlayerPosition.x || PlayerPosition.y != check.PlayerPosition.y || PlayerPosition.z != check.PlayerPosition.z;
            }
        }

        protected override void UpdateData(IndicatorViewData data)
        {
            // TODO: Get items to actually float above their sources, not a random spot in the sky.
            foreach (Transform child in transform)
            {
                if (!data.HasOriginalRotation)
                {
                    data.OriginalRotation = child.rotation;
                    data.HasOriginalRotation = true;
                }

                if (PreferenceHandler.GetFirstPersonStateSetting())
                {
                    child.LookAt(data.PlayerPosition);
                    child.Rotate(Vector3.right, 80f);
                    child.Rotate(Vector3.up, 180f);
                }
                else
                {
                    child.rotation = data.OriginalRotation;
                }
            }
        }
    }
}
