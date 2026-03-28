
using Unity.Entities;
using FMODUnity;

namespace Trove.Audio.FMOD
{
    public class StudioListenerBaker : Baker<StudioListener>
    {
        public override void Bake(StudioListener authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // Get private authoring field(s) by reflection :(
            bool nonRigidbodyVelocity = (bool)(typeof(StudioListener).GetField("nonRigidbodyVelocity", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(authoring));

            AddComponent(entity, new FMODListener
            {
                ListenerIndex = -1,
                AttenuationEntity = authoring.AttenuationObject != null ? GetEntity(authoring.AttenuationObject, TransformUsageFlags.None) : Entity.Null,
                
                AttenuationPosition = authoring.AttenuationObject != null ? authoring.AttenuationObject.transform.position : default,
                PreviousPosition = authoring.transform.position,
            });
            
            AddComponent(entity, new FMODListenerUseNonRigidbodyVelocity());
            SetComponentEnabled<FMODListenerUseNonRigidbodyVelocity>(entity, nonRigidbodyVelocity);
        }
    }
}
