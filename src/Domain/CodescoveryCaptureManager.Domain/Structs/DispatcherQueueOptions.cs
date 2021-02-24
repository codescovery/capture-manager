using CodescoveryCaptureManager.Domain.Enums;

namespace CodescoveryCaptureManager.Domain.Structs
{
    public struct DispatcherQueueOptions
    {
        public int DwSize;
        public DispatcherqueueThreadType ThreadType;
        public DispatcherqueueThreadApartmentType ApartmentType;
    }
}