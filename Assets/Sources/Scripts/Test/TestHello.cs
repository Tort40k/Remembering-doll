using UnityEngine;
using VContainer;

public class TestHello : MonoBehaviour
{
    private string _message;

    [Inject]
    public void Construct(string message)
    {
        _message = message;
        print(_message);
        // Debug.Log(_message);
    }
}