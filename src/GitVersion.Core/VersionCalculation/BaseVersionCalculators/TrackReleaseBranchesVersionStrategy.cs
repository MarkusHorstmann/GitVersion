using System;
using System.Collections.Generic;
using System.Linq;
using GitVersion.Common;
using GitVersion.Configuration;
using GitVersion.Model.Configuration;

namespace GitVersion.VersionCalculation
{
    /// <summary>
    /// Active only when the branch is marked as IsDevelop.
    /// Two different algorithms (results are merged):
    /// <para>
    /// Using <see cref="VersionInBranchNameVersionStrategy"/>:
    /// Version is that of any child branches marked with IsReleaseBranch (except if they have no commits of their own).
    /// BaseVersionSource is the commit where the child branch was created.
    /// Always increments.
    /// </para>
    /// <para>
    /// Using <see cref="TaggedCommitVersionStrategy"/>:
    /// Version is extracted from all tags on the <c>main</c> branch which are valid.
    /// BaseVersionSource is the tag's commit (same as base strategy).
    /// Increments if the tag is not the current commit (same as base strategy).
    /// </para>
    /// </summary>
    public class TrackReleaseBranchesVersionStrategy : VersionStrategyBase
    {
        private readonly IRepositoryStore repositoryStore;
        private readonly VersionInBranchNameVersionStrategy releaseVersionStrategy;
        private readonly TaggedCommitVersionStrategy taggedCommitVersionStrategy;

        public TrackReleaseBranchesVersionStrategy(IRepositoryStore repositoryStore, Lazy<GitVersionContext> versionContext)
            : base(versionContext)
        {
            this.repositoryStore = repositoryStore ?? throw new ArgumentNullException(nameof(repositoryStore));

            releaseVersionStrategy = new VersionInBranchNameVersionStrategy(repositoryStore, versionContext);
            taggedCommitVersionStrategy = new TaggedCommitVersionStrategy(repositoryStore, versionContext);
        }

        public override IEnumerable<BaseVersion> GetVersions()
        {
            if (Context.Configuration.TracksReleaseBranches)
            {
                return ReleaseBranchBaseVersions().Union(MainTagsVersions());
            }

            return new BaseVersion[0];
        }

        private IEnumerable<BaseVersion> MainTagsVersions()
        {
            var main = repositoryStore.FindBranch(Config.MainBranchKey);
            return main != null ? taggedCommitVersionStrategy.GetTaggedVersions(main, null) : new BaseVersion[0];
        }



        private IEnumerable<BaseVersion> ReleaseBranchBaseVersions()
        {
            var releaseBranchConfig = Context.FullConfiguration.GetReleaseBranchConfig();
            if (releaseBranchConfig.Any())
            {
                var releaseBranches = repositoryStore.GetReleaseBranches(releaseBranchConfig);

                return releaseBranches
                    .SelectMany(b => GetReleaseVersion(Context, b))
                    .Select(baseVersion =>
                    {
                        // Need to drop branch overrides and give a bit more context about
                        // where this version came from
                        var source1 = "Release branch exists -> " + baseVersion.Source;
                        return new BaseVersion(source1,
                            baseVersion.ShouldIncrement,
                            baseVersion.SemanticVersion,
                            baseVersion.BaseVersionSource,
                            null);
                    })
                    .ToList();
            }
            return new BaseVersion[0];
        }

        private IEnumerable<BaseVersion> GetReleaseVersion(GitVersionContext context, IBranch releaseBranch)
        {
            var tagPrefixRegex = context.Configuration.GitTagPrefix;

            // Find the commit where the child branch was created.
            var baseSource = repositoryStore.FindMergeBase(releaseBranch, context.CurrentBranch);
            if (Equals(baseSource, context.CurrentCommit))
            {
                // Ignore the branch if it has no commits.
                return new BaseVersion[0];
            }

            return releaseVersionStrategy
                .GetVersions(tagPrefixRegex, releaseBranch)
                .Select(b => new BaseVersion(b.Source, true, b.SemanticVersion, baseSource, b.BranchNameOverride));
        }
    }
}
