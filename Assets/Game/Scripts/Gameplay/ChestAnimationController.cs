using Game.Scripts.UI.Chest;
using UnityEngine;

public class ChestAnimationController : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private Chest _chest;

    private void OnEnable()
    {
        _chest.OnShow += OpenChest;
    }

    private void OnDisable()
    {
        _chest.OnShow -= OpenChest;
        _chest.Presenter.OnHide -= CloseChest;
    }

    private void CloseChest()
    {
        _animator.Play("Idle");
        _chest.Presenter.OnHide -= CloseChest;
    }

    private void OpenChest()
    {
        _animator.Play("OpenChest");
        _chest.Presenter.OnHide += CloseChest;
    }
}