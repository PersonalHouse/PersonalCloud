namespace NSPersonalCloud.Interfaces.FileSystem
{
    /// <summary>
    /// An access-controlled the generic file system, designed for easy sharing over the network.
    /// </summary>
    public interface IFileSystem : IReadableFileSystem, IWritableFileSystem { }
}
