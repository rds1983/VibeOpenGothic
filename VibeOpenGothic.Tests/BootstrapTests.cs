namespace VibeOpenGothic.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public void GameAssembly_NameMatchesBootstrapInfo()
    {
        var assemblyName = typeof(BootstrapInfo).Assembly.GetName().Name;

        Assert.Equal(BootstrapInfo.Name, assemblyName);
    }

    [Fact]
    public void RequiredPackageAssemblies_AreAvailable()
    {
        var types = new[]
        {
            typeof(Microsoft.Xna.Framework.Vector3),
            typeof(ZenKit.Vfs),
            typeof(BepuPhysics.Simulation),
            typeof(DigitalRiseModel.DrModel),
            typeof(Myra.Graphics2D.UI.Desktop),
        };

        Assert.All(types, type => Assert.False(string.IsNullOrWhiteSpace(type.Assembly.GetName().Name)));
    }
}
