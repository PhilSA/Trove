using FMODUnity;
using Unity.Entities;
using Unity.Collections;

namespace Trove.Audio.FMOD
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(FMODSingletonSystem))]
    public partial class FMODBankLoaderSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RuntimeUtils.EnforceLibraryOrder();
            RequireForUpdate<FMODBankLoaderComponent>();
        }

        protected override void OnUpdate()
        {
            if (!FMODUnity.RuntimeManager.IsInitialized)
                return;

            // Load
            foreach (var (loader, bankBuffer, bankLoadRequest)
                     in SystemAPI
                         .Query<RefRW<FMODBankLoaderComponent>, DynamicBuffer<FMODBankElement>,
                             EnabledRefRW<FMODBankLoadRequest>>()
                         .WithAll<FMODBankLoadRequest>())
            {
                bankLoadRequest.ValueRW = false;

                for (int i = 0; i < bankBuffer.Length; i++)
                {
                    FMODBankElement bank = bankBuffer[i];

                    try
                    {
                        RuntimeManager.LoadBank(bank.BankName.ConvertToString(), loader.ValueRO.PreloadSamples);
                    }
                    catch (BankLoadException e)
                    {
                        RuntimeUtils.DebugLogException(e);
                    }
                }

                if (loader.ValueRO.PreloadSamples)
                {
                    RuntimeManager.WaitForAllSampleLoading();
                }
            }
            
            // Unload
            foreach (var (loader, bankBuffer, unloadRequest)
                     in SystemAPI.Query<RefRW<FMODBankLoaderComponent>, DynamicBuffer<FMODBankElement>,
                    EnabledRefRW<FMODBankLoadRequest>>()
                         .WithAll<FMODBankUnloadRequest>())
            {
                unloadRequest.ValueRW = false;

                for (int i = 0; i < bankBuffer.Length; i++)
                {
                    FMODBankElement bank = bankBuffer[i];
                    RuntimeManager.UnloadBank(bank.BankName.ConvertToString());
                }
            }
        }
    }
}
