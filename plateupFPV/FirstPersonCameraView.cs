using Controllers;
using Kitchen;
using KitchenMods;
using MessagePack;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;
using UnityEngine;

namespace KitchenFirstPersonView
{
    public struct CFirstPersonPlayer : IModComponent
    {
        public bool IsActive;
        public bool IsInitialised;
    }

    public struct SPlayerToToggle : IComponentData
    {
        public int PlayerToToggle;
    }

    public class FirstPersonPlayerView : UpdatableObjectView<FirstPersonPlayerView.ViewData>, ISpecificViewResponse
    {
        // Paths for player models.
        private const string PLAYER_MODEL_PATH = "MorphmanPlus/Body";
        private const string COSMETICS_PATH = "Cosmetics";
        private const string ITEM_HOLDPOINT_PATH = "MorphmanPlus/Hold Points/Item Hold Point";
        private const string HOLDPOINTS_PATH = "MorphmanPlus/Hold Points";

        // Cached callback to send data back to host.
        // First parameter is the ResponseData instance
        // Second parameter is typeof(ResponseData). This is used to identify the view system that will handle the response
        // Callback is initialized after the first ViewData is received
        private Action<IResponseData, Type> Callback;

        private GameObject FirstPersonCameraGameObject = null;
        private ViewData Data;

        List<InputAction> movementAndLookActions = new List<InputAction>();
        private InputAction LookAction;
        private InputAction MoveAction;
        private float xRotation = 0f;

        // TODO: Figure why this is hard-coded.
        private KeyControl ToggleFirstPersonCameraKey = Keyboard.current.f5Key;

        // TODO: Work out what this is for.
        private static readonly int NightFade = Shader.PropertyToID("_NightFade");

        internal static EntityQuery CranePlayers;

        public class UpdateView : ResponsiveViewSystemBase<ViewData, ResponseData>, IModSystem
        {
            public static UpdateView Instance { get; private set; }

            EntityQuery Query;
            List<int> LocalInputSources;

            protected override void Initialise()
            {
                FPVLogger.DebugLog("Initialising new FirstPersonCameraView instance.");
                base.Initialise();
                Query = GetEntityQuery(typeof(CLinkedView), typeof(CFirstPersonPlayer));
                CranePlayers = GetEntityQuery(new QueryHelper().All(typeof(CIsCraneMode), typeof(CActivatingCraneMode), typeof(CPlayer)));
                LocalInputSources = new List<int>();
            }

            protected override void OnUpdate()
            {
                if (Query.IsEmpty)
                    return;

                using NativeArray<CLinkedView> LinkedViews = Query.ToComponentDataArray<CLinkedView>(Allocator.Temp);
                using NativeArray<CFirstPersonPlayer> FirstPersonPlayerComponents = Query.ToComponentDataArray<CFirstPersonPlayer>(Allocator.Temp);
                using NativeArray<CPlayer> PlayerComponents = Query.ToComponentDataArray<CPlayer>(Allocator.Temp);
                using var ents = Query.ToEntityArray(Allocator.Temp);

                using NativeArray<CInputData> inputDataComponent = Query.ToComponentDataArray<CInputData>(Allocator.Temp);

                for (var i = 0; i < LinkedViews.Length; i++)
                {
                    if (TryGetSingleton(out SPlayerToToggle sPlayerToToggle))
                    {
                        if (sPlayerToToggle.PlayerToToggle == PlayerComponents[i].ID)
                        {
                            var FppComponents = FirstPersonPlayerComponents[i];

                            FppComponents.IsActive = !FppComponents.IsActive;
                            Set(ents[i], sPlayerToToggle);

                            EntityManager.DestroyEntity(GetSingletonEntity<SPlayerToToggle>());
                        }
                    }

                    if (PlayerComponents[i].InputSource == InputSourceIdentifier.Identifier && !LocalInputSources.Contains(PlayerComponents[i].InputSource))
                    {
                        LocalInputSources.Add(PlayerComponents[i].InputSource);
                    }

                    bool IsActive = Main.PrefManager.Get<bool>(Main.PreferenceIdFirstPersonViewEnabled);
                    CFirstPersonPlayer cFirstPersonPlayer = FirstPersonPlayerComponents[i];
                    cFirstPersonPlayer.IsActive = IsActive;
                    Set(ents[i], cFirstPersonPlayer);

                    SendUpdate(LinkedViews[i], new ViewData
                    {
                        IsActive = FirstPersonPlayerComponents[i].IsActive,
                        IsInitialised = FirstPersonPlayerComponents[i].IsInitialised,
                        Source = PlayerComponents[i].InputSource,
                        Speed = PlayerComponents[i].Speed,
                        PlayerID = PlayerComponents[0].ID,
                        IsInMenu = (inputDataComponent[i].State.Request == GameStateRequest.InLocalMenu)
                    });
                }

                foreach (CLinkedView view in LinkedViews)
                {
                    //SendUpdate(view, new ViewData { IsActive = components[0].IsActive, IsInitialised = components[0].IsInitialised, Source = playerComponent[0].InputSource, LookSensitivity = 5.0f, Speed = playerComponent[0].Speed });

                    // protected bool ApplyUpdates(ViewIdentifier identifier, Action<TResp> act, bool only_final_update = false)
                    // As this is a subview, identifier refers to the main view identifier
                    // act is performed for each ResponseData packet received
                    // only_final_update makes act only performed for the latest packet. The rest are ignored.
                    // Set only_final_update to false if you need something to happen for every packet sent, in the event more than 1 packet is received this frame
                    if (ApplyUpdates(view.Identifier, PerformUpdateWithResponse, only_final_update: true))
                    {
                        // Do something if at least one ResponseData packet was processed this frame for the specified view
                        FPVLogger.Log("Received some data!");
                    }
                }

                CranePlayers = GetEntityQuery(new QueryHelper().All(typeof(CIsCraneMode), typeof(CActivatingCraneMode), typeof(CPlayer)));
            }

