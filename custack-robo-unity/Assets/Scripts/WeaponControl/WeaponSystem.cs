using UnityEngine;

public class WeaponSystem : MonoBehaviour
{
    [SerializeField]
    protected float _reloadTime = 1.0f;

    protected float _lastTime = 0.0f;

    protected GameObject _parentObject = null;
    protected GameObject _targetObject = null;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        var targets = GameObject.FindGameObjectsWithTag("Player");
        foreach (var target in targets)
        {
            if (gameObject.transform.IsChildOf(target.transform))
            {
                _parentObject = target;
                break;
            }
        }
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        // ターゲット更新
        var targets = GameObject.FindGameObjectsWithTag("Player");
        var minDistance = Mathf.Infinity;
        foreach (var target in targets)
        {
            if (_parentObject == target)
            {
                continue;
            }

            var distance = Vector3.Distance(
                transform.position,
                target.transform.position
            );

            if (distance < minDistance)
            {
                minDistance = distance;
                _targetObject = target;
            }
        }

    }


    public virtual void Play(Transform parentTransform=null)
    {

    }

    public virtual bool isPlaying()
    {
        return true;
    }
}
