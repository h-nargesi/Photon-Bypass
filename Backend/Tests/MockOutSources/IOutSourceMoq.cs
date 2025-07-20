namespace PhotonBypass.Test.MockOutSources;

interface IDataSource
{
    string FilePath { get; set; }
}

interface IOutSourceMoq
{
    IOutSourceMoq Setup(IDataSource source);
}