            private void PerformUpdateWithResponse(ResponseData data)
            {
                if (data == null)
                    return;

                using NativeArray<CLinkedView> LinkedViews = Query.ToComponentDataArray<CLinkedView>(Allocator.Temp);
                using NativeArray<CFirstPersonPlayer> FppComponents = Query.ToComponentDataArray<CFirstPersonPlayer>(Allocator.Temp);
                using NativeArray<CPlayer> PlayerComponents = Query.ToComponentDataArray<CPlayer>(Allocator.Temp);
                using NativeArray<Entity> Entities = Query.ToEntityArray(Allocator.Temp);

                // When Camera is initialised in UpdateData, this is called in callback and sets the component

                for (int i = 0; i < PlayerComponents.Length; i++)
                {
                    Entity SelectedEntity = Entities[i];
                    CPlayer player = PlayerComponents[i];
                    CFirstPersonPlayer FirstPersonPerspective = FppComponents[i];

                    if (player.InputSource != data.Source)
                        continue;

                    FirstPersonPerspective.IsInitialised = data.IsInitialised;
                    FirstPersonPerspective.IsActive = data.IsActive;
                    Set(SelectedEntity, FirstPersonPerspective);
                    break;
                }
            }

            public static void CreatePlayerToToggleSingleton(int playerID)
            {
                var sPlayerToToggle = new SPlayerToToggle()
                {
                    PlayerToToggle = playerID
                };
                Entity entity = Instance.Set(sPlayerToToggle);

                Instance.Set<CDoNotPersist>(entity);
            }
        }

        [MessagePackObject(false)]
        public class ViewData : ISpecificViewData, IViewData.ICheckForChanges<ViewData>
        {
            [Key(0)] public int Source;
            [Key(1)] public bool IsInitialised;
            [Key(2)] public bool IsActive;
            [Key(3)] public float Speed;
            [Key(4)] public int PlayerID;
            [Key(5)] public bool IsInMenu;

            public IUpdatableObject GetRelevantSubview(IObjectView view)
            {
                if (view == null)
                {
                    FPVLogger.Log(2, "View is null on GetRelevantSubview!  This should not happen!");
                }

                GameObject GameObjectVar = view.GameObject;

                if (GameObjectVar == null)
                {
                    FPVLogger.Log(2, "GameObject to add FirstPersonPlayerView subview does not exist.");
                    return null;
                }

                if (!GameObjectVar.GetComponent<FirstPersonPlayerView>())
                {
                    GameObjectVar.AddComponent<FirstPersonPlayerView>();
                    FPVLogger.DebugLog("Added FirstPersonPlayerView.");
                }

                return view.GetSubView<FirstPersonPlayerView>();
            }

            public bool IsChangedFrom(ViewData check)
            {
                return IsActive != check.IsActive || IsInitialised != check.IsInitialised || Source != check.Source || Speed != check.Speed || IsInMenu != check.IsInMenu;
            }
        }

