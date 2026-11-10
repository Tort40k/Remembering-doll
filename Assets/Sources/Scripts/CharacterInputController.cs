using UnityEngine;
using System;
public class CharacterInputController : MonoBehaviour
{
    private IControllable _controllable;

    private void Awake()
    {
        _controllable = GetComponent<IControllable>();

        if(_controllable == null)
        {
            throw new Exception("IContollable no exist in {gameobject.name}");
        }
    }

    private void Update()
    {
        ReadMove();
        ReadJump();
    }

    private void ReadMove()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 direction = new Vector3(horizontal, 0f, vertical);
        _controllable.Move(direction);
    }

    private void ReadJump()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _controllable.Jump();
        }
    }
}
