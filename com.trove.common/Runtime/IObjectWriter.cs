using Unity.Entities;

namespace Trove
{
    public unsafe interface IObjectByteWriter
    {
        public int GetByteSize();
        public void Write(byte* ptr);
    }
}