        // Definition of Message Packet that will be sent back to host via a callback
        // This should contain the minimum amount of data necessary to perform the view's function.
        // You MUST mark your ViewData as MessagePackObject
        // If you don't, the game will run locally but fail in multiplayer
        [MessagePackObject(false)]
        public class ResponseData : IResponseData, IViewResponseData
        {
            [Key(0)] public bool IsActive;
            [Key(1)] public bool IsInitialised;
            [Key(2)] public int Source;
        }

        // This runs locally for each client, every frame
        public void Update()
        {
            if (Data == null)
                return;

            if (Data.Source != InputSourceIdentifier.Identifier)
                return;

            if (FirstPersonCameraGameObject == null)
            {
                FPVLogger.DebugLog("FirstPersonCameraGameObject is null, reinstancing.");
                ResetFirstPersonCameraInstance();
                SetCameraToFirstPerson(false, Data.PlayerID, Data);
                CheckControls(Data.PlayerID);
            }

            // If crane mode activated, disable first-person immediately.
            bool IsPlayerCrane = CranePlayers.ToEntityArray(Allocator.Temp).Length != 0;
            bool IsFirstPersonViewEnabled = Main.PrefManager.Get<bool>(Main.PreferenceIdFirstPersonViewEnabled);
            if (IsPlayerCrane && Main.PrefManager.Get<bool>(Main.PreferenceIdFirstPersonViewEnabled))
            {
                Main.PrefManager.Set<bool>(Main.PreferenceIdFirstPersonViewEnabled, false);
                SetCameraToFirstPerson(false, Data.PlayerID, Data);
                return;
            }

            // TOOD: Prevent changing into first-person if in crane mode, OR force crane mode to non-crane mode before transitioning.

            // Toggle Active in preferences and set player visibility
            if (ToggleFirstPersonCameraKey.wasPressedThisFrame && !IsPlayerCrane)
            {
                IsFirstPersonViewEnabled = !IsFirstPersonViewEnabled;
                Main.PrefManager.Set<bool>(Main.PreferenceIdFirstPersonViewEnabled, IsFirstPersonViewEnabled);
            }

            // If we're not in first person mode we don't need to do any of this.
            if (!IsFirstPersonViewEnabled && !ToggleFirstPersonCameraKey.wasPressedThisFrame)
                return;

            if (!Data.IsActive || Data.IsInMenu)
                return;

            #region Field of View
            int FieldOfView = Main.PrefManager.Get<int>(Main.PreferenceIdFieldOfView);

            SetupFirstPersonCamera();

            Camera FirstPersonViewCamera = FirstPersonCameraGameObject.GetComponent<Camera>();
            if (FirstPersonViewCamera != null)
            {
                if (FirstPersonViewCamera.fieldOfView != FieldOfView)
                {
                    FPVLogger.Log("Field of view setting changed.  Updating FPV FoV.");
                    FirstPersonViewCamera.fieldOfView = FieldOfView;
                }
            }
            else
            {
                FPVLogger.Log("FirstPersonViewCamera object null.");
            }
            #endregion

            #region Movement and looking controls.
            float moveSpeed = 3000f;
            CheckControls(Data.PlayerID);
            Vector2 movementDir = MoveAction.ReadValue<Vector2>().normalized;
            Vector3 move = transform.right * movementDir.x + transform.forward * movementDir.y;
            GetComponent<Rigidbody>().AddForce(move * moveSpeed * Data.Speed * Time.deltaTime);

            // Looking
            Vector2 looking = LookAction.ReadValue<Vector2>();

            float LookSensitivity = Main.PrefManager.Get<float>(Main.PreferenceIdLookSensitivity);
            float lookX = looking.x * LookSensitivity * Time.deltaTime;
            float lookY = looking.y * LookSensitivity * Time.deltaTime;

            xRotation -= lookY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            FirstPersonCameraGameObject.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * lookX);
            #endregion

            // Hold Point Rotation
            transform.Find(ITEM_HOLDPOINT_PATH).rotation = FirstPersonCameraGameObject.transform.Find("HoldPoint").rotation;
            transform.Find(ITEM_HOLDPOINT_PATH).position = FirstPersonCameraGameObject.transform.Find("HoldPoint").position;
        }

