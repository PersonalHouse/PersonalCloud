namespace NSPersonalCloud.Interfaces.Cloud
{
    /// <summary>
    /// Required info on a Personal Cloud.
    /// </summary>
    public interface ICloud
    {
        string Id { get; }

        string DisplayName { get; set; }
    }
}
