namespace Riel.Graphics.MapView
{
    public interface IXnaMapShellSession : IMapShellSession
    {
        new IXnaMapShellHost ShellHost { get; }
    }
}
