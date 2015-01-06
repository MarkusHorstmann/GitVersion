using GitVersion;
using LibGit2Sharp;
using NUnit.Framework;

namespace GitVersionCore.Tests.IntegrationTests.GitHubFlow
{
    [TestFixture]
    public class FeatureBranchTests
    {
        [Test]
        public void ShouldNotUseNumberInFeatureBranchAsPreReleaseNumber()
        {
            using (var fixture = new EmptyRepositoryFixture(new Config()))
            {
                fixture.Repository.MakeATaggedCommit("1.0.0");
                fixture.Repository.CreateBranch("feature/JIRA-123");
                fixture.Repository.Checkout("feature/JIRA-123");
                fixture.Repository.MakeCommits(5);

                fixture.AssertFullSemver("1.0.1-feature.JIRA.123+5");
            }
        }    
    }
}