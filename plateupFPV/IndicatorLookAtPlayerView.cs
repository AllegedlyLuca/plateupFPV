using Kitchen;
using KitchenMods;
using MessagePack;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KitchenFirstPersonView 
{
    public class IndicatorLookAtPlayerView : UpdatableObjectView<IndicatorLookAtPlayerView.MyViewData>
    {
        public class MyViewSystemBase : IncrementalViewSystemBase<MyViewData>, IModSystem
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
                    SendUpdate(NativeArray[i], new MyViewData { PlayerPosition = Components[0].Position });
                }
            }
        }

        [MessagePackObject]
        public struct MyViewData : ISpecificViewData, IViewData, IViewResponseData, IViewData.ICheckForChanges<MyViewData>
        {
            [Key(0)] public Vector3 PlayerPosition;

            public IUpdatableObject GetRelevantSubview(IObjectView view)
            {
                return view.GameObject.GetComponent<IndicatorLookAtPlayerView>() != null ? view.GameObject.GetComponent<IndicatorLookAtPlayerView>() : view.GameObject.AddComponent<IndicatorLookAtPlayerView>();
            }

            public bool IsChangedFrom(MyViewData check)
            {
                return PlayerPosition.x != check.PlayerPosition.x || PlayerPosition.y != check.PlayerPosition.y || PlayerPosition.z != check.PlayerPosition.z;
            }
        }

        protected override void UpdateData(MyViewData data)
        {
            // TODO: Get items to actually float above their sources, not a random spot in the sky.
            bool IsFirstPersonViewEnabled = Main.PrefManager.Get<bool>(Main.PreferenceIdFirstPersonViewEnabled);
            foreach (Transform child in transform)
            {
                Quaternion originalRotation = child.rotation;

                if (IsFirstPersonViewEnabled)
                {
                    originalRotation = child.rotation;
                    child.LookAt(data.PlayerPosition);
                    child.Rotate(Vector3.right, 80f);
                    child.Rotate(Vector3.up, 180f);
                }
                else
                {
                    child.rotation = originalRotation;
                }
            }
        }
    }
}
