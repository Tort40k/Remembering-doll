using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Character : MonoBehaviour
{
    [SerializeField] private CharacterController controller;
    [SerializeField] private float _gravity = -9.81f;
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _jumpHeight = 2f;
    [SerializeField] private float _groundRadiusChecker = 0.2f;
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private Transform _groundChecker;

    private bool isGround;
    private float _velocityY;
    private Vector3 _moveDirection;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        isGround = IsOnTheGround();
        if (isGround && _velocityY < 0)
        {
            _velocityY = -2f; 
        }

        _moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));

        if (Input.GetKeyDown(KeyCode.Space) && isGround)
        {
            print("I`m jumping!");
            Jump();
        }

        DoGravity();
        Move(_moveDirection);
    }

    private void Move(Vector3 direction)
    {
        Vector3 finalMove = (direction * _speed) + (Vector3.up * _velocityY);
        controller.Move(finalMove * Time.deltaTime);
    }

    private void Jump()
    {
        _velocityY = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
    }

    private bool IsOnTheGround()
    {
        return Physics.CheckSphere(_groundChecker.position, _groundRadiusChecker, _groundMask);
    }

    private void OnDrawGizmosSelected()
    {
        if (_groundChecker == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_groundChecker.position, _groundRadiusChecker);
    }

    private void DoGravity()
    {
        _velocityY += _gravity * Time.deltaTime;
    }
}