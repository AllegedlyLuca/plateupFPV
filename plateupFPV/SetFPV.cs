/*
 This is the original code from https://github.com/SpiffySnail/plateupFPV
 */
/*using Kitchen;
using KitchenMods;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.InputSystem.Controls;
using System.Linq;
using Kitchen.Modules;
using System.Collections.Generic;
using UnityEngine.Assertions.Must;
using Controllers;
using Kitchen.Layouts.Modules;
using Kitchen.Components;

namespace FirstPersonView
{
    public class SetFPV : GenericSystemBase, IModSystem
    {
        private const string ITEM_HOLDPOINT_PATH = "MorphmanPlus/Hold Points/Item Hold Point";
        
        bool isFPV = false;
        bool isPopup = false;
        bool isInitialised = false;

        private float lookUpMax = 45f;
        private float lookUpMin = -45f;

        GameObject player;

        InputAction f1Action;
        InputAction lftStick;
        InputAction rgtStick;
        InputAction wAction;
        InputAction aAction;
        InputAction sAction;
        InputAction dAction;
        InputAction moveAction;
        InputAction lookAction;
        Camera fpvCamera;
        Camera topDownCamera;

        Material originalSkybox;
        Material skyboxMaterial;

        Vector3 originalHoldPointLocalPosition;

        List<InputAction> movementAndLookActions = new List<InputAction>();

        protected override void Initialise()
        {
            foreach (var action in InputSystem.ListEnabledActions())
            {
                if (action.name == "Movement" || action.name == "Look")
                {
                    movementAndLookActions.Add(action);
                }
            }
            f1Action = new InputAction("f3", binding: "<Keyboard>/f3");
            f1Action.performed += ctx =>
            {
                if (!isFPV)
                {
                    EnableFPV();
                    isFPV = true;
                }
                else
                {
                    DisableFPV();
                    isFPV = false;
                }
            };
            f1Action.Enable();
        }
        private void EnableFPV()
        {
            player = GameObject.Find("Player(Clone)");

            if (!isInitialised)
            {
                isInitialised = true;

                GameObject cameraObject = new GameObject("FPV Camera");
                fpvCamera = cameraObject.AddComponent<Camera>();
                fpvCamera.transform.localPosition = new Vector3(0, 1f, 0);
                fpvCamera.transform.parent = player.transform;
                fpvCamera.transform.localRotation = Quaternion.Euler(0, 0, 0);
                Vector3 pos = new Vector3(player.transform.position.x, player.transform.position.y + 1f, player.transform.position.z);
                fpvCamera.transform.SetPositionAndRotation(pos, player.transform.rotation);
                fpvCamera.fieldOfView = 75;
                fpvCamera.nearClipPlane = 0.3f;
                fpvCamera.clearFlags = CameraClearFlags.Skybox;
                fpvCamera.backgroundColor = new Color(0.5f, 0.5f, 1f);
                fpvCamera.farClipPlane = 3000f;

                skyboxMaterial = new Material(Shader.Find("Skybox/Procedural"));
                skyboxMaterial.SetColor("_SkyTint", new Color(0.5f, 0.5f, 1f));
                skyboxMaterial.SetFloat("_SunSize", 0.04f);
                skyboxMaterial.SetFloat("_AtmosphereThickness", 1f);
                skyboxMaterial = Resources.Load<Material>("Skybox/Blue Sky");

                originalSkybox = RenderSettings.skybox;
                originalHoldPointLocalPosition = player.transform.Find(ITEM_HOLDPOINT_PATH).localPosition;

                wAction = new InputAction("w", binding: "<Keyboard>/w");
                aAction = new InputAction("a", binding: "<Keyboard>/a");
                sAction = new InputAction("s", binding: "<Keyboard>/s");
                dAction = new InputAction("d", binding: "<Keyboard>/d");



                moveAction = new InputAction("move", binding: "<Gamepad>/leftStick", processors: "stickDeadzone(min=0.125,max=0.925)");
                moveAction.AddCompositeBinding("Dpad")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");


                lookAction = new InputAction("look", binding: "<Mouse>/delta");
                
                rgtStick = new InputAction("RightStick", binding: "<Gamepad>/rightStick");
            }

            fpvCamera.enabled = true;
            RenderSettings.skybox = skyboxMaterial;

            topDownCamera = Camera.main;
            topDownCamera.transform.localPosition = new Vector3(0, 10f, 0);
            topDownCamera.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Vector3 pos2 = new Vector3(player.transform.position.x, player.transform.position.y + 10f, 0);
            Vector3 rot = new Vector3(90, 0, 0);
            topDownCamera.transform.SetPositionAndRotation(pos2, Quaternion.Euler(rot));
            topDownCamera.fieldOfView = 60;
            topDownCamera.nearClipPlane = 0.3f;
            topDownCamera.rect = new Rect(0.75f, 0.75f, 0.333f, 0.333f);
            topDownCamera.clearFlags = CameraClearFlags.Skybox;
            topDownCamera.backgroundColor = new Color(0.5f, 0.5f, 1f);
            topDownCamera.farClipPlane = 3000f;
            topDownCamera.depth = 1;


            foreach (var action in movementAndLookActions)
            {
                action.Disable();
            }
            
            moveAction.Enable();
            lookAction.Enable();
            rgtStick.Enable();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector3 desiredHoldPointPosition = new Vector3(0f, 0.65f, 0.55f);
            player.transform.Find(ITEM_HOLDPOINT_PATH).localPosition = desiredHoldPointPosition;
        }

        private void DisableFPV()
        {
            if (isInitialised)
            {
                topDownCamera.rect = new Rect(0, 0, 1, 1);
                fpvCamera.enabled = false;
                
                moveAction.Disable();
                lookAction.Disable();
                rgtStick.Disable();

                RenderSettings.skybox = originalSkybox;

                foreach (var action in movementAndLookActions)
                {
                    action.Enable();
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;


                player.transform.Find(ITEM_HOLDPOINT_PATH).localPosition = originalHoldPointLocalPosition;
            }
        }
        
        private Vector3 GetMovementVector()
        {
            Vector2 movementDir = moveAction.ReadValue<Vector2>();


            return movementDir.normalized;
        }

        protected override void OnUpdate()
        {
            if (fpvCamera != null)
            {
                float moveSpeed = 60f;
                Vector2 movementDir = GetMovementVector();
                Vector3 move = player.transform.right * movementDir.x + player.transform.forward * movementDir.y;
                player.GetComponent<Rigidbody>().AddForce(move * moveSpeed * UnityEngine.Time.deltaTime, ForceMode.VelocityChange);

                if (rgtStick.ReadValue<Vector2>().x != 0 || rgtStick.ReadValue<Vector2>().y != 0)
                {
                    float x = rgtStick.ReadValue<Vector2>().x * 2f;
                    float y = rgtStick.ReadValue<Vector2>().y * -2f;
                    player.transform.Rotate(new Vector3(0, x, 0));
                    fpvCamera.transform.Rotate(new Vector3(y, 0, 0));
                }

                Vector2 mouseMove = lookAction.ReadValue<Vector2>();
                float mouseX = mouseMove.x / 4;
                float mouseY = (mouseMove.y / 8) * -1;


                player.transform.Rotate(new Vector3(0, mouseX, 0));

                fpvCamera.transform.Rotate(new Vector3(mouseY, 0, 0));

            }
            GameObject generic = GameObject.Find("Generic Choice Popup(Clone)");
            GameObject pause = GameObject.Find("Player Pause Popup");
            if (pause.transform.GetChild(0).gameObject.activeSelf)
            {
                isPopup = true;
                DisableFPV();

            }
            else
            {
                isPopup = false;
            }
            if (!isPopup && isFPV)
            {
                if (topDownCamera.rect == new Rect(0, 0, 1, 1))
                {
                    EnableFPV();
                }
            }
        }
    }
}
*/