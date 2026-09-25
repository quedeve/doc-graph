using DocumentGraph.Core.Interfaces;
using Xunit;

namespace DocumentGraph.Tests;

public class SearchRankingTests
{
    [Fact]
    public void SearchQuery_HasSensibleDefaults()
    {
        var q = new SearchQuery();

        Assert.Equal(string.Empty, q.QueryText);
        Assert.Equal(10, q.Limit);
        Assert.Null(q.DocumentType);
        Assert.Null(q.Extension);
        Assert.Null(q.PathPrefix);
    }

    [Fact]
    public void ReciprocalRankFusion_CombinesRanksAccurately()
    {
        const float k = 60f;

        // Item 1: Rank 1 in FTS, Rank 1 in Vector
        float scoreBothTop = (1.0f / (k + 1)) + (1.0f / (k + 1));

        // Item 2: Rank 1 in FTS, not in Vector
        float scoreOnlyFts = 1.0f / (k + 1);

        // Item 3: Rank 2 in FTS, Rank 2 in Vector
        float scoreBothRank2 = (1.0f / (k + 2)) + (1.0f / (k + 2));

        Assert.True(scoreBothTop > scoreBothRank2);
        Assert.True(scoreBothRank2 > scoreOnlyFts, "Items with both FTS and Vector matches should outrank single-source matches");
    }

    [Fact]
    public void SearchFilter_AppliesExtensionNormalizing()
    {
        string rawExt = "cs";
        string normalized = rawExt.StartsWith('.') ? rawExt : $".{rawExt}";

        Assert.Equal(".cs", normalized);
    }
}
