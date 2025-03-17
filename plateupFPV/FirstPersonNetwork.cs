//using Controllers;
//using Kitchen;
//using KitchenMods;
//using MessagePack;
//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using Unity.Collections;
//using Unity.Entities;
//using UnityEngine;
//using UnityEngine.InputSystem;

//namespace KitchenFirstPersonView
//{
//    public class FirstPersonNetwork : UpdatableObjectView<FirstPersonNetwork.ViewData>, ISpecificViewResponse
//    {
//        // Cached callback to send data back to host.
//        // First parameter is the ResponseData instance
//        // Second parameter is typeof(ResponseData). This is used to identify the view system that will handle the response
//        // Callback is initialized after the first ViewData is received
//        private Action<IResponseData, Type> Callback;
//        private ViewData Data;

//        internal static EntityQuery Popups;
//        private static QueryHelper PopupTypes = new QueryHelper().Any(typeof(CPopup), typeof(CGenericChoicePopup), typeof(CPopupRecipe));

//        /// <summary>
//        /// This is used when you are the host of a lobby.  This handles player positioning and movement within your game world.
//        /// </summary>
//        public class UpdateView : ResponsiveViewSystemBase<ViewData, ResponseData>, IModSystem
//        {
//            public static UpdateView Instance { get; private set; }

//            EntityQuery Query;

//            protected override void Initialise()
//            {
//                FPVLogger.Debug("Initialising new FirstPersonCameraView instance.");
//                base.Initialise();
//                Popups = GetEntityQuery(PopupTypes);
//                Query = GetEntityQuery(typeof(CLinkedView), typeof(CFirstPersonPlayer));
//            }

//            protected override void OnUpdate()
//            {
//                if (Query.IsEmpty)
//                    return;

//                using NativeArray<CLinkedView> LinkedViews = Query.ToComponentDataArray<CLinkedView>(Allocator.Temp);
//                using NativeArray<CFirstPersonPlayer> FirstPersonPlayerComponents = Query.ToComponentDataArray<CFirstPersonPlayer>(Allocator.Temp);
//                using NativeArray<CPlayer> PlayerComponents = Query.ToComponentDataArray<CPlayer>(Allocator.Temp);
//                using var ents = Query.ToEntityArray(Allocator.Temp);

//                using NativeArray<CInputData> inputDataComponent = Query.ToComponentDataArray<CInputData>(Allocator.Temp);

//                Popups = GetEntityQuery(PopupTypes);

//                for (var i = 0; i < LinkedViews.Length; i++)
//                {
//                    bool IsActive = Main.IsFirstPersonViewEnabled();
//                    CFirstPersonPlayer cFirstPersonPlayer = FirstPersonPlayerComponents[i];
//                    cFirstPersonPlayer.IsActive = IsActive;
//                    Set(ents[i], cFirstPersonPlayer);

//                    bool IsShowingPopup = Popups.ToEntityArray(Allocator.Temp).Length != 0;
//                    if (IsShowingPopup)
//                    {
//                        FPVLogger.Debug("Popup! count: " + (Popups.ToEntityArray(Allocator.Temp).Length != 0));
//                    }

//                    SendUpdate(LinkedViews[i], new ViewData
//                    {
//                        IsActive = FirstPersonPlayerComponents[i].IsActive,
//                        IsInitialised = FirstPersonPlayerComponents[i].IsInitialised,
//                        Source = PlayerComponents[i].InputSource,
//                        Speed = PlayerComponents[i].Speed,
//                        PlayerID = PlayerComponents[i].ID,
//                        IsInMenu = (inputDataComponent[i].State.Request == GameStateRequest.InLocalMenu),
//                        IsShowingPopup = IsShowingPopup
//                    });
//                }

//                foreach (CLinkedView view in LinkedViews)
//                {
//                    //SendUpdate(view, new ViewData { IsActive = components[0].IsActive, IsInitialised = components[0].IsInitialised, Source = playerComponent[0].InputSource, LookSensitivity = 5.0f, Speed = playerComponent[0].Speed });

//                    // protected bool ApplyUpdates(ViewIdentifier identifier, Action<TResp> act, bool only_final_update = false)
//                    // As this is a subview, identifier refers to the main view identifier
//                    // act is performed for each ResponseData packet received
//                    // only_final_update makes act only performed for the latest packet. The rest are ignored.
//                    // Set only_final_update to false if you need something to happen for every packet sent, in the event more than 1 packet is received this frame
//                    if (ApplyUpdates(view.Identifier, PerformUpdateWithResponse, only_final_update: true))
//                    {
//                        // Do something if at least one ResponseData packet was processed this frame for the specified view
//                        //FPVLogger.Info("Received some data!");
//                    }
//                }
//            }

