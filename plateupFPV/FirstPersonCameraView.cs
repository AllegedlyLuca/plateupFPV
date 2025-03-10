using Controllers;
using Kitchen;
using KitchenLib.Preferences;
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
        private const string PLAYER_MODEL_PATH_NONPLUS = "MorphmanPlus";
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

        public class UpdateView : ResponsiveViewSystemBase<ViewData, ResponseData>, IModSystem
        {
            public static UpdateView Instance { get; private set; }

            EntityQuery Query;
            List<int> LocalInputSources;

            protected override void Initialise()
            {
                base.Initialise();
                Query = GetEntityQuery(typeof(CLinkedView), typeof(CFirstPersonPlayer));
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

                    PreferenceInt FPVEnabledPrefInt = Main.PrefManager.GetPreference<PreferenceInt>(Main.FPV_ENABLED_ID);
                    bool IsActive = FPVEnabledPrefInt.Get() == 1;
                    CFirstPersonPlayer cFirstPersonPlayer = FirstPersonPlayerComponents[i];
                    cFirstPersonPlayer.IsActive = IsActive;
                    Set(ents[i], cFirstPersonPlayer);

                    SendUpdate(LinkedViews[i], new ViewData { 
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

        private EntityQuery PlayersWithoutCraneMode;
        // This runs locally for each client every frame
        public void Update()
        {
            if(Data == null)
                return;

            if (Data.Source != InputSourceIdentifier.Identifier)
                return;

            if (FirstPersonCameraGameObject == null)
            {
                FPVLogger.DebugLog("FirstPersonCameraGameObject is null, reinstancing.");
                ResetFirstPersonCameraInstance();
                SetCameraToFirstPerson(false, Data.PlayerID);
            }

            // TOOD: Prevent changing into first-person if in crane mode, OR force crane mode to non-crane mode before transitioning.

            // Toggle Active in preferences
            if (ToggleFirstPersonCameraKey.wasPressedThisFrame)
            {
                PreferenceInt FPVEnabledPrefInt = Main.PrefManager.GetPreference<PreferenceInt>(Main.FPV_ENABLED_ID);
                FPVEnabledPrefInt.Set(FPVEnabledPrefInt.Get() == 0 ? 1 : 0);
                Main.PrefManager.Save();
            }

            //
            PreferenceInt playerModelVisibilityPreference = Main.PrefManager.GetPreference<PreferenceInt>(Main.PLAYER_MODEL_VISIBLE_ID);
            
            if (playerModelVisibilityPreference == null)
                return;

            int playerModelVisibility = playerModelVisibilityPreference.Get();

            // TODO: Fix hiding/showing of player model when toggling first/third person.
            //if(transform.Find(PLAYER_MODEL_PATH) == null)
            //{
            //    FPVLogger.DebugLog("Transform PLAYER_MODEL_PATH null.  Should not be null.");
            //    if(transform.Find(PLAYER_MODEL_PATH_NONPLUS) == null)
            //    {
            //        FPVLogger.DebugLog("Transform PLAYER_MODEL_PATH_NONPLUS null too.");
            //    }
            //    return;
            //}

            //if (!Data.IsActive || Data.IsInMenu)
            //{
            //    transform.Find(PLAYER_MODEL_PATH).gameObject.SetActive(true);
            //    transform.Find(COSMETICS_PATH).gameObject.SetActive(true);
            //    return;
            //}

            // Player Model Visibility
            //transform.Find(PLAYER_MODEL_PATH).gameObject.SetActive(!(playerModelVisibility == 0));
            //transform.Find(COSMETICS_PATH).gameObject.SetActive(!(playerModelVisibility == 0));

            // Field of View
            PreferenceInt FieldOfViewPreference = Main.PrefManager.GetPreference<PreferenceInt>(Main.FOV_ID);
            int FieldOfView = FieldOfViewPreference.Get();

            Camera FirstPersonViewCamera = FirstPersonCameraGameObject.GetComponent<Camera>();
            if (FirstPersonViewCamera != null)
            {
                FirstPersonViewCamera.fieldOfView = FieldOfView;
            }

            // Movement
            float moveSpeed = 3000f;
            CheckControls(Data.PlayerID);
            Vector2 movementDir = MoveAction.ReadValue<Vector2>().normalized;
            Vector3 move = transform.right * movementDir.x + transform.forward * movementDir.y;
            GetComponent<Rigidbody>().AddForce(move * moveSpeed * Data.Speed * Time.deltaTime);

            // Looking
            Vector2 looking = LookAction.ReadValue<Vector2>();
            PreferenceFloat sensitivityFloat = Main.PrefManager.GetPreference<PreferenceFloat>(Main.SENSITIVITY_ID);
            float lookSensitivity = sensitivityFloat.Get();
            float lookX = looking.x * lookSensitivity * Time.deltaTime;
            float lookY = looking.y * lookSensitivity * Time.deltaTime;

            xRotation -= lookY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            FirstPersonCameraGameObject.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * lookX);

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
                if(Callback != null)
                {
                    FPVLogger.DebugLog("Initialising camera.");
                    Callback.Invoke(new ResponseData
                    {
                        IsInitialised = true,
                        IsActive = data.IsActive,
                        Source = data.Source,
                    }, typeof(ResponseData));
                    FPVLogger.DebugLog("Initialisation of camera complete.");
                }

                // Camera Setup
                SetupFirstPersonCamera();
                CheckControls(data.PlayerID);
            }

            // Anything below here requires the camera gameobject to not be null to be activated
            if (FirstPersonCameraGameObject == null)
                return;

            if (data.IsActive)
            {
                SetCameraToFirstPerson(true, data.PlayerID);
            }

            if (!data.IsActive || data.IsInMenu)
            {
                SetCameraToFirstPerson(false, data.PlayerID);
            }
        }

        private void CheckControls(int PlayerID)
        {
            if(MoveAction == null || MoveAction.type == null)
            {
                ConfigureControls(PlayerID);
                SetupFirstPersonCamera();
            }
        }

        private void ConfigureControls(int PlayerID)
        {
            foreach (var action in InputSystem.ListEnabledActions())
            {
                if ( (action.name == "Movement" || action.name == "Look") && !movementAndLookActions.Contains(action) )
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

        private void  SetupFirstPersonCamera()
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

        private void SetCameraToFirstPerson(bool state, int PlayerID)
        {
            CheckControls(PlayerID);
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
                while(Main.FPVCounter != 0)
                {
                    FPVLogger.DebugLog((state ? "Enabling" : "Disabling") + " first-person perspective state.");
                    MoveAction.Disable();
                    LookAction.Disable();
                    foreach (var action in movementAndLookActions)
                    {
                        action.Enable();
                    }

                    Cursor.lockState = CursorLockMode.None;

                    Vector3 origLocalPos = new Vector3(0f, 1.158f, 0.336f);
                    transform.Find(ITEM_HOLDPOINT_PATH).localPosition = origLocalPos;

                    Quaternion origLocalRot = Quaternion.identity;
                    transform.Find(ITEM_HOLDPOINT_PATH).localRotation = origLocalRot;
                    FPVLogger.DebugLog("View loop count-: " + Main.FPVCounter);
                    Main.FPVCounter--;
                    FPVLogger.DebugLog((state ? "Enabled" : "Disabled") + " first-person perspective state.");
                }
            }

            Cursor.visible = !state;
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
