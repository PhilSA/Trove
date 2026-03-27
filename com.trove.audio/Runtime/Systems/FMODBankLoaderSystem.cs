using Unity.Entities;
using Unity.Collections;

namespace DOTSFMOD
{
    /// <summary>
    /// Loads and unloads FMOD banks in response to FMODBankLoadRequest/FMODBankUnloadRequest tags.
    /// Replaces StudioBankLoader MonoBehaviour.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class FMODBankLoaderSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<FMODBankLoaderComponent>();
        }

        protected override void OnUpdate()
        {
            if (!FMODUnity.RuntimeManager.IsInitialized)
                return;

            ProcessLoadRequests();
            ProcessUnloadRequests();
        }

        private void ProcessLoadRequests()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (loader, bankBuffer, entity)
                in SystemAPI.Query<RefRW<FMODBankLoaderComponent>, DynamicBuffer<FMODBankReferenceElement>>()
                    .WithAll<FMODBankLoadRequest>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<FMODBankLoadRequest>(entity, false);

                ref var loaderData = ref loader.ValueRW;
                if (loaderData.IsLoaded)
                    continue;

                for (int i = 0; i < bankBuffer.Length; i++)
                {
                    string bankName = bankBuffer[i].BankName.ToString();
                    try
                    {
                        FMODUnity.RuntimeManager.LoadBank(bankName, loaderData.PreloadSamples);
                    }
                    catch (FMODUnity.BankLoadException e)
                    {
                        UnityEngine.Debug.LogException(e);
                    }
                }

                if (loaderData.PreloadSamples)
                {
                    FMODUnity.RuntimeManager.WaitForAllSampleLoading();
                }

                loaderData.IsLoaded = true;
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ProcessUnloadRequests()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (loader, bankBuffer, entity)
                in SystemAPI.Query<RefRW<FMODBankLoaderComponent>, DynamicBuffer<FMODBankReferenceElement>>()
                    .WithAll<FMODBankUnloadRequest>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<FMODBankUnloadRequest>(entity, false);

                ref var loaderData = ref loader.ValueRW;
                if (!loaderData.IsLoaded)
                    continue;

                for (int i = 0; i < bankBuffer.Length; i++)
                {
                    string bankName = bankBuffer[i].BankName.ToString();
                    FMODUnity.RuntimeManager.UnloadBank(bankName);
                }

                loaderData.IsLoaded = false;
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