//            private void PerformUpdateWithResponse(ResponseData data)
//            {
//                if (data == null)
//                    return;

//                using NativeArray<CLinkedView> LinkedViews = Query.ToComponentDataArray<CLinkedView>(Allocator.Temp);
//                using NativeArray<CFirstPersonPlayer> FppComponents = Query.ToComponentDataArray<CFirstPersonPlayer>(Allocator.Temp);
//                using NativeArray<CPlayer> PlayerComponents = Query.ToComponentDataArray<CPlayer>(Allocator.Temp);
//                using NativeArray<Entity> Entities = Query.ToEntityArray(Allocator.Temp);

//                // When Camera is initialised in UpdateData, this is called in callback and sets the component
//                for (int i = 0; i < PlayerComponents.Length; i++)
//                {
//                    if (!IsSourceMySource(data.Source))
//                    {
//                        continue;
//                    }

//                    Entity SelectedEntity = Entities[i];
//                    CFirstPersonPlayer FirstPersonPerspective = FppComponents[i];

//                    FirstPersonPerspective.IsInitialised = data.IsInitialised;
//                    FirstPersonPerspective.IsActive = data.IsActive;
//                    Set(SelectedEntity, FirstPersonPerspective);
//                    break;
//                }
//            }
//        }

//        /// <summary>
//        /// This is run for each client, and performs initial setup and creating the component.
//        /// </summary>
//        [MessagePackObject(false)]
//        public class ViewData : ISpecificViewData, IViewData.ICheckForChanges<ViewData>
//        {
//            [Key(0)] public int Source;
//            [Key(1)] public bool IsInitialised;
//            [Key(2)] public bool IsActive;
//            [Key(3)] public float Speed;
//            [Key(4)] public int PlayerID;
//            [Key(5)] public bool IsInMenu;
//            [Key(6)] public bool IsShowingPopup;

//            public IUpdatableObject GetRelevantSubview(IObjectView view)
//            {
//                if (view == null)
//                {
//                    FPVLogger.Error("View is null on GetRelevantSubview!  This should not happen!");
//                }

//                GameObject GameObjectVar = view.GameObject;

//                if (GameObjectVar == null)
//                {
//                    FPVLogger.Error("view.GameObject does not exist.");
//                    return null;
//                }

//                if (!GameObjectVar.GetComponent<FirstPersonPlayerView>())
//                {
//                    GameObjectVar.AddComponent<FirstPersonPlayerView>();
//                    FPVLogger.Debug("Added FirstPersonPlayerView.");
//                }

//                return view.GetSubView<FirstPersonPlayerView>();
//            }

//            public bool IsChangedFrom(ViewData check)
//            {
//                return IsActive != check.IsActive || IsInitialised != check.IsInitialised || Source != check.Source || Speed != check.Speed || IsInMenu != check.IsInMenu || IsShowingPopup != check.IsShowingPopup;
//            }
//        }

//        // Definition of Message Packet that will be sent back to host via a callback
//        // This should contain the minimum amount of data necessary to perform the view's function.
//        // You MUST mark your ViewData as MessagePackObject
//        // If you don't, the game will run locally but fail in multiplayer
//        [MessagePackObject(false)]
//        public class ResponseData : IResponseData, IViewResponseData
//        {
//            [Key(0)] public bool IsActive;
//            [Key(1)] public bool IsInitialised;
//            [Key(2)] public int Source;
//        }

//        /// <summary>
//        /// Exact purpose of this not yet determined.  This will be updated.
//        /// </summary>
//        /// <param name="data"></param>
//        protected override void UpdateData(ViewData data)
//        {
//            //if(Data != null && Data.PlayerID != Main.PlayerID)
//            //{
//            //    return;
//            //}

//            this.Data = data;

//            FPVLogger.Debug("Main.PlayerID, data.PlayerID, data.IsInitialised: " + Main.PlayerID + ", " + data.PlayerID + ", " + data.IsInitialised);

//            if (!data.IsInitialised && Callback != null && Main.PlayerID == 0)
//            {
//                FPVLogger.Info("Initialising camera.");
//                Callback.Invoke(new ResponseData
//                {
//                    IsInitialised = true,
//                    IsActive = data.IsActive,
//                    Source = data.Source,
//                }, typeof(ResponseData));

//                // Camera and control setup
//                FirstPersonPlayerView.RunConfigure();
//                FPVLogger.Info("Initialisation of camera complete.");
//            }
//        }

//        // This is automatically called after each UpdateData call
//        // Hence, this is when Callback is initialized
//        public void SetCallback(Action<IResponseData, Type> callback)
//        {
//            // Cache callback to send data back to host.
//            Callback = callback;
//        }
//    }
//}
