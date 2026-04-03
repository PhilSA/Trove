
using Unity.Entities;
using FMODUnity;

namespace Trove.Audio.FMOD
{
    public class StudioListenerBaker : Baker<StudioListener>
    {
        public override void Bake(StudioListener authoring)
        {
            if (authoring.gameObject.GetComponents<StudioListener>().Length > 1)
            {
                UnityEngine.Debug.LogError("Cannot have more than one StudioListener component on the same GameObject");
                return;
            }
            
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // Get private authoring field(s) by reflection :(
            bool nonRigidbodyVelocity = (bool)(typeof(StudioListener).GetField("nonRigidbodyVelocity", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(authoring));

            AddComponent(entity, new FMODListener
            {
                ListenerIndex = -1,
                AttenuationEntity = authoring.AttenuationObject != null ? 
                    GetEntity(authoring.AttenuationObject, TransformUsageFlags.Dynamic) : Entity.Null,
                NonRigidbodyVelocity = nonRigidbodyVelocity,
                
                AttenuationPosition = authoring.AttenuationObject != null ? 
                    authoring.AttenuationObject.transform.position : default, 
                PreviousPosition = authoring.transform.position,
            });
        }
    }
}
