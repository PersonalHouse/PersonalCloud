using NSPersonalCloud.Interfaces.Errors;

namespace NSPersonalCloud.Interfaces
{
    public static class PersonalCloudExceptions
    {
        public static DeviceNotFoundException ProviderNotFound => new DeviceNotFoundException();
        public static DeviceInternalErrorException DeviceError => new DeviceInternalErrorException();
        public static CannotMapDeviceException CannotCreateTopFolder => new CannotMapDeviceException();
        public static InvalidParametersException InvalidParamter => new InvalidParametersException();
        public static PortsInUseException NotEnoughPort => new PortsInUseException();
        public static HashCollisionException HashCollision => new HashCollisionException();
        public static NoDeviceResponseException NoDiscoverableNode => new NoDeviceResponseException();
        public static InvalidDeviceResponseException DiscoverableNodeHttpError => new InvalidDeviceResponseException();
        public static InviteNotAcceptedException CodeMismatch => new InviteNotAcceptedException();
    }
}