        /// <summary>
        /// Toggles between first-and-third person and handles camera related matters.
        /// </summary>
        /// <param name="data"></param>
        protected override void UpdateData(ViewData data)
        {
            this.Data = data;

            if (data.Source != InputSourceIdentifier.Identifier)
                return;

            if (!data.IsInitialised)
            {
                if (Callback != null)
                {
                    FPVLogger.DebugLog("Initialising camera.");
                    Callback.Invoke(new ResponseData
                    {
                        IsInitialised = true,
                        IsActive = data.IsActive,
                        Source = data.Source,
                    }, typeof(ResponseData));

                    // Camera Setup
                    SetupFirstPersonCamera();
                    CheckControls(data.PlayerID);
                    FPVLogger.DebugLog("Initialisation of camera complete.");
                }
            }

            // Anything below here requires the camera gameobject to not be null to be activated
            if (FirstPersonCameraGameObject == null)
                ResetFirstPersonCameraInstance();

            if (data.IsActive)
            {
                SetCameraToFirstPerson(true, data.PlayerID, data);
            }

            if (!data.IsActive || data.IsInMenu)
            {
                SetCameraToFirstPerson(false, data.PlayerID, data);
            }
        }

        private void SetPlayerModelVisibility(ViewData viewData)
        {
            SetPlayerModelVisibility(viewData, false);
        }

        private void SetPlayerModelVisibility(ViewData viewData, bool ForceModelToVisible)
        {
            if(transform.Find(PLAYER_MODEL_PATH) == null)
            {
                return;
            }
            
            bool IsFirstPersonViewEnabled = Main.PrefManager.Get<bool>(Main.PreferenceIdFirstPersonViewEnabled);
            bool IsPlayerModelVisiblePreference = Main.PrefManager.Get<bool>(Main.PreferenceIdIsPlayerModelVisible);
            bool IsPlayerCrane = CranePlayers.ToEntityArray(Allocator.Temp).Length != 0;
            bool ShouldShowModel = true;
            if(IsFirstPersonViewEnabled == true && IsPlayerModelVisiblePreference == false && Data.IsActive == true && Data.IsInMenu == false)
            {
                ShouldShowModel = false;
            }

            if (ForceModelToVisible == true || IsPlayerCrane)
                ShouldShowModel = true;

            if(IsPlayerCrane)
            {
                transform.Find(PLAYER_MODEL_PATH).gameObject.SetActive(false);
                transform.Find(COSMETICS_PATH).gameObject.SetActive(false);
            }

            bool IsPlayerModelCurrentlyVisible = transform.Find(PLAYER_MODEL_PATH).gameObject.activeSelf;
            bool ShouldChangeVisibility = transform.Find(PLAYER_MODEL_PATH).gameObject.activeSelf != ShouldShowModel;

            //FPVLogger.DebugLog("| \\/ IsPlayerModelCurrentlyVisible: " + IsPlayerModelCurrentlyVisible.ToString());
            //FPVLogger.DebugLog("|    ShouldChangeVisibility: " + ShouldChangeVisibility.ToString());
            //FPVLogger.DebugLog("|    ShouldShowModel: " + ShouldShowModel.ToString());
            //FPVLogger.DebugLog("|    -ForceModelToVisible: " + ForceModelToVisible.ToString());
            //FPVLogger.DebugLog("|    -IsPlayerCrane: " + IsPlayerCrane.ToString());
            //FPVLogger.DebugLog("|    -IsFirstPersonViewEnabled: " + IsFirstPersonViewEnabled.ToString());
            //FPVLogger.DebugLog("|    -IsPlayerModelVisiblePreference: " + IsPlayerModelVisiblePreference.ToString());
            //FPVLogger.DebugLog("|    -Data.IsActive: " + (Data.IsActive).ToString());
            //FPVLogger.DebugLog("| /\\ -Data.IsInMenu: " + (Data.IsInMenu).ToString());

            if (!ShouldChangeVisibility)
                return;

            if (ShouldShowModel)
            {
                //FPVLogger.DebugLog("|A^^ Changing player model visibility to " + ShouldShowModel.ToString());
                transform.Find(PLAYER_MODEL_PATH).gameObject.SetActive(true);
                transform.Find(COSMETICS_PATH).gameObject.SetActive(true);
            } else
            {
                //FPVLogger.DebugLog("|B^^ Changing player model visibility to " + ShouldShowModel.ToString());
                transform.Find(PLAYER_MODEL_PATH).gameObject.SetActive(false);
                transform.Find(COSMETICS_PATH).gameObject.SetActive(false);
            }
        }

        private void CheckControls(int PlayerID)
        {
            if (MoveAction == null || MoveAction.type == null)
            {
                ConfigureControls(PlayerID);
                SetupFirstPersonCamera();
            }
        }

