using System.Threading.Tasks;
using Xunit;

//Arrange -> Act -> Assert 
public class CliTests
{
    [Fact]
    public async Task ScanWithConflictOpts_ReturnsError()
    {
        string[] args =
        {
            "scan", "--all", "--repo", "owner/repo"
        };
        int exitCode = await VSCli.Main(args);
        Assert.Equal(1,exitCode);
    }
    [Fact]
    public async Task LoginWithCredentials()
    {
        string[] args =
        {
            "auth", "login"
        };
        int exitCode = await VSCli.Main(args);
        Assert.Equal(0,exitCode);
    }

    

}