namespace PhotonBypass.Test.Initializer.OutSourceManager;

internal interface IOutSourceInitializer
{
    Task Initialize(string key);

    Task Check(string key);

    Task Clear(string key);
}