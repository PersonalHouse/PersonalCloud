namespace NSPersonalCloud.Interfaces.FileSystem
{
    public interface ILocalFileSystem : IFileSystem
    {
        string RootPath { get; }
    }
}
