using System.Collections.Generic;
using System.Linq;
using HdbscanSharp.Distance;
using HdbscanSharp.Runner;
using Xunit;

namespace Tests;

public class ClusterSelectionMethodTests
{
    [Fact]
    public void DefaultBehaviorMatchesExplicitEom()
    {
        var dataset = GetHierarchicalDataset();
        var distance = GenericEuclideanDistance.GetFunc(dataset);

        var defaultResult = HdbscanRunner.Run(dataset.Length, 3, 3, distance);
        var explicitEomResult = HdbscanRunner.Run(
            dataset.Length,
            3,
            3,
            distance,
            clusterSelectionMethod: ClusterSelectionMethod.Eom);

        Assert.Equal(defaultResult.Labels, explicitEomResult.Labels);
    }

    [Fact]
    public void LeafReturnsValidClusteringAndAtLeastAsManyClustersAsEom()
    {
        var dataset = GetHierarchicalDataset();
        var distance = GenericEuclideanDistance.GetFunc(dataset);

        var eomResult = HdbscanRunner.Run(
            dataset.Length,
            3,
            3,
            distance,
            clusterSelectionMethod: ClusterSelectionMethod.Eom);

        var leafResult = HdbscanRunner.Run(
            dataset.Length,
            3,
            3,
            distance,
            clusterSelectionMethod: ClusterSelectionMethod.Leaf);

        Assert.Equal(dataset.Length, leafResult.Labels.Length);
        Assert.True(leafResult.Labels.All(label => label >= 0));

        var eomClusterCount = eomResult.Labels.Where(x => x > 0).Distinct().Count();
        var leafClusterCount = leafResult.Labels.Where(x => x > 0).Distinct().Count();
        Assert.True(leafClusterCount >= eomClusterCount);
    }

    [Fact]
    public void GroupsAreConsistentWithLabelsForEomAndLeafAndNoiseSemanticsStayValid()
    {
        var points = GetPointDataset();
        var vectors = points.Select(point => new[] { point.X, point.Y }).ToArray();
        var distance = GenericEuclideanDistance.GetFunc(vectors);

        var eomLabelsResult = HdbscanRunner.Run(
            vectors.Length,
            3,
            3,
            distance,
            clusterSelectionMethod: ClusterSelectionMethod.Eom);
        var eomGroupsResult = HdbscanRunner.Run(
            points,
            point => new[] { point.X, point.Y },
            3,
            3,
            GenericEuclideanDistance.GetFunc,
            clusterSelectionMethod: ClusterSelectionMethod.Eom);

        var leafLabelsResult = HdbscanRunner.Run(
            vectors.Length,
            3,
            3,
            distance,
            clusterSelectionMethod: ClusterSelectionMethod.Leaf);
        var leafGroupsResult = HdbscanRunner.Run(
            points,
            point => new[] { point.X, point.Y },
            3,
            3,
            GenericEuclideanDistance.GetFunc,
            clusterSelectionMethod: ClusterSelectionMethod.Leaf);

        Assert.Contains(0, eomLabelsResult.Labels);
        Assert.Contains(0, leafLabelsResult.Labels);

        AssertGroupsMatchLabels(points, eomLabelsResult.Labels, eomGroupsResult.Groups);
        AssertGroupsMatchLabels(points, leafLabelsResult.Labels, leafGroupsResult.Groups);
    }

    private static void AssertGroupsMatchLabels(
        IReadOnlyList<(double X, double Y)> points,
        IReadOnlyList<int> labels,
        IReadOnlyDictionary<int, List<(double X, double Y)>> groups)
    {
        var expected = labels
            .Select((label, index) => (label, point: points[index]))
            .GroupBy(x => x.label)
            .ToDictionary(x => x.Key, x => x.Select(y => y.point).OrderBy(p => p.X).ThenBy(p => p.Y).ToArray());

        Assert.Equal(expected.Keys.OrderBy(x => x), groups.Keys.OrderBy(x => x));

        foreach (var key in expected.Keys)
        {
            var actual = groups[key].OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
            Assert.Equal(expected[key], actual);
        }
    }

    private static double[][] GetHierarchicalDataset()
    {
        return
        [
            [0.0, 0.0], [0.1, 0.0], [0.0, 0.1], [0.1, 0.1],
            [0.0, 5.0], [0.1, 5.0], [0.0, 5.1], [0.1, 5.1],
            [20.0, 20.0], [20.1, 20.0], [20.0, 20.1], [20.1, 20.1], [20.2, 20.1],
            [100.0, 100.0], [-100.0, -100.0]
        ];
    }

    private static List<(double X, double Y)> GetPointDataset()
    {
        return GetHierarchicalDataset().Select(x => (x[0], x[1])).ToList();
    }
}
