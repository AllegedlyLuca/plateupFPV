using KitchenFirstPersonView;
using Kitchen;
using KitchenMods;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace KitchenFirstPersonView 
{
    public class IndicatorLookAtPlayerView : UpdatableObjectView<IndicatorLookAtPlayerView.MyViewData>
    {
        public class MyViewSystemBase : IncrementalViewSystemBase<MyViewData>, IModSystem
        {
            private EntityQuery _myEntityQuery;
            private EntityQuery _playerQuery;

            protected override void Initialise()
            {
                base.Initialise();
                _myEntityQuery = GetEntityQuery(new QueryHelper().All(typeof(CFirstPersonIndicator), typeof(CLinkedView)));
                _playerQuery = GetEntityQuery(new QueryHelper().All(typeof(CPlayer), typeof(CPosition)));
            }

            protected override void OnUpdate()
            {
                if (_myEntityQuery.IsEmpty) return;

                using NativeArray<CLinkedView> nativeArray = _myEntityQuery.ToComponentDataArray<CLinkedView>(Allocator.Temp);
                using var components = _playerQuery.ToComponentDataArray<CPosition>(Allocator.Temp);


                for (int i = 0; i < nativeArray.Length; i++)
                {
                    SendUpdate(nativeArray[i], new MyViewData { PlayerPosition = components[0].Position });
                }
            }
        }

        [MessagePackObject]
        public struct MyViewData : ISpecificViewData, IViewData, IViewResponseData, IViewData.ICheckForChanges<MyViewData>
        {
            [Key(0)] public Vector3 PlayerPosition;
            //[Key(1)] public bool IsFirstPerson;

            public IUpdatableObject GetRelevantSubview(IObjectView view)
            {
                return view.GameObject.AddComponent<IndicatorLookAtPlayerView>();
                //if this view component is already on one of your prefabs from your asset bundle, you can just use the built in method
                // return view.GetSubView<MyView>();
            }

            public bool IsChangedFrom(MyViewData check)
            {
                return PlayerPosition.x != check.PlayerPosition.x || PlayerPosition.y != check.PlayerPosition.y || PlayerPosition.z != check.PlayerPosition.z;
            }
        }

        protected override void UpdateData(MyViewData data)
        {
            //this method lets you manipulate the gameobject 
            
            foreach (Transform child in transform)
            {
                //Quaternion originalRotation = child.rotation;


                //if (data.IsFirstPerson)
                //{
                //    originalRotation = child.rotation;
                child.LookAt(data.PlayerPosition);
                child.Rotate(Vector3.right, -90);

                   
                //}
                //else
                //{
                //    child.rotation = originalRotation;
                //}
            }
        }
    }
}
