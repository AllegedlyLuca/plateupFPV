using Controllers;
using Kitchen;
using KitchenMods;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FirstPersonView
{
    public struct CFirstPersonPlayer : IModComponent
    {
        public bool IsFirstPerson;
        public bool IsInitialised;
    }

    public struct SPlayerToToggle : IComponentData
    {
        public int PlayerToToggle;
    }

    public class ManageFirstPersonView : UpdatableObjectView<ManageFirstPersonView.ViewData>, ISpecificViewResponse
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
        private ViewData Data;

        private bool IsCameraFirstPerson = false;
        private bool CameraHasBeenSetup = false;
        private bool IsPlayerMoving = false;
        private bool FPVDisabledViaMenu = false;

        private float CurrentLookVertical = 0f;

        // TODO: Work out what this is for.
        private readonly int NightFade = Shader.PropertyToID("_NightFade");

        [SerializeField]
        private Animator FirstPersonAnimator;
        private int AnimatorMovementKey = Animator.StringToHash("MovementSpeed");

        internal static EntityQuery CranePlayers;
        internal static EntityQuery Popups;

        private static QueryHelper PopupTypes = new QueryHelper().Any(typeof(CPopup), typeof(CGenericChoicePopup), typeof(CPopupRecipe));
        private static QueryHelper CraneTypes = new QueryHelper().All(typeof(CPlayer), typeof(CRequiresView), typeof(CIsCraneMode));

        #region Network components
        /// <summary>
        /// This is used when you are the host of a lobby.  This handles player positioning and movement within your game world.
        /// </summary>
        public class UpdateView : ResponsiveViewSystemBase<ViewData, ResponseData>, IModSystem
        {
            public static UpdateView Instance { get; private set; }

            EntityQuery FirstPersonPlayersQuery;

            protected override void Initialise()
            {
                FPVLogger.Debug("Initialising new FirstPersonCameraView instance.");
                base.Initialise();
                Popups = GetEntityQuery(PopupTypes);
                CranePlayers = GetEntityQuery(CraneTypes);
                FirstPersonPlayersQuery = GetEntityQuery(typeof(CLinkedView), typeof(CFirstPersonPlayer));

            }

            protected override void OnUpdate()
            {
                if (FirstPersonPlayersQuery.IsEmpty)
                    return;

                using NativeArray<CLinkedView> LinkedViews = FirstPersonPlayersQuery.ToComponentDataArray<CLinkedView>(Allocator.Temp);
                using NativeArray<CFirstPersonPlayer> FirstPersonPlayerComponents = FirstPersonPlayersQuery.ToComponentDataArray<CFirstPersonPlayer>(Allocator.Temp);
                using NativeArray<CPlayer> PlayerComponents = FirstPersonPlayersQuery.ToComponentDataArray<CPlayer>(Allocator.Temp);
                using var ents = FirstPersonPlayersQuery.ToEntityArray(Allocator.Temp);

                using NativeArray<CInputData> inputDataComponent = FirstPersonPlayersQuery.ToComponentDataArray<CInputData>(Allocator.Temp);

                Popups = GetEntityQuery(PopupTypes);
                CranePlayers = GetEntityQuery(CraneTypes);

                for (var i = 0; i < LinkedViews.Length; i++)
                {
                    bool IsFirstPerson = PreferenceHandler.GetFirstPersonStateSetting();
                    CFirstPersonPlayer cFirstPersonPlayer = FirstPersonPlayerComponents[i];
                    cFirstPersonPlayer.IsFirstPerson = IsFirstPerson;
                    Set(ents[i], cFirstPersonPlayer);

                    bool IsShowingPopup = Popups.ToEntityArray(Allocator.Temp).Length != 0;

                    SendUpdate(LinkedViews[i], new ViewData
                    {
                        IsFirstPerson = FirstPersonPlayerComponents[i].IsFirstPerson,
                        IsInitialised = FirstPersonPlayerComponents[i].IsInitialised,
                        Source = PlayerComponents[i].InputSource,
                        Speed = PlayerComponents[i].Speed,
                        PlayerID = PlayerComponents[i].ID,
                        IsInMenu = (inputDataComponent[i].State.Request == GameStateRequest.InLocalMenu),
                        IsShowingPopup = IsShowingPopup
                    });
                }

                foreach (CLinkedView view in LinkedViews)
                {
                    //SendUpdate(view, new ViewData { IsFirstPerson = components[0].IsFirstPerson, IsInitialised = components[0].IsInitialised, Source = playerComponent[0].InputSource, LookSensitivity = 5.0f, Speed = playerComponent[0].Speed });

                    // protected bool ApplyUpdates(ViewIdentifier identifier, Action<TResp> act, bool only_final_update = false)
                    // As this is a subview, identifier refers to the main view identifier
                    // act is performed for each ResponseData packet received
                    // only_final_update makes act only performed for the latest packet. The rest are ignored.
                    // Set only_final_update to false if you need something to happen for every packet sent, in the event more than 1 packet is received this frame
                    if (ApplyUpdates(view.Identifier, PerformUpdateWithResponse, only_final_update: true))
                    {
                        // Do something if at least one ResponseData packet was processed this frame for the specified view
                        //FPVLogger.Info("Received some data!");
                    }
                }
            }

            private void PerformUpdateWithResponse(ResponseData data)
            {
                if (data == null)
                    return;

                using NativeArray<CLinkedView> LinkedViews = FirstPersonPlayersQuery.ToComponentDataArray<CLinkedView>(Allocator.Temp);
                using NativeArray<CFirstPersonPlayer> FppComponents = FirstPersonPlayersQuery.ToComponentDataArray<CFirstPersonPlayer>(Allocator.Temp);
                using NativeArray<CPlayer> PlayerComponents = FirstPersonPlayersQuery.ToComponentDataArray<CPlayer>(Allocator.Temp);
                using NativeArray<Entity> FirstPersonEntities = FirstPersonPlayersQuery.ToEntityArray(Allocator.Temp);

                // When Camera is initialised in UpdateData, this is called in callback and sets the component
                for (int i = 0; i < PlayerComponents.Length; i++)
                {
                    if (!IsSourceMySource(data.Source))
                    {
                        continue;
                    }

                    Entity SelectedEntity = FirstPersonEntities[i];
                    CFirstPersonPlayer FirstPersonPerspective = FppComponents[i];

                    FirstPersonPerspective.IsInitialised = data.IsInitialised;
                    FirstPersonPerspective.IsFirstPerson = data.IsFirstPerson;
                    Set(SelectedEntity, FirstPersonPerspective);
                    break;
                }
            }
        }

        /// <summary>
        /// This is run for each client, and performs initial setup and creating the component.
        /// </summary>
        [MessagePackObject(false)]
        public class ViewData : ISpecificViewData, IViewData.ICheckForChanges<ViewData>
        {
            [Key(0)] public int Source;
            [Key(1)] public bool IsInitialised;
            [Key(2)] public bool IsFirstPerson;
            [Key(3)] public float Speed;
            [Key(4)] public int PlayerID;
            [Key(5)] public bool IsInMenu;
            [Key(6)] public bool IsShowingPopup;

            public IUpdatableObject GetRelevantSubview(IObjectView view)
            {
                if (view == null)
                {
                    FPVLogger.Error("View is null on GetRelevantSubview!  This should not happen!");
                }

                GameObject GameObjectVar = view.GameObject;

                if (GameObjectVar == null)
                {
                    FPVLogger.Error("view.GameObject does not exist.");
                    return null;
                }

                if (!GameObjectVar.GetComponent<ManageFirstPersonView>())
                {
                    GameObjectVar.AddComponent<ManageFirstPersonView>();
                    FPVLogger.Debug("Added FirstPersonPlayerView.");
                }

                return view.GetSubView<ManageFirstPersonView>();
            }

            public bool IsChangedFrom(ViewData check)
            {
                return IsFirstPerson != check.IsFirstPerson || IsInitialised != check.IsInitialised || Source != check.Source || Speed != check.Speed || IsInMenu != check.IsInMenu || IsShowingPopup != check.IsShowingPopup;
            }
        }

        // Definition of Message Packet that will be sent back to host via a callback
        // This should contain the minimum amount of data necessary to perform the view's function.
        // You MUST mark your ViewData as MessagePackObject
        // If you don't, the game will run locally but fail in multiplayer
        [MessagePackObject(false)]
        public class ResponseData : IResponseData, IViewResponseData
        {
            [Key(0)] public bool IsFirstPerson;
            [Key(1)] public bool IsInitialised;
            [Key(2)] public int Source;
        }

        /// <summary>
        /// Exact purpose of this not yet determined.  This will be updated.
        /// </summary>
        /// <param name="data"></param>
        protected override void UpdateData(ViewData data)
        {
            //if(Data != null && Data.PlayerID != Main.PlayerID)
            //{
            //    return;
            //}

            this.Data = data;

            FPVLogger.Debug("Main.PlayerID, data.PlayerID, data.IsInitialised: " + Main.PlayerID + ", " + data.PlayerID + ", " + data.IsInitialised);

            if (!data.IsInitialised && Callback != null && Main.PlayerID == 0)
            {
                FPVLogger.Info("Initialising camera.");
                Callback.Invoke(new ResponseData
                {
                    IsInitialised = true,
                    IsFirstPerson = data.IsFirstPerson,
                    Source = data.Source,
                }, typeof(ResponseData));

                // Camera and control setup
                SetupFirstPersonCamera();
                ManageControls.CheckControlState();
                FPVLogger.Info("Initialisation of camera complete.");
            }
        }

        // This is automatically called after each UpdateData call
        // Hence, this is when Callback is initialized
        public void SetCallback(Action<IResponseData, Type> callback)
        {
            // Cache callback to send data back to host.
            Callback = callback;
        }
        #endregion


        private static bool IsSourceMySource(int Source)
        {
            int ControllerIdentifier = ManageControls.GetMyControllerIdentifier().Value;
            int PlayerSource = Main.PlayerSource;

            //FPVLogger.Debug("Source to check: " + Source);
            //FPVLogger.Debug("PlayerSource: " + PlayerSource);
            //FPVLogger.Debug("ControllerIdentifier: " + ControllerIdentifier);

            // This assumes the first source is always the local client.
            if (Main.PlayerSource == 0 && Source != 0)
            {
                Main.PlayerSource = Source;
                PlayerSource = Source;
                FPVLogger.Info("Setting Main.PlayerSource for to " + Source);
            }

            if (Source != ControllerIdentifier && PlayerSource != 0 && PlayerSource != Source)
            {
                return false;
            }

            return true;
        }

        // This runs locally for each client, every frame
        public void Update()
        {
            // If not my data.
            if(Data.PlayerID != Main.PlayerID)
            {
                return;
            }

            GameObject PauseMenuGameObject = GameObject.Find("Player Pause Popup");
            if(PauseMenuGameObject != null && PauseMenuGameObject.transform.childCount > 0 && PauseMenuGameObject.transform.GetChild(0).gameObject.activeSelf)
            {
                // TODO: Better identification of various user interfaces to auto-toggle FPV.
                //FPVLogger.Debug("Pause menu identified.");
            }

            if (Main.PlayerGameObject == null)
            {
                FPVLogger.Warn("FirstPersonPlayerGameObject is not set, attempting to set.");
                Main.PlayerGameObject = FindPlayerGameObject();
                if(Main.PlayerGameObject == null)
                {
                    FPVLogger.Error("FirstPersonPlayerGameObject cannot be found.");
                    return;
                }
            }

            #region Enable or disable FPV
            if (Main.FirstPersonCameraObject == null)
            {
                FPVLogger.Warn("FirstPersonCameraObject is null, reinstancing.");
                ResetFirstPersonCameraInstance();
                if (PreferenceHandler.GetFirstPersonStateSetting())
                {
                    FPVLogger.Debug("Enabling FPV, setting is read as true.");
                    EnableFirstPerson();
                }
                else
                {
                    FPVLogger.Debug("Disabling FPV, setting is read as false.");
                    DisableFirstPerson();
                }
                return;
            }

            int CranePlayerCount = CranePlayers.ToEntityArray(Allocator.Temp).Length;
            if(CranePlayerCount != 0)
            {
                FPVLogger.Debug("Crane count: " + CranePlayers.ToEntityArray(Allocator.Temp).Length);
            }

            if(Main.LocalPlayers.Count > 1)
            {
                if(PreferenceHandler.GetFirstPersonStateSetting())
                {
                    FPVLogger.Debug("Local players count is " + Main.LocalPlayers.Count);
                    FPVLogger.Warn("Disabling first person view for all players due to more than one local player being present.");
                    DisableFirstPerson();
                }
                return;
            }

            bool IsMenuOrPopup = Data.IsInMenu == true || Data.IsShowingPopup == true;
            if (IsMenuOrPopup)
            {
                if (IsCameraFirstPerson)
                {
                    FPVLogger.Debug("Game is paused or showing popup, disabling first person.");
                    FPVDisabledViaMenu = true;
                    DisableFirstPerson();
                }
                return;
            }

            if(FPVDisabledViaMenu && !IsMenuOrPopup) 
            {
                FPVLogger.Debug("Game has unpaused, returning to first person.");
                FPVDisabledViaMenu = false;
                EnableFirstPerson();
                return;
            }

            if (PreferenceHandler.GetFirstPersonStateSetting() && !IsCameraFirstPerson)
            {
                FPVLogger.Debug("FPV is set to enabled but camera state does not match.  Correcting.");
                SetCameraState(CameraState.FirstPerson);
                if(!ManageControls.AreControlsSetToFirstPerson())
                {
                    ManageControls.SetControlState(ControlState.FirstPerson);
                }
            }

            if (!PreferenceHandler.GetFirstPersonStateSetting() && IsCameraFirstPerson)
            {
                FPVLogger.Debug("FPV is set to disabled but camera state does not match.  Correcting.");
                SetCameraState(CameraState.ThirdPerson);
                if (ManageControls.AreControlsSetToFirstPerson())
                {
                    ManageControls.SetControlState(ControlState.ThirdPerson);
                }
            }

            bool IsPlayerCrane = CranePlayers.ToEntityArray(Allocator.Temp).Length != 0;
            if (IsPlayerCrane && PreferenceHandler.GetFirstPersonStateSetting())
            {
                FPVLogger.Debug("Crane mode has been set to true, forcefully disabling first person view.");
                DisableFirstPerson();
                return;
            }

            if (!IsPlayerCrane && ManageControls.WasCameraToggleKeyPressedThisFrame())
            {
                if (!PreferenceHandler.GetFirstPersonStateSetting())
                {
                    FPVLogger.Debug("Key pressed to toggle state and crane is not present.  Enabling first person view.");
                    EnableFirstPerson();
                }
                else
                {
                    FPVLogger.Debug("Key pressed to toggle state and crane is not present.  Disabling first person view.");
                    DisableFirstPerson();
                }
                return;
            }

            if (!PreferenceHandler.GetFirstPersonStateSetting() && !ManageControls.WasCameraToggleKeyPressedThisFrame())
            {
                return;
            }
            #endregion

            HandleFirstPersonFieldOfView();
            HandleFirstPersonLooking();
            HandleFirstPersonMovement();
            HandleFirstPersonAnimator();
            HandleFirstPersonHoldPoints();
            HandleFirstPersonSkybox();
            HandlePlayerModelVisibility();
        }

        /// <summary>
        /// Enables first person view.
        /// </summary>
        private void EnableFirstPerson()
        {
            FPVLogger.Info("Enabling first person view.");
            FPVLogger.Debug("<- Routing.");
            PreferenceHandler.SetFirstPersonStateSetting(CameraState.FirstPerson);
            SetCameraState(CameraState.FirstPerson);
            HandlePlayerModelVisibility();
            ManageControls.SetControlState(ControlState.FirstPerson);
            FPVLogger.Info("Enabled first person view.");
        }

        /// <summary>
        /// Disables first person view.
        /// </summary>
        private void DisableFirstPerson()
        {
            FPVLogger.Info("Disabling first person view.");
            FPVLogger.Debug("<- Routing.");
            PreferenceHandler.SetFirstPersonStateSetting(CameraState.ThirdPerson);
            SetCameraState(CameraState.ThirdPerson);
            HandlePlayerModelVisibility();
            ManageControls.SetControlState(ControlState.ThirdPerson);
            FPVLogger.Info("Disabled first person view.");
        }

        /// <summary>
        /// Handles the Field of View for the first person perspective.
        /// </summary>
        private void HandleFirstPersonFieldOfView()
        {
            UpdateCameraPosition();

            int FieldOfView = PreferenceHandler.GetFieldOfViewSetting(); // Main.PrefManager.Get<int>(Main.PreferenceIdFieldOfView);
            if (Main.FirstPersonCameraObject.fieldOfView != FieldOfView)
            {
                if(ManageControls.WasCameraToggleKeyPressedThisFrame())
                {
                    FPVLogger.Info("Setting first person field of view.");
                }
                else
                {
                    FPVLogger.Info("Field of view setting changed.  Updating FPV FoV.");
                }
                Main.FirstPersonCameraObject.fieldOfView = FieldOfView;
            }
        }
        
        /// <summary>
        /// Handles movement while in first person.
        /// </summary>
        private void HandleFirstPersonMovement()
        {
            Rigidbody MyBody = Main.PlayerGameObject.GetComponent<Rigidbody>();
            if(FirstPersonAnimator == null)
            {
                FirstPersonAnimator = Main.PlayerGameObject.GetComponent<Animator>();
            }
            float moveSpeed = 3000f;
            float MovementDeadzone = 0.5f;

            Vector3 MovementVector = new Vector3();
            Vector2 MovementDir = ManageControls.MoveAction[Main.PlayerID].ReadValue<Vector2>().normalized;
            bool flag = false;

            if(InputSourceIdentifier.DefaultInputSource.GetCurrentInputData(Main.PlayerID, out var input_state))
            {
                Transform PlayerPosition = Main.PlayerGameObject.transform;
                Vector3 MovementX = (PlayerPosition.right * MovementDir.x);
                Vector3 MovementY = (PlayerPosition.forward * MovementDir.y);
                MovementVector = MovementX + MovementY;
                flag = input_state.StopMoving == ButtonState.Held || input_state.StopMoving == ButtonState.Pressed;
            }

            Vector3 force = (MovementVector * moveSpeed) * (Data.Speed * Time.deltaTime);

            IsPlayerMoving = false;
            if (MovementVector != Vector3.zero)
            {
                if (MovementVector.magnitude > MovementDeadzone && !flag)
                {
                    IsPlayerMoving = true;
                    force.y = 0f;
                    MyBody.AddForce(force);
                }
            }
        }

        /// <summary>
        /// Attempts to handle first person animation.
        /// </summary>
        /// <param name="DisableAnimator">If set, forces this animator to be disabled.</param>
        private void HandleFirstPersonAnimator(bool DisableAnimator = false)
        {
            if(FirstPersonAnimator == null)
            {
                FirstPersonAnimator = gameObject.GetComponentInChildren<Animator>();
                if(FirstPersonAnimator == null) {
                    FPVLogger.Warn("First person animator could not be found.  Player animations may not play if you are in first person mode.");
                    return;
                }
            }
            if (FirstPersonAnimator != null)
            {
                if(DisableAnimator)
                {
                    FirstPersonAnimator.SetFloat(AnimatorMovementKey, 0f);
                    return;
                }

                FirstPersonAnimator.SetFloat(AnimatorMovementKey, IsPlayerMoving ? 1f : 0f);
            }
        }

        /// <summary>
        /// Handles first person view direction (where you are looking) while in first person.
        /// </summary>
        private void HandleFirstPersonLooking()
        {
            Vector2 looking = ManageControls.LookAction[Main.PlayerID].ReadValue<Vector2>();

            if(Main.FirstPersonCameraObject.nearClipPlane != 0.1f)
            {
                Main.FirstPersonCameraObject.nearClipPlane = 0.1f;
            }

            if (Main.FirstPersonCameraObject.farClipPlane != 3000f)
            {
                Main.FirstPersonCameraObject.farClipPlane = 3000f;
            }

            float LookSensitivity = PreferenceHandler.GetLookSensitivitySetting();
            float LookHorizontal = looking.x * LookSensitivity * Time.deltaTime;
            float LookVertical = looking.y * LookSensitivity * Time.deltaTime;

            CurrentLookVertical -= LookVertical;
            CurrentLookVertical = Mathf.Clamp((CurrentLookVertical - LookVertical), -90f, 90f);
            Main.FirstPersonCameraObject.transform.localRotation = Quaternion.Euler(CurrentLookVertical, 0f, 0f);
            Main.PlayerGameObject.transform.Rotate(Vector3.up * LookHorizontal);
        }

        /// <summary>
        /// Handles the apprearance of the Skybox in first person.  Without this, the skybox is always black.
        /// </summary>
        private void HandleFirstPersonSkybox()
        {

        }
        
        /// <summary>
        /// Handles where HoldPoints (the points in which you appear to hold items) are located while in first person.
        /// </summary>
        private void HandleFirstPersonHoldPoints()
        {
            if(Main.FirstPersonCameraObject.transform.Find(ITEM_HOLDPOINT_PATH) != null && !PreferenceHandler.GetFirstPersonStateSetting())
            {
                Main.FirstPersonCameraObject.transform.Find(ITEM_HOLDPOINT_PATH).rotation = Main.PlayerGameObject.transform.Find("HoldPoint").rotation;
                Main.FirstPersonCameraObject.transform.Find(ITEM_HOLDPOINT_PATH).position = Main.PlayerGameObject.transform.Find("HoldPoint").position;
            }
        }

        private void HandlePlayerModelVisibility()
        {
            if (Main.PlayerGameObject.transform.Find(PLAYER_MODEL_PATH) == null || Main.PlayerGameObject.transform.Find(COSMETICS_PATH) == null)
            {
                return;
            }

            if (ManageControls.WasBodyToggleKeyPressedThisFrame())
            {
                BodyState IntendedState = PreferenceHandler.GetBodyVisibilitySetting() == BodyState.Displayed ? BodyState.Hidden : BodyState.Displayed;
                PreferenceHandler.SetBodyVisibilitySetting(IntendedState);
            }

            bool ShouldPlayerModelBeVisible = (PreferenceHandler.GetBodyVisibilitySetting() == BodyState.Displayed ? true : false) || PreferenceHandler.GetFirstPersonStateSetting() == false;
            bool IsPlayerModelCurrentlyVisible = (Main.PlayerGameObject.transform.Find(PLAYER_MODEL_PATH).gameObject.activeSelf == true) || (Main.PlayerGameObject.transform.Find(COSMETICS_PATH).gameObject.activeSelf == true);
            bool IsPlayerCrane = CranePlayers.ToEntityArray(Allocator.Temp).Length != 0;
            bool MenuOrPopupVisible = (Data.IsInMenu == true || Data.IsShowingPopup == true);

            bool VisibilityMismatch =
                (!ShouldPlayerModelBeVisible && IsPlayerModelCurrentlyVisible) ||
                ((IsPlayerCrane || MenuOrPopupVisible || ShouldPlayerModelBeVisible) && !IsPlayerModelCurrentlyVisible);

            if (PreferenceHandler.GetFirstPersonStateSetting() && ShouldPlayerModelBeVisible == IsPlayerModelCurrentlyVisible && !ManageControls.WasBodyToggleKeyPressedThisFrame())
            {
                return;
            }

            FPVLogger.Debug("Bracketed values are required to HIDE the player model.");
            FPVLogger.Debug("(false) ShouldPlayerModelBeVisible: " + ShouldPlayerModelBeVisible);
            FPVLogger.Debug("(true)  IsPlayerModelCurrentlyVisible: " + IsPlayerModelCurrentlyVisible);
            FPVLogger.Debug("(false) IsPlayerCrane: " + IsPlayerCrane);
            FPVLogger.Debug("(false) MenuOrPopupVisible: " + MenuOrPopupVisible);
            FPVLogger.Debug("(true)  VisibilityMismatch: " + VisibilityMismatch);

            if (VisibilityMismatch)
            {
                FPVLogger.Info((ShouldPlayerModelBeVisible ? "Showing" : "Hiding" ) + " player model.");
                SetPlayerModelVisibilityGameObject(ShouldPlayerModelBeVisible);
            }
        }

        private void SetPlayerModelVisibilityGameObject(bool Visibility)
        {
            //FPVLogger.Debug("Setting player model visibility to " + Visibility);
            Main.PlayerGameObject.transform.Find(PLAYER_MODEL_PATH).gameObject.SetActive(Visibility);
            Main.PlayerGameObject.transform.Find(COSMETICS_PATH).gameObject.SetActive(Visibility);
        }

        private void SetupFirstPersonCamera()
        {
            FPVLogger.Debug("Setting up first person camera.");
            if (Main.FirstPersonCameraObject == null || Main.PlayerGameObject == null)
            {
                ResetFirstPersonCameraInstance();
            }

            CameraState IntendedState = CameraState.ThirdPerson;

            if(IsCameraFirstPerson)
            {
                IntendedState = CameraState.FirstPerson;    
            }

            SetCameraState(IntendedState);
            FPVLogger.Debug("Camera configuration complete.");
            CameraHasBeenSetup = true;
        }

        private void UpdateCameraPosition()
        {
            if (!CameraHasBeenSetup)
            {
                SetupFirstPersonCamera();
            }

            Main.FirstPersonCameraObject.transform.parent = Main.PlayerGameObject.transform;
            Vector3 PlayerPositionAndRotation = new Vector3(Main.PlayerGameObject.transform.position.x, Main.PlayerGameObject.transform.position.y + 1f, Main.PlayerGameObject.transform.position.z);
            Main.FirstPersonCameraObject.transform.SetPositionAndRotation(PlayerPositionAndRotation, Main.PlayerGameObject.transform.rotation);
        }

        private void ResetFirstPersonCameraInstance()
        {
            FPVLogger.Debug("Reinstancing First Person View objects.");
            if (Main.FirstPersonCameraObject == null)
            {
                FPVLogger.Debug("Reinstancing FirstPersonCameraObject.");
                Main.FirstPersonCameraObject = new GameObject("FPV Camera").AddComponent<Camera>();
            }

            if (Main.PlayerGameObject == null)
            {
                FPVLogger.Debug("Reinstancing FirstPersonPlayerGameObject.");
                Main.PlayerGameObject = FindPlayerGameObject();
            }
        }

        /// <summary>
        /// Locates and returns GameObject of the first local player it finds.  Local players are defined as players where <value>player.IsLocalPlayer</value> flat set.
        /// </summary>
        /// <returns>The GameObject owned by the first identified local player.</returns>
        private GameObject FindPlayerGameObject()
        {
            if(Data.PlayerID == 0)
            {
                return null;
            }

            // Reset this each time.
            Main.LocalPlayers = new Dictionary<PlayerInfo, int>();

            PlayerView[] PlayerViews = UnityEngine.Object.FindObjectsOfType<PlayerView>();
            foreach (PlayerInfo player in Players.Main.All())
            {
                if(player.IsLocalUser && !Main.LocalPlayers.ContainsKey(player))
                {
                    Main.LocalPlayers.Add(player, player.ID);
                }
            }

            if(Main.LocalPlayers.Count == 0)
            {
                return null;
            }

            foreach (KeyValuePair<PlayerInfo, int> player in Main.LocalPlayers)
            {
                foreach (PlayerView playerView in PlayerViews)
                {
                    object value = typeof(PlayerView).GetField("Data", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playerView);

                    if (((PlayerView.ViewData)value).PlayerID == player.Value)
                    {
                        Main.PlayerUsername = player.Key.Username;
                        Main.PlayerID = player.Key.ID;
                        Main.PlayerUsernameIDString = player.Key.Username + " (" + player.Key.ID + ")";
                        FPVLogger.Info("Found player gameObject with name " + Main.PlayerUsernameIDString + ".");

                        return playerView.gameObject;
                    }
                }
            }

            FPVLogger.Error("Game object could not be found.");
            return null;
        }

        /// <summary>
        /// Sets the camera perspective to the indicated state.
        /// </summary>
        /// <param name="IntendedCameraState">Intended camera state.</param>
        private void SetCameraState(CameraState IntendedCameraState)
        {
            int PlayerID = Main.PlayerID; 
            bool IsPlayerCrane = CranePlayers.ToEntityArray(Allocator.Temp).Length != 0;

            // Make it impossible to use first person if in crane mode.
            if (IsPlayerCrane)
            {
                FPVLogger.Debug("Player is crane, forcing state to false (third-person).");
                PreferenceHandler.SetFirstPersonStateSetting(CameraState.ThirdPerson);
                IntendedCameraState = CameraState.ThirdPerson;
            }

            if (Main.FirstPersonCameraObject == null)
            {
                SetupFirstPersonCamera();
            }

            if (IntendedCameraState == CameraState.FirstPerson)
            {
                FPVLogger.Info("Enabling first person camera for " + Main.PlayerUsernameIDString + ".");
                FPVLogger.Debug("<- Routing.");

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                // X = left/right
                // Y = up/down
                // Z = front/back
                Vector3 desiredHoldPointLocalPosition = new Vector3(0f, 0.5f, 0.436f);
                if(Main.PlayerGameObject.transform.Find(ITEM_HOLDPOINT_PATH) != null)
                {
                    Main.PlayerGameObject.transform.Find(ITEM_HOLDPOINT_PATH).localPosition = desiredHoldPointLocalPosition;
                }

                Main.FirstPersonCameraObject.gameObject.SetActive(true);
                IsCameraFirstPerson = true;
                FPVLogger.Info("Enabled first person camera.");
            }
            else
            {
                FPVLogger.Info("Disabling first person camera for  " + Main.PlayerUsernameIDString + ".");
                FPVLogger.Debug("<- Routing.");

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                Vector3 origLocalPos = new Vector3(0f, 1.158f, 0.336f);
                Quaternion origLocalRot = Quaternion.identity;
                if (Main.PlayerGameObject.transform.Find(ITEM_HOLDPOINT_PATH) != null)
                {
                    Main.PlayerGameObject.transform.Find(ITEM_HOLDPOINT_PATH).localPosition = origLocalPos;
                    Main.PlayerGameObject.transform.Find(ITEM_HOLDPOINT_PATH).localRotation = origLocalRot;
                }

                Main.FirstPersonCameraObject.gameObject.SetActive(false);
                IsCameraFirstPerson = false;
                FPVLogger.Info("Disabled first person camera.");
            }
        }
    }
}
