using UnityEngine;
using System;
using UnityEngine.InputSystem;
using VContainer;

public class CharacterInputController : MonoBehaviour
{
    private InputSystem_Actions _inputActions;
    private IControllable _controllable;

    [Inject]
    public void Construct(InputSystem_Actions inputActions, IControllable controllable)
    {
        _inputActions = inputActions;
        _controllable = controllable;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (_controllable == null)
        {
            throw new Exception($"IControllable not exist in {gameObject.name}");
        }

        _inputActions.Enable();
        _inputActions.Player.Jump.performed += JumpOnPerformed;

        Debug.Log(this + " is active");
    }

    private void OnDestroy()
    {
        if (_inputActions != null)
        {
            _inputActions.Player.Jump.performed -= JumpOnPerformed;
            _inputActions.Disable();
        }
    }

    private void JumpOnPerformed(InputAction.CallbackContext context)
    {
        _controllable.Jump();
    }

    private void Update()
    {
        ReadMove();
    }

    private void ReadMove()
    {
        var moveDirection = _inputActions.Player.Move.ReadValue<Vector2>();
        

        Vector3 direction = new Vector3(moveDirection.x, 0f, moveDirection.y);
        
        _controllable.Move(direction);
    }
}