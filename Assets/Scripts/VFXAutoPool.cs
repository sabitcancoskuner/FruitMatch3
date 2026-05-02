using UnityEngine;

public class VFXAutoPool : MonoBehaviour
{
    private void OnParticleSystemStopped() 
    {
        ObjectPoolManager.ReturnObjectToPool(this.gameObject, PoolType.VFX);    
    }
}
