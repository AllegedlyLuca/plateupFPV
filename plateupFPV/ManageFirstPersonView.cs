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
        private bool CameraResetNotProcessed = false;
        private bool IsPlayerMoving = false;
        private bool FPVDisabledViaMenu = false;

        private bool ObjectNotFoundWarningShown = false;

        private float CurrentLookVertical = 0f;

        // TODO: Work out what this is for.
        private Material FirstPersonSkybox;

        [SerializeField]
        private Animator FirstPersonAnimator;
        private int AnimatorMovementKey = Animator.StringToHash("MovementSpeed");

        internal static EntityQuery Popups;

        private static QueryHelper PopupTypes = new QueryHelper().Any(typeof(CPopup), typeof(CGenericChoicePopup), typeof(CPopupRecipe));

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

            FPVLogger.Debug("Received UpdateData data: Main.PlayerID, data.PlayerID, data.IsInitialised: " + Main.PlayerID + ", " + data.PlayerID + ", " + data.IsInitialised);

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

            #region Enable or disable FPV
            if (Main.PlayerGameObject == null || Main.FirstPersonCameraObject == null)
            {
                if (Main.PlayerGameObject == null && !ObjectNotFoundWarningShown)
                {
                    FPVLogger.Warn("PlayerGameObject is not set.  Attempting to fix.");
                }

                if (Main.FirstPersonCameraObject == null && !ObjectNotFoundWarningShown)
                {
                    FPVLogger.Warn("FirstPersonCameraObject is not set.  Attempting to fix.");
                }

                ResetFirstPersonCameraInstance();
                if (Main.PlayerGameObject == null || Main.FirstPersonCameraObject == null)
                {
                    FPVLogger.Error("Attempt to reset missing object failed.");
                    FPVLogger.Error("This error will not be shown again until the missing object has been found.");
                    ObjectNotFoundWarningShown = true;
                    return;
                }
                ObjectNotFoundWarningShown = false;
                FPVLogger.Info("Successfully fixed objects not set.");
            }

            if (Main.LocalPlayers.Count > 1)
            {
                if (PreferenceHandler.GetFirstPersonStateSetting())
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

            if (FPVDisabledViaMenu && !IsMenuOrPopup) 
            {
                FPVLogger.Debug("Game has unpaused, returning to first person.");
                FPVDisabledViaMenu = false;
                EnableFirstPerson();
                return;
            }

            bool IsMyPlayerCraneBool = IsMyPlayerCrane();
            if(IsMyPlayerCraneBool == true)
            {
                if (PreferenceHandler.GetFirstPersonStateSetting() == true || IsCameraFirstPerson == true)
                {
                    FPVLogger.Debug("Player is a crane.  Disabling first person view.");
                    DisableFirstPerson();
                }
                return;
            }

            if (!IsMyPlayerCraneBool && ManageControls.WasCameraToggleKeyPressedThisFrame())
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

            if (!PreferenceHandler.GetFirstPersonStateSetting() && IsCameraFirstPerson)
            {
                FPVLogger.Debug("FPV is set to disabled but camera state does not match.  Correcting.");
                SetCameraState(CameraState.ThirdPerson);
                if (ManageControls.AreControlsSetToFirstPerson())
                {
                    ManageControls.SetControlState(ControlState.ThirdPerson);
                }
            }

            if (PreferenceHandler.GetFirstPersonStateSetting() && !IsCameraFirstPerson)
            {
                FPVLogger.Debug("FPV is set to enabled but camera state does not match.  Correcting.");
                SetCameraState(CameraState.FirstPerson);
                if (!ManageControls.AreControlsSetToFirstPerson())
                {
                    ManageControls.SetControlState(ControlState.FirstPerson);
                }
            }

            if (CameraResetNotProcessed == true)
            {
                if (PreferenceHandler.GetFirstPersonStateSetting())
                {
                    FPVLogger.Debug("Camera has been reset.  Setting calls to disable FPV.");
                    EnableFirstPerson();
                }
                else
                {
                    FPVLogger.Debug("Camera has been reset.  Setting calls to disable FPV.");
                    DisableFirstPerson();
                }
            }

            if (!PreferenceHandler.GetFirstPersonStateSetting() && !ManageControls.WasCameraToggleKeyPressedThisFrame())
            {
                return;
            }
            #endregion

            FirstPersonUpdate();
        }

        private void FirstPersonUpdate()
        {
            HandleFirstPersonFieldOfView();
            HandleFirstPersonLooking();
            HandleFirstPersonMovement();
            HandleFirstPersonAnimator();
            HandleFirstPersonHoldPoints();
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
            ManageControls.SetControlState(ControlState.FirstPerson);
            FirstPersonUpdate();
            SetSkybox(SkyboxState.FirstPersonSkybox);
            CameraResetNotProcessed = false;
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
            SetSkybox(SkyboxState.NativeSkybox);
            HandlePlayerModelVisibility();
            ManageControls.SetControlState(ControlState.ThirdPerson);
            CameraResetNotProcessed = false;
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

        // Code adapted from parts of code in https://github.com/quackandcheese/plateupFPV/blob/main/plateupFPV/SetFPV.cs
        private void SetSkybox(SkyboxState state)
        {
            if(Main.OriginalSkybox == null)
            {
                Main.OriginalSkybox = RenderSettings.skybox;
            }

            if (state == SkyboxState.NativeSkybox && Main.OriginalSkybox != null)
            {
                RenderSettings.skybox = Main.OriginalSkybox;
            }

            if(state == SkyboxState.FirstPersonSkybox)
            {
                FirstPersonSkybox = new Material(Shader.Find("Skybox/Procedural"));
                FirstPersonSkybox.SetColor("_SkyTint", new Color(1.5f, 0.5f, 1f));
                FirstPersonSkybox.SetFloat("_SunSize", 0.04f);
                FirstPersonSkybox.SetFloat("_AtmosphereThickness", 1f);
                FirstPersonSkybox = Resources.Load<Material>("Skybox/Blue Sky");
                RenderSettings.skybox = FirstPersonSkybox;
            }
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

        /// <summary>
        /// Handles the visibility of the player's model.
        /// </summary>
        /// <remarks>
        /// Player model visibility is determined by a number of different
        /// factors.  Primary amongst those is whether or not the model
        /// visibility setting is true or false.  If it is false, this means
        /// we should hide the player's model while the player is in first
        /// person mode.
        /// </remarks>
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
            bool IsPlayerCrane = IsMyPlayerCrane();
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

        /// <summary>
        /// Determines whether the player is a crane.
        /// </summary>
        /// <returns><i><b>bool</b></i>: true if crane, false otherwise.</returns>
        private bool IsMyPlayerCrane()
        {
            PlayerView playerView = GetLocalPlayerView();
            if (playerView.GetComponentInChildren<PlayerMovementComponent>().GetType() != typeof(Kitchen.PlayerCraneMovementComponent))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Handles changing of the player model's visibility.
        /// </summary>
        /// <param name="Visibility">true to display player model, false to hide player model.</param>
        private void SetPlayerModelVisibilityGameObject(bool Visibility)
        {
            //FPVLogger.Debug("Setting player model visibility to " + Visibility);
            Main.PlayerGameObject.transform.Find(PLAYER_MODEL_PATH).gameObject.SetActive(Visibility);
            Main.PlayerGameObject.transform.Find(COSMETICS_PATH).gameObject.SetActive(Visibility);
        }

        /// <summary>
        /// Resets the first person camera in the event that certain parameters are nulled.
        /// </summary>
        /// <remarks>
        /// In cases where the scene transitions between the main
        /// lobby (Kitchen.SceneType.Franchise) and a restaurant
        /// (Kitchen.SceneType.kitchen), this class may be reset
        /// or the class may lose track of the game objects.
        /// <para>
        /// If this happens, the game objects this mod relies on
        /// must be reset before we can do anything else.
        /// </para>
        /// </remarks>
        private void ResetFirstPersonCameraInstance()
        {
            if (Main.FirstPersonCameraObject == null)
            {
                FPVLogger.Debug("Reinstancing FirstPersonCameraObject.");
                Main.FirstPersonCameraObject = new GameObject("FPV Camera").AddComponent<Camera>();
                Main.FirstPersonCameraObject.clearFlags = CameraClearFlags.Skybox;
                if (PreferenceHandler.GetFirstPersonStateSetting())
                {
                    FPVLogger.Debug("Setting FirstPersonCameraObject to ACTIVE.");
                    Main.FirstPersonCameraObject.gameObject.SetActive(true);
                }
                else
                {
                    FPVLogger.Debug("Setting FirstPersonCameraObject to INACTIVE.");
                    Main.FirstPersonCameraObject.gameObject.SetActive(false);
                }
            }

            if (Main.PlayerGameObject == null)
            {
                if (!ObjectNotFoundWarningShown)
                {
                    FPVLogger.Debug("Reinstancing PlayerGameObject.");
                }
                Main.PlayerGameObject = GetLocalPlayerGameObject();
            }
            CameraResetNotProcessed = true;
        }

        /// <summary>
        /// Performs setup operations for the first person camera.
        /// </summary>
        /// <remarks>
        /// Before using the first person camera, it needs to be set up
        /// and its default state configured.  The default state can be
        /// either of first person or third person, depending on what
        /// the player's settings are at the time this method is called.
        /// </remarks>
        private void SetupFirstPersonCamera()
        {
            FPVLogger.Debug("Setting up first person camera.");
            if (Main.FirstPersonCameraObject == null || Main.PlayerGameObject == null)
            {
                ResetFirstPersonCameraInstance();
            }

            CameraState IntendedState = CameraState.ThirdPerson;

            if (IsCameraFirstPerson && !IsMyPlayerCrane())
            {
                IntendedState = CameraState.FirstPerson;
            }
            CameraHasBeenSetup = true;
            FPVLogger.Debug("Camera setup complete.");

            SetCameraState(IntendedState);
        }

        /// <summary>
        /// Updates the position of the first person camera.
        /// </summary>
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

        /// <summary>
        /// Returns the local player's view, or null if there are no local players.
        /// </summary>
        /// <returns><i><b>PlayerView</b></i>: The PlayerView object associated with the first local player found.</returns>
        private PlayerView GetLocalPlayerView()
        {
            if (Data.PlayerID == 0)
            {
                return null;
            }

            Main.LocalPlayers = new Dictionary<PlayerInfo, int>();
            foreach (PlayerInfo player in Players.Main.All())
            {
                if (player.IsLocalUser && !Main.LocalPlayers.ContainsKey(player))
                {
                    Main.LocalPlayers.Add(player, player.ID);
                }
            }

            if (Main.LocalPlayers.Count == 0)
            {
                return null;
            }

            PlayerView[] PlayerViews = UnityEngine.Object.FindObjectsOfType<PlayerView>();
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
                        return playerView;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Locates and returns GameObject of the first local player it finds.
        /// </summary>
        /// <remarks>
        /// Local players are defined as players where the <value>player.IsLocalPlayer</value> value
        /// has been set inside the <c>PlayerView</c> corresponding to that player.  This is critical to
        /// proper functioning of the mod, as it can only function on a local player.
        /// </remarks>
        /// <returns><i><b>GameObject</b></i>: The GameObject owned by the first identified local player.</returns>
        private GameObject GetLocalPlayerGameObject()
        {
            if (Data.PlayerID == 0)
            {
                return null;
            }

            PlayerView playerView = GetLocalPlayerView();
            if(playerView == null)
            {
                if (!ObjectNotFoundWarningShown)
                {
                    FPVLogger.Error("Game object could not be found.");
                }
                return null;
            }

            FPVLogger.Info("Found GameObject for player " + Main.PlayerUsernameIDString + ".");
            return playerView.gameObject;
        }

        /// <summary>
        /// Sets the camera perspective to the indicated state.
        /// </summary>
        /// <remarks>
        /// The camera state change consists of a number of smaller
        /// operations, primarily those of disabling the mouse cursor
        /// and activating the first person view.  This method handles
        /// all of these actions.
        /// </remarks>
        /// <param name="IntendedCameraState"><c>CameraState</c> The intended camera state.</param>
        private void SetCameraState(CameraState IntendedCameraState)
        {
            if (Main.FirstPersonCameraObject == null)
            {
                FPVLogger.Debug("Trying to set camera state but camera object is null.");
                SetupFirstPersonCamera();
            }

            if(Main.FirstPersonCameraObject != null && !CameraHasBeenSetup)
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