        private void ConfigureControls(int PlayerID)
        {
            foreach (var action in InputSystem.ListEnabledActions())
            {
                if ((action.name == "Movement" || action.name == "Look") && !movementAndLookActions.Contains(action))
                {
                    movementAndLookActions.Add(action);
                }
            }

            LookAction = new InputAction("look", InputActionType.Value);
            MoveAction = new InputAction("move", InputActionType.Value);

            if (InputSourceIdentifier.DefaultInputSource.GetCurrentController(PlayerID) == ControllerType.Keyboard)
            {
                MoveAction.AddCompositeBinding("Dpad")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
                LookAction.AddBinding("<Mouse>/delta");
            }
            else
            {
                MoveAction.AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone(min=0.4,max=0.5)");
                LookAction.AddBinding("<Gamepad>/rightStick")
                    .WithProcessor("stickDeadzone(min=0.125,max=0.925)")
                    .WithProcessor("scaleVector2(x=50,y=50)");
            }
        }

        private void SetupFirstPersonCamera()
        {
            if (FirstPersonCameraGameObject == null)
            {
                ResetFirstPersonCameraInstance();
            }
            FirstPersonCameraGameObject.transform.parent = transform;
            Vector3 PlayerPositionAndRotation = new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z);
            FirstPersonCameraGameObject.transform.SetPositionAndRotation(PlayerPositionAndRotation, transform.rotation);
        }

        private void ResetFirstPersonCameraInstance()
        {
            FirstPersonCameraGameObject = Instantiate(Main.Bundle.LoadAsset<GameObject>("FPV Camera"));
        }

        private void SetCameraToFirstPerson(bool state, int PlayerID, ViewData viewData)
        {
            CheckControls(PlayerID);
            bool IsPlayerCrane = CranePlayers.ToEntityArray(Allocator.Temp).Length != 0;
            Players players = Players.Main;
            // Make it impossible to use first-person if in crane mode.
            FPVLogger.DebugLog("Scene type: " + Kitchen.GameInfo.CurrentScene.ToString());
            if ((players.Has(PlayerID) && IsPlayerCrane))
                state = false;

            if (state)
            {
                FPVLogger.DebugLog((state ? "Enabling" : "Disabling") + " first-person perspective state.");
                MoveAction.Enable();
                LookAction.Enable();
                foreach (var action in movementAndLookActions)
                {
                    action.Disable();
                }

                Cursor.lockState = CursorLockMode.Locked;

                Vector3 desiredHoldPointLocalPosition = new Vector3(0f, 0f, 0f);
                transform.Find(ITEM_HOLDPOINT_PATH).localPosition = desiredHoldPointLocalPosition;
                Main.FPVCounter++;
                FPVLogger.DebugLog("View loop count+: " + Main.FPVCounter);
                FPVLogger.DebugLog((state ? "Enabled" : "Disabled") + " first-person perspective state.");
            }
            else
            {
                while (Main.FPVCounter != 0)
                {
                    FPVLogger.DebugLog((state ? "Enabling" : "Disabling") + " first-person perspective state.");
                    MoveAction.Disable();
                    LookAction.Disable();
                    foreach (var action in movementAndLookActions)
                    {
                        action.Enable();
                    }

                    Cursor.lockState = CursorLockMode.None;

                    FPVLogger.DebugLog("transform.Find(ITEM_HOLDPOINT_PATH): " + transform.Find(ITEM_HOLDPOINT_PATH));

                    Vector3 origLocalPos = new Vector3(0f, 1.158f, 0.336f);
                    Quaternion origLocalRot = Quaternion.identity;
                    transform.Find(ITEM_HOLDPOINT_PATH).localPosition = origLocalPos;
                    transform.Find(ITEM_HOLDPOINT_PATH).localRotation = origLocalRot;
                    FPVLogger.DebugLog("View loop count-: " + Main.FPVCounter);
                    Main.FPVCounter--;
                    FPVLogger.DebugLog((state ? "Enabled" : "Disabled") + " first-person perspective state.");
                }
            }

            Cursor.visible = !state;
            SetPlayerModelVisibility(viewData);

            if (FirstPersonCameraGameObject == null)
                ResetFirstPersonCameraInstance();

            FirstPersonCameraGameObject.gameObject.SetActive(state);
        }

        // This is automatically called after each UpdateData call
        // Hence, this is when Callback is initialized
        public void SetCallback(Action<IResponseData, Type> callback)
        {
            // Cache callback to send data back to host.
            Callback = callback;
        }
    }
}
