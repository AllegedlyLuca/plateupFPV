using Controllers;
using Kitchen;
using Kitchen.NetworkSupport;
using KitchenMods;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace KitchenFirstPersonView;
public class ManageControls
{
    // TODO: Figure why this is hard-coded.
    internal static KeyControl CameraToggleFirstPersonCameraKey = Keyboard.current.f5Key;
    internal static KeyControl BodyToggleFirstPersonCameraKey = Keyboard.current.f6Key;

    //internal static List<InputAction>[] movementAndLookActions = [];
    internal static Dictionary<Int32, List<InputAction>> movementAndLookActions = new Dictionary<Int32, List<InputAction>>();
    internal static Dictionary<Int32, InputAction> LookAction = new Dictionary<int, InputAction>();
    internal static Dictionary<Int32, InputAction> MoveAction = new Dictionary<int, InputAction>();
    internal static bool LocalControllerAssigned = false;
    private static bool AreControlsEnabled = false;

    internal static bool WasCameraToggleKeyPressedThisFrame()
    {
        if(CameraToggleFirstPersonCameraKey.wasPressedThisFrame)
        {
            FPVLogger.Debug("Camera toggle key pressed.");
        }
        return CameraToggleFirstPersonCameraKey.wasPressedThisFrame;
    }

    internal static bool WasBodyToggleKeyPressedThisFrame()
    {
        if (BodyToggleFirstPersonCameraKey.wasPressedThisFrame && PreferenceHandler.GetFirstPersonStateSetting())
        {
            FPVLogger.Debug("Body toggle key pressed.");
        }
        return BodyToggleFirstPersonCameraKey.wasPressedThisFrame;
    }

    internal static SourceIdentifier GetMyControllerIdentifier()
    {
        return Main.ThisIsMyController;
    }

    internal static void SetInitialInputSource()
    {
        FPVLogger.Debug("Setting initial input source to " + InputSourceIdentifier.Identifier.Value.ToString() + ".");
        Main.ThisIsMyController = InputSourceIdentifier.Identifier;
    }

    // TODO: Establish separate query for crane players.
    //internal static CraneState GetPlayerCraneState()
    //{
    //    QueryHelper CranePlayerTypes = new QueryHelper().All(typeof(CIsCraneMode));
    //    //EntityQuery CranePlayers = GetEntityQuery(CranePlayerTypes);

    //    return CraneState.NotCrane;
    //}

    private static void ConfigureControls(int PlayerID)
    {
        FPVLogger.Info("Setting up first person controls system for PlayerID " + PlayerID.ToString() + ".");
        //AssignLocalController();
        FPVLogger.Info("- Registering move and look input system.");

        foreach (var action in InputSystem.ListEnabledActions())
        {

            if (action.name == "Movement" || action.name == "Look")
            {
                if(movementAndLookActions == null)
                {
                    movementAndLookActions = new Dictionary<Int32, List<InputAction>>();
                }

                if (!movementAndLookActions.ContainsKey(PlayerID))
                {
                    movementAndLookActions.Add(PlayerID, new List<InputAction>());
                }

                if (!movementAndLookActions[PlayerID].Contains(action))
                {
                    movementAndLookActions[PlayerID].Add(action);
                    FPVLogger.Info("- Control registered for " + action.name + " on PlayerID " + PlayerID + ".");
                }
            }
        }

        FPVLogger.Info("- Registered move and look input system.");
        FPVLogger.Info("- Registering move and look actions.");
        MoveAction[PlayerID] = new InputAction("move", InputActionType.Value);
        LookAction[PlayerID] = new InputAction("look", InputActionType.Value);

        bool WereControlsRegistered = false;

        if (InputSourceIdentifier.DefaultInputSource.GetCurrentController(PlayerID) == ControllerType.Keyboard)
        {
            MoveAction[PlayerID].AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            LookAction[PlayerID].AddBinding("<Mouse>/delta");
            FPVLogger.Info("- Registered move and look actions for keyboard.");
            WereControlsRegistered = true;
        }
        else
        {
            MoveAction[PlayerID].AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone(min=0.4,max=0.5)");
            LookAction[PlayerID].AddBinding("<Gamepad>/rightStick")
                .WithProcessor("stickDeadzone(min=0.125,max=0.925)")
                .WithProcessor("scaleVector2(x=50,y=50)");
            FPVLogger.Info("- Registered move and look actions for controller.");
            WereControlsRegistered = true;
        }

        if(!WereControlsRegistered)
        {
            FPVLogger.Warn("- No control system was registered for first person mode!  You may not be able to control the character in first person if the cause is not identified.");
        }

        FPVLogger.Info("Completed control configuration for first person mode.");
    }

    internal static bool AreControlsSetToFirstPerson()
    {
        return AreControlsEnabled;
    }

    internal static void SetControlState(CameraState IntendedControlState)
    {
        CheckControlState();
        if (IntendedControlState == CameraState.FirstPerson)
        {
            EnableFirstPersonControls();
            return;
        }

        DisableFirstPersonControls();
    }

    internal static void CheckControlState()
    {
        if (MoveAction == null || LookAction == null || !MoveAction.ContainsKey(Main.PlayerID) || !LookAction.ContainsKey(Main.PlayerID))
        {
            ConfigureControls(Main.PlayerID);
        }
    }

    private static void EnableFirstPersonControls()
    {
        FPVLogger.Info("Enabling first person controls for " + Main.PlayerID + " (" + Main.PlayerUsername + ").");

        if(!MoveAction.ContainsKey(Main.PlayerID))
        {
            FPVLogger.Debug("Controls not found for " + Main.PlayerID);
        }

        MoveAction[Main.PlayerID].Enable();
        LookAction[Main.PlayerID].Enable();
        foreach (var action in movementAndLookActions[Main.PlayerID])
        {
            action.Disable();
        }
        AreControlsEnabled = true;
        FPVLogger.Info("Enabled first person controls for " + Main.PlayerID + ".");
    }

    private static void DisableFirstPersonControls()
    {
        FPVLogger.Info("Disabling first person controls for " + Main.PlayerID + ".");
        MoveAction[Main.PlayerID].Disable();
        LookAction[Main.PlayerID].Disable();
        foreach (var action in movementAndLookActions[Main.PlayerID])
        {
            action.Enable();
        }
        AreControlsEnabled = false;
        FPVLogger.Info("Disabled first person controls for " + Main.PlayerID + ".");
    }
}