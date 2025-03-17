using Controllers;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FirstPersonView;
public class ManageControls
{
    // TODO: Figure why this is hard-coded.
    internal static KeyControl CameraToggleFirstPersonCameraKey = Keyboard.current.f5Key;
    internal static KeyControl BodyToggleFirstPersonCameraKey = Keyboard.current.f6Key;

    //internal static List<InputAction>[] movementAndLookActions = [];
    internal static Dictionary<int, List<InputAction>> movementAndLookActions = new Dictionary<int, List<InputAction>>();
    internal static Dictionary<int, InputAction> LookAction = new Dictionary<int, InputAction>();
    internal static Dictionary<int, InputAction> MoveAction = new Dictionary<int, InputAction>();
    internal static bool LocalControllerAssigned = false;
    private static bool AreControlsEnabled = false;

    /// <summary>
    /// Returns true if the first person mode key was pressed, and false if not.
    /// </summary>
    /// <returns>bool</returns>
    internal static bool WasCameraToggleKeyPressedThisFrame()
    {
        if(CameraToggleFirstPersonCameraKey.wasPressedThisFrame)
        {
            FPVLogger.Debug("Camera toggle key pressed.");
        }
        return CameraToggleFirstPersonCameraKey.wasPressedThisFrame;
    }

    /// <summary>
    /// Returns true if the body visibility toggle key was pressed, and false if not.
    /// </summary>
    /// <returns>bool</returns>
    internal static bool WasBodyToggleKeyPressedThisFrame()
    {
        if (BodyToggleFirstPersonCameraKey.wasPressedThisFrame && PreferenceHandler.GetFirstPersonStateSetting())
        {
            FPVLogger.Debug("Body toggle key pressed.");
        }
        return BodyToggleFirstPersonCameraKey.wasPressedThisFrame;
    }

    /// <summary>
    /// Gets the source identified as the player's input source.
    /// </summary>
    /// <returns>The identified SourceIdentifier.</returns>
    internal static SourceIdentifier GetMyControllerIdentifier()
    {
        return Main.ThisIsMyController;
    }

    /// <summary>
    /// Sets the input source identified during initial configuration.
    /// </summary>
    internal static void SetInitialInputSource()
    {
        FPVLogger.Debug("Setting initial input source to " + InputSourceIdentifier.Identifier.Value.ToString() + ".");
        Main.ThisIsMyController = InputSourceIdentifier.Identifier;
    }

    /// <summary>
    /// Configures the controls for the player.
    /// </summary>
    private static void ConfigureControls()
    {
        FPVLogger.Info("Setting up first person controls system for " + Main.PlayerUsernameIDString + ".");
        FPVLogger.Info("- Registering move and look input system.");

        foreach (var action in InputSystem.ListEnabledActions())
        {

            if (action.name == "Movement" || action.name == "Look")
            {
                if (movementAndLookActions == null)
                {
                    movementAndLookActions = new Dictionary<int, List<InputAction>>();
                }

                if (!movementAndLookActions.ContainsKey(Main.PlayerID))
                {
                    movementAndLookActions.Add(Main.PlayerID, new List<InputAction>());
                }

                if (!movementAndLookActions[Main.PlayerID].Contains(action))
                {
                    movementAndLookActions[Main.PlayerID].Add(action);
                    FPVLogger.Info("- Control registered for " + action.name + " on PlayerID " + Main.PlayerID + ".");
                }
            }
        }

        FPVLogger.Info("- Registered move and look input system.");
        FPVLogger.Info("- Registering move and look actions.");
        MoveAction[Main.PlayerID] = new InputAction("move", InputActionType.Value);
        LookAction[Main.PlayerID] = new InputAction("look", InputActionType.Value);

        bool WereControlsRegistered = false;

        if (InputSourceIdentifier.DefaultInputSource.GetCurrentController(Main.PlayerID) == ControllerType.Keyboard)
        {
            MoveAction[Main.PlayerID].AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            LookAction[Main.PlayerID].AddBinding("<Mouse>/delta");
            FPVLogger.Info("- Registered move and look actions for keyboard.");
            WereControlsRegistered = true;
        }
        else
        {
            MoveAction[Main.PlayerID].AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone(min=0.4,max=0.5)");
            LookAction[Main.PlayerID].AddBinding("<Gamepad>/rightStick")
                .WithProcessor("stickDeadzone(min=0.125,max=0.925)")
                .WithProcessor("scaleVector2(x=50,y=50)");
            FPVLogger.Info("- Registered move and look actions for controller.");
            WereControlsRegistered = true;
        }

        if (!WereControlsRegistered)
        {
            FPVLogger.Warn("- No control system was registered for first person mode!  You may not be able to control the character in first person if the cause is not identified.");
        }

        FPVLogger.Info("Completed control configuration for first person mode.");
    }

    /// <summary>
    /// Sets the state of the player's controls to the specified state.
    /// </summary>
    /// <param name="IntendedControlState"></param>
    internal static void SetControlState(ControlState IntendedControlState)
    {
        CheckControlState();
        if (IntendedControlState == ControlState.FirstPerson)
        {
            EnableFirstPersonControls();
            return;
        }

        DisableFirstPersonControls();
    }

    /// <summary>
    /// Determines whether the current control configuration is set to first person or third person.
    /// </summary>
    /// <returns>True if controls are in first person mode, false if not.</returns>
    internal static bool AreControlsSetToFirstPerson()
    {
        return AreControlsEnabled;
    }

    /// <summary>
    /// Checks if controls are null, and if not checks whether the player's control state has been configured.  If it is not configured, it handles configuration.
    /// </summary>
    internal static void CheckControlState()
    {
        if (MoveAction == null || LookAction == null || !MoveAction.ContainsKey(Main.PlayerID) || !LookAction.ContainsKey(Main.PlayerID))
        {
            ConfigureControls();
        }
    }

    /// <summary>
    /// Enables first person mode controls.
    /// </summary>
    private static void EnableFirstPersonControls()
    {
        FPVLogger.Info("Enabling first person controls for " + Main.PlayerUsernameIDString + ".");

        if(!MoveAction.ContainsKey(Main.PlayerID))
        {
            FPVLogger.Error("Controls not found!");
            return;
        }

        MoveAction[Main.PlayerID].Enable();
        LookAction[Main.PlayerID].Enable();
        foreach (var action in movementAndLookActions[Main.PlayerID])
        {
            action.Disable();
        }
        AreControlsEnabled = true;
        FPVLogger.Info("Enabled first person controls for " + Main.PlayerUsernameIDString + ".");
    }

    /// <summary>
    /// Disables first person mode controls.
    /// </summary>
    private static void DisableFirstPersonControls()
    {
        FPVLogger.Info("Disabling first person controls for " + Main.PlayerUsernameIDString + ".");
        MoveAction[Main.PlayerID].Disable();
        LookAction[Main.PlayerID].Disable();
        foreach (var action in movementAndLookActions[Main.PlayerID])
        {
            action.Enable();
        }
        AreControlsEnabled = false;
        FPVLogger.Info("Disabled first person controls for " + Main.PlayerUsernameIDString + ".");
    }
}